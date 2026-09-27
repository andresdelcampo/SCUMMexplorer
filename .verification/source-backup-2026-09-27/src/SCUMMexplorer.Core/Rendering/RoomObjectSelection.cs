namespace SCUMMexplorer.Core.Rendering;

public readonly record struct RoomObjectPosition(int X, int Y);

/// <summary>
/// Holds each object's state and current position. Script operations can set a
/// state directly or draw after moving and clearing exact-bounds peers. Viewer
/// clicks inspect those peers in directory order, then state zero.
/// </summary>
public sealed class RoomObjectSelection
{
    private readonly IReadOnlyList<RoomObjectImage> _objects;
    private readonly Dictionary<int, byte> _states;
    private readonly HashSet<int> _knownStates = [];
    private readonly Dictionary<int, RoomObjectPosition> _positions;
    private readonly HashSet<int> _backgroundPossible = [];
    private readonly IReadOnlyDictionary<int, LoomRoomObjectRecord>? _recordsById;
    private readonly IReadOnlyDictionary<int, LoomRoomObjectRecord>? _recordsByLocalIndex;
    private readonly RoomImage? _background;

    public RoomObjectSelection(IReadOnlyList<RoomObjectImage> objects,
        IReadOnlyDictionary<int, byte>? initialStates = null,
        IReadOnlyDictionary<int, RoomObjectPosition>? initialPositions = null,
        IReadOnlyList<LoomRoomObjectRecord>? roomRecords = null,
        RoomImage? background = null,
        IReadOnlyDictionary<int, byte>? indexedStates = null)
    {
        _objects = objects;
        _background = background;
        if (roomRecords is not null)
        {
            _recordsById = roomRecords.ToDictionary(record => record.Id);
            _recordsByLocalIndex = roomRecords.ToDictionary(record => record.LocalIndex);
        }
        _positions = objects.ToDictionary(obj => obj.Id, obj => new RoomObjectPosition(obj.X, obj.Y));
        if (initialPositions is not null)
            foreach (var (id, position) in initialPositions)
                if (_positions.ContainsKey(id)) _positions[id] = position;
        _states = objects.ToDictionary(obj => obj.Id, obj =>
            initialStates is null ? (byte)0 : initialStates.TryGetValue(obj.Id, out var state)
                ? state : throw new InvalidDataException($"Object {obj.Id} is absent from the global state table."));
        _knownStates.UnionWith(_states.Keys);
        // The room code directory also contains hitboxes and parent objects
        // without their own image. Parent-state requirements apply to those
        // records too, so keep their known state available to the chain check.
        if (roomRecords is not null)
            foreach (var record in roomRecords)
                if (!_states.ContainsKey(record.Id))
                {
                    var known = initialStates is null ||
                                initialStates.TryGetValue(record.Id, out _);
                    _states[record.Id] = initialStates is not null &&
                        initialStates.TryGetValue(record.Id, out var recordState) ? recordState : (byte)0;
                    if (known) _knownStates.Add(record.Id);
                }
        foreach (var obj in objects)
        {
            RememberBackgroundIfEmpty(obj);
            if (indexedStates is not null && Peers(obj).All(peer =>
                    indexedStates.TryGetValue(peer.Id, out var state) && (state & 0x0F) == 0))
                foreach (var peer in Peers(obj)) _backgroundPossible.Add(peer.Id);
        }
    }

    public bool IsActive(RoomObjectImage obj)
    {
        return _states.TryGetValue(obj.Id, out var state) && (state & 0x0F) != 0 &&
               ParentStateMatches(obj);
    }

    private bool ParentStateMatches(RoomObjectImage obj)
    {
        if (_recordsById is null || _recordsByLocalIndex is null) return true;
        if (!_recordsById.TryGetValue(obj.Id, out var record)) return false;
        var remaining = _recordsByLocalIndex.Count;
        while (record.ParentLocalIndex != 0)
        {
            if (--remaining < 0 || !_recordsByLocalIndex.TryGetValue(record.ParentLocalIndex, out var parent) ||
                !_knownStates.Contains(parent.Id) || !_states.TryGetValue(parent.Id, out var parentState) ||
                (parentState & 0x0F) != record.ParentRequiredState)
                return false;
            record = parent;
        }
        return true;
    }

    public bool HasAlternatives(RoomObjectImage obj) => Peers(obj).Count(ParentStateMatches) > 1;

    public bool CanShowBackground(RoomObjectImage obj) =>
        _backgroundPossible.Contains(obj.Id) && HasBackgroundArtwork(obj);

    /// <summary>
    /// Supplies a reproducible artwork estimate where a partial script run
    /// leaves no image. Existing script-visible images always take precedence.
    /// This is a viewer fallback, not a SCUMM state transition.
    /// </summary>
    public IReadOnlyList<int> ShowUnresolvedArtwork()
    {
        var added = new List<int>();
        foreach (var obj in _objects)
        {
            if (IsActive(obj) || _objects.Any(other => IsActive(other) && Intersects(obj, other)))
                continue;
            var peers = Peers(obj);
            if (peers.Any(peer => IsActive(peer))) continue;
            _states[obj.Id] = 1;
            if (IsActive(obj)) added.Add(obj.Id);
            else _states[obj.Id] = 0;
        }
        return added;
    }

    public RoomObjectPosition GetPosition(RoomObjectImage obj) => _positions[obj.Id];

    public bool SetState(int objectId, byte state)
    {
        if (!_states.ContainsKey(objectId)) return false;
        _states[objectId] = state;
        _knownStates.Add(objectId);
        var obj = _objects.FirstOrDefault(item => item.Id == objectId);
        if (obj is not null) RememberBackgroundIfEmpty(obj);
        return true;
    }

    public bool DrawObject(int objectId, int? x = null, int? y = null, byte state = 1)
    {
        var obj = _objects.FirstOrDefault(item => item.Id == objectId);
        if (obj is null) return false;
        if (x.HasValue != y.HasValue)
            throw new ArgumentException("An object move requires both coordinates.");
        if (x.HasValue) _positions[objectId] = new RoomObjectPosition(x.Value, y!.Value);
        // SCUMM compares the requested object's current rectangle against all
        // local objects after applying any position change.
        foreach (var peer in Peers(obj)) _states[peer.Id] = 0;
        _states[objectId] = state;
        return true;
    }

    public bool Select(RoomObjectImage obj) => DrawObject(obj.Id);

    public RoomObjectImage? HitTest(int x, int y)
    {
        // Old SCUMM draws local objects in reverse directory order, so the
        // first matching image is the topmost one at this pixel.
        var visible = _objects.FirstOrDefault(obj => IsActive(obj) && Contains(obj, x, y));
        if (visible is not null) return visible;

        // When the background shows through, the first stored image at that
        // rectangle can still be reached by clicking its recorded bounds.
        return _objects.FirstOrDefault(obj => ParentStateMatches(obj) && Contains(obj, x, y));
    }

    public RoomObjectImage? Next(RoomObjectImage current)
    {
        if (!_states.ContainsKey(current.Id)) return null;
        var peers = Peers(current).Where(ParentStateMatches).ToList();
        if (peers.Count == 0) return null;
        if (!IsActive(current))
        {
            Select(peers[0]);
            return peers[0];
        }

        var index = peers.FindIndex(obj => obj.Id == current.Id);
        if (index + 1 == peers.Count)
        {
            if (CanShowBackground(current))
            {
                foreach (var peer in peers) _states[peer.Id] = 0;
                return null;
            }
            Select(peers[0]);
            return peers[0];
        }

        Select(peers[index + 1]);
        return peers[index + 1];
    }

    private List<RoomObjectImage> Peers(RoomObjectImage obj) =>
        _objects.Where(item => GetPosition(item) == GetPosition(obj) &&
                               item.Width == obj.Width && item.Height == obj.Height).ToList();

    private void RememberBackgroundIfEmpty(RoomObjectImage obj)
    {
        var peers = Peers(obj);
        if (peers.All(peer => (_states[peer.Id] & 0x0F) == 0))
            foreach (var peer in peers) _backgroundPossible.Add(peer.Id);
    }

    private bool Contains(RoomObjectImage obj, int x, int y)
    {
        var position = GetPosition(obj);
        return x >= position.X && x < position.X + obj.Width &&
               y >= position.Y && y < position.Y + obj.Height;
    }

    private bool Intersects(RoomObjectImage first, RoomObjectImage second)
    {
        var a = GetPosition(first);
        var b = GetPosition(second);
        return a.X < b.X + second.Width && b.X < a.X + first.Width &&
               a.Y < b.Y + second.Height && b.Y < a.Y + first.Height;
    }

    private bool HasBackgroundArtwork(RoomObjectImage obj)
    {
        if (_background is null) return true;
        var position = GetPosition(obj);
        var left = Math.Max(0, position.X);
        var right = Math.Min(_background.Width, position.X + obj.Width);
        var top = Math.Max(0, position.Y);
        var bottom = Math.Min(_background.Height, position.Y + obj.Height);
        var pixels = _background.Pixels.Span;
        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
                if (pixels[y * _background.Width + x] != 0) return true;
        return false;
    }
}

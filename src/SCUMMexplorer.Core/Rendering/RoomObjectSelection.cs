using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Rendering;

public readonly record struct RoomObjectPosition(int X, int Y);

/// <summary>One <c>drawObject</c> call found in stored script code; X/Y are room pixels when the call moves the object.</summary>
public readonly record struct RoomDrawStep(int ObjectId, int? X = null, int? Y = null);

/// <summary>
/// A run of <c>drawObject</c> calls that a script makes within one frame.
/// Higher priority batches carry stronger first-visit evidence, for example
/// the frame drawn right after a script loads the room.
/// </summary>
public sealed record RoomDrawBatch(IReadOnlyList<RoomDrawStep> Steps, int Priority, string Source);

/// <summary>An object state that static script evidence establishes, with that evidence.</summary>
public readonly record struct RoomStateEvidence(byte State, string Source);

/// <summary>An image shown to close a gap that the reached script state left open.</summary>
public sealed record RoomArtworkEstimate(int ObjectId, string Reason);

/// <summary>
/// A stored image rectangle that currently shows solid colour 0 while its
/// surroundings do not. <see cref="Fillable"/> is false when no image stored
/// for it has enough non-black pixels to close it.
/// </summary>
public readonly record struct RoomGap(int X, int Y, int Width, int Height, bool Fillable);

/// <summary>
/// Holds each object's state, current position and paint order. SCUMM
/// repaints active objects in reverse room-directory order whenever the room is
/// redrawn, then paints queued <c>drawObject</c> calls on top in call order.
/// <c>setState</c> causes a redraw and clears that queue. Viewer clicks act as
/// <c>drawObject</c> calls.
/// </summary>
public sealed class RoomObjectSelection
{
    private const double GapInsideBlack = 0.9;
    private const double GapRingBlack = 0.6;
    private const double CandidateMaxBlack = 0.6;
    private const double CutoutBlack = 0.9;

    private readonly IReadOnlyList<RoomObjectImage> _objects;
    private readonly Dictionary<int, byte> _states;
    private readonly HashSet<int> _knownStates = [];
    private readonly Dictionary<int, RoomObjectPosition> _positions;
    private readonly HashSet<int> _backgroundPossible = [];
    private readonly HashSet<int> _indexedBackground = [];
    private readonly IReadOnlyDictionary<int, LoomRoomObjectRecord>? _recordsById;
    private readonly IReadOnlyDictionary<int, LoomRoomObjectRecord>? _recordsByLocalIndex;
    private readonly RoomImage? _background;
    private readonly List<int> _drawQueue = [];
    private readonly Dictionary<int, double> _blackFractions = [];
    private readonly HashSet<int> _interfaceImages = [];
    private IReadOnlyList<RoomDrawBatch> _batches = [];
    private int _seed;

    public RoomObjectSelection(IReadOnlyList<RoomObjectImage> objects,
        IReadOnlyDictionary<int, byte>? initialStates = null,
        IReadOnlyDictionary<int, RoomObjectPosition>? initialPositions = null,
        IReadOnlyList<LoomRoomObjectRecord>? roomRecords = null,
        RoomImage? background = null,
        IReadOnlyDictionary<int, byte>? indexedStates = null,
        IReadOnlyList<int>? drawQueue = null)
    {
        _objects = objects;
        _background = background is { IsValid: true } ? background : null;
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
        if (drawQueue is not null)
            foreach (var id in drawQueue)
                if (_positions.ContainsKey(id)) Enqueue(id);
        foreach (var obj in objects)
        {
            RememberBackgroundIfEmpty(obj);
            if (indexedStates is not null && Peers(obj).All(peer =>
                    indexedStates.TryGetValue(peer.Id, out var state) && (state & 0x0F) == 0))
                foreach (var peer in Peers(obj))
                {
                    _backgroundPossible.Add(peer.Id);
                    _indexedBackground.Add(peer.Id);
                }
        }
        // The images the reached states draw: only these change the depth mask. Images shown later by the artwork
        // estimates (or by the viewer) fill gaps in the picture; they are not what the game drew.
        _reachedActive.UnionWith(objects.Where(IsActive).Select(obj => obj.Id));
    }

    private readonly HashSet<int> _reachedActive = [];

    /// <summary>Objects drawn since the last redraw, in call order.</summary>
    public IReadOnlyList<int> DrawQueue => _drawQueue;

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

    /// <summary>
    /// Marks images that decoded code shows only in the verb area below the
    /// room (see <c>LoomSceneDrawBatches.InterfaceImages</c>). They are never
    /// used to estimate room artwork, their rectangles are not room gaps, and
    /// a click on empty background does not reach them. Reached state still
    /// paints them, as SCUMM's room redraw would.
    /// </summary>
    public void ExcludeInterfaceImages(IEnumerable<int> objectIds) => _interfaceImages.UnionWith(objectIds);

    private bool IsRoomArtwork(RoomObjectImage obj) => !_interfaceImages.Contains(obj.Id) && ParentStateMatches(obj);

    public bool HasAlternatives(RoomObjectImage obj) => Peers(obj).Count(ParentStateMatches) > 1;

    /// <summary>
    /// Hiding is offered when the room background behind the image has no
    /// cut-out gap. Without a decoded background, the index must show that
    /// the position can be empty.
    /// </summary>
    public bool CanShowBackground(RoomObjectImage obj) =>
        _background is null ? _backgroundPossible.Contains(obj.Id) : !IsBackgroundGap(obj);

    /// <summary>
    /// Active images from bottom to top: a directory-order redraw of every
    /// active image, then queued <c>drawObject</c> calls in call order.
    /// </summary>
    public IReadOnlyList<RoomObjectImage> PaintOrder()
    {
        var byId = _objects.GroupBy(obj => obj.Id).ToDictionary(group => group.Key, group => group.First());
        var queued = new List<RoomObjectImage>();
        var seen = new HashSet<int>();
        for (var index = _drawQueue.Count - 1; index >= 0; index--)
        {
            var id = _drawQueue[index];
            if (!seen.Add(id) || !byId.TryGetValue(id, out var obj) || !IsActive(obj)) continue;
            queued.Insert(0, obj);
        }
        var order = new List<RoomObjectImage>();
        // Old SCUMM paints local objects from the end of the room directory
        // toward the start, so an earlier directory entry covers a later one.
        for (var index = _objects.Count - 1; index >= 0; index--)
        {
            var obj = _objects[index];
            if (IsActive(obj) && !seen.Contains(obj.Id)) order.Add(obj);
        }
        order.AddRange(queued);
        return order;
    }

    /// <summary>Paints the active images over a copy of the room background.</summary>
    public byte[] Compose(RoomImage room)
    {
        ArgumentNullException.ThrowIfNull(room);
        var pixels = room.Pixels.ToArray();
        foreach (var obj in PaintOrder()) Paint(obj, pixels, room.Width, room.Height);
        return pixels;
    }

    /// <summary>
    /// The depth mask (z-plane 1) as the room redraw leaves it: the room's mask, then each active image's own mask
    /// over its rectangle in paint order (ScummVM <c>drawBitmap</c> → <c>decodeMask</c>; EGA strips are never
    /// transparent, so an object's mask replaces the room's there). Null when the room has no mask. An image whose
    /// mask could not be read leaves the mask under it as it was. Only images the reached states draw count: an image
    /// the artwork estimate shows (Room 02's object 157, state 0 in the game) paints no mask, so it cannot hide an actor.
    /// </summary>
    public byte[]? ComposeDepthMask(RoomImage room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.DepthMask is not { } depth) return null;
        var mask = depth.Pixels.ToArray();
        foreach (var obj in PaintOrder())
            if (obj.Mask is { } objectMask && _reachedActive.Contains(obj.Id))
                Paint(obj with { Pixels = objectMask }, mask, room.Width, room.Height, GetPosition(obj));
        return mask;
    }

    public const string FirstStoredImageReason = "first stored image for this position; no decoded script draws it";

    private bool RemovesBackgroundColours(RoomObjectImage obj)
    {
        if (_background is null) return false;
        var position = GetPosition(obj);
        if (position.X < 0 || position.Y < 0 || position.X + obj.Width > _background.Width ||
            position.Y + obj.Height > _background.Height) return false;
        var backgroundColours = new HashSet<byte>();
        for (var y = 0; y < obj.Height; y++)
            for (var x = 0; x < obj.Width; x++)
                backgroundColours.Add(_background.Pixels.Span[(position.Y + y) * _background.Width + position.X + x]);
        return obj.Pixels.ToHashSet().IsProperSubsetOf(backgroundColours);
    }

    /// <summary>
    /// Supplies a reproducible artwork estimate where a partial script run
    /// leaves no image. Existing script-visible images always take precedence.
    /// An empty position that no active image overlaps is first shown with a
    /// script draw batch, with the priorities <see cref="FillGaps"/> uses: the
    /// strongest batch that draws one of its images, applied as a whole, with a
    /// seeded pick among equally strong batches. Positions no batch covers get
    /// their first stored image. Cut-outs opened by these images are then
    /// closed as in <see cref="FillGaps"/>. This is a viewer fallback, not a
    /// SCUMM state transition; every image it shows is returned with its source.
    /// Interface images are never shown. A position whose state reached code
    /// set (<paramref name="statesSetByCode"/>) is still shown from a batch,
    /// because that draw may run after the reached <c>setState</c> (script 106
    /// sets Room 02's 157 to 0 on entry and then draws it in the opening
    /// frame), but it never gets the first-stored-image guess. A position no
    /// batch covers next takes a state its room's entry script sets on every
    /// path (<paramref name="entryStates"/>): a nonzero state shows that image,
    /// a zero state leaves the position empty. Only then does the first stored
    /// image apply.
    /// An intact background can already contain the artwork: index-hidden
    /// colour-removal overlays are not guessed without positive entry state
    /// or an entry/opening draw batch. Reached draws remain authoritative.
    /// </summary>
    public IReadOnlyList<RoomArtworkEstimate> ShowUnresolvedArtwork(IReadOnlyList<RoomDrawBatch>? batches = null,
        int seed = 0, IReadOnlyCollection<int>? statesSetByCode = null,
        IReadOnlyDictionary<int, RoomStateEvidence>? entryStates = null)
    {
        _batches = batches ?? _batches;
        _seed = seed;
        var estimates = new List<RoomArtworkEstimate>();
        var random = new Random(seed);

        // Some stored overlays erase details already drawn into the background.
        // Do not guess these index-hidden removal images from a later reaction.
        // Entry/opening evidence and actual source draws still take precedence.
        bool KeepIndexedBackground(RoomObjectImage obj) => _background is not null &&
            _indexedBackground.Contains(obj.Id) && !IsBackgroundGap(obj) && RemovesBackgroundColours(obj) &&
            !Peers(obj).Any(peer => entryStates?.TryGetValue(peer.Id, out var state) == true && (state.State & 0x0F) != 0) &&
            !_batches.Any(batch => batch.Priority >= LoomSceneDrawBatches.EntryScriptPriority &&
                batch.Steps.Any(step => Peers(obj).Any(peer => peer.Id == step.ObjectId)));

        // A later-reaction guess cannot overwrite a source-selected hidden region
        // on intact background. Keep the existing entry/opening-frame estimates
        // and unconditional entry-reset policy; neither proves a played branch.
        bool KeepReachedBackground(RoomObjectImage obj, int priority = LoomSceneDrawBatches.OtherScriptPriority) =>
            priority < LoomSceneDrawBatches.EntryScriptPriority && _background is not null &&
            _objects.Any(other => statesSetByCode?.Contains(other.Id) == true &&
                entryStates?.ContainsKey(other.Id) != true && Intersects(obj, other) && !IsBackgroundGap(other));

        // The room's entry script sets every image at this position to state 0 on every path, and the background already
        // fills the position (no gap): the entry reset wins over a later script's draw (Room 19's sphere stays a plain sphere
        // instead of showing the chicken that script 094 draws during the vision).
        bool EntryKeepsBackground(RoomObjectImage obj) => _background is not null && entryStates is not null && !IsBackgroundGap(obj) &&
            Peers(obj).All(peer => entryStates.TryGetValue(peer.Id, out var evidence) && (evidence.State & 0x0F) == 0);

        bool Open(RoomObjectImage obj) => !_interfaceImages.Contains(obj.Id) && !KeepIndexedBackground(obj) && !EntryKeepsBackground(obj) &&
                                          !Peers(obj).Any(IsActive) &&
                                          !_objects.Any(other => IsActive(other) && Intersects(obj, other));

        // Strongest evidence first: each round shows the empty position whose
        // best covering batch has the highest priority (directory order breaks
        // ties), since that batch may also fill other empty positions.
        var skipped = new HashSet<(int, int, int, int)>();
        for (var round = 0; round < 256; round++)
        {
            RoomDrawBatch[]? best = null;
            HashSet<int>? bestMembers = null;
            RoomObjectImage? bestPosition = null;
            foreach (var obj in _objects)
            {
                if (skipped.Contains(Rect(obj)) || !Open(obj)) continue;
                var members = Peers(obj).Where(IsRoomArtwork).Select(peer => peer.Id).ToHashSet();
                var group = _batches.Where(item => !KeepReachedBackground(obj, item.Priority) &&
                        item.Steps.Any(step => members.Contains(step.ObjectId)))
                    .GroupBy(item => item.Priority).OrderByDescending(items => items.Key)
                    .Select(items => items.OrderBy(item => item.Source, StringComparer.Ordinal).ToArray())
                    .FirstOrDefault();
                if (group is null || best is not null && group[0].Priority <= best[0].Priority) continue;
                best = group;
                bestMembers = members;
                bestPosition = obj;
            }
            if (best is null || bestMembers is null || bestPosition is null) break;

            var chosen = best[random.Next(best.Length)];
            var shown = false;
            foreach (var step in chosen.Steps)
            {
                var obj = _objects.FirstOrDefault(item => item.Id == step.ObjectId);
                if (obj is null || !IsRoomArtwork(obj) || KeepReachedBackground(obj, chosen.Priority)) continue;
                // As in FillGaps: keep what is already visible elsewhere.
                if (!bestMembers.Contains(obj.Id) && Peers(obj).Any(IsActive)) continue;
                if (!DrawObject(obj.Id, step.X, step.Y)) continue;
                estimates.Add(new RoomArtworkEstimate(obj.Id, chosen.Source));
                shown |= bestMembers.Contains(obj.Id);
            }
            // A batch that could not show its position is not retried; the
            // position falls through to the stored-image estimate below.
            if (!shown) skipped.Add(Rect(bestPosition));
        }

        foreach (var obj in _objects)
        {
            if (!Open(obj) || KeepReachedBackground(obj) || Peers(obj).Any(peer => statesSetByCode?.Contains(peer.Id) == true)) continue;
            var evidence = entryStates is null ? [] : Peers(obj).Where(peer => entryStates.ContainsKey(peer.Id)).ToArray();
            if (evidence.Length > 0)
            {
                foreach (var peer in evidence.Where(peer => (entryStates![peer.Id].State & 0x0F) != 0 && IsRoomArtwork(peer)))
                {
                    _states[peer.Id] = entryStates![peer.Id].State;
                    _knownStates.Add(peer.Id);
                    if (IsActive(peer)) estimates.Add(new RoomArtworkEstimate(peer.Id, entryStates[peer.Id].Source));
                    else _states[peer.Id] = 0;
                }
                continue;
            }
            _states[obj.Id] = 1;
            if (IsActive(obj)) estimates.Add(new RoomArtworkEstimate(obj.Id, FirstStoredImageReason));
            else _states[obj.Id] = 0;
        }

        estimates.AddRange(FillGapsWithin(null));
        return estimates;
    }

    /// <summary>
    /// Closes visible gaps: rectangles that show solid colour 0 while the
    /// surrounding pixels do not, where stored images of that exact size can
    /// fill them. A gap is closed by repainting an already active image, then
    /// by the strongest script draw batch that includes one of its images
    /// (applied as a whole so images that are drawn together stay together),
    /// and otherwise by a seeded random choice. Larger gaps are filled first
    /// so a container is chosen before the inserts in its cut-outs.
    /// </summary>
    public IReadOnlyList<RoomArtworkEstimate> FillGaps(IReadOnlyList<RoomDrawBatch>? batches = null, int seed = 0)
    {
        _batches = batches ?? _batches;
        _seed = seed;
        return FillGapsWithin(null);
    }

    private List<RoomArtworkEstimate> FillGapsWithin((int X, int Y, int Width, int Height)? region)
    {
        var estimates = new List<RoomArtworkEstimate>();
        if (_background is null) return estimates;
        var random = new Random(_seed);
        var tried = new HashSet<(int, int, int, int)>();
        for (var iteration = 0; iteration < 256; iteration++)
        {
            var composite = Compose(_background);
            var gap = FindGap(composite, region, tried);
            if (gap is null) break;
            var rect = Rect(gap[0]);
            tried.Add(rect);

            var active = gap.FirstOrDefault(IsActive);
            if (active is not null)
            {
                Enqueue(active.Id);
                continue;
            }

            var memberIds = gap.Select(obj => obj.Id).ToHashSet();
            var batch = _batches.Where(item => item.Steps.Any(step => memberIds.Contains(step.ObjectId)))
                .GroupBy(item => item.Priority).OrderByDescending(group => group.Key)
                .Select(group => group.OrderBy(item => item.Source, StringComparer.Ordinal).ToArray())
                .FirstOrDefault();
            if (batch is { Length: > 0 })
            {
                var chosen = batch[random.Next(batch.Length)];
                foreach (var step in chosen.Steps)
                {
                    var obj = _objects.FirstOrDefault(item => item.Id == step.ObjectId);
                    if (obj is null || !IsRoomArtwork(obj)) continue;
                    // Keep reached state elsewhere: only fill this gap or
                    // positions that currently show nothing.
                    if (!memberIds.Contains(obj.Id) && Peers(obj).Any(IsActive)) continue;
                    if (DrawObject(obj.Id, step.X, step.Y))
                        estimates.Add(new RoomArtworkEstimate(obj.Id, chosen.Source));
                }
                if (gap.Any(IsActive)) continue;
            }

            var candidates = gap.Where(obj => IsRoomArtwork(obj) && BlackFraction(obj) <= CandidateMaxBlack).ToArray();
            if (candidates.Length == 0) continue;
            var pick = candidates[random.Next(candidates.Length)];
            if (DrawObject(pick.Id))
                estimates.Add(new RoomArtworkEstimate(pick.Id, "random choice among the images stored for this position"));
        }
        return estimates;
    }

    /// <summary>Gaps visible in the current composite, using the same test as <see cref="FillGaps"/>.</summary>
    public IReadOnlyList<RoomGap> VisibleGaps()
    {
        if (_background is null) return [];
        var composite = Compose(_background);
        return _objects.Where(obj => !_interfaceImages.Contains(obj.Id)).GroupBy(Rect)
            .Where(group => IsGap(composite, group.Key))
            .Select(group => new RoomGap(group.Key.X, group.Key.Y, group.Key.Width, group.Key.Height,
                group.Any(obj => IsRoomArtwork(obj) && BlackFraction(obj) <= CandidateMaxBlack)))
            .ToArray();
    }

    private List<RoomObjectImage>? FindGap(byte[] composite, (int X, int Y, int Width, int Height)? region,
        HashSet<(int, int, int, int)> tried)
    {
        List<RoomObjectImage>? best = null;
        var bestArea = -1;
        foreach (var group in _objects.GroupBy(Rect))
        {
            var rect = group.Key;
            if (tried.Contains(rect)) continue;
            if (region is { } area && (rect.Item1 < area.X || rect.Item2 < area.Y ||
                                       rect.Item1 + rect.Item3 > area.X + area.Width ||
                                       rect.Item2 + rect.Item4 > area.Y + area.Height)) continue;
            var members = group.ToList();
            if (!members.Any(obj => IsRoomArtwork(obj) && BlackFraction(obj) <= CandidateMaxBlack)) continue;
            if (!IsGap(composite, rect)) continue;
            var size = rect.Item3 * rect.Item4;
            if (size <= bestArea) continue;
            best = members;
            bestArea = size;
        }
        return best;
    }

    private bool IsGap(byte[] pixels, (int X, int Y, int Width, int Height) rect)
    {
        if (_background is null) return false;
        var (inside, ring) = BlackFractions(pixels, _background.Width, _background.Height, rect);
        return inside >= GapInsideBlack && ring <= GapRingBlack;
    }

    private bool IsBackgroundGap(RoomObjectImage obj)
    {
        if (_background is null) return false;
        var (inside, ring) = BlackFractions(_background.Pixels.ToArray(), _background.Width, _background.Height, Rect(obj));
        return inside >= 0.5 && ring <= GapRingBlack;
    }

    private static (double Inside, double Ring) BlackFractions(byte[] pixels, int width, int height,
        (int X, int Y, int Width, int Height) rect)
    {
        var left = Math.Max(0, rect.X);
        var right = Math.Min(width, rect.X + rect.Width);
        var top = Math.Max(0, rect.Y);
        var bottom = Math.Min(height, rect.Y + rect.Height);
        if (left >= right || top >= bottom) return (0, 1);
        var black = 0;
        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
                if (pixels[y * width + x] == 0) black++;
        var inside = black / (double)((right - left) * (bottom - top));

        var ringTotal = 0;
        var ringBlack = 0;
        void Sample(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            ringTotal++;
            if (pixels[y * width + x] == 0) ringBlack++;
        }
        for (var x = left - 1; x <= right; x++) { Sample(x, top - 1); Sample(x, bottom); }
        for (var y = top; y < bottom; y++) { Sample(left - 1, y); Sample(right, y); }
        // A rectangle covering the whole room has no surroundings to compare.
        var ring = ringTotal == 0 ? 0 : ringBlack / (double)ringTotal;
        return (inside, ring);
    }

    private double BlackFraction(RoomObjectImage obj)
    {
        if (_blackFractions.TryGetValue(obj.Id, out var value)) return value;
        value = obj.Pixels.Length == 0 ? 1 : obj.Pixels.Count(pixel => pixel == 0) / (double)obj.Pixels.Length;
        _blackFractions[obj.Id] = value;
        return value;
    }

    public RoomObjectPosition GetPosition(RoomObjectImage obj) => _positions[obj.Id];

    public bool SetState(int objectId, byte state)
    {
        if (!_states.ContainsKey(objectId)) return false;
        _states[objectId] = state;
        _knownStates.Add(objectId);
        // o5_setState marks the object's rectangle dirty, which redraws the
        // room in directory order and clears the pending draw queue.
        _drawQueue.Clear();
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
        _knownStates.Add(objectId);
        Enqueue(objectId);
        return true;
    }

    /// <summary>
    /// Shows an image as a viewer <c>drawObject</c> call, then repaints or
    /// estimates the inserts that belong in its cut-outs, as the game's scripts
    /// do after drawing such an image.
    /// </summary>
    public bool Select(RoomObjectImage obj)
    {
        if (!DrawObject(obj.Id)) return false;
        var position = GetPosition(obj);
        FillGapsWithin((position.X, position.Y, obj.Width, obj.Height));
        return true;
    }

    public RoomObjectImage? HitTest(int x, int y)
    {
        // The last painted image at this pixel is the one the viewer sees.
        var order = PaintOrder();
        for (var index = order.Count - 1; index >= 0; index--)
            if (Contains(order[index], x, y)) return order[index];

        // When the background shows through, the first stored image at that
        // rectangle can still be reached by clicking its recorded bounds.
        return _objects.FirstOrDefault(obj => IsRoomArtwork(obj) && Contains(obj, x, y));
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
                foreach (var insert in CutoutInserts(current)) _states[insert.Id] = 0;
                foreach (var peer in peers) _states[peer.Id] = 0;
                return null;
            }
            Select(peers[0]);
            return peers[0];
        }

        Select(peers[index + 1]);
        return peers[index + 1];
    }

    /// <summary>Active images that sit in a solid colour-0 cut-out of the container.</summary>
    private IEnumerable<RoomObjectImage> CutoutInserts(RoomObjectImage container)
    {
        var outer = GetPosition(container);
        foreach (var obj in _objects)
        {
            if (obj.Id == container.Id || !IsActive(obj) || Rect(obj) == Rect(container)) continue;
            var inner = GetPosition(obj);
            if (inner.X < outer.X || inner.Y < outer.Y || inner.X + obj.Width > outer.X + container.Width ||
                inner.Y + obj.Height > outer.Y + container.Height) continue;
            var black = 0;
            for (var y = 0; y < obj.Height; y++)
                for (var x = 0; x < obj.Width; x++)
                    if (container.Pixels[(inner.Y - outer.Y + y) * container.Width + inner.X - outer.X + x] == 0) black++;
            if (black >= CutoutBlack * obj.Width * obj.Height) yield return obj;
        }
    }

    private void Enqueue(int objectId)
    {
        _drawQueue.Remove(objectId);
        _drawQueue.Add(objectId);
    }

    private (int X, int Y, int Width, int Height) Rect(RoomObjectImage obj)
    {
        var position = GetPosition(obj);
        return (position.X, position.Y, obj.Width, obj.Height);
    }

    private static void Paint(RoomObjectImage obj, byte[] pixels, int width, int height, RoomObjectPosition position)
    {
        var left = Math.Max(0, position.X);
        var right = Math.Min(width, position.X + obj.Width);
        var top = Math.Max(0, position.Y);
        var bottom = Math.Min(height, position.Y + obj.Height);
        for (var y = top; y < bottom; y++)
            if (left < right)
                Array.Copy(obj.Pixels, (y - position.Y) * obj.Width + left - position.X,
                    pixels, y * width + left, right - left);
    }

    private void Paint(RoomObjectImage obj, byte[] pixels, int width, int height) =>
        Paint(obj, pixels, width, height, GetPosition(obj));

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
}

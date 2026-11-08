using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Scene order (ROADMAP 3c; user, 8 November 2026: Maniac Mansion's Edsel launched with the garage door closed). A picked object that lies under other
/// objects the player sees and can click (drawn, touchable, over the picked object's middle) is out of the player's reach until something takes them
/// away: the Edsel (Room 16 object 340, part of the background) is behind the garage door's slats (objects 144 to 149) until Script 14 opens it. When one
/// piece of the game's code turns every covering object off, that code ran first, so the scene gets what it sets on them. An estimate of the story point,
/// kept only when the scene still begins and reaches the same rooms.
/// </summary>
public sealed partial class ScummSceneSetupSearch
{
    private Lazy<IReadOnlyDictionary<int, byte>>? _initialStates;
    private Lazy<IReadOnlyDictionary<int, List<(string Unit, ScummSetupEdit Write)>>>? _offWrites;

    private IReadOnlyDictionary<int, byte> InitialStates => (_initialStates ??= new(() =>
    {
        try { return ScummGameSession.ReadIndexedObjects(_game).States; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { return new Dictionary<int, byte>(); }
    })).Value;

    /// <summary>The state writes that take an object away (version 1 and 2: <c>IntrinsicOff</c>, <c>Untouchable</c>; later versions: state 0), by object.</summary>
    private IReadOnlyDictionary<int, List<(string Unit, ScummSetupEdit Write)>> OffWrites => (_offWrites ??= new(() =>
    {
        var found = new Dictionary<int, List<(string, ScummSetupEdit)>>();
        foreach (var unit in CodeUnits())
        {
            if (Read(unit.Label, unit.AsTrigger) is not { } script) continue;
            foreach (var ins in script.Instructions.Where(i => i.Operation == "setState"))
            {
                var parts = ins.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2 || Literal(parts[0]) is not int obj) continue;
                var where = $"{unit.Label} +0x{ins.Offset:X4}";
                ScummSetupEdit? edit = parts[1] switch
                {
                    "IntrinsicOff" => new(ScummSetupKind.ObjectStateBit, obj.ToString(CultureInfo.InvariantCulture), -8, where),
                    "Untouchable" => new(ScummSetupKind.ObjectStateBit, obj.ToString(CultureInfo.InvariantCulture), 2, where),
                    _ when !_isV2 && Literal(parts[1]) == 0 => new(ScummSetupKind.ObjectState, obj.ToString(CultureInfo.InvariantCulture), 0, where),
                    _ => null,
                };
                if (edit is null) continue;
                (found.TryGetValue(obj, out var list) ? list : found[obj] = []).Add((unit.Label, edit));
            }
        }
        return found.ToDictionary(p => p.Key, p => p.Value);
    })).Value;

    /// <summary>
    /// The state that uncovers the picked object: the objects drawn and touchable over its middle at the start, and what the one piece of code that takes
    /// them all away sets on them. Empty when nothing covers it, or no single piece of code (outside the scene) uncovers it.
    /// </summary>
    public IReadOnlyList<ScummSetupEdit> UncoverState(ScummCutscene trigger, IReadOnlyList<ScummSetupEdit> kept)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(kept);
        if (trigger.IsRoomEntry) return [];
        IReadOnlyList<ScummRoomObjectRecord> records;
        try { records = _data.ReadRoom(trigger.Room).Objects; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { return []; }
        if (records.FirstOrDefault(r => r.Id == trigger.ObjectId) is not { } picked) return [];
        var (midX, midY) = (picked.X + picked.Width / 2, picked.Y + picked.Height / 2);
        var states = InitialStates;
        bool Drawn(int id) => states.GetValueOrDefault(id) is var state && (_isV2 ? (state & 8) != 0 && (state & 2) == 0 : state != 0);
        var covering = records.Where(r => r.Id != picked.Id && r.ImageOffset is not null && r.Width > 0 && r.Height > 0 &&
                                          r.X <= midX && midX < r.X + r.Width && r.Y <= midY && midY < r.Y + r.Height && Drawn(r.Id))
            .Select(r => r.Id).ToList();
        if (covering.Count == 0 || covering.Any(id => !OffWrites.ContainsKey(id))) return [];
        var labels = SceneLabels(trigger).ToHashSet(StringComparer.Ordinal);
        var units = covering.Select(id => OffWrites[id].Where(w => !labels.Contains(w.Unit)).Select(w => w.Unit).ToHashSet(StringComparer.Ordinal))
            .Aggregate((a, b) => { a.IntersectWith(b); return a; });
        if (units.Count != 1) return [];
        var unit = units.First();
        var taken = kept.Select(k => (k.Kind, k.Target, k.Value)).ToHashSet();
        // All that code takes away in this room, as one change (the whole garage door, not only the slat over the Edsel's middle).
        var inRoom = records.Select(r => r.Id).ToHashSet();
        return OffWrites.Where(p => inRoom.Contains(p.Key)).SelectMany(p => p.Value.Where(w => w.Unit == unit).Select(w => w.Write))
            .DistinctBy(w => (w.Kind, w.Target, w.Value)).Where(w => !taken.Contains((w.Kind, w.Target, w.Value)))
            .Select(w => w with { Where = $"{w.Where}, which uncovers object {picked.Id}: earlier code that takes away what lies over it" }).ToList();
    }
}

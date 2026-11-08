using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Scene order, second step (ROADMAP 3c, 8 November 2026): where a scene sits in the story by the rooms reachable at that point. Every room load the
/// game's code holds (an object's doors and other verbs, a room's entry and local scripts, a global script) is read with the tests on the way to it
/// (<see cref="Ranked"/> toward the load); a flag or an item that <b>every</b> way into a room needs was there when the hero came in, so a scene in
/// that room, one a fresh start begins included, gets it, and then the state of the code that sets it (<see cref="EarlierScenesState"/>). A load from
/// code with no room of its own (a global script), or into a room no read way leads to, needs nothing; values only ever leave the set, so a way the
/// read misses makes a room need less, never more. An estimate of the story point, kept only when the scene still begins and reaches the same rooms.
/// </summary>
public sealed partial class ScummSceneSetupSearch
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int, int, int), Lazy<ScummScenePlan>> _placed = new();
    private readonly Lazy<IReadOnlyDictionary<int, IReadOnlyList<ScummSetupEdit>>> _roomNeeds;

    /// <summary>One room load: from a room's code (<c>From</c>; -1 for a global script), into <c>To</c>, with the values on the way to it.</summary>
    private sealed record RoomLoad(int From, int To, IReadOnlyList<ScummSetupEdit> Gate);

    private static bool IsReachKind(ScummSetupKind kind) => kind is ScummSetupKind.Bit or ScummSetupKind.Variable or ScummSetupKind.Held;

    /// <summary>What every way into a room that the game's code reads needs (flags, items held), by room.</summary>
    public IReadOnlyList<ScummSetupEdit> RoomNeeds(int room) =>
        _roomNeeds.Value.TryGetValue(room, out var needs) ? needs : [];

    /// <summary>The trigger's plan when a fresh start already begins it: the state of the room it is in (<see cref="RoomNeeds"/>) and of the code before, if kept.</summary>
    public ScummScenePlan Place(ScummCutscene trigger, ScummCutsceneRunResult fresh)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(fresh);
        return _placed.GetOrAdd(Key(trigger), _ => new(() =>
        {
            var (best, kept) = Placed(trigger, fresh, []);
            var plan = new ScummScenePlan(kept, fresh, best, "");
            return kept.Count == 0 ? plan : plan with
            {
                Note = $"Story point (estimate): {string.Join("; ", kept.Select(edit => edit.Describe()))}; every way into Room {trigger.Room} the game's code " +
                       "reads needs these, or the code that sets them ran before. Kept because the scene still begins and reaches the same rooms with them.",
            };
        })).Value;
    }

    /// <summary>The setup <paramref name="kept"/> with the state of the scene's room and of the code before, when the scene still begins and reaches its rooms.</summary>
    private (ScummCutsceneRunResult Best, List<ScummSetupEdit> Kept) Placed(ScummCutscene trigger, ScummCutsceneRunResult best, List<ScummSetupEdit> kept)
    {
        var tried = new List<List<ScummSetupEdit>>();
        if (RoomState(trigger, kept) is { Count: > 0 } room)
        {
            var withRoom = kept.Concat(room).ToList();
            tried.Add(withRoom.Concat(EarlierScenesState(trigger, withRoom)).ToList());
        }
        if (EarlierScenesState(trigger, kept) is { Count: > 0 } earlier) tried.Add(kept.Concat(earlier).ToList());
        foreach (var trial in tried)
        {
            var result = _run(trigger, trial);
            if (result.Failure is null && result.Begun && best.RoomsEntered.All(result.RoomsEntered.Contains)) return (result, trial);
        }
        return (best, kept);
    }

    /// <summary>The values of <see cref="RoomNeeds"/> for the trigger's room the setup does not have, its own code does not set, and its way does not test otherwise.</summary>
    public IReadOnlyList<ScummSetupEdit> RoomState(ScummCutscene trigger, IReadOnlyList<ScummSetupEdit> kept)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(kept);
        var needs = RoomNeeds(trigger.Room);
        if (needs.Count == 0) return [];
        var own = OwnWrites(trigger);
        var taken = kept.Select(k => k.Target).ToHashSet(StringComparer.Ordinal);
        var guards = GuardValues(trigger).Where(g => IsReachKind(g.Kind)).GroupBy(g => (g.Kind, g.Target)).ToDictionary(g => g.Key, g => g.First().Value);
        return needs.GroupBy(n => (n.Kind, n.Target)).Where(g => g.Count() == 1).Select(g => g.First())
            .Where(n => !own.Contains(n.Target) && !taken.Contains(n.Target) && (!guards.TryGetValue((n.Kind, n.Target), out var value) || value == n.Value))
            .ToList();
    }

    private HashSet<string> OwnWrites(ScummCutscene trigger)
    {
        var order = _order.Value;
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in SceneLabels(trigger))
            if (order.Writes.TryGetValue(label, out var writes)) own.UnionWith(writes.Select(w => w.Write.Target));
            else if (Read(label, trigger) is { } script) own.UnionWith(WritesOf(label, script).Select(w => w.Write.Target));
        return own;
    }

    private IReadOnlyDictionary<int, IReadOnlyList<ScummSetupEdit>> ReadRoomNeeds()
    {
        var into = RoomLoads().Where(l => l.From != l.To).GroupBy(l => l.To).ToDictionary(g => g.Key, g => g.ToList());
        // null: nothing known yet (every way in comes from rooms not reached yet); a room no read way leads into needs nothing.
        var need = into.Keys.ToDictionary(r => r, _ => (Dictionary<(ScummSetupKind, string, int), ScummSetupEdit>?)null);
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var (room, loads) in into)
            {
                Dictionary<(ScummSetupKind, string, int), ScummSetupEdit>? meet = null;
                foreach (var load in loads)
                {
                    var from = load.From >= 0 && need.TryGetValue(load.From, out var known) ? known : [];
                    if (from is null) continue;
                    var way = new Dictionary<(ScummSetupKind, string, int), ScummSetupEdit>(from);
                    foreach (var gate in load.Gate) way.TryAdd((gate.Kind, gate.Target, gate.Value), gate);
                    meet = meet is null ? way : meet.Where(p => way.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value);
                }
                if (meet is null || need[room] is { } before && before.Count == meet.Count) continue;
                need[room] = meet;
                changed = true;
            }
        }
        return need.Where(p => p.Value is { Count: > 0 }).ToDictionary(p => p.Key, p => (IReadOnlyList<ScummSetupEdit>)p.Value!.Values.ToList());
    }

    /// <summary>Every room load with a literal room in the game's code, with the values on the scene's side of the tests on the way to it.</summary>
    private List<RoomLoad> RoomLoads()
    {
        var loads = new List<RoomLoad>();
        var finder = new ScummDoorwayFinder(_game, _data);
        void Add(int from, string unit, ScummCutscene stand, string script, int offset, int to)
        {
            IReadOnlyList<ScummSetupEdit> gate;
            try
            {
                gate = Ranked(stand, (script, offset)).Where(r => r.Rank == 0 && IsReachKind(r.Edit.Kind)).Select(r => r.Edit)
                    .Select(e => e with { Where = $"{e.Where}, on the way into Room {to} ({unit} +0x{offset:X4}): every way in the code reads needs it" }).ToList();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException) { gate = []; }
            loads.Add(new(from, to, gate));
        }
        foreach (var id in _data.GlobalScriptIds())
        {
            ScummScriptDisassembly? script;
            try { script = _data.GlobalScript(id); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or EndOfStreamException) { continue; }
            if (script is null) continue;
            var label = $"global script {id}";
            foreach (var ins in script.Instructions.Where(i => i.Operation is "loadRoom" or "loadRoomWithEgo"))
            {
                var parts = ins.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (Literal(ins.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "") is int to)
                    Add(-1, label, Stand(label, 0), label, ins.Offset, to);
            }
        }
        foreach (var resource in _game.Rooms.Where(r => r.ResourceId is not null).DistinctBy(r => r.ResourceId).OrderBy(r => r.ResourceId))
        {
            var room = resource.ResourceId!.Value;
            var units = new List<(string Label, ScummCutscene Stand, Func<ScummScriptDisassembly?> Script)>
            {
                ($"Room {room} entry script", Stand($"Room {room} entry script", room), () => _data.RoomEntryScript(room)),
            };
            try
            {
                foreach (var local in _data.RoomLocalScriptIds(room).ToArray())
                    units.Add(($"Room {room} local script {local}", Stand($"Room {room} local script {local}", room), () => _data.RoomLocalScript(room, local)));
                var objects = _data.ReadRoom(room).Objects;
                foreach (var (objectId, entries) in _data.ReadObjectVerbs(room, objects))
                    foreach (var entry in entries.Where(e => !e.IsFallback))
                    {
                        var label = $"Room {room} object {objectId} verb 0x{entry.VerbId:X2}";
                        var record = objects.First(o => o.Id == objectId);
                        units.Add((label, Stand(label, room, objectId, entry.VerbId), () => _data.ReadObjectVerb(room, record, entry.VerbId, out _)));
                    }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or KeyNotFoundException) { }
            foreach (var (label, stand, read) in units)
            {
                IReadOnlyList<ScummDoorwayTarget> targets;
                try { targets = read() is { } script ? finder.Find(resource, script) : []; }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or EndOfStreamException or InvalidOperationException) { continue; }
                foreach (var target in targets)
                {
                    // The script that holds the load: the last step of the path ("local script 201 → global script 7").
                    var last = target.Path == "direct" ? "" : target.Path.Split('→', StringSplitOptions.TrimEntries)[^1];
                    var holder = last.StartsWith("local script ", StringComparison.Ordinal) ? $"Room {room} {last}"
                        : last.StartsWith("global script ", StringComparison.Ordinal) ? last : label;
                    Add(room, label, stand, holder, target.InstructionOffset, target.RoomId);
                }
            }
        }
        return loads;
    }
}

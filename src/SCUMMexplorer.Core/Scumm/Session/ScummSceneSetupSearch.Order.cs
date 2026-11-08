using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Scene order (ROADMAP 3c; user, 2 November 2026: "some always predate others, so the bits are all set correctly when playing them, across all games").
/// What sets a story flag comes before what needs it: when a picked scene's setup needs a flag (a bit, a story variable) that exactly one piece of the
/// game's code sets (an object's verb, a room's entry or local script, a global script no more than <see cref="SharedGlobalScenes"/> scenes share),
/// that code ran first, so the picked scene also gets the flags it leaves and the values its own tests needed on the way to that write, back through up
/// to <see cref="MaxOrderDepth"/> steps. Kept only when the scene still begins and reaches the same rooms; an estimate of the story point, never a
/// played route.
/// </summary>
public sealed partial class ScummSceneSetupSearch
{
    /// <summary>How many steps back the order is followed.</summary>
    public const int MaxOrderDepth = 4;

    /// <summary>The most values the earlier code adds to a setup.</summary>
    public const int MaxOrderEdits = 24;

    /// <summary>A global script reached by more scenes than this is a shared helper (a print routine, the sentence script): its writes are no one step's.</summary>
    public const int SharedGlobalScenes = 3;

    private readonly IReadOnlyList<ScummCutscene> _scenes = [];
    private readonly Lazy<GameOrder> _order;
    private readonly ConcurrentDictionary<(int, int, int), IReadOnlyList<string>> _sceneLabels = new();

    /// <summary>One piece of the game's code: its label (as the scan writes them) and the trigger that stands for it when its tests are read.</summary>
    private sealed record CodeUnit(string Label, ScummCutscene AsTrigger);

    private sealed record GameOrder(IReadOnlyDictionary<string, IReadOnlyList<(ScummSetupEdit Write, int Offset)>> Writes,
        IReadOnlyDictionary<(ScummSetupKind, string, int), IReadOnlyList<(CodeUnit Unit, int Offset)>> Setters, IReadOnlySet<string> Shared);

    /// <summary>The labels of the code that sets a flag to a value (scene order), each with the instruction.</summary>
    public IReadOnlyList<string> CodeSetting(ScummSetupKind kind, string target, int value) =>
        _order.Value.Setters.TryGetValue((kind, target, value), out var units) ? units.Select(u => $"{u.Unit.Label} +0x{u.Offset:X4}").ToArray() : [];

    private IReadOnlyList<string> SceneLabels(ScummCutscene scene) => _sceneLabels.GetOrAdd(Key(scene), _ =>
    {
        var own = scene.IsRoomEntry ? $"Room {scene.Room} entry script" : $"Room {scene.Room} object {scene.ObjectId} verb 0x{scene.VerbId:X2}";
        try { return new[] { own }.Concat(SceneScripts(_game, scene)).Distinct().ToArray(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { return [own]; }
    });

    private static ScummCutscene Stand(string label, int room, int objectId = 0, int verb = 0) => new(room, objectId, "", verb, [label], 0, 0, 0, "");

    private IEnumerable<CodeUnit> CodeUnits()
    {
        foreach (var id in _data.GlobalScriptIds()) yield return new($"global script {id}", Stand($"global script {id}", 0));
        foreach (var room in _game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
        {
            yield return new($"Room {room} entry script", Stand($"Room {room} entry script", room));
            IReadOnlyList<int> locals;
            try { locals = _data.RoomLocalScriptIds(room).ToArray(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { locals = []; }
            foreach (var local in locals) yield return new($"Room {room} local script {local}", Stand($"Room {room} local script {local}", room));
            List<(int Object, int Verb)> verbs = [];
            try
            {
                var objects = _data.ReadRoom(room).Objects;
                foreach (var (objectId, entries) in _data.ReadObjectVerbs(room, objects))
                    foreach (var entry in entries.Where(e => !e.IsFallback)) verbs.Add((objectId, entry.VerbId));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException) { }
            foreach (var (objectId, verb) in verbs)
            {
                var label = $"Room {room} object {objectId} verb 0x{verb:X2}";
                yield return new(label, Stand(label, room, objectId, verb));
            }
        }
    }

    private GameOrder ReadOrder()
    {
        var globalUse = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var scene in _scenes.DistinctBy(Key))
            foreach (var label in SceneLabels(scene).Where(l => l.StartsWith("global script ", StringComparison.Ordinal)))
                globalUse[label] = globalUse.GetValueOrDefault(label) + 1;
        var shared = globalUse.Where(p => p.Value > SharedGlobalScenes).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        // The boot script ran its own way before any scene; its other branches (Monkey Island 2's part selection, global script 1 +0x0009) are not earlier story.
        shared.Add($"global script {ScummSession.BootScript}");
        var writes = new Dictionary<string, IReadOnlyList<(ScummSetupEdit, int)>>(StringComparer.Ordinal);
        var setters = new Dictionary<(ScummSetupKind, string, int), List<(CodeUnit, int)>>();
        foreach (var unit in CodeUnits())
        {
            if (shared.Contains(unit.Label) || Read(unit.Label, unit.AsTrigger) is not { } script) continue;
            var found = WritesOf(unit.Label, script);
            writes[unit.Label] = found;
            foreach (var (edit, offset) in found)
            {
                var list = setters.TryGetValue((edit.Kind, edit.Target, edit.Value), out var existing) ? existing : setters[(edit.Kind, edit.Target, edit.Value)] = [];
                list.Add((unit, offset));
            }
        }
        return new(writes, setters.ToDictionary(p => p.Key, p => (IReadOnlyList<(CodeUnit, int)>)p.Value), shared);
    }

    /// <summary>The flags one script sets to constants (bits raised, story variables; the last constant written wins), not those it also computes.</summary>
    private List<(ScummSetupEdit Write, int Offset)> WritesOf(string label, ScummScriptDisassembly script)
    {
        var scratch = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ins in script.Instructions)
            if (WrittenVariable(ins) is { } written && !IsFlagWrite(ins)) scratch.Add(written);
        var found = new List<(ScummSetupEdit Write, int Offset)>();
        foreach (var ins in script.Instructions.Where(IsFlagWrite))
        {
            var parts = ins.Operands.Split(new[] { " = ", ", " }, 2, StringSplitOptions.None);
            var target = parts[0].Trim();
            if (Literal(parts[1].Trim()) is not int value || scratch.Contains(target)) continue;
            ScummSetupEdit? edit = BitTarget().IsMatch(target) ? value == 0 ? null : new(ScummSetupKind.Bit, target, 1, $"{label} +0x{ins.Offset:X4}")
                : StoryVariable(target) && !_roomLeftVariables.Value.Contains(target) ? new(ScummSetupKind.Variable, target, value, $"{label} +0x{ins.Offset:X4}")
                : null;
            // The last constant a script writes is what it leaves (a flag cleared, then set as it goes on).
            found.RemoveAll(f => f.Write.Target == target);
            if (edit is not null) found.Add((edit, ins.Offset));
        }
        return found;
    }

    /// <summary>
    /// The state the code before <paramref name="trigger"/> leaves, from the flags its setup <paramref name="kept"/> needs: for each flag exactly one
    /// piece of code outside the scene sets, that code's other flags and the values its tests need on the way to the write (followed back in turn). A
    /// flag the scene sets itself, one the setup already has, and a value against the side of a test that leads on to its cutscene are left alone; the
    /// first value found for a flag wins (the nearest step).
    /// </summary>
    public IReadOnlyList<ScummSetupEdit> EarlierScenesState(ScummCutscene trigger, IReadOnlyList<ScummSetupEdit> kept)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(kept);
        var order = _order.Value;
        var labels = SceneLabels(trigger).ToHashSet(StringComparer.Ordinal);
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in labels)
            if (order.Writes.TryGetValue(label, out var writes)) own.UnionWith(writes.Select(w => w.Write.Target));
            else if (Read(label, trigger) is { } script) own.UnionWith(WritesOf(label, script).Select(w => w.Write.Target));
        var taken = kept.Select(k => k.Target).ToHashSet(StringComparer.Ordinal);
        var needed = GuardValues(trigger).Where(g => g.Kind is ScummSetupKind.Bit or ScummSetupKind.Variable).GroupBy(g => g.Target)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
        var visited = new HashSet<string>(labels, StringComparer.Ordinal);
        var added = new List<ScummSetupEdit>();
        var queue = new Queue<(ScummSetupEdit Need, int Depth)>(kept.Where(k => k.Kind is ScummSetupKind.Bit or ScummSetupKind.Variable).Select(k => (k, 0)));
        while (queue.TryDequeue(out var item) && added.Count < MaxOrderEdits)
        {
            // A variable needed at 0 holds what it starts with: that is no sign of the code that also writes 0 (Indy 3 Room 40's local 201, var[0x076]).
            if (item.Need is { Kind: ScummSetupKind.Variable, Value: 0 }) continue;
            if (item.Depth >= MaxOrderDepth || !order.Setters.TryGetValue((item.Need.Kind, item.Need.Target, item.Need.Value), out var setters)) continue;
            var outside = setters.Where(s => !labels.Contains(s.Unit.Label)).ToList();
            if (outside.Count == 0 || outside.Select(s => s.Unit.Label).Distinct().Count() != 1 || !visited.Add(outside[0].Unit.Label)) continue;
            var (unit, offset) = outside[0];
            // Only the writes one run of that code can make with this one (Monkey Island 2's global script 48 sets each part's flags on a branch of its own).
            var path = Read(unit.Label, unit.AsTrigger) is { } code ? OnOnePath(code, offset) : null;
            foreach (var (write, at) in order.Writes[unit.Label])
                if (path is null || path.Contains(at))
                    Offer(write with { Where = $"{write.Where}, which sets it: earlier code that sets {item.Need.Target}" }, item.Depth, false);
            foreach (var (need, rank) in Ranked(unit.AsTrigger, (unit.Label, offset)))
                if (rank == 0 && need.Kind is ScummSetupKind.Bit or ScummSetupKind.Variable)
                    Offer(need with { Where = $"{need.Where}: needed on the way to {unit.Label} +0x{offset:X4}, earlier code that sets {item.Need.Target}" }, item.Depth, true);
        }
        return added;

        void Offer(ScummSetupEdit edit, int depth, bool follow)
        {
            if (added.Count >= MaxOrderEdits || own.Contains(edit.Target) || taken.Contains(edit.Target)) return;
            // Only story state: an engine variable on the way (Maniac Mansion's var[0x00A], the second object, tested by Room 6's object 139) is the
            // sentence of that earlier code, not something it left; set, it spoils the scene's own pick.
            if (edit.Kind == ScummSetupKind.Variable && !StoryVariable(edit.Target)) return;
            if (needed.TryGetValue(edit.Target, out var value) && value != edit.Value) return;
            taken.Add(edit.Target);
            added.Add(edit);
            if (follow) queue.Enqueue((edit, depth + 1));
        }
    }

    /// <summary>
    /// The instructions a run through <paramref name="offset"/> can also run: those it leads on to and those that lead to it, by forward jumps and
    /// falling through (a jump back is a loop going round again, as in <see cref="GuardSides(string, ScummScriptDisassembly, (string Label, int Offset)?, IReadOnlySet{string}, ScummCutscene)"/>),
    /// so the other branches of an if/else chain are left out.
    /// </summary>
    private static HashSet<int> OnOnePath(ScummScriptDisassembly script, int offset)
    {
        var instructions = script.Instructions;
        var index = new Dictionary<int, int>();
        for (var i = 0; i < instructions.Count; i++) index.TryAdd(instructions[i].Offset, i);
        if (!index.TryGetValue(offset, out var start)) return instructions.Select(i => i.Offset).ToHashSet();
        int? Branch(int i) => BranchPattern().Match(instructions[i].Operands) is { Success: true } m &&
                              index.TryGetValue(int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), out var to) ? to : null;
        IEnumerable<int> Next(int i)
        {
            var ins = instructions[i];
            if (ins.Operation == "jumpRelative")
            {
                if (Branch(i) is int to && to > i) yield return to;
                yield break;
            }
            if (ins.Operation is "stopObjectCode" or "chainScript" || ins.Operation == "stopScript" && (ins.Operands.Length == 0 || Literal(ins.Operands.Trim()) == 0)) yield break;
            if (i + 1 < instructions.Count) yield return i + 1;
            if (Branch(i) is int branch && branch > i) yield return branch;
        }
        var into = new List<int>[instructions.Count];
        for (var i = 0; i < instructions.Count; i++)
            foreach (var to in Next(i)) (into[to] ??= []).Add(i);
        var found = new HashSet<int> { start };
        foreach (var forward in new[] { true, false })
        {
            var seen = new HashSet<int> { start };
            var work = new Stack<int>([start]);
            while (work.Count > 0)
                foreach (var next in forward ? Next(work.Pop()) : into[work.Pop()] ?? [])
                    if (seen.Add(next)) work.Push(next);
            found.UnionWith(seen);
        }
        return found.Select(i => instructions[i].Offset).ToHashSet();
    }

    [GeneratedRegex(@"^bit\[0x[0-9A-F]+\]$")]
    private static partial Regex BitTarget();
}

using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// One thing run before a CD scene starts. <see cref="Kind"/> is <c>verb</c> (visit <see cref="Room"/> and run object <see cref="ObjectId"/>'s verb
/// <see cref="VerbId"/> as an exploration action, unseen), <c>entry</c> (visit <see cref="Room"/>, whose entry script then runs), <c>scene</c> (another
/// CD scene, the room's entry when <see cref="ObjectId"/> is 0 or that object's verb, run unseen after its own <see cref="Inner"/> setup) or <c>box</c>
/// (after the scene's room is entered, put the ego into walk box <see cref="Box"/>, or walk him across it).
/// </summary>
public sealed record LoomCdSetupStep(string Kind, int Room, int ObjectId, int VerbId, int Box, string Where, string Reason = "", LoomCdStartSetup? Inner = null);

/// <summary>
/// The start a CD scene needs, derived from its scripts and checked by running it: <see cref="Before"/> runs unseen before the room is entered,
/// <see cref="After"/> once it is entered. <see cref="Plays"/> says whether a trial then began a cutscene; <see cref="Notes"/> lists every assumption
/// (or why nothing could be derived).
/// </summary>
public sealed record LoomCdStartSetup(IReadOnlyList<LoomCdSetupStep> Before, IReadOnlyList<LoomCdSetupStep> After, IReadOnlyList<string> Notes, bool Plays)
{
    public static readonly LoomCdStartSetup None = new([], [], [], true);
    public bool HasSteps => Before.Count + After.Count > 0;

    /// <summary>The setup as one paragraph for the picker note and the status tooltip (empty when a fresh start already plays the scene).</summary>
    public string Describe() => Notes.Count == 0 ? "" : (Plays ? "Derived start setup (estimates): " : "No start found: ") + string.Join(" ", Notes);
}

/// <summary>
/// Derives a CD scene's start setup the way <see cref="LoomSceneSetupPlanner"/> does for the EGA game, from the code and not from a list: the tests the
/// scene's code makes on the way to its <c>cutscene</c> (<see cref="LoomCdSceneGuards"/>) that the fresh post-boot state fails are met by what the game
/// itself does: a story bit or variable by the object verb, room entry or earlier scene whose code assigns the needed constant (run unseen, an
/// exploration action), and a walk-box monitor (a script that waits until an actor's box is N) by putting the ego into box N or walking him across it.
/// A candidate counts only if a headless trial then begins a cutscene. No story flag is written by the explorer, and nothing is applied that the trial
/// did not need.
/// </summary>
public sealed partial class LoomCdSceneSetupPlanner
{
    private const int TrialFrames = 600;
    private const int EndFrames = 6000;
    private const int SettleFrameLimit = 4000;
    private const int MaxTrials = 120;
    private const int MaxDepth = 2;

    private readonly GameLibrary _game;
    private readonly IScummDataSource _data;
    private readonly LoomSceneStateScanner _scanner;
    private readonly IReadOnlyList<LoomCdCutscene> _triggers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int, int, int), LoomCdStartSetup> _cache = [];
    private readonly object _gate = new();

    /// <param name="triggers">Every cutscene trigger of the game (<see cref="LoomCdCutsceneIndex.Scan"/>): a story state written by a scene's script is set by running that scene.</param>
    public LoomCdSceneSetupPlanner(GameLibrary game, IReadOnlyList<LoomCdCutscene> triggers, IScummDataSource? data = null)
    {
        _game = game;
        _data = data ?? ScummDataSources.For(game);
        _scanner = LoomSceneStateScanner.For(game);
        _triggers = triggers;
    }

    /// <summary>Every cutscene trigger of the game the planner works from.</summary>
    public IReadOnlyList<LoomCdCutscene> Triggers => _triggers;

    [GeneratedRegex(@"^(?:Room (\d+) (?:local|entry|exit)(?: (\d+))?|Script (\d+))")]
    private static partial Regex WriterPlace();

    private LoomExplorationSession NewSession() => new(_game, _game.Vga!.ReadIndexedObjects(), completeBoot: false);

    private static GameResource RoomOf(GameLibrary game, int room) => game.Rooms.First(r => r.ResourceId == room);

    /// <summary>Runs frames until nothing that takes the player's control is going on for 30 frames (Escape skips a scene that allows it).</summary>
    public static void Settle(LoomExplorationSession session)
    {
        var quiet = 0;
        for (var frames = 0; frames < SettleFrameLimit && quiet < 30; frames++)
        {
            var busy = session.InCutscene || session.Messages.HaveMsg != 0 || session.EgoMoving || session.CameraMoving;
            quiet = busy ? 0 : quiet + 1;
            if (session.CutsceneSkippable) session.PressKey(0x1B);
            session.AdvanceFrame();
        }
    }

    /// <summary>Applies the steps that run before the scene's room is entered.</summary>
    public static void ApplyBefore(LoomExplorationSession session, GameLibrary game, IEnumerable<LoomCdSetupStep> steps)
    {
        foreach (var step in steps)
        {
            if (step.Kind == "scene" && step.Inner is { } inner) ApplyBefore(session, game, inner.Before);
            session.VisitRoom(RoomOf(game, step.Room));
            if (step.Kind == "scene" && step.Inner is { } after) ApplyAfter(session, after.After);
            if (step.Kind is "verb" or "scene" && step.ObjectId != 0) session.ActivateObjectVerb(step.ObjectId, step.VerbId);
            Settle(session);
        }
    }

    /// <summary>Applies the steps that run once the scene's room is entered (the ego into a walk box), after a few frames so the room's monitors have started.</summary>
    public static void ApplyAfter(LoomExplorationSession session, IEnumerable<LoomCdSetupStep> steps)
    {
        var all = steps.ToList();
        foreach (var step in all)
        {
            // What the previous box set going (a monitor's scene) finishes before the next box.
            if (step != all[0]) Settle(session);
            for (var i = 0; i < 20; i++) session.AdvanceFrame();
            if (session.CurrentWalkBoxes?.Coordinates(step.Box) is not { } box) continue;
            var x = box.Corners.Sum(c => c.X) / 4;
            var y = box.Corners.Sum(c => c.Y) / 4;
            if (!session.CurrentWalkBoxes.Contains(step.Box, x, y))
            {
                var (_, cx, cy) = LoomWalkBoxSet.ClosestPointOnBox(box, x, y);
                x = cx; y = cy;
            }
            var start = session.Actors.First(a => a.Number == 1);
            session.PlaceEgo(x, y);
            if (session.Actors.First(a => a.Number == 1).WalkBox == step.Box) continue;
            // A box that is a thin line (a doorway) does not hold a placed ego; the ego is in it only while he walks across, so he goes back to where he was and walks a route that crosses it.
            if (start.X is int sx && start.Y is int sy) session.PlaceEgo(sx, sy);
            var set = session.CurrentWalkBoxes;
            var from = start.WalkBox ?? 0;
            foreach (var target in set.Boxes.Where(b => b.Index != step.Box && (set.GetFlags(b.Index) & LoomWalkBox.InvisibleFlag) == 0))
            {
                var route = new List<int> { from };
                for (var hop = 0; hop < 24 && route[^1] != target.Index; hop++)
                {
                    var next = set.GetNextBox(route[^1], target.Index);
                    if (next < 0 || route.Contains(next)) break;
                    route.Add(next);
                }
                if (route[^1] != target.Index || !route.Contains(step.Box)) continue;
                session.WalkEgoTo(target.Corners.Sum(c => c.X) / 4, target.Corners.Sum(c => c.Y) / 4);
                break;
            }
        }
        if (all.Count > 1) Settle(session);
    }

    /// <summary>
    /// Runs the scene headless from the given start. Without <paramref name="mustEnd"/> it is enough that a cutscene begins; with it the cutscene must also
    /// end by itself (no Escape) within <see cref="EndFrames"/> frames, which tells a scene that plays from one that stalls on the state around it.
    /// </summary>
    private bool Trial(LoomCdCutscene t, IReadOnlyList<LoomCdSetupStep> before, IReadOnlyList<LoomCdSetupStep> after, bool mustEnd = false)
    {
        try
        {
            var session = NewSession();
            ApplyBefore(session, _game, before);
            var earlier = session.CutscenesBegun; // the setup's own scenes do not count
            session.VisitRoom(RoomOf(_game, t.Room));
            ApplyAfter(session, after);
            if (!t.IsRoomEntry)
            {
                earlier = session.CutscenesBegun; // scenes the setup's own walk set going (a box monitor's) are not this scene
                session.ActivateObjectVerb(t.ObjectId, t.VerbId);
            }
            var begun = false;
            for (var i = 0; i < (mustEnd ? EndFrames : TrialFrames); i++)
            {
                if (session.InCutscene || session.CutscenesBegun > earlier) begun = true;
                if (begun && !mustEnd) return true;
                if (begun && !session.InCutscene && session.Messages.HaveMsg == 0) return true;
                session.AdvanceFrame();
            }
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException) { return false; }
    }

    private IReadOnlyList<LoomCdScenePath> Paths(LoomCdCutscene t)
    {
        var room = _data.ReadRoom(t.Room);
        var root = t.IsRoomEntry ? _data.RoomEntryScript(t.Room)
            : _data.ReadObjectVerb(t.Room, room.Objects.First(o => o.Id == t.ObjectId), t.VerbId, out _);
        return root is null ? [] : LoomCdSceneGuards.Find(_data, t.Room, root, "root");
    }

    /// <summary>The value a guard needs its subject to have, when that is one value.</summary>
    private static int? Wanted(LoomCdGuard g) => (g.Operation, g.MustHold) switch
    {
        ("equalZero", true) => 0,
        ("equalZero", false) => 1,
        ("notEqualZero", true) => 1,
        ("notEqualZero", false) => 0,
        ("isEqual", true) => g.Constant,
        _ => null
    };

    /// <summary>The script label (as <see cref="LoomCdCutscene.Scripts"/> names it) of the code a writer's place names, or null for an entry script or verb.</summary>
    private static string? ScriptLabel(LoomStateSource w)
    {
        if (WriterPlace().Match(w.Where) is not { Success: true } m) return null;
        if (m.Groups[3].Success) return $"global script {int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)}";
        return m.Groups[2].Success ? $"Room {int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)} local script {int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)}" : null;
    }

    /// <summary>The simplest way to set a state first: an object verb, a room entry, a scene's local script, then a global script (which many scenes share).</summary>
    private static int WriterRank(LoomStateSource w) => w.ObjectId is not null ? 0 : w.Where.Contains(" entry ", StringComparison.Ordinal) ? 1 : w.Where.StartsWith("Room ", StringComparison.Ordinal) ? 2 : 3;

    private static int Rank(LoomCdCutscene c) => c.IsRoomEntry ? 0 : c.VerbId switch { 0x38 => 1, 0x37 => 2, 0x36 => 3, _ => 4 };

    private static (int, int, int) Key(LoomCdCutscene t) => (t.Room, t.ObjectId, t.VerbId);

    /// <summary>The derived setup when it has been derived already (so a caller on the UI thread can ask without waiting).</summary>
    public bool TryGet(LoomCdCutscene t, out LoomCdStartSetup setup) => _cache.TryGetValue(Key(t), out setup!);

    /// <summary>Derives (once, then cached) the setup of one trigger; one derivation runs at a time, since they share the scanner's data source.</summary>
    public LoomCdStartSetup Derive(LoomCdCutscene t)
    {
        if (_cache.TryGetValue(Key(t), out var cached)) return cached;
        lock (_gate) return Derive(t, 0, []);
    }

    private LoomCdStartSetup Derive(LoomCdCutscene t, int depth, HashSet<(int, int, int)> stack)
    {
        stack = [.. stack, Key(t)];
        var result = DeriveOnce(t, depth, stack);
        if (depth == 0) _cache[Key(t)] = result;
        return result;
    }

    /// <summary>The step that would make <paramref name="w"/>'s assignment happen first, or null: an object verb, a room entry, or the scene whose script it is.</summary>
    private LoomCdSetupStep? StepFor(LoomStateSource w, string tested, HashSet<string> chain, int depth, HashSet<(int, int, int)> stack, bool broad = false, int pick = 0, bool ownChain = false)
    {
        var label = ScriptLabel(w);
        if (label is not null && chain.Contains(label) && !ownChain) return null; // the scene's own script: only another trigger that runs it can have set the state first
        if (w.ObjectId is int obj && w.VerbId is int verb)
            return new("verb", w.Room, obj, verb, 0, w.Where,
                tested + $"The game's own {w.Where.Split(" +")[0]} (object {obj}, verb 0x{verb:X2}) assigns it, so it runs first, unseen, as an exploration action (not the game's click; which earlier scene really left the state was not executed).");
        if (label is null && w.Where.Contains(" entry ", StringComparison.Ordinal))
            return new("entry", w.Room, 0, 0, 0, w.Where, tested + $"The game's own {w.Where.Split(" +")[0]} assigns it, so Room {w.Room:00} is entered first, unseen.");
        if (label is null || depth >= MaxDepth) return null;
        var scene = _triggers.Where(c => (c.Scripts.Contains(label) || broad && Started(c).Contains(label)) && !stack.Contains(Key(c))).OrderBy(Rank).ThenBy(c => c.ObjectId).ThenBy(c => c.VerbId).Skip(pick).FirstOrDefault();
        if (scene is null) return null;
        var inner = Derive(scene, depth + 1, stack);
        if (!inner.Plays) return null;
        return new("scene", scene.Room, scene.ObjectId, scene.VerbId, 0, w.Where,
            tested + $"The game's own {label} ({w.Where.Split(" +")[1].Insert(0, "+")}) assigns it, and the scene {(scene.IsRoomEntry ? $"Room {scene.Room:00}'s entry" : $"object {scene.ObjectId}'s verb 0x{scene.VerbId:X2} in Room {scene.Room:00}")} runs that script, so it is played first, unseen, as an exploration action" +
            (inner.HasSteps ? $", after its own setup ({string.Join(" ", inner.Notes)})." : "."), inner);
    }

    [GeneratedRegex(@"^(bit\[0x[0-9A-Fa-f]+\]) = 0x0+$")]
    private static partial Regex ClearsBit();

    /// <summary>
    /// A scene that begins from the fresh state but never ends there. A scene's code that clears a story bit (<c>bit[k] = 0</c>) which other code
    /// of the game sets (<c>bit[k] = 1</c>) is written for the state in which that bit is set; so the code that sets it is the candidate, run first (an
    /// object verb, an entry, or the scene that runs it) and kept if the scene then ends by itself. Null when no candidate makes it end.
    /// </summary>
    private LoomCdStartSetup? DeriveStall(LoomCdCutscene t, int depth, HashSet<(int, int, int)> stack)
    {
        var paths = Paths(t);
        var chain = paths.SelectMany(p => p.Chain).ToHashSet();
        var scripts = new List<(string Label, LoomScriptDisassembly Script)>();
        foreach (var label in chain)
            if (LoadLabel(t.Room, label == "root" ? (t.IsRoomEntry ? $"Room {t.Room} entry" : $"Room {t.Room} object {t.ObjectId} verb 0x{t.VerbId:X2}") : label) is { } script) scripts.Add((label, script));
        var cleared = scripts.SelectMany(s => s.Script.Instructions.Where(i => i.Operation == "move" && ClearsBit().IsMatch(i.Operands)).Select(i => (s.Label, i.Offset, Bit: ClearsBit().Match(i.Operands).Groups[1].Value)))
            .DistinctBy(x => x.Bit).ToList();
        var candidates = new List<LoomCdSetupStep>();
        foreach (var (label, offset, bit) in cleared)
        {
            var tested = $"The scene's code ({label} +0x{offset:X4}) clears {bit}, which other code sets, so it is written for the state in which {bit} is set; started from a fresh game it begins but never ends. ";
            foreach (var w in _scanner.FindConstantWriters(bit, 1).OrderBy(WriterRank).ThenBy(w => w.Room).Take(12))
                for (var pick = 0; pick < 4; pick++) // several scenes can run the same script (a room entry may only test the bit)
                    if (StepFor(w, tested, chain, depth, stack, broad: true, pick) is { } step && !candidates.Any(c => c.Kind == step.Kind && c.Room == step.Room && c.ObjectId == step.ObjectId && c.VerbId == step.VerbId))
                        candidates.Add(step);
        }
        foreach (var step in candidates.Take(12))
            if (Trial(t, [step], [], mustEnd: true)) return new([step], [], [step.Reason + " That state is an estimate: the game's own way to it was not executed."], true);
        return null;
    }

    [GeneratedRegex(@"^(?:Room \d+ local script (\d+)|global script (\d+))$")]
    private static partial Regex ScriptNumber();

    [GeneratedRegex(@"^Room (\d+) object (\d+) verb 0x([0-9A-Fa-f]+)$")]
    private static partial Regex ObjectVerbLabel();

    [GeneratedRegex(@"^0x([0-9A-Fa-f]+)")]
    private static partial Regex FirstLiteral();

    /// <summary>The script a label names (as <see cref="LoomCdCutscene.Scripts"/> and the guard paths write it), or null.</summary>
    private LoomScriptDisassembly? LoadLabel(int room, string label)
    {
        try
        {
            if (label == $"Room {room} entry") return _data.RoomEntryScript(room);
            if (ObjectVerbLabel().Match(label) is { Success: true } o)
                return _data.ReadObjectVerb(room, _data.ReadRoom(room).Objects.First(x => x.Id == int.Parse(o.Groups[2].Value, CultureInfo.InvariantCulture)),
                    int.Parse(o.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), out _);
            if (ScriptNumber().Match(label) is { Success: true } m)
                return m.Groups[1].Success ? _data.RoomLocalScript(room, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : _data.GlobalScript(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException) { }
        return null;
    }

    /// <summary>The room's local scripts that read the ego's walk box (<c>getActorWalkBox v, 1</c>) and compare it with constants: the script and those boxes, in code order.</summary>
    private IEnumerable<(int Script, List<int> Boxes)> MonitorBoxes(int room)
    {
        foreach (var id in _data.RoomLocalScriptIds(room))
        {
            if (LoadLabel(room, $"Room {room} local script {id}") is not { } script) continue;
            var readInto = new HashSet<string>();
            var boxes = new List<int>();
            foreach (var i in script.Instructions)
            {
                if (i.Operation == "getActorWalkBox" && i.Operands.Split(", ") is [var dst, "0x01"]) readInto.Add(dst);
                else if (i.Operation == "isEqual" && i.Operands.Split(", ") is [var left, var right, ..] && readInto.Contains(left)
                    && right.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(right.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var box) && !boxes.Contains(box))
                    boxes.Add(box);
            }
            if (boxes.Count is >= 2 and <= 4) yield return (id, boxes);
        }
    }

    private static IEnumerable<List<int>> Permutations(List<int> items)
    {
        if (items.Count <= 1) { yield return [.. items]; yield break; }
        for (var i = 0; i < items.Count; i++)
            foreach (var rest in Permutations([.. items.Where((_, j) => j != i)]))
                yield return [items[i], .. rest];
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int, int, int), HashSet<string>> _started = [];

    /// <summary>The scripts a scene's cutscene scripts run (one step: what they start or chain to), with the cutscene scripts themselves.</summary>
    private HashSet<string> Started(LoomCdCutscene c) => _started.GetOrAdd(Key(c), _ =>
    {
        var set = new HashSet<string>(c.Scripts);
        foreach (var label in c.Scripts)
            if (LoadLabel(c.Room, label) is { } script)
                foreach (var call in script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
                    if (FirstLiteral().Match(call.Operands) is { Success: true } m)
                    {
                        var id = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        set.Add(id >= _data.GlobalScriptCount ? $"Room {c.Room} local script {id}" : $"global script {id}");
                    }
        return set;
    });
    private LoomCdStartSetup DeriveOnce(LoomCdCutscene t, int depth, HashSet<(int, int, int)> stack)
    {
        var trials = 0;
        bool Try(IReadOnlyList<LoomCdSetupStep> b, IReadOnlyList<LoomCdSetupStep> a) { trials++; return Trial(t, b, a); }
        if (Try([], []))
            return depth > 0 || Trial(t, [], [], mustEnd: true) ? LoomCdStartSetup.None : DeriveStall(t, depth, stack) ?? LoomCdStartSetup.None;

        var paths = Paths(t);
        if (paths.Count == 0)
            return new([], [], ["No path from this trigger to a cutscene span was found in the scripts, so no setup could be derived."], false);
        var chain = paths.SelectMany(p => p.Chain).ToHashSet();

        // What the fresh state fails: evaluate every path's tests after a catalog visit of the scene's room.
        var probe = NewSession();
        probe.VisitRoom(RoomOf(_game, t.Room));
        var vars = probe.Snapshot.KnownGlobalVariables;
        var classes = probe.ObjectClasses;
        var unmet = paths.SelectMany(p => p.Guards)
            .Select(g => (g, v: LoomCdSceneGuards.Evaluate(g, k => vars.TryGetValue(k, out var u) ? u : null, id => classes.TryGetValue(id, out var c) ? c : null)))
            .ToList();

        var boxes = unmet.Select(x => x.g).Where(g => g is { MustHold: true, WalkBoxOfActor: 1, Constant: not null })
            .Select(g => (Box: g.Constant!.Value, g)).DistinctBy(x => x.Box).ToList();
        LoomCdSetupStep BoxStep((int Box, LoomCdGuard G) b) => new("box", t.Room, 0, 0, b.Box, $"{b.G.Script} +0x{b.G.Offset:X4}",
            $"The scene's code ({b.G.Script} +0x{b.G.Offset:X4}) waits until the ego's walk box becomes {b.Box}. In the game Bobbin walks there; here he is placed in box {b.Box} (or walks across it, for a doorway) after the room's monitors have started (an estimate of where in the box).");

        // Candidate steps that would set a state the code tests: an object verb or room entry that assigns it, or an earlier scene whose script does.
        var states = new List<LoomCdSetupStep>();
        foreach (var (g, _) in unmet.Where(x => x.v == LoomCdGuardVerdict.Unsatisfied))
        {
            if (!(g.Subject.StartsWith("bit[", StringComparison.Ordinal) || g.Subject.StartsWith("var[", StringComparison.Ordinal)) || Wanted(g) is not int wanted) continue;
            var tested = $"The scene's code tests {g.Subject} ({g.Script} +0x{g.Offset:X4}, it must be {wanted}) and a fresh game has it otherwise. ";
            foreach (var w in _scanner.FindConstantWriters(g.Subject, wanted).OrderBy(WriterRank).ThenBy(w => w.Room).Take(40))
            {
                var step = StepFor(w, tested, chain, depth, stack) ?? StepFor(w, tested, chain, depth, stack, ownChain: true);
                if (step is not null && !states.Any(s => s.Kind == step.Kind && s.Room == step.Room && s.ObjectId == step.ObjectId && s.VerbId == step.VerbId)) states.Add(step);
                if (states.Count >= 12) break;
            }
        }

        // Smallest sets first: one step, then two, then three (a state step, with a box step at the end).
        var boxSteps = boxes.Select(BoxStep).ToList();
        IEnumerable<List<LoomCdSetupStep>> Subsets()
        {
            foreach (var b in boxSteps) yield return [b];
            // A monitor that compares the box before with the box now (a crossing from one box to another) needs the ego in each in turn.
            for (var i = 0; i < boxSteps.Count; i++)
                for (var j = 0; j < boxSteps.Count; j++) if (i != j) yield return [boxSteps[i], boxSteps[j]];
            foreach (var s in states) yield return [s];
            // The same scene twice: the first run changes what the second does (Room 34's object 487 draft verb runs a long scene the first time and the disguise change after that).
            foreach (var s in states.Where(s => s.Kind == "scene")) yield return [s, s];
            for (var i = 0; i < states.Count; i++)
                for (var j = 0; j < states.Count; j++) if (i != j) yield return [states[i], states[j]];
            foreach (var s in states) foreach (var b in boxSteps) yield return [s, b];
            for (var i = 0; i < states.Count; i++)
                for (var j = 0; j < states.Count; j++)
                    for (var k = 0; k < states.Count; k++)
                        if (i != j && j != k && i != k) yield return [states[i], states[j], states[k]];
        }
        foreach (var set in Subsets())
        {
            if (trials >= MaxTrials) break;
            var before = set.Where(s => s.Kind != "box").ToList();
            var after = set.Where(s => s.Kind == "box").ToList();
            if (Try(before, after))
            {
                var notes = set.Select(s => s.Reason).Distinct().ToList();
                if (set.Count == 2 && set[0] == set[1]) notes.Add("The scene is run twice: the first run changes what the second does.");
                return new(before, after, notes, true);
            }
        }

        // The room's own walk-box monitor (a local script that reads the ego's box and starts other scripts per box) sets the story bits the code tests when
        // the ego has been through its boxes in an order; the order is tried, each box settled before the next (the monitor's scenes run unseen).
        if (unmet.Any(x => x.v == LoomCdGuardVerdict.Unsatisfied && (x.g.Subject.StartsWith("bit[", StringComparison.Ordinal) || x.g.Subject.StartsWith("var[", StringComparison.Ordinal))))
            foreach (var (script, monitored) in MonitorBoxes(t.Room))
                foreach (var order in Permutations(monitored))
                {
                    var steps = order.Select(b => new LoomCdSetupStep("box", t.Room, 0, 0, b, $"Room {t.Room} local script {script}",
                        $"Room {t.Room}'s local script {script} watches the ego's walk box and starts other scripts as he enters boxes {string.Join(", ", monitored.Order())}; the code tested here needs what they set, so the ego is placed in boxes {string.Join(", ", order)} in turn, each scene it starts left to finish (in the game Bobbin walks there; the order and the point in each box are estimates).")).ToList();
                    if (trials++ >= MaxTrials + 30) break;
                    if (Trial(t, [], steps))
                        return new([], steps, [steps[0].Reason], true);
                }

        // Who else assigns what the code tests, for the note: the scene's own code, or code no scene or verb starts.
        var writerNotes = new List<string>();
        foreach (var g in unmet.Where(x => x.v == LoomCdGuardVerdict.Unsatisfied).Select(x => x.g).DistinctBy(g => g.Subject))
        {
            if (!(g.Subject.StartsWith("bit[", StringComparison.Ordinal) || g.Subject.StartsWith("var[", StringComparison.Ordinal)) || Wanted(g) is not int wanted) continue;
            var all = _scanner.FindConstantWriters(g.Subject, wanted).Take(6).Select(w =>
            {
                var label = ScriptLabel(w);
                var why = label is not null && chain.Contains(label) ? "this scene's own script"
                    : label is not null && !_triggers.Any(c => c.Scripts.Contains(label)) ? "no scene or verb starts it"
                    : "tried";
                return $"{w.Where.Split(" +")[0]} +{w.Where.Split(" +")[^1]} ({why})";
            }).ToList();
            writerNotes.Add($"{g.Subject} = {wanted} is assigned by " + (all.Count == 0 ? "no code at all" : string.Join(", ", all)));
        }
        // A monitor that wants the ego to change from one box straight into another that is not its neighbour is not met by walking.
        var gaps = new List<string>();
        if (probe.CurrentWalkBoxes is { } boxSet)
            foreach (var from in boxes) foreach (var to in boxes.Where(b => b.Box > from.Box))
            {
                var route = new List<int> { from.Box };
                for (var hop = 0; hop < 24 && route[^1] != to.Box; hop++)
                {
                    var next = boxSet.GetNextBox(route[^1], to.Box);
                    if (next < 0 || route.Contains(next)) break;
                    route.Add(next);
                }
                if (route.Count > 2 && route[^1] == to.Box)
                    gaps.Add($"boxes {from.Box} and {to.Box} are not neighbours (a walk between them passes box {string.Join(", ", route.Skip(1).SkipLast(1))}), so a walking ego changes box one neighbour at a time and the code's one-frame change between them needs a script that places the ego");
            }
        var unmetText = unmet.Where(x => x.v != LoomCdGuardVerdict.Satisfied).Select(x => x.g).DistinctBy(g => (g.Script, g.Offset)).Take(6).ToList();
        return new([], [], [$"Not started from the fresh state, and no derived setup began it ({trials} headless trials). The code's tests it fails or cannot read here: " +
            (unmetText.Count == 0 ? "none of its tests failed in the fresh state, so something else (a wait, an item, an actor) keeps it from starting" :
                string.Join("; ", unmetText.Select(g => $"{g.Script} +0x{g.Offset:X4} {g.Operation} {g.Operands.Split(", branch")[0]} must {(g.MustHold ? "hold" : "fail")}"))) + ". " +
            (states.Count == 0 && boxes.Count == 0 ? "No object verb, room entry or scene that assigns the needed state, and no walk-box monitor, was found in the scripts" + (writerNotes.Count > 0 ? $" ({string.Join("; ", writerNotes)})." : ".") :
             "The game's own writers of that state (and the walk boxes) were tried alone and in combination and did not begin it; " +
             (gaps.Count > 0 ? string.Join("; ", gaps.Take(1)) + "; none was run here." : "the scene may need an earlier part of the story, an item or a draft that this explorer does not model."))], false);
    }
}









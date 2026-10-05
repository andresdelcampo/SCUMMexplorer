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
        foreach (var step in steps)
        {
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
    }

    private bool Trial(LoomCdCutscene t, IReadOnlyList<LoomCdSetupStep> before, IReadOnlyList<LoomCdSetupStep> after)
    {
        try
        {
            var session = NewSession();
            ApplyBefore(session, _game, before);
            session.VisitRoom(RoomOf(_game, t.Room));
            ApplyAfter(session, after);
            if (!t.IsRoomEntry) session.ActivateObjectVerb(t.ObjectId, t.VerbId);
            for (var i = 0; i < TrialFrames; i++)
            {
                if (session.InCutscene || session.CutscenesBegun > 0) return true;
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

    private LoomCdStartSetup DeriveOnce(LoomCdCutscene t, int depth, HashSet<(int, int, int)> stack)
    {
        var trials = 0;
        bool Try(IReadOnlyList<LoomCdSetupStep> b, IReadOnlyList<LoomCdSetupStep> a) { trials++; return Trial(t, b, a); }
        if (Try([], [])) return LoomCdStartSetup.None;

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
                var label = ScriptLabel(w);
                if (label is not null && chain.Contains(label)) continue;
                LoomCdSetupStep? step = null;
                if (w.ObjectId is int obj && w.VerbId is int verb)
                    step = new("verb", w.Room, obj, verb, 0, w.Where,
                        tested + $"The game's own {w.Where.Split(" +")[0]} (object {obj}, verb 0x{verb:X2}) assigns it, so it runs first, unseen, as an exploration action (not the game's click; which earlier scene really left the state was not executed).");
                else if (label is null && w.Where.Contains(" entry ", StringComparison.Ordinal))
                    step = new("entry", w.Room, 0, 0, 0, w.Where, tested + $"The game's own {w.Where.Split(" +")[0]} assigns it, so Room {w.Room:00} is entered first, unseen.");
                else if (label is not null && depth < MaxDepth)
                {
                    var scene = _triggers.Where(c => c.Scripts.Contains(label) && !stack.Contains(Key(c))).OrderBy(Rank).ThenBy(c => c.ObjectId).ThenBy(c => c.VerbId).FirstOrDefault();
                    if (scene is null) continue;
                    var inner = Derive(scene, depth + 1, stack);
                    if (!inner.Plays) continue;
                    step = new("scene", scene.Room, scene.ObjectId, scene.VerbId, 0, w.Where,
                        tested + $"The game's own {label} ({w.Where.Split(" +")[1].Insert(0, "+")}) assigns it, and the scene {(scene.IsRoomEntry ? $"Room {scene.Room:00}'s entry" : $"object {scene.ObjectId}'s verb 0x{scene.VerbId:X2} in Room {scene.Room:00}")} runs that script, so it is played first, unseen, as an exploration action" +
                        (inner.HasSteps ? $", after its own setup ({string.Join(" ", inner.Notes)})." : "."), inner);
                }
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
            if (Try(before, after)) return new(before, after, set.Select(s => s.Reason).ToList(), true);
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
        var unmetText = unmet.Where(x => x.v != LoomCdGuardVerdict.Satisfied).Select(x => x.g).DistinctBy(g => (g.Script, g.Offset)).Take(6).ToList();
        return new([], [], [$"Not started from the fresh state, and no derived setup began it ({trials} headless trials). The code's tests it fails or cannot read here: " +
            (unmetText.Count == 0 ? "none of its tests failed in the fresh state, so something else (a wait, an item, an actor) keeps it from starting" :
                string.Join("; ", unmetText.Select(g => $"{g.Script} +0x{g.Offset:X4} {g.Operation} {g.Operands.Split(", branch")[0]} must {(g.MustHold ? "hold" : "fail")}"))) + ". " +
            (states.Count == 0 && boxes.Count == 0 ? "No object verb, room entry or scene that assigns the needed state, and no walk-box monitor, was found in the scripts" + (writerNotes.Count > 0 ? $" ({string.Join("; ", writerNotes)})." : ".") :
             "The game's own writers of that state (and the walk boxes) were tried alone and in combination and did not begin it; the scene may need an earlier part of the story, an item or a draft that this explorer does not model.")], false);
    }
}

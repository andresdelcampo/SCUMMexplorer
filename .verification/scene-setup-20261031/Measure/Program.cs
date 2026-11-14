using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: Measure <game folder> [max groups] [filter room]
// 31 October 2026 (REQUIREMENTS §8.6): every scan group's first trigger run from a fresh start, then the setup search for those a fresh start does not begin.
var game = new GameFolderScanner().Scan(args[0]);
var limit = args.Length > 1 ? int.Parse(args[1]) : int.MaxValue;
int? onlyRoom = args.Length > 2 ? int.Parse(args[2]) : null;
var clock = Stopwatch.StartNew();
// Maniac Mansion starts its scenes after the reviewed party selection, as the app does (ManiacCutsceneSetup, 1 November 2026).
var maniac = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;
// LOOM EGA runs on its own session, as the app's shared planner does for it (MainWindow.NewLoomScenePlanner; 10 November 2026: the picture checks).
var loomSnapshot = game.Id == GameId.Loom ? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")) : null;
var planner = maniac ? SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(game)
    : loomSnapshot is not null ? new ScummCutscenePlanner(game, () => new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, loomSnapshot))
    : new ScummCutscenePlanner(game);
Func<ScummSession> newRun = () =>
{
    // As the planner's runs start: Maniac's party start, then into control when the boot ends inside a cutscene.
    ScummSession session = maniac ? SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.NewSession(game)
        : loomSnapshot is not null ? new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, loomSnapshot) : ScummGameSession.BootForPlay(game, out _);
    ScummCutscenePlanner.IntoControl(session);
    return session;
};
Console.WriteLine($"{planner.Groups.Count} groups, planner ready in {clock.Elapsed.TotalSeconds:F1} s");
if (Environment.GetEnvironmentVariable("MEASURE_OBJECT_CODE") is { Length: > 0 } code)
{
    // MEASURE_OBJECT_CODE=room,object,verb(hex): the verb code as the data source decodes it for the planner.
    var p = code.Split(',');
    var data = ScummDataSources.For(game);
    var record = data.ReadRoom(int.Parse(p[0])).Objects.First(o => o.Id == int.Parse(p[1]));
    foreach (var ins in data.ReadObjectVerb(int.Parse(p[0]), record, Convert.ToInt32(p[2], 16), out var entry)!.Instructions.Take(int.TryParse(Environment.GetEnvironmentVariable("MEASURE_TAKE"), out var take) ? take : 6))
        Console.WriteLine($"  {ins.Offset:X4} {ins.Operation} {ins.Operands}");
    var codePlanner = new ScummCutscenePlanner(game);
    foreach (var t in codePlanner.Triggers.Where(t => t.Room == int.Parse(p[0]) && t.ObjectId == int.Parse(p[1]) && t.VerbId == Convert.ToInt32(p[2], 16)))
    {
        foreach (var c in codePlanner.Setup.Candidates(t)) Console.WriteLine($"  candidate {c.Kind} {c.Target} {c.Value}: {c.Describe()}");
        foreach (var c in codePlanner.Setup.GuardValues(t)) Console.WriteLine($"  guard {c.Describe()}");
    }
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_OBJECTS") is { Length: > 0 } objectRoom)
{
    // MEASURE_OBJECTS=room: the room's objects (directory order) and their stored images by state, with where they lie.
    var data = ScummDataSources.For(game);
    var number = int.Parse(objectRoom);
    var records = data.ReadRoom(number).Objects;
    var verbTables = data.ReadObjectVerbs(number, records);
    foreach (var record in records)
    {
        Console.WriteLine($"object {record.Id} at ({record.X},{record.Y}) {record.Width}x{record.Height}");
        foreach (var verb in verbTables.GetValueOrDefault(record.Id) ?? [])
            foreach (var ins in data.ReadObjectVerb(number, record, verb.VerbId, out _).Instructions.Where(i => i.Operation.StartsWith("loadRoom", StringComparison.Ordinal) || i.Operation == "putActorInRoom"))
                Console.WriteLine($"    verb 0x{verb.VerbId:X2} +0x{ins.Offset:X4} {ins.Operation} {ins.Operands}");
    }
    if (data is ScummV5DataSource v5)
        foreach (var state in new byte[] { 0, 1, 2 })
            foreach (var image in v5.ReadObjectImages(number, records.ToDictionary(r => r.Id, _ => state)))
                Console.WriteLine($"  state {state}: image of {image.Id} at ({image.X},{image.Y}) {image.Width}x{image.Height} mask {(image.Mask is null ? "none" : "yes")}");
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_ACTORS") is { Length: > 0 } actorRoom)
{
    // MEASURE_ACTORS=room: the room visited from the boot (as the app's catalog pick of its entry scene), the actors in it after 200 frames.
    var visit = ScummGameSession.BootForPlay(game, out _);
    var target = game.Rooms.First(r => r.ResourceId == int.Parse(actorRoom));
    visit.VisitRoom(target);
    visit.EnterRoomAgain(target);
    for (var f = 0; f < 200; f++) visit.AdvanceFrame();
    var runtime = typeof(ScummSession).GetField("_runtime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(visit)!;
    dynamic actors = runtime.GetType().GetProperty("Actors")!.GetValue(runtime)!;
    foreach (var actor in (IEnumerable<ScummActorSnapshot>)actors.Snapshot())
        if (actor.Room == int.Parse(actorRoom)) Console.WriteLine($"actor {actor.Number}: ({actor.X},{actor.Y}) visible {actor.Visible} costume {actor.Look.Costume} box {actor.WalkBox} ignoreBoxes {actor.IgnoreBoxes} forceClip {actor.ForceClip}");
    if (visit.CurrentWalkBoxes is { } wb) foreach (var b in wb.Boxes) Console.WriteLine($"  box {b.Index} mask {b.Mask} flags {wb.GetFlags(b.Index)} corners {string.Join(" ", b.Corners.Select(c => $"({c.X},{c.Y})"))}");
    foreach (var line in visit.SessionTrace.Where(l => l.Contains("ego") || l.Contains("hero") || l.Contains("putActor")).TakeLast(12)) Console.WriteLine("  | " + (line.Length > 220 ? line[..220] : line));
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_INTRO") is { Length: > 0 } introFrames)
{
    // MEASURE_INTRO=frames: Play Game Intro headless (2 November 2026, user report: the button stays disabled after the intro), its state every 300 frames.
    var intro = ScummGameSession.StartIntro(game, out _);
    for (var f = 1; f <= int.Parse(introFrames); f++)
    {
        intro.AdvanceFrame();
        if (f % 300 == 0) Console.WriteLine($"{f}: room {intro.CurrentRoomId} jiffies {intro.FrameJiffies} cutscene {intro.InCutscene} control {intro.PlayerHasControl} boot {intro.BootRunning} " +
            $"last [{intro.Messages.Log.LastOrDefault()?.Text}] | {intro.SessionTrace.LastOrDefault()}");
    }
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_SCREEN") is { Length: > 0 } screenRoom)
{
    // MEASURE_SCREEN=room: the screen rows after a visit of the room (roomOps setScreen), as the app's visit leaves them.
    var visited = newRun();
    Console.WriteLine($"after boot: {visited.MainScreen}");
    var shown = (System.Collections.IDictionary)typeof(ScummSession).GetField("_bootShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(visited)!;
    foreach (System.Collections.DictionaryEntry e in shown) Console.WriteLine($"  boot showed room {e.Key}: {e.Value} verbs [{string.Join(",", (IEnumerable<int>)e.Value!.GetType().GetField("Item2")!.GetValue(e.Value)!)}]");
    foreach (var line in visited.SessionTrace.Where(l => l.Contains("verbOps") || l.Contains("verb ") && l.Contains("new") || l.Contains("Room 76") || l.Contains("room 76")).Take(15)) Console.WriteLine("  | " + (line.Length > 200 ? line[..200] : line));
    visited.VisitRoom(game.Rooms.First(r => r.ResourceId == int.Parse(screenRoom)));
    Console.WriteLine($"after visiting {screenRoom}: {visited.MainScreen}, room {visited.CurrentRoomId}");
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_VERBS") == "1")
{
    Console.WriteLine($"verb source: {planner.VerbSource}");
    foreach (var v in Enumerable.Range(1, 0x80)) if (planner.Verbs.Word?.Invoke(v) is { } w) Console.WriteLine($"  verb 0x{v:X2} = {w}");
    var boot = ScummGameSession.BootForPlay(game, out _);
    ScummCutscenePlanner.UntilAVerbIsOn(boot);
    foreach (var verb in boot.Verbs.Take(20)) Console.WriteLine($"  session verb 0x{verb.VerbId:X2} mode {verb.Mode} name [{verb.Name}] image {verb.ImageObject}");
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_WINDOWPATH") is { Length: > 0 } wp)
{
    // MEASURE_WINDOWPATH=room,object,verb(hex),reenter(0/1): the window's order for a non-entry scene with a setup (room opened first, setup, entered again).
    var q = wp.Split(',');
    var t = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(c => c.Room == int.Parse(q[0]) && c.ObjectId == int.Parse(q[1]) && c.VerbId == Convert.ToInt32(q[2], 16));
    var plan = planner.PlanSetup(t);
    Console.WriteLine($"plan: way {plan.Result.Way} edits {string.Join("; ", plan.Edits.Select(e => e.Describe()))}");
    var s1 = newRun();
    var room = game.Rooms.First(r => r.ResourceId == t.Room);
    s1.VisitRoom(room);
    s1.HurryUntilCutscene(ScummCutsceneRun.HurriedScripts(game, t), t.ObjectId);
    s1.ArrivalsSkippedFor = t; s1.ArrivalSetup = plan.Edits; s1.WantHeroForScene(t);
    var changed = s1.ApplySceneSetup(plan.Edits).Count;
    if (changed > 0 && q[3] == "1") { s1.EnterRoomAgain(room); s1.ApplySceneSetup(plan.Edits); }
    Console.WriteLine($"changed {changed}, room {s1.CurrentRoomId}");
    {
        var rt = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(s1)!;
        Console.WriteLine($"lights var[0x00C]={rt.GlobalVariables.GetValueOrDefault("var[0x00C]")} var[0x06F]={rt.GlobalVariables.GetValueOrDefault("var[0x06F]")} obj157={rt.ObjectStates.GetValueOrDefault(157)}");
    }
    var meet = ScummCutsceneRun.LetRoomMeetHero(s1, t);
    var before = s1.CutscenesBegun;
    s1.PickInteraction(t.ObjectId, plan.Result.Verb ?? t.VerbId, 0);
    for (var f = 0; f < 400; f++) s1.StepFrame();
    Console.WriteLine($"meet {meet}; cutscenes {s1.CutscenesBegun - before}; room {s1.CurrentRoomId}; rooms [{string.Join(",", s1.RoomsEntered)}]");
    foreach (var line in s1.SessionTrace.TakeLast(25)) Console.WriteLine("  | " + (line.Length > 180 ? line[..180] : line));
    return;
}
// MEASURE_LINES=room,object,verb(hex): the scene played as its plan plays it, then the texts it printed, distinct, with their count and the place that
// printed them (12 November 2026, roadmap step 4: entries whose runs print a line nearly every frame).
if (Environment.GetEnvironmentVariable("MEASURE_LINES") is { Length: > 0 } linesSpec)
{
    var q = linesSpec.Split(',');
    var t = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(c => c.Room == int.Parse(q[0]) && c.ObjectId == int.Parse(q[1]) && c.VerbId == Convert.ToInt32(q[2], 16));
    var plan = planner.PlanSetup(t);
    ScummSession? last = null;
    var bootLines = 0;
    var seen = ScummCutsceneRun.Run(game, () => { last = newRun(); bootLines = last.Messages.Log.Count; return last; }, plan.Result.Verb is int v ? t with { VerbId = v } : t, plan.Edits, plan.Result.Way);
    Console.WriteLine($"way {seen.Way} begun {seen.Begun} lines {seen.Lines} rooms [{string.Join(",", seen.RoomsEntered)}] frames {seen.Frames}");
    // After the boot, in the order printed; a text repeated in a row is shown once with its count.
    var after = last!.Messages.Log.Skip(bootLines).Where(m => m.ShowsText).Select(m => (Text: string.Join(" / ", m.Lines).Trim(), m.SourceLabel, m.Offset)).ToList();
    for (var i = 0; i < after.Count && i < 400;)
    {
        var j = i; while (j < after.Count && after[j] == after[i]) j++;
        Console.WriteLine($"  {(j - i > 1 ? $"{j - i} x " : "")}[{after[i].Text}] from {after[i].SourceLabel} +0x{after[i].Offset:X4}");
        i = j;
    }
    return;
}
// MEASURE_SEQ=room;bit:0xD66=1;verb:834,0x54;frames:3000 (12 November 2026): a LOOM replay's steps on LOOM's session, with the room, the lines and the
// scripts' last trace lines every 300 frames.
if (Environment.GetEnvironmentVariable("MEASURE_SEQ") is { Length: > 0 } seq && loomSnapshot is not null)
{
    var s = new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, loomSnapshot);
    var parts = seq.Split(';');
    s.VisitRoomById(int.Parse(parts[0]));
    var total = 3000;
    foreach (var part in parts.Skip(1))
    {
        var kv = part.Split(':');
        if (kv[0] == "bit") { var b = kv[1].Split('='); s.ApplySceneSetup([new ScummSetupEdit(ScummSetupKind.Bit, $"bit[{b[0]}]", int.Parse(b[1]), "probe")]); }
        else if (kv[0] == "verb") { var v = kv[1].Split(','); var r = s.ActivateObjectVerb(int.Parse(v[0]), Convert.ToInt32(v[1], 16)); Console.WriteLine("verb: " + string.Join(" | ", r.Uncertainties.TakeLast(2))); }
        else if (kv[0] == "frames") total = int.Parse(kv[1]);
    }
    for (var f = 1; f <= total; f++)
    {
        s.AdvanceFrame();
        if (f % 300 == 0) Console.WriteLine("   actors: " + string.Join(" ", s.Actors.Where(a => a.Room == s.CurrentRoomId && a.Visible == true).Select(a => $"{a.Number}:c{a.Look.Costume}@({a.X},{a.Y})")));
        if (f % 300 == 0) Console.WriteLine($"{f}: room {s.CurrentRoomId} cutscene {s.InCutscene} lines {s.Messages.Log.Count} last [{s.Messages.Log.LastOrDefault()?.Text.Replace('\n', ' ')}] | {string.Join(" | ", s.SessionTrace.TakeLast(2))}");
    }
    { var rt = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(s)!; Console.WriteLine($"bits D66 {rt.GlobalVariables.GetValueOrDefault("bit[0xD66]")} D65 {rt.GlobalVariables.GetValueOrDefault("bit[0xD65]")} D64 {rt.GlobalVariables.GetValueOrDefault("bit[0xD64]")} D79 {rt.GlobalVariables.GetValueOrDefault("bit[0xD79]")} pending {s.Snapshot.PendingScripts} | {string.Join(" | ", s.Snapshot.Uncertainties.TakeLast(4))}"); }
    if (Environment.GetEnvironmentVariable("MEASURE_SEQ_TRACE") is { Length: > 0 } tr) foreach (var line in s.SessionTrace.Where(l => l.Contains(tr)).TakeLast(40)) Console.WriteLine("  | " + (line.Length > 220 ? line[..220] : line));
    return;
}
// MEASURE_RUN=room,object,verb(hex);state:565=1;var:var[0x063]=556;class:573=6;bit:bit[0x5CF]=1 (13 November 2026): one run of a scene with exactly
// these setup values, its lines in order (to find a reviewed start).
if (Environment.GetEnvironmentVariable("MEASURE_RUN") is { Length: > 0 } runSpec)
{
    var parts = runSpec.Split(';');
    var q = parts[0].Split(',');
    var t = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(c => c.Room == int.Parse(q[0]) && c.ObjectId == int.Parse(q[1]) && c.VerbId == Convert.ToInt32(q[2], 16));
    var edits = new List<ScummSetupEdit>();
    foreach (var part in parts.Skip(1))
    {
        var kv = part.Split(':', 2); var tv = kv[1].Split('=');
        edits.Add(kv[0] switch
        {
            "state" => new(ScummSetupKind.ObjectState, tv[0], int.Parse(tv[1]), "probe"),
            "var" => new(ScummSetupKind.Variable, tv[0], int.Parse(tv[1]), "probe"),
            "bit" => new(ScummSetupKind.Bit, tv[0], int.Parse(tv[1]), "probe"),
            "class" => new(ScummSetupKind.ObjectClass, tv[0], int.Parse(tv[1]), "probe"),
            "by" => new(ScummSetupKind.EnteredBy, ScummVars.WalkToObject, int.Parse(tv[1]), "probe", t.Room),
            _ => throw new ArgumentException(kv[0]),
        });
    }
    ScummSession? last = null; var bootLines = 0;
    var seen = ScummCutsceneRun.Run(game, () => { last = newRun(); bootLines = last.Messages.Log.Count; return last; }, t, edits);
    Console.WriteLine($"begun {seen.Begun} lines {seen.Lines} rooms [{string.Join(",", seen.RoomsEntered)}] frames {seen.Frames} view {seen.ChangesView} animation {seen.Animation} {seen.Failure}");
    foreach (var m in last!.Messages.Log.Skip(bootLines).Where(m => m.ShowsText).Take(30)) Console.WriteLine($"  [{string.Join(" / ", m.Lines).Trim()}] from {m.SourceLabel}");
    return;
}
// MEASURE_TRIGGERS=1: every trigger the scan found, with its click point (14 November 2026), no runs.
if (Environment.GetEnvironmentVariable("MEASURE_TRIGGERS") == "1")
{
    foreach (var t in planner.Triggers) Console.WriteLine($"TRIGGER Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} click {t.Click} [{t.ObjectName}] scripts {string.Join(" | ", t.Scripts)}");
    return;
}
// MEASURE_LIST=1: every entry with its title, kind, plan (way, lines, rooms, frames), whether Normal offers it, and its first printed text (12 November
// 2026: finding the scenes the user named as ones that matter).
if (Environment.GetEnvironmentVariable("MEASURE_LIST") == "1")
{
    foreach (var g in planner.Groups.Where(g => onlyRoom is not int only || g.First.Room == only))
    {
        var t = g.First;
        var plan = planner.PlanSetup(t);
        Console.WriteLine($"LIST {(planner.IsScene(g) ? "scene" : "react")} {(planner.NotOfferedReason(t) is { } why ? "HIDDEN" : "normal")} Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} " +
                          $"[{ScummCutsceneTitles.Plain(t)}] way {(plan.Result.Begun ? plan.Result.Way.ToString() : "none")} lines {plan.Result.Lines} rooms [{string.Join(",", plan.Result.RoomsEntered)}] frames {plan.Result.Frames} " +
                          $"edits {plan.Edits.Count} ({string.Join("; ", plan.Edits.Select(e => e.Describe()))}) first \"{t.FirstText}\" scripts {string.Join(" | ", t.Scripts)}");
    }
    return;
}
// MEASURE_GREP=text: every instruction of every script, entry, exit and object verb whose operation or operands contain the text (9 November 2026).
if (Environment.GetEnvironmentVariable("MEASURE_GREP") is { Length: > 0 } grep)
{
    var data = ScummDataSources.For(game);
    void Scan(string label, ScummScriptDisassembly? script)
    {
        if (script is null) return;
        foreach (var ins in script.Instructions)
            if ($"{ins.Operation} {ins.Operands}".Contains(grep, StringComparison.Ordinal)) Console.WriteLine($"{label} +0x{ins.Offset:X4} {ins.Operation} {ins.Operands}");
    }
    foreach (var id in data.GlobalScriptIds()) { try { Scan($"Script {id}", data.GlobalScript(id)); } catch (Exception) { } }
    foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
    {
        try
        {
            Scan($"Room {room} entry", data.RoomEntryScript(room));
            Scan($"Room {room} exit", data.RoomExitScript(room));
            foreach (var local in data.RoomLocalScriptIds(room)) Scan($"Room {room} local {local}", data.RoomLocalScript(room, local));
            var objects = data.ReadRoom(room).Objects;
            foreach (var (objectId, verbs) in data.ReadObjectVerbs(room, objects))
                foreach (var verb in verbs)
                    Scan($"Room {room} object {objectId} verb 0x{verb.VerbId:X2}", data.ReadObjectVerb(room, objects.Last(o => o.Id == objectId), verb.VerbId, out _));
        }
        catch (Exception) { }
    }
    return;
}
// MEASURE_ARRIVALS=room:the game's arrivals of the hero into that room (door objects and pseudo room numbers).
if (int.TryParse(Environment.GetEnvironmentVariable("MEASURE_ARRIVALS"), out var arrivalsRoom))
{
    foreach (var arrival in ScummEgoArrivalIndex.For(game).Into(arrivalsRoom)) Console.WriteLine($"arrival {arrival.Describe()}");
    return;
}
if (Environment.GetEnvironmentVariable("MEASURE_LOADS") == "1")
{
    // MEASURE_LOADS=1 (8 November 2026, ROADMAP 3c): what every way into each room needs, and the room loads whose room the read cannot tell.
    foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
        if (planner.Setup.RoomNeeds(room) is { Count: > 0 } needs) Console.WriteLine($"Room {room}: {string.Join("; ", needs.Select(e => e.Describe()))}");
    foreach (var unread in planner.Setup.UnreadRoomLoads()) Console.WriteLine($"unread: {unread}");
    return;
}
int fresh = 0, setup = 0, still = 0, n = 0;
foreach (var group in planner.Groups)
{
    var t = group.First;
    if (onlyRoom is int r && t.Room != r) continue;
    if (n++ >= limit) break;
    var run = planner.Run(t);
    if (Environment.GetEnvironmentVariable("MEASURE_DIRECT") == "1")
    {
        var d = ScummCutsceneRun.Run(game, newRun, t, null, ScummPickedStart.Direct);
        if (d.Begun != run.Begun) Console.WriteLine($"DIFF Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2}: direct {d.Begun} ({d.Failure}), picked {run.Begun} way={run.Way} ({run.Failure})");
        if (run.Begun) fresh++;
        continue;
    }
    if (run.Begun)
    {
        fresh++;
        if (Environment.GetEnvironmentVariable("MEASURE_TRIALS") == "1" && onlyRoom is not null)
        {
            Console.WriteLine($"FRESH Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2}: rooms [{string.Join(",", run.RoomsEntered)}] lines {run.Lines}; candidates: {string.Join(" | ", planner.Setup.Candidates(t).Select(c => c.Describe()))}");
            foreach (var c in planner.Setup.Candidates(t))
            {
                var r1 = ScummCutsceneRun.RunPicked(game, newRun, t, [c]);
                Console.WriteLine($"  trial {c.Describe()}: begun {r1.Begun} rooms [{string.Join(",", r1.RoomsEntered)}] lines {r1.Lines}");
            }
        }
        // MEASURE_ORDER=1 (8 November 2026, ROADMAP 3c second step): a scene a fresh start begins, with the state of its room's ways in and the code before.
        if (Environment.GetEnvironmentVariable("MEASURE_ORDER") == "1" && planner.PlanSetup(t) is { HasEdits: true } placed)
            Console.WriteLine($"PLACED Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName}): {string.Join("; ", placed.Edits.Select(e => e.Describe()))}; " +
                              $"rooms [{string.Join(",", run.RoomsEntered)}] -> [{string.Join(",", placed.Result.RoomsEntered)}] lines {run.Lines} -> {placed.Result.Lines}");
        continue;
    }
    var start = clock.Elapsed;
    var candidates = planner.Setup.Candidates(t);
    if (Environment.GetEnvironmentVariable("MEASURE_TRIALS") == "1")
    {
        Console.WriteLine($"  fresh: begun {run.Begun} rooms [{string.Join(",", run.RoomsEntered)}] lines {run.Lines} frames {run.Frames} failure {run.Failure}");
        foreach (var c in candidates)
        {
            var r1 = ScummCutsceneRun.RunPicked(game, newRun, t, [c]);
            Console.WriteLine($"  trial {c.Describe()}: begun {r1.Begun} rooms [{string.Join(",", r1.RoomsEntered)}] lines {r1.Lines} frames {r1.Frames} failure {r1.Failure}");
        }
        // MEASURE_COMBO=0,2,5: one run with those candidates together (by their index in the list above).
        if (Environment.GetEnvironmentVariable("MEASURE_COMBO") is { Length: > 0 } combo)
        {
            var picked = combo.Split(',').Select(int.Parse).Where(i => i < candidates.Count).Select(i => candidates[i]).ToList();
            ScummSession? last = null;
            var rc = ScummCutsceneRun.RunPicked(game, () => last = newRun(), t, picked);
            Console.WriteLine($"  combo {string.Join(" + ", picked.Select(c => c.Describe()))}: begun {rc.Begun} rooms [{string.Join(",", rc.RoomsEntered)}] lines {rc.Lines} frames {rc.Frames}");
            // MEASURE_COMBO_TRACE=n: the run's last n session trace lines and printed lines (SCUMM_TRACE_SCRIPTS names scripts traced instruction by instruction).
            if (int.TryParse(Environment.GetEnvironmentVariable("MEASURE_COMBO_TRACE"), out var tail) && last is not null)
            {
                foreach (var line in last.SessionTrace.TakeLast(tail)) Console.WriteLine($"    trace {line}");
                foreach (var message in last.Messages.Log) Console.WriteLine($"    printed {message.Text.Replace('\n', ' ')}");
                foreach (var note in last.Snapshot.Uncertainties.TakeLast(8)) Console.WriteLine($"    note {note}");
                var runtime = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(last)!;
                Console.WriteLine($"    walk-to object {runtime.GlobalVariables.GetValueOrDefault(ScummVars.WalkToObject)}, hero costume {runtime.Actors[1].Look.Costume}, room {last.CurrentRoomId}");
            }
        }
        // Pairs, for a few candidates (a scene that needs two values at once).
        if (candidates.Count is > 1 and <= 4)
            for (var i = 0; i < candidates.Count; i++)
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var r2 = ScummCutsceneRun.RunPicked(game, newRun, t, [candidates[i], candidates[j]]);
                    Console.WriteLine($"  pair {candidates[i].Describe()} + {candidates[j].Describe()}: begun {r2.Begun} rooms [{string.Join(",", r2.RoomsEntered)}] lines {r2.Lines}");
                }
    }
    var plan = planner.PlanSetup(t);
    var took = (clock.Elapsed - start).TotalSeconds;
    if (plan.Result.Begun) setup++; else still++;
    Console.WriteLine($"{(plan.Result.Begun ? (plan.Result.Way == ScummPickedStart.AtCutscene ? "ATCUT" : "SETUP") : "STILL")} Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName}) {took:F1} s, {candidates.Count} candidates: " +
                      $"{string.Join(" | ", candidates.Select(c => c.Describe()))}");
    if (Environment.GetEnvironmentVariable("MEASURE_SCRIPTS") == "1")
        Console.WriteLine($"      scripts: {string.Join(" | ", t.Scripts)}; cutscene: {ScummSceneSetupSearch.FirstCutscene(game, t)}");
    Console.WriteLine($"      result: way {plan.Result.Way} lines {plan.Result.Lines} rooms [{string.Join(",", plan.Result.RoomsEntered)}] frames {plan.Result.Frames}");
    if (plan.HasEdits) Console.WriteLine($"      kept: {string.Join("; ", plan.Edits.Select(e => e.Describe()))}; rooms [{string.Join(",", plan.Result.RoomsEntered)}] lines {plan.Result.Lines}");
    // MEASURE_ORDER=1 (8 November 2026, ROADMAP 3c): for each flag the setup needs, the scenes that set it, and what the earlier scenes would add.
    if (Environment.GetEnvironmentVariable("MEASURE_ORDER") == "1" && plan.HasEdits)
    {
        foreach (var need in plan.Edits.Where(e => e.Kind is ScummSetupKind.Bit or ScummSetupKind.Variable))
            Console.WriteLine($"      order: {need.Describe()} set by [{string.Join(", ", planner.Setup.CodeSetting(need.Kind, need.Target, need.Value))}]");
        Console.WriteLine($"      order: earlier code adds {string.Join("; ", planner.Setup.EarlierScenesState(t, plan.Edits.Where(e => !e.Where.Contains("earlier code")).ToList()).Select(e => e.Describe()))}");
    }
}
// R-8.10 (9 November 2026): each entry's kind from its run, and what decided it.
var kinds = 0;
foreach (var g in planner.Groups.Where(g => onlyRoom is not int only || g.First.Room == only).Take(limit))
{
    var shown = planner.PlanSetup(g.First).Result;
    var cut = planner.IsScene(g);
    if (cut) kinds++;
    if (Environment.GetEnvironmentVariable("MEASURE_KINDS") == "1")
        Console.WriteLine($"KIND {(cut ? "cutscene" : "reaction")}{(g.First.Widened ? " widened" : "")} way {(shown.Begun ? shown.Way.ToString() : "none")} Room {g.First.Room:00} object {g.First.ObjectId} verb 0x{g.First.VerbId:X2} ({g.First.ObjectName}): rooms [{string.Join(",", shown.RoomsEntered)}] loads {shown.ChangesRoom} view {shown.ChangesView} lines {shown.Lines} animation {shown.Animation ?? "-"}");
}
var listed = planner.Groups.Where(g => onlyRoom is not int only || g.First.Room == only).Take(limit).ToList();
Console.WriteLine($"cutscenes {kinds} of {listed.Count}; old rule Scenes {listed.Count(g => !g.First.Widened && g.IsScene)} of {listed.Count(g => !g.First.Widened)} (widened candidates {listed.Count(g => g.First.Widened)}, of them cutscenes {listed.Count(g => g.First.Widened && planner.IsScene(g))})");
Console.WriteLine($"fresh start begins {fresh}; setup begins {setup}; still nothing {still}; {clock.Elapsed.TotalSeconds:F0} s");
// 10 November 2026 (audit, roadmap step 2): the picture checks. The planner plays every scene that begins once more as its plan plays it, watched by
// ScummScenePictureCheck, and keeps what it found with the plan (what Advanced shows); a scene with a finding is a suspect (a list for a person to
// look at, not a list of faults). MEASURE_PICTURE=0 leaves the list out.
if (Environment.GetEnvironmentVariable("MEASURE_PICTURE") != "0")
{
    int watched = 0, suspects = 0;
    var byKind = new SortedDictionary<ScummPictureSuspectKind, int>();
    foreach (var g in listed)
    {
        var t = g.First;
        var plan = planner.PlanSetup(t);
        if (!plan.Result.Begun) continue;
        var seen = plan.Result;
        watched++;
        var found = seen.Picture ?? [];
        if (found.Count == 0) continue;
        suspects++;
        foreach (var kind in found.Select(f => f.Kind).Distinct()) byKind[kind] = byKind.GetValueOrDefault(kind) + 1;
        Console.WriteLine($"PICTURE Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName}) way {plan.Result.Way}" +
                          $"{(plan.HasEdits ? " setup: " + string.Join("; ", plan.Edits.Select(e => e.Describe())) : "")}; ends in Room {string.Join(",", seen.RoomsEntered.DefaultIfEmpty(t.Room).TakeLast(1))}, lines {seen.Lines}, frames {seen.Frames}");
        foreach (var f in found) Console.WriteLine($"      {f.Describe()}");
    }
    string Percent(int count) => watched == 0 ? "-" : $"{100.0 * count / watched:F1}%";
    Console.WriteLine($"picture: {suspects} of {watched} scenes that begin are suspects ({Percent(suspects)})");
    foreach (var kind in Enum.GetValues<ScummPictureSuspectKind>())
        Console.WriteLine($"picture: {kind} {byKind.GetValueOrDefault(kind)} ({Percent(byKind.GetValueOrDefault(kind))})");
}
// 12 November 2026 (roadmap step 4, R-8.42): the scenes a room's list offers in Normal mode, and those it leaves out (shown in Advanced, labelled),
// by reason. The scenes are the entries the rooms list (IsScene); dialogue and reactions are Interact's.
{
    var scenes = listed.Where(planner.IsScene).ToList();
    var hidden = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var g in scenes)
    {
        planner.PlanSetup(g.First);
        if (planner.NotOfferedReason(g.First) is not { } reason) continue;
        var kind = reason.StartsWith("the picture check", StringComparison.Ordinal) ? "a picture suspect" : reason;
        hidden[kind] = hidden.GetValueOrDefault(kind) + 1;
        Console.WriteLine($"NORMAL hidden Room {g.First.Room:00} object {g.First.ObjectId} verb 0x{g.First.VerbId:X2} ({g.First.ObjectName}): {reason}");
    }
    string P(int count) => scenes.Count == 0 ? "-" : $"{100.0 * count / scenes.Count:F1}%";
    var left = hidden.Values.Sum();
    Console.WriteLine($"normal: {scenes.Count - left} of {scenes.Count} scenes offered ({P(scenes.Count - left)}); not offered {left} ({P(left)})");
    foreach (var (reason, count) in hidden) Console.WriteLine($"normal: not offered, {reason}: {count} ({P(count)})");
}

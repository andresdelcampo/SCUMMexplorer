using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// CutsceneList <folder> loom: every scanned LOOM trigger group (EGA or CD) with its title, category and scan fields, one line each.
// Used before and after the S7 move of the scan to Core/Scumm (17 October 2026): the two outputs must be identical.
// S9 MonkeyCd (18 October 2026): "path|SPANISH" reads one language folder of a multi-language disc (GameFolderScanner.DiscFolder).
var game = ScanArg(args[0]);
var data = ScummDataSources.For(game);
var rooms = game.Rooms.Select(r => r.ResourceId!.Value).ToList();
if (args.Length > 1 && args[1] == "loom")
{
    var triggers = LoomCutsceneVerbs.Scan(data, rooms, LoomCutsceneTitles.Doorways(game), game);
    var groups = ScummCutsceneIndex.Group(triggers);
    Console.WriteLine($"triggers {triggers.Count} groups {groups.Count} scenes {groups.Count(g => g.IsScene)}");
    foreach (var g in groups)
    {
        var f = g.First;
        Console.WriteLine($"{f.Room}/{f.ObjectId}/0x{f.VerbId:X2} | {LoomCutsceneTitles.Title(f)} | {(g.IsScene ? "Scenes" : "Dialogue")} | {f.VerbLabel} | " +
                          $"{string.Join(";", f.Scripts)} | t{f.Texts} l{f.RoomLoads} i{f.SpanInstructions} a{f.LoadsAfter} | {f.FirstText} | " +
                          $"to[{string.Join(",", f.LeadsTo)}] lt[{string.Join(",", f.LoadTargets ?? [])}] | others[{string.Join(",", g.Others.Select(o => $"{o.Room}/{o.ObjectId}/0x{o.VerbId:X2} {o.VerbLabel}"))}]");
    }
    return;
}

// CutsceneList <folder> groups <room>: the scenes the App lists under a room, with where each was found (2 November 2026, user report on Indy 3 Room 83).
if (args.Length > 2 && args[1] == "groups")
{
    var listed = new ScummCutscenePlanner(game, start: ScummCutsceneStart.BootPerRun).Groups.Where(g => args[2] == "all" || g.First.Room == int.Parse(args[2]));
    foreach (var g in listed)
        Console.WriteLine($"{g.First.Room}/{g.First.ObjectId}/0x{g.First.VerbId:X2} {g.First.VerbLabel} | {string.Join(";", g.First.Scripts)} | t{g.First.Texts} | {g.First.FirstText} | lt[{string.Join(",", g.First.LoadTargets ?? [])}]");
    return;
}

// CutsceneList <folder> probe <room> <object> <verb>: one fresh run of a trigger (object 0 = the room's entry) with the session trace's cutscene lines
// after the visit (Monkey Island floppy S7, 17 October 2026: which script began the cutscene a run counted).
// CutsceneList <folder> prun <workers>: every group's fresh run on several threads, each with its own copy of the game (its own disk or archive
// reader), as the App derives a disk game's list since 17 October 2026 (Monkey Island floppy S7); the lines must equal the single-threaded "scan run".
if (args.Length > 2 && args[1] == "prun")
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var copies = new ThreadLocal<GameLibrary>(() => ScanArg(args[0]));
    // Since the cutscene snapshot (18 October 2026) the runs start from a pool of boots, each of its own copy of the game, as the App does;
    // "prun <workers> boot" boots for every run on the worker's own copy, as before.
    var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
    var planner = args.Length > 3 && args[3] == "boot"
        ? new ScummCutscenePlanner(game, () => ScummGameSession.BootForPlay(copies.Value!, out _), start: ScummCutsceneStart.BootPerRun)
        : PooledPlanner();
    // As the App: the planner's copy of the game also serves the pool's first boot.
    ScummCutscenePlanner PooledPlanner()
    {
        GameLibrary? first = game;
        return new ScummCutscenePlanner(game, () => ScummGameSession.BootForPlay(Interlocked.Exchange(ref first, null) ?? ScanArg(args[0]), out _),
            start: ScummCutsceneStart.SnapshotPerWorker);
    }
    // As the App since 19 October 2026: entries handed out one at a time ("boot": the list's default fixed ranges, as before).
    var options = new ParallelOptions { MaxDegreeOfParallelism = int.Parse(args[2]) };
    if (args.Length > 3 && args[3] == "boot") Parallel.ForEach(planner.Groups, options, g => planner.Run(g.First));
    else Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(planner.Groups, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), options, g => planner.Run(g.First));
    Console.WriteLine($"{planner.Groups.Count} runs on {args[2]} workers: {clock.ElapsedMilliseconds} ms, begun {planner.Groups.Count(g => planner.Run(g.First).Begun)}; " +
                      $"CPU {(System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalSeconds:0} s; boots {planner.PoolBoots?.ToString() ?? "one per run"}");
    foreach (var g in planner.Groups)
    {
        var r = planner.Run(g.First);
        Console.WriteLine($"{g.First.Room}/{g.First.ObjectId}/0x{g.First.VerbId:X2} | run: {(r.Begun ? "begun" : "NOT begun")} frames {r.Frames} rooms [{string.Join(",", r.RoomsEntered)}]");
    }
    return;
}

// CutsceneList <folder> snap: the cost of a boot, of keeping its snapshot and of one copy (cutscene snapshot, 18 October 2026).
if (args.Length > 1 && args[1] == "snap")
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var booted = ScummGameSession.BootForPlay(game, out _);
    var boot = clock.ElapsedMilliseconds; clock.Restart();
    var snapshot = ScummBootSnapshot.Of(booted);
    var keep = clock.ElapsedMilliseconds; clock.Restart();
    for (var i = 0; i < 20; i++) snapshot.Start();
    Console.WriteLine($"boot {boot} ms, snapshot {keep} ms, copy {clock.ElapsedMilliseconds / 20.0:0.0} ms (mean of 20)");
    return;
}

// CutsceneList <folder> bench <n>: n entries spread over the list, each run once after its own boot and once from a copy of one boot, alternately
// on one thread, with the time of each part (cutscene snapshot, 18 October 2026: the machine is shared, so both kinds are timed side by side).
if (args.Length > 2 && args[1] == "bench")
{
    var booting = new ScummCutscenePlanner(game, start: ScummCutsceneStart.BootPerRun);
    var copying = new ScummCutscenePlanner(ScanArg(args[0]));
    var step = Math.Max(1, booting.Groups.Count / int.Parse(args[2]));
    long bootRuns = 0, copyRuns = 0, boots = 0, copies = 0;
    var clock = new System.Diagnostics.Stopwatch();
    var snapshot = ScummBootSnapshot.Of(ScummGameSession.BootForPlay(game, out _));
    foreach (var g in booting.Groups.Where((_, i) => i % step == 0))
    {
        clock.Restart(); ScummGameSession.BootForPlay(game, out _); boots += clock.ElapsedMilliseconds;
        clock.Restart(); snapshot.Start(); copies += clock.ElapsedMilliseconds;
        clock.Restart(); var a = booting.Run(g.First); bootRuns += clock.ElapsedMilliseconds;
        clock.Restart(); var b = copying.Run(copying.Groups.First(c => c.First.Room == g.First.Room && c.First.ObjectId == g.First.ObjectId && c.First.VerbId == g.First.VerbId).First); copyRuns += clock.ElapsedMilliseconds;
        if (a != b && !(a.Begun == b.Begun && a.Frames == b.Frames && a.RoomsEntered.SequenceEqual(b.RoomsEntered))) Console.WriteLine($"DIFFERENT {g.First.Room}/{g.First.ObjectId}/0x{g.First.VerbId:X2}");
    }
    Console.WriteLine($"{booting.Groups.Count / step} entries: runs with their own boot {bootRuns} ms, runs from a copy {copyRuns} ms; a boot alone {boots} ms, a copy alone {copies} ms");
    return;
}

// CutsceneList <folder> rooms <workers> <room>...: the groups of the named rooms, on that many workers from a pool of boots, with the time of each
// run, the process's CPU time and the garbage collector's pauses (cutscene snapshot, 19 October 2026: whether long runs gain from running together).
if (args.Length > 3 && args[1] == "rooms")
{
    var planner = new ScummCutscenePlanner(game, () => ScummGameSession.BootForPlay(ScanArg(args[0]), out _), start: ScummCutsceneStart.SnapshotPerWorker);
    var only = args.Skip(3).Select(int.Parse).ToHashSet();
    var groups = planner.Groups.Where(g => only.Contains(g.First.Room)).ToList();
    var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
    var pause = GC.GetTotalPauseDuration();
    var gen2 = GC.CollectionCount(2);
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var times = new System.Collections.Concurrent.ConcurrentBag<string>();
    Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(groups, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering),
        new ParallelOptions { MaxDegreeOfParallelism = int.Parse(args[2]) }, g =>
        {
            var one = System.Diagnostics.Stopwatch.StartNew();
            var r = planner.Run(g.First);
            times.Add($"{g.First.Room}/{g.First.ObjectId}/0x{g.First.VerbId:X2} frames {r.Frames} {one.ElapsedMilliseconds} ms");
        });
    Console.WriteLine($"{groups.Count} runs on {args[2]} workers: {clock.ElapsedMilliseconds} ms; CPU {(System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalSeconds:0.0} s; " +
                      $"GC pauses {(GC.GetTotalPauseDuration() - pause).TotalSeconds:0.0} s, gen2 {GC.CollectionCount(2) - gen2}, server GC {System.Runtime.GCSettings.IsServerGC}, allocated {GC.GetTotalAllocatedBytes() / 1048576} MB");
    foreach (var t in times.Order()) Console.WriteLine(t);
    return;
}

// CutsceneList <folder> alloc <room> <object> <verb> <frames> [advance]: the bytes one run's frames allocate, by what the run asks the session each frame
// (cutscene snapshot, 19 October 2026: Indy 4's long runs allocated about 16 GB each).
if (args.Length > 5 && args[1] == "alloc")
{
    var room = int.Parse(args[2]); var obj = int.Parse(args[3]); var verb = Convert.ToInt32(args[4], 16);
    // A sixth argument "advance" measures AdvanceFrame (its snapshot included); by default the frame the cutscene runs play (StepFrame, 19 October 2026).
    var step = !(args.Length > 6 && args[6] == "advance");
    var session = ScummGameSession.BootForPlay(game, out _);
    var roomInfo = game.Rooms.First(r => r.ResourceId == room);
    if (obj == 0) session.EnterRoomAgain(roomInfo); else { session.VisitRoom(roomInfo); session.ActivateObjectVerb(obj, verb); }
    long snapshotBytes = 0, frame = 0, begun = 0, inCutscene = 0, haveMsg = 0, trace = 0;
    long Bytes() => GC.GetAllocatedBytesForCurrentThread();
    using var ticks = new AllocationTicks();
    for (var i = 0; i < int.Parse(args[5]); i++)
    {
        var at = Bytes(); _ = session.CutscenesBegun; begun += Bytes() - at;
        at = Bytes(); _ = session.InCutscene; inCutscene += Bytes() - at;
        at = Bytes(); _ = session.Messages.HaveMsg; haveMsg += Bytes() - at;
        at = Bytes(); _ = session.SessionTrace.Count; trace += Bytes() - at;
        at = Bytes(); if (step) session.StepFrame(); else session.AdvanceFrame(); frame += Bytes() - at;
        at = Bytes(); _ = session.Snapshot; snapshotBytes += Bytes() - at;
    }
    Console.WriteLine($"per frame: AdvanceFrame {frame / int.Parse(args[5])} B, CutscenesBegun {begun / int.Parse(args[5])} B, InCutscene {inCutscene / int.Parse(args[5])} B, " +
                      $"Messages.HaveMsg {haveMsg / int.Parse(args[5])} B, SessionTrace.Count {trace / int.Parse(args[5])} B, the Snapshot property alone {snapshotBytes / int.Parse(args[5])} B; uncertainties {session.Snapshot.Uncertainties.Count}, variables {session.Snapshot.KnownGlobalVariables.Count}; trace lines {session.SessionTrace.Count}");
    // Then the same number of frames again, timed, without the per-question measurement (19 October 2026).
    var timed = System.Diagnostics.Stopwatch.StartNew();
    for (var i = 0; i < int.Parse(args[5]); i++) { if (step) session.StepFrame(); else session.AdvanceFrame(); }
    Console.WriteLine($"{args[5]} more frames: {timed.Elapsed.TotalMilliseconds:0} ms");
    foreach (var (type, bytes) in ticks.Top(15)) Console.WriteLine($"   {bytes / 1048576.0,9:0.0} MB sampled  {type}");
    return;
}

// CutsceneList <folder> race <n>: n planners built at once, each on its own copy of the game (S7 MonkeyCd, 18 October 2026: the window counted 164
// steps for a list of 160 entries); prints each planner's trigger and group counts.
if (args.Length > 2 && args[1] == "race")
{
    var counts = new System.Collections.Concurrent.ConcurrentBag<string>();
    Parallel.For(0, int.Parse(args[2]), new ParallelOptions { MaxDegreeOfParallelism = int.Parse(args[2]) }, i =>
    {
        var copy = ScanArg(args[0]);
        var p = new ScummCutscenePlanner(copy);
        counts.Add($"triggers {p.Triggers.Count} groups {p.Groups.Count} scenes {p.Groups.Count(g => g.IsScene)}");
    });
    foreach (var c in counts.GroupBy(c => c)) Console.WriteLine($"{c.Count()} x {c.Key}");
    return;
}

// CutsceneList <folder> skip: the boot, then Esc whenever the game allows a skip, until a verb is on (S7 Indy4, 18 October 2026: whether the game's own
// skips from the boot reach the full verb interface the titles need).
if (args.Length > 1 && args[1] == "skip")
{
    var session = ScummGameSession.BootForPlay(game, out _);
    Console.WriteLine($"boot: room {session.CurrentRoomId}, verbs on {session.Verbs.Count(v => v.Mode == 1)}, skippable {session.CutsceneSkippable}");
    var frame = 0; var presses = 0;
    for (; frame < 6000 && !(session.Verbs.Any(v => v.Mode == 1) && session.UserInputOn && !session.InCutscene); frame++)
    {
        if (session.CutsceneSkippable) { session.PressKey(0x1B); presses++; Console.WriteLine($"frame {frame}: Esc in room {session.CurrentRoomId}"); }
        session.AdvanceFrame();
    }
    Console.WriteLine($"end: frame {frame}, room {session.CurrentRoomId}, Esc {presses}, verbs on {session.Verbs.Count(v => v.Mode == 1)}");
    Console.WriteLine($"verb words: {string.Join(", ", Enumerable.Range(0, 256).Select(v => (v, w: ScummCutscenePlanner.VerbsOf(session).Word!(v))).Where(p => p.w is not null).Select(p => $"0x{p.v:X2} {p.w}"))}");
    return;
}

// CutsceneList <folder> dump <room> <object> <verb>: the object verb's instructions (Monkey Island floppy S7, 17 October 2026).
if (args.Length > 4 && args[1] == "dump")
{
    var room = int.Parse(args[2]); var obj = int.Parse(args[3]); var verb = Convert.ToInt32(args[4], 16);
    // A sixth argument "advance" measures AdvanceFrame (its snapshot included); by default the frame the cutscene runs play (StepFrame, 19 October 2026).
    var step = !(args.Length > 6 && args[6] == "advance");
    var roomData = data.ReadRoom(room);
    var script = data.ReadObjectVerb(room, roomData.Objects.First(o => o.Id == obj), verb, out _)!;
    foreach (var i in script.Instructions) Console.WriteLine($"+0x{i.Offset:X4} {i.Operation} {i.Operands}");
    return;
}

// CutsceneList <folder> script <room|0> <id|entry>: every instruction of a room's local script or entry script, or (room 0) a global script
// (S7 Monkey2, 18 October 2026: why an entry began no cutscene from a fresh start).
if (args.Length > 3 && args[1] == "script")
{
    var room = int.Parse(args[2]);
    var script = room == 0 ? data.GlobalScript(int.Parse(args[3])) : args[3] == "entry" ? data.RoomEntryScript(room) : data.RoomLocalScript(room, int.Parse(args[3]));
    foreach (var i in script?.Instructions ?? []) Console.WriteLine($"+0x{i.Offset:X4} {i.Operation} {i.Operands}");
    return;
}

// CutsceneList <folder> find <text>: every instruction of the global, entry and local scripts whose operation and operands contain the text
// (S7 Monkey2, 18 October 2026: which scripts ask a song for its beat).
if (args.Length > 2 && args[1] == "find")
{
    void Find(string label, ScummScriptDisassembly? script)
    {
        foreach (var i in script?.Instructions ?? [])
            if ($"{i.Operation} {i.Operands}".Contains(args[2], StringComparison.Ordinal)) Console.WriteLine($"{label} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    foreach (var id in data.GlobalScriptIds()) Find($"Script {id}", data.GlobalScript(id));
    foreach (var room in rooms)
    {
        Find($"Room {room} entry", data.RoomEntryScript(room));
        foreach (var id in data.RoomLocalScriptIds(room)) Find($"Room {room} local script {id}", data.RoomLocalScript(room, id));
    }
    return;
}

// CutsceneList <folder> tail<room> <object> <verb> <frames>: one fresh run of a trigger for that many frames, then the session's state and the last
// distinct trace lines (S7 Monkey2, 18 October 2026: what a scene that never ends headless waits on).
if (args.Length > 5 && args[1] == "tail")
{
    var room = int.Parse(args[2]); var obj = int.Parse(args[3]); var verb = Convert.ToInt32(args[4], 16);
    // A sixth argument "advance" measures AdvanceFrame (its snapshot included); by default the frame the cutscene runs play (StepFrame, 19 October 2026).
    var step = !(args.Length > 6 && args[6] == "advance");
    var session = ScummGameSession.BootForPlay(game, out _);
    var roomInfo = game.Rooms.First(r => r.ResourceId == room);
    if (obj == 0) session.EnterRoomAgain(roomInfo); else { session.VisitRoom(roomInfo); session.ActivateObjectVerb(obj, verb); }
    for (var frame = 0; frame < int.Parse(args[5]); frame++) session.AdvanceFrame();
    Console.WriteLine($"rooms [{string.Join(",", session.RoomsEntered.TakeLast(3))}] in cutscene {session.InCutscene} haveMsg {session.Messages.HaveMsg} input {session.UserInputOn}");
    foreach (var t in session.SessionTrace.TakeLast(400).Distinct().TakeLast(40)) Console.WriteLine(t);
    foreach (var p in session.PendingScriptSummary) Console.WriteLine("pending: " + p);
    return;
}

// CutsceneList <folder> local <room> <id>: a room-local script's print instructions, raw and as the scan's first-text decoding reads them
// (S9 MonkeyFloppy, 17 October 2026: which escaped bytes changed a line count when the scan moved to ScummTextDecoder.DecodeLine).
if (args.Length > 3 && args[1] == "local")
{
    var script = data.RoomLocalScript(int.Parse(args[2]), int.Parse(args[3]))!;
    foreach (var i in script.Instructions.Where(i => i.Operation == "print"))
        Console.WriteLine($"+0x{i.Offset:X4} {i.Operands}\n        => {string.Join(" / ", System.Text.RegularExpressions.Regex.Matches(i.Operands, @"text, ""(.*?)""").Select(m => ScummTextDecoder.DecodeLine(m.Groups[1].Value).Trim()))}");
    return;
}

if (args.Length > 4 && args[1] == "probe")
{
    var room = int.Parse(args[2]); var obj = int.Parse(args[3]); var verb = Convert.ToInt32(args[4], 16);
    // A sixth argument "advance" measures AdvanceFrame (its snapshot included); by default the frame the cutscene runs play (StepFrame, 19 October 2026).
    var step = !(args.Length > 6 && args[6] == "advance");
    var session = ScummGameSession.BootForPlay(game, out _);
    var roomInfo = game.Rooms.First(r => r.ResourceId == room);
    if (obj == 0) { var at = session.SessionTrace.Count; session.EnterRoomAgain(roomInfo); Print(at, "entry"); }
    else
    {
        var at = session.SessionTrace.Count; session.VisitRoom(roomInfo); Print(at, $"visit (in cutscene {session.InCutscene})");
        at = session.SessionTrace.Count; session.ActivateObjectVerb(obj, verb); Print(at, "verb");
    }
    for (var frame = 0; frame < 700; frame++)
    {
        var at = session.SessionTrace.Count; session.AdvanceFrame();
        if (session.SessionTrace.Skip(at).Any(t => t.Contains("cutscene", StringComparison.OrdinalIgnoreCase) || t.Contains("Room", StringComparison.Ordinal) && t.Contains("enter", StringComparison.OrdinalIgnoreCase))) Print(at, $"frame {frame}");
    }
    void Print(int from, string what)
    {
        foreach (var t in session.SessionTrace.Skip(from).Where(t => t.Contains("cutscene", StringComparison.OrdinalIgnoreCase) || t.Contains("enter", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"{what}: {t}");
    }
    return;
}

// CutsceneList <folder> scan [run] [room...]: the generic list of a game played through ScummGameSession (S7): triggers, groups, titles with the game's
// verb names; with "run", a fresh headless run of each group (only the named rooms' groups when rooms are given), timed.
if (args.Length > 1 && args[1] == "scan")
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    // The Maniac Mansion families start from the reviewed character menu, as the App's planner does (cutscene snapshot, 18 October 2026).
    var planner = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced
        ? SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(game) : new ScummCutscenePlanner(game);
    Console.WriteLine($"planner (one boot and the scan) {clock.ElapsedMilliseconds} ms");
    if (planner.VerbSource is { } verbSource) Console.WriteLine("verb source: " + verbSource);
    Console.WriteLine($"verb words: {string.Join(", ", Enumerable.Range(0, 256).Select(v => (v, w: planner.Verbs.Word!(v))).Where(p => p.w is not null).Select(p => $"0x{p.v:X2} {p.w}"))}");
    var groups = planner.Groups;
    Console.WriteLine($"triggers {planner.Triggers.Count} groups {groups.Count} scenes {groups.Count(g => g.IsScene)}");
    var run = args.Length > 2 && args[2] == "run";
    var only = args.Skip(3).Select(int.Parse).ToHashSet();
    int begun = 0, ran = 0;
    foreach (var g in groups)
    {
        var f = g.First;
        var line = $"{f.Room}/{f.ObjectId}/0x{f.VerbId:X2} | {ScummCutsceneTitles.Title(f)} | {(g.IsScene ? "Scenes" : "Dialogue")} | {string.Join(";", f.Scripts)} | t{f.Texts} l{f.RoomLoads} i{f.SpanInstructions} a{f.LoadsAfter} | {f.FirstText} | others[{string.Join(",", g.Others.Select(o => $"{o.Room}/{o.ObjectId}/0x{o.VerbId:X2}"))}]";
        if (run && (only.Count == 0 || only.Contains(f.Room)))
        {
            clock.Restart();
            var r = planner.Run(f);
            ran++; if (r.Begun) begun++;
            line += $" | run: {(r.Begun ? "begun" : "NOT begun")} frames {r.Frames} rooms [{string.Join(",", r.RoomsEntered)}] {clock.ElapsedMilliseconds} ms{(r.Failure is null ? "" : " failure: " + r.Failure)}";
        }
        Console.WriteLine(line);
    }
    if (run) Console.WriteLine($"runs {ran}, begun {begun}");
}

static GameLibrary ScanArg(string arg) => arg.Split('|') is [var path, var disc] ? new GameFolderScanner { DiscFolder = disc }.Scan(path) : new GameFolderScanner().Scan(arg);

/// <summary>The runtime's allocation ticks (one about every 100 KB allocated), summed by allocated type: which types a run's frames allocate.</summary>
sealed class AllocationTicks : System.Diagnostics.Tracing.EventListener
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _bytes = new();

    protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
    }

    protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick", StringComparison.Ordinal) != true || e.Payload is null) return;
        var type = e.PayloadNames!.IndexOf("TypeName") is >= 0 and var t ? e.Payload[t]?.ToString() ?? "?" : "?";
        var amount = e.PayloadNames.IndexOf("AllocationAmount64") is >= 0 and var a ? Convert.ToInt64(e.Payload[a]) : 100_000;
        _bytes.AddOrUpdate(type, amount, (_, sum) => sum + amount);
    }

    public IEnumerable<(string Type, long Bytes)> Top(int count) => _bytes.OrderByDescending(p => p.Value).Take(count).Select(p => (p.Key, p.Value));
}

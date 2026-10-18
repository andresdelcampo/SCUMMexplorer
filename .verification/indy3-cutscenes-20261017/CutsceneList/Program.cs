using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// CutsceneList <folder> loom: every scanned LOOM trigger group (EGA or CD) with its title, category and scan fields, one line each.
// Used before and after the S7 move of the scan to Core/Scumm (17 October 2026): the two outputs must be identical.
var game = new GameFolderScanner().Scan(args[0]);
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

// CutsceneList <folder> probe <room> <object> <verb>: one fresh run of a trigger (object 0 = the room's entry) with the session trace's cutscene lines
// after the visit (Monkey Island floppy S7, 17 October 2026: which script began the cutscene a run counted).
// CutsceneList <folder> prun <workers>: every group's fresh run on several threads, each with its own copy of the game (its own disk or archive
// reader), as the App derives a disk game's list since 17 October 2026 (Monkey Island floppy S7); the lines must equal the single-threaded "scan run".
if (args.Length > 2 && args[1] == "prun")
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var copies = new ThreadLocal<GameLibrary>(() => new GameFolderScanner().Scan(args[0]));
    var planner = new ScummCutscenePlanner(game, () => ScummGameSession.BootForPlay(copies.Value!, out _));
    Parallel.ForEach(planner.Groups, new ParallelOptions { MaxDegreeOfParallelism = int.Parse(args[2]) }, g => planner.Run(g.First));
    Console.WriteLine($"{planner.Groups.Count} runs on {args[2]} workers: {clock.ElapsedMilliseconds} ms, begun {planner.Groups.Count(g => planner.Run(g.First).Begun)}");
    foreach (var g in planner.Groups)
    {
        var r = planner.Run(g.First);
        Console.WriteLine($"{g.First.Room}/{g.First.ObjectId}/0x{g.First.VerbId:X2} | run: {(r.Begun ? "begun" : "NOT begun")} frames {r.Frames} rooms [{string.Join(",", r.RoomsEntered)}]");
    }
    return;
}

// CutsceneList <folder> race <n>: n planners built at once, each on its own copy of the game (S7 MonkeyCd, 18 October 2026: the window counted 164
// steps for a list of 160 entries); prints each planner's trigger and group counts.
if (args.Length > 2 && args[1] == "race")
{
    var counts = new System.Collections.Concurrent.ConcurrentBag<string>();
    Parallel.For(0, int.Parse(args[2]), new ParallelOptions { MaxDegreeOfParallelism = int.Parse(args[2]) }, i =>
    {
        var copy = new GameFolderScanner().Scan(args[0]);
        var p = new ScummCutscenePlanner(copy);
        counts.Add($"triggers {p.Triggers.Count} groups {p.Groups.Count} scenes {p.Groups.Count(g => g.IsScene)}");
    });
    foreach (var c in counts.GroupBy(c => c)) Console.WriteLine($"{c.Count()} x {c.Key}");
    return;
}

// CutsceneList <folder> dump <room> <object> <verb>: the object verb's instructions (Monkey Island floppy S7, 17 October 2026).
if (args.Length > 4 && args[1] == "dump")
{
    var room = int.Parse(args[2]); var obj = int.Parse(args[3]); var verb = Convert.ToInt32(args[4], 16);
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
    var planner = new ScummCutscenePlanner(game);
    Console.WriteLine($"planner (one boot and the scan) {clock.ElapsedMilliseconds} ms");
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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// Usage: EgaSeedStudy <EGA game folder> <output json>
// Read-only study (no shipped data): times the live derivation the app does for an EGA edition without a catalog (phase 1 = one fresh headless run per scene in
// parallel on half the cores, phase 2 = the planner's derived starts and runs) and dumps the derived results per group, for comparing editions by room+verb+script.
if (args.Length > 2 && args[2] == "probe") // EgaSeedStudy <folder> - probe <room> <box>: fresh EGA session, enter the room, place the ego in the box, and report what the session does
{
    var g2 = new GameFolderScanner().Scan(args[0]);
    var s0 = new LoomExplorationSession(g2, new LflObjectStateReader().ReadSnapshot(Path.Combine(g2.FolderPath, "00.LFL")));
    s0.VisitRoom(g2.Rooms.First(r => r.ResourceId == int.Parse(args[3])));
    for (var i = 0; i < 20; i++) s0.AdvanceFrame();
    var ego = s0.Actors.First(a => a.Number == 1);
    Console.WriteLine($"room {s0.Snapshot.RoomId} ego x={ego.X} y={ego.Y} box={ego.WalkBox} boxes={(s0.CurrentWalkBoxes?.Boxes.Count.ToString() ?? "null")} cutscenes={s0.CutscenesBegun} inCutscene={s0.InCutscene}");
    Console.WriteLine("status: " + s0.GameStatus.ToString()[..Math.Min(300, s0.GameStatus.ToString().Length)]);
    var bx = s0.CurrentWalkBoxes!.Coordinates(int.Parse(args[4]))!;
    var x = bx.Corners.Sum(c => c.X) / 4; var y = bx.Corners.Sum(c => c.Y) / 4;
    s0.PlaceEgo(x, y);
    ego = s0.Actors.First(a => a.Number == 1);
    Console.WriteLine($"placed ({x},{y}) -> ego box={ego.WalkBox} contains={s0.CurrentWalkBoxes.Contains(int.Parse(args[4]), x, y)}");
    for (var i = 0; i < 600; i++) { s0.AdvanceFrame(); if (i % 100 == 0) Console.WriteLine($"f{i} cutscenes={s0.CutscenesBegun} in={s0.InCutscene} ego box={s0.Actors.First(a => a.Number == 1).WalkBox}"); }
    foreach (var l in s0.SessionTrace.TakeLast(15)) Console.WriteLine("  " + l);
    return;
}
if (args.Length > 2 && args[2] == "schema") // EgaSeedStudy <folder> - schema: why the status schema does or does not resolve
{
    var g3 = new GameFolderScanner().Scan(args[0]);
    var d3 = ScummDataSources.For(g3);
    Console.WriteLine($"Id={g3.Id} Version={g3.Version} IsDemo={g3.IsDemo} globals={d3.GlobalScriptCount}");
    var init = d3.RoomLocalScript(1, 211);
    Console.WriteLine("room1 local211: " + (init is null ? "null" : $"{init.Instructions.Count} instr, has var[0x07A]=3: {init.Instructions.Any(i => i.Operation == "move" && i.Operands == "var[0x07A] = 0x0003")}"));
    Console.WriteLine("room1 locals: " + string.Join(",", d3.RoomLocalScriptIds(1)) + " | room46 locals: " + string.Join(",", d3.RoomLocalScriptIds(46)));
    var cp = d3.RoomLocalScript(46, 204);
    Console.WriteLine("room46 local204: " + (cp is null ? "null" : string.Join(" ; ", cp.Instructions.Where(i => i.Operation == "startScript").Select(i => i.Operands))));
    return;
}
if (args.Length > 2 && args[2] == "writers") // EgaSeedStudy <folder> - writers <var[0x..]> <value>: every constant assignment of the variable in the game's code
{
    var g4 = new GameFolderScanner().Scan(args[0]);
    foreach (var w in LoomSceneStateScanner.For(g4).FindConstantWriters(args[3], int.Parse(args[4]))) Console.WriteLine($"  {w.Where} {w.Instruction}");
    return;
}
if (args.Length > 2 && args[2] == "grep") // EgaSeedStudy <folder> - grep <text>: instructions mentioning the text in Room 1's local scripts and global scripts 0-60
{
    var g5 = new GameFolderScanner().Scan(args[0]); var d5 = ScummDataSources.For(g5);
    foreach (var id in d5.RoomLocalScriptIds(1)) foreach (var i in d5.RoomLocalScript(1, id)?.Instructions ?? []) if (i.Operands.Contains(args[3], StringComparison.Ordinal)) Console.WriteLine($"  room1 local {id} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
    for (var id = 0; id < 60; id++) foreach (var i in d5.GlobalScript(id)?.Instructions ?? []) if (i.Operands.Contains(args[3], StringComparison.Ordinal)) Console.WriteLine($"  global {id} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
    return;
}
if (args.Length > 0 && args[0] == "index") // EgaSeedStudy index <folder>...: id/version and the 00.LFL directory counts of each folder, to find a data signature for a game
{
    foreach (var dir in args.Skip(1))
    {
        var g6 = new GameFolderScanner().Scan(dir);
        var byKind = g6.Resources.Where(r => r.ContainerFileNumber is not null || r.Kind is ResourceKind.Room or ResourceKind.Costume or ResourceKind.Script or ResourceKind.Sound).GroupBy(r => r.Kind).ToDictionary(x => x.Key, x => x.Count());
        Console.WriteLine($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(dir))}: {g6.Id} {g6.Version} demo={g6.IsDemo} rooms={byKind.GetValueOrDefault(ResourceKind.Room)} costumes={byKind.GetValueOrDefault(ResourceKind.Costume)} scripts={byKind.GetValueOrDefault(ResourceKind.Script)} sounds={byKind.GetValueOrDefault(ResourceKind.Sound)}");
    }
    return;
}
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var total = Stopwatch.StartNew();
var triggers = LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value).ToList(), LoomCutsceneTitles.Doorways(game), game);
var groups = LoomCutsceneIndex.Group(triggers);
var scanSec = total.Elapsed.TotalSeconds;
var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
var t1 = Stopwatch.StartNew();
var fresh = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, g =>
{
    var entered = LoomCutsceneRun.VisitedRooms(game, snapshot, g.First);
    fresh[$"{g.First.Room},{g.First.ObjectId},{g.First.VerbId}"] = entered is not null;
    if (entered is not null) planner.RecordFreshRun(g.First, entered);
});
var p1 = t1.Elapsed.TotalSeconds;
var t2 = Stopwatch.StartNew();
var rows = new List<object>();
var noStart = new List<string>();
foreach (var g in groups)
{
    var t = g.First; var key = $"{t.Room},{t.ObjectId},{t.VerbId}";
    var setup = planner.Derive(t);
    var visited = planner.VisitedRooms(t);
    if (!setup.Plays) noStart.Add($"{key} {LoomCutsceneTitles.Title(t)}");
    rows.Add(new { key, t.Room, t.VerbId, isEntry = t.IsRoomEntry, scripts = string.Join(",", t.Scripts), t.Texts, t.RoomLoads, scene = g.IsScene, others = g.Others.Count,
        freshBegan = fresh[key], plays = setup.Plays, hasSteps = setup.HasSteps, visited = visited.ToArray(), title = LoomCutsceneTitles.Title(t), firstText = t.FirstText });
}
var p2 = t2.Elapsed.TotalSeconds;
var lfl = Directory.GetFiles(game.FolderPath, "*.LFL").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
var fp = Convert.ToHexString(SHA256.HashData(lfl.SelectMany(f => File.ReadAllBytes(f)).ToArray()));
Console.WriteLine($"{Path.GetFileName(args[0])}: triggers {triggers.Count}, groups {groups.Count}, scan {scanSec:F1}s, phase1 {p1:F0}s, phase2 {p2:F0}s, total {total.Elapsed.TotalSeconds:F0}s, fresh-began {fresh.Values.Count(v => v)}/{groups.Count}, no-start {noStart.Count}, scenes {groups.Count(g => g.IsScene)}, lflFiles {lfl.Count}, fp {fp[..16]}");
foreach (var n in noStart) Console.WriteLine("  no start: " + n);
File.WriteAllText(args[1], JsonSerializer.Serialize(new { edition = Path.GetFileName(args[0]), fp, scanSec, p1, p2, rows }, new JsonSerializerOptions { WriteIndented = true }));

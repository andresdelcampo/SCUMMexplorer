using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// Usage: DemoProbe <game folder> [index | play <frames>]
if (args.Length > 1 && args[1] == "texts")
{
    var textGame = new GameFolderScanner().Scan(args[0]);
    var extraction = new LoomTextExtractor().Extract(textGame);
    Console.WriteLine($"{extraction.Entries.Count} texts; {extraction.Coverage}; skipped ranges {extraction.ResourcesSkipped}; unresolved {extraction.UnresolvedPaths}");
    foreach (var d in extraction.Diagnostics.Take(8)) Console.WriteLine("  diag: " + d);
    foreach (var group in extraction.Entries.GroupBy(e => e.Kind)) Console.WriteLine($"  {group.Key}: {group.Count()}");
    foreach (var e in extraction.Entries.Where(e => e.Kind != "Object name").Take(12)) Console.WriteLine($"  [{e.Kind}] room {e.RoomId} script {e.ScriptId}: {e.Text}");
    return;
}
if (args.Length > 3 && args[1] == "ego")
{
    // DemoProbe <folder> ego <room> <room>...: visit the rooms in turn and print the actors in each (costume, room, position).
    var egoGame = new GameFolderScanner().Scan(args[0]);
    var egoSession = new LoomExplorationSession(egoGame, new LflObjectStateReader().ReadSnapshot(Path.Combine(egoGame.FolderPath, "00.LFL")));
    foreach (var room in args.Skip(2).Select(int.Parse))
    {
        egoSession.VisitRoomById(room);
        Console.WriteLine($"room {room}: " + string.Join("; ", egoSession.Actors.Where(a => a.Room == room).Select(a => $"actor {a.Number} costume {a.Look.Costume} at {a.X},{a.Y}")));
    }
    return;
}
if (args.Length > 6 && args[1] == "shot") { RoomShot.Run(args[0], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), args[6], args.Length > 7 ? args[7] : ""); return; }
if (args.Length > 4 && args[1] == "adjust")
{
    // DemoProbe <folder> adjust <room> <x> <y>: our Contains / ClosestPointOnBox / Adjust for a destination point, per box.
    var adGame = new GameFolderScanner().Scan(args[0]);
    var adBoxes = LoomWalkBoxReader.Read(File.ReadAllBytes(adGame.Rooms.Single(r => r.ResourceId == int.Parse(args[2])).FilePath));
    int ax = int.Parse(args[3]), ay = int.Parse(args[4]);
    foreach (var box in adBoxes.Boxes.Where(b => b.Index >= 11))
    {
        var closest = LoomWalkBoxSet.ClosestPointOnBox(box, ax, ay);
        Console.WriteLine($"box {box.Index}: contains {adBoxes.Contains(box.Index, ax, ay)}, closest ({closest.X},{closest.Y}) dist² {closest.Distance}");
    }
    var adjusted = adBoxes.Adjust(ax, ay, true, 551, 111);
    Console.WriteLine($"Adjust({ax},{ay}) -> ({adjusted.X},{adjusted.Y}) box {adjusted.Box}");
    return;
}
if (args.Length > 3 && args[1] == "verbs")
{
    // DemoProbe <folder> verbs <room> <object>: every verb of the object, decoded.
    var vbGame = new GameFolderScanner().Scan(args[0]);
    var vbRoom = vbGame.Rooms.Single(r => r.ResourceId == int.Parse(args[2]));
    var vbBytes = File.ReadAllBytes(vbRoom.FilePath);
    var vbRecord = new LoomRoomObjectDirectory().Decode(vbBytes).Single(r => r.Id == int.Parse(args[3]));
    var vbReader = new LoomObjectVerbReader(ScummScriptDialects.For(vbGame.Id));
    foreach (var entry in vbReader.ReadAll(vbBytes, [vbRecord])[vbRecord.Id])
    {
        Console.WriteLine($"== {entry.Label} (code +0x{entry.ScriptOffset:X4})");
        foreach (var i in vbReader.Decode(vbRoom, vbRecord, entry.VerbId).Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
if (args.Length > 2 && args[1] == "objects")
{
    // DemoProbe <folder> objects <room>: the room's object records (id, rectangle, walk point).
    var obGame = new GameFolderScanner().Scan(args[0]);
    var obRoom = obGame.Rooms.Single(r => r.ResourceId == int.Parse(args[2]));
    var obBytes = File.ReadAllBytes(obRoom.FilePath);
    var obNames = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(obBytes), new LoomRoomObjectDirectory().Decode(obBytes));
    foreach (var r in new LoomRoomObjectDirectory().Decode(obBytes))
        Console.WriteLine($"object {r.Id} \"{obNames.GetValueOrDefault(r.Id, "")}\": {System.Text.Json.JsonSerializer.Serialize(r)}");
    return;
}
if (args.Length > 2 && args[1] == "findload")
{
    // DemoProbe <folder> findload <room>: every loadRoom/loadRoomWithEgo in the room's local scripts and entry, with the lines before it.
    var flGame = new GameFolderScanner().Scan(args[0]);
    var flReader = new LoomScriptReader(ScummScriptDialects.For(flGame.Id));
    var flRoom = flGame.Rooms.Single(r => r.ResourceId == int.Parse(args[2]));
    var globalCount = new LflIndexReader().ReadDeclaredScriptCount(Path.Combine(flGame.FolderPath, "00.LFL"));
    foreach (var id in LoomScriptReader.ReadRoomLocalScriptIds(File.ReadAllBytes(flRoom.FilePath), globalCount))
    {
        var script = flReader.DecodeRoomLocalScript(flRoom, id, globalCount);
        if (script is null) continue;
        var list = script.Instructions.ToList();
        for (var i = 0; i < list.Count; i++)
            if (list[i].Operation.StartsWith("loadRoom"))
            {
                Console.WriteLine($"== local {id} +0x{list[i].Offset:X4}");
                foreach (var l in list.Skip(Math.Max(0, i - 10)).Take(12)) Console.WriteLine($"   {l.Offset:X4} {l.Operation} {l.Operands}");
            }
    }
    return;
}
if (args.Length > 4 && args[1] == "walk")
{
    // DemoProbe <folder> walk <room> <x> <y>: visit the room, click the point, and print the ego's place, box, costume and size every few frames.
    var walkGame = new GameFolderScanner().Scan(args[0]);
    var walkSession = new LoomExplorationSession(walkGame, new LflObjectStateReader().ReadSnapshot(Path.Combine(walkGame.FolderPath, "00.LFL")));
    walkSession.VisitRoomById(int.Parse(args[2]));
    var lastLine = "";
    for (var click = 0; click < 3 && walkSession.Snapshot.RoomId == int.Parse(args[2]); click++)
    {
        var action = walkSession.Click(int.Parse(args[3]), int.Parse(args[4]));
        Console.WriteLine($"click {click}: {action.Evidence}");
        foreach (var note in action.Uncertainties.Take(6)) Console.WriteLine("   - " + note);
        for (var frame = 0; frame < 400 && walkSession.Snapshot.RoomId == int.Parse(args[2]); frame++)
        {
            var ego = walkSession.Actors.First(a => a.Number == 1);
            var line = $"({ego.X},{ego.Y}) box {ego.WalkBox} costume {ego.Look.Costume} size {walkSession.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x07C]")}";
            // Print when box, costume or size change, and every 20th frame.
            var key = line[(line.IndexOf(')') + 1)..];
            if (key != lastLine[(lastLine.IndexOf(')') + 1 is var i and > 0 ? i : 0)..] || frame % 20 == 0) Console.WriteLine($"click {click} frame {frame}: {line}");
            lastLine = line;
            if (args.Length > 5 && args[5] == "mask") MaskSample.Print(walkGame, walkSession, int.Parse(args[2]), click, frame);
            walkSession.AdvanceFrame();
            if (!walkSession.EgoMoving && !walkSession.InCutscene && frame > 32) break;
        }
    }
    Console.WriteLine($"final room {walkSession.Snapshot.RoomId}");
    return;
}
if (args.Length > 1 && args[1] == "index") { IndexDump.Run(args[0]); return; }
if (args.Length > 1 && args[1] is "play" or "idle") { PlayDump.Run(args[0], args.Length > 2 ? int.Parse(args[2]) : 3000, args[1] == "idle"); return; }
// What the existing code makes of a LOOM folder: identity, resources, every script decoded, every room opened in a session, the boot.
var game = new GameFolderScanner().Scan(args[0]);
Console.WriteLine($"{game.Title}: id {game.Id}, version {game.Version}, rooms {game.Rooms.Count()}, resources {game.Resources.Count} ({string.Join(", ", game.Resources.GroupBy(r => r.Kind).Select(g => $"{g.Count()} {g.Key}"))})");
var scripts = 0; var scriptFail = new List<string>();
var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script))
{
    try { reader.Decode(r); scripts++; } catch (Exception ex) { scriptFail.Add($"script {r.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
}
Console.WriteLine($"scripts decoded {scripts}, failed {scriptFail.Count}");
foreach (var f in scriptFail.Take(8)) Console.WriteLine("  " + f);
var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
LoomExplorationSession session;
try { session = new LoomExplorationSession(game, snapshot); }
catch (Exception ex) { Console.WriteLine("session: " + ex); return; }
Console.WriteLine($"session created; boot running {session.BootRunning}, room {session.Snapshot.RoomId}");
var frames = 0;
try
{
    while (session.BootRunning && frames < 3000) { session.AdvanceFrame(); frames++; }
    Console.WriteLine($"boot ended after {frames} frames in room {session.Snapshot.RoomId}; uncertainties {session.Snapshot.Uncertainties.Count}");
}
catch (Exception ex) { Console.WriteLine($"boot frames {frames}: {ex.GetType().Name}: {ex.Message}"); }
var ok = 0; var failed = new List<string>();
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
{
    try { session.VisitRoomById(room.ResourceId!.Value); ok++; }
    catch (Exception ex) { failed.Add($"room {room.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
}
Console.WriteLine($"rooms visited {ok}, failed {failed.Count}");
foreach (var f in failed.Take(10)) Console.WriteLine("  " + f);
Console.WriteLine("room ids: " + string.Join(" ", game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId)));

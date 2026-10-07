using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Usage: CutsceneScanCompare <loom ega folder> <loom-replays.json>
// Runs the CD's script scan (LoomCdCutsceneIndex) on the EGA data and compares its triggers with the hand-made catalog.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).ToList();
var scan = LoomCdCutsceneIndex.Scan(data, rooms);
var groups = LoomCdCutsceneIndex.Group(scan);
var scanned = scan.Select(t => (t.Room, t.ObjectId, t.VerbId)).ToHashSet();
if (args.Length > 2 && args[2] == "verbs") // CutsceneScanCompare <folder> - verbs <room>...: every object verb of those rooms, with whether the scan found a cutscene in it
{
    foreach (var room in args.Skip(3).Select(int.Parse))
    {
        var rd = data.ReadRoom(room);
        foreach (var (oid, list) in data.ReadObjectVerbs(room, rd.Objects))
            foreach (var v in list)
                Console.WriteLine($"room {room} object {oid} verb 0x{v.VerbId:X2}: " + (scanned.Contains((room, oid, v.VerbId)) ? "SCAN" : "-"));
    }
    return;
}
if (args.Length > 2 && args[2] == "script") // CutsceneScanCompare <folder> - script <id>...: global scripts listed
{
    foreach (var id in args.Skip(3).Select(int.Parse))
    {
        Console.WriteLine($"== script {id}");
        foreach (var ins in data.GlobalScript(id)?.Instructions ?? []) Console.WriteLine($"{ins.Offset:X4} {ins.Operation} {ins.Operands}");
    }
    return;
}
if (args.Length > 2 && args[2] == "boxes") // CutsceneScanCompare <folder> - boxes <room>...: walk boxes with mask and flags
{
    foreach (var room in args.Skip(3).Select(int.Parse))
        foreach (var b in data.ReadRoom(room).WalkBoxes?.Boxes ?? [])
            Console.WriteLine($"room {room} box {b.Index}: ({b.UpperLeft.X},{b.UpperLeft.Y}) ({b.UpperRight.X},{b.UpperRight.Y}) ({b.LowerRight.X},{b.LowerRight.Y}) ({b.LowerLeft.X},{b.LowerLeft.Y}) mask {b.Mask} flags 0x{b.Flags:X2} scale {b.Scale}");
    return;
}
if (args.Length > 2 && args[2] == "maskmap") // CutsceneScanCompare <CD folder> - maskmap <room>: the depth mask at 1/4 scale (# masked)
{
    var im = game.Vga!.DecodeRoom(int.Parse(args[3]));
    var px = (args.Length > 4 ? im.ExtraDepthMasks[int.Parse(args[4]) - 2] : im.DepthMask!).Pixels.ToArray();
    Console.WriteLine($"{im.Width}x{im.Height}");
    for (var y = 0; y < im.Height; y += 4)
        Console.WriteLine($"{y,3} " + string.Concat(Enumerable.Range(0, im.Width / 4).Select(x => px[y * im.Width + x * 4] != 0 ? '#' : '.')));
    return;
}
if (args.Length > 2 && args[2] == "masks") // CutsceneScanCompare <CD folder> - masks: rooms whose depth mask is missing, empty or set
{
    foreach (var room in rooms)
    {
        var image = game.Vga!.DecodeRoom(room);
        var set = image.DepthMask is { } m ? m.Pixels.Span.ToArray().Count(b => b != 0) : -1;
        Console.WriteLine($"room {room}: mask pixels {(set < 0 ? "none (block missing or unreadable)" : set.ToString())}, extra planes {image.ExtraDepthMasks.Count}");
    }
    return;
}
if (args.Length > 2 && args[2] == "grep") // CutsceneScanCompare <folder> - grep <text>: instructions of global, entry and local scripts containing the text
{
    void Scan(string name, LoomScriptDisassembly? d) { foreach (var ins in d?.Instructions ?? []) if ($"{ins.Operation} {ins.Operands}".Contains(args[3])) Console.WriteLine($"{name} +{ins.Offset:X4} {ins.Operation} {ins.Operands}"); }
    foreach (var id in data.GlobalScriptIds()) Scan($"global {id}", data.GlobalScript(id));
    foreach (var room in rooms) { Scan($"room {room} entry", data.RoomEntryScript(room)); foreach (var id in data.RoomLocalScriptIds(room)) Scan($"room {room} local {id}", data.RoomLocalScript(room, id)); }
    return;
}
using var doc = JsonDocument.Parse(File.ReadAllText(args[1]));
var catalog = doc.RootElement.GetProperty("replays").EnumerateArray()
    .Select(e => (Room: e.GetProperty("room").GetInt32(), Obj: e.GetProperty("objectId").GetInt32(), Verb: e.GetProperty("verb").GetInt32(), Id: e.GetProperty("id").GetString()!)).ToList();
Console.WriteLine($"scan: {scan.Count} triggers in {groups.Count} groups; catalog: {catalog.Count} replays");
var found = catalog.Where(c => scanned.Contains((c.Room, c.Obj, c.Verb))).ToList();
Console.WriteLine($"catalog replays the scan finds as a trigger: {found.Count}");
foreach (var c in catalog.Where(c => !scanned.Contains((c.Room, c.Obj, c.Verb)))) Console.WriteLine($"  catalog only: {c.Id} (room {c.Room}, object {c.Obj}, verb 0x{c.Verb:X2})");
var inCatalog = catalog.Select(c => (c.Room, c.Obj, c.Verb)).ToHashSet();
var extra = scan.Where(t => !inCatalog.Contains((t.Room, t.ObjectId, t.VerbId))).ToList();
Console.WriteLine($"scan only: {extra.Count}");
foreach (var t in extra) Console.WriteLine($"  scan only: room {t.Room} object {t.ObjectId} {t.ObjectName} {t.VerbLabel}");
// Extras that run the same scripts as a catalogued trigger (the group's other members) are not new scenes.
var uncovered = groups.Where(g => !g.Others.Append(g.First).Any(t => inCatalog.Contains((t.Room, t.ObjectId, t.VerbId)))).ToList();
Console.WriteLine($"groups with no catalogued member: {uncovered.Count}");
foreach (var g in uncovered) Console.WriteLine($"  uncovered group: room {g.First.Room} object {g.First.ObjectId} {g.First.ObjectName} {g.First.VerbLabel}, scene {g.IsScene}, {g.First.Texts} text(s), {g.First.RoomLoads} room load(s)");

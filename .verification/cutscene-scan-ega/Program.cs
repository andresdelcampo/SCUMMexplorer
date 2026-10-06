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

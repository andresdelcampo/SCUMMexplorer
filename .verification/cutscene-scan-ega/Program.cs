using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

// Usage: CutsceneScanCompare <loom ega folder> <loom-replays.json>
// Runs the CD's script scan (LoomCutsceneIndex) on the EGA data and compares its triggers with the hand-made catalog.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).ToList();
var scan = LoomCutsceneIndex.Scan(data, rooms, LoomCutsceneTitles.Doorways(game), game);
var groups = LoomCutsceneIndex.Group(scan);
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
    var im = game.V4Archive!.DecodeRoom(int.Parse(args[3]));
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
        var image = game.V4Archive!.DecodeRoom(room);
        var set = image.DepthMask is { } m ? m.Pixels.Span.ToArray().Count(b => b != 0) : -1;
        Console.WriteLine($"room {room}: mask pixels {(set < 0 ? "none (block missing or unreadable)" : set.ToString())}, extra planes {image.ExtraDepthMasks.Count}");
    }
    return;
}
if (args.Length > 2 && args[2] == "grep") // CutsceneScanCompare <folder> - grep <text>: instructions of global, entry and local scripts containing the text
{
    void Scan(string name, ScummScriptDisassembly? d) { foreach (var ins in d?.Instructions ?? []) if ($"{ins.Operation} {ins.Operands}".Contains(args[3])) Console.WriteLine($"{name} +{ins.Offset:X4} {ins.Operation} {ins.Operands}"); }
    foreach (var id in data.GlobalScriptIds()) Scan($"global {id}", data.GlobalScript(id));
    foreach (var room in rooms) { Scan($"room {room} entry", data.RoomEntryScript(room)); foreach (var id in data.RoomLocalScriptIds(room)) Scan($"room {room} local {id}", data.RoomLocalScript(room, id)); }
    return;
}
if (args.Length > 2 && args[2] == "speakers") // CutsceneScanCompare <folder> - speakers: per actor number and script/room, the speaker table's name and a sample line (checks LoomSpeakerNames against this edition)
{
    foreach (var g in new LoomTextExtractor().Extract(game).Entries.Where(en => en.Speaker is { } sp && sp.StartsWith("Actor "))
        .GroupBy(en => (en.Speaker, en.ScriptId, en.RoomId)).OrderBy(g => g.Key.Speaker).ThenBy(g => g.Key.ScriptId))
        Console.WriteLine($"{g.Key.Speaker} script {g.Key.ScriptId} room {g.Key.RoomId} => {LoomSpeakerNames.Display(g.First())}: {string.Join(" / ", g.Take(2).Select(en => en.Text.Length > 60 ? en.Text[..60] : en.Text))}");
    return;
}
if (args.Length > 2 && args[2] == "titles") // CutsceneScanCompare <folder> <loom-replays.json> titles: scan-derived title and category against the catalog's, per replay
{
    using var tdoc = JsonDocument.Parse(File.ReadAllText(args[1]));
    var groupOf = groups.SelectMany(g => g.Others.Append(g.First).Select(t => (t, g))).ToDictionary(x => (x.t.Room, x.t.ObjectId, x.t.VerbId), x => x.g);
    int same = 0, titleDiff = 0, catDiff = 0, helper = 0;
    foreach (var e in tdoc.RootElement.GetProperty("replays").EnumerateArray())
    {
        var key = (e.GetProperty("room").GetInt32(), e.GetProperty("objectId").GetInt32(), e.GetProperty("verb").GetInt32());
        var trigger = scan.First(t => (t.Room, t.ObjectId, t.VerbId) == key);
        var catTitle = LoomCutsceneTitles.Format(key.Item1, LoomDraftNames.ReplayTitle(e.GetProperty("title").GetString()!, key.Item3));
        var scanTitle = LoomCutsceneTitles.Title(trigger);
        var catCat = e.GetProperty("category").GetString();
        var scanCat = groupOf.TryGetValue(key, out var g) ? (g.IsScene ? "Scenes" : "Dialogue and reactions") : "(shared helper: no group)";
        if (!groupOf.ContainsKey(key)) helper++;
        var t = catTitle == scanTitle; var c = catCat == scanCat;
        if (t && c) same++; if (!t) titleDiff++; if (!c) catDiff++;
        if (args.Length > 3 && args[3] == "csv")
            Console.WriteLine($"CSV,{catCat},{trigger.Texts},{trigger.RoomLoads},{trigger.SpanInstructions},{trigger.IsRoomEntry},{e.GetProperty("rooms").GetArrayLength()},{(scanTitle.Contains("Reached") ? 1 : 0)},{(groupOf.ContainsKey(key) ? 1 : 0)}");
        if (!t || !c) Console.WriteLine($"{e.GetProperty("id").GetString()}{Environment.NewLine}    catalog: {catTitle} [{catCat}]{Environment.NewLine}    scan:    {scanTitle} [{scanCat}]{Environment.NewLine}    stats:   texts {trigger.Texts}, room loads {trigger.RoomLoads}, span {trigger.SpanInstructions}, catalog rooms {e.GetProperty("rooms").GetArrayLength()}, frames {e.GetProperty("frames").GetInt32()}");
    }
    Console.WriteLine($"identical title and category: {same}; title differs: {titleDiff}; category differs: {catDiff}; replays whose scripts the scan drops as shared helpers: {helper}");
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

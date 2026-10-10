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
if (args.Length > 2 && args[2] == "visit") // CutsceneScanCompare <folder> <loom-replays.json> visit: a fresh headless run of every catalogued trigger (no start setup); rooms it entered vs the catalog's, and whether a cutscene began
{
    using var vdoc = JsonDocument.Parse(File.ReadAllText(args[1]));
    var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    int agree = 0, total = 0, notBegun = 0;
    var started = System.Diagnostics.Stopwatch.StartNew();
    foreach (var e in vdoc.RootElement.GetProperty("replays").EnumerateArray())
    {
        var room = e.GetProperty("room").GetInt32(); var obj = e.GetProperty("objectId").GetInt32(); var verb = e.GetProperty("verb").GetInt32();
        var catScene = e.GetProperty("category").GetString() == "Scenes";
        var catRooms = e.GetProperty("rooms").EnumerateArray().Select(x => x.GetInt32()).ToList();
        var visited = new List<int>(); var begun = false; string note = "";
        try
        {
            var session = new LoomExplorationSession(game, snapshot);
            session.VisitRoom(game.Rooms.First(r => r.ResourceId == room));
            var earlier = session.CutscenesBegun; var mark = session.RoomsEntered.Count;
            if (obj != 0) session.ActivateObjectVerb(obj, verb);
            for (var i = 0; i < 6000; i++)
            {
                if (session.InCutscene || session.CutscenesBegun > earlier) begun = true;
                if (begun && !session.InCutscene && session.Messages.HaveMsg == 0) break;
                if (!begun && i >= 600) break;
                session.AdvanceFrame();
            }
            visited = session.RoomsEntered.Skip(mark).Distinct().ToList();
        }
        catch (Exception ex) { note = ex.GetType().Name + ": " + ex.Message; }
        var left = visited.Any(r => r != room);
        total++; if (!begun) notBegun++;
        var runScene = left;
        if (begun && runScene == catScene || !begun && !catScene) agree++;
        Console.WriteLine($"{e.GetProperty("id").GetString()} | catalog {(catScene ? "Scene" : "Dialogue")} rooms [{string.Join(",", catRooms)}] | run began={begun} entered [{string.Join(",", visited)}] {note}");
    }
    Console.WriteLine($"agree on scene-ness (left the room vs catalog category; not-begun counts as dialogue): {agree} of {total}; no cutscene began: {notBegun}; {started.Elapsed.TotalSeconds:F0} s");
    return;
}
if (args.Length > 2 && args[2] == "uncovered") // CutsceneScanCompare <folder> <loom-replays.json> uncovered: the scan's groups with no catalogued member, each run headless (LoomCutsceneRun)
{
    using var udoc = JsonDocument.Parse(File.ReadAllText(args[1]));
    var inCat = udoc.RootElement.GetProperty("replays").EnumerateArray().Select(e => (e.GetProperty("room").GetInt32(), e.GetProperty("objectId").GetInt32(), e.GetProperty("verb").GetInt32())).ToHashSet();
    var snap = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    foreach (var g in groups.Where(g => !g.Others.Append(g.First).Any(t => inCat.Contains((t.Room, t.ObjectId, t.VerbId)))))
    {
        var ran = LoomCutsceneRun.VisitedRooms(game, snap, g.First);
        var t = g.First;
        Console.WriteLine($"{LoomCutsceneTitles.Title(t)} | {t.VerbLabel} obj {t.ObjectId} | static {(g.IsScene ? "Scene" : "Dialogue")} run {(ran is null ? "NO CUTSCENE BEGAN" : ran.Count > 0 ? "left to " + string.Join(",", ran) : "stayed")} -> {(LoomCutsceneRun.IsScene(g, ran) ? "Scene" : "Dialogue")} | texts {t.Texts} loads {t.RoomLoads} span {t.SpanInstructions} others {g.Others.Count} | \"{(t.FirstText.Length > 70 ? t.FirstText[..70] : t.FirstText)}\"");
    }
    return;
}
if (args.Length > 2 && args[2] == "gated") // CutsceneScanCompare <folder> - gated: the scan's triggers whose fresh run begins no cutscene, with the setup the CD planner derives for them on EGA data
{
    var planner = new LoomCdSceneSetupPlanner(game, scan, data);
    foreach (var g in groups)
    {
        var t = g.First;
        var fresh = LoomCutsceneRun.VisitedRooms(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")), t);
        if (fresh is not null) continue;
        var setup = planner.Derive(t);
        Console.WriteLine($"{LoomCutsceneTitles.Title(t)} | {t.VerbLabel} | plays={setup.Plays} steps={setup.Before.Count}+{setup.After.Count} | {setup.Describe()}");
        if (setup.Plays) Console.WriteLine($"    rooms: {string.Join(",", planner.VisitedRooms(t))}");
    }
    return;
}
if (args.Length > 2 && args[2] == "local") // CutsceneScanCompare <folder> - local <room> entry|<id>...: a room's entry script or local scripts listed
{
    var lroom = int.Parse(args[3]);
    foreach (var which in args.Skip(4))
    {
        Console.WriteLine($"== room {lroom} {which}");
        var d = which == "entry" ? data.RoomEntryScript(lroom) : data.RoomLocalScript(lroom, int.Parse(which));
        foreach (var ins in d?.Instructions ?? []) Console.WriteLine($"{ins.Offset:X4} {ins.Operation} {ins.Operands}");
    }
    return;
}
if (args.Length > 2 && args[2] == "walk") // CutsceneScanCompare <folder> - walk <room> <x,y>...: fresh session, visit the room, then click the points one after another (Bobbin walks there), reporting cutscenes begun and the ego's box
{
    var snap2 = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    var wroom = int.Parse(args[3]);
    var ws = new LoomExplorationSession(game, snap2);
    foreach (var pre in (Environment.GetEnvironmentVariable("PREV") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) { ws.VisitRoom(game.Rooms.First(r => r.ResourceId == int.Parse(pre))); for (var k = 0; k < 200; k++) ws.AdvanceFrame(); }
    ws.VisitRoom(game.Rooms.First(r => r.ResourceId == wroom));
    for (var i = 0; i < 200; i++) ws.AdvanceFrame();
    Console.WriteLine($"in room {ws.Snapshot.RoomId}; cutscenes begun {ws.CutscenesBegun}");
    foreach (var point in args.Skip(4))
    {
        var place = point.StartsWith('p');
        var xy = point.TrimStart('p').Split(',').Select(int.Parse).ToArray();
        if (place) { ws.PlaceEgo(xy[0], xy[1]); for (var k = 0; k < 40; k++) ws.AdvanceFrame(); Console.WriteLine($"placed at {point}: ego box {ws.Actors.First(x => x.Number == 1).WalkBox}"); continue; }
        ws.WalkEgoTo(xy[0], xy[1]);
        var before = ws.CutscenesBegun;
        for (var i = 0; i < 1500; i++)
        {
            ws.AdvanceFrame();
            if (i % 10 == 0 && ws.Actors.FirstOrDefault(x => x.Number == 1) is { } ego) Console.WriteLine($"    f{i}: ego ({ego.X},{ego.Y}) box {ego.WalkBox}");
            if (ws.CutscenesBegun > before) { Console.WriteLine($"  cutscene began at frame {i} after clicking {point}"); break; }
            if (!ws.EgoMoving && i > 60) break;
        }
        Console.WriteLine($"clicked {point}: cutscenes begun {ws.CutscenesBegun}, room {ws.Snapshot.RoomId}, moving={ws.EgoMoving}");
    }
    return;
}
if (args.Length > 2 && args[2] == "trigger") // CutsceneScanCompare <folder> - trigger <room> <object> <verb hex>: the verb's code and the scripts it starts (literal numbers, depth 3), instructions listed
{
    int room = int.Parse(args[3]), obj = int.Parse(args[4]), verb = Convert.ToInt32(args[5], 16);
    var rd = data.ReadRoom(room);
    var seen = new HashSet<(bool, int)>();
    void Dump(string label, ScummScriptDisassembly? d, int depth)
    {
        Console.WriteLine($"== {label}");
        foreach (var ins in d?.Instructions ?? []) Console.WriteLine($"{new string(' ', depth * 2)}{ins.Offset:X4} {ins.Operation} {ins.Operands}");
        if (depth >= 3) return;
        foreach (var ins in (d?.Instructions ?? []).Where(i => i.Operation is "startScript" or "chainScript"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(ins.Operands, @"^(?:0x)?([0-9A-Fa-f]+),");
            if (!m.Success) continue;
            var id = int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
            var local = id >= data.GlobalScriptCount;
            if (!seen.Add((local, id))) continue;
            Dump(local ? $"room {room} local {id}" : $"global {id}", local ? data.RoomLocalScript(room, id) : data.GlobalScript(id), depth + 1);
        }
    }
    var record = rd.Objects.First(o => o.Id == obj);
    Dump($"room {room} object {obj} verb 0x{verb:X2}", data.ReadObjectVerb(room, record, verb, out _), 0);
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
            Console.WriteLine($"CSV,{catCat},{trigger.Texts},{trigger.RoomLoads},{trigger.SpanInstructions},{trigger.IsRoomEntry},{e.GetProperty("rooms").GetArrayLength()},{(scanTitle.Contains("Reached") ? 1 : 0)},{(groupOf.ContainsKey(key) ? 1 : 0)},{e.GetProperty("frames").GetInt32()},{trigger.LeadsTo.Count},{trigger.VerbId:X2},{trigger.LoadsAfter},0,{trigger.LoadTargets?.Count ?? 0},{string.Join("/", trigger.LoadTargets ?? [])}");
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

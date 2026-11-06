using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: Doorways <game folder> [list]
// 6 November 2026 (user: "we need a clear definition of what a doorway and an object hotspot are"): for every object of every room, which verbs' code can reach a
// room load (ScummDoorwayFinder), per verb, so the verb a game walks through its doors with can be read from its data. `list` also prints every object that counts.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var finder = new ScummDoorwayFinder(game, data);
var list = args.Length > 1 && args[1] == "list";
var byVerb = new Dictionary<int, (int Objects, int Direct)>();
var objectsWithVerb = new Dictionary<int, int>();
var total = 0;
var doorways = 0;
var dropped = new List<string>();
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).DistinctBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value;
    IReadOnlyList<ScummRoomObjectRecord> records;
    IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> verbs;
    try { records = data.ReadRoom(id).Objects; verbs = data.ReadObjectVerbs(id, records); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { continue; }
    foreach (var record in records)
    {
        if (!verbs.TryGetValue(record.Id, out var entries)) continue;
        var any = false;
        foreach (var verb in entries)
        {
            objectsWithVerb[verb.VerbId] = objectsWithVerb.GetValueOrDefault(verb.VerbId) + 1;
            ScummScriptDisassembly script;
            try { script = data.ReadObjectVerb(id, record, verb.VerbId, out _); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { continue; }
            var targets = finder.Find(room, script);
            if (targets.Count == 0) continue;
            any = true;
            var seen = byVerb.GetValueOrDefault(verb.VerbId);
            byVerb[verb.VerbId] = (seen.Objects + 1, seen.Direct + (targets.Any(t => t.Path == "direct") ? 1 : 0));
            if (list) Console.WriteLine($"Room {id:000} object {record.Id} verb 0x{verb.VerbId:X2}: {string.Join(", ", targets.Select(t => $"{t.RoomId} ({t.Path})"))}");
        }
        if (any) total++;
        var doors = finder.DoorVerbs(entries).SelectMany(IEnumerable<ScummDoorwayTarget> (verb) =>
        {
            try { return finder.FindDoorway(room, data.ReadObjectVerb(id, record, verb.VerbId, out _), verb); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { return []; }
        }).ToArray();
        if (doors.Length > 0) doorways++;
        if (any && doors.Length == 0) dropped.Add($"Room {id:000} object {record.Id}");
    }
}
Console.WriteLine($"{Path.GetFileName(args[0].TrimEnd('/', '\\'))}: {total} objects can load a room, {doorways} are doorways");
if (list) Console.WriteLine("  no longer doorways: " + string.Join("; ", dropped));
foreach (var (verb, count) in byVerb.OrderByDescending(item => item.Value.Objects))
    Console.WriteLine($"  verb 0x{verb:X2}: {count.Objects} objects ({count.Direct} directly), of {objectsWithVerb.GetValueOrDefault(verb)} with that verb");

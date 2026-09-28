using System.Text;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// For every room: a catalog visit plus up to 8 frames, then every object verb
// activated directly (fresh session each) plus up to 8 frames. Lists each
// getRandomNr the session stopped at ("blocked") or picked through ("picked"),
// and what the same script stopped at next.
Console.OutputEncoding = Encoding.UTF8;
var folder = args[0];
var output = args[1];
// Optional: print every uncertainty containing this text, with where it came from.
var trace = args.Length > 2 ? args[2] : null;
var traced = new List<string>();
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var verbReader = new LoomObjectVerbReader(ScummScriptDialects.For(game.Id));
var source = new Regex(@"^(?<ctx>.+?) \+0x[0-9A-F]{4}: ", RegexOptions.Compiled);
var offset = new Regex(@"(?:at|getRandomNr at) \+0x(?<off>[0-9A-F]{4})", RegexOptions.Compiled);

var rows = new SortedSet<string>(StringComparer.Ordinal);
var next = new SortedSet<string>(StringComparer.Ordinal);
var roomsByKind = new Dictionary<string, SortedSet<int>>();
// Every stop reason seen, with operands and offsets removed, counted once per room visit or action.
var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
var shape = new Regex(@"^.+? \+0x[0-9A-F]{4}: |\+0x[0-9A-F]{4}|\(.*\)|\d+", RegexOptions.Compiled);

void Collect(int room, string how, IEnumerable<string> uncertainties)
{
    var list = uncertainties.ToArray();
    if (trace is not null)
        traced.AddRange(list.Where(l => l.Contains(trace, StringComparison.Ordinal)).Select(l => $"Room {room:00} {how}: {l}"));
    foreach (var reason in list.Select(l => shape.Replace(l, "#")).Distinct())
        reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
    foreach (var line in list.Where(l => l.Contains("getRandomNr", StringComparison.Ordinal)))
    {
        var kind = line.Contains("is not modeled", StringComparison.Ordinal) ? "blocked" : "picked";
        var ctx = source.Match(line) is { Success: true } m ? m.Groups["ctx"].Value : "?";
        var off = offset.Match(line) is { Success: true } o ? o.Groups["off"].Value : "?";
        rows.Add($"{kind}\t{ctx}\t+0x{off}\tRoom {room:00} {how}");
        (roomsByKind.TryGetValue(kind, out var set) ? set : roomsByKind[kind] = []).Add(room);
        if (kind == "picked")
            foreach (var other in list.Where(l => l.StartsWith(ctx + " +0x", StringComparison.Ordinal) &&
                                                  !l.Contains("getRandomNr", StringComparison.Ordinal)))
                next.Add($"{ctx}\t{other[(ctx.Length + 1)..]}");
    }
}

void Frames(LoomExplorationSession session, int room)
{
    for (var frame = 0; frame < 8 && session.Snapshot.PendingScripts > 0 && session.Snapshot.RoomId == room; frame++)
        session.AdvanceFrame();
}

foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value;
    var visit = new LoomExplorationSession(game, index);
    visit.VisitRoom(room);
    Frames(visit, id);
    Collect(id, "visit", visit.Snapshot.Uncertainties);

    var bytes = File.ReadAllBytes(room.FilePath);
    var records = new LoomRoomObjectDirectory().Decode(bytes);
    var tables = verbReader.ReadAll(bytes, records);
    foreach (var record in records)
    {
        if (!tables.TryGetValue(record.Id, out var verbs)) continue;
        foreach (var verb in verbs.DistinctBy(v => v.ScriptOffset))
        {
            var session = new LoomExplorationSession(game, index);
            session.VisitRoom(room);
            var before = session.Snapshot.Uncertainties.Count;
            try { session.ActivateObjectVerb(record.Id, verb.VerbId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            Frames(session, id);
            Collect(id, $"object {record.Id} verb 0x{verb.VerbId:X2}", session.Snapshot.Uncertainties.Skip(before));
        }
    }
}

var report = new StringBuilder();
foreach (var kind in new[] { "blocked", "picked" })
{
    var mine = rows.Where(r => r.StartsWith(kind + "\t", StringComparison.Ordinal)).ToArray();
    var sites = mine.Select(r => string.Join('\t', r.Split('\t')[1..3])).Distinct().ToArray();
    var scripts = mine.Select(r => r.Split('\t')[1]).Distinct().ToArray();
    report.AppendLine($"{kind}: {sites.Length} instruction sites in {scripts.Length} scripts; rooms: " +
                      string.Join(',', (roomsByKind.GetValueOrDefault(kind) ?? []).Select(r => r.ToString("00"))));
    foreach (var site in sites) report.AppendLine($"  {site}");
}
report.AppendLine();
report.AppendLine("Where scripts that picked stopped next (other uncertainties from the same script):");
foreach (var line in next) report.AppendLine($"  {line}");
report.AppendLine();
report.AppendLine("Uncertainty shapes (count of visits and actions that show each):");
foreach (var (reason, count) in reasons.OrderByDescending(pair => pair.Value)) report.AppendLine($"  {count,5}  {reason}");
report.AppendLine();
report.AppendLine("Every occurrence:");
foreach (var row in rows) report.AppendLine($"  {row}");
if (trace is not null)
{
    report.AppendLine();
    report.AppendLine($"Uncertainties containing \"{trace}\":");
    foreach (var line in traced) report.AppendLine($"  {line}");
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, report.ToString());
Console.Write(report.ToString().Split("Every occurrence:")[0]);

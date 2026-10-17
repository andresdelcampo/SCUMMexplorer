using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using SCUMMexplorer.Core.Discovery;

// Usage: ManiacEnhancedRoomAudit <game folder> <output folder> [room ...]
// S5 ManiacEnhanced audit, 17 October 2026 (based on the Indy3/MonkeyFloppy audit): every room's first visit after the labelled Maniac party-selection recipe (ScummRoomFirstVisit: boot, a visit that does not
// play the route, frames until the room settles), the gaps (colour-0 holes in stored images) that remain, how the run ended, the actors, the
// object states and what the session could not follow. Writes room-audit.txt, room-audit.tsv and one BMP per room (objects and actors).
var folder = args[0];
var output = args[1];
var only = args.Skip(2).Select(int.Parse).ToHashSet();
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
if (Environment.GetEnvironmentVariable("AUDIT_PROBE") == "1")
{
    var probe = ScummGameSession.BootForPlay(game, out _);
    SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Prepare(game, probe);
    var scene = SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Run(game, 44);
    foreach (var actor in scene.ActorLayer().Actors) Console.WriteLine(actor);
    return;
}
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue && (only.Count == 0 || only.Contains(r.ResourceId.Value))).Select(r => r.ResourceId!.Value).OrderBy(i => i).ToArray();
var lines = new ConcurrentDictionary<int, (string Report, string Row, int Gaps, bool Failed)>();
string[] unsupported = ["unsupported", "not modelled", "unresolved", "not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

var totalBefore = 0;
var roomsBefore = 0;
var data = ScummDataSources.For(game);
if (Environment.GetEnvironmentVariable("AUDIT_SOURCES") == "1")
{
    var weighted = rooms.Select(id =>
    {
        var records = data.ReadRoom(id).Objects;
        var scripts = new List<ScummScriptDisassembly>();
        if (data.RoomEntryScript(id) is { } entry) scripts.Add(entry);
        if (data.RoomExitScript(id) is { } exit) scripts.Add(exit);
        var verbs = data.ReadObjectVerbs(id, records);
        foreach (var record in records.GroupBy(r => r.Id).Select(g => g.Last()))
            if (verbs.TryGetValue(record.Id, out var entries))
                foreach (var verb in entries.GroupBy(v => v.ScriptOffset).Select(g => g.First()))
                    scripts.Add(data.ReadObjectVerb(id, record, verb.VerbId, out _));
        return (Room: id, Count: scripts.Sum(s => s.Instructions.Count));
    }).OrderByDescending(x => x.Count).ToArray();
    var source = new StringBuilder("# Decoded first-visit source comparisons\n\nTen heaviest rooms by entry/exit/object-verb decoded instruction count, plus opening Room44 and Room1. This is static source comparison, not original-game execution.\n\n");
    foreach (var item in weighted.Take(10).Concat(weighted.Where(x => x.Room is 1 or 44)).DistinctBy(x => x.Room))
    {
        source.AppendLine($"## Room {item.Room:00}: {item.Count} decoded instructions across room blocks");
        source.AppendLine("```text");
        source.AppendLine(data.RoomEntryScript(item.Room)?.Format() ?? "No entry script");
        source.AppendLine("```");
        foreach (var instruction in data.RoomEntryScript(item.Room)?.Instructions ?? [])
        {
            if (instruction.Operation is not ("startScript" or "chainScript") || !instruction.Operands.StartsWith("0x")) continue;
            var token = instruction.Operands.Split([',', ' '])[0];
            if (!int.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, null, out var target)) continue;
            var child = data.GlobalScript(target);
            source.AppendLine($"Entry +0x{instruction.Offset:X4} calls Script{target}, {child?.Instructions.Count ?? 0} decoded instructions. Its literal object-state operations (all branches):");
            foreach (var op in child?.Instructions.Where(i => i.Operation is "setState" or "drawObject" or "setOwnerOf") ?? [])
                source.AppendLine($"- Script{target} +0x{op.Offset:X4}: {op.Operation} {op.Operands}");
        }
    }
    File.WriteAllText(Path.Combine(output, "source-comparisons.md"), source.ToString());
    return;
}
var dataLock = new object();
// Static cross-check: the objects the room's own entry and local scripts name with a literal setState above 0 or a drawObject (any branch).
(HashSet<int> Named, int Instructions, Dictionary<int, string> Where) NamedByRoomScripts(int room)
{
    var named = new HashSet<int>();
    var where = new Dictionary<int, string>();
    var count = 0;
    lock (dataLock)
    {
        var scripts = new List<(string Name, ScummScriptDisassembly? Script)> { ("entry", data.RoomEntryScript(room)) };
        scripts.AddRange(data.RoomLocalScriptIds(room).Select(local => ($"local {local}", data.RoomLocalScript(room, local))));
        foreach (var (name, script) in scripts.Where(s => s.Script is not null))
            foreach (var i in script!.Instructions)
            {
                count++;
                var parts = i.Operands.Split(", ");
                if (parts.Length == 0 || !parts[0].StartsWith("0x", StringComparison.Ordinal)) continue;
                var target = Convert.ToInt32(parts[0][2..], 16);
                if (i.Operation == "drawObject" || i.Operation == "setState" && parts.Length > 1 && parts[1].StartsWith("0x", StringComparison.Ordinal) && Convert.ToInt32(parts[1][2..], 16) > 0 || i.Operation == "setState" && parts.Length > 1 && parts[1] == "IntrinsicOn")
                {
                    named.Add(target);
                    where.TryAdd(target, $"{name} +0x{i.Offset:X4}");
                }
            }
    }
    return (named, count, where);
}
Parallel.ForEach(rooms, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id =>
{
    var watch = Stopwatch.StartNew();
    var report = new StringBuilder();
    try
    {
        var scene = SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Run(game, id);
        var snapshot = scene.Snapshot;
        var gaps = scene.Gaps();
        // Before S5: the view painted the objects of the index states only (no script run).
        var indexOnly = new RoomObjectSelection(scene.Images, scene.IndexedStates, new Dictionary<int, RoomObjectPosition>(), scene.Records, scene.Room, scene.IndexedStates, [], scene.IntrinsicStateMask);
        var gapsBefore = indexOnly.VisibleGaps().Count;
        Interlocked.Add(ref totalBefore, gapsBefore);
        if (gapsBefore > 0) Interlocked.Increment(ref roomsBefore);
        var active = scene.Images.Count(scene.Selection.IsActive);
        var layer = scene.ActorLayer();
        var notFollowed = snapshot.Uncertainties.Where(item => unsupported.Any(item.Contains) && !item.Contains("seeded pick")).Distinct().ToArray();
        report.AppendLine($"== Room {id:00} ({scene.Images.Count} images, {scene.Records.Count} objects) outcome={scene.Outcome} frames={scene.Frames}{(scene.LeftTo is int to ? $" left to {to:00}" : "")} lit={scene.RoomLit} {watch.Elapsed.TotalSeconds:0.0}s");
        report.AppendLine($"   before S5 (index states only): {gapsBefore} gaps, {scene.Images.Count(indexOnly.IsActive)} active images");
        foreach (var arrival in snapshot.Uncertainties.Where(item => item.StartsWith("Assumed arrival", StringComparison.Ordinal) || item.Contains("Assumed arrival:")).Distinct())
            report.AppendLine("   " + (arrival.Length > 300 ? arrival[..300] + "..." : arrival));
        report.AppendLine($"   VAR_ROOM {scene.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x004]")}; active states: {string.Join(',', scene.Images.Where(scene.Selection.IsActive).Select(i => i.Id).Distinct())}");
        if (Environment.GetEnvironmentVariable("AUDIT_NOTES") == "1")
            File.WriteAllLines(Path.Combine(output, $"room-{id:00}-notes.txt"), snapshot.Uncertainties.Distinct());
        var (named, scriptSize, namedWhere) = NamedByRoomScripts(id);
        var shown = scene.Images.Where(scene.Selection.IsActive).Select(i => i.Id).ToHashSet();
        var namedNotShown = named.Where(o => scene.Records.Any(r => r.Id == o) && !shown.Contains(o)).Order().ToArray();
        report.AppendLine($"   static: room scripts {scriptSize} instructions; named by literal setState>0/drawObject: {named.Count}; named and shown: {named.Count(shown.Contains)}; named, not shown: {string.Join("; ", namedNotShown.Select(o => $"{o} ({namedWhere[o]})"))}");
        foreach (var note in scene.Notes) report.AppendLine("   " + note);
        report.AppendLine($"   active images: {active}; paint order: {string.Join(',', scene.Selection.PaintOrder().Select(o => o.Id))}");
        report.AppendLine($"   actors drawn: {string.Join(", ", layer.Actors.Select(a => $"{a.Number}(costume {a.Costume} at {a.X},{a.Y}, {a.Pixels} pixels)"))}; skipped: {string.Join("; ", layer.Skipped)}");
        report.AppendLine($"   pending scripts: {snapshot.PendingScripts}; camera {scene.State.CameraX}; lights {scene.State.Lights}");
        foreach (var gap in gaps) report.AppendLine($"   GAP ({gap.X},{gap.Y}) {gap.Width}x{gap.Height}{(gap.Fillable ? "" : " [no usable stored image]")}");
        foreach (var item in notFollowed) report.AppendLine("   NOT FOLLOWED: " + (item.Length > 260 ? item[..260] + "..." : item));
        WriteBmp(Path.Combine(output, $"room-{id:00}.bmp"), scene.Room, scene.ComposeWithActors(), scene.DisplayPalette());
        lines[id] = (report.ToString(), $"{id:00}\t{scene.Outcome}\t{scene.Frames}\t{active}/{scene.Images.Count}\t{gaps.Count}\t{layer.Actors.Count}\t{notFollowed.Length}\t{scene.RoomLit}\t{scriptSize}\t{named.Count}\t{named.Count(shown.Contains)}\t{scene.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x004]")}\t{watch.Elapsed.TotalSeconds:0.0}", gaps.Count, false);
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException)
    {
        lines[id] = ($"== Room {id:00}: ERROR {ex.GetType().Name}: {ex.Message}\n", $"{id:00}\terror\t\t\t\t\t\t\t\t\t\t\t", 0, true);
    }
});

var totalGaps = lines.Values.Sum(item => item.Gaps);
var roomsWithGaps = lines.Values.Count(item => item.Gaps > 0);
var failed = lines.Values.Count(item => item.Failed);
var head = $"Before S5 (index states only): rooms with a gap {roomsBefore}, gaps {totalBefore}\nRooms: {rooms.Length}; rooms without a gap: {rooms.Length - roomsWithGaps - failed}; rooms with remaining gaps: {roomsWithGaps} ({totalGaps} gaps); rooms that could not run: {failed}\n\n";
File.WriteAllText(Path.Combine(output, "room-audit.txt"), head + string.Concat(rooms.Select(id => lines[id].Report)));
File.WriteAllText(Path.Combine(output, "room-audit.tsv"), "room\toutcome\tframes\tactive/images\tgaps\tactors\tnotFollowed\tlit\tscriptInstructions\tnamed\tnamedShown\tvarRoom\tseconds\n" + string.Join("\n", rooms.Select(id => lines[id].Row)) + "\n");
Console.WriteLine(head.Trim());

static void WriteBmp(string path, RoomImage room, byte[] pixels, IReadOnlyList<uint> palette)
{
    var stride = (room.Width * 3 + 3) & ~3;
    using var w = new BinaryWriter(File.Create(path));
    w.Write((ushort)0x4D42); w.Write(54 + stride * room.Height); w.Write(0); w.Write(54);
    w.Write(40); w.Write(room.Width); w.Write(room.Height); w.Write((ushort)1); w.Write((ushort)24);
    w.Write(0); w.Write(stride * room.Height); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
    var row = new byte[stride];
    for (var y = room.Height - 1; y >= 0; y--)
    {
        for (var x = 0; x < room.Width; x++)
        {
            var c = palette[pixels[y * room.Width + x] % palette.Count];
            row[x * 3] = (byte)c; row[x * 3 + 1] = (byte)(c >> 8); row[x * 3 + 2] = (byte)(c >> 16);
        }
        w.Write(row);
    }
}

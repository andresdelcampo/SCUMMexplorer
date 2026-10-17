using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using SCUMMexplorer.Core.Discovery;

// Usage: RoomAudit <ManiacClassic game folder> <output folder> [room ...]
// S5 room audit for ManiacClassic: every room's first visit from the generic session (ScummRoomFirstVisit: boot, a visit that does not
// play the route, frames until the room settles), the gaps (colour-0 holes in stored images) that remain, how the run ended, the actors, the
// object states and what the session could not follow. Writes room-audit.txt, room-audit.tsv and one BMP per room (objects and actors).
var folder = args[0];
var output = args[1];
var only = args.Skip(2).Select(int.Parse).ToHashSet();
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue && (only.Count == 0 || only.Contains(r.ResourceId.Value))).Select(r => r.ResourceId!.Value).OrderBy(i => i).ToArray();
var lines = new ConcurrentDictionary<int, (string Report, string Row, int Gaps, bool Failed)>();
var comparisons = new ConcurrentDictionary<int, (int Weight, string Row)>();
string[] unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

var totalBefore = 0;
var roomsBefore = 0;
Parallel.ForEach(rooms, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id =>
{
    var watch = Stopwatch.StartNew();
    var report = new StringBuilder();
    try
    {
        var scene = SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Run(game, id);
        var snapshot = scene.Snapshot;
        var source = ScummDataSources.For(game);
        var entry = source.RoomEntryScript(id);
        var instructions = (entry?.Instructions ?? []).Concat(source.RoomLocalScriptIds(id)
            .SelectMany(number => source.RoomLocalScript(id, number)?.Instructions ?? [])).ToArray();
        var mutations = instructions.Where(instruction => instruction.Operation.Contains("State", StringComparison.OrdinalIgnoreCase)
            || instruction.Operation is "drawObject" or "startScript" or "stopScript" or "loadRoom" or "putActorInRoom").ToArray();
        var changes = snapshot.ObjectStates.Where(pair => scene.IndexedStates.GetValueOrDefault(pair.Key) != pair.Value).OrderBy(pair => pair.Key);
        report.AppendLine("   states differing from index: " + string.Join(", ", changes.Select(pair => $"{pair.Key}={pair.Value}")));
        var ops = string.Join("; ", mutations.Select(instruction => $"+0x{instruction.Offset:X4} {instruction.Operation} {instruction.Operands}"));
        report.AppendLine("   local object states: " + string.Join(", ", scene.Records.Select(record => record.Id).Distinct().Order()
            .Select(objectId => $"{objectId}={snapshot.ObjectStates.GetValueOrDefault(objectId)}")));
        report.AppendLine("   hotspots: " + string.Join(',', scene.Records.Where(scene.IsHotspot).Select(record => record.Id)));
        comparisons[id] = (instructions.Length, $"| {id:00} | {instructions.Length} | {scene.Outcome}/{scene.Frames} | {string.Join(',', scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Order())} | {(ops.Length == 0 ? "No room-entry/local state writes or child starts; index and global scripts still apply." : ops.Replace("|", "/"))} |");
        var gaps = scene.Gaps();
        // Before S5: the view painted the objects of the index states only (no script run).
        var indexOnly = new RoomObjectSelection(scene.Images, scene.IndexedStates, new Dictionary<int, RoomObjectPosition>(), scene.Records, scene.Room, scene.IndexedStates, [], scene.IntrinsicStateMask);
        var gapsBefore = indexOnly.VisibleGaps().Count;
        Interlocked.Add(ref totalBefore, gapsBefore);
        if (gapsBefore > 0) Interlocked.Increment(ref roomsBefore);
        report.AppendLine($"   before S5 (index states only): {indexOnly.VisibleGaps().Count} gaps, {scene.Images.Count(indexOnly.IsActive)} active images");
        var active = scene.Images.Count(scene.Selection.IsActive);
        var layer = scene.ActorLayer();
        report.AppendLine("   actor pixel counts: " + string.Join("; ", layer.Actors.Select(actor => $"{actor.Number} pixels={actor.Pixels} frame={actor.Frame} bounds={actor.Bounds}")));
        foreach (var actor in scene.State.Actors.Where(actor => actor.Room == id && actor.Visible == true))
            report.AppendLine($"   actor {actor.Number} limb positions: {string.Join(',', actor.Look.Animation.Positions)}");
        var notFollowed = snapshot.Uncertainties.Where(item => unsupported.Any(item.Contains) && !item.Contains("seeded pick")).Distinct().ToArray();
        report.AppendLine($"== Room {id:00} ({scene.Images.Count} images, {scene.Records.Count} objects) outcome={scene.Outcome} frames={scene.Frames}{(scene.LeftTo is int to ? $" left to {to:00}" : "")} lit={scene.RoomLit} {watch.Elapsed.TotalSeconds:0.0}s");
        foreach (var note in scene.Notes) report.AppendLine("   " + note);
        report.AppendLine($"   active images: {active}; paint order: {string.Join(',', scene.Selection.PaintOrder().Select(o => o.Id))}");
        report.AppendLine($"   actors drawn: {string.Join(", ", layer.Actors.Select(a => $"{a.Number}(costume {a.Costume} at {a.X},{a.Y})"))}; skipped: {string.Join("; ", layer.Skipped)}");
        report.AppendLine($"   pending scripts: {snapshot.PendingScripts}; camera {scene.State.CameraX}; lights {scene.State.Lights}");
        foreach (var gap in gaps) report.AppendLine($"   GAP ({gap.X},{gap.Y}) {gap.Width}x{gap.Height}{(gap.Fillable ? "" : " [no usable stored image]")}");
        foreach (var gap in gaps)
        {
            var background = scene.Room.Pixels.Span;
            var originalBlack = 0;
            for (var y = gap.Y; y < gap.Y + gap.Height; y++)
            for (var x = gap.X; x < gap.X + gap.Width; x++)
                if (x >= 0 && x < scene.Room.Width && y >= 0 && y < scene.Room.Height && background[y * scene.Room.Width + x] == 0) originalBlack++;
            report.AppendLine($"   gap background already black: {originalBlack}/{gap.Width * gap.Height}; same-bounds records: " +
                string.Join(',', scene.Records.Where(record => record.X == gap.X && record.Y == gap.Y && record.Width == gap.Width && record.Height == gap.Height).Select(record => record.Id)));
        }
        foreach (var item in notFollowed) report.AppendLine("   NOT FOLLOWED: " + (item.Length > 260 ? item[..260] + "..." : item));
        WriteBmp(Path.Combine(output, $"room-{id:00}.bmp"), scene.Room, scene.ComposeWithActors(), scene.DisplayPalette());
        lines[id] = (report.ToString(), $"{id:00}\t{scene.Outcome}\t{scene.Frames}\t{active}/{scene.Images.Count}\t{gaps.Count}\t{layer.Actors.Count}\t{notFollowed.Length}\t{scene.RoomLit}\t{watch.Elapsed.TotalSeconds:0.0}", gaps.Count, false);
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException)
    {
        lines[id] = ($"== Room {id:00}: ERROR {ex.GetType().Name}: {ex.Message}\n", $"{id:00}\terror\t\t\t\t\t\t\t", 0, true);
    }
});

var totalGaps = lines.Values.Sum(item => item.Gaps);
var roomsWithGaps = lines.Values.Count(item => item.Gaps > 0);
var failed = lines.Values.Count(item => item.Failed);
var head = $"Before S5 (index states only): rooms with a gap {roomsBefore}, gaps {totalBefore}\nRooms: {rooms.Length}; rooms without a gap: {rooms.Length - roomsWithGaps - failed}; rooms with remaining gaps: {roomsWithGaps} ({totalGaps} gaps); rooms that could not run: {failed}\n\n";
File.WriteAllText(Path.Combine(output, "room-audit.txt"), head + string.Concat(rooms.Select(id => lines[id].Report)));
File.WriteAllText(Path.Combine(output, "room-audit.md"), "# ManiacClassic room audit\n\n```text\n" + head + string.Concat(rooms.Select(id => lines[id].Report)) + "```\n");
File.WriteAllText(Path.Combine(output, "room-audit.tsv"), "room\toutcome\tframes\tactive/images\tgaps\tactors\tnotFollowed\tlit\tseconds\n" + string.Join("\n", rooms.Select(id => lines[id].Row)) + "\n");
File.WriteAllText(Path.Combine(output, "source-comparison.md"), "# Static source comparison\n\nEntry/local instructions ranked by count; this is not original-game observation. Named operations may be on branches not taken.\n\n| Room | Instructions | Outcome/frames | Active images | Decoded state operations and child starts |\n|---|---|---|---|---|\n" + string.Join("\n", comparisons.Values.OrderByDescending(item => item.Weight).Take(12).Select(item => item.Row)) + "\n");
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



using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// Renders every room's first-visit state the way MainWindow does in
// exploration mode (fresh session per room, gap filling when the snapshot is
// not faithful) and reports the colour-0 gaps that remain.
var folder = args[0];
var output = args[1];
var detail = args.Skip(2).Select(int.Parse).ToHashSet();
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var batches = LoomSceneDrawBatches.Build(game);
var doorways = new LoomDoorwayFinder(game);
var dialect = ScummScriptDialects.For(game.Id);
var verbReader = new LoomObjectVerbReader(dialect);
var report = new StringBuilder();
var summary = new StringBuilder("room\tfaithful\tgapsBefore\tgapsAfter\tunfillable\testimates\tdoorways\n");
int totalBefore = 0, totalAfter = 0, roomsWithGaps = 0;

foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value;
    try
    {
        var bytes = File.ReadAllBytes(room.FilePath);
        var background = new LoomEgaRoomDecoder().Decode(bytes);
        var records = new LoomRoomObjectDirectory().Decode(bytes);
        var images = new LoomRoomObjects().Decode(bytes);
        var session = new LoomExplorationSession(game, index);
        var snapshot = session.VisitRoom(room);
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions,
            records, background, index.States, snapshot.DrawOrder);
        var before = selection.VisibleGaps();
        IReadOnlyList<RoomArtworkEstimate> estimates = [];
        IReadOnlyList<int> fallback = [];
        if (!snapshot.IsFaithful)
        {
            estimates = selection.FillGaps(batches.ForRoom(id), seed: id);
            if (!images.Any(selection.IsActive))
                fallback = selection.ShowUnresolvedArtwork();
        }
        var after = selection.VisibleGaps();

        var exits = new SortedSet<int>();
        var tables = verbReader.ReadAll(bytes, records);
        foreach (var record in records)
            if (tables.TryGetValue(record.Id, out var verbs))
                foreach (var verb in verbs.DistinctBy(v => v.ScriptOffset))
                {
                    try { foreach (var t in doorways.Find(room, verbReader.Decode(room, record, verb.VerbId))) exits.Add(t.RoomId); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
                }

        totalBefore += before.Count; totalAfter += after.Count;
        if (after.Count > 0) roomsWithGaps++;
        summary.Append($"{id:00}\t{snapshot.IsFaithful}\t{before.Count}\t{after.Count}\t{after.Count(g => !g.Fillable)}\t{estimates.Count}\t{string.Join(',', exits.Select(e => e.ToString("00")))}\n");
        report.AppendLine($"== Room {id:00} ({images.Count} images, faithful={snapshot.IsFaithful}, pending={snapshot.PendingScripts})");
        report.AppendLine($"   paint order: {string.Join(',', selection.PaintOrder().Select(o => o.Id))}");
        report.AppendLine($"   draw queue:  {string.Join(',', selection.DrawQueue)}");
        if (fallback.Count > 0) report.AppendLine($"   nothing visible; fallback estimate shows {string.Join(',', fallback)}");
        foreach (var e in estimates) report.AppendLine($"   estimate {e.ObjectId}: {e.Reason}");
        foreach (var g in after) report.AppendLine($"   GAP ({g.X},{g.Y}) {g.Width}x{g.Height}{(g.Fillable ? "" : " [no usable stored image]")}");
        report.AppendLine($"   doorways: {string.Join(',', exits.Select(e => e.ToString("00")))}");
        if (detail.Contains(id))
        {
            foreach (var image in images)
                report.AppendLine($"   image {image.Id} ({image.X},{image.Y}) {image.Width}x{image.Height} state={snapshot.ObjectStates.GetValueOrDefault(image.Id)} black={image.Pixels.Count(p => p == 0) * 100 / Math.Max(1, image.Pixels.Length)}%");
            foreach (var batch in batches.ForRoom(id).OrderByDescending(b => b.Priority))
                report.AppendLine($"   batch p{batch.Priority} [{string.Join(',', batch.Steps.Select(s => s.ObjectId))}] {batch.Source}");
        }
        WriteBmp(Path.Combine(output, $"room-{id:00}.bmp"), background, selection.Compose(background));
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
    {
        report.AppendLine($"== Room {id:00}: ERROR {ex.GetType().Name}: {ex.Message}");
        summary.Append($"{id:00}\terror\n");
    }
}

report.Insert(0, $"Rooms with remaining gaps: {roomsWithGaps}; gaps before filling: {totalBefore}; after: {totalAfter}\n\n");
File.WriteAllText(Path.Combine(output, "room-audit.txt"), report.ToString());
File.WriteAllText(Path.Combine(output, "room-audit.tsv"), summary.ToString());
Console.WriteLine($"Rooms with remaining gaps: {roomsWithGaps}; gaps before filling: {totalBefore}; after: {totalAfter}");

static void WriteBmp(string path, RoomImage room, byte[] pixels)
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
            var c = room.Palette[pixels[y * room.Width + x]];
            row[x * 3] = (byte)c; row[x * 3 + 1] = (byte)(c >> 8); row[x * 3 + 2] = (byte)(c >> 16);
        }
        w.Write(row);
    }
}

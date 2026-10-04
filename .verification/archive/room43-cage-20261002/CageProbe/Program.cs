using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// One bounded comparison of the two existing sword capture replay recipes.
// No App reference, input injection, runtime writes, or historical output overwrites.
var output = args[1];
if (Directory.Exists(output)) throw new IOException("Use a fresh evidence directory.");
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(args[0]);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var room = game.Rooms.Single(r => r.ResourceId == 43);
var bytes = File.ReadAllBytes(room.FilePath);
var background = new LoomEgaRoomDecoder().Decode(bytes);
var records = new LoomRoomObjectDirectory().Decode(bytes);
var images = new LoomRoomObjects().Decode(bytes);
var batches = LoomSceneDrawBatches.Build(game);
var options = new JsonSerializerOptions { WriteIndented = true };
void Json(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, options));
if (args.Contains("--artwork-only"))
{
    foreach (var id in new[] { 2, 43, 86 })
    {
        var encoded = File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == id).FilePath);
        var bg = new LoomEgaRoomDecoder().Decode(encoded);
        WriteBmp(Path.Combine(output, $"background-{id}.bmp"), bg, bg.Pixels.ToArray());
    }
    return;
}
Json("objects.json", new { records, images = images.Select(i => new { i.Id, i.X, i.Y, i.Width, i.Height, sha256 = Convert.ToHexString(SHA256.HashData(i.Pixels)) }) });
WriteBmp(Path.Combine(output, "background.bmp"), background, background.Pixels.ToArray());
foreach (var i in images)
    WriteBmp(Path.Combine(output, $"object-{i.Id}.bmp"), new RoomImage(i.Width, i.Height, i.Pixels, background.Palette), i.Pixels);
using var catalog = JsonDocument.Parse(File.ReadAllText("src/SCUMMexplorer.App/loom-replays.json"));
foreach (var verb in new[] { 0x41, 0x4B })
{
    var recipe = catalog.RootElement.GetProperty("replays").EnumerateArray().Single(r =>
        r.GetProperty("room").GetInt32() == 41 && r.GetProperty("objectId").GetInt32() == 633 && r.GetProperty("verb").GetInt32() == verb).Clone();
    var s = new LoomExplorationSession(game, index);
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 41));
    int traceStart = s.SessionTrace.Count, begun = s.CutscenesBegun;
    s.ActivateObjectVerb(633, verb);
    var changes = new List<object>(); var rooms = new List<object>(); var renders = new Dictionary<string, string>();
    string? previous = null; int? previousRoom = null; int idle = 0, frame = 0;
    for (; frame < 3000; frame++)
    {
        var snap = s.Snapshot;
        if (snap.RoomId != previousRoom) { rooms.Add(new { frame, snap.RoomId }); previousRoom = snap.RoomId; previous = null; }
        if (snap.RoomId == 43)
        {
            var states = records.ToDictionary(r => r.Id, r => snap.ObjectStates.GetValueOrDefault(r.Id));
            var raw = new RoomObjectSelection(images, snap.ObjectStates, snap.ObjectPositions, records, background, index.States, snap.DrawOrder);
            var shown = new RoomObjectSelection(images, snap.ObjectStates, snap.ObjectPositions, records, background, index.States, snap.DrawOrder);
            IReadOnlyList<RoomArtworkEstimate> estimates = [];
            // Mirrors MainWindow.CreateSessionRoomSelection / FillArtworkGaps.
            if (!snap.IsFaithful)
            {
                shown.ExcludeInterfaceImages(batches.InterfaceImages.Keys);
                estimates = shown.FillGaps(batches.ForRoom(43), seed: 43);
                if (!images.Any(shown.IsActive)) estimates = [.. estimates, .. shown.ShowUnresolvedArtwork(batches.ForRoom(43), 43, snap.StatesSetByCode, batches.EntryStatesForRoom(43))];
            }
            var rawPixels = raw.Compose(background); var shownPixels = shown.Compose(background);
            var key = Convert.ToHexString(SHA256.HashData(rawPixels)) + Convert.ToHexString(SHA256.HashData(shownPixels));
            if (!renders.TryGetValue(key, out var render))
            {
                render = $"verb-{verb:X2}-render-{renders.Count}"; renders.Add(key, render);
                WriteBmp(Path.Combine(output, render + "-raw.bmp"), background, rawPixels);
                WriteBmp(Path.Combine(output, render + "-shown.bmp"), background, shownPixels);
            }
            var facts = new { states, estimated = snap.EstimatedStates, sourceWritten = snap.StatesSetByCode,
                bits = snap.KnownGlobalVariables.Where(p => p.Key is "bit[0xD96]" or "bit[0xD97]" or "bit[0xDA2]" or "bit[0xDC0]").ToDictionary(),
                rawPaint = raw.PaintOrder().Select(i => i.Id).ToArray(), shownPaint = shown.PaintOrder().Select(i => i.Id).ToArray(),
                estimates, render, messages = snap.ScreenMessages.Select(m => m.Text).ToArray() };
            var stateKey = JsonSerializer.Serialize(facts);
            if (stateKey != previous) { changes.Add(new { frame, facts }); previous = stateKey; }
        }
        var boundaries = s.SessionTrace.Skip(traceStart).ToArray();
        if (s.CutscenesBegun > begun && !s.InCutscene && !s.EgoMoving && !s.CameraMoving && s.Messages.HaveMsg == 0 &&
            boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at ")))
        { if (++idle >= 32) break; } else idle = 0;
        s.AdvanceFrame();
    }
    var events = (IEnumerable<LoomScriptEvent>)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s)!;
    Json($"verb-{verb:X2}.json", new { recipe, frames = frame, rooms, changes, final = s.Snapshot,
        boundaries = s.SessionTrace.Skip(traceStart).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray(),
        events = events.Where(e => e.SourceLabel.Contains("Room 43") || e.SourceScriptId == 90).ToArray() });
    Console.WriteLine($"Verb {verb:X2}: {frame} frames, final Room{s.Snapshot.RoomId}, {changes.Count} Room43 changes, {renders.Count} image pairs.");
}

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

using SCUMMexplorer.Core.Discovery;

// Renders the screen as the session leaves it after a step: the room at the camera (rows 0-143),
// the verb screen left black, and the message layer drawn with charset 0 (ScummV3MessageRenderer).
// Usage: MessageRender <game folder> <output folder> <name> <step>...
// Steps: v:obj:verb (direct verb), c:x,y / d:x,y (click / double click), f:n (n frames), t:"source" (frames until that message shows).
var folder = args[0];
var output = args[1];
var name = args[2];
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var session = new LoomExplorationSession(game, index);
var charset = ScummV3Charset.TryLoad(folder, 0, out var error) ?? throw new InvalidDataException(error);
var log = new List<string>();
foreach (var step in args.Skip(3))
{
    var kind = step[..step.IndexOf(':')];
    var value = step[(step.IndexOf(':') + 1)..];
    switch (kind)
    {
        case "v":
            var parts = value.Split(':');
            session.ActivateObjectVerb(int.Parse(parts[0]), Convert.ToInt32(parts[1], 16));
            break;
        case "c" or "d":
            var point = value.Split(',').Select(int.Parse).ToArray();
            session.Click(point[0], point[1], kind == "d");
            break;
        case "f":
            for (var i = 0; i < int.Parse(value); i++) session.AdvanceFrame();
            break;
        case "t":
            for (var i = 0; i < 200 && session.Messages.Talk?.Source != value; i++) session.AdvanceFrame();
            break;
    }
    log.Add($"after {step}: room {session.Snapshot.RoomId}, camera {session.CameraX}, talk {Describe(session.Messages.Talk)}");
}

var room = game.Rooms.Single(r => r.ResourceId == session.Snapshot.RoomId);
var bytes = File.ReadAllBytes(room.FilePath);
var background = new ScummV3EgaRoomDecoder().Decode(bytes);
var snapshot = session.Snapshot;
var selection = new RoomObjectSelection(new ScummRoomObjects().Decode(bytes), snapshot.ObjectStates, snapshot.ObjectPositions,
    new ScummRoomObjectDirectory().Decode(bytes), background, index.States, snapshot.DrawOrder);
var roomPixels = selection.Compose(background);
var cameraX = session.CameraX ?? background.Width / 2;
var viewLeft = Math.Clamp(cameraX - 160, 0, Math.Max(0, background.Width - 320));
var layer = ScummV3MessageRenderer.Render(snapshot.ScreenMessages, charset);
var screen = new uint[320 * 200];
for (var y = 0; y < 200; y++)
    for (var x = 0; x < 320; x++)
    {
        uint color = 0;
        // The boot's initScreens(0, 144): the room screen is rows 0–143, so screen row y shows room row y.
        var roomY = y - ScummV3MessageRenderer.RoomScreenTop;
        if (roomY >= 0 && y < ScummV3MessageRenderer.VerbScreenTop && roomY < background.Height && viewLeft + x < background.Width)
            color = background.Palette[roomPixels[roomY * background.Width + viewLeft + x]];
        var text = layer.Pixels[y * 320 + x];
        if (text != ScummV3MessageRenderer.Transparent) color = background.Palette[text];
        screen[y * 320 + x] = color;
    }
WriteBmp(Path.Combine(output, $"{name}.bmp"), screen, 320, 200, 3);
log.Add($"view: room x {viewLeft}..{viewLeft + 319}; text bounds {layer.Bounds}");
foreach (var message in snapshot.ScreenMessages) log.Add($"on screen: {Describe(message)}");
File.WriteAllLines(Path.Combine(output, $"{name}.txt"), log);
foreach (var line in log) Console.WriteLine(line);

static string Describe(ScummMessage? m) => m is null ? "none" :
    $"\"{m.Text.Replace("\n", " / ")}\" {m.Kind} actor {m.Actor} at ({m.X}, {m.Y}){(m.Center ? " centred" : "")} colour 0x{m.Color:X2} line height {m.LineHeight} delay {m.TalkDelay} from {m.Source}{(m.Estimate is null ? "" : $" [estimate: {m.Estimate}]")}";

static void WriteBmp(string path, uint[] pixels, int width, int height, int scale)
{
    int w = width * scale, h = height * scale, stride = (w * 3 + 3) & ~3;
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write((ushort)0x4D42); writer.Write(54 + stride * h); writer.Write(0); writer.Write(54);
    writer.Write(40); writer.Write(w); writer.Write(h); writer.Write((ushort)1); writer.Write((ushort)24);
    writer.Write(0); writer.Write(stride * h); writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
    var row = new byte[stride];
    for (var y = h - 1; y >= 0; y--)
    {
        for (var x = 0; x < w; x++)
        {
            var c = pixels[(y / scale) * width + x / scale];
            row[x * 3] = (byte)c; row[x * 3 + 1] = (byte)(c >> 8); row[x * 3 + 2] = (byte)(c >> 16);
        }
        writer.Write(row);
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// DemoProbe <folder> shot <room> <x0> <x1> <scale> <out.bmp> [marks x,y;x,y]: the room's first-visit background over x0..x1 at a scale, the depth mask tinted red, marks as green dots.
static class RoomShot
{
    public static void Run(string folder, int roomId, int x0, int x1, int scale, string output, string marks)
    {
        var game = new GameFolderScanner().Scan(folder);
        var resource = game.Rooms.Single(r => r.ResourceId == roomId);
        var bytes = File.ReadAllBytes(resource.FilePath);
        var room = new ScummV3EgaRoomDecoder().Decode(bytes);
        var records = new ScummRoomObjectDirectory().Decode(bytes);
        var images = new ScummRoomObjects().Decode(bytes);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")));
        session.VisitRoomById(roomId);
        var snapshot = session.Snapshot;
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, records, room, drawQueue: snapshot.DrawOrder);
        var pixels = selection.Compose(room);
        var mask = selection.ComposeDepthMask(room);
        var dots = marks.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(m => m.Split(',')).Select(p => (X: int.Parse(p[0]), Y: int.Parse(p[1]))).ToArray();
        var width = (x1 - x0) * scale; var height = room.Height * scale;
        var bmp = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var rx = x0 + x / scale; var ry = y / scale;
                var color = room.Palette[pixels[ry * room.Width + rx]];
                int r = (int)(color >> 16 & 0xFF), g = (int)(color >> 8 & 0xFF), b = (int)(color & 0xFF);
                if (mask is not null && mask[ry * room.Width + rx] != 0) { r = (r + 255 * 2) / 3; g = g * 2 / 3; b = b * 2 / 3; }
                if (dots.Any(d => Math.Abs(d.X - rx) <= 0 && Math.Abs(d.Y - ry) <= 0)) { r = 0; g = 255; b = 0; }
                var o = ((height - 1 - y) * width + x) * 3;
                bmp[o] = (byte)b; bmp[o + 1] = (byte)g; bmp[o + 2] = (byte)r;
            }
        using var file = File.Create(output);
        using var writer = new BinaryWriter(file);
        writer.Write((byte)'B'); writer.Write((byte)'M'); writer.Write(54 + bmp.Length); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((short)1); writer.Write((short)24);
        writer.Write(0); writer.Write(bmp.Length); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        writer.Write(bmp);
    }
}

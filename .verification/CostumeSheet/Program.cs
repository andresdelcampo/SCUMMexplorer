using SCUMMexplorer.Core.Discovery;

// Usage: CostumeSheet <game folder> <costume id> <output .bmp>
// Draws every animation (frame * 4 + direction) of a costume with the default EGA colours, for a look at what its limbs show.
var game = new GameFolderScanner().Scan(args[0]);
var costume = ScummCostume.Load(game, int.Parse(args[1]))!;
uint[] ega = [0x000000, 0x0000AA, 0x00AA00, 0x00AAAA, 0xAA0000, 0xAA00AA, 0xAA5500, 0xAAAAAA, 0x555555, 0x5555FF, 0x55FF55, 0x55FFFF, 0xFF5555, 0xFF55FF, 0xFFFF55, 0xFFFFFF];
var limit = costume.AnimationLimit;
const int cell = 96, columns = 8;
var count = limit + 1;
var rows = (count + columns - 1) / columns;
var width = columns * cell; var height = rows * cell;
var buffer = new byte[width * height]; Array.Fill(buffer, (byte)0xFF);
var notes = new List<string>();
for (var animation = 0; animation <= limit; animation++)
{
    var state = new ScummCostumeAnimationState();
    var ok = costume.DecodeAnimation(state, animation / 4, animation % 4);
    var cels = ok ? costume.Draw(state, animation % 4) : [];
    notes.Add($"anim {animation} (frame {animation / 4} dir {animation % 4}): decoded={ok} limbs={string.Join(",", cels.Select(c => $"{c.Limb}:{c.Width}x{c.Height}"))}");
    var ox = animation % columns * cell + cell / 2; var oy = animation / columns * cell + cell - 8;
    foreach (var cel in cels)
        for (var r = 0; r < cel.Height; r++)
        for (var c = 0; c < cel.Width; c++)
        {
            var px = ox + cel.Left + c; var py = oy + cel.Top + r;
            var color = cel.Pixels[r * cel.Width + c];
            if (color == 0 || px < 0 || py < 0 || px >= width || py >= height) continue;
            buffer[py * width + px] = costume.MapColor(color, null);
        }
}
using (var file = File.Create(args[2]))
using (var w = new BinaryWriter(file))
{
    var rowSize = width * 3 + (4 - width * 3 % 4) % 4;
    w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + rowSize * height); w.Write(0); w.Write(54);
    w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(rowSize * height); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
    for (var y = height - 1; y >= 0; y--)
    {
        for (var x = 0; x < width; x++)
        {
            var i = buffer[y * width + x]; var rgb = i == 0xFF ? ((x / cell + y / cell) % 2 == 0 ? 0x202020u : 0x282828u) : ega[i & 15];
            w.Write((byte)(rgb & 255)); w.Write((byte)(rgb >> 8 & 255)); w.Write((byte)(rgb >> 16 & 255));
        }
        for (var p = width * 3; p < rowSize; p++) w.Write((byte)0);
    }
}
File.WriteAllLines(Path.ChangeExtension(args[2], ".txt"), notes);

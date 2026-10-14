using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: V1CostumeColors <game folder> table
//        V1CostumeColors <game folder> sheet <output .bmp>
// table: every costume with the colours the scripts give it and the evidence; sheet: each costume's fullest pose, left in the derived colours, right in the old
// estimate (Maniac: the actor table with the costume number as actor; Zak: suit 14), 3x scale on a grey ground.
var game = new GameFolderScanner().Scan(args[0]);
var colors = ScummV1ActorColors.For(game)!;
var costumes = game.Resources.Where(r => r.Kind == ResourceKind.Costume && r.ResourceId.HasValue).OrderBy(r => r.ResourceId).ToList();
if (args[1] == "table")
{
    Console.WriteLine($"{game.Title}: {costumes.Count} costumes, {colors.Settings.Count} constant+variable costume/palette instructions, {colors.UnresolvedInstructions} unresolved (variable operand)");
    var byBasis = costumes.Select(c => colors.ColorOf(c.ResourceId!.Value)).GroupBy(c => c.Basis).Select(g => $"{g.Key}={g.Count()}");
    Console.WriteLine(string.Join(" ", byBasis));
    foreach (var c in costumes)
    {
        var x = colors.ColorOf(c.ResourceId!.Value);
        Console.WriteLine($"costume {x.Costume,2}: skin {x.Skin,2} suit {x.Suit,2} {x.Basis,-12} alt={x.Alternatives}  {x.Evidence}");
    }
    return;
}

byte[] maniacSkin = [8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8];
byte[] maniacSuit = [0, 7, 2, 6, 9, 1, 3, 7, 7, 1, 1, 9, 1, 4, 5, 5, 4, 1, 0, 5, 4, 2, 2, 7, 7];
var renderer = new ScummCostumeRenderer();
var reader = new ScummV3CostumeReader();
const int cell = 160, scale = 3, columns = 4;
var rows = (costumes.Count + columns - 1) / columns;
var width = columns * 2 * cell; var height = rows * cell;
var rgbBuffer = new uint[width * height];
Array.Fill(rgbBuffer, 0xFF303030u);
for (var n = 0; n < costumes.Count; n++)
{
    var resource = costumes[n];
    var id = resource.ResourceId!.Value;
    var container = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
    var offset = resource.Offset!.Value;
    var derived = colors.ColorOf(id);
    var old = game.V1Game == ScummV1Game.ManiacMansion && id < 25
        ? new ScummV1CostumeColor(id, maniacSkin[id], maniacSuit[id], ScummV1ColorBasis.Estimate, "old", 0)
        : new ScummV1CostumeColor(id, 8, 14, ScummV1ColorBasis.Estimate, "old", 0);
    foreach (var (variant, set) in new[] { (0, derived), (1, old) })
    {
        RoomImage? best = null; var bestPixels = 0;
        foreach (var slot in reader.Decode(container, offset).AnimationSlots.Where(s => s.HasData))
        {
            RoomImage? pose;
            try { pose = renderer.DecodePose(container, offset, slot.Index, game.V1Game, set); } catch (InvalidDataException) { continue; }
            if (pose is null) continue;
            var count = pose.Pixels.Span.ToArray().Count(p => p != 0);
            if (count > bestPixels) { best = pose; bestPixels = count; }
        }
        if (best is null) continue;
        var ox = (n % columns * 2 + variant) * cell + (cell - best.Width * scale) / 2;
        var oy = n / columns * cell + (cell - best.Height * scale) / 2;
        var pixels = best.Pixels.Span;
        for (var y = 0; y < best.Height * scale; y++)
        for (var x = 0; x < best.Width * scale; x++)
        {
            var p = pixels[y / scale * best.Width + x / scale];
            if (p == 0) continue;
            var px = ox + x; var py = oy + y;
            if (px < 0 || py < 0 || px >= width || py >= height) continue;
            rgbBuffer[py * width + px] = best.Palette[p] | 0xFF000000u;
        }
    }
    Console.WriteLine($"costume {id}: derived skin {derived.Skin} suit {derived.Suit} ({derived.Basis}); old skin {old.Skin} suit {old.Suit}");
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
            var rgb = rgbBuffer[y * width + x];
            w.Write((byte)(rgb & 255)); w.Write((byte)(rgb >> 8 & 255)); w.Write((byte)(rgb >> 16 & 255));
        }
        for (var p = width * 3; p < rowSize; p++) w.Write((byte)0);
    }
}

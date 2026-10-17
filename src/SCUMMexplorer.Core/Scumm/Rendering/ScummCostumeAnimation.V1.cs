namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummCostume
{
    private readonly ScummV1Game? _classicGame;

    // ScummVM costume.cpp, ClassicCostumeLoader/drawLimb/byleRLEDecode_C64, read 17 October 2026.
    // The v1 resource's offsets use base +4; its eight limbs have six-byte headers and packed pairs of pixels.
    private IReadOnlyList<ScummCostumeCel> DrawClassic(ScummCostumeAnimationState state, int direction)
    {
        var right = direction != 0 || Mirror;
        var xMove = 0;
        var yMove = 0;
        var cels = new List<ScummCostumeCel>();
        for (var limb = 0; limb < 8; limb++)
        {
            var position = state.CurrentPosition[limb];
            if (position == ScummCostumeAnimationState.None || (state.Stopped & (1 << limb)) != 0) continue;
            var code = Command(position & 0x7FFF) & 0x7F;
            if (code == 0x7B) continue;
            var table = 4 + ReadWord(_frameOffsetsStart + limb * 2);
            var cel = 4 + ReadWord(table + code * 2);
            var columns = Byte(cel);
            var height = Byte(cel + 1);
            var width = columns * 8;
            var x = xMove + (sbyte)Byte(cel + 2) * 8;
            var y = yMove - (sbyte)Byte(cel + 3) + 1; // paintCelByleRLE: v0/v1 actor Y is shifted by one.
            xMove += (sbyte)Byte(cel + 4) * 8;
            yMove -= (sbyte)Byte(cel + 5);
            if (columns == 0 || height == 0) continue;
            var pixels = new byte[width * height];
            var source = cel + 6;
            var written = 0;
            while (written < columns * height)
            {
                var run = Byte(source++);
                var repeat = (run & 0x80) != 0;
                var length = run & 0x7F;
                if (length == 0) throw new InvalidDataException("The classic costume contains an empty image run.");
                byte packed = repeat ? Byte(source++) : (byte)0;
                for (var i = 0; i < length && written < columns * height; i++, written++)
                {
                    if (!repeat) packed = Byte(source++);
                    var column = written / height;
                    var row = written % height;
                    for (var pair = 0; pair < 4; pair++)
                    {
                        var color = (byte)((packed >> (6 - pair * 2)) & 3);
                        var at = row * width + column * 8 + pair * 2;
                        pixels[at] = pixels[at + 1] = color;
                    }
                }
            }
            if (!right) pixels = MirrorColumns(pixels, width, height);
            cels.Add(new(limb, right ? x : -x - width, y, width, height, pixels));
        }
        return cels;
    }
}

using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Rendering;

/// <summary>
/// An actor's per-limb costume state (ScummVM <c>CostumeData</c>): the command index each limb is at,
/// the start and end of its command run, the animation it came from, and the stopped-limb bits.
/// </summary>
public sealed class LoomCostumeAnimationState
{
    public const ushort None = 0xFFFF;

    public ushort[] CurrentPosition { get; } = new ushort[16];
    public ushort[] Start { get; } = new ushort[16];
    public ushort[] End { get; } = new ushort[16];
    public ushort[] Animation { get; } = new ushort[16];
    public ushort Stopped { get; set; }
    public int AnimationCounter { get; set; }
    public int SoundCounter { get; set; }

    public LoomCostumeAnimationState() => Reset();

    /// <summary>ScummVM <c>CostumeData::reset</c>.</summary>
    public void Reset()
    {
        Array.Fill(CurrentPosition, None);
        Array.Fill(Start, None);
        Array.Fill(End, None);
        Array.Fill(Animation, None);
        Stopped = 0;
        AnimationCounter = 0;
        SoundCounter = 0;
    }

    public LoomCostumeAnimationState Clone()
    {
        var copy = new LoomCostumeAnimationState { Stopped = Stopped, AnimationCounter = AnimationCounter, SoundCounter = SoundCounter };
        CurrentPosition.CopyTo(copy.CurrentPosition, 0);
        Start.CopyTo(copy.Start, 0);
        End.CopyTo(copy.End, 0);
        Animation.CopyTo(copy.Animation, 0);
        return copy;
    }

    /// <summary>The command index of each limb, or -1 for an unused limb (for traces and tests).</summary>
    public IReadOnlyList<int> Positions => CurrentPosition.Select(value => value == None ? -1 : value & 0x7FFF).ToArray();
}

/// <summary>One drawn limb image, placed relative to the actor's feet (x, y − elevation), in costume colours (0 is transparent).</summary>
public sealed record LoomCostumeCel(int Limb, int Left, int Top, int Width, int Height, byte[] Pixels);

/// <summary>
/// A DOS LOOM (old-bundle, format 0x58) costume with ScummVM's <c>ClassicCostumeLoader</c> animation:
/// <c>costumeDecodeData</c> starts an animation (<c>frame * 4 + direction</c>) on each limb,
/// <c>increaseAnim</c> steps a limb's command index per frame (looping, or holding at the end when the
/// start's extra byte has 0x80), and <c>ClassicCostumeRenderer::drawLimb</c> draws limbs 0–15 with the
/// shared x/y move. Offsets are relative to the resource start (ScummVM's <c>_baseptr</c> for GF_OLD_BUNDLE).
/// </summary>
public sealed class LoomCostume
{
    private const int FrameOffsetsStart = 9;
    private const int AnimationOffsetsStart = 41;
    private readonly byte[] _resource;

    public int Id { get; }
    /// <summary>The highest animation index (ScummVM <c>_numAnim</c>).</summary>
    public int AnimationLimit { get; }
    /// <summary>Header bit 0x80: the costume has its own left-facing animations and is never mirrored.</summary>
    public bool Mirror { get; }
    /// <summary>The costume colour drawn as the actor's palette entry 0 (ScummVM <c>_palette[_loaded._palette[0]] = _palette[0]</c>).</summary>
    public byte PaletteColor { get; }
    public int AnimationCommandsOffset { get; }

    private LoomCostume(int id, byte[] resource)
    {
        Id = id;
        _resource = resource;
        AnimationLimit = resource[4];
        var format = resource[5] & 0x7F;
        if (format != 0x58)
            throw new NotSupportedException($"Costume format 0x{format:X2} is not the DOS LOOM format 0x58.");
        Mirror = (resource[5] & 0x80) != 0;
        PaletteColor = resource[6];
        AnimationCommandsOffset = ReadWord(7);
        if (AnimationCommandsOffset >= resource.Length || AnimationOffsetsStart + (AnimationLimit + 1) * 2 > resource.Length)
            throw new InvalidDataException("The costume tables are outside the resource.");
    }

    public static LoomCostume Read(ReadOnlySpan<byte> decodedContainer, int resourceOffset, int id = 0)
    {
        if (resourceOffset < 0 || resourceOffset > decodedContainer.Length - 2)
            throw new InvalidDataException("The costume offset is outside its LFL container.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(decodedContainer[resourceOffset..]);
        if (length < AnimationOffsetsStart || length > decodedContainer.Length - resourceOffset)
            throw new InvalidDataException("The costume resource length is invalid.");
        return new LoomCostume(id, decodedContainer.Slice(resourceOffset, length).ToArray());
    }

    /// <summary>Reads costume <paramref name="id"/> from the game's indexed resources, or returns null when it is not indexed.</summary>
    public static LoomCostume? Load(GameLibrary game, int id)
    {
        var resource = game.Resources.FirstOrDefault(entry => entry.Kind == ResourceKind.Costume && entry.ResourceId == id);
        if (resource?.Offset is not int offset || !File.Exists(resource.FilePath)) return null;
        return Read(LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath)), offset, id);
    }

    /// <summary>ScummVM <c>newDirToOldDir</c>: 0 left (W), 1 right (E), 2 down (S), 3 up (N).</summary>
    public static int NewDirToOldDir(int facing)
    {
        facing = ((facing % 360) + 360) % 360;
        if (facing is >= 71 and <= 109) return 1;
        if (facing is >= 109 and <= 251) return 2;
        if (facing is >= 251 and <= 289) return 0;
        return 3;
    }

    /// <summary>ScummVM <c>oldDirToNewDir</c>.</summary>
    public static int OldDirToNewDir(int direction) => (direction & 3) switch { 0 => 270, 1 => 90, 2 => 180, _ => 0 };

    /// <summary>
    /// ScummVM <c>ClassicCostumeLoader::costumeDecodeData</c> (v3): starts animation <c>direction + frame * 4</c>
    /// on the limbs in its mask that <paramref name="useMask"/> allows (bit 0x8000 is limb 0). Returns false
    /// when the animation is beyond the costume or empty, as ScummVM returns without changing anything.
    /// </summary>
    public bool DecodeAnimation(LoomCostumeAnimationState state, int frame, int direction, uint useMask = 0xFFFF)
    {
        var animation = direction + frame * 4;
        if (frame < 0 || animation > AnimationLimit) return false;
        var cursor = ReadWord(AnimationOffsetsStart + animation * 2);
        if (cursor == 0) return false;

        var mask = (uint)ReadWord(cursor);
        cursor += 2;
        for (var limb = 0; (mask & 0xFFFF) != 0; limb++, useMask <<= 1, mask <<= 1)
        {
            if ((mask & 0x8000) == 0) continue;
            int command = Byte(cursor++);
            if (command == 0xFF) command = LoomCostumeAnimationState.None;
            if ((useMask & 0x8000) != 0)
            {
                if (command == LoomCostumeAnimationState.None)
                {
                    state.CurrentPosition[limb] = LoomCostumeAnimationState.None;
                    state.Start[limb] = 0;
                    state.Animation[limb] = (ushort)animation;
                    continue;
                }
                var extra = Byte(cursor++);
                var code = Command(command);
                if (code == 0x7A)
                    state.Stopped &= (ushort)~(1 << limb);
                else if (code == 0x79)
                    state.Stopped |= (ushort)(1 << limb);
                else
                {
                    state.Start[limb] = (ushort)command;
                    state.CurrentPosition[limb] = (ushort)(command | ((extra & 0x80) != 0 ? 0x8000 : 0));
                    state.End[limb] = (ushort)(command + (extra & 0x7F));
                    state.Animation[limb] = (ushort)animation;
                }
            }
            else if (command != LoomCostumeAnimationState.None)
                cursor++;
        }
        return true;
    }

    /// <summary>ScummVM <c>increaseAnims</c>: steps every used limb; true when any limb's drawn image changed.</summary>
    public bool IncreaseAnimations(LoomCostumeAnimationState state)
    {
        var changed = false;
        for (var limb = 0; limb < 16; limb++)
            if (state.CurrentPosition[limb] != LoomCostumeAnimationState.None)
                changed |= IncreaseAnimation(state, limb);
        return changed;
    }

    /// <summary>
    /// ScummVM <c>ClassicCostumeLoader::increaseAnim</c> for v3: without the hold flag the index runs from start
    /// to end and loops; with it, the index stops at the end. Command 0x7C counts the animation counter and 0x78
    /// the sound counter and are skipped unless the run is one command long.
    /// </summary>
    public bool IncreaseAnimation(LoomCostumeAnimationState state, int limb)
    {
        var position = state.CurrentPosition[limb];
        if (position == LoomCostumeAnimationState.None) return false;
        var hold = position & 0x8000;
        int index = position & 0x7FFF;
        int end = state.End[limb];
        var code = Command(index) & 0x7F;
        if ((Command(index) & 0x80) != 0) state.SoundCounter++;

        // A run whose commands are all 0x7C/0x78 would spin forever in ScummVM too; stop after one lap.
        for (var guard = 0; guard <= 0x8000; guard++)
        {
            if (hold == 0)
            {
                if (index++ >= end) index = state.Start[limb];
            }
            else if (index != end)
                index++;

            var next = Command(index);
            if (next == 0x7C)
            {
                state.AnimationCounter++;
                if (state.Start[limb] != end) continue;
            }
            else if (next == 0x78)
            {
                state.SoundCounter++;
                if (state.Start[limb] != end) continue;
            }
            state.CurrentPosition[limb] = (ushort)(index | hold);
            return (Command(index) & 0x7F) != code;
        }
        return false;
    }

    /// <summary>
    /// The limb images as <c>ClassicCostumeRenderer::drawCostume</c>/<c>drawLimb</c> place them, relative to the actor's
    /// point (x, y − elevation), in draw order (limb 0 first, later limbs on top). <paramref name="direction"/> is the old
    /// direction (0 left, 1 right, 2 down, 3 up); left is drawn mirrored unless the costume has the mirror flag.
    /// </summary>
    public IReadOnlyList<LoomCostumeCel> Draw(LoomCostumeAnimationState state, int direction)
    {
        var drawToRight = direction != 0 || Mirror;
        var xMove = -72;
        var yMove = -100;
        var cels = new List<LoomCostumeCel>();
        for (var limb = 0; limb < 16; limb++)
        {
            var position = state.CurrentPosition[limb];
            if (position == LoomCostumeAnimationState.None || (state.Stopped & (1 << limb)) != 0) continue;
            var code = Command(position & 0x7FFF) & 0x7F;
            if (code == 0x7B) continue;

            var frameTable = ReadWord(FrameOffsetsStart + limb * 2);
            if (frameTable + code * 2 + 2 > _resource.Length) continue;
            var cel = ReadWord(frameTable + code * 2);
            if (cel == 0 || cel + 12 > _resource.Length) continue;

            var width = ReadWord(cel);
            var height = ReadWord(cel + 2);
            var xCurrent = xMove + ReadSigned(cel + 4);
            var yCurrent = yMove + ReadSigned(cel + 6);
            xMove += ReadSigned(cel + 8);
            yMove -= ReadSigned(cel + 10);
            if (width == 0 || height == 0 || width > 1024 || height > 1024) continue;

            // Unscaled placement (BaseCostumeRenderer::paintCelByleRLECommon): mirrored cels end at x.
            var left = drawToRight ? xCurrent : -xCurrent - width;
            var pixels = DecodeCel(cel + 12, width, height);
            if (pixels is null) continue;
            if (!drawToRight) pixels = MirrorColumns(pixels, width, height);
            cels.Add(new LoomCostumeCel(limb, left, yCurrent, width, height, pixels));
        }
        return cels;
    }

    /// <summary>
    /// Maps a costume colour to the EGA colour drawn (<c>ClassicCostumeRenderer::setPalette</c> for GF_OLD_BUNDLE with
    /// the lights on): entry c of the actor's palette, except the costume's palette colour, which draws entry 0.
    /// </summary>
    public byte MapColor(byte color, IReadOnlyList<byte>? actorPalette) =>
        actorPalette is null
            ? color == PaletteColor ? (byte)0 : color
            : color == PaletteColor ? actorPalette[0] : actorPalette[color & 15];

    /// <summary>
    /// Draws the cels into an indexed room buffer at the actor's point, clipped to the buffer. Transparent pixels
    /// (costume colour 0) are skipped. Returns the number of pixels written.
    /// </summary>
    public int Compose(IReadOnlyList<LoomCostumeCel> cels, byte[] target, int targetWidth, int targetHeight,
        int x, int y, IReadOnlyList<byte>? actorPalette = null)
    {
        var written = 0;
        foreach (var cel in cels)
        for (var row = 0; row < cel.Height; row++)
        {
            var ty = y + cel.Top + row;
            if (ty < 0 || ty >= targetHeight) continue;
            for (var column = 0; column < cel.Width; column++)
            {
                var tx = x + cel.Left + column;
                if (tx < 0 || tx >= targetWidth) continue;
                var color = cel.Pixels[row * cel.Width + column];
                if (color == 0) continue;
                target[ty * targetWidth + tx] = MapColor(color, actorPalette);
                written++;
            }
        }
        return written;
    }

    /// <summary>The raw animation command at an index (0x79 hide, 0x7A show, 0x7B nothing, 0x7C/0x78 counters, else a cel code).</summary>
    public byte Command(int index)
    {
        var offset = AnimationCommandsOffset + index;
        return offset >= 0 && offset < _resource.Length ? _resource[offset] : (byte)0x7B;
    }

    private byte[]? DecodeCel(int offset, int width, int height)
    {
        // ByleRLE, column by column: high nibble colour, low nibble length (0 = the next byte).
        var pixels = new byte[width * height];
        var written = 0;
        var source = offset;
        while (written < pixels.Length)
        {
            if (source >= _resource.Length) return null;
            var run = _resource[source++];
            var color = (byte)(run >> 4);
            var length = run & 0x0F;
            if (length == 0)
            {
                if (source >= _resource.Length) return null;
                length = _resource[source++];
            }
            if (length == 0) return null;
            for (var i = 0; i < length && written < pixels.Length; i++, written++)
                if (color != 0) pixels[written % height * width + written / height] = color;
        }
        return pixels;
    }

    private static byte[] MirrorColumns(byte[] pixels, int width, int height)
    {
        var mirrored = new byte[pixels.Length];
        for (var row = 0; row < height; row++)
        for (var column = 0; column < width; column++)
            mirrored[row * width + column] = pixels[row * width + width - 1 - column];
        return mirrored;
    }

    private byte Byte(int offset) =>
        offset >= 0 && offset < _resource.Length ? _resource[offset] : throw new InvalidDataException("The costume animation data is incomplete.");

    private int ReadWord(int offset) =>
        offset >= 0 && offset + 2 <= _resource.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(_resource.AsSpan(offset))
            : throw new InvalidDataException("The costume data is incomplete.");

    private int ReadSigned(int offset) => unchecked((short)ReadWord(offset));
}

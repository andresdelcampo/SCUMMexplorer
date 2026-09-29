using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Rendering;

/// <summary>
/// An old-v3 charset (ScummVM <c>ScummEngine_v3::loadCharset</c>): charset n is
/// the file <c>99 - n</c>.LFL, not XOR-encoded, a little-endian size and then
/// the resource. <c>CharsetRendererV3::setCurID</c> reads the number of glyphs
/// at +4, the font height at +5, a width table, then 8 bytes per glyph (one
/// byte per row, the high bit on the left).
/// </summary>
public sealed class LoomCharset
{
    /// <summary>Every glyph is drawn 8 rows high (<c>getDrawHeightIntern</c>).</summary>
    public const int GlyphRows = 8;

    private readonly byte[] _widths;
    private readonly byte[] _glyphs;

    private LoomCharset(int number, string path, int height, byte[] widths, byte[] glyphs)
    {
        Number = number;
        FilePath = path;
        FontHeight = height;
        _widths = widths;
        _glyphs = glyphs;
    }

    public int Number { get; }
    public string FilePath { get; }
    /// <summary>The line height for new lines (<c>getFontHeight</c>).</summary>
    public int FontHeight { get; }
    public int GlyphCount => _widths.Length;

    /// <summary>Reads charset <paramref name="number"/> from a LOOM folder; null with a reason when it cannot.</summary>
    public static LoomCharset? TryLoad(string gameFolder, int number, out string? error)
    {
        var path = Path.Combine(gameFolder, $"{99 - number:00}.LFL");
        error = null;
        try
        {
            return Parse(File.ReadAllBytes(path), number, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = $"charset {number} ({Path.GetFileName(path)}) could not be read: {ex.Message}";
            return null;
        }
    }

    public static LoomCharset Parse(byte[] file, int number = 0, string path = "")
    {
        // loadCharset reads the size word, then that many bytes (the file may end first).
        if (file.Length < 2 + 6) throw new InvalidDataException("The charset file is too short.");
        var resource = file.AsSpan(2);
        var count = resource[4];
        var height = resource[5];
        if (count == 0 || height == 0 || resource.Length < 6 + count + count * GlyphRows)
            throw new InvalidDataException($"The charset declares {count} glyphs of height {height}, which the file does not hold.");
        return new LoomCharset(number, path, height, resource.Slice(6, count).ToArray(),
            resource.Slice(6 + count, count * GlyphRows).ToArray());
    }

    /// <summary><c>getCharWidth</c>: the glyph's width from the table (0 outside it).</summary>
    public int CharWidth(int glyph) => glyph >= 0 && glyph < _widths.Length ? _widths[glyph] : 0;

    /// <summary>
    /// ScummVM <c>getStringWidth</c> for v3 (no extra pixel): the width of the
    /// text up to the end, a new line (codes 1, 2, 9) or a wait (code 3); '@' and
    /// codes 10, 12, 13 with their operand add nothing.
    /// </summary>
    public int StringWidth(IReadOnlyList<byte> text, int start = 0)
    {
        var width = 0;
        for (var pos = start; pos < text.Count;)
        {
            int chr = text[pos++];
            if (chr == 0) break;
            if (chr == '@') continue;
            if (chr is 0xFF or 0xFE)
            {
                chr = pos < text.Count ? text[pos++] : 0;
                if (chr == 3) break;
                if (chr == 8)
                {
                    while (pos < text.Count && text[pos++] == ' ') { }
                    continue;
                }
                if (chr is 10 or 21 or 12 or 13)
                {
                    pos += 2;
                    continue;
                }
                if (chr is 9 or 1 or 2) break;
                if (chr == 14)
                {
                    pos += 2;
                    continue;
                }
            }
            width += CharWidth(chr);
        }
        return width;
    }

    internal ReadOnlySpan<byte> Glyph(int glyph) => _glyphs.AsSpan(glyph * GlyphRows, GlyphRows);
}

/// <summary>A message drawn into a screen-sized layer of palette indices.</summary>
/// <param name="Pixels">320 × 200 palette indices; <see cref="LoomMessageRenderer.Transparent"/> where nothing was drawn.</param>
/// <param name="Bounds">The drawn rectangle (screen coordinates), or null when nothing was drawn.</param>
public sealed record LoomMessageLayer(byte[] Pixels, int Width, int Height, (int Left, int Top, int Right, int Bottom)? Bounds);

/// <summary>
/// Draws messages with the game's charset as ScummVM does for LOOM EGA:
/// <c>displayDialog</c>/<c>newLine</c> for talk text (each line centred on the
/// slot's x, clamped at 0), <c>drawString</c> for drawn strings, and
/// <c>CharsetRendererV3::printChar</c> with <c>CharsetRendererPC::drawBits1</c>
/// for the pixels. The colour's low 4 bits are the EGA colour; bit 0x80 adds
/// a shadow and, for talk text, bit 0x40 an outline, both in colour 0
/// (<c>CharsetRendererV3::setColor</c> for LOOM v3).
/// </summary>
public static class LoomMessageRenderer
{
    public const int ScreenWidth = 320;
    public const int ScreenHeight = 200;
    /// <summary>
    /// LOOM's boot (script 001 +0x0000) runs <c>roomOps setScreen 0, 144</c>, ScummVM <c>initScreens(0, 144)</c>
    /// (<c>o5_roomOps</c> case 3, <c>gfx.cpp</c>): the room (main) screen is rows 0–143, so screen row y is room row y,
    /// the text screen has no rows (text is drawn over the room), and the verb screen is rows 144–199.
    /// <c>printChar</c> draws at <c>_top - vs-&gt;topline</c> and clips at the screen's bottom.
    /// </summary>
    public const int RoomScreenTop = 0;
    public const int VerbScreenTop = 144;
    public const byte Transparent = 0xFF;
    private const byte ShadowColor = 0;

    public static LoomMessageLayer Render(IEnumerable<LoomMessage> messages, LoomCharset charset)
    {
        var layer = new Layer();
        foreach (var message in messages)
        {
            // A box drawn after the string covers its pixels there (drawBox paints over it).
            layer.ErasedBy = message.ErasedBy;
            if (message.Kind == LoomMessageKind.Talk) DrawTalk(layer, message, charset);
            else DrawDrawnString(layer, message, charset);
            layer.ErasedBy = [];
        }
        return new LoomMessageLayer(layer.Pixels, ScreenWidth, ScreenHeight, layer.Bounds);
    }

    public static LoomMessageLayer Render(LoomMessage message, LoomCharset charset) => Render([message], charset);

    private static void DrawTalk(Layer layer, LoomMessage message, LoomCharset charset)
    {
        var (color, shadow) = TextColor(message.Color, outlineAllowed: true);
        var top = message.Y;
        for (var index = 0; index < message.LineGlyphs.Count; index++)
        {
            var line = message.LineGlyphs[index];
            if (index > 0) top += message.LineHeight != 0 ? message.LineHeight : charset.FontHeight;
            // displayDialog and newLine: a centred line starts half its width left of x, at 0 at least.
            var left = message.X;
            if (message.Center)
            {
                left -= charset.StringWidth(line) / 2;
                if (left < 0) left = 0;
            }
            foreach (var glyph in line)
            {
                // v3: a line that runs past the screen's width stays at its edge.
                if (left >= ScreenWidth) left = ScreenWidth;
                if (!PrintChar(layer, charset, glyph, ref left, top, message.Right, color, shadow)) continue;
            }
        }
    }

    private static void DrawDrawnString(Layer layer, LoomMessage message, LoomCharset charset)
    {
        // drawString: the shadow bit applies; bit 0x40 gives no outline in the room screen.
        var (color, shadow) = TextColor(message.Color, outlineAllowed: message.Y >= VerbScreenTop);
        var startLeft = message.X;
        var top = message.Y;
        for (var index = 0; index < message.LineGlyphs.Count; index++)
        {
            var line = message.LineGlyphs[index];
            int left;
            if (index == 0)
                left = message.Center ? startLeft - charset.StringWidth(line) / 2 : startLeft;
            else
            {
                // Codes 1 and 8: ScummVM takes the next line's whole width off the start when centring,
                // and moves down by the font height unless text slot 0 has a height of its own.
                left = message.Center ? startLeft - charset.StringWidth(line) : startLeft;
                if (message.LineHeight == 0) top += charset.FontHeight;
            }
            foreach (var glyph in line)
                PrintChar(layer, charset, glyph, ref left, top, message.Right, color, shadow);
        }
    }

    /// <summary><c>setColor</c> for LOOM v3: 0x40 is an outline (not for drawn strings in the room), else 0x80 a shadow.</summary>
    private static (byte Color, Shadow Shadow) TextColor(int value, bool outlineAllowed)
    {
        var shadow = (value & 0x40) != 0
            ? outlineAllowed ? Shadow.Outline : Shadow.None
            : (value & 0x80) != 0 ? Shadow.Normal : Shadow.None;
        return ((byte)(value & 0x0F), shadow);
    }

    /// <summary><c>CharsetRendererV3::printChar</c>: false when the glyph was clipped at the right edge (the position stays).</summary>
    private static bool PrintChar(Layer layer, LoomCharset charset, int glyph, ref int left, int top, int right, byte color, Shadow shadow)
    {
        if (glyph == '@') return false;
        var width = charset.CharWidth(glyph);
        if (left + width > right + 1) return false;
        // findVirtScreen(top): the room or the verb screen, each clipped at its bottom.
        var screenTop = top >= VerbScreenTop ? VerbScreenTop : RoomScreenTop;
        var screenBottom = top >= VerbScreenTop ? ScreenHeight : VerbScreenTop;
        if (top < 0 || top >= ScreenHeight) return false;
        DrawBits1(layer, charset.Glyph(glyph), left, top, width, LoomCharset.GlyphRows, color, shadow, screenTop, screenBottom);
        left += width;
        return true;
    }

    /// <summary>ScummVM <c>CharsetRendererPC::drawBits1</c>.</summary>
    private static void DrawBits1(Layer layer, ReadOnlySpan<byte> glyph, int x0, int y0, int width, int height,
        byte color, Shadow shadow, int screenTop, int screenBottom)
    {
        if (shadow == Shadow.Outline)
        {
            x0++;
            y0++;
        }
        byte bits = 0, previousBits = 0;
        var row = 0;
        for (var y = 0; y < height && y0 + y < screenBottom; y++)
        {
            var leftShade = false;
            for (var x = 0; x < width; x++)
            {
                if (x % 8 == 0)
                {
                    previousBits = (byte)~bits;
                    bits = glyph[row++];
                    leftShade = true;
                }
                var mask = 0x80 >> (x % 8);
                if ((bits & mask) != 0)
                {
                    var px = x0 + x;
                    var py = y0 + y;
                    if (shadow == Shadow.Normal)
                    {
                        layer.Set(px + 1, py, ShadowColor, screenTop, screenBottom);
                        layer.Set(px + 1, py + 1, ShadowColor, screenTop, screenBottom);
                        layer.Set(px, py + 1, ShadowColor, screenTop, screenBottom);
                    }
                    else if (shadow == Shadow.Outline)
                    {
                        layer.Set(px + 1, py, ShadowColor, screenTop, screenBottom);
                        layer.Set(px, py + 1, ShadowColor, screenTop, screenBottom);
                        layer.Set(px + 1, py + 1, ShadowColor, screenTop, screenBottom);
                        if (leftShade)
                        {
                            layer.Set(px - 1, py, ShadowColor, screenTop, screenBottom);
                            leftShade = false;
                        }
                        if ((previousBits & mask) != 0) layer.Set(px, py - 1, ShadowColor, screenTop, screenBottom);
                    }
                    layer.Set(px, py, color, screenTop, screenBottom);
                }
                else leftShade = true;
            }
        }
    }

    private enum Shadow { None, Normal, Outline }

    private sealed class Layer
    {
        public byte[] Pixels { get; } = Enumerable.Repeat(Transparent, ScreenWidth * ScreenHeight).ToArray();
        public (int Left, int Top, int Right, int Bottom)? Bounds { get; private set; }
        public IReadOnlyList<LoomScreenBox> ErasedBy { get; set; } = [];

        public void Set(int x, int y, byte value, int screenTop, int screenBottom)
        {
            if (x < 0 || x >= ScreenWidth || y < screenTop || y >= screenBottom) return;
            foreach (var box in ErasedBy)
                if (box.Contains(x, y)) return;
            Pixels[y * ScreenWidth + x] = value;
            Bounds = Bounds is { } b
                ? (Math.Min(b.Left, x), Math.Min(b.Top, y), Math.Max(b.Right, x + 1), Math.Max(b.Bottom, y + 1))
                : (x, y, x + 1, y + 1);
        }
    }
}

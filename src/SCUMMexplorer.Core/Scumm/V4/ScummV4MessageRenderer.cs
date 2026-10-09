
namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Draws messages with a CD-game charset (ScummVM <c>CharsetRendererClassic</c>): the same message layout as the EGA renderer
/// (talk lines centred on the slot's x and clamped at 0, drawn strings centred on the first line), the glyphs of
/// <see cref="ScummV4Charset"/>. The colour is a whole palette index (no shadow or outline bits): pixel value 1 takes the
/// message colour, the others the charset's colour map (the outline of the 2-bit fonts is map entry 2, black).
/// </summary>
public static class ScummV4MessageRenderer
{
    public static ScummMessageLayer Render(IEnumerable<ScummMessage> messages, ScummV4Charset charset) =>
        Render(messages, _ => charset);

    /// <summary>Each message in the charset it was printed with (<see cref="ScummMessage.Charset"/>, null when none was set).</summary>
    public static ScummMessageLayer Render(IEnumerable<ScummMessage> messages, Func<int?, ScummV4Charset> charsetFor)
    {
        var layer = new Layer();
        foreach (var message in messages)
        {
            var charset = charsetFor(message.Charset);
            layer.ErasedBy = message.ErasedBy;
            var top = message.Y;
            for (var index = 0; index < message.LineGlyphs.Count; index++)
            {
                var line = message.LineGlyphs[index];
                int left;
                if (message.Kind == ScummMessageKind.Talk)
                {
                    if (index > 0) top += message.LineHeight != 0 ? message.LineHeight : charset.FontHeight;
                    left = message.X;
                    if (message.Center) left = Math.Max(0, left - charset.StringWidth(line) / 2);
                }
                else if (index == 0) left = message.Center ? message.X - charset.StringWidth(line) / 2 : message.X;
                else
                {
                    left = message.Center ? message.X - charset.StringWidth(line) : message.X;
                    if (message.LineHeight == 0) top += charset.FontHeight;
                }
                foreach (var glyph in line)
                {
                    var width = charset.CharWidth(glyph);
                    if (left + width > message.Right + 1) continue;
                    var (x0, y0) = (left, top);
                    charset.DrawGlyph(glyph, (x, y, value) =>
                        layer.Set(x0 + x, y0 + y, value == 1 ? (byte)message.Color : ColorOf(charset, value)));
                    left += width;
                }
            }
            layer.ErasedBy = [];
        }
        return new ScummMessageLayer(layer.Pixels, ScummV3MessageRenderer.ScreenWidth, ScummV3MessageRenderer.ScreenHeight, layer.Bounds);
    }

    public static ScummMessageLayer Render(ScummMessage message, ScummV4Charset charset) => Render([message], charset);

    public static ScummMessageLayer Render(ScummMessage message, Func<int?, ScummV4Charset> charsetFor) => Render([message], charsetFor);

    private static byte ColorOf(ScummV4Charset charset, int value) => charset.ColorMap[value - 1];

    private sealed class Layer
    {
        public byte[] Pixels { get; } = Enumerable.Repeat(ScummV3MessageRenderer.Transparent,
            ScummV3MessageRenderer.ScreenWidth * ScummV3MessageRenderer.ScreenHeight).ToArray();
        public (int Left, int Top, int Right, int Bottom)? Bounds { get; private set; }
        public IReadOnlyList<ScummScreenBox> ErasedBy { get; set; } = [];

        public void Set(int x, int y, byte value)
        {
            if (x < 0 || x >= ScummV3MessageRenderer.ScreenWidth || y < 0 || y >= ScummV3MessageRenderer.ScreenHeight) return;
            foreach (var box in ErasedBy)
                if (box.Contains(x, y)) return;
            Pixels[y * ScummV3MessageRenderer.ScreenWidth + x] = value;
            Bounds = Bounds is { } b
                ? (Math.Min(b.Left, x), Math.Min(b.Top, y), Math.Max(b.Right, x + 1), Math.Max(b.Bottom, y + 1))
                : (x, y, x + 1, y + 1);
        }
    }
}

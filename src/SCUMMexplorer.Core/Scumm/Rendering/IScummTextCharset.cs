
namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A game charset as play draws text with it (S6, 16 October 2026): text verbs (ScummVM <c>drawVerb</c>, <c>drawString</c> of string slot 4) and the
/// messages on screen. Two renderers stand behind it: the old-v3 one (<c>CharsetRendererV3</c>, 1 bit, 8 rows, EGA colour in the low 4 bits) and the
/// classic one of version 4 and later (<c>CharsetRendererClassic</c>, a colour map, the colour a whole palette index; charset.cpp, read 16 October 2026).
/// </summary>
public interface IScummTextCharset
{
    int Number { get; }

    /// <summary>The line height for new lines (<c>getFontHeight</c>).</summary>
    int FontHeight { get; }

    /// <summary>How many rows a line of text covers below its top: <c>_charset-&gt;_str.bottom</c> of a drawn verb (its <c>curRect.bottom</c>).</summary>
    int LineRows { get; }

    int CharWidth(int glyph);

    int StringWidth(IReadOnlyList<byte> text, int start = 0);

    /// <summary>One line of glyphs in one colour into an indexed buffer whose first row is screen row <paramref name="originY"/>; returns the right edge reached.</summary>
    int DrawPlainLine(byte[] pixels, int width, int originY, IReadOnlyList<byte> glyphs, int left, int top, byte color);

    /// <summary>The messages on screen, each in this charset or (classic renderer) in the one it was printed with, from <paramref name="charsetFor"/>.</summary>
    ScummMessageLayer RenderMessages(IEnumerable<ScummMessage> messages, Func<int?, IScummTextCharset?> charsetFor);
}

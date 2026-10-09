using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A charset of the CD game (<c>901.LFL</c> to <c>904.LFL</c>, charsets 1 to 4; plain files, not XOR-encoded). ScummVM
/// <c>CharsetRendererClassic</c> (SCUMM v4): a size dword, the signature 0x0363, a 16-byte colour map, then the font:
/// bits per pixel, font height, a glyph count word, a dword offset per glyph (0 = none, counted from the font's start),
/// and per glyph width, height, x offset and y offset (signed) bytes followed by the pixels, packed bits per pixel
/// running on across rows. A glyph's advance is its width plus its x offset (<c>getCharWidth</c>).
/// </summary>
public sealed class ScummV4Charset
{
    private const int FontStart = 21;
    private readonly byte[] _file;
    private readonly int[] _offsets;

    private ScummV4Charset(int number, byte[] file, int bitsPerPixel, int height, int[] offsets)
    {
        Number = number;
        _file = file;
        BitsPerPixel = bitsPerPixel;
        FontHeight = height;
        _offsets = offsets;
    }

    public int Number { get; }
    public int BitsPerPixel { get; }
    /// <summary>The line height for new lines (<c>getFontHeight</c>).</summary>
    public int FontHeight { get; }
    public int GlyphCount => _offsets.Length;

    /// <summary>The 15-entry colour map the file carries (ScummVM <c>_charsetData</c>): pixel value n draws entry n - 1; the message colour replaces entry 0 (value 1). A 2-bit font's value 2 is black here, its outline.</summary>
    public byte[] ColorMap => _file[6..21];

    public static ScummV4Charset Parse(byte[] file, int number = 0)
    {
        if (file.Length < FontStart + 4 || BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(4)) != 0x0363)
            throw new InvalidDataException("The charset does not start with the 0x0363 signature.");
        var bits = file[FontStart];
        var height = file[FontStart + 1];
        var count = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(FontStart + 2));
        if (bits is not (1 or 2 or 4 or 8) || height == 0 || count == 0 || file.Length < FontStart + 4 + count * 4)
            throw new InvalidDataException($"The charset declares {count} glyphs of height {height} at {bits} bits per pixel, which the file does not hold.");
        var offsets = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(FontStart + 4 + i * 4));
            offsets[i] = offset != 0 && FontStart + offset + 4 <= file.Length ? FontStart + offset : 0;
        }
        return new ScummV4Charset(number, file, bits, height, offsets);
    }

    /// <summary><c>getCharWidth</c>: width plus x offset of the glyph (0 where the font has none).</summary>
    public int CharWidth(int glyph) =>
        glyph >= 0 && glyph < _offsets.Length && _offsets[glyph] != 0 ? _file[_offsets[glyph]] + (sbyte)_file[_offsets[glyph] + 2] : 0;

    /// <summary>The width of the text up to its end or a new line (codes 1, 2, 9) or wait (code 3); the control codes add nothing.</summary>
    public int StringWidth(IReadOnlyList<byte> text, int start = 0)
    {
        var width = 0;
        for (var pos = start; pos < text.Count;)
        {
            int chr = text[pos++];
            if (chr == 0) break;
            if (chr is 0xFF or 0xFE)
            {
                chr = pos < text.Count ? text[pos++] : 0;
                if (chr is 3 or 9 or 1 or 2) break;
                if (chr == 8)
                {
                    while (pos < text.Count && text[pos++] == ' ') { }
                    continue;
                }
                if (chr is 10 or 21 or 12 or 13 or 14) pos += 2;
                continue;
            }
            width += CharWidth(chr);
        }
        return width;
    }

    /// <summary>
    /// Calls <paramref name="plot"/> for each lit pixel of the glyph (offset from the pen position, the x and y offsets
    /// applied); the pixel value is the glyph's own (1 to 2^bpp - 1), 0 pixels are not reported.
    /// </summary>
    public void DrawGlyph(int glyph, Action<int, int, int> plot)
    {
        if (glyph < 0 || glyph >= _offsets.Length || _offsets[glyph] == 0) return;
        var at = _offsets[glyph];
        int width = _file[at], height = _file[at + 1], left = (sbyte)_file[at + 2], top = (sbyte)_file[at + 3];
        var src = at + 4;
        if (src >= _file.Length) return;
        int bits = _file[src++], numbits = 8;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = (bits >> (8 - BitsPerPixel)) & 0xFF;
                if (value != 0) plot(left + x, top + y, value);
                bits = (bits << BitsPerPixel) & 0xFF;
                numbits -= BitsPerPixel;
                if (numbits == 0)
                {
                    bits = src < _file.Length ? _file[src++] : 0;
                    numbits = 8;
                }
            }
    }
}

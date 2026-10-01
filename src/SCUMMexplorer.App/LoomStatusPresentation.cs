using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.App;

internal sealed record LoomNoteChoice(int Id, string Label);
internal static class LoomNoteNames
{
    public static readonly LoomNoteChoice[] Choices = [new(1, "C"), new(2, "D"), new(3, "E"), new(4, "F"), new(5, "G"), new(6, "A"), new(7, "B"), new(8, "C′")];
    public static string Label(int id) => id is >= 1 and <= 8 ? Choices[id - 1].Label : "Unresolved";
}

internal static class LoomDraftNames
{
    // Display labels only. Associations and terminology are recorded in the UI validation.
    public static string Title(LoomDraftStatus draft) => (draft.Slot, draft.ForwardVerb, draft.ReverseVerb) switch
    {
        (0, 0x3A, 0x3B) => "Opening / Closing",
        (1, 0x3C, 0x3D) => "Straw into Gold",
        (2, 0x3E, 0x3F) => "Dyeing / Bleaching",
        (3, 0x40, 0) => "Night Vision",
        (4, 0x41, 0x42) => "Twisting",
        (5, 0x43, 0x44) => "Invisibility",
        (6, 0x45, 0x46) => "Emptying",
        (7, 0x47, 0x48) => "Sleep",
        (8, 0x49, 0) => "Terror",
        (9, 0x4A, 0x4B) => "Sharpening",
        (10, 0x4C, 0) => "Healing",
        (11, 0x4D, 0) => "Reflection",
        (12, 0x4E, 0x4F) => "Silence",
        (13, 0x50, 0x51) => "Shaping",
        (14, 0x52, 0x53) => "Unmaking",
        (15, 0x54, 0) => "Transcend",
        _ => $"Draft {draft.Slot + 1}"
    };
}

internal sealed record LoomStatusArtwork(BitmapSource Staff, BitmapSource EmptyStaff, BitmapSource[] Notes, BitmapSource[] EmptyNotes)
{
    public static LoomStatusArtwork? TryLoad(GameLibrary game, out string? error)
    {
        error = null;
        if (game.Id != GameId.Loom) return null;
        try
        {
            var script = new LoomScriptReader(ScummScriptDialects.For(game.Id)).Decode(game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == 18));
            var bytes = File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 1).FilePath);
            var objects = new LoomRoomObjects().Decode(bytes);
            var palette = new OldV3EgaRoomDecoder().Decode(bytes).Palette;
            var staffPixels = new byte[224 * 24];
            int note = 0, color = 8;
            var notes = new List<BitmapSource>();
            var emptyNotes = new List<BitmapSource>();
            var scorePixels = new byte[224 * 32];
            var notePixels = new byte[224 * 32];
            int scoreX = 0, letterColor = 7;
            var scoreMessages = new List<LoomMessage>();
            var charset = LoomCharset.TryLoad(game.FolderPath, 0, out _);
            if (charset is null) { error = "Native charset unavailable."; return null; }
            foreach (var instruction in script.Instructions)
            {
                if (instruction.Operation == "verbOps" && note < 8)
                {
                    var m = Regex.Match(instruction.Operands, @"^0x([0-9A-F]+), \[at, 0x([0-9A-F]+), local\[0x004\]; image, 0x([0-9A-F]+); on\]$");
                    if (!m.Success || Hex(m.Groups[1].Value) != note + 1) continue;
                    var picture = objects.Single(p => p.Id == Hex(m.Groups[3].Value));
                    int left = Hex(m.Groups[2].Value) / 8 * 8 - 16;
                    for (int y = 0; y < Math.Min(picture.Height, 24); y++)
                        for (int x = 0; x < picture.Width && left + x < 224; x++) staffPixels[y * 224 + left + x] = picture.Pixels[y * picture.Width + x];
                    note++;
                }
                if (instruction.Operation == "move")
                {
                    var m = Regex.Match(instruction.Operands, @"^local\[0x002\] = 0x([0-9A-F]+)$");
                    // The alternate source assignment8 dims unavailable notes; retain the full-color assignment.
                    if (m.Success && Hex(m.Groups[1].Value) != 8) color = Hex(m.Groups[1].Value);
                    m = Regex.Match(instruction.Operands, @"^local\[0x001\] = 0x([0-9A-F]+)$");
                    if (m.Success && Hex(m.Groups[1].Value) != 8) letterColor = Hex(m.Groups[1].Value);
                    m = Regex.Match(instruction.Operands, @"^local\[0x003\] = 0x([0-9A-F]+)$");
                    if (m.Success) scoreX = Hex(m.Groups[1].Value);
                }
                var scoreImage = Regex.Match(instruction.Operands, @"^0x(?:31|32), \[at, 0x([0-9A-F]+), 0x00A8; image, 0x([0-9A-F]+); on\]$");
                if (instruction.Operation == "verbOps" && scoreImage.Success)
                {
                    var picture = objects.Single(p => p.Id == Hex(scoreImage.Groups[2].Value));
                    int left = Hex(scoreImage.Groups[1].Value) / 8 * 8 - 16;
                    for (int y = 0; y < Math.Min(picture.Height, 32); y++)
                        for (int x = 0; x < picture.Width && left + x < 224; x++)
                            scorePixels[y * 224 + left + x] = picture.Pixels[y * picture.Width + x];
                }
                if (instruction.Operation != "print") continue;
                var print = Regex.Match(instruction.Operands, "position, (local\\[0x003\\]|0x[0-9A-F]+), 0x([0-9A-F]+); color, local\\[0x00([12])\\]; text, \"(.*?)\"");
                if (!print.Success) continue;
                var glyphText = print.Groups[4].Value;
                byte glyph = glyphText.StartsWith("\\x", StringComparison.Ordinal) ? (byte)Hex(glyphText[2..]) : (byte)glyphText[0];
                bool musical = print.Groups[3].Value == "2";
                var message = new LoomMessage(LoomMessageKind.Drawn, 254, 1, [new[] { glyph }],
                    print.Groups[1].Value.StartsWith("0x", StringComparison.Ordinal) ? Hex(print.Groups[1].Value[2..]) : scoreX,
                    Hex(print.Groups[2].Value), false, 0, 319, musical ? color : letterColor, 1, "script018", instruction.Offset, 0, null);
                scoreMessages.Add(message);
                if (musical)
                {
                    var layer = LoomMessageRenderer.Render([message], charset);
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 224; x++)
                    {
                        byte p = layer.Pixels[(168 + y) * 320 + 16 + x];
                        if (p != LoomMessageRenderer.Transparent) notePixels[y * 224 + x] = p;
                    }
                }
            }
            var scoreLayer = LoomMessageRenderer.Render(scoreMessages, charset);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 224; x++)
            {
                byte p = scoreLayer.Pixels[(168 + y) * 320 + 16 + x];
                if (p != LoomMessageRenderer.Transparent) scorePixels[y * 224 + x] = p;
            }
            // Native score starts at x24, eight contiguous24-pixel choices; no relocated glyphs.
            // Crop only the shared empty rows below every glyph; preserve native note positions and 2x scale.
            int scoreHeight = Enumerable.Range(0, 32).Last(y => scorePixels.AsSpan(y * 224, 224).ContainsAnyExcept((byte)0)) + 1;
            for (int i = 0; i < 8; i++)
            {
                var pixels = new byte[24 * scoreHeight]; var empty = new byte[24 * scoreHeight];
                for (int y = 0; y < scoreHeight; y++) for (int x = 0; x < 24; x++)
                {
                    int at = y * 224 + 8 + i * 24 + x;
                    pixels[y * 24 + x] = scorePixels[at];
                    empty[y * 24 + x] = notePixels[at] == 0 ? scorePixels[at] : (byte)7;
                    if (notePixels[at] != 0 && x > 0 && x < 23 && y > 0 && y < 31 &&
                        notePixels[at-1] != 0 && notePixels[at+1] != 0 && notePixels[at-224] != 0 && notePixels[at+224] != 0)
                        empty[y * 24 + x] = 0;
                }
                notes.Add(Bitmap(pixels, 24, scoreHeight, palette)); emptyNotes.Add(Bitmap(empty, 24, scoreHeight, palette));
            }
            if (note != 8 || notes.Count != 8) { error = $"Native artwork contract unresolved: {note} staff segments, {notes.Count} note glyphs."; return null; }
            return new(Bitmap(staffPixels, 224, 24, palette), Bitmap(staffPixels, 224, 24, palette, outline: true), notes.ToArray(), emptyNotes.ToArray());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { error = ex.Message; return null; }
    }

    private static int Hex(string value) => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static BitmapSource Bitmap(byte[] pixels, int width, int height, IReadOnlyList<uint> palette, bool outline = false)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x; if (pixels[index] == 0) continue;
            if (outline && x > 0 && x < width - 1 && y > 0 && y < height - 1 &&
                pixels[index - 1] != 0 && pixels[index + 1] != 0 && pixels[index - width] != 0 && pixels[index + width] != 0) continue;
            uint c = palette[outline ? 7 : pixels[index]];
            rgba[index * 4] = (byte)c; rgba[index * 4 + 1] = (byte)(c >> 8); rgba[index * 4 + 2] = (byte)(c >> 16); rgba[index * 4 + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, rgba, width * 4); bitmap.Freeze(); return bitmap;
    }
}

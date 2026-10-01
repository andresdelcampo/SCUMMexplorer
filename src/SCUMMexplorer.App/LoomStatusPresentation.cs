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
        (2, 0x3E, 0x3F) => "Dyeing / Bleaching",
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
            var glyphs = new List<(byte Glyph, int Y, int Color)>();
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
                }
                if (instruction.Operation != "print") continue;
                var print = Regex.Match(instruction.Operands, "position, local\\[0x003\\], 0x([0-9A-F]+); color, local\\[0x002\\]; text, \"([_`<=])\"");
                if (!print.Success) continue;
                glyphs.Add(((byte)print.Groups[2].Value[0], Hex(print.Groups[1].Value), color));
                if (glyphs.Count != 2) continue;
                int top = glyphs.Min(g => g.Y);
                var messages = glyphs.Select(g => new LoomMessage(LoomMessageKind.Drawn, 254, 1, [new[] { g.Glyph }],
                    4, 146 + g.Y - top, false, 0, 319, g.Color, 1, "script018", instruction.Offset, 0, null));
                var layer = LoomMessageRenderer.Render(messages, charset);
                var pixels = new byte[16 * 24];
                for (int y = 0; y < 24; y++) for (int x = 0; x < 16; x++)
                { byte p = layer.Pixels[(144 + y) * 320 + x]; pixels[y * 16 + x] = p == LoomMessageRenderer.Transparent ? (byte)0 : p; }
                notes.Add(Bitmap(pixels, 16, 24, palette)); emptyNotes.Add(Bitmap(pixels, 16, 24, palette, outline: true)); glyphs.Clear();
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

using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// One image verb as it would stand in the verb area: its slot, verb, mode and
/// screen rectangle. ScummVM <c>drawVerbBitmap</c> sets the verb's
/// <c>curRect</c> to its image's size (whole strips of 8 pixels) at the verb's
/// position, and <c>findVerbAtPos</c> tests clicks against that rectangle.
/// </summary>
public sealed record LoomVerbAreaItem(int Slot, int VerbId, int Mode, int X, int Y, int Width, int Height,
    int ImageObject, int ImageRoom, string? Estimate)
{
    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// The verb area drawn from the session's verb slots: <see cref="Pixels"/> are
/// the screen rows from <see cref="LoomVerbArea.Top"/> down, 320 pixels wide,
/// colour indices into the room palette (0 where nothing is drawn).
/// <see cref="Items"/> are the drawn verbs in slot order; <see cref="Notes"/>
/// list verbs that are on but could not be drawn.
/// </summary>
public sealed record LoomVerbAreaPicture(byte[] Pixels, IReadOnlyList<LoomVerbAreaItem> Items, IReadOnlyList<string> Notes)
{
    public int Width => LoomVerbArea.Width;
    public int Height => LoomVerbArea.Height;
}

/// <summary>
/// LOOM EGA's verb area (the boot's <c>initScreens(0, 144)</c>: the room
/// occupies screen rows 0–143, the verb screen rows 144–199). Image verbs are
/// drawn in the order the game drew them; text verbs and highlight colours are
/// not drawn, and the note letters scripts print there are drawn by the message
/// layer.
/// </summary>
public static class LoomVerbArea
{
    public const int Top = 144;
    public const int Width = 320;
    public const int Height = 56;

    /// <summary>
    /// Draws every image verb that is on or dim (ScummVM <c>drawVerb</c> draws a
    /// slot whose mode is not 0) in the order the game last drew each one
    /// (<see cref="LoomVerbSnapshot.DrawOrder"/>, then slot order), with the
    /// rectangles cleared since (<paramref name="erases"/>) in between. Several
    /// verbs can be on at one place: script 031's note animation turns on nine
    /// verbs over a distaff note and the last one drawn is what the game shows.
    /// <paramref name="image"/> returns an object's image from a room (the room
    /// the verb's image was taken from). <see cref="LoomVerbAreaPicture.Items"/>
    /// stay in slot order.
    /// </summary>
    public static LoomVerbAreaPicture Compose(IEnumerable<LoomVerbSnapshot> verbs, Func<int, int, RoomObjectImage?> image,
        IEnumerable<LoomVerbErase>? erases = null)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        ArgumentNullException.ThrowIfNull(image);
        var pixels = new byte[Width * Height];
        var items = new List<LoomVerbAreaItem>();
        var notes = new List<string>();
        var paints = new List<(int Order, int Slot, Action Paint)>();
        foreach (var erase in erases ?? [])
            paints.Add((erase.Order, 0, () => Clear(pixels, erase.Box)));
        foreach (var verb in verbs.OrderBy(item => item.Slot))
        {
            if (verb.Mode is 0) continue;
            if (verb.Mode is null)
            {
                notes.Add($"verb 0x{verb.VerbId:X2}: whether it is on is unknown ({verb.Unknown ?? "not known"})");
                continue;
            }
            if (verb.ImageObject is not int objectId || verb.ImageRoom is not int room)
            {
                notes.Add(verb.Name is null
                    ? $"verb 0x{verb.VerbId:X2} is on but has no known image"
                    : $"verb 0x{verb.VerbId:X2} is a text verb (\"{verb.Name}\"), not drawn");
                continue;
            }
            if (verb.X is not int x || verb.Y is not int y)
            {
                notes.Add($"verb 0x{verb.VerbId:X2}: its position is unknown");
                continue;
            }
            var picture = image(room, objectId);
            if (picture is null)
            {
                notes.Add($"verb 0x{verb.VerbId:X2}: object {objectId}'s image in Room {room:00} could not be read");
                continue;
            }
            // drawVerbBitmap: imgw = width / 8 strips from xstrip = x / 8, imgh = height / 8
            // strips down; curRect keeps the verb's own x and y with that size.
            var drawnWidth = picture.Width / 8 * 8;
            var drawnHeight = picture.Height / 8 * 8;
            var left = x / 8 * 8;
            var top = y - Top;
            paints.Add((verb.DrawOrder, verb.Slot, () =>
            {
                for (var row = 0; row < drawnHeight; row++)
                {
                    var targetY = top + row;
                    if (targetY is < 0 or >= Height) continue;
                    for (var column = 0; column < drawnWidth; column++)
                    {
                        var targetX = left + column;
                        if (targetX is < 0 or >= Width) continue;
                        pixels[targetY * Width + targetX] = picture.Pixels[row * picture.Width + column];
                    }
                }
            }));
            items.Add(new LoomVerbAreaItem(verb.Slot, verb.VerbId, verb.Mode.Value, x, y,
                drawnWidth, drawnHeight, objectId, room, verb.Estimate));
        }
        foreach (var paint in paints.OrderBy(item => item.Order).ThenBy(item => item.Slot)) paint.Paint();
        return new LoomVerbAreaPicture(pixels, items, notes);
    }

    // restoreVerbBG: the rectangle (screen coordinates, corners inclusive) back to colour 0.
    private static void Clear(byte[] pixels, LoomScreenBox box)
    {
        for (var y = Math.Max(box.Top, Top); y <= Math.Min(box.Bottom, Top + Height - 1); y++)
            for (var x = Math.Max(box.Left, 0); x <= Math.Min(box.Right, Width - 1); x++)
                pixels[(y - Top) * Width + x] = 0;
    }

    /// <summary>
    /// ScummVM <c>findVerbAtPos</c>: from the last slot down, the first verb that
    /// is on (mode 1, not dim or off) whose rectangle holds the screen point.
    /// </summary>
    public static LoomVerbAreaItem? FindVerbAt(IEnumerable<LoomVerbAreaItem> items, int x, int y) =>
        items.Where(item => item.Mode == 1 && item.Contains(x, y)).OrderByDescending(item => item.Slot).FirstOrDefault();
}

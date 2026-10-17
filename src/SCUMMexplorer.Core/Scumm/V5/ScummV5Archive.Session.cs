using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What the live session reads from a v5 game besides what browsing reads (S4b MonkeyCd, 17 October 2026): the index's object table, the room's size,
/// its object images in state order with their depth masks, its walk boxes with the box matrix and its scale slots.
/// </summary>
public sealed partial class ScummV5Archive
{
    /// <summary>
    /// The object table of the index (<c>DOBJ</c>; ScummVM <c>readIndexBlock</c>, version 5): a count word, then one byte per object (owner in the low nibble,
    /// state in the high one), then one little-endian class dword per object.
    /// </summary>
    public ScummIndexedObjectSnapshot ReadIndexedObjects()
    {
        var states = new Dictionary<int, byte>();
        var owners = new Dictionary<int, byte>();
        var classes = new Dictionary<int, uint>();
        var block = Blocks(_index, 0, _index.Length).FirstOrDefault(b => b.Tag == "DOBJ");
        if (block is null) return new ScummIndexedObjectSnapshot(states, owners, classes);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_index.AsSpan(block.Start + 8));
        var bytes = block.Start + 10;
        var classStart = bytes + count;
        for (var id = 0; id < count && classStart + (id + 1) * 4 <= block.Start + block.Size; id++)
        {
            states[id] = (byte)(_index[bytes + id] >> 4);
            owners[id] = (byte)(_index[bytes + id] & 0x0F);
            classes[id] = BinaryPrimitives.ReadUInt32LittleEndian(_index.AsSpan(classStart + id * 4));
        }
        return new ScummIndexedObjectSnapshot(states, owners, classes);
    }

    /// <summary>The room's size from its <c>RMHD</c> block (width and height words).</summary>
    public (int Width, int Height) ReadRoomSize(int number)
    {
        var room = Room(number);
        var header = Blocks(_resources, room.Start + 8, room.Start + room.Size).FirstOrDefault(b => b.Tag == "RMHD")
            ?? throw new InvalidDataException($"Room {number} has no RMHD block.");
        return (BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(header.Start + 8)), BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(header.Start + 10)));
    }

    /// <summary>
    /// The room's walk boxes for the session: <c>BOXD</c> (a count word, then 20-byte boxes) followed by the body of <c>BOXM</c> (the box matrix, in the layout
    /// ScummVM's <c>getNextBox</c> reads up to version 5), returned as the body of a v4 <c>BX</c> block (count byte, boxes, matrix). Null when the room has no boxes.
    /// </summary>
    public byte[]? ReadWalkBoxBlock(int number)
    {
        var room = Room(number);
        var children = Blocks(_resources, room.Start + 8, room.Start + room.Size);
        if (children.FirstOrDefault(b => b.Tag == "BOXD") is not { } boxd || boxd.Size < 10) return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(boxd.Start + 8));
        var size = ScummWalkBoxReader.BoxSizeV5;
        if (count > 255 || 10 + count * size > boxd.Size) throw new InvalidDataException($"Room {number}'s BOXD block cannot hold {count} boxes.");
        var matrix = children.FirstOrDefault(b => b.Tag == "BOXM") is { } boxm ? _resources.AsSpan(boxm.Start + 8, boxm.Size - 8).ToArray() : [];
        var body = new byte[1 + count * size + matrix.Length];
        body[0] = (byte)count;
        _resources.AsSpan(boxd.Start + 10, count * size).CopyTo(body.AsSpan(1));
        matrix.CopyTo(body.AsSpan(1 + count * size));
        return body;
    }

    /// <summary>The room's <c>SCAL</c> block body (scale slots of four words: scale, y, scale, y), or null when it has none.</summary>
    public byte[]? ReadScaleSlotBlock(int number)
    {
        var room = Room(number);
        return Blocks(_resources, room.Start + 8, room.Start + room.Size).FirstOrDefault(b => b.Tag == "SCAL") is { } scal
            ? _resources.AsSpan(scal.Start + 8, scal.Size - 8).ToArray()
            : null;
    }

    /// <summary>
    /// The images of the room's objects (<c>OBIM</c> &gt; <c>IMHD</c>, then <c>IM01</c>, <c>IM02</c>… one per state, in that order), each an <c>SMAP</c> sized by the
    /// object's <c>CDHD</c> and, when it has one, its first depth mask (<c>ZP01</c>, coded as the room's). An image that does not decode is left out.
    /// </summary>
    public IReadOnlyList<ScummV4ObjectImage> ReadObjectImages(int number)
    {
        var room = Room(number);
        var children = Blocks(_resources, room.Start + 8, room.Start + room.Size);
        var sizes = new Dictionary<int, (int X, int Y, int W, int H)>();
        foreach (var obcd in children.Where(b => b.Tag == "OBCD"))
            if (Blocks(_resources, obcd.Start + 8, obcd.Start + obcd.Size).FirstOrDefault(b => b.Tag == "CDHD") is { Size: >= 8 + 13 } cdhd)
            {
                var h = cdhd.Start + 8;
                sizes[BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(h))] = (_resources[h + 2] * 8, _resources[h + 3] * 8, _resources[h + 4] * 8, _resources[h + 5] * 8);
            }
        var images = new List<ScummV4ObjectImage>();
        foreach (var obim in children.Where(b => b.Tag == "OBIM"))
        {
            var parts = Blocks(_resources, obim.Start + 8, obim.Start + obim.Size);
            if (parts.FirstOrDefault(b => b.Tag == "IMHD") is not { } imhd || imhd.Size < 10) continue;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(imhd.Start + 8));
            if (!sizes.TryGetValue(id, out var box) || box.W < 8 || box.H < 1) continue;
            foreach (var state in parts.Where(b => b.Tag.Length == 4 && b.Tag.StartsWith("IM", StringComparison.Ordinal) && b.Tag != "IMHD"))
            {
                var inner = Blocks(_resources, state.Start + 8, state.Start + state.Size);
                if (inner.FirstOrDefault(b => b.Tag == "SMAP") is not { } smap) continue;
                var pixels = new byte[box.W * box.H];
                byte[]? mask = null;
                try
                {
                    for (var strip = 0; strip < box.W / 8; strip++)
                    {
                        var start = smap.Start + (int)BinaryPrimitives.ReadUInt32LittleEndian(_resources.AsSpan(smap.Start + 8 + strip * 4));
                        if (start >= smap.Start + smap.Size) throw new InvalidDataException("strip outside the image");
                        ScummV4Archive.DecodeStrip(_resources, start, smap.Start + smap.Size, pixels, box.W, box.H, strip * 8);
                    }
                    if (inner.FirstOrDefault(b => b.Tag == "ZP01") is { } zp)
                        mask = ScummV4Archive.DecodeMaskStrips(_resources, zp.Start, 8, zp.Start + zp.Size, box.W, box.H);
                }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException) { continue; }
                var stateNumber = int.TryParse(state.Tag.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var n) ? n : 1;
                images.Add(new ScummV4ObjectImage(id, box.X, box.Y, box.W, box.H, pixels) { Mask = mask, State = stateNumber });
            }
        }
        return images;
    }
}

using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Formats;

/// <summary>A walk-box corner in room pixels (ScummVM <c>Common::Point</c>, 16-bit).</summary>
public readonly record struct LoomBoxPoint(int X, int Y);

/// <summary>
/// One old-v3 walk box (ScummVM <c>Box::old</c>, <c>SIZEOF_BOX_V3</c> = 18 bytes):
/// the corners ul, ur, lr, ll as signed little-endian words, then the depth
/// mask and the flags bytes.
/// </summary>
public sealed record LoomWalkBox(int Index, LoomBoxPoint UpperLeft, LoomBoxPoint UpperRight,
    LoomBoxPoint LowerRight, LoomBoxPoint LowerLeft, byte Mask, byte Flags, int Scale = 0)
{
    /// <summary>ScummVM <c>kBoxInvisible</c>: <c>adjustXYToBeInBox</c> skips the box.</summary>
    public const byte InvisibleFlag = 0x80;
    /// <summary>ScummVM <c>kBoxPlayerOnly</c>: an invisible box stays usable by actors that are not the player.</summary>
    public const byte PlayerOnlyFlag = 0x20;

    public IReadOnlyList<LoomBoxPoint> Corners => [UpperLeft, UpperRight, LowerRight, LowerLeft];

    /// <summary>True when the quadrangle collapses to a line segment (LOOM's paths are mostly these).</summary>
    public bool IsLine => (UpperLeft == UpperRight && LowerRight == LowerLeft) ||
                          (UpperLeft == LowerLeft && UpperRight == LowerRight);
}

/// <summary>One box-matrix triplet: from <see cref="FromBox"/>, any box in LowBox..HighBox is reached through <see cref="NextBox"/>.</summary>
public sealed record LoomBoxMatrixEntry(int FromBox, int LowBox, int HighBox, int NextBox);

/// <summary>
/// The walk boxes of one old-v3 room and ScummVM's box geometry for them
/// (<c>boxes.cpp</c> and <c>Actor::adjustXYToBeInBox</c>, version 3 paths).
/// Flags can change at run time (<c>setBoxFlags</c>); a room entry reads them afresh.
/// </summary>
public sealed class LoomWalkBoxSet
{
    /// <summary>ScummVM <c>kOldInvalidBox</c>, <c>Actor::kInvalidBox</c> for small-header games.</summary>
    public const int InvalidBox = 255;

    private readonly byte[] _flags;
    private readonly byte[] _matrix;

    internal LoomWalkBoxSet(IReadOnlyList<LoomWalkBox> boxes, byte[] matrix, int tableOffset, bool matrixTruncated)
    {
        Boxes = boxes;
        _matrix = matrix;
        _flags = boxes.Select(box => box.Flags).ToArray();
        TableOffset = tableOffset;
        MatrixTruncated = matrixTruncated;
        Matrix = ReadMatrixRows(matrix, boxes.Count);
    }

    public IReadOnlyList<LoomWalkBox> Boxes { get; }
    /// <summary>Matrix triplets per box, as stored (before ScummVM's leading-0xFF skip).</summary>
    public IReadOnlyList<LoomBoxMatrixEntry> Matrix { get; }
    /// <summary>Offset of the box count byte in the decoded room (the word at room + 0x15).</summary>
    public int TableOffset { get; }
    /// <summary>True when the matrix rows run past the room image offset (ScummVM notes Indy3 Room 46).</summary>
    public bool MatrixTruncated { get; }
    public int Count => Boxes.Count;

    /// <summary>The CD room's scale slots (SA block; ScummVM <c>_scaleSlots</c>): scale and y at two points, interpolated between them.</summary>
    public IReadOnlyList<(int Scale1, int Y1, int Scale2, int Y2)> ScaleSlots { get; internal set; } = [];

    public LoomWalkBoxSet Clone() => new(Boxes, _matrix, TableOffset, MatrixTruncated) { ScaleSlots = ScaleSlots };

    /// <summary>
    /// The scale the box gives an actor at <paramref name="y"/> (ScummVM <c>setupActorScale</c>: the box's scale word, or with bit 15 set the
    /// slot it names, <c>getScaleFromSlot</c>); null when the box leaves the actor's own scale (255, or no such slot).
    /// </summary>
    public int? BoxScale(int box, int y)
    {
        if (Resolve(box) is not int index) return null;
        var word = Boxes[index].Scale;
        if ((word & 0x8000) == 0) return word is > 0 and < 255 ? word : null;
        var slot = word & 0x7FFF;
        if (slot >= ScaleSlots.Count) return null;
        var (scale1, y1, scale2, y2) = ScaleSlots[slot];
        if (y1 == y2) return null;
        return Math.Clamp((scale2 - scale1) * (y - y1) / (y2 - y1) + scale1, 1, 255);
    }

    /// <summary>
    /// ScummVM <c>getBoxBaseAddr</c> for version 3: 255 is no box, and a box one
    /// past the last one is read as the last one (a workaround ScummVM documents
    /// for the LOOM demo and Indy3 EGA). Null for anything else out of range.
    /// </summary>
    private int? Resolve(int box)
    {
        if (box == InvalidBox || box < 0) return null;
        if (box == Count) box--;
        return box >= 0 && box < Count ? box : null;
    }

    public byte GetFlags(int box) => Resolve(box) is int index ? _flags[index] : (byte)0;

    /// <summary>
    /// ScummVM <c>getMaskFromBox</c> for version 3: the box's mask byte, the z-plane an actor standing in it is
    /// masked by. No box (255) gives 1 (ScummVM's workaround for bugs #791 and #897); an unknown box gives 0.
    /// </summary>
    public byte GetMask(int box) => box == InvalidBox ? (byte)1 : Resolve(box) is int index ? Boxes[index].Mask : (byte)0;

    /// <summary>ScummVM <c>setBoxFlags</c> (<c>o3_setBoxFlags</c>) for the current room; false when the box does not exist.</summary>
    public bool SetFlags(int box, byte value)
    {
        if (Resolve(box) is not int index) return false;
        _flags[index] = value;
        return true;
    }

    /// <summary>
    /// ScummVM <c>checkXYInBoxBounds</c> as used by version 3 games: the bounding
    /// test, then the four slope tests. The line-segment tolerance is left out,
    /// as ScummVM does for games before SCUMM 5 (LOOM DOS EGA was checked there).
    /// </summary>
    public bool Contains(int box, int x, int y)
    {
        if (box < 0 || box == InvalidBox || Resolve(box) is not int index) return false;
        var b = Boxes[index];
        var (ul, ur, lr, ll) = (b.UpperLeft, b.UpperRight, b.LowerRight, b.LowerLeft);
        if (x < ul.X && x < ur.X && x < lr.X && x < ll.X) return false;
        if (x > ul.X && x > ur.X && x > lr.X && x > ll.X) return false;
        if (y < ul.Y && y < ur.Y && y < lr.Y && y < ll.Y) return false;
        if (y > ul.Y && y > ur.Y && y > lr.Y && y > ll.Y) return false;
        var p = new LoomBoxPoint(x, y);
        return CompareSlope(ul, ur, p) && CompareSlope(ur, lr, p) && CompareSlope(lr, ll, p) && CompareSlope(ll, ul, p);
    }

    /// <summary>
    /// ScummVM <c>Actor::adjustXYToBeInBox</c> (small-header games, version 3):
    /// searches the boxes from the last to the first with thresholds 30, 80 and
    /// then any distance, returning the point itself when a box contains it,
    /// otherwise the closest point on the closest box. Like the v3/v4
    /// interpreters, the result starts from the actor's last valid point, which
    /// is what comes back when no box is closer than the 0xFFFF search bound.
    /// </summary>
    public BoxAdjustment Adjust(int x, int y, bool isPlayer, int lastValidX, int lastValidY)
    {
        int[] thresholds = [30, 80, 0];
        var resultX = lastValidX;
        var resultY = lastValidY;
        var validX = lastValidX;
        var validY = lastValidY;
        var numBoxes = Count - 1;
        if (numBoxes < 0) return new BoxAdjustment(resultX, resultY, InvalidBox, validX, validY);
        foreach (var threshold in thresholds)
        {
            var bestDist = 0xFFFF;
            var bestBox = InvalidBox;
            for (var box = numBoxes; box >= 0; box--)
            {
                var flags = GetFlags(box);
                if ((flags & LoomWalkBox.InvisibleFlag) != 0 &&
                    !((flags & LoomWalkBox.PlayerOnlyFlag) != 0 && !isPlayer)) continue;
                var corners = Boxes[box];
                if (threshold > 0 && QuickReject(corners, x, y, threshold)) continue;
                if (Contains(box, x, y))
                    return new BoxAdjustment(x, y, box, x, y);
                var (distance, closestX, closestY) = ClosestPointOnBox(corners, x, y);
                if (distance < bestDist)
                {
                    validX = resultX = closestX;
                    validY = resultY = closestY;
                    if (distance == 0) return new BoxAdjustment(resultX, resultY, box, validX, validY);
                    bestDist = distance;
                    bestBox = box;
                }
            }
            if (threshold == 0 || threshold * threshold >= bestDist)
                return new BoxAdjustment(resultX, resultY, bestBox, validX, validY);
        }
        return new BoxAdjustment(resultX, resultY, InvalidBox, validX, validY);
    }

    /// <summary>
    /// ScummVM <c>getNextBox</c> for version 3: the box after <paramref name="from"/>
    /// on the way to <paramref name="to"/>, or -1 when there is no way.
    /// </summary>
    public int GetNextBox(int from, int to)
    {
        if (from == to) return to;
        if (to == InvalidBox) return -1;
        if (from == InvalidBox) return to;
        var position = _matrix.Length > 0 && _matrix[0] == 0xFF ? 1 : 0;
        for (var index = 0; index < from && position < _matrix.Length; index++)
        {
            while (position < _matrix.Length && _matrix[position] != 0xFF) position += 3;
            position++;
        }
        var destination = -1;
        while (position < _matrix.Length && _matrix[position] != 0xFF)
        {
            if (position + 2 < _matrix.Length && _matrix[position] <= to && to <= _matrix[position + 1])
                destination = unchecked((sbyte)_matrix[position + 2]);
            position += 3;
        }
        return destination;
    }

    public static (int Distance, int X, int Y) ClosestPointOnBox(LoomWalkBox box, int x, int y)
    {
        var p = new LoomBoxPoint(x, y);
        var best = 0xFFFFFF;
        var (bestX, bestY) = (x, y);
        foreach (var (start, end) in new[]
                 {
                     (box.UpperLeft, box.UpperRight), (box.UpperRight, box.LowerRight),
                     (box.LowerRight, box.LowerLeft), (box.LowerLeft, box.UpperLeft)
                 })
        {
            var point = ClosestPointOnLine(start, end, p);
            var distance = SquareDistance(p, point);
            if (distance < best)
            {
                best = distance;
                (bestX, bestY) = (point.X, point.Y);
            }
        }
        return (best, bestX, bestY);
    }

    /// <summary>ScummVM <c>closestPtOnLine</c>, with its integer arithmetic.</summary>
    public static LoomBoxPoint ClosestPointOnLine(LoomBoxPoint start, LoomBoxPoint end, LoomBoxPoint p)
    {
        var lxdiff = end.X - start.X;
        var lydiff = end.Y - start.Y;
        int resultX, resultY;
        if (end.X == start.X)
        {
            resultX = start.X;
            resultY = p.Y;
        }
        else if (end.Y == start.Y)
        {
            resultX = p.X;
            resultY = start.Y;
        }
        else
        {
            var dist = lxdiff * lxdiff + lydiff * lydiff;
            if (Math.Abs(lxdiff) > Math.Abs(lydiff))
            {
                var a = start.X * lydiff / lxdiff;
                var b = p.X * lxdiff / lydiff;
                var c = (a + b - start.Y + p.Y) * lydiff * lxdiff / dist;
                resultX = c;
                resultY = c * lydiff / lxdiff - a + start.Y;
            }
            else
            {
                var a = start.Y * lxdiff / lydiff;
                var b = p.Y * lydiff / lxdiff;
                var c = (a + b - start.X + p.X) * lydiff * lxdiff / dist;
                resultX = c * lxdiff / lydiff - a + start.X;
                resultY = c;
            }
        }
        var result = new LoomBoxPoint((short)resultX, (short)resultY);
        if (Math.Abs(lydiff) < Math.Abs(lxdiff))
        {
            if (lxdiff > 0)
            {
                if (result.X < start.X) result = start;
                else if (result.X > end.X) result = end;
            }
            else
            {
                if (result.X > start.X) result = start;
                else if (result.X < end.X) result = end;
            }
        }
        else if (lydiff > 0)
        {
            if (result.Y < start.Y) result = start;
            else if (result.Y > end.Y) result = end;
        }
        else
        {
            if (result.Y > start.Y) result = start;
            else if (result.Y < end.Y) result = end;
        }
        return result;
    }

    /// <summary>ScummVM <c>Common::Point::sqrDist</c>: 0xFFFFFF beyond 0x1000 on either axis.</summary>
    private static int SquareDistance(LoomBoxPoint a, LoomBoxPoint b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return dx >= 0x1000 || dy >= 0x1000 ? 0xFFFFFF : dx * dx + dy * dy;
    }

    /// <summary>ScummVM <c>getBoxCoordinates</c> for version 3 (with <c>getBoxBaseAddr</c>'s one-past-the-end rule), or null.</summary>
    public LoomWalkBox? Coordinates(int box) => Resolve(box) is int index ? Boxes[index] : null;

    /// <summary>
    /// ScummVM <c>getGates</c>: the "corridor" between two boxes as two lines, each from a corner of one box
    /// to the closest point on the other. Gate A's first point and gate B's first point lie on the first box.
    /// </summary>
    public static (LoomBoxPoint A0, LoomBoxPoint A1, LoomBoxPoint B0, LoomBoxPoint B1) GetGates(LoomWalkBox box1, LoomWalkBox box2)
    {
        var corners = new LoomBoxPoint[8];
        var closest = new LoomBoxPoint[8];
        var dist = new int[8];
        LoomBoxPoint[] first = [box1.UpperLeft, box1.UpperRight, box1.LowerRight, box1.LowerLeft];
        LoomBoxPoint[] second = [box2.UpperLeft, box2.UpperRight, box2.LowerRight, box2.LowerLeft];
        for (var i = 0; i < 4; i++)
        {
            corners[i] = first[i];
            var (d, x, y) = ClosestPointOnBox(box2, first[i].X, first[i].Y);
            (dist[i], closest[i]) = (d, new LoomBoxPoint(x, y));
        }
        for (var i = 4; i < 8; i++)
        {
            corners[i] = second[i - 4];
            var (d, x, y) = ClosestPointOnBox(box1, second[i - 4].X, second[i - 4].Y);
            (dist[i], closest[i]) = (d, new LoomBoxPoint(x, y));
        }
        var minDist = new int[3];
        var nearest = new int[3];
        var onSecond = new bool[3];
        for (var j = 0; j < 3; j++)
        {
            minDist[j] = 0xFFFF;
            for (var i = 0; i < 8; i++)
            {
                if (dist[i] < minDist[j])
                {
                    minDist[j] = dist[i];
                    nearest[j] = i;
                }
            }
            dist[nearest[j]] = 0xFFFF;
            minDist[j] = (int)Math.Sqrt(minDist[j]);
            onSecond[j] = nearest[j] > 3;
        }
        int line1, line2;
        if (onSecond[0] == onSecond[1] && Math.Abs(minDist[0] - minDist[1]) < 4) (line1, line2) = (nearest[0], nearest[1]);
        else if (onSecond[0] == onSecond[1] && minDist[0] == minDist[1]) (line1, line2) = (nearest[0], nearest[1]);
        else if (onSecond[0] == onSecond[2] && minDist[0] == minDist[2]) (line1, line2) = (nearest[0], nearest[2]);
        else if (onSecond[1] == onSecond[2] && minDist[1] == minDist[2]) (line1, line2) = (nearest[1], nearest[2]);
        else if (onSecond[0] == onSecond[2] && Math.Abs(minDist[0] - minDist[2]) < 4) (line1, line2) = (nearest[0], nearest[2]);
        else if (Math.Abs(minDist[0] - minDist[2]) < 4) (line1, line2) = (nearest[1], nearest[2]);
        else if (Math.Abs(minDist[0] - minDist[1]) < 4) (line1, line2) = (nearest[0], nearest[1]);
        else (line1, line2) = (nearest[0], nearest[0]);
        var (a0, a1) = line1 < 4 ? (corners[line1], closest[line1]) : (closest[line1], corners[line1]);
        var (b0, b1) = line2 < 4 ? (corners[line2], closest[line2]) : (closest[line2], corners[line2]);
        return (a0, a1, b0, b1);
    }

    /// <summary>ScummVM <c>compareSlope</c>.</summary>
    public static bool CompareSlope(LoomBoxPoint p1, LoomBoxPoint p2, LoomBoxPoint p3) =>
        (p2.Y - p1.Y) * (p3.X - p1.X) <= (p3.Y - p1.Y) * (p2.X - p1.X);

    private static bool QuickReject(LoomWalkBox box, int x, int y, int threshold)
    {
        var (ul, ur, lr, ll) = (box.UpperLeft, box.UpperRight, box.LowerRight, box.LowerLeft);
        var t = x - threshold;
        if (t > ul.X && t > ur.X && t > lr.X && t > ll.X) return true;
        t = x + threshold;
        if (t < ul.X && t < ur.X && t < lr.X && t < ll.X) return true;
        t = y - threshold;
        if (t > ul.Y && t > ur.Y && t > lr.Y && t > ll.Y) return true;
        t = y + threshold;
        return t < ul.Y && t < ur.Y && t < lr.Y && t < ll.Y;
    }

    private static IReadOnlyList<LoomBoxMatrixEntry> ReadMatrixRows(byte[] matrix, int count)
    {
        var entries = new List<LoomBoxMatrixEntry>();
        var position = 0;
        for (var from = 0; from < count && position < matrix.Length; from++)
        {
            while (position + 2 < matrix.Length && matrix[position] != 0xFF)
            {
                entries.Add(new LoomBoxMatrixEntry(from, matrix[position], matrix[position + 1], matrix[position + 2]));
                position += 3;
            }
            position++;
        }
        return entries;
    }
}

/// <summary>ScummVM <c>AdjustBoxResult</c>, plus the actor's last valid point after the search.</summary>
public readonly record struct BoxAdjustment(int X, int Y, int Box, int LastValidX, int LastValidY);

/// <summary>
/// Reads the walk boxes of an old-v3 room (ScummVM <c>ScummEngine_v3old::resetRoomSubBlocks</c>):
/// the word at room + 0x15 points at a count byte, the boxes follow at 18 bytes
/// each, and the box matrix runs from there to the room image (the word at + 0x0A).
/// </summary>
public static class LoomWalkBoxReader
{
    public const int BoxSize = 18;
    /// <summary>The CD game's (SCUMM v4/v5) box: the 18 bytes above plus a scale word.</summary>
    public const int BoxSizeV5 = 20;

    /// <summary>
    /// Reads the body of a v4/v5 BX block (ScummVM <c>BOXD</c>): a count byte, then 20-byte boxes (four signed corner pairs, mask, flags,
    /// scale word), then the box matrix to the end of the block, in the same triplet rows as the old-v3 game.
    /// </summary>
    public static LoomWalkBoxSet ReadBlockBody(byte[] body)
    {
        if (body.Length < 1) throw new InvalidDataException("The BX block is empty.");
        var count = body[0];
        var matrixStart = 1 + count * BoxSizeV5;
        if (matrixStart > body.Length) throw new InvalidDataException($"{count} walk boxes run past the end of the BX block.");
        var boxes = new List<LoomWalkBox>(count);
        for (var index = 0; index < count; index++)
        {
            var at = 1 + index * BoxSizeV5;
            LoomBoxPoint Point(int offset) => new(SignedWord(body, at + offset), SignedWord(body, at + offset + 2));
            boxes.Add(new LoomWalkBox(index, Point(0), Point(4), Point(8), Point(12), body[at + 16], body[at + 17], Word(body, at + 18)));
        }
        var matrix = body.AsSpan(matrixStart).ToArray();
        return new LoomWalkBoxSet(boxes, matrix, 0, !MatrixFits(matrix, count));
    }

    public static LoomWalkBoxSet Read(ReadOnlySpan<byte> encodedRoom) => ReadPlain(LflEncoding.DecodeXorFF(encodedRoom));

    public static LoomWalkBoxSet ReadPlain(byte[] room)
    {
        if (room.Length < 29) throw new InvalidDataException("The room is too short for a walk-box offset.");
        var roomLength = Word(room, 0);
        var imageOffset = Word(room, 0x0A);
        var tableOffset = Word(room, 0x15);
        if (tableOffset < 29 || tableOffset >= imageOffset || imageOffset > roomLength || roomLength > room.Length)
            throw new InvalidDataException($"The walk-box offset 0x{tableOffset:X4} is outside the room header area (image at 0x{imageOffset:X4}).");
        var count = room[tableOffset];
        var matrixStart = tableOffset + 1 + count * BoxSize;
        if (matrixStart > imageOffset)
            throw new InvalidDataException($"{count} walk boxes at 0x{tableOffset:X4} run past the room image at 0x{imageOffset:X4}.");
        var boxes = new List<LoomWalkBox>(count);
        for (var index = 0; index < count; index++)
        {
            var at = tableOffset + 1 + index * BoxSize;
            LoomBoxPoint Point(int offset) => new(SignedWord(room, at + offset), SignedWord(room, at + offset + 2));
            boxes.Add(new LoomWalkBox(index, Point(0), Point(4), Point(8), Point(12), room[at + 16], room[at + 17]));
        }
        var matrix = room.AsSpan(matrixStart, imageOffset - matrixStart).ToArray();
        return new LoomWalkBoxSet(boxes, matrix, tableOffset, !MatrixFits(matrix, count));
    }

    /// <summary>True when every box has a row ending in 0xFF inside the matrix bytes.</summary>
    private static bool MatrixFits(byte[] matrix, int count)
    {
        var position = 0;
        for (var from = 0; from < count; from++)
        {
            while (position < matrix.Length && matrix[position] != 0xFF) position += 3;
            if (position >= matrix.Length) return false;
            position++;
        }
        return true;
    }

    private static int Word(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
    private static int SignedWord(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Old-v3 walk boxes (ScummVM <c>ScummEngine_v3old::resetRoomSubBlocks</c> and
/// <c>boxes.cpp</c>): a count byte at the word at room + 0x15, 18-byte boxes,
/// then the box matrix up to the room image.
/// </summary>
public sealed class LoomWalkBoxTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void EveryLoomRoomHasBoxesWhoseMatrixEndsAtTheRoomImage()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var rooms = game.Rooms.Where(room => room.ResourceId is > 0).ToArray();
        Assert.Equal(76, rooms.Length);
        foreach (var room in rooms)
        {
            var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(room.FilePath));
            Assert.True(boxes.Count > 0, $"Room {room.ResourceId} has no boxes");
            // The layout is only right if every box's matrix row ends exactly at the image.
            Assert.False(boxes.MatrixTruncated, $"Room {room.ResourceId} matrix does not fit");
        }
    }

    [Fact]
    public void Room02IsAPathOfLineSegmentBoxes()
    {
        if (!File.Exists(Path.Combine(Folder, "02.LFL"))) return;
        var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(Path.Combine(Folder, "02.LFL")));

        Assert.Equal(15, boxes.Count);
        Assert.All(boxes.Boxes, box => Assert.True(box.IsLine));
        Assert.Equal(new LoomBoxPoint(830, 60), boxes.Boxes[0].UpperLeft);
        Assert.Equal(new LoomBoxPoint(811, 60), boxes.Boxes[0].LowerLeft);
        Assert.Equal(new LoomBoxPoint(912, 80), boxes.Boxes[3].UpperLeft);
        Assert.Equal(new LoomBoxPoint(890, 118), boxes.Boxes[3].LowerRight);
        Assert.Equal(1, boxes.Boxes[3].Mask);
        Assert.Equal(0x12, boxes.Boxes[3].Flags);
        // A point on a segment is inside; one pixel off it is not (no v5 tolerance in v3).
        Assert.True(boxes.Contains(0, 820, 60));
        Assert.False(boxes.Contains(0, 820, 61));
        // The path is a chain: box 0 reaches box 14 through box 1.
        Assert.Equal(1, boxes.GetNextBox(0, 14));
        Assert.Equal(13, boxes.GetNextBox(14, 0));
    }

    [Fact]
    public void Room17MatrixRoutesAroundItsFloorBox()
    {
        if (!File.Exists(Path.Combine(Folder, "17.LFL"))) return;
        var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(Path.Combine(Folder, "17.LFL")));

        Assert.Equal(12, boxes.Count);
        Assert.False(boxes.Boxes[8].IsLine);
        Assert.True(boxes.Contains(8, 140, 130));
        Assert.Equal(9, boxes.GetNextBox(8, 10));
        Assert.Equal(11, boxes.GetNextBox(8, 11));
        Assert.Equal(8, boxes.GetNextBox(11, 0));
        Assert.Equal(-1, boxes.GetNextBox(0, LoomWalkBoxSet.InvalidBox));
    }

    [Fact]
    public void AdjustKeepsAPointInsideABoxAndSnapsOthersToTheClosestEdge()
    {
        var boxes = Synthetic(
            Box(10, 10, 50, 10, 50, 40, 10, 40),
            Box(100, 10, 140, 10, 140, 40, 100, 40, flags: LoomWalkBox.InvisibleFlag));

        Assert.Equal(new BoxAdjustment(20, 20, 0, 20, 20), boxes.Adjust(20, 20, isPlayer: false, 0, 0));
        // 60,20 is 10 pixels right of box 0's right edge.
        var right = boxes.Adjust(60, 20, isPlayer: false, 0, 0);
        Assert.Equal((50, 20, 0), (right.X, right.Y, right.Box));
        // The invisible box is skipped even though the point lies in it.
        var hidden = boxes.Adjust(120, 20, isPlayer: false, 0, 0);
        Assert.Equal((50, 20, 0), (hidden.X, hidden.Y, hidden.Box));
    }

    [Fact]
    public void PlayerOnlyInvisibleBoxesStayUsableByOtherActors()
    {
        var boxes = Synthetic(Box(10, 10, 50, 10, 50, 40, 10, 40,
            flags: LoomWalkBox.InvisibleFlag | LoomWalkBox.PlayerOnlyFlag));

        Assert.Equal(0, boxes.Adjust(20, 20, isPlayer: false, 0, 0).Box);
        Assert.Equal(LoomWalkBoxSet.InvalidBox, boxes.Adjust(20, 20, isPlayer: true, 7, 9).Box);
    }

    [Fact]
    public void AFarPointKeepsTheActorsLastValidPointAsTheV3InterpretersDid()
    {
        // Room 53's only box lies off screen at 349..355, 100..106.
        var boxes = Synthetic(Box(349, 100, 355, 100, 355, 106, 349, 106));

        // 0,0 is more than 0xFFFF (squared) away, so nothing beats the search bound.
        var far = boxes.Adjust(0, 0, isPlayer: false, 12, 34);
        Assert.Equal(new BoxAdjustment(12, 34, LoomWalkBoxSet.InvalidBox, 12, 34), far);
        var near = boxes.Adjust(300, 103, isPlayer: false, 12, 34);
        Assert.Equal((349, 103, 0), (near.X, near.Y, near.Box));
    }

    [Fact]
    public void SetFlagsTreatsOnePastTheLastBoxAsTheLastBox()
    {
        var boxes = Synthetic(Box(0, 0, 8, 0, 8, 8, 0, 8), Box(20, 0, 28, 0, 28, 8, 20, 8));

        Assert.True(boxes.SetFlags(2, 0x80));
        Assert.Equal(0x80, boxes.GetFlags(1));
        Assert.False(boxes.SetFlags(LoomWalkBoxSet.InvalidBox, 0x80));
        Assert.Equal(0, boxes.Clone().GetFlags(1));
    }

    [Fact]
    public void Indy3Room46MatrixIsTheTruncatedOneScummVmWorksAround()
    {
        var root = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "46.LFL"))) return;
        var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(Path.Combine(root, "46.LFL")));

        Assert.Equal(2, boxes.Count);
        Assert.True(boxes.MatrixTruncated);
    }

    private static byte[] Box(int ulx, int uly, int urx, int ury, int lrx, int lry, int llx, int lly,
        byte mask = 0, byte flags = 0)
    {
        var bytes = new List<byte>();
        foreach (var value in new[] { ulx, uly, urx, ury, lrx, lry, llx, lly })
            bytes.AddRange(BitConverter.GetBytes((short)value));
        bytes.Add(mask);
        bytes.Add(flags);
        return bytes.ToArray();
    }

    /// <summary>A plain room header with the boxes at 0x20 and a matrix that links every box to itself.</summary>
    private static LoomWalkBoxSet Synthetic(params byte[][] boxes)
    {
        const int tableOffset = 0x20;
        var body = new List<byte> { (byte)boxes.Length };
        foreach (var box in boxes) body.AddRange(box);
        for (var index = 0; index < boxes.Length; index++)
            body.AddRange(new byte[] { (byte)index, (byte)index, (byte)index, 0xFF });
        var imageOffset = tableOffset + body.Count;
        var room = new byte[imageOffset + 2];
        BitConverter.GetBytes((ushort)room.Length).CopyTo(room, 0);
        BitConverter.GetBytes((ushort)imageOffset).CopyTo(room, 0x0A);
        BitConverter.GetBytes((ushort)tableOffset).CopyTo(room, 0x15);
        body.CopyTo(room, tableOffset);
        var set = LoomWalkBoxReader.ReadPlain(room);
        Assert.False(set.MatrixTruncated);
        return set;
    }
}

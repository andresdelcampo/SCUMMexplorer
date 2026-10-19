namespace SCUMMexplorer.Tests;

public sealed class ScummV2WalkGeometryTests
{
    [Fact]
    public void SlantedEdgeClampsOnTheRequestedRowInsteadOfProjectingDiagonally()
    {
        var boxes = NativeBoxes((10, 18, 10, 20, 14, 24, 0));
        Assert.Equal((96, 28, 0), Point(boxes.Adjust(32, 28, true, 0, 0)));
        Assert.Equal((8, 96, 28), ScummWalkBoxSet.ClosestPointV2(boxes.Boxes[0], 32, 28));
    }

    [Fact]
    public void NativeMetricWeighsFourVerticalUnitsAsOneHorizontalUnit()
    {
        var boxes = NativeBoxes((8, 10, 0, 2, 0, 2, 0), (0, 1, 3, 4, 3, 4, 0));
        // Vertical native distance8/4=2 beats horizontal3. Euclidean squared
        // distance incorrectly picks the right-hand box (3² < 8²).
        Assert.Equal((0, 16, 0), Point(boxes.Adjust(0, 0, true, 0, 0)));
    }

    [Fact]
    public void CoarseZeroDistanceAcceptsTheHigherBoxBeforeAnExactContainingBox()
    {
        var boxes = NativeBoxes((0, 1, 0, 4, 0, 4, 0), (3, 4, 0, 4, 0, 4, 0));
        Assert.Equal((8, 6, 1), Point(boxes.Adjust(8, 0, true, 0, 0)));
    }

    [Fact]
    public void EqualDistancesKeepTheHigherBoxAndTheNativeFfBoundKeepsTheRequestedPoint()
    {
        var boxes = NativeBoxes((0, 1, 0, 1, 0, 1, 0), (0, 1, 5, 6, 5, 6, 0));
        Assert.Equal((40, 0, 1), Point(boxes.Adjust(24, 0, true, 0, 0)));
        var far = NativeBoxes((0, 1, 255, 255, 255, 255, 0));
        Assert.Equal((0, 0, 255), Point(far.Adjust(0, 0, true, 992, 888)));
        Assert.Equal((24, 8, 255), Point(NativeBoxes().Adjust(24, 8, true, 992, 888)));
    }

    [Fact]
    public void AnInteriorCrossingPointStaysInteriorAndHiddenPlayerBoxesRemainExcluded()
    {
        var boxes = NativeBoxes((0, 20, 0, 20, 0, 20, 0));
        Assert.Equal((0, 80, 20), ScummWalkBoxSet.ClosestPointV2(boxes.Boxes[0], 80, 20));
        var hidden = NativeBoxes((0, 20, 0, 20, 0, 20, 0xA0));
        Assert.Equal(255, hidden.Adjust(80, 20, true, 0, 0).Box);
        Assert.Equal(0, hidden.Adjust(80, 20, false, 0, 0).Box);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothOldActorVersionsRecomputeNativePriorityEvenInsideTheirCachedDestinationBox(bool classic)
    {
        var boxes = NativeBoxes((0, 1, 0, 10, 0, 10, 0), (3, 4, 5, 6, 5, 6, 0));
        var actors = new ScummActorTable();
        actors.InitializeV2Game(classic);
        var place = new ScummActorTable.Placement(1, boxes, null, null, _ => true);
        actors.Put(1, 8, 0, 1, place, 1);
        Assert.Equal(0, actors[1].WalkBox);
        actors.Walk(1, 40, 0, place, "native cached-box priority");
        for (int i = 0; i < 100 && actors[1].Moving != 0; i++) actors.StepWalks(place);
        Assert.Equal((40, 6, 1, 0), (actors[1].X, actors[1].Y, actors[1].WalkBox, actors[1].Moving));
    }

    [Theory]
    [InlineData("mmansion ega en", 1, 440, 88, 440, 92, 4)]
    [InlineData("mmansionE ega es", 1, 352, 72, 352, 84, 4)]
    public void ActualManiacObjectApproachesUseTheirOwnNativeBoxRows(string folder, int room, int x, int y, int expectedX, int expectedY, int expectedBox)
        => CheckFixture(folder, room, x, y, expectedX, expectedY, expectedBox);

    [Theory]
    [InlineData("zak ega en", 3, 792, 104, 816, 104, 7)]
    [InlineData("zakE ega en", 3, 792, 104, 816, 104, 7)]
    [InlineData("zak ega en", 3, 744, 104, 736, 114, 10)]
    [InlineData("zakE ega en", 3, 744, 104, 736, 114, 10)]
    public void ActualZakObjectApproachesKeepNativeRowsAndWeightedBoxChoice(string folder, int room, int x, int y, int expectedX, int expectedY, int expectedBox)
        => CheckFixture(folder, room, x, y, expectedX, expectedY, expectedBox);

    private static void CheckFixture(string folder, int roomId, int x, int y, int expectedX, int expectedY, int expectedBox)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var room = ScummDataSources.For(game).ReadRoom(roomId);
        Assert.Contains(room.Objects, o => o.WalkX == x && o.WalkY == y);
        Assert.Equal((expectedX, expectedY, expectedBox), Point(room.WalkBoxes!.Adjust(x, y, true, 0, 0)));
    }

    private static (int X, int Y, int Box) Point(BoxAdjustment result) => (result.X, result.Y, result.Box);

    // Native byte layout, not a game-specific synthetic production path.
    private static ScummWalkBoxSet NativeBoxes(params (byte Top, byte Bottom, byte Ul, byte Ur, byte Ll, byte Lr, byte Flags)[] boxes)
    {
        var bytes = new byte[31 + boxes.Length * 8 + boxes.Length * (boxes.Length + 1)];
        bytes[0x15] = 30; bytes[30] = (byte)boxes.Length;
        for (int i = 0; i < boxes.Length; i++)
        {
            var b = boxes[i]; var at = 31 + i * 8;
            byte[] record = [b.Top, b.Bottom, b.Ul, b.Ur, b.Ll, b.Lr, 0, b.Flags];
            record.CopyTo(bytes, at);
        }
        var matrix = 31 + boxes.Length * 8;
        for (int from = 0; from < boxes.Length; from++)
        {
            bytes[matrix + from] = (byte)(from * boxes.Length);
            for (int to = 0; to < boxes.Length; to++) bytes[matrix + boxes.Length + from * boxes.Length + to] = (byte)to;
        }
        return ScummWalkBoxReader.ReadV2(bytes);
    }
}

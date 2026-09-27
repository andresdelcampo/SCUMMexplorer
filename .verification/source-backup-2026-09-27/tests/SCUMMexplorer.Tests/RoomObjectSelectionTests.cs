using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class RoomObjectSelectionTests
{
    [Fact]
    public void IndexStateControlsStartingVisibility()
    {
        var first = Image(10, 8, 0, 8, 8);
        var second = Image(11, 8, 0, 8, 8);
        var selection = new RoomObjectSelection([first, second],
            new Dictionary<int, byte> { [10] = 0, [11] = 2 });

        Assert.False(selection.IsActive(first));
        Assert.True(selection.IsActive(second));
        Assert.Equal(second, selection.HitTest(9, 2));
    }

    [Fact]
    public void BackgroundClickShowsFirstThenCyclesImagesThenBackground()
    {
        var first = Image(10, 8, 0, 8, 8);
        var second = Image(11, 8, 0, 8, 8);
        var selection = new RoomObjectSelection([first, second]);

        Assert.Equal(first, selection.HitTest(9, 2));
        Assert.Equal(first, selection.Next(selection.HitTest(9, 2)!));
        Assert.Equal(second, selection.Next(selection.HitTest(9, 2)!));
        Assert.Null(selection.Next(selection.HitTest(9, 2)!));
        Assert.False(selection.IsActive(first));
        Assert.False(selection.IsActive(second));
        Assert.Equal(first, selection.Next(selection.HitTest(9, 2)!));
    }

    [Fact]
    public void OneImageAlsoCyclesThroughBackgroundIncludingColourZero()
    {
        var image = Image(1, 8, 0, 8, 8);
        var selection = new RoomObjectSelection([image]);

        Assert.Equal(image, selection.Next(selection.HitTest(10, 3)!));
        Assert.Null(selection.Next(selection.HitTest(10, 3)!));
        Assert.Equal(image, selection.HitTest(10, 3));
    }

    [Fact]
    public void BackgroundIsNotInventedWhenKnownStatesNeverLeaveThePositionEmpty()
    {
        var first = Image(10, 8, 0, 8, 8);
        var second = Image(11, 8, 0, 8, 8);
        var selection = new RoomObjectSelection([first, second],
            new Dictionary<int, byte> { [10] = 1, [11] = 0 });

        Assert.False(selection.CanShowBackground(first));
        Assert.Equal(second, selection.Next(first));
        Assert.Equal(first, selection.Next(second));
        Assert.True(selection.IsActive(first));
    }

    [Fact]
    public void BlackPlaceholderDoesNotBecomeAClickAlternative()
    {
        var first = Image(10, 0, 0, 8, 8);
        var second = Image(11, 0, 0, 8, 8);
        var selection = new RoomObjectSelection([first, second],
            background: Background(new byte[64]));

        Assert.False(selection.CanShowBackground(first));
        Assert.Equal(first, selection.Next(selection.HitTest(2, 2)!));
        Assert.Equal(second, selection.Next(selection.HitTest(2, 2)!));
        Assert.Equal(first, selection.Next(selection.HitTest(2, 2)!));
    }

    [Fact]
    public void PaintedBackgroundCanHideAndRestoreSessionObject()
    {
        var image = Image(10, 0, 0, 8, 8);
        var selection = new RoomObjectSelection([image],
            new Dictionary<int, byte> { [10] = 1 },
            background: Background(Enumerable.Repeat((byte)1, 64).ToArray()),
            indexedStates: new Dictionary<int, byte> { [10] = 0 });

        Assert.True(selection.CanShowBackground(image));
        Assert.Null(selection.Next(selection.HitTest(2, 2)!));
        Assert.False(selection.IsActive(image));
        Assert.Equal(image, selection.Next(selection.HitTest(2, 2)!));
        Assert.True(selection.IsActive(image));
    }

    [Fact]
    public void DifferentBoundsRemainIndependentEvenIfTheyShareBottomEdge()
    {
        var first = Image(479, 280, 48, 56, 88);
        var second = Image(486, 280, 48, 56, 88);
        var taller = Image(493, 280, 24, 56, 112);
        var selection = new RoomObjectSelection([first, second, taller]);

        Assert.Equal(first, selection.Next(first));
        Assert.Equal(second, selection.Next(first));
        Assert.False(selection.IsActive(taller));
        Assert.True(selection.Select(taller));
        Assert.True(selection.IsActive(second));
        Assert.True(selection.IsActive(taller));
        Assert.Equal(taller, selection.HitTest(300, 32));
    }

    [Fact]
    public void HitTestPrefersFirstDirectoryImageOverLaterOverlappingImage()
    {
        var lower = Image(1, 8, 0, 16, 16);
        var upper = Image(2, 12, 0, 8, 8);
        var selection = new RoomObjectSelection([lower, upper]);

        Assert.True(selection.Select(lower));
        Assert.Equal(lower, selection.HitTest(14, 2));
        Assert.True(selection.Select(upper));
        Assert.Equal(lower, selection.HitTest(14, 2));
        Assert.Null(selection.Next(lower));
        Assert.Equal(upper, selection.HitTest(14, 2));
    }

    [Fact]
    public void OriginIsAValidStoredPosition()
    {
        var image = Image(5, 0, 0, 8, 8);
        var selection = new RoomObjectSelection([image]);
        Assert.Equal(image, selection.Next(selection.HitTest(0, 0)!));
        Assert.True(selection.IsActive(image));
    }

    [Fact]
    public void DrawObjectMovesBeforeClearingOnlyExactCurrentRectangle()
    {
        var moved = Image(1, 0, 0, 8, 8);
        var oldPeer = Image(2, 0, 0, 8, 8);
        var newPeer = Image(3, 16, 8, 8, 8);
        var selection = new RoomObjectSelection([moved, oldPeer, newPeer]);

        Assert.True(selection.SetState(2, 1));
        Assert.True(selection.SetState(3, 1));
        Assert.True(selection.DrawObject(1, 16, 8));

        Assert.Equal(new RoomObjectPosition(16, 8), selection.GetPosition(moved));
        Assert.True(selection.IsActive(oldPeer));
        Assert.False(selection.IsActive(newPeer));
        Assert.True(selection.IsActive(moved));
        Assert.Equal(moved, selection.HitTest(17, 9));
    }

    [Fact]
    public void SessionVisibilityFollowsDecodedParentStateChain()
    {
        var parent = Image(1, 0, 0, 8, 8);
        var child = Image(2, 8, 0, 8, 8);
        var grandchild = Image(3, 16, 0, 8, 8);
        var records = new[]
        {
            Record(parent, 1, 0, 0),
            Record(child, 2, 1, 2),
            Record(grandchild, 3, 2, 1)
        };
        var selection = new RoomObjectSelection([parent, child, grandchild],
            new Dictionary<int, byte> { [1] = 1, [2] = 1, [3] = 1 }, roomRecords: records);

        Assert.True(selection.IsActive(parent));
        Assert.False(selection.IsActive(child));
        Assert.False(selection.IsActive(grandchild));

        Assert.True(selection.SetState(1, 2));
        Assert.True(selection.IsActive(child));
        Assert.True(selection.IsActive(grandchild));
    }

    [Fact]
    public void SessionVisibilityUsesStateOfCodeOnlyParent()
    {
        var child = Image(2, 8, 0, 8, 8);
        var records = new[]
        {
            new LoomRoomObjectRecord(1, 1, 0, 0, 8, 8,
                0, 0, 0, 0, 0, 0, null),
            new LoomRoomObjectRecord(3, 2, 0, 0, 8, 8,
                1, 1, 0, 0, 0, 0, null),
            Record(child with { Id = 4 }, 3, 2, 2)
        };
        child = child with { Id = 4 };
        var selection = new RoomObjectSelection([child],
            new Dictionary<int, byte> { [1] = 1, [3] = 2, [4] = 1 }, roomRecords: records);

        Assert.True(selection.IsActive(child));
        Assert.True(selection.SetState(3, 0));
        Assert.False(selection.IsActive(child));
        Assert.True(selection.SetState(3, 2));
        Assert.True(selection.IsActive(child));
    }

    [Fact]
    public void UnknownCodeOnlyParentDoesNotSatisfyRequiredZeroState()
    {
        var child = Image(2, 8, 0, 8, 8);
        var records = new[]
        {
            new LoomRoomObjectRecord(1, 1, 0, 0, 8, 8,
                0, 0, 0, 0, 0, 0, null),
            Record(child, 2, 1, 0)
        };
        var selection = new RoomObjectSelection([child],
            new Dictionary<int, byte> { [2] = 1 }, roomRecords: records);

        Assert.False(selection.IsActive(child));
        Assert.True(selection.SetState(1, 0));
        Assert.True(selection.IsActive(child));
    }

    [Fact]
    public void SessionVisibilityHandlesMissingOrCyclicParents()
    {
        var image = Image(5, 0, 0, 8, 8);
        var selection = new RoomObjectSelection([image],
            new Dictionary<int, byte> { [5] = 1 },
            roomRecords: [Record(image, 1, 2, 1)]);
        Assert.False(selection.IsActive(image));
    }

    [Fact]
    public void ArtworkEstimateKeepsScriptVisibleImageAndAvoidsOverlappingAlternatives()
    {
        var scriptImage = Image(1, 0, 0, 16, 16);
        var overlapping = Image(2, 8, 0, 16, 16);
        var first = Image(3, 32, 0, 8, 8);
        var alternative = Image(4, 32, 0, 8, 8);
        var selection = new RoomObjectSelection([scriptImage, overlapping, first, alternative],
            new Dictionary<int, byte> { [1] = 1, [2] = 0, [3] = 0, [4] = 0 });

        Assert.Equal([3], selection.ShowUnresolvedArtwork());
        Assert.True(selection.IsActive(scriptImage));
        Assert.False(selection.IsActive(overlapping));
        Assert.True(selection.IsActive(first));
        Assert.False(selection.IsActive(alternative));
        Assert.True(selection.CanShowBackground(first));
    }

    private static RoomObjectImage Image(int id, int x, int y, int width, int height) =>
        new(id, x, y, width, height, new byte[width * height]);

    private static LoomRoomObjectRecord Record(RoomObjectImage image, int localIndex,
        int parentIndex, byte parentState) =>
        new(image.Id, localIndex, image.X, image.Y, image.Width, image.Height,
            parentIndex, parentState, 0, 0, 0, 0, null);

    private static RoomImage Background(byte[] pixels) =>
        new(8, 8, pixels, [0xFF000000, 0xFFFFFFFF]);
}

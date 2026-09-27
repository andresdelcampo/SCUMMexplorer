using SCUMMexplorer.Core.Formats;
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
    public void DirectoryOrderPaintsEarlierEntryOnTopUntilADrawCallRepaints()
    {
        var lower = Filled(1, 8, 0, 16, 16, 1);
        var upper = Filled(2, 12, 0, 8, 8, 2);
        var selection = new RoomObjectSelection([lower, upper],
            new Dictionary<int, byte> { [1] = 1, [2] = 1 });

        // Full redraw: the first directory entry is painted last.
        Assert.Equal([upper, lower], selection.PaintOrder());
        Assert.Equal(lower, selection.HitTest(14, 2));

        // drawObject paints over the redraw in call order.
        Assert.True(selection.Select(upper));
        Assert.Equal(upper, selection.HitTest(14, 2));
        Assert.Equal(lower, selection.HitTest(9, 2));
        Assert.True(selection.Select(lower));
        Assert.Equal(lower, selection.HitTest(14, 2));

        // setState redraws the room and clears the queue.
        Assert.True(selection.SetState(2, 1));
        Assert.Empty(selection.DrawQueue);
        Assert.Equal(lower, selection.HitTest(14, 2));
    }

    [Fact]
    public void ScriptDrawQueueKeepsInsertAboveItsCutoutContainer()
    {
        // Room 56 pattern: a face whose eye area is colour 0, drawn before the eyes.
        var face = Cutout(779, 0, 0, 24, 16, 8, 8, 8, 8, 12);
        var eyes = Filled(782, 8, 8, 8, 8, 10);
        var states = new Dictionary<int, byte> { [779] = 1, [782] = 1 };
        var background = new RoomImage(24, 16, new byte[24 * 16], Palette);

        var redrawOnly = new RoomObjectSelection([face, eyes], states, background: background);
        Assert.Equal(0, redrawOnly.Compose(background)[9 * 24 + 9]);

        var scripted = new RoomObjectSelection([face, eyes], states, background: background,
            drawQueue: [779, 782]);
        Assert.Equal(10, scripted.Compose(background)[9 * 24 + 9]);
        Assert.Equal(eyes, scripted.HitTest(9, 9));
        Assert.Equal(face, scripted.HitTest(1, 1));
    }

    [Fact]
    public void GapFillingUsesScriptBatchesAndThenInsertsForCutouts()
    {
        var bgPixels = Enumerable.Repeat((byte)2, 32 * 24).ToArray();
        for (var y = 4; y < 20; y++)
            for (var x = 8; x < 24; x++) bgPixels[y * 32 + x] = 0;
        var background = new RoomImage(32, 24, bgPixels, Palette);
        var face = Cutout(10, 8, 4, 16, 16, 12, 12, 8, 8, 12);
        var fullFace = Filled(11, 8, 4, 16, 16, 13);
        var eyes = Filled(12, 12, 12, 8, 8, 10);
        var blink = Filled(13, 12, 12, 8, 8, 11);
        var selection = new RoomObjectSelection([face, fullFace, eyes, blink],
            new Dictionary<int, byte> { [10] = 0, [11] = 0, [12] = 0, [13] = 0 }, background: background);

        var estimates = selection.FillGaps(
            [new RoomDrawBatch([new RoomDrawStep(10), new RoomDrawStep(13)], 3, "opening frame")]);

        Assert.Equal([10, 13], estimates.Select(item => item.ObjectId));
        Assert.All(estimates, item => Assert.Equal("opening frame", item.Reason));
        var composite = selection.Compose(background);
        Assert.DoesNotContain(composite, pixel => pixel == 0);

        // Cycling the face keeps the eyes painted in its cut-out.
        Assert.Equal(fullFace, selection.Next(face));
        Assert.Equal(face, selection.Next(fullFace));
        Assert.Equal(11, selection.Compose(background)[13 * 32 + 13]);
        Assert.False(selection.CanShowBackground(face));
    }

    [Fact]
    public void GapWithoutBatchIsFilledFromStoredImagesAndBlackScenesAreLeftAlone()
    {
        var bgPixels = Enumerable.Repeat((byte)3, 16 * 16).ToArray();
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++) bgPixels[(y + 4) * 16 + x + 4] = 0;
        var background = new RoomImage(16, 16, bgPixels, Palette);
        var first = Filled(1, 4, 4, 8, 8, 5);
        var second = Filled(2, 4, 4, 8, 8, 6);
        var selection = new RoomObjectSelection([first, second], background: background);

        var estimate = Assert.Single(selection.FillGaps(seed: 7));
        Assert.Contains(estimate.ObjectId, new[] { 1, 2 });
        Assert.DoesNotContain(selection.Compose(background), pixel => pixel == 0);

        var dark = new RoomImage(16, 16, new byte[256], Palette);
        var darkSelection = new RoomObjectSelection([Filled(3, 4, 4, 8, 8, 5)], background: dark);
        Assert.Empty(darkSelection.FillGaps());
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

        var estimate = Assert.Single(selection.ShowUnresolvedArtwork());
        Assert.Equal(3, estimate.ObjectId);
        Assert.Equal(RoomObjectSelection.FirstStoredImageReason, estimate.Reason);
        Assert.True(selection.IsActive(scriptImage));
        Assert.False(selection.IsActive(overlapping));
        Assert.True(selection.IsActive(first));
        Assert.False(selection.IsActive(alternative));
        Assert.True(selection.CanShowBackground(first));
    }

    [Fact]
    public void ArtworkEstimatePrefersTheStrongestScriptBatchAndLabelsEveryPick()
    {
        // Position A (0,0) is drawn by an opening frame, an entry script and
        // another script; position B (16,0) only by the entry script, which
        // draws it together with A's alternative; position C (32,0) by nothing.
        var a1 = Filled(1, 0, 0, 8, 8, 1);
        var a2 = Filled(2, 0, 0, 8, 8, 2);
        var a3 = Filled(3, 0, 0, 8, 8, 3);
        var b1 = Filled(4, 16, 0, 8, 8, 4);
        var b2 = Filled(5, 16, 0, 8, 8, 5);
        var c1 = Filled(6, 32, 0, 8, 8, 6);
        var c2 = Filled(7, 32, 0, 8, 8, 7);
        var selection = new RoomObjectSelection([a1, a2, a3, b1, b2, c1, c2]);

        var estimates = selection.ShowUnresolvedArtwork(
        [
            new RoomDrawBatch([new RoomDrawStep(1)], LoomSceneDrawBatches.OtherScriptPriority, "script 01"),
            new RoomDrawBatch([new RoomDrawStep(2), new RoomDrawStep(5)], LoomSceneDrawBatches.EntryScriptPriority, "entry script"),
            new RoomDrawBatch([new RoomDrawStep(3)], LoomSceneDrawBatches.OpeningFramePriority, "opening frame"),
        ], seed: 4);

        Assert.Equal([(3, "opening frame"), (5, "entry script"), (6, RoomObjectSelection.FirstStoredImageReason)],
            estimates.Select(item => (item.ObjectId, item.Reason)));
        // The entry batch also draws 2, but A already shows the opening frame.
        Assert.Equal([3, 5, 6], new[] { a1, a2, a3, b1, b2, c1, c2 }.Where(selection.IsActive).Select(obj => obj.Id));
        Assert.Equal([3, 5], selection.DrawQueue);
    }

    [Fact]
    public void ArtworkEstimateBreaksBatchTiesWithTheRoomSeed()
    {
        var first = Filled(1, 0, 0, 8, 8, 1);
        var second = Filled(2, 0, 0, 8, 8, 2);
        RoomDrawBatch[] batches =
        [
            new([new RoomDrawStep(2)], LoomSceneDrawBatches.OtherScriptPriority, "script 02"),
            new([new RoomDrawStep(1)], LoomSceneDrawBatches.OtherScriptPriority, "script 01"),
        ];
        (int, string) Pick(int seed) =>
            Assert.Single(new RoomObjectSelection([first, second]).ShowUnresolvedArtwork(batches, seed)) is var estimate
                ? (estimate.ObjectId, estimate.Reason) : default;

        var picks = Enumerable.Range(0, 16).Select(Pick).ToArray();
        Assert.Equal(picks, Enumerable.Range(0, 16).Select(Pick));
        Assert.Contains((1, "script 01"), picks);
        Assert.Contains((2, "script 02"), picks);
        // Ties are ordered by source before the seeded pick, so input order does not matter.
        Assert.Equal(Pick(5), Assert.Single(new RoomObjectSelection([first, second])
            .ShowUnresolvedArtwork(batches.Reverse().ToArray(), 5)) is var e ? (e.ObjectId, e.Reason) : default);
    }

    [Fact]
    public void ArtworkEstimateFromABatchClosesTheCutoutsItOpens()
    {
        var bgPixels = Enumerable.Repeat((byte)2, 32 * 24).ToArray();
        var background = new RoomImage(32, 24, bgPixels, Palette);
        var face = Cutout(10, 8, 4, 16, 16, 12, 12, 8, 8, 12);
        var fullFace = Filled(11, 8, 4, 16, 16, 13);
        var eyes = Filled(12, 12, 12, 8, 8, 10);
        var selection = new RoomObjectSelection([face, fullFace, eyes],
            new Dictionary<int, byte> { [10] = 0, [11] = 0, [12] = 0 }, background: background);

        var estimates = selection.ShowUnresolvedArtwork(
            [new RoomDrawBatch([new RoomDrawStep(10)], LoomSceneDrawBatches.EntryScriptPriority, "entry script")]);

        Assert.Equal([(10, "entry script"), (12, "random choice among the images stored for this position")],
            estimates.Select(item => (item.ObjectId, item.Reason)));
        Assert.DoesNotContain(selection.Compose(background), pixel => pixel == 0);
    }

    [Fact]
    public void InterfaceImagesAreNeverUsedAsRoomArtwork()
    {
        var background = new RoomImage(32, 24, Enumerable.Repeat((byte)2, 32 * 24).ToArray(), Palette);
        var icon = Filled(1, 0, 0, 8, 8, 3);
        var room = Filled(2, 16, 0, 8, 8, 4);
        var selection = new RoomObjectSelection([icon, room], new Dictionary<int, byte> { [1] = 0, [2] = 0 },
            background: background);
        selection.ExcludeInterfaceImages([1]);

        var estimate = Assert.Single(selection.ShowUnresolvedArtwork());

        Assert.Equal(2, estimate.ObjectId);
        Assert.False(selection.IsActive(icon));
        Assert.Null(selection.HitTest(2, 2));
        Assert.Equal(room, selection.HitTest(18, 2));
    }

    [Fact]
    public void AReachedStateBlocksOnlyTheStoredImageGuess()
    {
        var a = Filled(1, 0, 0, 8, 8, 1);
        var b = Filled(2, 16, 0, 8, 8, 2);
        var selection = new RoomObjectSelection([a, b], new Dictionary<int, byte> { [1] = 0, [2] = 0 });

        // Both were set to 0 by reached code; a later batch still draws 1.
        var estimates = selection.ShowUnresolvedArtwork(
            [new RoomDrawBatch([new RoomDrawStep(1)], LoomSceneDrawBatches.OpeningFramePriority, "opening frame")],
            statesSetByCode: [1, 2]);

        Assert.Equal([(1, "opening frame")], estimates.Select(item => (item.ObjectId, item.Reason)));
        Assert.False(selection.IsActive(b));
    }

    [Fact]
    public void AnUnavoidableEntryStateReplacesTheStoredImageGuess()
    {
        var hidden = Filled(1, 0, 0, 8, 8, 1);
        var shownFirst = Filled(2, 16, 0, 8, 8, 2);
        var shownSecond = Filled(3, 16, 0, 8, 8, 3);
        var guessed = Filled(4, 32, 0, 8, 8, 4);
        var selection = new RoomObjectSelection([hidden, shownFirst, shownSecond, guessed]);

        var estimates = selection.ShowUnresolvedArtwork(entryStates: new Dictionary<int, RoomStateEvidence>
        {
            [1] = new(0, "entry +0x0000 sets it to 0 on every path"),
            [3] = new(1, "entry +0x0004 sets it to 1 on every path"),
        });

        Assert.Equal([(3, "entry +0x0004 sets it to 1 on every path"), (4, RoomObjectSelection.FirstStoredImageReason)],
            estimates.Select(item => (item.ObjectId, item.Reason)));
        Assert.False(selection.IsActive(hidden));
        Assert.False(selection.IsActive(shownFirst));
    }

    private static RoomObjectImage Image(int id, int x, int y, int width, int height) =>
        new(id, x, y, width, height, new byte[width * height]);

    private static readonly uint[] Palette = Enumerable.Range(0, 16).Select(value => 0xFF000000u | (uint)value).ToArray();

    private static RoomObjectImage Filled(int id, int x, int y, int width, int height, byte colour) =>
        new(id, x, y, width, height, Enumerable.Repeat(colour, width * height).ToArray());

    private static RoomObjectImage Cutout(int id, int x, int y, int width, int height,
        int holeX, int holeY, int holeWidth, int holeHeight, byte colour)
    {
        var pixels = Enumerable.Repeat(colour, width * height).ToArray();
        for (var row = holeY - y; row < holeY - y + holeHeight; row++)
            for (var column = holeX - x; column < holeX - x + holeWidth; column++)
                pixels[row * width + column] = 0;
        return new RoomObjectImage(id, x, y, width, height, pixels);
    }

    private static LoomRoomObjectRecord Record(RoomObjectImage image, int localIndex,
        int parentIndex, byte parentState) =>
        new(image.Id, localIndex, image.X, image.Y, image.Width, image.Height,
            parentIndex, parentState, 0, 0, 0, 0, null);

    private static RoomImage Background(byte[] pixels) =>
        new(8, 8, pixels, [0xFF000000, 0xFFFFFFFF]);
}

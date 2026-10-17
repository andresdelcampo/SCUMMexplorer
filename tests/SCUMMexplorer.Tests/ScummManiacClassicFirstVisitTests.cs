namespace SCUMMexplorer.Tests;

/// <summary>Classic English v1 S5. These are bounded source-state previews, not original-game picture comparisons.</summary>
public sealed class ScummManiacClassicFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(44)]
    public void FirstVisitUsesClassicStateAndNativeHotspotFlags(int room)
    {
        if (Game() is not { } game) return;
        var scene = SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Run(game, room);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Empty(scene.Gaps());
        Assert.Equal(room == 1 ? new[] { 56, 57, 62, 63, 64 } : [], scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Order().ToArray());
        if (room == 1)
        {
            Assert.Equal(11, scene.Snapshot.ObjectStates[56]);
            Assert.Equal(10, scene.Snapshot.ObjectStates[62]);
            Assert.Equal(4, scene.Snapshot.ObjectStates[53]);
        }
        if (room == 2)
        {
            Assert.Equal(1, scene.Snapshot.ObjectStates[141]);
            Assert.Equal(1, scene.Snapshot.ObjectStates[167]);
        }
        if (room == 44)
        {
            Assert.Equal(0, scene.Snapshot.ObjectStates[258]);
            Assert.Equal(0, scene.Snapshot.ObjectStates[278]);
            Assert.True(scene.IsFirstRoomOfTheGame);
            foreach (var number in new[] { 1, 3, 5 })
            {
                var drawn = scene.ActorLayer().Actors.Single(actor => actor.Number == number);
                Assert.True(drawn.Pixels > 500, $"Actor {number} has no substantial visible sprite.");
                Assert.True(drawn.Bounds is { Top: >= 0, Bottom: > 90 }, $"Actor {number} is displaced above the room.");
                Assert.True(drawn.Bounds!.Value.Bottom - drawn.Bounds.Value.Top > 60, $"Actor {number} lacks its body limbs.");
            }
        }
        foreach (var image in scene.Images.Where(scene.Selection.IsActive))
            Assert.NotEqual(0, scene.Snapshot.ObjectStates[image.Id] & 8);
        foreach (var record in scene.Records.Where(scene.IsHotspot))
        {
            Assert.Equal(0, scene.Snapshot.ObjectStates[record.Id] & 2);
            Assert.False(scene.Snapshot.IsUntouchable(record.Id));
            Assert.Equal(15, scene.Snapshot.ObjectOwners.GetValueOrDefault(record.Id, (byte)15));
            var current = record;
            while (current.ParentLocalIndex != 0)
            {
                var parent = scene.Records.Single(item => item.LocalIndex == current.ParentLocalIndex);
                Assert.Equal(current.ParentRequiredState, scene.Snapshot.ObjectStates[parent.Id] & 8);
                current = parent;
            }
        }
    }

    [Fact]
    public void ArcadeCoinSlotDetectorFindingsAreAlreadyInTheNativeBackground()
    {
        if (Game() is not { } game) return;
        var scene = SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Run(game, 18);
        var gaps = scene.Gaps();
        Assert.Equal(6, gaps.Count);
        Assert.All(gaps, gap =>
        {
            Assert.False(gap.Fillable);
            Assert.Equal(8, gap.Width); Assert.Equal(8, gap.Height);
            var black = 0;
            for (var y = gap.Y; y < gap.Y + gap.Height; y++)
            for (var x = gap.X; x < gap.X + gap.Width; x++)
                if (scene.Room.Pixels.Span[y * scene.Room.Width + x] == 0) black++;
            Assert.Equal(58, black);
        });
        Assert.DoesNotContain(scene.Images, scene.Selection.IsActive);
        Assert.Equal(scene.Room.Pixels.ToArray(), scene.Selection.Compose(scene.Room));
    }

}

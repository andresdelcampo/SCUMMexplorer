namespace SCUMMexplorer.Tests;

public sealed class ScummClassicCostumeAnimationTests
{
    [Fact]
    public void ClassicLiveLimbPixelsAgreeWithTheAcceptedStoredPoseDecoder()
    {
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var colors = ScummV1ActorColors.For(game)!;
        var checkedPoses = 0;
        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Costume && item.ResourceId is > 0))
        {
            var id = resource.ResourceId!.Value;
            var costume = ScummCostume.Load(game, id)!;
            var container = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
            for (var pose = 0; pose <= costume.AnimationLimit; pose++)
            {
                var expected = new ScummCostumeRenderer().DecodePose(container, resource.Offset!.Value, pose, game.V1Game, colors.ColorOf(id));
                var animation = new ScummCostumeAnimationState();
                costume.DecodeAnimation(animation, pose / 4, pose % 4);
                var cels = costume.Draw(animation, pose % 4);
                if (expected is null) { Assert.Empty(cels); continue; }
                Assert.NotEmpty(cels);
                var left = cels.Min(cel => cel.Left); var top = cels.Min(cel => cel.Top);
                var width = cels.Max(cel => cel.Left + cel.Width) - left;
                var height = cels.Max(cel => cel.Top + cel.Height) - top;
                Assert.Equal(expected.Width, width); Assert.Equal(expected.Height, height);
                var pixels = new byte[width * height]; var coverage = new bool[pixels.Length];
                costume.Compose(cels, pixels, width, height, -left, -top, coverage: coverage, actorNumber: id);
                for (var pixel = 0; pixel < pixels.Length; pixel++)
                {
                    Assert.Equal(expected.Pixels.Span[pixel] != 0, coverage[pixel]);
                    if (coverage[pixel]) Assert.Equal(expected.Pixels.Span[pixel] == 16 ? 0 : expected.Pixels.Span[pixel], pixels[pixel]);
                }
                checkedPoses++;
            }
        }
        Assert.True(checkedPoses > 100);
    }

    [Fact]
    public void ClassicColoursFollowTheActorRatherThanTheCostumeNumber()
    {
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var costume = ScummCostume.Load(new GameFolderScanner().Scan(folder), 1)!;
        Assert.Equal(12, costume.MapColor(1, null, 1));
        Assert.Equal(5, costume.MapColor(1, null, 4));
        Assert.Equal(14, costume.MapColor(2, null, 1));
        Assert.Equal(6, costume.MapColor(2, null, 4));
        Assert.Equal(0, costume.MapColor(3, null, 1));
    }
}


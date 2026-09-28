using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Costume animation (ScummVM <c>ClassicCostumeLoader::costumeDecodeData</c>, <c>increaseAnim</c> and
/// <c>ClassicCostumeRenderer::drawLimb</c>) on the ego's costume 1: script 001 sets <c>var[0x077]</c> = 1 and
/// script 017 gives it to actor 1 with init 1, walk 2 and stand 3.
/// </summary>
public sealed class LoomCostumeAnimationTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static LoomCostume? EgoCostume() =>
        File.Exists(Path.Combine(Folder, "00.LFL")) ? LoomCostume.Load(new GameFolderScanner().Scan(Folder), 1) : null;

    [Fact]
    public void TheStandFrameHoldsStill()
    {
        var costume = EgoCostume();
        if (costume is null) return;
        Assert.Equal((63, false, (byte)1), (costume.AnimationLimit, costume.Mirror, costume.PaletteColor));

        var state = new LoomCostumeAnimationState();
        Assert.True(costume.DecodeAnimation(state, frame: 3, direction: 2));
        Assert.Equal([3, 4], state.Positions.Take(2));
        Assert.All(state.Positions.Skip(2), position => Assert.Equal(-1, position));
        Assert.Equal(14, state.Animation[0]);

        var first = Describe(costume.Draw(state, 2));
        Assert.Equal("L0(-14,-42 24x43) L1(-4,-41 8x9)", first);
        for (var step = 0; step < 5; step++)
        {
            Assert.False(costume.IncreaseAnimations(state));
            Assert.Equal(first, Describe(costume.Draw(state, 2)));
        }
    }

    [Fact]
    public void TheWalkFrameLoopsOverItsCommands()
    {
        var costume = EgoCostume();
        if (costume is null) return;
        var state = new LoomCostumeAnimationState();
        Assert.True(costume.DecodeAnimation(state, frame: 2, direction: 2));

        var body = new List<int> { state.Positions[0] };
        for (var step = 0; step < 7; step++)
        {
            Assert.True(costume.IncreaseAnimations(state));
            body.Add(state.Positions[0]);
        }
        // Limb 0 runs commands 23..28 and loops; the head (limb 1) stays at command 4.
        Assert.Equal([23, 24, 25, 26, 27, 28, 23, 24], body);
        Assert.Equal(4, state.Positions[1]);
    }

    [Fact]
    public void LeftIsTheRightAnimationMirroredAroundThePoint()
    {
        var costume = EgoCostume();
        if (costume is null) return;
        var left = new LoomCostumeAnimationState();
        costume.DecodeAnimation(left, frame: 3, direction: 0);
        var cels = costume.Draw(left, 0);
        Assert.Equal("L0(-16,-43 24x45) L1(-6,-41 12x10)", Describe(cels));

        // Direction 0 draws animation 12 mirrored: its cels end at the actor's x instead of starting there.
        var unmirrored = costume.Draw(left, 1);
        Assert.Equal(cels.Select(cel => (-cel.Left - cel.Width, cel.Top)), unmirrored.Select(cel => (cel.Left, cel.Top)));
        Assert.Equal(unmirrored[0].Pixels[0], cels[0].Pixels[cels[0].Width - 1]);
    }

    [Fact]
    public void AnEmptyOrMissingAnimationChangesNothing()
    {
        var costume = EgoCostume();
        if (costume is null) return;
        var state = new LoomCostumeAnimationState();
        costume.DecodeAnimation(state, 3, 2);
        var before = state.Positions.ToArray();
        Assert.False(costume.DecodeAnimation(state, frame: 16, direction: 0)); // animation 64 > 63
        Assert.Equal(before, state.Positions);
    }

    [Fact]
    public void ComposeMapsThePaletteColourToEntryZeroAndSkipsTransparency()
    {
        var costume = EgoCostume();
        if (costume is null) return;
        var state = new LoomCostumeAnimationState();
        costume.DecodeAnimation(state, 3, 2);
        var cels = costume.Draw(state, 2);
        var target = new byte[64 * 64];
        Array.Fill(target, (byte)200);
        var written = costume.Compose(cels, target, 64, 64, 32, 60);
        Assert.True(written > 300);
        Assert.DoesNotContain(target, value => value == costume.PaletteColor);
        Assert.Contains(target, value => value == 0);
        // Pixels outside the cels keep the background.
        Assert.Equal(200, target[0]);

        // actorOps palette: entry 7 drawn as 15 changes the grey cloak to white.
        var palette = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        palette[7] = 15;
        var recoloured = new byte[64 * 64];
        costume.Compose(cels, recoloured, 64, 64, 32, 60, palette);
        Assert.DoesNotContain(recoloured, value => value == 7);
    }

    private static string Describe(IEnumerable<LoomCostumeCel> cels) =>
        string.Join(" ", cels.Select(c => $"L{c.Limb}({c.Left},{c.Top} {c.Width}x{c.Height})"));
}

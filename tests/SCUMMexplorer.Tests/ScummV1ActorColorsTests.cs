using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The actor colours of the classic v1 costumes come from the game's scripts (<see cref="ScummV1ActorColors"/>), not from a table keyed by costume number.
/// The pinned values were read from the instructions named in each assertion (14 October 2026); a test is skipped without the local game folder.
/// </summary>
public class ScummV1ActorColorsTests
{
    private static GameLibrary? Open(string folder)
    {
        var root = SupportMatrixTests.RepositoryRoot();
        foreach (var candidate in new[] { Path.Combine(root, "Games", folder), Path.Combine(root, "Games", "FUTURE", folder) })
            if (Directory.Exists(candidate)) return new GameFolderScanner().Scan(candidate);
        return null;
    }

    [Fact]
    public void ManiacMansionCostumesTakeTheEngineTablesOfTheActorThatWearsThem()
    {
        if (Open("mmansion ega en") is not { } game) return;
        var colors = ScummV1ActorColors.For(game)!;

        // No script gives any actor another costume (the only constant-free sub-opcode 4 sets costume 0), so actor n wears costume n.
        Assert.DoesNotContain(colors.Settings, s => s.IsCostume && s.Value is not (null or 0));
        var dave = colors.ColorOf(3);
        Assert.Equal((8, 6, ScummV1ColorBasis.InitialState), (dave.Skin, dave.Suit, dave.Basis));
        var fourth = colors.ColorOf(4);
        Assert.Equal((4, 9), (fourth.Skin, fourth.Suit));
        Assert.All(Enumerable.Range(1, 23), id => Assert.Equal(ScummV1ColorBasis.InitialState, colors.ColorOf(id).Basis));
    }

    [Fact]
    public void ZakMcKrackenCostumesTakeThePaletteTheScriptsSetNextToTheCostumeInstruction()
    {
        if (Open("zak ega en") is not { } game) return;
        var colors = ScummV1ActorColors.For(game)!;

        // Script 01 (boot): actorOps(1, 30, costume), palette 0 at @15 and palette 14 at @10C; the palette persists, so the last one in the span wins.
        Assert.Equal((14, ScummV1ColorBasis.Script), (colors.ColorOf(30).Suit, colors.ColorOf(30).Basis));
        // Zak's costume 1 is given to actor 1 by scripts that set no palette: the boot script's 14 is the estimate (labelled).
        Assert.Equal((14, ScummV1ColorBasis.Estimate), (colors.ColorOf(1).Suit, colors.ColorOf(1).Basis));
        // Room 08 entry script: costume 23 on actor 10, palette 9 right after; Room 19 entry script: palette 1 right before costume 22.
        Assert.Equal(9, colors.ColorOf(23).Suit);
        Assert.Equal(1, colors.ColorOf(22).Suit);
        // Room 38 entry script: the actor is a variable, the costume and the palette name the same variable.
        Assert.Equal((9, ScummV1ColorBasis.Script), (colors.ColorOf(21).Suit, colors.ColorOf(21).Basis));
        // Actor 5 never gets a palette, so its costumes stay black (palette 0 from ScummVM's initActor).
        Assert.Equal((0, ScummV1ColorBasis.Script), (colors.ColorOf(13).Suit, colors.ColorOf(13).Basis));
        // Costume 2 is only ever worn by actor 2 from the start.
        Assert.Equal(ScummV1ColorBasis.InitialState, colors.ColorOf(2).Basis);
        // Nothing decides costume 35 (no actor 35, no constant instruction): a labelled estimate.
        Assert.Equal(ScummV1ColorBasis.Estimate, colors.ColorOf(35).Basis);
    }
}

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
        var candidate = Path.Combine(TestGames.Root, folder);
        return Directory.Exists(candidate) ? new GameFolderScanner().Scan(candidate) : null;
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
        // Inline actor names formerly stopped decoding before these later costume/palette pairs.
        Assert.Equal((13, ScummV1ColorBasis.Script), (colors.ColorOf(1).Suit, colors.ColorOf(1).Basis));
        Assert.Contains(colors.Settings, s => s.Source == "Script 108" && s.Offset == 0xD8 && s.Actor == 1 && s.IsCostume && s.Value == 1);
        Assert.Contains(colors.Settings, s => s.Source == "Script 108" && s.Offset == 0xEC && s.Actor == 1 && !s.IsCostume && s.Value == 13);
        // Room 08 entry script: costume 23 on actor 10, palette 9 right after; Room 19 entry script: palette 1 right before costume 22.
        Assert.Equal(9, colors.ColorOf(23).Suit);
        Assert.Equal(1, colors.ColorOf(22).Suit);
        // Room 38 entry script: the actor is a variable, the costume and the palette name the same variable.
        Assert.Equal((9, ScummV1ColorBasis.Script), (colors.ColorOf(21).Suit, colors.ColorOf(21).Basis));
        // Script 50 sets actor 5's palette; whether it precedes costume 13 depends on the run.
        Assert.Equal((11, ScummV1ColorBasis.Estimate), (colors.ColorOf(13).Suit, colors.ColorOf(13).Basis));
        Assert.Contains(colors.Settings, s => s.Source == "Script 50" && s.Offset == 0x1D && s.Actor == 5 && !s.IsCostume && s.Value == 11);
        Assert.Equal((2, ScummV1ColorBasis.Script), (colors.ColorOf(2).Suit, colors.ColorOf(2).Basis));
        Assert.Contains(colors.Settings, s => s.Source == "Script 102" && s.Offset == 0x164 && s.Actor == 10 && s.IsCostume && s.Value == 2);
        Assert.Contains(colors.Settings, s => s.Source == "Script 102" && s.Offset == 0x168 && s.Actor == 10 && !s.IsCostume && s.Value == 2);
        Assert.Equal((1, ScummV1ColorBasis.Script), (colors.ColorOf(35).Suit, colors.ColorOf(35).Basis));
        Assert.Contains(colors.Settings, s => s.Source == "Room 44 / Entry script" && s.Offset == 0x99 && s.Actor == 12 && s.IsCostume && s.Value == 35);
        Assert.Contains(colors.Settings, s => s.Source == "Room 44 / Entry script" && s.Offset == 0x90 && s.Actor == 12 && !s.IsCostume && s.Value == 1);
    }
}

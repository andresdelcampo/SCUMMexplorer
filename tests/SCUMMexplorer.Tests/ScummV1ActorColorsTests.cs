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
    public void ZakContextDependentCostumesRetainEstimatesAndExactVariableOperands()
    {
        if (Open("zak ega en") is not { } game) return;
        var colors = ScummV1ActorColors.Read(game);
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32 },
            Enumerable.Range(1, 35).Where(id => colors.ColorOf(id).Basis == ScummV1ColorBasis.Estimate));
        var dynamicColor = Assert.Single(colors.Settings, s => s.Source == "Script 108" && s.Offset == 0xFC);
        Assert.Null(dynamicColor.Value);
        Assert.Equal("var[0x0C4]", dynamicColor.ValueOperand);
        // Costume 7 is shared by two differently coloured actors. A nearest constant on actor 3 cannot decide actor 4's result.
        Assert.Equal(new[] { 3, 4 }, colors.ActorsOf(7));
        Assert.Contains("actor 4:", colors.ColorOf(7).Evidence);
        Assert.Contains("var[0x0C4] at Script 108 @00FC", colors.ColorOf(7).Evidence);
        // Costume 6 is both an engine-initial costume on actor 6 and an alternate costume on actor 2.
        Assert.Contains("actor 2: constants [4]", colors.ColorOf(6).Evidence);
        Assert.Contains("actor 6: constants [6]", colors.ColorOf(6).Evidence);
    }

    [Fact]
    public void ZakSourceEntryChangesCostumeWithoutReplacingItsReachedPalette()
    {
        if (Open("zak ega en") is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        var before = session.Actors.Single(a => a.Number == 1).Look;
        Assert.Equal(13, before.Palette[1]); // Script 108 +00EC
        // Explicit source-state preview; this is not a claim that the route or prerequisites were played.
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 22));
        var after = session.Actors.Single(a => a.Number == 1).Look;
        Assert.Equal(5, after.Costume); // Room 22 entry +0010, Zak branch (other branch assigns Annie costume 6).
        Assert.Equal(before.Palette, after.Palette);
        Assert.Equal(ScummV1ColorBasis.Estimate, ScummV1ActorColors.Read(game).ColorOf(5).Basis);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(41)]
    public void ZakUnpreparedWaterPreviewDoesNotInventThePaletteOfTheMissingPriorScene(int room)
    {
        if (Open("zak ega en") is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        var before = session.Actors.Single(a => a.Number == 5).Look;
        Assert.Equal(0, before.Palette[5]);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        var after = session.Actors.Single(a => a.Number == 5).Look;
        Assert.Equal(13, after.Costume);
        Assert.Equal(before.Palette, after.Palette);
        // Script 50 +001D would establish 11, but that scene was not played. The global card is still an estimate.
        Assert.Equal((11, ScummV1ColorBasis.Estimate),
            (ScummV1ActorColors.Read(game).ColorOf(13).Suit, ScummV1ActorColors.Read(game).ColorOf(13).Basis));
    }

    [Fact]
    public void ZakOpeningAssignsTheBossBeforeTheLaterBootPaletteWrite()
    {
        if (Open("zak ega en") is not { } game) return;
        var earlyData = new ScummV1DataSource(game);
        var early = new ScummGameSession(game, earlyData.ReadIndexedObjects(), data: earlyData, boot: false);
        // Drive the same engine boot entry one frame at a time so the stop is the reached assignment, not an arbitrary sampled frame.
        typeof(ScummSession).GetMethod("RunBootScript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(early, null);
        for (var frame = 0; frame < 3000 && early.Actors.Single(a => a.Number == 10).Look.Costume != 32; frame++) early.AdvanceFrame();
        var bossLook = early.Actors.Single(a => a.Number == 10).Look;
        Assert.Equal(32, bossLook.Costume);
        Assert.Equal(0, bossLook.Palette[10]);
        var reached = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(reached.Add, site => site.Operation == "actorOps");
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        Assert.False(session.BootReport!.HitFrameLimit);
        var boss = Assert.Single(reached, o => o.Site is { Stage: "instruction", Script: 127, Offset: 2 });
        Assert.Equal("0x0A, 0x20, sub=0x04", boss.Site!.Operands);
        var laterPalette = Assert.Single(reached, o => o.Site is { Stage: "instruction", Script: 1, Offset: 0x108 });
        Assert.True(boss.Sequence < laterPalette.Sequence);
        Assert.DoesNotContain(reached.Where(o => o.Sequence < boss.Sequence), o =>
            o.Site is { Stage: "instruction" } site && site.Operands!.StartsWith("0x0A,") && site.Operands.Contains("sub=0x02"));
        Assert.Equal(7, session.Actors.Single(a => a.Number == 10).Look.Palette[10]);
        Assert.Equal((7, ScummV1ColorBasis.Estimate),
            (ScummV1ActorColors.Read(game).ColorOf(32).Suit, ScummV1ActorColors.Read(game).ColorOf(32).Basis));
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

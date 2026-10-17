using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A claim in the support matrix (<see cref="GameSupport.Families"/>) must be true of real data. For every family whose game folder is present
/// under <c>Games/</c>, the folder must be recognised as that family and each feature the registry claims at least partly must show up:
/// rooms, costumes, sounds and scripts offered, texts extracted. A family without a folder is skipped (the check is not run, not passed).
/// Only the cheap, mechanical level of each feature is checked here; ✔ against ◐ is a judgement the notes under the matrix state.
/// </summary>
public class SupportConformanceTests
{
    // One fixture folder per family, relative to Games/.
    private static readonly (GameFamily Family, string Folder)[] Fixtures =
    [
        (GameFamily.LoomEgaReference, "loom ega mt32 en"),
        (GameFamily.LoomEga, "loom ega es"),
        (GameFamily.LoomEgaDemo, "loom demo short ega en"),
        (GameFamily.LoomCd, "loom vga en"),
        (GameFamily.Indy3Ega, "indy3 ega en"),
        (GameFamily.Indy3Vga, "indy3 vga en"),
        (GameFamily.MonkeyFloppy, "monkey1 ega en"),
        (GameFamily.MonkeyCd, "monkey1 vga en"),
        (GameFamily.Monkey2, "mi2 talkie en"),
        (GameFamily.Indy4, "indy4 vga en"),
        (GameFamily.ManiacEnhanced, "mmansionE ega es"),
        (GameFamily.ManiacClassic, "mmansion ega en"),
        (GameFamily.ZakEnhanced, "zakE ega en"),
        (GameFamily.ZakClassic, "zak ega en")
    ];

    public static IEnumerable<object[]> Families() => Fixtures.Select(f => new object[] { f.Family });

    [Fact]
    public void EveryMatrixFamilyHasAFixture()
    {
        var withoutFixture = GameSupport.Families.Where(row => row.InMatrix).Select(row => row.Family).Except(Fixtures.Select(f => f.Family));
        Assert.Empty(withoutFixture);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void FamilyIsRecognisedAndItsClaimsHold(GameFamily family)
    {
        var folder = Locate(Fixtures.Single(f => f.Family == family).Folder);
        if (folder is null) return; // no local copy of this game

        var game = new GameFolderScanner().Scan(folder);
        var support = GameSupport.For(game);
        Assert.Equal(family, support.Family);

        if (support[SupportFeature.Rooms] != SupportLevel.None) Assert.NotEmpty(game.Rooms);
        if (support[SupportFeature.Costumes] != SupportLevel.None) Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        if (support[SupportFeature.Sound] != SupportLevel.None) Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Sound);
        if (support[SupportFeature.Scripts] != SupportLevel.None) Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Script);
        if (support[SupportFeature.Texts] != SupportLevel.None) Assert.NotEmpty(new LoomTextExtractor().Extract(game).Entries);
        // Play outside LOOM (S6): the game is played through its own verbs (REQUIREMENTS §7.1); ScummIndy3PlayTests plays the acceptance route of R-7.18.
        if (support[SupportFeature.Play] != SupportLevel.None && game.Id is not (GameId.Loom or GameId.LoomVga))
            Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
        // Objects ✔ outside LOOM needs the first visit (S5) and click responses (S6).
        if (support[SupportFeature.Objects] == SupportLevel.Full && game.Id is not (GameId.Loom or GameId.LoomVga))
            Assert.True(ScummProfile.For(game).HasFirstVisitScenes && ScummProfile.For(game).PlaysThroughVerbs);
        // Cutscenes outside LOOM (S7): ◐ only, a list derived from the game's scripts by the engine's scan, played in the play session; a hand-validated
        // catalog (✔) exists only for the LOOM reference (R-8.18). ScummIndy3CutsceneTests runs three scenes from a fresh start.
        if (support[SupportFeature.Cutscenes] != SupportLevel.None && game.Id is not (GameId.Loom or GameId.LoomVga))
        {
            Assert.Equal(SupportLevel.Partial, support[SupportFeature.Cutscenes]);
            Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
            Assert.NotEmpty(ScummCutsceneIndex.Group(ScummCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value), game: game)));
        }
    }

    private static string? Locate(string folder)
    {
        // The games sit directly under Games/.
        var path = Path.Combine(TestGames.Root, folder);
        return Directory.Exists(path) ? path : null;
    }
}

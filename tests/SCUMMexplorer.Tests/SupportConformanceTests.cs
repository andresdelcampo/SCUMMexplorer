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
    }

    private static string? Locate(string folder)
    {
        // The games sit directly under Games/ (they were once split into FUTURE/ and ALTERNATIVE LOOMS/ subfolders; those are still tried).
        var games = Path.Combine(SupportMatrixTests.RepositoryRoot(), "Games");
        foreach (var parent in new[] { "", "FUTURE", "ALTERNATIVE LOOMS" })
        {
            var path = Path.Combine(games, parent, folder);
            if (Directory.Exists(path)) return path;
        }
        return null;
    }
}

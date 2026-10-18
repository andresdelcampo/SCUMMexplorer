using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class ScummOldEditionTests
{
    [Theory]
    [InlineData("mmansion ega en", "English", GameFamily.ManiacClassic)]
    [InlineData("mmansionE ega es", "Spanish", GameFamily.ManiacEnhanced)]
    [InlineData("zak ega en", "English", GameFamily.ZakClassic)]
    [InlineData("zakE ega en", "English", GameFamily.ZakEnhanced)]
    public void ReviewedOldEditionsKeepTheirFamilyAndIdentifyFontLanguage(string fixture, string language, GameFamily family)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(family, GameSupport.FamilyOf(game));
        Assert.StartsWith(language + ",", game.Edition);
        // The edition detector receives filenames and bytes, not the folder label.
        var paths = Directory.GetFiles(folder, "*.LFL").ToDictionary(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(game.Edition, KnownGameFiles.IdentifyEdition(paths)?.Label);
    }
}

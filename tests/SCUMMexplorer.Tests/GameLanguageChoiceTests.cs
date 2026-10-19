using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Choosing the language of a game whose data holds several (19 October 2026; REQUIREMENTS R-3.16): discovery lists them from the data, opening one reads that data
/// set, and a game with one language offers no choice. Record: <c>.verification/language-choice-20261019/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class GameLanguageChoiceTests
{
    private static string? MonkeyCd()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? path : null;
    }

    [Fact]
    public void MonkeyCdListsTheFiveLanguageFoldersOfTheDisc()
    {
        if (MonkeyCd() is not { } folder) return;
        var expected = new[] { ("ENGLISH", "English"), ("FRENCH", "French"), ("GERMAN", "German"), ("ITALIAN", "Italian"), ("SPANISH", "Spanish") };
        Assert.Equal(expected, GameFolderScanner.ListLanguages(folder).Select(l => (l.Key, l.Label)).OrderBy(l => l.Key));
        // The default stays as before: the ENGLISH folder wins the tie of equal room counts.
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(expected, game.Languages.Select(l => (l.Key, l.Label)).OrderBy(l => l.Key));
        Assert.Equal("ENGLISH", game.Language);
        Assert.EndsWith("|ENGLISH", game.Key);
        Assert.Contains("an ENGLISH folder winning a tie", game.DetectionNote);
    }

    [Fact]
    public void OpeningSpanishReadsTheSpanishTexts()
    {
        if (MonkeyCd() is not { } folder) return;
        var spanish = new GameFolderScanner { DiscFolder = "SPANISH" }.Scan(folder);
        Assert.Equal("SPANISH", spanish.Language);
        Assert.Contains("is read (chosen)", spanish.DetectionNote);
        Assert.NotEqual(new GameFolderScanner().Scan(folder).Key, spanish.Key);
        var texts = new LoomTextExtractor().Extract(spanish).Entries.Select(e => e.Text).ToList();
        Assert.Contains(texts, t => t.Contains("Re-eliga a la Gobernadora Marley.", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("cartel", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("Re-elect Governor Marley.", StringComparison.Ordinal));
        // A worker's own reader of the same data set reads the same language.
        Assert.Equal("SPANISH", GameFolderScanner.Reopen(spanish).Language);
    }

    [Fact]
    public void ALanguageTheDiscLacksFallsBackToTheDefault()
    {
        if (MonkeyCd() is not { } folder) return;
        var game = new GameFolderScanner { DiscFolder = "KLINGON" }.Scan(folder);
        Assert.Equal("ENGLISH", game.Language);
        Assert.Contains("\"KLINGON\" is not on this disc", game.DetectionNote);
    }

    [Fact]
    public void AGameWithOneLanguageOffersNoChoice()
    {
        var loom = TestGames.LoomRelative;
        if (!File.Exists(Path.Combine(loom, "00.LFL"))) return;
        Assert.Empty(GameFolderScanner.ListLanguages(loom));
        var game = new GameFolderScanner().Scan(loom);
        Assert.Empty(game.Languages);
        Assert.Null(game.Language);
        Assert.Equal(game.FolderPath, game.Key);
        // The Indy 4 disc holds the game beside demos: a demo with as many rooms but other files is not a language.
        var indy4 = Path.Combine(TestGames.Root, "indy4 vga en");
        if (Directory.Exists(indy4)) Assert.Empty(GameFolderScanner.ListLanguages(indy4));
    }
}



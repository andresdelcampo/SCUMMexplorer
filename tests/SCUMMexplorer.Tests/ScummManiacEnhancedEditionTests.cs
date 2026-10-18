using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedEditionTests
{
    [Fact]
    public void ReviewedNativeFontRendersEveryGlyphWithoutLanguageSubstitution()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var native = ScummV2NativeFont.TryLoad(folder);
        Assert.NotNull(native);
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        var font = session.PlayCharset();
        Assert.NotNull(font);
        for (var glyph = 0; glyph < 128; glyph++)
        {
            var pixels = new byte[64];
            font.DrawPlainLine(pixels, 8, 0, [(byte)glyph], 0, 0, 1);
            var expected = Enumerable.Range(0, 64)
                .Select(p => (byte)(glyph != '@' && (native[glyph * 8 + p / 8] & (128 >> (p % 8))) != 0 ? 1 : 0)).ToArray();
            // '@' is a native text marker suppressed by the text renderer.
            Assert.Equal(expected, pixels);
        }
    }

    [Fact]
    public void NativeFontRequiresCompleteGlyphTableAndEmptyMeansExistingFallback()
    {
        Assert.Throws<ArgumentException>(() => new ScummV2Charset(nativeFont: new byte[1023]));
        var oldFont = new ScummV2Charset(2, "es");
        var fallback = new ScummV2Charset(2, "es", nativeFont: default);
        for (var glyph = 0; glyph < 128; glyph++)
        {
            var expected = new byte[64];
            var actual = new byte[64];
            oldFont.DrawPlainLine(expected, 8, 0, [(byte)glyph], 0, 0, 1);
            fallback.DrawPlainLine(actual, 8, 0, [(byte)glyph], 0, 0, 1);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void NativeLabelMappingRequiresTheReviewedExecutableRatherThanALanguageLabel()
    {
        var folder=Path.Combine(Path.GetTempPath(),"scumm-edition-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var game=new GameLibrary("Maniac Mansion",folder,ScummVersion.V2,"DOS",[],"",Edition:"Spanish");
            Assert.Null(ManiacPreviewText.For(game));
            Assert.Null(ScummV2NativeFont.TryLoad(folder));
            File.WriteAllBytes(Path.Combine(folder,"MANIAC.EXE"),new byte[69084]);
            Assert.Null(ManiacPreviewText.For(game));
            Assert.Null(ScummV2NativeFont.TryLoad(folder));
        }
        finally
        {
            File.Delete(Path.Combine(folder,"MANIAC.EXE"));
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void ReviewedEnhancedFixtureKeepsOwnVersionEditionAndReadableSlots()
    {
        var folder=Path.Combine(TestGames.Root,"mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game=new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.ManiacEnhanced,GameSupport.FamilyOf(game));
        Assert.Equal(ScummVersion.V2,game.Version);
        Assert.StartsWith("Spanish",game.Edition);
        var decode=ManiacPreviewText.For(game);
        Assert.NotNull(decode);
        Assert.Equal("Qué es el teléfono",decode("Qu] es el tel]fono"));
        Assert.Equal("¡áí¿èéñóúü",decode("<=>[\\]{|}~"));
        var font=ScummV2NativeFont.TryLoad(folder);
        Assert.NotNull(font);
        Assert.Equal(1024,font.Length);
        Assert.Equal("098B285F43AEC7C97CA2C05D07D31365D2C8371304645AFFC20FEBCE7B12453D",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(font)));
        // Exact native é rows, and the first inventory-arrow glyph whose encoded location
        // precedes the contiguous ASCII section. A direct 1024-byte executable slice fails.
        Assert.Equal(Convert.FromHexString("08103C667E603C00"),font.AsSpan(93*8,8).ToArray());
        Assert.Equal(Convert.FromHexString("0103060C183E0300"),font.AsSpan(8,8).ToArray());
    }
}

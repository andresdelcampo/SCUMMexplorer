using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class ScummZakEnhancedEditionTests
{
    [Fact]
    public void AvailableEditionKeepsV2DialectWhenDisplayEditionLabelChanges()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.ZakEnhanced, GameSupport.FamilyOf(game));
        Assert.Equal(ScummScriptDialect.V2, ScummProfile.For(game).Dialect);
        // Synthetic metadata, not another language fixture: human labels must not select opcode widths.
        Assert.Equal(ScummProfile.For(game).Dialect,
            ScummProfile.For(game with { Edition = "unreviewed language label" }).Dialect);
        var data = ScummDataSources.For(game);
        Assert.Equal(58, game.Rooms.Count());
        Assert.Contains(data.GlobalScript(140)!.Instructions,
            i => i.Operation == "print" && i.Operands.Contains("Meanwhile, back in the secret room^", StringComparison.Ordinal));
        static byte[] Apostrophe(int version) {
            var pixels = new byte[64];
            new ScummV2Charset(version).DrawPlainLine(pixels,8,0,[39],0,0,7);
            return pixels;
        }
        Assert.NotEqual(Apostrophe(1), Apostrophe(2));
    }
}

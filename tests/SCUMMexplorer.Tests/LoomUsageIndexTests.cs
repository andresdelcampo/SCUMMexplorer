using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomUsageIndexTests
{
    private static string Folder => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void UsedByListsTheCodeThatStartsAScript()
    {
        var game = new GameFolderScanner().Scan(Folder);
        var glossary = LoomGlossary.Load(game);
        var uses = LoomUsageIndex.Build(game).UsedBy(LoomUsageTarget.Script, 91);
        var use = Assert.Single(uses, u => u.ObjectId == 643);
        Assert.Equal("Room 43 object 643 verb 0x3A", use.Where);
        var text = LoomUsageIndex.Describe(use, glossary, v => v == 0x3A ? "Draft: Opening" : null);
        Assert.Contains("“jaula”", text);
        Assert.EndsWith("starts it", text);
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LflResourceChainTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void ChainFollowsEachResourceLength()
    {
        // Two resources of 6 and 4 bytes, stored XOR 0xFF.
        byte[] plain = [6, 0, (byte)'S', (byte)'C', 1, 2, 4, 0, (byte)'S', (byte)'O'];
        var chain = LflResourceChain.Read(plain.Select(value => (byte)(value ^ 0xFF)).ToArray());

        Assert.Equal([new LflChainResource(0, 6), new LflChainResource(6, 4)], chain);
    }

    [Fact]
    public void EntriesInsideAnotherResourceAreLeftoversWithTheirOwner()
    {
        var folder = Path.Combine(Path.GetTempPath(), "scumm-chain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            byte[] plain = [6, 0, (byte)'S', (byte)'C', 1, 2, 4, 0, (byte)'S', (byte)'O'];
            File.WriteAllBytes(Path.Combine(folder, "05.LFL"), plain.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var owner = new GameResource("Script 10", ResourceKind.Script, "", 10, ContainerFileNumber: 5, Offset: 0);
            var second = new GameResource("Sound 3", ResourceKind.Sound, "", 3, ContainerFileNumber: 5, Offset: 6);
            var leftover = new GameResource("Script 11", ResourceKind.Script, "", 11, ContainerFileNumber: 5, Offset: 2);
            var game = new GameLibrary("Test", folder, ScummVersion.V3, "DOS", [owner, second, leftover], "");
            var chains = new LflResourceChain();

            Assert.Null(chains.FindLeftover(game, owner));
            Assert.Null(chains.FindLeftover(game, second));
            var found = Assert.IsType<LflLeftoverIndexEntry>(chains.FindLeftover(game, leftover));
            Assert.Same(owner, found.Owner);
            Assert.Equal("Script 11: leftover index entry (points inside Script 10); 05.LFL 0x0002 is 2 bytes into the resource at 0x0000.",
                found.Describe());
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void GameTextsListTheFourLeftoverScriptsSeparately()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);

        var extraction = new LoomTextExtractor().Extract(game);

        Assert.Equal([0, 51, 55, 116], extraction.LeftoverEntries.Select(item => item.Entry.ResourceId!.Value).Order());
        Assert.Equal(["Costume 01", "Script 49", "Room 18", "Script 114"],
            extraction.LeftoverEntries.OrderBy(item => item.Entry.ResourceId).Select(item => item.OwnerLabel));
        // They are no longer counted or reported as decode failures.
        Assert.DoesNotContain(extraction.Diagnostics, line =>
            extraction.LeftoverEntries.Any(item => line.StartsWith(item.Entry.Name + ":", StringComparison.Ordinal)));
        Assert.Equal(0, extraction.ResourcesSkipped);
    }
}

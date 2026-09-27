using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The LOOM EGA index lists some script entries whose offsets do not start a
/// resource. ScummVM reads the same directory (<c>ScummEngine_v3old::readResTypeList</c>)
/// and loads a resource by reading a 16-bit size at that offset
/// (<c>ScummEngine::loadResource</c>, <c>GF_OLD_BUNDLE</c>), as our reader does, so
/// these entries are left over in the index rather than misread.
/// </summary>
public sealed class LoomScriptIndexTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void ScriptsThatFailToDecodePointInsideAnotherIndexedResource()
    {
        var indexPath = Path.Combine(Folder, "00.LFL");
        if (!File.Exists(indexPath)) return;
        var index = new LflIndexReader().Read(indexPath);
        var game = new GameFolderScanner().Scan(Folder);
        var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));

        var failing = new List<int>();
        foreach (var script in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            try { reader.Decode(script); }
            catch (InvalidDataException) { failing.Add(script.ResourceId!.Value); }
        }
        Assert.Equal([0, 51, 55, 116], failing.Order());

        foreach (var entry in index.Where(item => item.Kind == ResourceKind.Script))
        {
            var chain = ResourceChain(entry.ContainerFileNumber);
            if (!failing.Contains(entry.ResourceId))
            {
                Assert.Contains(chain, item => item.Start == entry.Offset);
                continue;
            }
            // Off the chain, and strictly inside a resource another index entry owns.
            Assert.DoesNotContain(chain, item => item.Start == entry.Offset);
            var owner = Assert.Single(chain, item => item.Start < entry.Offset && entry.Offset < item.Start + item.Length);
            Assert.Contains(index, other => other.ContainerFileNumber == entry.ContainerFileNumber &&
                                            other.Offset == owner.Start && other != entry);
        }
    }

    private static List<(int Start, int Length)> ResourceChain(int fileNumber)
    {
        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(Path.Combine(Folder, $"{fileNumber:00}.LFL")));
        var chain = new List<(int, int)>();
        for (var position = 0; position + 2 <= data.Length;)
        {
            var length = data[position] | data[position + 1] << 8;
            if (length < 4) break;
            chain.Add((position, length));
            position += length;
        }
        return chain;
    }
}

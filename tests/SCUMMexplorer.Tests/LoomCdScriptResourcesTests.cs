using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomCdScriptResourcesTests
{
    [Fact]
    public void CdScriptResourcesKeepGlobalAndRoomLocalNamespacesSeparate()
    {
        var game = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom vga en")));
        var scripts = game.Resources.Where(r => r.Kind == ResourceKind.Script).ToArray();
        Assert.Contains(scripts, r => r.ResourceId == 17 && r.ScriptRoom is null);
        Assert.Contains(scripts, r => r.ResourceId == 200 && r.ScriptRoom == 2);
        Assert.Contains(scripts, r => r.ResourceId == 200 && r.ScriptRoom == 3);
        var data = ScummDataSources.For(game);
        Assert.Equal(200, data.GlobalScriptCount);
        foreach (var r in scripts.Where(r => r.ResourceId is 17 or 200))
        {
            var script = (r.ScriptRoom is int room ? data.RoomLocalScript(room, r.ResourceId!.Value) : data.GlobalScript(r.ResourceId!.Value))!;
            Assert.NotEmpty(script.Instructions);
            Assert.Contains("DISK01.LEC", script.Format(false));
            Assert.Equal(script.ResourceOffset + (r.ScriptRoom is null ? 6 : 7), script.CodeFileOffset);
        }
    }
}

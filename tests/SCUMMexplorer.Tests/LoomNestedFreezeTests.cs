using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomNestedFreezeTests
{
    [Fact]
    public void AChildThatFreezesItsCallerSuspendsTheInstructionAfterItsStart()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        var parent = new LoomScriptDisassembly(250, 1, 0, 11, 11, [
            new(0, 0x0A, "startScript", "0x02, []", "0A 02 FF"),
            new(3, 0x1A, "move", "var[0x0F1] = 0x1234", "1A F1 00 34 12"),
            new(8, 0, "stopObjectCode", "", "00")], null, null, [], []);
        var run = session.RunTopLevelScript(parent);
        Assert.Equal(3, run.ResumeOffset);
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["var[0x0F1]"]);
        session.AdvanceFrame();
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["var[0x0F1]"]);
        session.RunTopLevelScript(new LoomScriptReader().Decode(game.Resources.Single(r => r.ResourceId == 3 && r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script)));
        session.AdvanceFrame();
        Assert.Equal(0x1234, session.Snapshot.KnownGlobalVariables["var[0x0F1]"]);
    }
}

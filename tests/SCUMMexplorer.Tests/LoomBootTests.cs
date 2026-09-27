using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The boot: script 001 loads the intro screen (Room 69), waits for bit 0xD6B,
/// sets the hooks, plays the opening cutscene (script 044) and chains to the
/// input loop (script 006).
/// </summary>
public sealed class LoomBootTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void BootRunsPastTheIntroScreenAndTheOpeningCutscene()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        var snapshot = session.Snapshot;
        var variables = snapshot.KnownGlobalVariables;

        // The opening cutscene ends in Room 02 (script 044 +0x0536), and script 001 chains on (+0x0249).
        Assert.Equal(2, snapshot.RoomId);
        Assert.InRange(session.BootFrames, 1, LoomExplorationSession.BootFrameLimit - 1);
        Assert.Contains("Script 01 chains to script 6", session.SessionTrace);
        Assert.Contains("Script 01 continues at +0x0162 in Room 69 after loadRoom", session.SessionTrace);
        Assert.Contains("Script 44 continues at +0x03BC in Room 53 after loadRoom", session.SessionTrace);
        Assert.False(session.InCutscene);
        Assert.Null(session.LastAction);

        // Values script 001 sets after the intro wait (+0x016B to +0x01B1) and at its end (+0x0240).
        Assert.Equal((ushort)1, variables["bit[0xD6B]"]);
        Assert.Equal((ushort)5, variables["var[0x020]"]);
        Assert.Equal((ushort)14, variables["var[0x01D]"]);
        Assert.Equal((ushort)15, variables["var[0x01E]"]);
        Assert.Equal((ushort)2, variables["var[0x023]"]);
        Assert.Equal((ushort)3, variables["var[0x024]"]);
        Assert.Equal((ushort)1, variables["var[0x001]"]);
        Assert.Equal((ushort)1, variables["bit[0xD83]"]);
        Assert.Equal((ushort)0, variables["bit[0xD6E]"]);

        // The intro screen's answer is an assumption, and it says which code it stands for.
        Assert.Contains(snapshot.Uncertainties, line => line.StartsWith("Player input assumed: script 001 +0x0166 waits for bit[0xD6B], which only Room 69 local script 205 sets (+0x0048)", StringComparison.Ordinal) &&
                                                        line.Contains("local 203 +0x01D8, local 203 +0x0241", StringComparison.Ordinal));
    }

    [Fact]
    public void BootCanStopAtTheIntroScreenLoad()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")),
            completeBoot: false);

        Assert.Null(session.Snapshot.RoomId);
        Assert.Equal((ushort)0, session.Snapshot.KnownGlobalVariables["var[0x023]"]);
        Assert.Contains(session.Snapshot.Uncertainties, line => line.Contains("room transition awaits", StringComparison.Ordinal));
    }
}

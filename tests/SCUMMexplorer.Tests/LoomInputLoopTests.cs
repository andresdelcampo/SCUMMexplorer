using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Script 006, the input loop: it reads the virtual mouse (var[0x014], var[0x015]),
/// calls findObject at +0x0096 and idles when nothing is under the pointer.
/// </summary>
public sealed class LoomInputLoopTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void NoRoomObjectReachesTheRowBelowThePicture()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var directory = new ScummRoomObjectDirectory();
        foreach (var room in game.Rooms)
        {
            var bytes = File.ReadAllBytes(room.FilePath);
            var height = (bytes[6] ^ 0xFF) | ((bytes[7] ^ 0xFF) << 8);
            foreach (var record in directory.Decode(bytes))
                Assert.True(record.Y + record.Height <= height,
                    $"Room {room.ResourceId:00} object {record.Id} reaches y {record.Y + record.Height} of {height}");
        }
    }

    [Fact]
    public void TheInputLoopFindsNoObjectUnderTheAssumedPointerAndIdles()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        // The pointer rests just below Room 02's picture, under the camera (800).
        var variables = session.Snapshot.KnownGlobalVariables;
        Assert.Equal((ushort)800, variables["var[0x014]"]);
        Assert.Equal((ushort)144, variables["var[0x015]"]);
        Assert.Contains(session.Snapshot.Uncertainties, item =>
            item.Contains("findObject at +0x0096 reads the virtual mouse: assumed input", StringComparison.Ordinal));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => item.Contains("'findObject' is not modeled", StringComparison.Ordinal));

        // Room 03 object 162 covers (0, 0); the loop still finds nothing, frame after frame.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        session.AdvanceFrame();
        session.AdvanceFrame();
        Assert.Equal((ushort)144, session.Snapshot.KnownGlobalVariables["var[0x015]"]);
        Assert.Contains(session.Snapshot.Uncertainties, item =>
            item.StartsWith("Script 06 ", StringComparison.Ordinal) && item.Contains("at (160, 144)", StringComparison.Ordinal));
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["var[0x067]"]);
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The Room 03 ↔ Room 04 route on the real LOOM data, run by the bounded
/// session from its boot state. Object verbs are activated directly, so this is
/// an exploration action on a state preview, not a played route.
/// </summary>
public sealed class LoomRoom03ExitTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void Object162ReachesRoom04AndObject167LeadsBack()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        // The English release has no "full animation" bit (the Spanish script 1 sets 0xD62 at +0x015B, so its entry skips startScript 0x2A): the entry starts it.
        Assert.Contains(session.LastVisitEvents, item =>
            item.Kind == ScummScriptEventKind.ScriptStarted && item.TargetId == 0x2A);

        var action = session.ActivateObjectVerb(162, 0x36);
        Assert.Null(action.ReachedRoomLoad);

        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(4, session.Snapshot.RoomId);
        var load = session.LastAction?.ReachedRoomLoad;
        Assert.Equal(("Room 03 local script 200", 0x0008, "loadRoomWithEgo"), (load?.SourceLabel, load?.Offset, load?.Operation));
        Assert.Contains(session.SessionTrace, line => line.StartsWith(
            "stopScript 42 at Room 03 local script 200 +0x0006: ", StringComparison.Ordinal));

        session.ActivateObjectVerb(167, 0x38);
        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(3, session.Snapshot.RoomId);
        Assert.Equal((4, 167, 3), (session.LastAction?.SourceRoomId, session.LastAction?.ObjectId, session.LastAction?.DestinationRoomId));
    }
}

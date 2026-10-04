using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// DemoProbe <folder> play <frames>: a fresh session, then frames; logs every room change, the pending-script count and the uncertainties.
static class PlayDump
{
    public static void Run(string folder, int frames)
    {
        var game = new GameFolderScanner().Scan(folder);
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
        var session = new LoomExplorationSession(game, snapshot);
        var room = session.Snapshot.RoomId;
        Console.WriteLine($"frame 0: room {room}, pending {session.Snapshot.PendingScripts}, in cutscene {session.InCutscene}");
        for (var frame = 1; frame <= frames; frame++)
        {
            try { session.AdvanceFrame(); }
            catch (Exception ex) { Console.WriteLine($"frame {frame}: {ex.GetType().Name}: {ex.Message}"); break; }
            var now = session.Snapshot.RoomId;
            if (now != room)
            {
                Console.WriteLine($"frame {frame}: room {room} -> {now}, pending {session.Snapshot.PendingScripts}");
                room = now;
            }
            if (frame % 500 == 0)
                Console.WriteLine($"frame {frame}: room {room}, pending {session.Snapshot.PendingScripts}, var[0xE0] {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0E0]")}");
        }
        foreach (var note in session.Snapshot.Uncertainties) Console.WriteLine("uncertainty: " + note);
    }
}

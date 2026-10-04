using System.Collections.Concurrent;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// How the ego looks in each room of a LOOM demo, read from the demo's own preview: the costume he has on the frame the
/// preview first enters the room. The full game's script 017 (the ego's costume size) is not in a demo, so a catalog
/// visit has no rule for it; the preview is the demo's own evidence of which look belongs to which room (Room 03 shows the
/// smaller Bobbin, Room 16 the ordinary one).
/// </summary>
internal static class LoomDemoLooks
{
    private const int FrameLimit = 12000;
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<int, int>> Cache = new();

    /// <summary>Room number to the ego's costume when the preview first enters it (rooms the preview never enters are absent).</summary>
    public static IReadOnlyDictionary<int, int> EgoCostumes(GameLibrary game) => Cache.GetOrAdd(game.FolderPath, _ => Read(game));

    private static IReadOnlyDictionary<int, int> Read(GameLibrary game)
    {
        var looks = new Dictionary<int, int>();
        try
        {
            var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
            var session = new LoomExplorationSession(game, snapshot, demoPreview: true);
            var room = session.Snapshot.RoomId;
            var starts = 0;
            void Record()
            {
                if (session.Snapshot.RoomId is int now && session.Actors.FirstOrDefault(a => a.Number == 1)?.Look.Costume is int costume)
                    looks.TryAdd(now, costume);
            }
            Record();
            // One round of the preview: until it starts over in Room 01.
            for (var frame = 0; frame < FrameLimit && starts < 2; frame++)
            {
                session.AdvanceFrame();
                var now = session.Snapshot.RoomId;
                if (now == room) continue;
                room = now;
                Record();
                if (now == 1) starts++;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException) { }
        return looks;
    }
}

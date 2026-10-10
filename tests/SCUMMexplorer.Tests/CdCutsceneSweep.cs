using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>Plays every derived CD cutscene headless from its stated start, with and without Escape (env CD_SWEEP_OUT); does nothing without it.</summary>
public sealed class CdCutsceneSweep
{
    private sealed record Run(int Frames, bool Skippable, int Lines, int Rooms, int Voices, bool Began, string? Error);

    private static Run Play(GameLibrary game, LoomCutsceneGroup group, bool escape, LoomCdSceneSetupPlanner planner)
    {
        var t = group.First;
        var frames = 0; var skippable = false; var rooms = 0; var began = false;
        try
        {
            var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
            var room = game.Rooms.First(r => r.ResourceId == t.Room);
            var setup = planner.Derive(t);
            var useSetup = setup is { Plays: true, HasSteps: true };
            if (useSetup) LoomCdSceneSetupPlanner.ApplyBefore(session, game, setup.Before);
            var earlier = session.CutscenesBegun; // the setup's own scenes do not count as the scene beginning
            session.VisitRoom(room);
            if (useSetup) LoomCdSceneSetupPlanner.ApplyAfter(session, setup.After);
            if (!t.IsRoomEntry) session.ActivateObjectVerb(t.ObjectId, t.VerbId);
            var last = session.Snapshot.RoomId;
            var quiet = 0;
            while (frames < 6000)
            {
                var busy = session.InCutscene || session.Messages.HaveMsg != 0 || session.EgoMoving || session.CameraMoving;
                quiet = busy ? 0 : quiet + 1;
                if (session.InCutscene) began = true;
                // Ambient scripts (swans, waves) never end, so the scene is over once nothing that takes the player's control has run for 90 frames.
                if (quiet >= 90 || (!began && frames >= 300)) break;
                if (session.CutsceneSkippable) { skippable = true; if (escape) session.PressKey(0x1B); }
                session.AdvanceFrame(); frames++;
                if (session.Snapshot.RoomId != last) { rooms++; last = session.Snapshot.RoomId; }
            }
            return new(frames, skippable, session.Messages.Log.Count, rooms, session.Messages.VoiceEvents.Count, began || session.CutscenesBegun > earlier, null);
        }
        catch (Exception ex) { return new(frames, skippable, 0, rooms, 0, began, ex.GetType().Name + ": " + ex.Message); }
    }

    [Fact]
    public void Sweep()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_SWEEP_OUT");
        if (outPath is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var triggers = LoomCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value).ToList());
        var groups = LoomCutsceneIndex.Group(triggers);
        var planner = new LoomCdSceneSetupPlanner(game, triggers);
        var lines = new List<string>();
        foreach (var g in groups)
        {
            var plain = Play(game, g, false, planner);
            var esc = plain.Skippable ? Play(game, g, true, planner) : null;
            lines.Add($"{(g.IsScene ? "SCENE" : "react")} room {g.First.Room:00} obj {g.First.ObjectId} {g.First.ObjectName} {g.First.VerbLabel}: frames {plain.Frames} cutscene {plain.Began} skippable {plain.Skippable} lines {plain.Lines} roomChanges {plain.Rooms} voices {plain.Voices}"
                      + (esc is null ? "" : $" | escape: frames {esc.Frames} rooms {esc.Rooms} {esc.Error}") + (plain.Error is null ? "" : $" | ERROR {plain.Error}"));
        }
        File.WriteAllLines(outPath, lines);
    }
}

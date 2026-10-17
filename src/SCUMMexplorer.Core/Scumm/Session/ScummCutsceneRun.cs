using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>What a headless run of a scanned trigger did: whether a cutscene began, the rooms other than the trigger's own it entered, the frames.</summary>
public sealed record ScummCutsceneRunResult(bool Begun, IReadOnlyList<int> RoomsEntered, int Frames, string? Failure = null);

/// <summary>
/// Runs a scanned cutscene trigger headless in a fresh generic session (<see cref="ScummGameSession"/>), as <c>LoomCutsceneRun</c> does for LOOM (S7,
/// 17 October 2026): the boot to the first playable room, the trigger's room visited as a state preview, then its entry script (the visit) or the
/// object's verb run directly (an exploration action, not the game's click), and frames until the cutscene and its lines are over (no Escape, so a
/// scene is not cut short). A load that sits in a branch the run does not take is not seen. No start setup is applied: this is a fresh start.
/// </summary>
public static class ScummCutsceneRun
{
    public const int EndFrames = 6000;
    public const int TrialFrames = 600;

    /// <param name="newSession">A fresh session booted to the first playable room (each call must give a new one).</param>
    public static ScummCutsceneRunResult Run(GameLibrary game, Func<ScummGameSession> newSession, ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(newSession);
        ArgumentNullException.ThrowIfNull(trigger);
        try
        {
            var session = newSession();
            if (game.Rooms.FirstOrDefault(r => r.ResourceId == trigger.Room) is not { } room) return new(false, [], 0, $"Room {trigger.Room} is not in the game's index");
            var earlier = session.CutscenesBegun;
            var mark = session.RoomsEntered.Count;
            // An entry trigger in the room the boot ends in plays that room's entry again (its first run was inside the boot's own cutscene).
            if (trigger.IsRoomEntry) session.EnterRoomAgain(room);
            else session.VisitRoom(room);
            if (!trigger.IsRoomEntry)
            {
                // The rooms the visit itself entered (a pseudo room, an entry that moves on) are not the scene's.
                mark = session.RoomsEntered.Count;
                earlier = session.CutscenesBegun;
                session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
            }
            var begun = false;
            var frame = 0;
            for (; frame < EndFrames; frame++)
            {
                if (session.InCutscene || session.CutscenesBegun > earlier) begun = true;
                if (begun && !session.InCutscene && session.Messages.HaveMsg == 0) break;
                if (!begun && frame >= TrialFrames) break;
                session.AdvanceFrame();
            }
            return new(begun, session.RoomsEntered.Skip(mark).Where(r => r != trigger.Room).Distinct().ToArray(), frame);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException)
        {
            return new(false, [], 0, ex.Message);
        }
    }
}

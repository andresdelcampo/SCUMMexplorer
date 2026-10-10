using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// Runs a scanned cutscene trigger headless, in a fresh session, to see what the code does instead of what it could do: the rooms the session
/// enters while the cutscene and its lines play (no Escape, so a scene is not cut short). A load that sits in a branch the run does not take is
/// not seen, which is exactly why the static count of room loads over-reports (the dragon's sleep reaction has loads in the code and stays in the
/// room). No start setup is applied: this is the game's first-visit state, the state the EGA catalog's own validation started most scenes from.
/// </summary>
public static class LoomCutsceneRun
{
    private const int EndFrames = 6000;
    private const int TrialFrames = 600;

    /// <summary>The rooms other than the trigger's own that the run entered; null when the run could not be made or no cutscene began.</summary>
    public static IReadOnlyList<int>? VisitedRooms(GameLibrary game, ScummIndexedObjectSnapshot snapshot, LoomCutscene trigger)
    {
        try
        {
            var session = new LoomExplorationSession(game, snapshot);
            session.VisitRoom(game.Rooms.First(r => r.ResourceId == trigger.Room));
            var earlier = session.CutscenesBegun;
            var mark = session.RoomsEntered.Count; // rooms the visit itself entered are not the scene's
            if (!trigger.IsRoomEntry) session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
            var begun = false;
            for (var frame = 0; frame < EndFrames; frame++)
            {
                if (session.InCutscene || session.CutscenesBegun > earlier) begun = true;
                if (begun && !session.InCutscene && session.Messages.HaveMsg == 0) break;
                if (!begun && frame >= TrialFrames) break;
                session.AdvanceFrame();
            }
            return begun ? session.RoomsEntered.Skip(mark).Where(r => r != trigger.Room).Distinct().ToArray() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException) { return null; }
    }

    /// <summary>
    /// The category of a group once its run is known: a scene when it is the room's entry, when the run left the room, or when it is a silent Use
    /// (the same silent-Use condition as <see cref="LoomCutsceneGroup.IsScene"/>). A run that could not be made (<paramref name="visited"/> null) falls back
    /// to the static rule. Checked on 10 October against the 328-replay English EGA catalog: 320 agree, against 225 of the 238 grouped ones for the static rule alone.
    /// </summary>
    public static bool IsScene(LoomCutsceneGroup group, IReadOnlyList<int>? visited) =>
        visited is null ? group.IsScene : group.First.IsRoomEntry || visited.Count > 0 || (group.First.Texts == 0 && group.First.VerbId == 0x38);
}

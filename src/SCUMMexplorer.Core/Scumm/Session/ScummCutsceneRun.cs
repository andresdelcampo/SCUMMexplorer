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
            var traced = session.SessionTrace.Count;
            var mark = session.RoomsEntered.Count;
            // An entry trigger in the room the boot ends in plays that room's entry again (its first run was inside the boot's own cutscene).
            if (trigger.IsRoomEntry) session.EnterRoomAgain(room);
            else session.VisitRoom(room);
            if (!trigger.IsRoomEntry)
            {
                // The rooms the visit itself entered (a pseudo room, an entry that moves on) are not the scene's.
                mark = session.RoomsEntered.Count;
                earlier = session.CutscenesBegun;
                traced = session.SessionTrace.Count;
                session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
            }
            // Only a cutscene begun by a script the trigger's code reaches counts: a room's own background scripts begin cutscenes of their
            // own (Monkey Island's SCUMM Bar and High Street open and close doors through global scripts 25/26, each a short cutscene), and a visit's
            // entry may still be in its own. A cutscene begun by a room the trigger led to (its entry or local scripts) is the scene going on there.
            var reached = trigger.Scripts.Select(ScriptKey).Where(key => key is not null).ToHashSet();
            var begun = false;
            var frame = 0;
            for (; frame < EndFrames; frame++)
            {
                if (!begun && session.CutscenesBegun > earlier)
                {
                    var lines = session.SessionTrace.Skip(traced).Where(line => line.StartsWith("cutscene at ", StringComparison.Ordinal)).ToList();
                    traced = session.SessionTrace.Count;
                    // A begin the trace does not show (its start script unknown) is counted as before.
                    begun = lines.Count < session.CutscenesBegun - earlier || lines.Any(line => reached.Contains(ScriptKey(line["cutscene at ".Length..])) ||
                        RoomOf(line["cutscene at ".Length..]) is int at && at != trigger.Room && session.RoomsEntered.Skip(mark).Contains(at));
                    earlier = session.CutscenesBegun;
                }
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

    /// <summary>
    /// A script's identity in both the scan's labels (<c>global script 25</c>, <c>Room 35 local script 207</c>, <c>Room 28 object 322 verb 0x02</c>,
    /// <c>Room 35 entry script</c>) and the session trace's (<c>Script 25</c>, <c>Room 07 local script 200</c>, <c>Room 77 Object 845 Verb 0x0A</c>):
    /// global scripts by number, local scripts by room and number, objects by id, the entry by room. Null for a label of another shape.
    /// </summary>
    internal static string? ScriptKey(string label)
    {
        var match = System.Text.RegularExpressions.Regex.Match(label,
            @"^(?:Room (?<room>\d+) (?:(?<entry>entry)|local script (?<script>\d+)|[Oo]bject (?<object>\d+))|(?:global script|Script) (?<script>\d+))");
        if (!match.Success) return null;
        if (match.Groups["entry"].Success) return $"entry {int.Parse(match.Groups["room"].Value, System.Globalization.CultureInfo.InvariantCulture)}";
        if (match.Groups["object"].Success) return $"object {int.Parse(match.Groups["object"].Value, System.Globalization.CultureInfo.InvariantCulture)}";
        var script = int.Parse(match.Groups["script"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return match.Groups["room"].Success ? $"local {int.Parse(match.Groups["room"].Value, System.Globalization.CultureInfo.InvariantCulture)}/{script}" : $"global {script}";
    }

    /// <summary>The room of a room-owned script's label (<c>Room 12 local script 200</c>), else null.</summary>
    internal static int? RoomOf(string label) =>
        System.Text.RegularExpressions.Regex.Match(label, @"^Room (\d+) ") is { Success: true } match ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
}

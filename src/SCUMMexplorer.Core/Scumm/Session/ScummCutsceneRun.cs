using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>What a headless run of a scanned trigger did: whether a cutscene began, the rooms other than the trigger's own it entered, the frames.</summary>
/// <summary>How a picked scene was started (§8.6, R-8.35), in the order they are tried.</summary>
public enum ScummPickedStart
{
    /// <summary>The room's entry, or the verb given to the game's sentence script (<see cref="ScummSession.PickInteraction"/>).</summary>
    Game,
    /// <summary>The object's verb code run directly (<see cref="ScummSession.ActivateObjectVerb"/>).</summary>
    Direct,
    /// <summary>The script holding the cutscene started at its <c>cutscene</c> (<see cref="ScummSession.StartSceneAt"/>).</summary>
    AtCutscene,
}

public sealed record ScummCutsceneRunResult(bool Begun, IReadOnlyList<int> RoomsEntered, int Frames, string? Failure = null, int Lines = 0,
    ScummPickedStart Way = ScummPickedStart.Game)
{
    /// <summary>Beginning at all outweighs everything; then the rooms the scene went on to and the lines it printed (as LOOM's <c>LoomSceneOutcome</c>).</summary>
    public int Score => (Begun ? 10000 : 0) + RoomsEntered.Count * 20 + Lines;
}

/// <summary>
/// Runs a scanned cutscene trigger headless in a fresh generic session (<see cref="ScummGameSession"/>), as <c>LoomCutsceneRun</c> does for LOOM (S7,
/// 17 October 2026): the boot to the first playable room, the trigger's room visited as a state preview, then its entry script (the visit) or the
/// object's verb run directly (an exploration action, not the game's click), and frames until the cutscene and its lines are over (no Escape, so a
/// scene is not cut short). A load that sits in a branch the run does not take is not seen. Without <c>setup</c> this is a fresh start; with it, a picked
/// scene's setup (§8.6) is applied to the booted session before the room is visited, as if the game had reached that state earlier.
/// </summary>
public static class ScummCutsceneRun
{
    public const int EndFrames = 6000;
    public const int TrialFrames = 600;

    /// <param name="newSession">A fresh session booted to the first playable room (each call must give a new one).</param>
    /// <summary>
    /// A picked trigger (§8.6, R-8.35): the game's own way first (the entry, or the sentence script); when that does not begin the scene (the game's
    /// own refusals: too dark, out of reach), the verb's code run directly; the fuller of the two.
    /// </summary>
    public static ScummCutsceneRunResult RunPicked(GameLibrary game, Func<ScummGameSession> newSession, ScummCutscene trigger,
        IReadOnlyList<ScummSetupEdit>? setup = null)
    {
        var sentence = Run(game, newSession, trigger, setup);
        if (sentence.Begun || trigger.IsRoomEntry) return sentence;
        var direct = Run(game, newSession, trigger, setup, ScummPickedStart.Direct);
        // On a tie the verb's own code wins: the sentence script's answer is then the game's refusal ("I can't get there from here", Indy 4's crate).
        return direct.Score >= sentence.Score ? direct : sentence;
    }

    public static ScummCutsceneRunResult Run(GameLibrary game, Func<ScummGameSession> newSession, ScummCutscene trigger,
        IReadOnlyList<ScummSetupEdit>? setup = null, ScummPickedStart way = ScummPickedStart.Game)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(newSession);
        ArgumentNullException.ThrowIfNull(trigger);
        try
        {
            var session = newSession();
            if (setup is { Count: > 0 }) session.ApplySceneSetup(setup);
            if (game.Rooms.FirstOrDefault(r => r.ResourceId == trigger.Room) is not { } room) return new(false, [], 0, $"Room {trigger.Room} is not in the game's index");
            var earlier = session.CutscenesBegun;
            var traced = session.SessionTrace.Count;
            var mark = session.RoomsEntered.Count;
            var logMark = session.Messages.Log.Count;
            // An entry trigger in the room the boot ends in plays that room's entry again (its first run was inside the boot's own cutscene).
            if (trigger.IsRoomEntry && way != ScummPickedStart.AtCutscene) session.EnterRoomAgain(room);
            else session.VisitRoom(room);
            // Again after the visit: the room's entry may have reset what the setup set (Indy 3 Room 23's entry closes its doors, whose Close tests
            // state 1); only what the session lacks is set, so values the entry derived from the setup stay.
            if (setup is { Count: > 0 }) session.ApplySceneSetup(setup);
            if (way == ScummPickedStart.AtCutscene)
            {
                mark = session.RoomsEntered.Count;
                earlier = session.CutscenesBegun;
                traced = session.SessionTrace.Count;
                logMark = session.Messages.Log.Count;
                if (ScummSceneSetupSearch.FirstCutscene(game, trigger) is not { } at || !session.StartSceneAt(at.Label, at.Offset))
                    return new(false, [], 0, "no cutscene the session can start in this room", Way: way);
            }
            else if (!trigger.IsRoomEntry)
            {
                // The rooms the visit itself entered (a pseudo room, an entry that moves on) are not the scene's.
                mark = session.RoomsEntered.Count;
                earlier = session.CutscenesBegun;
                traced = session.SessionTrace.Count;
                logMark = session.Messages.Log.Count;
                // A picked interaction goes through the game's own sentence script where it has one (§8.6, R-8.38), with the second object a setup gave.
                var second = setup?.FirstOrDefault(edit => edit.Kind == ScummSetupKind.SecondObject) is { } with ? int.Parse(with.Target, System.Globalization.CultureInfo.InvariantCulture) : 0;
                if (way == ScummPickedStart.Direct) session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
                else session.PickInteraction(trigger.ObjectId, trigger.VerbId, second);
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
                session.StepFrame();
            }
            return new(begun, session.RoomsEntered.Skip(mark).Where(r => r != trigger.Room).Distinct().ToArray(), frame, Lines: session.Messages.Log.Count - logMark, Way: way);
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

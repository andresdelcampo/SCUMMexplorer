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
    /// <summary>The sentence script given the held second object first, as the player says "Use the Sunstone with the pedestal" (Indy 4 Room 33): tried when
    /// the object first does not begin the scene (<see cref="ScummSession.PickInteraction"/>).</summary>
    GameHeldFirst,
}

/// <param name="Verb">The verb the game's own way used when it is not the trigger's: another verb of the object whose code is the same scene (Indy 4 Room 48's
/// stone, Push and Use share it, and only Use reaches it with the Sunstone).</param>
public sealed record ScummCutsceneRunResult(bool Begun, IReadOnlyList<int> RoomsEntered, int Frames, string? Failure = null, int Lines = 0,
    ScummPickedStart Way = ScummPickedStart.Game, int? Verb = null)
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

    /// <summary>Frames with no cutscene and no line after which a begun scene is over (the idle tail of LOOM English's reviewed replays).</summary>
    public const int QuietFrames = 32;

    /// <param name="newSession">A fresh session booted to the first playable room (each call must give a new one).</param>
    /// <summary>
    /// A picked trigger (§8.6, R-8.35): the game's own way first (the entry, or the sentence script); when that does not begin the scene (the game's
    /// own refusals: too dark, out of reach), the verb's code run directly; the fuller of the two.
    /// </summary>
    public static ScummCutsceneRunResult RunPicked(GameLibrary game, Func<ScummSession> newSession, ScummCutscene trigger,
        IReadOnlyList<ScummSetupEdit>? setup = null)
    {
        // An arrival scene the room's loader starts: the room's entry does not begin it, so it starts at its cutscene, as the loader starts it.
        if (trigger.IsArrivalScript) return Run(game, newSession, trigger, setup, ScummPickedStart.AtCutscene);
        var sentence = Run(game, newSession, trigger, setup);
        if (sentence.Begun || trigger.IsRoomEntry) return sentence;
        // A held second object may need to come first in the sentence (Indy 4 Script 2 +0x04A3 refuses an object of no usable class in front; the
        // Sunstone's own code hands over to the pedestal). Most two-object scenes begin the other way (Give: Monkey Island 2, Indy 4), so this is second.
        if (setup?.Any(edit => edit.Kind == ScummSetupKind.SecondObject) == true)
        {
            var heldFirst = Run(game, newSession, trigger, setup, ScummPickedStart.GameHeldFirst);
            if (heldFirst.Begun) return heldFirst;
            // The object's other verbs whose code is the same scene: the scan names the scene by one of them (Indy 4 Room 48's stone: Push, 0x06), the game
            // reaches it by another (Use, 0x08: the Sunstone's code, +0x0057, starts the stone's Use with itself as the argument).
            foreach (var verb in SameCodeVerbs(game, trigger))
                foreach (var way in new[] { ScummPickedStart.Game, ScummPickedStart.GameHeldFirst })
                    if (Run(game, newSession, trigger with { VerbId = verb }, setup, way) is { Begun: true } other) return other with { Verb = verb };
        }
        var direct = Run(game, newSession, trigger, setup, ScummPickedStart.Direct);
        // On a tie the verb's own code wins: the sentence script's answer is then the game's refusal ("I can't get there from here", Indy 4's crate).
        return direct.Score >= sentence.Score ? direct : sentence;
    }

    /// <summary>The trigger object's other verbs whose code starts where the trigger verb's does (one scene, several verbs).</summary>
    private static IReadOnlyList<int> SameCodeVerbs(GameLibrary game, ScummCutscene trigger)
    {
        try
        {
            var data = ScummDataSources.For(game);
            var records = data.ReadRoom(trigger.Room).Objects;
            if (!data.ReadObjectVerbs(trigger.Room, records).TryGetValue(trigger.ObjectId, out var entries)) return [];
            if (entries.FirstOrDefault(e => e.VerbId == trigger.VerbId) is not { } own) return [];
            return entries.Where(e => !e.IsFallback && e.VerbId != trigger.VerbId && e.CodeOffset == own.CodeOffset).Select(e => e.VerbId).ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or KeyNotFoundException) { return []; }
    }

    public static ScummCutsceneRunResult Run(GameLibrary game, Func<ScummSession> newSession, ScummCutscene trigger,
        IReadOnlyList<ScummSetupEdit>? setup = null, ScummPickedStart way = ScummPickedStart.Game)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(newSession);
        ArgumentNullException.ThrowIfNull(trigger);
        try
        {
            var session = newSession();
            session.DressHeroForScene(trigger);
            if (setup is { Count: > 0 }) session.ApplySceneSetup(setup);
            if (game.Rooms.FirstOrDefault(r => r.ResourceId == trigger.Room) is not { } room) return new(false, [], 0, $"Room {trigger.Room} is not in the game's index");
            var earlier = session.CutscenesBegun;
            var traced = session.SessionTrace.Count;
            var mark = session.RoomsEntered.Count;
            var logMark = session.Messages.Log.Count;
            // An entry trigger in the room the boot ends in plays that room's entry again (its first run was inside the boot's own cutscene).
            // A verb that plays only in another room is used there, the object held (ScummSetupKind.PlayedIn).
            var playIn = !trigger.IsRoomEntry && way != ScummPickedStart.AtCutscene && ScummSetupEdit.PlayRoom(setup) is int other &&
                         game.Rooms.FirstOrDefault(r => r.ResourceId == other) is { } otherRoom ? otherRoom : room;
            // A scene that is not the room's arrival does not replay the arrival on the way in (ScummSession.ArrivalScenesSeen).
            session.ArrivalsSkippedFor = trigger.IsRoomEntry ? null : trigger;
            session.ArrivalSetup = setup;
            session.WantHeroForScene(trigger);
            if (trigger.IsRoomEntry && way != ScummPickedStart.AtCutscene) session.EnterRoomAgain(room);
            else session.VisitRoom(playIn);
            session.ArrivalsSkippedFor = null;
            session.ArrivalSetup = null;
            // Again after the visit: the room's entry may have reset what the setup set (Indy 3 Room 23's entry closes its doors, whose Close tests
            // state 1); only what the session lacks is set, so values the entry derived from the setup stay.
            if (setup is { Count: > 0 }) session.ApplySceneSetup(setup);
            if (way == ScummPickedStart.AtCutscene)
            {
                mark = session.RoomsEntered.Count;
                earlier = session.CutscenesBegun;
                traced = session.SessionTrace.Count;
                logMark = session.Messages.Log.Count;
                if (ScummSceneSetupSearch.FirstCutscene(game, trigger) is not { } at || !session.StartSceneAt(at.Label, at.Offset, at.Arguments))
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
                if (way == ScummPickedStart.Direct) session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId, second);
                else session.PickInteraction(trigger.ObjectId, trigger.VerbId, second, heldFirst: way == ScummPickedStart.GameHeldFirst);
                // The hero is in the room only from the pick on (put at the object): the values that stand actors in it are set again now (Zak
                // McKracken Room 10's purple button waits for the hero past x 50).
                if (setup is { Count: > 0 }) session.ApplySceneSetup(ScummSetupEdit.InRoomActors(setup));
            }
            // Only a cutscene begun by a script the trigger's code reaches counts: a room's own background scripts begin cutscenes of their
            // own (Monkey Island's SCUMM Bar and High Street open and close doors through global scripts 25/26, each a short cutscene), and a visit's
            // entry may still be in its own. A cutscene begun by a room the trigger led to (its entry or local scripts) is the scene going on there.
            var reached = trigger.Scripts.Select(ScriptKey).Where(key => key is not null).ToHashSet();
            // A scene whose script waits before its cutscene (Maniac Mansion's Script 148: delay 3600, a minute after the kids go in) is waited for:
            // at most one frame per jiffy of the delay.
            var wait = way == ScummPickedStart.AtCutscene ? 0 : ScummSceneSetupSearch.DelayBeforeCutscene(game, trigger);
            // The scene's own scripts do not wait out their timers (Maniac Mansion's Script 139: seven minutes before Ed's round).
            if (way != ScummPickedStart.AtCutscene) session.HurryUntilCutscene(HurriedScripts(game, trigger));
            var begun = false;
            var frame = 0;
            var quiet = 0;
            // Versions 1 and 2 (Maniac Mansion, Zak McKracken: recurring background cutscenes) keep ending at the first quiet frame until they move
            // onto the shared scene setup (ROADMAP 2b); their fidelity tests pin it.
            var quietNeeded = ScummProfile.For(game).EngineVersion <= ScummVersion.V2 ? 1 : QuietFrames;
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
                // A scene may be a chain of cutscenes (LOOM Room 18's bell: Script 62, then Room 20's, then Scripts 58 and 59, one frame apart): it is
                // over only after QuietFrames frames with no cutscene and no line, as LOOM English's reviewed replays were run (31 October 2026).
                quiet = begun && !session.InCutscene && session.Messages.HaveMsg == 0 ? quiet + 1 : 0;
                if (quiet >= quietNeeded) break;
                if (!begun && frame >= TrialFrames + wait) break;
                session.StepFrame();
            }
            return new(begun, session.RoomsEntered.Skip(mark).Where(r => r != trigger.Room).Distinct().ToArray(), frame - Math.Max(0, quiet - 1), Lines: session.Messages.Log.Count - logMark, Way: way);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException)
        {
            return new(false, [], 0, ex.Message);
        }
    }

    /// <summary>
    /// A script's identity in both the scan's labels (<c>global script 25</c>, <c>Room 35 local script 207</c>, <c>Room 28 object 322 verb 0x02</c>,
    /// <c>Room 35 entry script</c>) and the session trace's (<c>Script 25</c>, <c>Room 07 local script 200</c>, <c>Room 77 Object 845 Verb 0x0A</c>,
    /// versions 1 and 2 <c>Room 08 / Object 208 / Verb 0xFD</c>):
    /// global scripts by number, local scripts by room and number, objects by id, the entry by room. Null for a label of another shape.
    /// </summary>
    internal static string? ScriptKey(string label)
    {
        // Versions 1 and 2 label object code "Room 08 / Object 208 / Verb 0xFD" (Zak McKracken).
        label = label.Replace(" / ", " ", StringComparison.Ordinal);
        var match = System.Text.RegularExpressions.Regex.Match(label,
            @"^(?:Room (?<room>\d+) (?:(?<entry>entry)|local script (?<script>\d+)|[Oo]bject (?<object>\d+))|(?:global script|Script) (?<script>\d+))");
        if (!match.Success) return null;
        if (match.Groups["entry"].Success) return $"entry {int.Parse(match.Groups["room"].Value, System.Globalization.CultureInfo.InvariantCulture)}";
        if (match.Groups["object"].Success) return $"object {int.Parse(match.Groups["object"].Value, System.Globalization.CultureInfo.InvariantCulture)}";
        var script = int.Parse(match.Groups["script"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return match.Groups["room"].Success ? $"local {int.Parse(match.Groups["room"].Value, System.Globalization.CultureInfo.InvariantCulture)}/{script}" : $"global {script}";
    }

    /// <summary>The numbers of the global and local scripts a trigger's scene runs through (its scan labels), whose timers a pick hurries.</summary>
    public static IReadOnlyList<int> HurriedScripts(GameLibrary game, ScummCutscene trigger) =>
        ScummSceneSetupSearch.SceneScripts(game, trigger).Select(label => System.Text.RegularExpressions.Regex.Match(label, @"^(?:global script|Room \d+ local script) (\d+)$"))
            .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToArray();

    /// <summary>The room of a room-owned script's label (<c>Room 12 local script 200</c>), else null.</summary>
    internal static int? RoomOf(string label) =>
        System.Text.RegularExpressions.Regex.Match(label, @"^Room (\d+) ") is { Success: true } match ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
}

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>
    /// Set while a picked scene that is not a room's arrival is being set up (2 November 2026, user report: Indy 3's Give Grail entered Room 82 and
    /// replayed its first arrival, the beheading, whose head stayed on the floor through the ending). A room entered meanwhile does not play its arrival:
    /// a room script the entry started that holds a cutscene once the entry has run (Room 82's local script 200, which beheads the soldier, then walks
    /// Indy on to Room 83) is ended and its cutscene closed, as a skipped route is (the game's cutscene-end hook runs). The picked scene's own scripts
    /// are kept. The actors and objects the entry itself set stay as it left them.
    /// </summary>
    public ScummCutscene? ArrivalsSkippedFor { get; set; }

    /// <summary>The picked scene's setup values: a room script whose tests they come from is part of the scene and keeps its cutscene (Indy 4 Room 10's
    /// back door: "arrived from Room 15", tested in the arrival script, local script 204).</summary>
    public IReadOnlyCollection<ScummSetupEdit>? ArrivalSetup { get; set; }

    private void EndArrivalCutscenes(int roomId)
    {
        if (ArrivalsSkippedFor is not { } trigger || _cutscenes.Count == 0) return;
        // A setup that says how the room is entered (where from, by which door, under which number) wants that arrival.
        if (ArrivalSetup?.Any(edit => edit.Kind is ScummSetupKind.CameFrom or ScummSetupKind.EnteredBy or ScummSetupKind.EnteredAs) == true) return;
        var kept = ScummSceneSetupSearch.SceneScripts(_game, trigger).ToHashSet(StringComparer.Ordinal);
        foreach (var edit in ArrivalSetup ?? []) kept.Add(edit.Where.Split(" +0x", 2)[0]);
        var active = _pending.Select(p => p.Frame).Concat(_frameScripts.Select(p => p.Frame)).Where(f => !f.Stopped).ToHashSet();
        var owners = _cutscenes.Select(c => c.Owner).OfType<ScriptFrame>().Where(active.Contains).ToHashSet();
        bool Kept(ScriptFrame frame)
        {
            for (var current = frame; current is not null; current = current.Parent)
                if (current.Global || kept.Contains($"Room {roomId} local script {current.Number}")) return true;
            return false;
        }
        var arrivals = owners.Where(frame => !frame.Global && frame.OriginRoom == roomId && !Kept(frame)).ToHashSet();
        if (arrivals.Count == 0) return;
        foreach (var frame in active.Where(f => DescendsFrom(f, arrivals))) frame.Stopped = true;
        _pending.RemoveAll(p => p.Frame.Stopped);
        _frameScripts.RemoveAll(p => p.Frame.Stopped);
        for (var scopes = 0; _cutscenes.Count > 0 && _cutscenes.Peek().Owner is { Stopped: true } && scopes < MaxNestedScripts; scopes++) CloseTopCutscene();
        _uncertainties.Add($"A picked scene enters Room {roomId:00}: its arrival (local script {string.Join(", ", arrivals.Select(f => f.Number))}, a cutscene the entry " +
                           "started) does not play; it was ended and its cutscene closed, as a skipped route is.");
    }

    private sealed record GameScriptFacts(IReadOnlyDictionary<string, int> RaisedFlags, IReadOnlyDictionary<int, HashSet<string>> Loaders)
    {
        /// <summary>The one script that loads the room (loadRoom, loadRoomWithEgo with a constant room), or null when none or several do.</summary>
        public string? OnlyLoader(int room) => Loaders.TryGetValue(room, out var labels) && labels.Count == 1 ? labels.Single() : null;

        /// <summary>A room only the boot script loads (Indy 3's Lucasfilm logo, Room 76): the game shows it in its opening and never in play.</summary>
        public bool OpeningOnly(int room) => OnlyLoader(room) == $"global script {BootScript}";
    }

    /// <summary>
    /// The global script that alone shows the room (2 November 2026, user report: Indy 3's plane manual, Room 74, whose page is blank until Script 90,
    /// the only script that loads it, prints the instructions), other than the boot; null for a room the game enters otherwise. A room picked from the
    /// list is shown as that script shows it.
    /// </summary>
    public string? ShownOnlyBy(int room)
    {
        var facts = Facts();
        // A viewer: a global script (not the boot) that loads this room and no other, so starting it shows the room and leaves the story alone.
        return facts.OnlyLoader(room) is { } label && label.StartsWith("global script ", StringComparison.Ordinal) && label != $"global script {BootScript}" &&
               facts.Loaders.Count(entry => entry.Value.Contains(label)) == 1 ? label : null;
    }

    /// <summary>
    /// 2 November 2026 (user report, Indy 3 Room 13): a picked room entry whose scripts speak as the hero (<c>printEgo</c>) plays with the hero in its
    /// room. When no decoded arrival brings him in, he was left in the room the boot ended in (Room 73), and the game's cutscene-end hook (Script 23
    /// +0x0022, <c>actorFollowCamera</c> on the hero) then loaded his room; now the next visit of the scene's room puts him in it, as a setup that tests
    /// he is there does (a teleport, labelled).
    /// </summary>
    public void WantHeroForScene(ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        // A room's own arrival (its entry) only, and not a room the game enters under pseudo room numbers, whose ways in our arrival index does
        // not see (Indy 4 Rooms 72 and 93: the hero brought in where he last stood changed what their entries test).
        if (!trigger.IsRoomEntry || _runtime.PseudoRooms.Values.Contains(trigger.Room)) return;
        foreach (var label in ScummSceneSetupSearch.SceneScripts(_game, trigger))
        {
            ScummScriptDisassembly? script = null;
            try
            {
                if (Regex.Match(label, @"^global script (\d+)$") is { Success: true } g) script = _data.GlobalScript(int.Parse(g.Groups[1].Value, CultureInfo.InvariantCulture));
                else if (Regex.Match(label, @"^Room (\d+) local script (\d+)$") is { Success: true } l)
                    script = _data.RoomLocalScript(int.Parse(l.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(l.Groups[2].Value, CultureInfo.InvariantCulture));
                else if (Regex.Match(label, @"^Room (\d+) entry script$") is { Success: true } e) script = _data.RoomEntryScript(int.Parse(e.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { }
            if (script?.Instructions.Any(i => i.Operation == "printEgo") != true) continue;
            WantedHeroIn = trigger.Room;
            return;
        }
    }

    private static readonly ConditionalWeakTable<GameLibrary, Lazy<GameScriptFacts>> ScriptFacts = new();

    private GameScriptFacts Facts() => ScriptFacts.GetValue(_game, game => new Lazy<GameScriptFacts>(() => ReadScriptFacts(game, _data))).Value;
    private static readonly Regex FlagMove = new(@"^((?:bit|var)\[0x[0-9A-F]+\]) = (0x[0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex BranchTarget = new(@"-> 0x([0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex FlagGuard = new(@"^((?:bit|var)\[0x([0-9A-F]+)\]),", RegexOptions.CultureInvariant);

    // Over the game's global, room and object scripts: every story flag set to a constant other than 0 (the first such value), and the rooms loaded
    // (loadRoom, loadRoomWithEgo with a constant room) by the boot script and by any other script.
    private static GameScriptFacts ReadScriptFacts(GameLibrary game, IScummDataSource data)
    {
        var found = new Dictionary<string, int>();
        var loaders = new Dictionary<int, HashSet<string>>();
        void Read(Func<ScummScriptDisassembly?> read, string label)
        {
            ScummScriptDisassembly? script;
            try { script = read(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException) { return; }
            if (script is null) return;
            foreach (var instruction in script.Instructions)
            {
                if (instruction.Operation == "move" && FlagMove.Match(instruction.Operands) is { Success: true } m &&
                    int.Parse(m.Groups[2].Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) is var value and not 0)
                    found.TryAdd(m.Groups[1].Value, value);
                if (instruction.Operation is "loadRoom" or "loadRoomWithEgo")
                {
                    var operands = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                    var target = instruction.Operation == "loadRoom" ? operands[0] : operands.ElementAtOrDefault(1) ?? "";
                    if (target.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(target[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var room))
                        (loaders.TryGetValue(room, out var labels) ? labels : loaders[room] = []).Add(label);
                }
            }
        }
        foreach (var id in data.GlobalScriptIds()) Read(() => data.GlobalScript(id), $"global script {id}");
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            Read(() => data.RoomEntryScript(room), $"Room {room} entry script");
            Read(() => data.RoomExitScript(room), $"Room {room} exit script");
            foreach (var local in data.RoomLocalScriptIds(room)) Read(() => data.RoomLocalScript(room, local), $"Room {room} local script {local}");
            try
            {
                var objects = data.ReadRoom(room).Objects;
                foreach (var (objectId, verbs) in data.ReadObjectVerbs(room, objects))
                    foreach (var verb in verbs) Read(() => data.ReadObjectVerb(room, objects.Single(o => o.Id == objectId), verb.VerbId, out _),
                        $"Room {room} object {objectId} verb 0x{verb.VerbId:X2}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException) { }
        }
        return new(found, loaders);
    }
}

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

    private void EndArrivalCutscenes(int roomId, IReadOnlySet<int>? inRoomBefore)
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
                if (kept.Contains(current.Global ? $"global script {current.Number}" : $"Room {roomId} local script {current.Number}")) return true;
            return false;
        }
        var arrivals = owners.Where(frame => !frame.Global && frame.OriginRoom == roomId && !Kept(frame)).ToHashSet();
        if (arrivals.Count == 0) return;
        foreach (var frame in active.Where(f => DescendsFrom(f, arrivals))) frame.Stopped = true;
        _pending.RemoveAll(p => p.Frame.Stopped);
        _frameScripts.RemoveAll(p => p.Frame.Stopped);
        for (var scopes = 0; _cutscenes.Count > 0 && _cutscenes.Peek().Owner is { Stopped: true } && scopes < MaxNestedScripts; scopes++) CloseTopCutscene();
        // The arrival had already begun: Room 82's local script 200 puts the soldier's head (actor 10, costume 85) in before its first pause. The
        // actors its script puts into this room, and that were not here before the entry, go back out (the user saw the head roll by later).
        var placed = new HashSet<int>();
        foreach (var number in arrivals.Select(f => f.Number).OfType<int>())
        {
            ScummScriptDisassembly? script;
            try { script = _data.RoomLocalScript(roomId, number); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { continue; }
            if (script is not null) MarkArrivalSeen(roomId, number, script);
            foreach (var put in script?.Instructions.Where(i => i.Operation == "putActorInRoom") ?? [])
            {
                var operands = put.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (operands.Length == 2 && operands[0].StartsWith("0x", StringComparison.Ordinal) &&
                    int.TryParse(operands[0][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var actor)) placed.Add(actor);
            }
        }
        var ego = _runtime.GlobalVariables.TryGetValue(EgoVariable, out var egoValue) ? (int?)egoValue : null;
        foreach (var actor in placed.Where(a => _runtime.Actors.Contains(a) && _runtime.Actors[a].Room == roomId && inRoomBefore?.Contains(a) != true && a != ego))
            _runtime.Actors.PutInRoom(actor, 0, CurrentPlacement(), ego);
        _uncertainties.Add($"A picked scene enters Room {roomId:00}: its arrival (local script {string.Join(", ", arrivals.Select(f => f.Number))}, a cutscene the entry " +
                           "started) does not play; it was ended and its cutscene closed, as a skipped route is.");
    }

    /// <summary>
    /// An ended arrival also counts as seen, so a later entry in the same scene skips it as the game would (the user saw the head roll by when Give
    /// Grail's ending entered Room 82 again): the flag the arrival tests just inside its cutscene (Room 82's local script 200 +0x0005, bit[0x5E2], which
    /// Script 122 sets as Indy comes back with the Grail), or the flag it tests and sets itself before its cutscene, is raised to the value the game
    /// gives it. A test that loops back (a wait for the flag) is left alone.
    /// </summary>
    private void MarkArrivalSeen(int roomId, int number, ScummScriptDisassembly script)
    {
        var instructions = script.Instructions;
        var first = instructions.ToList().FindIndex(i => i.Operation == "cutscene");
        if (first < 0) return;
        var raised = Facts().RaisedFlags;
        for (var index = 0; index < instructions.Count; index++)
        {
            var guard = instructions[index];
            if (guard.Operation is not ("notEqualZero" or "equalZero") || FlagGuard.Match(guard.Operands) is not { Success: true } match) continue;
            var flag = match.Groups[1].Value;
            var marks = instructions.Skip(index + 1).Take(2).FirstOrDefault(i => i.Operation == "move" && i.Operands.StartsWith(flag + " =", StringComparison.Ordinal) &&
                FlagMove.Match(i.Operands) is { Success: true } m && m.Groups[2].Value != "0x0000");
            var inside = index > first && index <= first + 3;
            var selfMarked = marks is not null && instructions.Skip(index + 1).Take(4).Any(i => i.Operation == "cutscene");
            if (!inside && !selfMarked) continue;
            if (BranchTarget.Match(guard.Operands) is { Success: true } target &&
                int.Parse(target.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) <= guard.Offset) continue;
            if (flag.StartsWith("var[", StringComparison.Ordinal) && int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) < 0x80) continue;
            int value;
            if (marks is not null) value = int.Parse(FlagMove.Match(marks.Operands).Groups[2].Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (!raised.TryGetValue(flag, out value)) continue;
            var where = $"Room {roomId} local script {number} +0x{guard.Offset:X4}";
            if (ApplySetupEdit(new ScummSetupEdit(flag.StartsWith("bit[", StringComparison.Ordinal) ? ScummSetupKind.Bit : ScummSetupKind.Variable, flag, value, where)))
                _uncertainties.Add($"The ended arrival counts as seen ({where} tests {flag}): {flag} = {value}, as the game sets it.");
        }
    }

    private sealed record GameScriptFacts(IReadOnlyDictionary<string, int> RaisedFlags, IReadOnlyDictionary<int, HashSet<string>> Loaders, int? HeroOutfit)
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

    /// <summary>
    /// 2 November 2026 (user report, Indy 3 Room 85: Indy in his college suit in the catacombs). A room picked from the list is a state preview whose
    /// route was not played; the hero wears what the boot left (the suit). Unless the opening shows the room (the college, where the suit is right),
    /// he is dressed in the outfit the game's scripts give him most often (Indy 3: costume 2, the action outfit; the suit is set twice), labelled.
    /// </summary>
    public void DressHeroForPreview(int roomId)
    {
        if (_runtime.IsV2 || _bootShown.ContainsKey(roomId) || Facts().HeroOutfit is not int outfit) return;
        if (!_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !_runtime.Actors.Contains(ego)) return;
        if (_runtime.Actors[ego].Look.Costume is int worn && worn == outfit) return;
        _runtime.Actors.CostumeChanged(ego, outfit, CurrentPlacement());
        _uncertainties.Add($"Room {roomId:00} was picked without its route: the hero wears costume {outfit}, the outfit the game's scripts dress him in most often " +
                           "(an estimate; the room's own code or a picked scene's setup may change it).");
    }

    private static readonly ConditionalWeakTable<GameLibrary, Lazy<GameScriptFacts>> ScriptFacts = new();

    private GameScriptFacts Facts() => ScriptFacts.GetValue(_game, game => new Lazy<GameScriptFacts>(() => ReadScriptFacts(game, _data))).Value;
    private static readonly Regex FlagMove = new(@"^((?:bit|var)\[0x[0-9A-F]+\]) = (0x[0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex BranchTarget = new(@"-> 0x([0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex HeroCostume = new(@"^(?:0x01|var\[0x001\]), \[[^\]]*costume, 0x([0-9A-F]+)", RegexOptions.CultureInvariant);
    private static readonly Regex FlagGuard = new(@"^((?:bit|var)\[0x([0-9A-F]+)\]),", RegexOptions.CultureInvariant);

    // Over the game's global, room and object scripts: every story flag set to a constant other than 0 (the first such value), and the rooms loaded
    // (loadRoom, loadRoomWithEgo with a constant room) by the boot script and by any other script.
    private static GameScriptFacts ReadScriptFacts(GameLibrary game, IScummDataSource data)
    {
        var found = new Dictionary<string, int>();
        var loaders = new Dictionary<int, HashSet<string>>();
        var outfits = new Dictionary<int, int>();
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
                // actorOps on actor 1 or VAR_EGO (versions 3 to 5): the costumes the game dresses its hero in.
                if (instruction.Operation == "actorOps" && HeroCostume.Match(instruction.Operands) is { Success: true } dressed)
                {
                    var costume = int.Parse(dressed.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    outfits[costume] = outfits.GetValueOrDefault(costume) + 1;
                }
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
        var outfit = game.Version is ScummVersion.V1 or ScummVersion.V2 || outfits.Count == 0 ? (int?)null : outfits.MaxBy(pair => pair.Value).Key;
        return new(found, loaders, outfit);
    }
}

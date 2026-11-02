using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>
    /// Set while a picked scene that is not a room's arrival is being set up (2 November 2026, user report: Indy 3's Give Grail entered Room 82 and
    /// replayed its first arrival, the beheading, whose head stayed on the floor through the ending). A room entered meanwhile has its arrival cutscenes
    /// counted as seen: a flag that a cutscene the entry starts tests first, and that the game raises elsewhere, is raised before the entry runs
    /// (Room 82 local script 200 +0x0005 tests <c>bit[0x5E2]</c>, which Script 122 +0x0233 sets as the hero comes back with the Grail).
    /// </summary>
    public bool ArrivalScenesSeen { get; set; }

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

    private static readonly ConditionalWeakTable<GameLibrary, Lazy<GameScriptFacts>> ScriptFacts = new();

    private GameScriptFacts Facts() => ScriptFacts.GetValue(_game, game => new Lazy<GameScriptFacts>(() => ReadScriptFacts(game, _data))).Value;
    private static readonly Regex FlagMove = new(@"^((?:bit|var)\[0x[0-9A-F]+\]) = (0x[0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex BranchTarget = new(@"-> 0x([0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex FlagGuard = new(@"^((?:bit|var)\[0x([0-9A-F]+)\]),", RegexOptions.CultureInvariant);

    private void MarkArrivalScenesSeen(int roomId)
    {
        ScummScriptDisassembly? entry;
        try { entry = _data.RoomEntryScript(roomId); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { return; }
        if (entry is null) return;
        var locals = _data.RoomLocalScriptIds(roomId).ToHashSet();
        var started = entry.Instructions.Where(i => i.Operation == "startScript")
            .Select(i => int.TryParse(i.Operands.Split(',')[0].Trim().Replace("0x", "", StringComparison.Ordinal), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) ? id : -1)
            .Where(locals.Contains).Distinct();
        var raised = Facts().RaisedFlags;
        foreach (var id in started)
        {
            ScummScriptDisassembly? script;
            try { script = _data.RoomLocalScript(roomId, id); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { continue; }
            if (script is null || !script.Instructions.Any(i => i.Operation == "cutscene")) continue;
            // The guards that mark an arrival as seen: one just inside the arrival's cutscene (Indy 3 Room 82's local script 200: cutscene, then
            // bit[0x5E2] at +0x0005), or a test of a flag the script then sets itself just before its cutscene begins (the "first time" pattern:
            // Monkey Island 2 Room 50's local script 202 +0x003C, bit[0x058]). A test before any cutscene that leads elsewhere is a story state, not
            // a "seen" mark (the same script's bit[0x05B] at +0x0000 plays another state), and is left alone.
            var instructions = script.Instructions;
            var first = instructions.ToList().FindIndex(i => i.Operation == "cutscene");
            var guards = instructions.Select((instruction, index) => (instruction, index)).Where(item => item.instruction.Operation is "notEqualZero" or "equalZero" &&
                (item.index > first && item.index <= first + 3 ||
                 FlagGuard.Match(item.instruction.Operands) is { Success: true } tested &&
                 instructions.Skip(item.index + 1).Take(2).Any(next => next.Operation == "move" && next.Operands.StartsWith(tested.Groups[1].Value + " =", StringComparison.Ordinal)) &&
                 instructions.Skip(item.index + 1).Take(4).Any(next => next.Operation == "cutscene")))
                .Select(item => item.instruction);
            foreach (var guard in guards)
            {
                if (FlagGuard.Match(guard.Operands) is not { Success: true } match) continue;
                // A test that loops back waits for the flag (Monkey Island 1 Room 80's local script 202 +0x0001 waits for bit[0x089], then plays):
                // raising it would start that scene, not count it as seen.
                if (BranchTarget.Match(guard.Operands) is { Success: true } target &&
                    int.Parse(target.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) <= guard.Offset) continue;
                var flag = match.Groups[1].Value;
                // Story variables only: a bit, or a variable above the engine's own.
                if (flag.StartsWith("var[", StringComparison.Ordinal) && int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) < 0x80) continue;
                // The value the script itself marks it with, else one the game sets elsewhere.
                var marked = instructions.FirstOrDefault(i => i.Operation == "move" && i.Operands.StartsWith(flag + " =", StringComparison.Ordinal) &&
                    FlagMove.Match(i.Operands) is { Success: true } m && m.Groups[2].Value != "0x0000");
                int value;
                if (marked is not null) value = int.Parse(FlagMove.Match(marked.Operands).Groups[2].Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else if (!raised.TryGetValue(flag, out value)) continue;
                var where = $"Room {roomId} local script {id} +0x{guard.Offset:X4}";
                if (ApplySetupEdit(new ScummSetupEdit(flag.StartsWith("bit[", StringComparison.Ordinal) ? ScummSetupKind.Bit : ScummSetupKind.Variable, flag, value, where)))
                    _uncertainties.Add($"A picked scene enters Room {roomId:00}: its arrival cutscene ({where} tests {flag}) counts as seen, so {flag} = {value}, as the game sets it.");
            }
        }
    }

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

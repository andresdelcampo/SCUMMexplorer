using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A script a room's entry starts that only keeps the room moving (R-8.5b, 20 October 2026): its literal start and why it counts.</summary>
public sealed record ScummAmbientScript(int ScriptId, IReadOnlyList<int> Arguments, int StartOffset, string Evidence);

/// <summary>A script the room's entry starts that is not ambient, and the instruction that rules it out.</summary>
public sealed record ScummAmbientRejection(int ScriptId, string Reason);

/// <summary>
/// The ambient scripts of a room (R-8.5b, rule confirmed by the user on 20 October 2026: a manually picked room that cannot be prepared live may start
/// only its permanent animations on entering, never its other entry scripts). Decided from the game's own code, the same way for every family:
/// a script is ambient when the room's entry script starts it with a literal number and literal arguments, and the code it can reach (itself and
/// the scripts it starts, followed through their branches) loops back, works on the picture or the sound (object states and images, actors,
/// palette, sounds), and contains nothing that tells the story: no cutscene or override, no printed line, no room load or camera move, no change
/// to the cursor, the input or the verbs, no object verb or sentence, no inventory change, no freezing of other scripts. A branch that is never
/// taken still counts, so the classification errs on the side of not ambient. A static check over the instructions, not a run.
/// </summary>
public sealed class ScummAmbientScripts
{
    // Operations that tell the story or change what the player can do; any of them in the reachable code rules a script out.
    private static readonly HashSet<string> StoryOperations = new(StringComparer.Ordinal)
    {
        "cutscene", "endCutscene", "beginOverride", "endOverride", "print", "printEgo", "cursorCommand", "verbOps", "saveVerbs", "restoreVerbs",
        "deleteVerbs", "loadRoom", "loadRoomWithEgo", "startObject", "doSentence", "panCameraTo", "setCameraAt", "actorFollowCamera", "freezeScripts",
        "pickupObject", "setOwnerOf", "waitForSentence", "restart", "systemOps", "saveRestoreVerbs", "saveLoadGame", "saveLoadVars", "quitPauseRestart",
        "cursor", "userput", "stopObjectScript"
    };

    // Operations that change the picture or the sound; ambient code does at least one of them.
    private static readonly HashSet<string> AmbientOperations = new(StringComparer.Ordinal)
    {
        "setState", "drawObject", "animateActor", "actorOps", "putActor", "putActorInRoom", "putActorAtObject", "walkActorTo", "walkActorToActor",
        "walkActorToObject", "faceActor", "startSound", "startMusic", "stopSound", "stopMusic", "soundKludge", "roomOps", "setClass", "lights"
    };

    private const int MaxDepth = 8;

    private readonly IScummDataSource _data;
    private readonly int _room;

    public int RoomId => _room;
    public IReadOnlyList<ScummAmbientScript> Ambient { get; }
    public IReadOnlyList<ScummAmbientRejection> Rejected { get; }

    private ScummAmbientScripts(IScummDataSource data, int room)
    {
        _data = data;
        _room = room;
        var ambient = new List<ScummAmbientScript>();
        var rejected = new List<ScummAmbientRejection>();
        var entry = data.RoomEntryScript(room);
        foreach (var start in entry?.Instructions.Where(i => i.Operation is "startScript" or "chainScript") ?? [])
        {
            var literal = Regex.Match(start.Operands, @"^0x([0-9A-F]+)", RegexOptions.IgnoreCase);
            if (!literal.Success) continue; // A computed script number cannot be told apart from the story's.
            var id = int.Parse(literal.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (ambient.Any(a => a.ScriptId == id) || rejected.Any(r => r.ScriptId == id)) continue;
            if (Arguments(start.Operands) is not { } arguments)
            {
                rejected.Add(new(id, $"entry +0x{start.Offset:X4} {start.Operation} passes arguments that are not literal"));
                continue;
            }
            var verdict = Inspect(id);
            if (verdict.Reason is not null) rejected.Add(new(id, verdict.Reason));
            else ambient.Add(new(id, arguments, start.Offset,
                $"Room {room:00}'s entry starts script {id} at +0x{start.Offset:X4}; its code loops ({verdict.Loop}) and only {string.Join(", ", verdict.Works.Order())}"));
        }
        Ambient = ambient;
        Rejected = rejected;
    }

    /// <summary>Classifies the scripts <paramref name="room"/>'s entry script starts.</summary>
    public static ScummAmbientScripts Classify(IScummDataSource data, int room)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ScummAmbientScripts(data, room);
    }

    public bool IsAmbient(int scriptId) => Ambient.Any(item => item.ScriptId == scriptId);

    private ScummScriptDisassembly? Script(int number) =>
        number < _data.GlobalScriptCount ? _data.GlobalScript(number) : _data.RoomLocalScript(_room, number);

    private (string? Reason, string? Loop, HashSet<string> Works) Inspect(int id)
    {
        var works = new HashSet<string>(StringComparer.Ordinal);
        string? loop = null;
        var visited = new HashSet<int>();
        var path = new HashSet<int>();
        string? Walk(int number, int depth)
        {
            if (path.Contains(number)) { loop ??= $"script {number} starts itself again"; return null; }
            if (!visited.Add(number)) return null;
            if (depth > MaxDepth) return $"script {number} is started more than {MaxDepth} scripts deep";
            ScummScriptDisassembly? script;
            try { script = Script(number); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return $"script {number} could not be read: {ex.Message}"; }
            if (script is null) return $"script {number} is not in the game";
            if (!script.HasCompleteControlFlow || script.UndecodedOffset is not null) return $"script {number} is not fully decoded";
            path.Add(number);
            try { return WalkScript(number, script, depth); }
            finally { path.Remove(number); }
        }
        string? WalkScript(int number, ScummScriptDisassembly script, int depth)
        {
            var byOffset = script.Instructions.ToDictionary(i => i.Offset);
            var pending = new Stack<int>();
            var seen = new HashSet<int>();
            pending.Push(script.EntryOffset);
            while (pending.TryPop(out var offset))
            {
                if (!seen.Add(offset) || !byOffset.TryGetValue(offset, out var instruction)) continue;
                var at = $"script {number} +0x{offset:X4} {instruction.Operation}";
                if (StoryOperations.Contains(instruction.Operation)) return $"{at}: tells the story or changes the player's controls";
                if (instruction.Operation == "waitFor" && instruction.Operands == "message") return $"{at} message: waits for a printed line";
                if (AmbientOperations.Contains(instruction.Operation)) works.Add(instruction.Operation);
                if (instruction.Operation is "startScript" or "chainScript")
                {
                    var literal = Regex.Match(instruction.Operands, @"^0x([0-9A-F]+)", RegexOptions.IgnoreCase);
                    if (!literal.Success) return $"{at}: starts a script whose number is computed";
                    var called = int.Parse(literal.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    if (Walk(called, depth + 1) is { } inner) return inner;
                    if (instruction.Operation == "chainScript") continue;
                }
                if (instruction.Operation == "stopObjectCode") continue;
                var next = offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var jump = Regex.Match(instruction.Operands, @"->\s*0x([0-9A-F]+)", RegexOptions.IgnoreCase);
                if (jump.Success)
                {
                    var target = int.Parse(jump.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    if (target <= offset) loop ??= $"script {number} +0x{offset:X4} jumps back to +0x{target:X4}";
                    pending.Push(target);
                    if (instruction.Operation == "jumpRelative") continue;
                }
                pending.Push(next);
            }
            return null;
        }
        if (Walk(id, 0) is { } reason) return (reason, null, works);
        if (loop is null) return ($"script {id} ends: it does not loop", null, works);
        if (works.Count == 0) return ($"script {id} changes neither the picture nor the sound", null, works);
        return (null, loop, works);
    }

    // The literal values of a start's argument list ("0x0C, [1, 0x02]"), empty when it has none; null when one is not literal.
    private static IReadOnlyList<int>? Arguments(string operands)
    {
        var list = Regex.Match(operands, @"\[(.*)\]");
        if (!list.Success || list.Groups[1].Value.Trim().Length == 0) return [];
        var values = new List<int>();
        foreach (var item in list.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (item.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(item[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) values.Add(hex);
            else if (int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) values.Add(number);
            else return null;
        }
        return values;
    }
}

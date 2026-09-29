using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>What a <c>loadRoom</c> arrival does with the ego after the load.</summary>
public enum LoomEgoArrivalStepKind
{
    /// <summary><c>putActor ego, x, y</c>.</summary>
    Put,
    /// <summary><c>putActorAtObject ego, obj</c>: the object's walk point (<see cref="LoomEgoArrivalStep.A"/>).</summary>
    PutAtObject,
    /// <summary><c>walkActorTo ego, x, y</c>.</summary>
    Walk,
}

/// <summary>One ego step after a <c>loadRoom</c> arrival, with its offset in the source.</summary>
public sealed record LoomEgoArrivalStep(LoomEgoArrivalStepKind Kind, int A, int B, int Offset)
{
    public string Describe() => Kind switch
    {
        LoomEgoArrivalStepKind.Put => $"put at ({A}, {B})",
        LoomEgoArrivalStepKind.PutAtObject => $"put at object {A}",
        _ => $"walks to ({A}, {B})",
    };
}

/// <summary>
/// One decoded arrival of the ego with literal operands. Either a
/// <c>loadRoomWithEgo</c>: the ego enters <see cref="TargetRoom"/> at
/// <see cref="ObjectId"/>'s walk point, then walks to (<see cref="WalkX"/>,
/// <see cref="WalkY"/>) unless x is −1. Or a <c>loadRoom</c> followed, in
/// straight-line code, by <c>putActorInRoom</c> of <see cref="EgoActor"/> into
/// the same room and its <see cref="Steps"/> (puts and walks), with the last
/// <c>setCameraAt</c> on the way in <see cref="CameraX"/>. <see cref="SourceRoom"/>
/// is the room whose own code holds it (entry, exit, local script or object
/// verb); null for a global script.
/// </summary>
public sealed record LoomEgoArrival(int TargetRoom, int ObjectId, int WalkX, int WalkY,
    string Source, int Offset, int? SourceRoom)
{
    /// <summary>Steps after a <c>loadRoom</c>; null for a <c>loadRoomWithEgo</c>.</summary>
    public IReadOnlyList<LoomEgoArrivalStep>? Steps { get; init; }

    /// <summary>The literal actor a <c>loadRoom</c> arrival places (the ego only when <c>var[0x001]</c> holds it).</summary>
    public int? EgoActor { get; init; }

    /// <summary>The last literal <c>setCameraAt</c> between the <c>loadRoom</c> and the ego's last step.</summary>
    public int? CameraX { get; init; }

    public bool IsLoadRoomWithEgo => Steps is null;

    /// <summary>Arrivals with the same key place the ego the same way.</summary>
    public string PlacementKey => Steps is null
        ? $"object {ObjectId}, walk {WalkX},{WalkY}"
        : $"actor {EgoActor}: {string.Join("; ", Steps.Select(step => step.Describe()))}; camera {CameraX?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    /// <summary>What happens to the ego, without the source.</summary>
    public string DescribePlacement() => Steps is null
        ? $"object {ObjectId}, " + (WalkX == -1 ? "no walk" : $"walk to ({WalkX}, {WalkY})")
        : $"loadRoom, then actor {EgoActor} " + string.Join(", then ", Steps.Select(step => step.Describe())) +
          (CameraX is int camera ? $"; setCameraAt {camera}" : "");

    public string Describe() => $"{Source} +0x{Offset:X4} ({DescribePlacement()})";
}

/// <summary>A decoded <c>startScript</c> with a literal script number and the text of its first argument.</summary>
public sealed record LoomScriptStart(string Source, int? SourceRoom, bool IsRoomEntry, int Offset, int Script, string? FirstArgument);

/// <summary>
/// Every decoded <c>loadRoomWithEgo</c> with a literal object and room, and every
/// literal <c>loadRoom</c> that straight-line code follows with
/// <c>putActorInRoom</c> of the ego (a literal actor or <c>var[0x001]</c>) into
/// that room and a literal <c>putActor</c> or <c>putActorAtObject</c>, across
/// global scripts, room entry, exit and local scripts, and object verbs. These
/// are possible arrivals: the conditions that lead to each one are not evaluated.
/// Arrivals whose object is not in the target room's directory are left out.
/// </summary>
public sealed class LoomEgoArrivalIndex
{
    private static readonly ConcurrentDictionary<string, LoomEgoArrivalIndex> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex LiteralOperands = new(
        @"^0x(?<object>[0-9A-Fa-f]+), 0x(?<room>[0-9A-Fa-f]+), walkTo=\((?<x>-?\d+), (?<y>-?\d+)\)$",
        RegexOptions.CultureInvariant);
    private readonly Dictionary<int, List<LoomEgoArrival>> _byRoom = [];
    private readonly List<LoomScriptStart> _starts = [];

    private LoomEgoArrivalIndex() { }

    /// <summary>The literal arrivals into <paramref name="room"/>, in source order.</summary>
    public IReadOnlyList<LoomEgoArrival> Into(int room) =>
        _byRoom.TryGetValue(room, out var list) ? list : [];

    /// <summary>
    /// The literal <c>startScript</c>s of <paramref name="script"/> in the same code as the arrivals: global scripts,
    /// room entry, exit and local scripts, and object verbs, in source order.
    /// </summary>
    public IEnumerable<LoomScriptStart> StartsOf(int script) => _starts.Where(item => item.Script == script);

    public static LoomEgoArrivalIndex For(GameLibrary game, int globalScriptCount)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Cache.GetOrAdd($"{game.FolderPath}|{game.Id}", _ => Build(game, globalScriptCount));
    }

    private static LoomEgoArrivalIndex Build(GameLibrary game, int globalScriptCount)
    {
        var index = new LoomEgoArrivalIndex();
        var dialect = ScummScriptDialects.For(game.Id);
        var reader = new LoomScriptReader(dialect);
        var verbs = new LoomObjectVerbReader(dialect);
        var directory = new LoomRoomObjectDirectory();
        var objectsByRoom = new Dictionary<int, HashSet<int>>();
        var found = new List<LoomEgoArrival>();

        void Scan(string source, int? sourceRoom, Func<LoomScriptDisassembly?> decode)
        {
            LoomScriptDisassembly? script;
            try { script = decode(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
            if (script is null) return;
            var instructions = script.Instructions;
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.Operation == "startScript" && StartOf(instruction, source, sourceRoom) is { } start)
                {
                    index._starts.Add(start);
                    continue;
                }
                if (instruction.Operation == "loadRoom")
                {
                    if (FollowLoadRoom(instructions, i, source, sourceRoom) is { } followed) found.Add(followed);
                    continue;
                }
                if (instruction.Operation != "loadRoomWithEgo") continue;
                var match = LiteralOperands.Match(instruction.Operands);
                if (!match.Success) continue;
                found.Add(new LoomEgoArrival(
                    int.Parse(match.Groups["room"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["object"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                    source, instruction.Offset, sourceRoom));
            }
        }

        foreach (var script in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                     .OrderBy(item => item.ResourceId))
            Scan($"Script {script.ResourceId:000}", null, () => reader.Decode(script));
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue).OrderBy(item => item.ResourceId))
        {
            var id = room.ResourceId!.Value;
            byte[] bytes;
            IReadOnlyList<LoomRoomObjectRecord> records;
            try
            {
                bytes = File.ReadAllBytes(room.FilePath);
                records = directory.Decode(bytes);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            objectsByRoom[id] = records.Select(item => item.Id).ToHashSet();
            Scan($"Room {id:00} entry", id, () => reader.DecodeRoomEntry(room));
            Scan($"Room {id:00} exit", id, () => reader.DecodeRoomExit(room));
            foreach (var local in LoomScriptReader.ReadRoomLocalScriptIds(bytes, globalScriptCount))
                Scan($"Room {id:00} local script {local}", id, () => reader.DecodeRoomLocalScript(room, local, globalScriptCount));
            IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> tables;
            try { tables = verbs.ReadAll(bytes, records); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            foreach (var record in records)
                if (tables.TryGetValue(record.Id, out var entries))
                    foreach (var group in entries.GroupBy(item => item.ScriptOffset))
                    {
                        var label = string.Join("/", group.Select(item => item.IsFallback ? "fallback" : $"0x{item.VerbId:X2}"));
                        Scan($"Room {id:00} Object {record.Id} Verb {label}", id, () => verbs.Decode(room, record, group.First().VerbId));
                    }
        }

        foreach (var arrival in found)
        {
            if (!objectsByRoom.TryGetValue(arrival.TargetRoom, out var objects)) continue;
            if (arrival.IsLoadRoomWithEgo ? !objects.Contains(arrival.ObjectId)
                    : arrival.Steps!.Any(step => step.Kind == LoomEgoArrivalStepKind.PutAtObject && !objects.Contains(step.A))) continue;
            if (!index._byRoom.TryGetValue(arrival.TargetRoom, out var list)) index._byRoom[arrival.TargetRoom] = list = [];
            list.Add(arrival);
        }
        return index;
    }

    private static LoomScriptStart? StartOf(LoomScriptInstruction instruction, string source, int? sourceRoom)
    {
        var operands = instruction.Operands;
        var open = LoomBoundedScriptExecutor.ArgumentListStart(operands);
        var head = (open < 0 ? operands : operands[..open]).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (head.Length == 0 || LiteralValue(head[0]) is not int script) return null;
        string? first = null;
        var close = operands.LastIndexOf(']');
        if (open >= 0 && close > open)
            first = operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return new LoomScriptStart(source, sourceRoom, source.EndsWith(" entry", StringComparison.Ordinal), instruction.Offset, script, first);
    }

    private static readonly Regex Literal = new(@"^0x(?<value>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);

    private static int? LiteralValue(string operand)
    {
        var match = Literal.Match(operand.Trim());
        return match.Success ? int.Parse(match.Groups["value"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// A literal <c>loadRoom R</c> followed, without a jump or branch, another load
    /// or the end of the script, by <c>putActorInRoom A, R</c> for a literal actor
    /// (or <c>var[0x001]</c>) and literal puts and walks of that actor. Scanning
    /// stops at anything else that moves that actor, or an actor instruction whose
    /// actor is not a literal. The camera is the last literal <c>setCameraAt</c>
    /// before the actor's last step.
    /// </summary>
    private static LoomEgoArrival? FollowLoadRoom(IReadOnlyList<LoomScriptInstruction> instructions, int loadIndex,
        string source, int? sourceRoom)
    {
        if (LiteralValue(instructions[loadIndex].Operands) is not int room) return null;
        int? actor = null;
        int? camera = null, cameraAtLastStep = null;
        var steps = new List<LoomEgoArrivalStep>();
        for (var i = loadIndex + 1; i < instructions.Count; i++)
        {
            var instruction = instructions[i];
            var operation = instruction.Operation;
            if (operation is "jumpRelative" or "stopObjectCode" or "chainScript" or "loadRoom" or "loadRoomWithEgo" ||
                instruction.Operands.Contains("branch ", StringComparison.Ordinal))
                break;
            if (operation == "setCameraAt")
            {
                camera = LiteralValue(instruction.Operands);
                continue;
            }
            if (operation is not ("putActorInRoom" or "putActor" or "putActorAtObject" or "walkActorTo" or
                "walkActorToObject" or "walkActorToActor" or "actorOps" or "animateActor" or "faceActor"))
                continue;
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            var first = operation == "actorOps" ? instruction.Operands[..Math.Max(0, instruction.Operands.IndexOf(", [", StringComparison.Ordinal))] : parts[0];
            int? number = first == "var[0x001]" ? 1 : LiteralValue(first);
            if (number is null)
            {
                // An actor the index cannot read could be the ego.
                if (operation is "animateActor" or "faceActor") continue;
                break;
            }
            if (actor is null)
            {
                // The ego enters the room: putActorInRoom of actor 1 (var[0x001] after the boot).
                if (operation == "putActorInRoom" && number == 1 && parts.Length > 1 && LiteralValue(parts[1]) == room)
                    actor = number;
                continue;
            }
            if (number != actor) continue;
            if (operation is "animateActor" or "faceActor") continue;
            if (operation == "actorOps")
            {
                if (instruction.Operands.Contains("ignoreBoxes", StringComparison.Ordinal) ||
                    instruction.Operands.Contains("followBoxes", StringComparison.Ordinal) ||
                    instruction.Operands.Contains("init", StringComparison.Ordinal)) break;
                continue;
            }
            LoomEgoArrivalStep? step = operation switch
            {
                "putActor" when parts.Length == 3 && LiteralValue(parts[1]) is int x && LiteralValue(parts[2]) is int y =>
                    new(LoomEgoArrivalStepKind.Put, x, y, instruction.Offset),
                "putActorAtObject" when parts.Length == 2 && LiteralValue(parts[1]) is int obj =>
                    new(LoomEgoArrivalStepKind.PutAtObject, obj, 0, instruction.Offset),
                "walkActorTo" when parts.Length == 3 && LiteralValue(parts[1]) is int x && LiteralValue(parts[2]) is int y =>
                    new(LoomEgoArrivalStepKind.Walk, x, y, instruction.Offset),
                _ => null,
            };
            if (step is null) break;
            // A walk before any put starts from wherever the route left the ego.
            if (step.Kind == LoomEgoArrivalStepKind.Walk && steps.Count == 0) break;
            steps.Add(step);
            cameraAtLastStep = camera;
        }
        if (actor is null || steps.Count == 0) return null;
        return new LoomEgoArrival(room, 0, -1, -1, source, instructions[loadIndex].Offset, sourceRoom)
        {
            Steps = steps,
            EgoActor = actor,
            CameraX = cameraAtLastStep,
        };
    }
}

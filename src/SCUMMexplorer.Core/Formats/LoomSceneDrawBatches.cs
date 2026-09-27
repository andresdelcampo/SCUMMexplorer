using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// Collects the <c>drawObject</c> runs stored in a game's scripts, grouped by the
/// room that owns the drawn objects. A run ends at a frame break, a jump or
/// branch, a stop or a room load, so its images appear together on screen.
/// The draws directly after a script's first literal <c>loadRoom</c> of a room
/// are that room's opening frame, the strongest static evidence of what a first
/// visit shows. A later load of the same room in the same script is a return to
/// it (LOOM script 68 shows Room 56 as 779 and 782, and only later as 781). These
/// batches are static candidates used to close gaps; they do not prove which
/// branch runs.
/// The same scan also finds interface images: objects whose image a
/// <c>verbOps ... image</c> call shows in the verb area below the room (LOOM's
/// distaff and the close-up of the selected object). ScummVM copies such an
/// image into a verb (<c>setVerbObject</c>); it is not room artwork.
/// </summary>
public sealed class LoomSceneDrawBatches
{
    public const int OpeningFramePriority = 3;
    public const int EntryScriptPriority = 2;
    public const int OtherScriptPriority = 1;

    private static readonly Regex Literal = new(@"^0x(?<value>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex VerbImage = new(@"image, (?<operand>0x[0-9A-Fa-f]+|var\[0x[0-9A-Fa-f]+\])",
        RegexOptions.CultureInvariant);
    private static readonly Regex LiteralAssignment = new(@"^(?<variable>var\[0x[0-9A-Fa-f]+\]) = 0x(?<value>[0-9A-Fa-f]+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex BranchTarget = new(@"->\s*0x(?<target>[0-9A-Fa-f]+)", RegexOptions.CultureInvariant);
    private readonly IReadOnlyDictionary<int, IReadOnlyList<RoomDrawBatch>> _byRoom;
    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>> _entryStates;

    private LoomSceneDrawBatches(IReadOnlyDictionary<int, IReadOnlyList<RoomDrawBatch>> byRoom,
        IReadOnlyDictionary<int, string> interfaceImages,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>> entryStates)
    {
        _byRoom = byRoom;
        InterfaceImages = interfaceImages;
        _entryStates = entryStates;
    }

    public static LoomSceneDrawBatches Empty { get; } = new(new Dictionary<int, IReadOnlyList<RoomDrawBatch>>(),
        new Dictionary<int, string>(), new Dictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>>());

    /// <summary>
    /// States that a room's entry script sets on every path through it (see
    /// <see cref="FindUnavoidableStates"/>), keyed by object.
    /// </summary>
    public IReadOnlyDictionary<int, RoomStateEvidence> EntryStatesForRoom(int roomId) =>
        _entryStates.TryGetValue(roomId, out var states) ? states : new Dictionary<int, RoomStateEvidence>();

    /// <summary>
    /// Objects whose image decoded code shows as a verb image, with the
    /// instruction that establishes it. A literal operand names the object
    /// directly; a global-variable operand names every literal assigned to
    /// that variable anywhere in decoded code. Local-variable operands are not
    /// followed.
    /// </summary>
    public IReadOnlyDictionary<int, string> InterfaceImages { get; }

    public IReadOnlyList<RoomDrawBatch> ForRoom(int roomId) =>
        _byRoom.TryGetValue(roomId, out var batches) ? batches : [];

    public static LoomSceneDrawBatches Build(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != ScummVersion.V3) return Empty;
        var dialect = ScummScriptDialects.For(game.Id);
        var reader = new LoomScriptReader(dialect);
        var verbReader = new LoomObjectVerbReader(dialect);
        var directory = new LoomRoomObjectDirectory();

        var roomByObject = new Dictionary<int, int>();
        var roomRecords = new Dictionary<int, (GameResource Room, IReadOnlyList<LoomRoomObjectRecord> Records, byte[] Bytes)>();
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            try
            {
                var bytes = File.ReadAllBytes(room.FilePath);
                var records = directory.Decode(bytes);
                roomRecords[room.ResourceId!.Value] = (room, records, bytes);
                foreach (var record in records) roomByObject.TryAdd(record.Id, room.ResourceId.Value);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }

        var byRoom = new Dictionary<int, List<RoomDrawBatch>>();
        var decoded = new List<(LoomScriptDisassembly Script, string Source)>();
        void Scan(LoomScriptDisassembly? script, int priority, string source)
        {
            if (script is null) return;
            decoded.Add((script, source));
            foreach (var (roomId, batch) in ScanScript(script, priority, source, roomByObject))
            {
                if (!byRoom.TryGetValue(roomId, out var list)) byRoom[roomId] = list = [];
                list.Add(batch);
            }
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            try { Scan(reader.Decode(resource), OtherScriptPriority, $"script {resource.ResourceId:00}"); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
        }

        var globalCount = ReadGlobalScriptCount(game);
        var entryStates = new Dictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>>();
        foreach (var (roomId, (room, records, bytes)) in roomRecords)
        {
            try
            {
                var entry = reader.DecodeRoomEntry(room);
                Scan(entry, EntryScriptPriority, $"Room {roomId:00} entry script");
                if (entry is not null)
                {
                    var states = FindUnavoidableStates(entry, $"Room {roomId:00} entry script");
                    if (states.Count > 0) entryStates[roomId] = states;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            foreach (var localId in ReadLocalScriptIds(bytes, globalCount))
            {
                try { Scan(reader.DecodeRoomLocalScript(room, localId, globalCount), OtherScriptPriority,
                    $"Room {roomId:00} local script {localId}"); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            }
            IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> tables;
            try { tables = verbReader.ReadAll(bytes, records); }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { continue; }
            foreach (var record in records)
            {
                if (!tables.TryGetValue(record.Id, out var verbs)) continue;
                foreach (var verb in verbs.DistinctBy(item => item.ScriptOffset))
                {
                    try { Scan(verbReader.Decode(room, record, verb.VerbId), OtherScriptPriority,
                        $"Room {roomId:00} object {record.Id} {verb.Label}"); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
                }
            }
        }

        return new LoomSceneDrawBatches(byRoom.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<RoomDrawBatch>)pair.Value), FindInterfaceImages(decoded), entryStates);
    }

    /// <summary>
    /// Finds literal <c>setState</c> calls that every control path from the
    /// start of the script to its end must pass, so they hold whichever branch
    /// runs. An object qualifies only when the script sets it exactly once; a
    /// <c>setState</c> with a variable object makes the whole script ineligible.
    /// A path ends at <c>stopObjectCode</c>, <c>stopScript 0x00</c>,
    /// <c>chainScript</c>, the end of the code or an unreadable branch target.
    /// This is static evidence for the state right after the script, not proof
    /// that later scripts leave it unchanged.
    /// </summary>
    internal static IReadOnlyDictionary<int, RoomStateEvidence> FindUnavoidableStates(LoomScriptDisassembly script,
        string source)
    {
        var result = new Dictionary<int, RoomStateEvidence>();
        var instructions = script.Instructions;
        var sets = new List<(int Index, int ObjectId, int State)>();
        for (var index = 0; index < instructions.Count; index++)
        {
            if (instructions[index].Operation != "setState") continue;
            var parts = instructions[index].Operands.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !TryReadLiteral(parts[0], out var objectId) ||
                !TryReadLiteral(parts[1], out var state) || state > byte.MaxValue)
                return result;
            sets.Add((index, objectId, state));
        }
        if (sets.Count == 0) return result;

        var indexByOffset = new Dictionary<int, int>();
        for (var index = 0; index < instructions.Count; index++) indexByOffset[instructions[index].Offset] = index;

        // Successor indices; -1 is an exit.
        IEnumerable<int> Next(int index)
        {
            var instruction = instructions[index];
            if (instruction.Operation is "stopObjectCode" or "chainScript" ||
                instruction.Operation == "stopScript" && TryReadLiteral(instruction.Operands, out var stopped) && stopped == 0)
            {
                yield return -1;
                yield break;
            }
            var match = BranchTarget.Match(instruction.Operands);
            var target = match.Success && indexByOffset.TryGetValue(
                int.Parse(match.Groups["target"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), out var found)
                ? found : -1;
            if (instruction.Operation == "jumpRelative")
            {
                yield return target;
                yield break;
            }
            yield return index + 1 < instructions.Count ? index + 1 : -1;
            if (instruction.Operands.Contains("branch", StringComparison.Ordinal)) yield return target;
        }

        bool ExitReachableAvoiding(int avoided)
        {
            var seen = new HashSet<int>();
            var work = new Stack<int>();
            work.Push(0);
            while (work.Count > 0)
            {
                var index = work.Pop();
                if (index == -1) return true;
                if (index == avoided || !seen.Add(index)) continue;
                foreach (var next in Next(index)) work.Push(next);
            }
            return false;
        }

        foreach (var group in sets.GroupBy(item => item.ObjectId).Where(group => group.Count() == 1))
        {
            var (index, objectId, state) = group.Single();
            if (!ExitReachableAvoiding(index))
                result[objectId] = new RoomStateEvidence((byte)state,
                    $"{source} +0x{instructions[index].Offset:X4} sets it to {state} on every path");
        }
        return result;
    }

    internal static IReadOnlyDictionary<int, string> FindInterfaceImages(
        IReadOnlyList<(LoomScriptDisassembly Script, string Source)> sources)
    {
        var found = new Dictionary<int, string>();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (script, source) in sources)
            foreach (var instruction in script.Instructions.Where(item => item.Operation == "verbOps"))
                foreach (Match match in VerbImage.Matches(instruction.Operands))
                {
                    var operand = match.Groups["operand"].Value;
                    var where = $"verbOps image in {source} +0x{instruction.Offset:X4}";
                    if (TryReadLiteral(operand, out var id)) found.TryAdd(id, where);
                    else variables.TryAdd(operand, where);
                }
        if (variables.Count == 0) return found;
        foreach (var (script, source) in sources)
            foreach (var instruction in script.Instructions.Where(item => item.Operation == "move"))
            {
                var match = LiteralAssignment.Match(instruction.Operands.Trim());
                if (!match.Success || !variables.TryGetValue(match.Groups["variable"].Value, out var shownBy)) continue;
                var id = int.Parse(match.Groups["value"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (id != 0)
                    found.TryAdd(id, $"{match.Groups["variable"].Value} set in {source} +0x{instruction.Offset:X4}, shown by {shownBy}");
            }
        return found;
    }

    internal static IEnumerable<(int RoomId, RoomDrawBatch Batch)> ScanScript(LoomScriptDisassembly script,
        int priority, string source, IReadOnlyDictionary<int, int> roomByObject)
    {
        var results = new List<(int, RoomDrawBatch)>();
        var steps = new List<RoomDrawStep>();
        int? loadedRoom = null;
        var openedRooms = new HashSet<int>();
        var batchStart = 0;

        void Flush()
        {
            if (steps.Count > 0)
            {
                foreach (var group in steps.Where(step => roomByObject.ContainsKey(step.ObjectId))
                             .GroupBy(step => roomByObject[step.ObjectId]))
                {
                    var opening = loadedRoom == group.Key;
                    results.Add((group.Key, new RoomDrawBatch(group.ToArray(),
                        opening ? OpeningFramePriority : priority,
                        opening ? $"opening frame of Room {group.Key:00} in {source} +0x{batchStart:X4}"
                                : $"{source} +0x{batchStart:X4}")));
                }
            }
            steps.Clear();
        }

        foreach (var instruction in script.Instructions)
        {
            switch (instruction.Operation)
            {
                case "drawObject":
                    if (TryReadDraw(instruction.Operands, out var step))
                    {
                        if (steps.Count == 0) batchStart = instruction.Offset;
                        steps.Add(step);
                    }
                    break;
                case "loadRoom":
                case "loadRoomWithEgo":
                    Flush();
                    var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                    var roomOperand = instruction.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                    loadedRoom = TryReadLiteral(roomOperand, out var room) && openedRooms.Add(room) ? room : null;
                    break;
                case "breakHere":
                case "stopObjectCode":
                case "jumpRelative":
                case "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or
                    "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState":
                    Flush();
                    loadedRoom = null;
                    break;
                case "setState":
                    // A redraw separates what was drawn before from what follows.
                    Flush();
                    break;
            }
        }
        Flush();
        return results;
    }

    private static bool TryReadDraw(string operands, out RoomDrawStep step)
    {
        step = default;
        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !TryReadLiteral(parts[0], out var id)) return false;
        int? x = null, y = null;
        if (parts.Length >= 4 && parts[1] == "at" && TryReadLiteral(parts[2], out var tileX) &&
            TryReadLiteral(parts[3], out var tileY) && tileX != 0xFF)
        {
            x = tileX * 8;
            y = tileY * 8;
        }
        else if (parts.Length >= 4 && parts[1] == "at" && !(TryReadLiteral(parts[2], out _) && TryReadLiteral(parts[3], out _)))
            return false;
        step = new RoomDrawStep(id, x, y);
        return true;
    }

    private static bool TryReadLiteral(string text, out int value)
    {
        value = 0;
        var match = Literal.Match(text.Trim());
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out value);
    }

    private static int ReadGlobalScriptCount(GameLibrary game)
    {
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        try
        {
            if (File.Exists(indexPath)) return new LflIndexReader().ReadDeclaredScriptCount(indexPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException) { }
        return game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                   .Select(item => item.ResourceId!.Value).DefaultIfEmpty(-1).Max() + 1;
    }

    internal static IReadOnlyList<int> ReadLocalScriptIds(byte[] encoded, int globalScriptCount)
    {
        var data = LflEncoding.DecodeXorFF(encoded);
        if (data.Length < 29) return [];
        var roomEnd = data[0] | data[1] << 8;
        var cursor = 29 + data[20] * 4 + data[23] + data[24];
        if (roomEnd > data.Length || cursor >= roomEnd) return [];
        var found = new List<int>();
        while (cursor < roomEnd - 2 && data[cursor] != 0)
        {
            if (data[cursor] >= globalScriptCount) found.Add(data[cursor]);
            cursor += 3;
        }
        return found.Distinct().ToArray();
    }
}

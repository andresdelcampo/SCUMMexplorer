using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

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
public sealed class ScummSceneDrawBatches
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

    private ScummSceneDrawBatches(IReadOnlyDictionary<int, IReadOnlyList<RoomDrawBatch>> byRoom,
        IReadOnlyDictionary<int, string> interfaceImages,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>> entryStates)
    {
        _byRoom = byRoom;
        InterfaceImages = interfaceImages;
        _entryStates = entryStates;
    }

    public static ScummSceneDrawBatches Empty { get; } = new(new Dictionary<int, IReadOnlyList<RoomDrawBatch>>(),
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

    public static ScummSceneDrawBatches Build(GameLibrary game, IScummDataSource? data = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != ScummVersion.V3) return Empty;
        data ??= ScummDataSources.For(game);

        var roomByObject = new Dictionary<int, int>();
        var roomRecords = new Dictionary<int, IReadOnlyList<ScummRoomObjectRecord>>();
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            try
            {
                var records = data.ReadRoom(room.ResourceId!.Value).Objects;
                roomRecords[room.ResourceId!.Value] = records;
                foreach (var record in records) roomByObject.TryAdd(record.Id, room.ResourceId.Value);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }

        var rectByObject = new Dictionary<int, (int X, int Y, int Width, int Height)>();
        foreach (var records in roomRecords.Values)
            foreach (var record in records)
                rectByObject.TryAdd(record.Id, (record.X, record.Y, record.Width, record.Height));

        var byRoom = new Dictionary<int, List<RoomDrawBatch>>();
        var decoded = new List<(ScummScriptDisassembly Script, string Source)>();
        void Add(int roomId, RoomDrawBatch batch)
        {
            if (!byRoom.TryGetValue(roomId, out var list)) byRoom[roomId] = list = [];
            list.Add(batch);
        }
        void Scan(ScummScriptDisassembly? script, int priority, string source)
        {
            if (script is null) return;
            decoded.Add((script, source));
            foreach (var (roomId, batch) in ScanScript(script, priority, source, roomByObject))
                Add(roomId, batch);
            foreach (var batch in FindLocalTableRestFrames(script, source, rectByObject))
                foreach (var group in batch.Steps.Where(step => roomByObject.ContainsKey(step.ObjectId))
                             .GroupBy(step => roomByObject[step.ObjectId]))
                    Add(group.Key, batch with { Steps = group.ToArray() });
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            try { Scan(data.GlobalScript(resource.ResourceId!.Value), OtherScriptPriority, $"script {resource.ResourceId:00}"); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
        }

        var entryStates = new Dictionary<int, IReadOnlyDictionary<int, RoomStateEvidence>>();
        foreach (var (roomId, records) in roomRecords)
        {
            try
            {
                var entry = data.RoomEntryScript(roomId);
                Scan(entry, EntryScriptPriority, $"Room {roomId:00} entry script");
                if (entry is not null)
                {
                    var states = FindUnavoidableStates(entry, $"Room {roomId:00} entry script");
                    if (states.Count > 0) entryStates[roomId] = states;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            foreach (var localId in data.RoomLocalScriptIds(roomId))
            {
                try { Scan(data.RoomLocalScript(roomId, localId), OtherScriptPriority,
                    $"Room {roomId:00} local script {localId}"); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            }
            IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> tables;
            try { tables = data.ReadObjectVerbs(roomId, records); }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { continue; }
            foreach (var record in records)
            {
                if (!tables.TryGetValue(record.Id, out var verbs)) continue;
                foreach (var verb in verbs.DistinctBy(item => item.ScriptOffset))
                {
                    try { Scan(data.ReadObjectVerb(roomId, record, verb.VerbId, out _), OtherScriptPriority,
                        $"Room {roomId:00} object {record.Id} {verb.Label}"); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
                }
            }
        }

        foreach (var (script, source) in decoded)
            ApplyBoxErasures(script, source, byRoom, rectByObject);

        return new ScummSceneDrawBatches(byRoom.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<RoomDrawBatch>)pair.Value), FindInterfaceImages(decoded), entryStates);
    }

    private static readonly Regex LocalScriptSource = new(@"^Room (?<room>\d+) local script \d+$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A room-local script that stops another local script of its room and then paints a literal
    /// <c>drawBox</c> over an object's whole image removes that object from the stopped script's
    /// batches: the box blanks what the stopped script drew (Room 69 local 201 stops 202, the
    /// difficulty menu, and boxes over its three buttons). Objects the box only partly covers stay.
    /// </summary>
    private static void ApplyBoxErasures(ScummScriptDisassembly script, string source,
        Dictionary<int, List<RoomDrawBatch>> byRoom,
        IReadOnlyDictionary<int, (int X, int Y, int Width, int Height)> rectByObject)
    {
        var match = LocalScriptSource.Match(source);
        if (!match.Success || !byRoom.TryGetValue(int.Parse(match.Groups["room"].Value, CultureInfo.InvariantCulture), out var batches))
            return;
        var roomId = int.Parse(match.Groups["room"].Value, CultureInfo.InvariantCulture);
        var stopped = new HashSet<int>();
        foreach (var instruction in script.Instructions)
        {
            if (instruction.Operation == "stopScript" && TryReadLiteral(instruction.Operands, out var id) && id != 0)
                stopped.Add(id);
            if (instruction.Operation != "drawBox" || stopped.Count == 0) continue;
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 5) continue;
            var corners = new int[4];
            var literal = true;
            for (var i = 0; literal && i < 4; i++) literal = TryReadLiteral(parts[i], out corners[i]);
            if (!literal) continue;
            int left = Math.Min(corners[0], corners[2]), right = Math.Max(corners[0], corners[2]);
            int top = Math.Min(corners[1], corners[3]), bottom = Math.Max(corners[1], corners[3]);
            for (var b = batches.Count - 1; b >= 0; b--)
            {
                var batch = batches[b];
                if (!stopped.Any(id => batch.Source.Contains($"Room {roomId:00} local script {id} +0x", StringComparison.Ordinal)))
                    continue;
                var kept = batch.Steps.Where(step => !rectByObject.TryGetValue(step.ObjectId, out var r) ||
                    !(r.X >= left && r.Y >= top && r.X + r.Width - 1 <= right && r.Y + r.Height - 1 <= bottom)).ToArray();
                if (kept.Length == batch.Steps.Count) continue;
                if (kept.Length == 0) batches.RemoveAt(b);
                else batches[b] = batch with { Steps = kept };
            }
        }
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
    internal static IReadOnlyDictionary<int, RoomStateEvidence> FindUnavoidableStates(ScummScriptDisassembly script,
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
        IReadOnlyList<(ScummScriptDisassembly Script, string Source)> sources)
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

    internal static IEnumerable<(int RoomId, RoomDrawBatch Batch)> ScanScript(ScummScriptDisassembly script,
        int priority, string source, IReadOnlyDictionary<int, int> roomByObject)
    {
        var results = new List<(int, RoomDrawBatch)>();
        var steps = new List<RoomDrawStep>();
        int? loadedRoom = null;
        var openedRooms = new HashSet<int>();
        var batchStart = 0;

        void Flush(int end)
        {
            if (steps.Count > 0)
            {
                foreach (var group in steps.Where(step => roomByObject.ContainsKey(step.ObjectId) &&
                                                          !UndoneLater(script.Instructions, end, step.ObjectId))
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

        for (var index = 0; index < script.Instructions.Count; index++)
        {
            var instruction = script.Instructions[index];
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
                    Flush(index);
                    var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                    var roomOperand = instruction.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                    loadedRoom = TryReadLiteral(roomOperand, out var room) && openedRooms.Add(room) ? room : null;
                    break;
                case "breakHere":
                case "stopObjectCode":
                case "jumpRelative":
                case "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or
                    "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState":
                    Flush(index);
                    loadedRoom = null;
                    break;
                case "setState":
                    // A redraw separates what was drawn before from what follows.
                    Flush(index);
                    break;
            }
        }
        Flush(script.Instructions.Count);
        return results;
    }

    /// <summary>
    /// True when the straight-line code after <paramref name="start"/> sets the
    /// object back to state 0 before any branch, jump, room load, stop or new
    /// draw of it (frame breaks and other commands may come between). Such a
    /// draw is a passing frame, such as script 90's flashes in Room 42, not
    /// something the room keeps showing.
    /// </summary>
    private static bool UndoneLater(IReadOnlyList<ScummScriptInstruction> instructions, int start, int objectId)
    {
        for (var index = start; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            switch (instruction.Operation)
            {
                case "setState":
                    var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && TryReadLiteral(parts[0], out var target) && target == objectId)
                        return TryReadLiteral(parts[1], out var state) && state == 0;
                    if (parts.Length != 2 || !TryReadLiteral(parts[0], out _)) return false;
                    break;
                case "drawObject":
                    if (!TryReadDraw(instruction.Operands, out var step) || step.ObjectId == objectId) return false;
                    break;
                case "stopObjectCode" or "jumpRelative" or "loadRoom" or "loadRoomWithEgo" or
                    "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or
                    "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState":
                    return false;
            }
        }
        return false;
    }

    private static readonly Regex LocalOperand = new(@"^local\[0x(?<number>[0-9A-Fa-f]+)\]$", RegexOptions.CultureInvariant);
    private static readonly Regex IndexedLocal = new(@"^local\[0x(?<base>[0-9A-Fa-f]+)\+local\[0x(?<index>[0-9A-Fa-f]+)\]\]$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LeadingLocal = new(@"^local\[0x(?<number>[0-9A-Fa-f]+)\](?:\s*(?:=|,)|$)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Finds draws whose object comes from a literal table in the script's own
    /// locals: <c>setVarRange local[..]</c> fills the table and a loop reads it
    /// through an indexed operand (<c>local[B+local[I]]</c>) up to a literal
    /// bound. Room 04 local script 201 animates its four fences this way.
    /// Each control path is followed with the literal locals it sets; a
    /// comparison whose operands are known is decided as ScummVM does
    /// (<c>jumpRelative</c> skips when the condition is false), any other
    /// branch is followed both ways. Locals that come from elsewhere
    /// (arguments, globals, results) stay unknown, and a path that draws an
    /// unknown local gives nothing. A path that draws at least one table entry
    /// gives one batch: its rest frame, the last image drawn at each position
    /// before the script ends, since <c>drawObject</c> sets the drawn object's
    /// state and clears the others at its position. It is a static candidate
    /// for what the script leaves on screen, not proof that the path runs.
    /// </summary>
    internal static IEnumerable<RoomDrawBatch> FindLocalTableRestFrames(ScummScriptDisassembly script, string source,
        IReadOnlyDictionary<int, (int X, int Y, int Width, int Height)> rectByObject)
    {
        var instructions = script.Instructions;
        if (!instructions.Any(item => item.Operation == "drawObject" && item.Operands.StartsWith("local[", StringComparison.Ordinal)) ||
            !instructions.Any(item => item.Operation == "setVarRange" && item.Operands.StartsWith("local[", StringComparison.Ordinal)))
            return [];
        var indexByOffset = new Dictionary<int, int>();
        for (var index = 0; index < instructions.Count; index++) indexByOffset[instructions[index].Offset] = index;

        const int stepBudget = 50_000;
        const int pathLimit = 256;
        var steps = 0;
        var results = new List<RoomDrawBatch>();
        var seenBatches = new HashSet<string>();
        var visited = new HashSet<string>();
        var work = new Stack<(int Index, Dictionary<int, (int Value, int TableOffset)> Locals, List<(int ObjectId, int Offset, int TableOffset)> Draws)>();
        work.Push((0, new(), new()));
        var paths = 0;

        bool TryValue(string operand, Dictionary<int, (int Value, int TableOffset)> locals, out int value, out int table)
        {
            value = 0;
            table = -1;
            if (TryReadLiteral(operand, out value)) { value = (short)value; return true; }
            var local = LocalOperand.Match(operand);
            if (local.Success && locals.TryGetValue(Hex(local.Groups["number"].Value), out var known))
            {
                (value, table) = known;
                return true;
            }
            var indexed = IndexedLocal.Match(operand);
            if (indexed.Success && locals.TryGetValue(Hex(indexed.Groups["index"].Value), out var at) &&
                locals.TryGetValue(Hex(indexed.Groups["base"].Value) + at.Value, out var entry))
            {
                (value, table) = entry;
                return true;
            }
            return false;
        }

        while (work.Count > 0 && paths < pathLimit)
        {
            var (index, locals, draws) = work.Pop();
            var ended = false;
            while (!ended)
            {
                if (++steps > stepBudget) return results;
                if (index < 0 || index >= instructions.Count) { ended = true; break; }
                var instruction = instructions[index];
                var operands = instruction.Operands;
                // A path that returns to a branch with the same locals would repeat
                // itself (or another path already went on from here): it has no
                // end of its own, so it gives no rest frame.
                if (instruction.Operation is "jumpRelative" || operands.Contains("branch", StringComparison.Ordinal))
                {
                    var key = $"{index}:{string.Join(";", locals.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value.Value}"))}";
                    if (!visited.Add(key)) { draws = null!; break; }
                }
                switch (instruction.Operation)
                {
                    case "stopObjectCode" or "chainScript":
                        ended = true;
                        continue;
                    case "stopScript" when TryReadLiteral(operands, out var stopped) && stopped == 0:
                        ended = true;
                        continue;
                    case "setVarRange":
                    {
                        var countAt = operands.IndexOf(", count=", StringComparison.Ordinal);
                        var open = operands.IndexOf('[', Math.Max(countAt, 0));
                        var destination = countAt < 0 ? null : LocalOperand.Match(operands[..countAt].Trim());
                        if (destination is { Success: true } && open > 0 && operands.EndsWith(']'))
                        {
                            var start = Hex(destination.Groups["number"].Value);
                            var values = operands[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries);
                            for (var item = 0; item < values.Length; item++)
                            {
                                if (TryReadLiteral(values[item], out var value)) locals[start + item] = ((short)value, instruction.Offset);
                                else locals.Remove(start + item);
                            }
                        }
                        index++;
                        continue;
                    }
                    case "move":
                    {
                        var parts = operands.Split(" = ", 2, StringSplitOptions.TrimEntries);
                        var target = LocalOperand.Match(parts[0]);
                        if (target.Success)
                        {
                            var number = Hex(target.Groups["number"].Value);
                            if (parts.Length == 2 && TryValue(parts[1], locals, out var value, out var table))
                                locals[number] = (value, table);
                            else locals.Remove(number);
                        }
                        index++;
                        continue;
                    }
                    case "increment" or "decrement":
                    {
                        var target = LocalOperand.Match(operands.Trim());
                        if (target.Success && locals.TryGetValue(Hex(target.Groups["number"].Value), out var known))
                            locals[Hex(target.Groups["number"].Value)] =
                                ((short)(known.Value + (instruction.Operation == "increment" ? 1 : -1)), known.TableOffset);
                        index++;
                        continue;
                    }
                    case "drawObject":
                    {
                        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
                        if (parts.Length == 0 || !TryValue(parts[0], locals, out var objectId, out var table))
                        {
                            draws = null!;
                            ended = true;
                            continue;
                        }
                        draws.Add((objectId, instruction.Offset, table));
                        index++;
                        continue;
                    }
                    case "jumpRelative":
                    {
                        var match = BranchTarget.Match(operands);
                        index = match.Success && indexByOffset.TryGetValue(Hex(match.Groups["target"].Value), out var found) ? found : -1;
                        if (index < 0) { draws = null!; ended = true; }
                        continue;
                    }
                }
                if (operands.Contains("branch", StringComparison.Ordinal))
                {
                    var match = BranchTarget.Match(operands);
                    if (!match.Success || !indexByOffset.TryGetValue(Hex(match.Groups["target"].Value), out var target))
                    {
                        draws = null!;
                        ended = true;
                        continue;
                    }
                    var parts = operands[..operands.IndexOf("branch", StringComparison.Ordinal)]
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    bool? condition = null;
                    if (parts.Length >= 1 && TryValue(parts[0], locals, out var a, out _))
                    {
                        if (instruction.Operation is "equalZero") condition = a == 0;
                        else if (instruction.Operation is "notEqualZero") condition = a != 0;
                        else if (parts.Length >= 2 && TryValue(parts[1], locals, out var b, out _))
                            // o5_isEqual and friends: a is the variable, b the second operand.
                            condition = instruction.Operation switch
                            {
                                "isEqual" => b == a,
                                "isNotEqual" => b != a,
                                "isLess" => b < a,
                                "isGreater" => b > a,
                                "isLessEqual" => b <= a,
                                "isGreaterEqual" => b >= a,
                                _ => null
                            };
                    }
                    if (condition is null)
                        work.Push((target, new(locals), new(draws)));
                    index = condition == false ? target : index + 1;
                    continue;
                }
                // Any other instruction that names a local first may write it (results such as getRandomNr).
                var written = LeadingLocal.Match(operands);
                if (written.Success && instruction.Operation is not ("startScript" or "print" or "printEgo"))
                    locals.Remove(Hex(written.Groups["number"].Value));
                index++;
            }
            paths++;
            if (draws is null || !draws.Any(draw => draw.TableOffset >= 0)) continue;

            // The rest frame: the last draw at each position, in draw order.
            var last = new Dictionary<(int, int, int, int), (int ObjectId, int Offset, int TableOffset, int Order)>();
            for (var order = 0; order < draws.Count; order++)
            {
                var draw = draws[order];
                if (!rectByObject.TryGetValue(draw.ObjectId, out var rect)) continue;
                last[rect] = (draw.ObjectId, draw.Offset, draw.TableOffset, order);
            }
            var rest = last.Values.OrderBy(item => item.Order).ToArray();
            if (rest.Length == 0) continue;
            var fromTable = rest.Where(item => item.TableOffset >= 0).ToArray();
            var reason = fromTable.Length == 0
                ? $"{source} +0x{rest[^1].Offset:X4}"
                : $"{source} +0x{fromTable[0].Offset:X4}, last frame from the table set at +0x{fromTable[0].TableOffset:X4}";
            var batch = new RoomDrawBatch(rest.Select(item => new RoomDrawStep(item.ObjectId, null, null)).ToArray(),
                OtherScriptPriority, reason);
            if (seenBatches.Add(string.Join(",", batch.Steps.Select(step => step.ObjectId)) + "|" + reason))
                results.Add(batch);
        }
        return results;
    }

    private static int Hex(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

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
}

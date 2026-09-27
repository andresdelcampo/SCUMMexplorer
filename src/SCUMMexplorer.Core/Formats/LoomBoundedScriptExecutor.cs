using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomBoundedScriptResult(
    IReadOnlyDictionary<int, byte> ObjectStates,
    IReadOnlyDictionary<int, RoomObjectPosition> ObjectPositions,
    IReadOnlyDictionary<string, ushort> KnownVariables,
    IReadOnlyList<string> Trace,
    IReadOnlyList<LoomScriptEvent> Events,
    int InstructionsVisited,
    bool ReachedStop,
    string? UnresolvedReason,
    int? ResumeOffset = null,
    IReadOnlyList<int>? DrawQueue = null)
{
    /// <summary>
    /// Objects drawn by <c>drawObject</c> since the last redraw-causing
    /// <c>setState</c>, in call order. SCUMM paints these over the room's
    /// directory-order redraw.
    /// </summary>
    public IReadOnlyList<int> DrawOrder => DrawQueue ?? [];

    public string FormatSummary()
    {
        var status = ResumeOffset.HasValue ? "paused at frame boundary" :
            UnresolvedReason is null ? "completed" : "partial / unresolved";
        var output = new List<string>
        {
            $"Bounded run: {status} · {InstructionsVisited} instructions visited",
            $"Object-state commands applied: {Trace.Count(line => line.StartsWith("object ", StringComparison.Ordinal))}",
        };
        output.AddRange(Trace.Take(100).Select(line => $"  {line}"));
        if (Trace.Count > 100) output.Add($"  … {Trace.Count - 100} more trace entries omitted");
        if (Events.Count > 0)
        {
            output.Add("Event trace:");
            output.AddRange(Events.Take(100).Select(item => $"  {item.Format()}"));
            if (Events.Count > 100) output.Add($"  … {Events.Count - 100} more events omitted");
        }
        if (ResumeOffset is int next) output.Add($"Pending: resume at +0x{next:X4} on a later frame.");
        if (UnresolvedReason is not null) output.Add($"Unresolved: {UnresolvedReason}");
        else if (!ReachedStop) output.Add("Unresolved: execution ended before stopObjectCode.");
        return string.Join(Environment.NewLine, output);
    }
}

/// <summary>
/// Executes a small, bounded subset of old-v3 bytecode for inspection. It only
/// commits object changes when the bytecode operands and the relevant path are known.
/// </summary>
public sealed class LoomBoundedScriptExecutor
{
    private const int DefaultInstructionLimit = 4096;
    private static readonly Regex VariablePattern = new(@"^(var|local|bit)\[0x([0-9A-Fa-f]+)\]$", RegexOptions.Compiled);

    public LoomBoundedScriptResult Execute(
        LoomScriptDisassembly script,
        IReadOnlyDictionary<int, byte> initialObjectStates,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects,
        int instructionLimit = DefaultInstructionLimit,
        LoomScriptRuntimeState? runtime = null,
        int startOffset = 0,
        IReadOnlyDictionary<string, ushort>? initialLocals = null,
        Func<LoomScriptEvent, LoomScriptRuntimeState, string?>? onEvent = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(initialObjectStates);
        ArgumentNullException.ThrowIfNull(roomObjects);
        if (instructionLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(instructionLimit));

        var states = runtime?.ObjectStates ?? new Dictionary<int, byte>(initialObjectStates);
        var positions = runtime?.ObjectPositions ?? new Dictionary<int, RoomObjectPosition>();
        foreach (var item in roomObjects)
            positions.TryAdd(item.Id, new RoomObjectPosition(item.X, item.Y));
        var knownVariables = runtime is null
            ? new Dictionary<string, ushort>(StringComparer.Ordinal)
            : new Dictionary<string, ushort>(runtime.GlobalVariables, StringComparer.Ordinal);
        for (var index = 0; index < 26; index++)
        {
            var key = $"local[0x{index:X3}]";
            knownVariables[key] = initialLocals is not null && initialLocals.TryGetValue(key, out var value)
                ? value : (ushort)0;
        }
        var drawQueue = runtime?.DrawQueue ?? new List<int>();
        var trace = new List<string>();
        var events = new List<LoomScriptEvent>();
        var limitations = new HashSet<string>(StringComparer.Ordinal);
        var instructions = script.Instructions;
        var indexByOffset = instructions.Select((instruction, index) => (instruction.Offset, index))
            .ToDictionary(item => item.Offset, item => item.index);
        var pc = startOffset;
        var visited = 0;
        var reachedStop = false;
        int? resumeOffset = null;
        string? unresolved = null;

        while (indexByOffset.TryGetValue(pc, out var instructionIndex))
        {
            if (++visited > instructionLimit)
            {
                unresolved = $"instruction limit ({instructionLimit}) reached at +0x{pc:X4}";
                break;
            }

            var instruction = instructions[instructionIndex];
            var next = instruction.Offset + EncodedLength(instruction.Bytes);

            if (instruction.Operation == "stopObjectCode")
            {
                reachedStop = true;
                break;
            }

            if (instruction.Operation == "breakHere")
            {
                resumeOffset = next;
                break;
            }

            if (instruction.Operation == "jumpRelative")
            {
                if (!TryGetBranchTarget(instruction.Operands, out pc))
                {
                    unresolved = $"could not resolve jump at +0x{instruction.Offset:X4}";
                    break;
                }
                continue;
            }

            if (IsConditional(instruction.Operation))
            {
                if (!TryEvaluateCondition(instruction, knownVariables, states, out var condition, out var conditionDetail))
                {
                    if (!TrySkipPresentationOnlyBranch(instructionIndex, next, instructions, indexByOffset, out var join))
                    {
                        unresolved = $"condition at +0x{instruction.Offset:X4} depends on {conditionDetail}; its paths may change object state";
                        break;
                    }
                    trace.Add($"condition at +0x{instruction.Offset:X4} unresolved ({conditionDetail}); both paths contain only unmodeled presentation commands");
                    limitations.Add($"conditional at +0x{instruction.Offset:X4} has an unknown value; the differing presentation commands were omitted");
                    foreach (var possible in instructions.Where(item => item.Offset >= next && item.Offset < join))
                    {
                        var possibleEvent = LoomScriptEvent.FromInstruction(script, possible, LoomScriptEventCertainty.Conditional);
                        if (possibleEvent is not null) events.Add(possibleEvent);
                    }
                    pc = join;
                    continue;
                }

                if (!TryGetBranchTarget(instruction.Operands, out var target))
                {
                    unresolved = $"could not resolve branch at +0x{instruction.Offset:X4}";
                    break;
                }
                // SCUMM's jumpRelative skips to the encoded target when the
                // condition is false; a true condition runs the next opcode.
                pc = condition ? next : target;
                continue;
            }

            var scriptEvent = LoomScriptEvent.FromInstruction(script, instruction, LoomScriptEventCertainty.Reached);
            if (scriptEvent is { TargetId: null, Kind: LoomScriptEventKind.MusicStarted or
                    LoomScriptEventKind.SoundStarted or LoomScriptEventKind.SoundStopped } &&
                TryReadValue(instruction.Operands.Trim(), knownVariables, out var soundId, out _))
                scriptEvent = scriptEvent with { TargetId = soundId };
            if (scriptEvent is not null) events.Add(scriptEvent);
            string? eventHandling = null;
            if (scriptEvent is not null && runtime is not null && onEvent is not null)
            {
                CopyGlobalVariables(knownVariables, runtime.GlobalVariables);
                eventHandling = onEvent(scriptEvent, runtime);
                foreach (var (key, value) in runtime.GlobalVariables) knownVariables[key] = value;
                if (!string.IsNullOrEmpty(eventHandling))
                {
                    unresolved = $"{eventHandling} at +0x{instruction.Offset:X4}";
                    break;
                }
            }

            if (instruction.Operation is "move" or "add" or "subtract" or "multiply" or "divide" or "and" or "or")
            {
                if (!TryApplyVariableOperation(instruction, knownVariables, out var detail))
                {
                    unresolved = $"{detail} at +0x{instruction.Offset:X4}";
                    break;
                }
                pc = next;
                continue;
            }

            if (instruction.Operation is "increment" or "decrement")
            {
                if (!TryReadValue(instruction.Operands, knownVariables, out var value, out var detail) ||
                    !TryGetVariableKey(instruction.Operands, out var variableKey))
                {
                    unresolved = $"{instruction.Operation} cannot update its variable ({detail}; {instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                knownVariables[variableKey] = instruction.Operation == "increment"
                    ? unchecked((ushort)(value + 1))
                    : unchecked((ushort)(value - 1));
                RefreshBitAliases(variableKey, knownVariables);
                pc = next;
                continue;
            }

            if (instruction.Operation == "setState")
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2 || !TryReadValue(parts[0], knownVariables, out var objectId, out _) ||
                    !TryReadValue(parts[1], knownVariables, out var state, out _) || state > byte.MaxValue ||
                    !states.ContainsKey((int)objectId))
                {
                    unresolved = $"setState operands do not identify a known object and byte state ({instruction.Operands})";
                    break;
                }
                states[(int)objectId] = (byte)state;
                runtime?.StatesSetInRoom.Add((int)objectId);
                // o5_setState marks the rectangle dirty and clears the draw queue.
                drawQueue.Clear();
                trace.Add($"object {objectId} set to state {state}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "setOwnerOf")
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (runtime is null || parts.Length < 2 ||
                    !TryReadValue(parts[0], knownVariables, out var objectId, out _) ||
                    !TryReadValue(parts[1], knownVariables, out var owner, out _) ||
                    owner > byte.MaxValue || !runtime.ObjectOwners.ContainsKey(objectId))
                {
                    unresolved = $"setOwnerOf operands or owner table are unknown ({instruction.Operands})";
                    break;
                }
                runtime.ObjectOwners[objectId] = (byte)owner;
                if (knownVariables.TryGetValue("var[0x001]", out var ego))
                {
                    if (owner == ego) runtime.Inventory.Add(objectId);
                    else runtime.Inventory.Remove(objectId);
                }
                trace.Add($"object {objectId} owner set to {owner}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "pickupObject")
            {
                unresolved = $"pickupObject also changes classes and invokes the inventory script at +0x{instruction.Offset:X4}";
                break;
            }

            if (instruction.Operation == "drawObject")
            {
                if (!TryApplyDrawObject(instruction.Operands, roomObjects, states, positions,
                        knownVariables, trace, drawQueue, out var detail))
                {
                    unresolved = $"{detail} at +0x{instruction.Offset:X4}";
                    break;
                }
                pc = next;
                continue;
            }

            if (IsPresentationOperation(instruction.Operation))
            {
                if (eventHandling == "")
                {
                    pc = next;
                    continue;
                }
                if (instruction.Operation is "startScript" or "chainScript" or "startObject")
                {
                    trace.Add($"child execution omitted: {instruction.Operation} {instruction.Operands}");
                    limitations.Add($"child execution omitted: {instruction.Operation} {instruction.Operands}");
                }
                else
                {
                    trace.Add($"presentation operation not rendered: {instruction.Operation} {instruction.Operands}");
                    limitations.Add($"presentation operation not applied: {instruction.Operation}");
                }
                pc = next;
                continue;
            }

            unresolved = $"operation '{instruction.Operation}' is not modeled at +0x{instruction.Offset:X4}";
            break;
        }

        if (unresolved is null && !reachedStop && resumeOffset is null)
        {
            unresolved = script.DecodeNote is null
                ? $"execution reached an undecoded byte at +0x{pc:X4}"
                : $"disassembly stops at +0x{script.UndecodedOffset.GetValueOrDefault():X4}: {script.DecodeNote}";
        }
        if (unresolved is null && limitations.Count > 0)
            unresolved = string.Join("; ", limitations.Take(5)) + (limitations.Count > 5 ? $"; and {limitations.Count - 5} more omitted behaviors" : "");

        if (runtime is not null) CopyGlobalVariables(knownVariables, runtime.GlobalVariables);
        return new LoomBoundedScriptResult(new Dictionary<int, byte>(states),
            new Dictionary<int, RoomObjectPosition>(positions), new Dictionary<string, ushort>(knownVariables),
            trace, events, visited, reachedStop, unresolved, resumeOffset, drawQueue.ToArray());
    }

    private static void CopyGlobalVariables(IReadOnlyDictionary<string, ushort> source,
        IDictionary<string, ushort> destination)
    {
        foreach (var (key, value) in source)
            if (key.StartsWith("var[", StringComparison.Ordinal) ||
                key.StartsWith("bit[", StringComparison.Ordinal)) destination[key] = value;
    }

    private static bool IsConditional(string operation) => operation is
        "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or
        "equalZero" or "notEqualZero" or "ifState" or "ifNotState";

    private static bool TryEvaluateCondition(LoomScriptInstruction instruction,
        IDictionary<string, ushort> variables, IReadOnlyDictionary<int, byte> objectStates,
        out bool result, out string detail)
    {
        result = false;
        var expression = instruction.Operands;
        var branchIndex = expression.IndexOf(", branch ", StringComparison.Ordinal);
        if (branchIndex >= 0) expression = expression[..branchIndex];
        var values = expression.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
        {
            detail = "missing operands";
            return false;
        }

        if (instruction.Operation is "ifState" or "ifNotState")
        {
            detail = "missing object state operands";
            if (values.Length < 2 || !TryReadValue(values[0], variables, out var objectId, out detail) ||
                !TryReadValue(values[1], variables, out var expectedState, out detail))
                return false;
            if (!objectStates.TryGetValue(objectId, out var currentState))
            {
                detail = $"object {objectId} state outside this room snapshot";
                return false;
            }
            result = instruction.Operation == "ifState"
                ? currentState == expectedState : currentState != expectedState;
            detail = "known object state";
            return true;
        }

        if (!TryReadValue(values[0], variables, out var left, out detail)) return false;
        if (instruction.Operation is "equalZero" or "notEqualZero")
        {
            result = instruction.Operation == "equalZero" ? left == 0 : left != 0;
            detail = "known value";
            return true;
        }
        if (values.Length < 2 || !TryReadValue(values[1], variables, out var right, out detail)) return false;

        // Old-v3 comparisons use readVar() as a and the following operand as b.
        // ScummVM evaluates b OP a, with signed 16-bit relational operands.
        var signedLeft = unchecked((short)left);
        var signedRight = unchecked((short)right);
        result = instruction.Operation switch
        {
            "isGreaterEqual" => signedRight >= signedLeft,
            "isNotEqual" => left != right,
            "isLessEqual" => signedRight <= signedLeft,
            "isLess" => signedRight < signedLeft,
            "isEqual" => left == right,
            "isGreater" => signedRight > signedLeft,
            _ => false
        };
        detail = "known values";
        return true;
    }

    private static bool TryApplyVariableOperation(LoomScriptInstruction instruction,
        IDictionary<string, ushort> variables, out string detail)
    {
        var parts = instruction.Operands.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            detail = $"could not parse {instruction.Operation} operands ({instruction.Operands})";
            return false;
        }
        var keyMatch = VariablePattern.Match(parts[0]);
        if (!keyMatch.Success)
        {
            detail = $"cannot write destination ({parts[0]})";
            return false;
        }
        var variableKey = $"{keyMatch.Groups[1].Value}[0x{int.Parse(keyMatch.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]";
        if (!TryReadValue(parts[2], variables, out var right, out detail)) return false;

        if (keyMatch.Groups[1].Value == "bit")
        {
            if (parts[1] != "=")
            {
                detail = $"unsupported bit operation {parts[1]}";
                return false;
            }
            var bitIndex = int.Parse(keyMatch.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var backingKey = $"var[0x{bitIndex >> 4:X3}]";
            var bitValue = (ushort)(right == 0 ? 0 : 1);
            variables[variableKey] = bitValue;
            if (variables.TryGetValue(backingKey, out var backingValue))
                variables[backingKey] = bitValue == 0
                    ? (ushort)(backingValue & ~(1 << (bitIndex & 15)))
                    : (ushort)(backingValue | (1 << (bitIndex & 15)));
            detail = "known bit value";
            return true;
        }

        ushort left = 0;
        if (parts[1] != "=")
        {
            if (!variables.TryGetValue(variableKey, out left))
            {
                detail = $"{instruction.Operation} reads unknown destination {parts[0]}";
                return false;
            }
        }

        try
        {
            variables[variableKey] = parts[1] switch
            {
                "=" => right,
                "+=" => unchecked((ushort)(left + right)),
                "-=" => unchecked((ushort)(left - right)),
                "*=" => unchecked((ushort)(left * right)),
                "/=" when right != 0 => (ushort)(left / right),
                "&=" => (ushort)(left & right),
                "|=" => (ushort)(left | right),
                "/=" => throw new DivideByZeroException(),
                _ => throw new InvalidOperationException($"unsupported operator {parts[1]}")
            };
            RefreshBitAliases(variableKey, variables);
            detail = "known value";
            return true;
        }
        catch (Exception ex) when (ex is DivideByZeroException or InvalidOperationException)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static void RefreshBitAliases(string variableKey, IDictionary<string, ushort> variables)
    {
        if (!variableKey.StartsWith("var[0x", StringComparison.Ordinal) ||
            !int.TryParse(variableKey.AsSpan(6, variableKey.Length - 7), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var wordIndex)) return;
        for (var bit = 0; bit < 16; bit++)
            variables[$"bit[0x{wordIndex * 16 + bit:X3}]"] =
                (ushort)((variables[variableKey] >> bit) & 1);
    }

    private static bool TryApplyDrawObject(string operands, IReadOnlyList<LoomRoomObjectRecord> roomObjects,
        IDictionary<int, byte> states, IDictionary<int, RoomObjectPosition> positions,
        IDictionary<string, ushort> variables, ICollection<string> trace,
        ICollection<int> drawQueue, out string detail)
    {
        var parts = operands.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !TryReadValue(parts[0], variables, out var rawId, out _))
        {
            detail = $"could not read drawObject id ({operands})";
            return false;
        }
        var objectId = (int)rawId;
        var record = roomObjects.FirstOrDefault(item => item.Id == objectId);
        if (record is null || !states.ContainsKey(objectId))
        {
            detail = $"drawObject targets object {objectId}, which is not in the room state table";
            return false;
        }

        var state = (byte)1;
        var position = positions[objectId];
        if (parts.Length > 1 && parts[1] == "state")
        {
            if (parts.Length < 3 || !TryReadValue(parts[2], variables, out var rawState, out _) || rawState > byte.MaxValue)
            {
                detail = $"could not read drawObject state ({operands})";
                return false;
            }
            state = (byte)rawState;
        }
        else if (parts.Length > 1 && parts[1] == "at")
        {
            if (parts.Length < 4 || !TryReadValue(parts[2], variables, out var x, out _) ||
                !TryReadValue(parts[3], variables, out var y, out _))
            {
                detail = $"could not read drawObject position ({operands})";
                return false;
            }
            if (x != 0xFF)
                position = new RoomObjectPosition(checked((int)x * 8), checked((int)y * 8));
        }
        else if (parts.Length > 1 && parts[1] != "default state and position")
        {
            detail = $"unsupported drawObject mode ({operands})";
            return false;
        }

        positions[objectId] = position;
        foreach (var peer in roomObjects)
        {
            if (peer.Id == objectId || peer.Width != record.Width || peer.Height != record.Height ||
                !positions.TryGetValue(peer.Id, out var peerPosition) || peerPosition != position) continue;
            if (states.ContainsKey(peer.Id)) states[peer.Id] = 0;
        }
        states[objectId] = state;
        drawQueue.Add(objectId);
        trace.Add($"object {objectId} drawn at {position.X},{position.Y} in state {state}");
        detail = "known state";
        return true;
    }

    private static bool TrySkipPresentationOnlyBranch(int instructionIndex, int fallthrough,
        IReadOnlyList<LoomScriptInstruction> instructions, IReadOnlyDictionary<int, int> indexByOffset,
        out int join)
    {
        join = fallthrough;
        if (!TryGetBranchTarget(instructions[instructionIndex].Operands, out var branchTarget) || branchTarget <= fallthrough)
            return false;
        if (!TryFindPresentationPathEnd(fallthrough, branchTarget, instructions, indexByOffset, out var fallthroughEnd))
            return false;
        if (fallthroughEnd == branchTarget)
        {
            join = branchTarget;
            return true;
        }
        if (!TryFindPresentationPathEnd(branchTarget, fallthroughEnd, instructions, indexByOffset, out var branchEnd) ||
            branchEnd != fallthroughEnd) return false;
        join = fallthroughEnd;
        return true;
    }

    private static bool TryFindPresentationPathEnd(int start, int boundary,
        IReadOnlyList<LoomScriptInstruction> instructions, IReadOnlyDictionary<int, int> indexByOffset,
        out int end)
    {
        end = start;
        var pc = start;
        for (var count = 0; count < 128; count++)
        {
            if (pc >= boundary)
            {
                end = pc;
                return true;
            }
            if (!indexByOffset.TryGetValue(pc, out var index)) return false;
            var instruction = instructions[index];
            if (instruction.Operation == "jumpRelative")
            {
                if (!TryGetBranchTarget(instruction.Operands, out end) || end < boundary) return false;
                return true;
            }
            if (!IsPresentationOperation(instruction.Operation)) return false;
            pc = instruction.Offset + EncodedLength(instruction.Bytes);
        }
        return false;
    }

    private static bool IsPresentationOperation(string operation) => operation is
        "roomOps" or "actorOps" or "setClass" or "animateActor" or "putActor" or "putActorInRoom" or
        "faceActor" or "walkActorTo" or "walkActorToActor" or "walkActorToObject" or "putActorAtObject" or
        "setCameraAt" or "panCameraTo" or "lights" or "resourceRoutines" or "print" or "printEgo" or
        "startScript" or "chainScript" or "startObject" or "startMusic" or "stopMusic" or "startSound" or
        "stopSound" or "cursorCommand" or "stringOps" or "oldRoomEffect" or "setBoxFlags" or
        "setObjectName" or "actorFollowCamera";

    private static bool TryGetBranchTarget(string operands, out int target)
    {
        target = 0;
        var marker = operands.LastIndexOf("->", StringComparison.Ordinal);
        if (marker < 0) return false;
        var targetText = operands[(marker + 2)..].Trim();
        return targetText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(targetText.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out target);
    }

    private static bool TryReadValue(string operand, IDictionary<string, ushort> variables,
        out ushort value, out string detail)
    {
        value = 0;
        var match = VariablePattern.Match(operand);
        if (match.Success)
        {
            var key = $"{match.Groups[1].Value}[0x{int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]";
            if (variables.TryGetValue(key, out value))
            {
                detail = "known value";
                return true;
            }
            if (match.Groups[1].Value == "bit")
            {
                var bitIndex = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (variables.TryGetValue($"var[0x{bitIndex >> 4:X3}]", out var word))
                {
                    value = (ushort)((word >> (bitIndex & 15)) & 1);
                    detail = "known bit from global word";
                    return true;
                }
            }
            detail = key;
            return false;
        }
        if (TryReadLiteral(operand, out value))
        {
            detail = "known value";
            return true;
        }
        detail = operand;
        return false;
    }

    private static bool TryGetVariableKey(string operand, out string variableKey)
    {
        variableKey = "";
        var match = VariablePattern.Match(operand.Trim());
        if (!match.Success || match.Groups[1].Value == "bit") return false;
        variableKey = $"{match.Groups[1].Value}[0x{int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]";
        return true;
    }

    private static bool TryReadLiteral(string operand, out ushort value)
    {
        value = 0;
        return operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static int EncodedLength(string bytes) =>
        bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}

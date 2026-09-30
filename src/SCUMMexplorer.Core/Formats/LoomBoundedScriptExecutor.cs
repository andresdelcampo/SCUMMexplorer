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
    IReadOnlyList<int>? DrawQueue = null,
    IReadOnlyList<LoomRandomPick>? Picks = null,
    IReadOnlyCollection<string>? EstimatedLocalKeys = null,
    bool PathEstimated = false,
    int? DelayJiffies = null,
    string? DelayEstimate = null)
{
    /// <summary>
    /// ScummVM <c>o5_delay</c>/<c>o5_delayVariable</c>: the slot's delay in jiffies when the run paused
    /// on one (<c>ssPaused</c>); the frame loop counts it down before the scripts run. Null otherwise.
    /// </summary>
    public int? Delay => DelayJiffies;

    /// <summary>Seeded stand-ins for <c>getRandomNr</c> taken during this run, in order.</summary>
    public IReadOnlyList<LoomRandomPick> RandomPicks => Picks ?? [];

    /// <summary>Local variables that hold a value derived from a seeded pick when the run ended.</summary>
    public IReadOnlyCollection<string> EstimatedLocals => EstimatedLocalKeys ?? [];

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

    /// <summary>
    /// Returned by an event handler that applies the event itself (such as a
    /// room load that a global script survives): the run pauses right after the
    /// instruction, and the caller resumes it at <see cref="LoomBoundedScriptResult.ResumeOffset"/>.
    /// </summary>
    public const string PauseAfterEvent = "\u0001pause-after-event";

    /// <summary>
    /// Returned by an event handler when the instruction ends the current script
    /// (such as <c>chainScript</c>, which starts another script in its place).
    /// </summary>
    public const string StopAfterEvent = "\u0001stop-after-event";
    private static readonly Regex VariablePattern = new(@"^(var|local|bit)\[0x([0-9A-Fa-f]+)\]$", RegexOptions.Compiled);
    // readVar/writeVar with the 0x2000 flag: base plus a literal or the value of another variable.
    private static readonly Regex IndexedPattern = new(@"^(var|local|bit)\[0x([0-9A-Fa-f]+)\+(.+)\]$", RegexOptions.Compiled);

    public LoomBoundedScriptResult Execute(
        LoomScriptDisassembly script,
        IReadOnlyDictionary<int, byte> initialObjectStates,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects,
        int instructionLimit = DefaultInstructionLimit,
        LoomScriptRuntimeState? runtime = null,
        int startOffset = 0,
        IReadOnlyDictionary<string, ushort>? initialLocals = null,
        Func<LoomScriptEvent, LoomScriptRuntimeState, string?>? onEvent = null,
        IReadOnlyCollection<string>? estimatedLocals = null,
        bool pathEstimated = false)
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
        // Values that come from a seeded getRandomNr pick, and whether control
        // flow has branched on one. Effects under either are estimates.
        var estimated = new HashSet<string>(StringComparer.Ordinal);
        if (runtime is not null) estimated.UnionWith(runtime.EstimatedVariables);
        if (estimatedLocals is not null) estimated.UnionWith(estimatedLocals);
        var onEstimatedPath = pathEstimated;
        var picks = new List<LoomRandomPick>();
        var instructions = script.Instructions;
        var indexByOffset = instructions.Select((instruction, index) => (instruction.Offset, index))
            .ToDictionary(item => item.Offset, item => item.index);
        var pc = startOffset;
        var visited = 0;
        var reachedStop = false;
        int? resumeOffset = null;
        int? delayJiffies = null;
        string? delayEstimate = null;
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

            if (instruction.Operation == "debug")
            {
                // o5_debug reads its operand and only logs it in debug builds.
                pc = next;
                continue;
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
                if (!TryEvaluateCondition(instruction, knownVariables, states, runtime?.ObjectClasses,
                        out var condition, out var conditionDetail))
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
                        if (runtime is not null && IsActorPlacement(possible.Operation) && possible.Operation is not ("animateActor" or "faceActor"))
                            ForgetPossiblePlacement(possible, runtime, knownVariables, instruction.Offset);
                    }
                    pc = join;
                    continue;
                }

                if (!TryGetBranchTarget(instruction.Operands, out var target))
                {
                    unresolved = $"could not resolve branch at +0x{instruction.Offset:X4}";
                    break;
                }
                if (!onEstimatedPath && ConditionReadsEstimate(instruction, estimated, runtime?.EstimatedStates, knownVariables))
                {
                    onEstimatedPath = true;
                    trace.Add($"branch at +0x{instruction.Offset:X4} reads a seeded pick; later steps are estimates");
                }
                // SCUMM's jumpRelative skips to the encoded target when the
                // condition is false; a true condition runs the next opcode.
                pc = condition ? next : target;
                continue;
            }

            // An event behind a branch on a seeded pick is a possible outcome, not a reached one.
            var scriptEvent = LoomScriptEvent.FromInstruction(script, instruction,
                onEstimatedPath ? LoomScriptEventCertainty.Conditional : LoomScriptEventCertainty.Reached);
            if (scriptEvent is not null) scriptEvent = scriptEvent with { Executed = true };
            if (scriptEvent is { TargetId: null, Kind: LoomScriptEventKind.MusicStarted or
                    LoomScriptEventKind.SoundStarted or LoomScriptEventKind.SoundStopped } &&
                TryReadValue(instruction.Operands.Trim(), knownVariables, out var soundId, out _))
                scriptEvent = scriptEvent with
                {
                    TargetId = soundId,
                    Certainty = OperandIsEstimated(instruction.Operands.Trim(), estimated, knownVariables)
                        ? LoomScriptEventCertainty.Conditional : scriptEvent.Certainty
                };
            // o5_startScript, o5_chainScript and o5_stopScript read the script
            // number with getVarOrDirectByte, so a variable or local target takes
            // its value now. A target that reads a seeded pick is a possible outcome.
            if (scriptEvent is { TargetId: null, Kind: LoomScriptEventKind.ScriptStarted or
                    LoomScriptEventKind.ScriptChained or LoomScriptEventKind.ScriptStopped })
            {
                var listStart = ArgumentListStart(instruction.Operands);
                var head = (listStart < 0 ? instruction.Operands : instruction.Operands[..listStart])
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                // stopScript 0 would end the current script; it is left unresolved.
                if (head.Length > 0 && TryReadValue(head[0], knownVariables, out var scriptId, out _) &&
                    !(scriptId == 0 && scriptEvent.Kind == LoomScriptEventKind.ScriptStopped))
                    scriptEvent = scriptEvent with
                    {
                        TargetId = scriptId,
                        Certainty = OperandIsEstimated(head[0], estimated, knownVariables)
                            ? LoomScriptEventCertainty.Conditional : scriptEvent.Certainty
                    };
            }
            // The session applies loadRoomWithEgo's ego steps after the room entry,
            // so it needs the object and walk destination as they are now.
            if (scriptEvent is { Operation: "loadRoomWithEgo" })
                scriptEvent = scriptEvent with { ResolvedArguments = ResolveLoadRoomWithEgo(instruction.Operands, knownVariables) };
            // A started script receives its arguments' values; the session reads only
            // globals and literals, so known locals are passed as literals. An
            // estimated local keeps its index, so the child runs with it as an estimate.
            if (scriptEvent is { Kind: LoomScriptEventKind.ScriptStarted or LoomScriptEventKind.ScriptChained or
                    LoomScriptEventKind.ObjectScriptStarted or LoomScriptEventKind.CutsceneStarted })
            {
                var (operands, estimatedIndexes) = SubstituteLocalArguments(scriptEvent.Operands, knownVariables, estimated);
                scriptEvent = scriptEvent with
                {
                    Operands = operands,
                    EstimatedArgumentIndexes = estimatedIndexes.Count > 0 ? estimatedIndexes : null
                };
            }
            // o5_startObject: the object (word) and verb (byte) as they are now.
            if (scriptEvent is { Kind: LoomScriptEventKind.ObjectScriptStarted })
            {
                var listStart = ArgumentListStart(instruction.Operands);
                var head = (listStart < 0 ? instruction.Operands : instruction.Operands[..listStart]).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                int? Read(int index) => head.Length > index && TryReadValue(head[index], knownVariables, out var value, out _) ? value : null;
                scriptEvent = scriptEvent with
                {
                    ResolvedArguments = [Read(0), Read(1)],
                    Certainty = head.Any(operand => OperandIsEstimated(operand, estimated, knownVariables))
                        ? LoomScriptEventCertainty.Conditional : scriptEvent.Certainty
                };
            }
            if (scriptEvent is not null) events.Add(scriptEvent);
            string? eventHandling = null;
            if (scriptEvent is not null && runtime is not null && onEvent is not null)
            {
                CopyGlobalVariables(knownVariables, runtime.GlobalVariables);
                CopyEstimatedGlobals(estimated, runtime.EstimatedVariables);
                eventHandling = onEvent(scriptEvent, runtime);
                foreach (var (key, value) in runtime.GlobalVariables) knownVariables[key] = value;
                estimated.RemoveWhere(IsGlobalKey);
                estimated.UnionWith(runtime.EstimatedVariables);
                if (eventHandling == StopAfterEvent)
                {
                    trace.Add($"{instruction.Operation} at +0x{instruction.Offset:X4} ends this script");
                    reachedStop = true;
                    break;
                }
                if (eventHandling == PauseAfterEvent)
                {
                    trace.Add($"{instruction.Operation} at +0x{instruction.Offset:X4} handed to the session; the script resumes after it");
                    resumeOffset = next;
                    break;
                }
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
                var operationParts = instruction.Operands.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (TryNormalizeKey(operationParts[0], knownVariables, out var destination))
                    MarkEstimated(estimated, destination, onEstimatedPath ||
                        OperandIsEstimated(operationParts[2], estimated, knownVariables) ||
                        OperandIsEstimated(operationParts[0], estimated, knownVariables, indexOnly: true) ||
                        (operationParts[1] != "=" && IsEstimated(destination, estimated)));
                pc = next;
                continue;
            }

            if (instruction.Operation == "getRandomNr")
            {
                // o5_getRandomNr: result = getRandomNumber(max), 0..max inclusive.
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (runtime is null)
                {
                    unresolved = $"getRandomNr needs a session's seeded random source at +0x{instruction.Offset:X4}";
                    break;
                }
                if (parts.Length != 2 || !TryGetVariableKey(parts[0], knownVariables, out var resultKey) ||
                    !TryReadValue(parts[1], knownVariables, out var max, out _))
                {
                    unresolved = $"getRandomNr operands are unknown ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                var pick = runtime.Random.Pick(script.Context ?? $"Script {script.ScriptId:00}", instruction.Offset, max);
                knownVariables[resultKey] = (ushort)pick.Value;
                RefreshBitAliases(resultKey, knownVariables);
                MarkEstimated(estimated, resultKey, true);
                picks.Add(pick);
                trace.Add($"{resultKey} = {pick.Value}: seeded pick of 0..{max} (seed {pick.Seed}, draw {pick.Draw})");
                pc = next;
                continue;
            }

            if (instruction.Operation is "increment" or "decrement")
            {
                if (!TryReadValue(instruction.Operands, knownVariables, out var value, out var detail) ||
                    !TryGetVariableKey(instruction.Operands, knownVariables, out var variableKey))
                {
                    unresolved = $"{instruction.Operation} cannot update its variable ({detail}; {instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                knownVariables[variableKey] = instruction.Operation == "increment"
                    ? unchecked((ushort)(value + 1))
                    : unchecked((ushort)(value - 1));
                RefreshBitAliases(variableKey, knownVariables);
                if (onEstimatedPath) MarkEstimated(estimated, variableKey, true);
                pc = next;
                continue;
            }

            if (instruction.Operation == "setVarRange")
            {
                // o5_setVarRange: each listed literal goes to the next variable,
                // starting at the result variable (_resultVarNumber++).
                if (!TryReadVarRange(instruction.Operands, knownVariables, out var destination, out var keys, out var values))
                {
                    unresolved = $"setVarRange operands are unknown ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                var indexEstimated = OperandIsEstimated(destination, estimated, knownVariables, indexOnly: true);
                for (var index = 0; index < keys.Count; index++)
                {
                    knownVariables[keys[index]] = values[index];
                    RefreshBitAliases(keys[index], knownVariables);
                    MarkEstimated(estimated, keys[index], onEstimatedPath || indexEstimated);
                }
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
                var stateEstimated = onEstimatedPath || OperandIsEstimated(parts[0], estimated, knownVariables) ||
                                     OperandIsEstimated(parts[1], estimated, knownVariables);
                if (stateEstimated)
                {
                    // Not a reached state: it only happened because of a seeded pick.
                    runtime?.EstimatedStates.Add((int)objectId);
                }
                else
                {
                    runtime?.StatesSetInRoom.Add((int)objectId);
                    runtime?.EstimatedStates.Remove((int)objectId);
                }
                // o5_setState marks the rectangle dirty and clears the draw queue.
                drawQueue.Clear();
                trace.Add($"object {objectId} set to state {state}{(stateEstimated ? " (estimate: follows a seeded pick)" : "")}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "stopScript")
            {
                // o5_stopScript: 0 ends the current script like stopObjectCode;
                // another number ends that script's running slots, which only
                // a session tracking the other scripts can apply.
                if (!TryReadValue(instruction.Operands.Trim(), knownVariables, out var stopped, out var stopDetail))
                {
                    unresolved = $"stopScript target is unknown ({stopDetail}) at +0x{instruction.Offset:X4}";
                    break;
                }
                if (stopped == 0)
                {
                    reachedStop = true;
                    break;
                }
                if (eventHandling != "")
                {
                    unresolved = $"stopScript {stopped} needs a session that tracks running scripts at +0x{instruction.Offset:X4}";
                    break;
                }
                trace.Add($"script {stopped} stopped");
                pc = next;
                continue;
            }

            if (instruction.Operation == "setClass" && runtime is not null &&
                TryReadClassOperands(instruction.Operands, knownVariables, out var classActor, out var actorClasses) &&
                LoomActorTable.IsValid(classActor))
            {
                // o5_setClass, small header: class 0 on an actor also clears its ignoreBoxes; putClass (v4 and older)
                // calls Actor::classChanged, so class 22 (kObjectClassIgnoreBoxes) sets or clears it.
                foreach (var value in actorClasses)
                {
                    if (value == 0) runtime.Actors.SetIgnoreBoxes(classActor, false);
                    else if ((value & 0x7F) == 22) runtime.Actors.SetIgnoreBoxes(classActor, (value & 0x80) != 0);
                }
            }

            if (instruction.Operation == "setClass" && runtime is not null &&
                TryReadClassOperands(instruction.Operands, knownVariables, out var classObject, out var classValues) &&
                runtime.ObjectClasses.TryGetValue(classObject, out var classBits))
            {
                // o5_setClass/putClass: 0 clears every class; 0x80|n sets class n; n clears it.
                foreach (var value in classValues)
                {
                    var cls = value & 0x7F;
                    if (value == 0) classBits = 0;
                    else if (cls is >= 1 and <= 32)
                        classBits = (value & 0x80) != 0 ? classBits | (1u << (cls - 1)) : classBits & ~(1u << (cls - 1));
                }
                runtime.ObjectClasses[classObject] = classBits;
                var classEstimated = onEstimatedPath || instruction.Operands.Split(',', StringSplitOptions.TrimEntries)
                    .Any(operand => OperandIsEstimated(operand.Trim('[', ']', ' '), estimated, knownVariables));
                if (classEstimated) runtime.EstimatedStates.Add(classObject);
                trace.Add($"object {classObject} classes set to 0x{classBits:X6}{(classEstimated ? " (estimate: follows a seeded pick)" : "")}");
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
                if (onEstimatedPath || instruction.Operands.Split(',', StringSplitOptions.TrimEntries)
                        .Any(operand => OperandIsEstimated(operand, estimated, knownVariables)))
                {
                    runtime?.EstimatedStates.Add(drawQueue[^1]);
                    trace[^1] += " (estimate: follows a seeded pick)";
                }
                pc = next;
                continue;
            }

            if (instruction.Operation is "cutscene" or "endCutscene" or "freezeScripts")
            {
                // beginCutscene/endCutscene run the scripts in VAR_CUTSCENE_START_SCRIPT and
                // VAR_CUTSCENE_END_SCRIPT, and freezeScripts suspends other running
                // scripts; only a session that runs scripts can apply them.
                if (eventHandling == "")
                {
                    pc = next;
                    continue;
                }
                unresolved = $"{instruction.Operation} needs a session that runs and tracks scripts at +0x{instruction.Offset:X4}";
                break;
            }

            if (runtime is not null && instruction.Operation == "drawBox")
            {
                // o5_drawBox: the box covers drawn strings under it (script 008 erases the close-up and name).
                var corners = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                var values = new ushort[corners.Length];
                var known = corners.Length == 5;
                for (var i = 0; known && i < corners.Length; i++)
                    known = TryReadValue(corners[i], knownVariables, out values[i], out _);
                trace.Add(known
                    ? runtime.Messages.DrawBox((short)values[0], (short)values[1], (short)values[2], (short)values[3], values[4])
                    : $"drawBox {instruction.Operands} not applied: an operand is unknown");
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation is "print" or "printEgo")
            {
                if (!TryReadPrint(instruction, script, knownVariables, estimated, onEstimatedPath, runtime, out var request, out var printDetail))
                {
                    unresolved = $"{printDetail} at +0x{instruction.Offset:X4}";
                    break;
                }
                trace.Add(runtime.Messages.Print(request, runtime));
                knownVariables[LoomMessageTable.HaveMsgVariable] = runtime.GlobalVariables[LoomMessageTable.HaveMsgVariable];
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation == "waitFor" && instruction.Operands == "message")
            {
                // o5_wait (SO_WAIT_FOR_MESSAGE): while VAR_HAVE_MSG is set, the script breaks
                // here and tries again next frame; the frame counts the talk delay down.
                if (TryReadValue(LoomMessageTable.HaveMsgVariable, knownVariables, out var haveMsg, out _) && haveMsg != 0)
                {
                    trace.Add($"waiting for the message to finish at +0x{instruction.Offset:X4} (VAR_HAVE_MSG = {haveMsg}, talk delay {runtime.Messages.TalkDelay} jiffies left)");
                    resumeOffset = instruction.Offset;
                    break;
                }
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation == "waitFor" && instruction.Operands.StartsWith("actor,", StringComparison.Ordinal) &&
                TryReadValue(instruction.Operands[6..].Trim(), knownVariables, out var walkingActor, out _) &&
                LoomActorTable.IsValid(walkingActor))
            {
                // o5_wait (SO_WAIT_FOR_ACTOR): while the actor's _moving is set (a walk or a
                // turn), the script breaks here and tries again next frame; otherwise it goes on.
                if (runtime.Actors.Moving(walkingActor) is var moving and not 0)
                {
                    trace.Add($"waiting for actor {walkingActor} to stop moving at +0x{instruction.Offset:X4} (_moving = 0x{moving:X2})");
                    resumeOffset = instruction.Offset;
                    break;
                }
                // A walk that could not be stepped was taken to end at once; the script
                // goes on only after it has ended, so its end is no longer an estimate.
                if (runtime.Actors.WalkWaited(walkingActor))
                    trace.Add($"actor {walkingActor}'s walk has ended when the script goes on, so its position is no longer an estimate (the walk itself was not stepped)");
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation == "waitFor" && instruction.Operands == "camera" &&
                knownVariables.TryGetValue("var[0x002]", out var cameraWaitX))
            {
                // o5_wait (SO_WAIT_FOR_CAMERA): breaks while the camera is not on its destination's strip.
                if (cameraWaitX / 8 != runtime.Camera.DestX / 8)
                {
                    trace.Add($"waiting for the camera at +0x{instruction.Offset:X4} (x {cameraWaitX}, destination {runtime.Camera.DestX})");
                    resumeOffset = instruction.Offset;
                    break;
                }
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation is "delay" or "delayVariable" &&
                TryReadDelay(instruction, knownVariables, out var jiffies))
            {
                // o5_delay (a 24-bit literal) and o5_delayVariable (getVar) set the slot's delay,
                // make it ssPaused and break; decreaseScriptDelay runs it again once the delay is below 0.
                delayJiffies = jiffies;
                if (instruction.Operation == "delayVariable" && OperandIsEstimated(instruction.Operands, estimated, knownVariables))
                    delayEstimate = $"the delay at +0x{instruction.Offset:X4} reads {instruction.Operands} = {jiffies}, an estimate";
                trace.Add($"a delay of {jiffies} jiffies at +0x{instruction.Offset:X4}{(delayEstimate is null ? "" : " (estimate)")}");
                resumeOffset = next;
                break;
            }

            if (instruction.Operation is "delay" or "delayVariable" ||
                instruction.Operation == "waitFor" && (instruction.Operands is "message" or "camera" ||
                                                       instruction.Operands.StartsWith("actor,", StringComparison.Ordinal)))
            {
                // o5_delay and o5_delayVariable pause the script for a time; o5_wait
                // breaks while the actor walks, the message shows or the camera
                // moves. None of these is simulated, so the script continues on
                // the next frame. Only timing is compressed; no state is set.
                var waited = instruction.Operation switch
                {
                    "delay" => $"a delay of {ReadDelay(instruction.Operands)} jiffies",
                    "delayVariable" => $"a delay of {instruction.Operands} jiffies",
                    _ when instruction.Operands == "message" => "waiting for the message to finish",
                    _ when instruction.Operands == "camera" => "waiting for the camera",
                    _ => $"waiting for actor {instruction.Operands[6..].Trim()} to stop walking"
                };
                trace.Add($"{waited} at +0x{instruction.Offset:X4} continues on the next frame");
                limitations.Add($"timing not simulated: {waited} continues on the next frame (+0x{instruction.Offset:X4})");
                resumeOffset = next;
                break;
            }

            if (runtime is not null && instruction.Operation == "cursorCommand")
            {
                // o5_cursorCommand: userInputOn/Off set _userPut to 1/0; the soft forms add or take one.
                var command = instruction.Operands.Split(',', StringSplitOptions.TrimEntries)[0];
                var before = runtime.UserPut;
                runtime.UserPut = command switch
                {
                    "userInputOn" => 1,
                    "userInputOff" => 0,
                    "userInputSoftOn" => runtime.UserPut + 1,
                    "userInputSoftOff" => runtime.UserPut - 1,
                    _ => runtime.UserPut
                };
                if (runtime.UserPut != before)
                {
                    if (onEstimatedPath) runtime.UserPutEstimate = $"{command} at +0x{instruction.Offset:X4} follows a seeded pick";
                    else if (command is "userInputOn" or "userInputOff") runtime.UserPutEstimate = null;
                    trace.Add($"user input {(runtime.UserPut > 0 ? "on" : "off")} ({command}, _userPut = {runtime.UserPut})");
                }
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation == "getDist")
            {
                // o5_getDist: result = getObjActToObjActDist(a, b).
                var distParts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (distParts.Length != 3 || !TryGetVariableKey(distParts[0], knownVariables, out var distKey))
                {
                    unresolved = $"getDist operands are unknown ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                if (!TryMeasureDistance(distParts[1], distParts[2], runtime, roomObjects, knownVariables,
                        out var distance, out var distDetail, out var distEstimate))
                {
                    unresolved = $"{distDetail} at +0x{instruction.Offset:X4}";
                    break;
                }
                knownVariables[distKey] = (ushort)distance;
                RefreshBitAliases(distKey, knownVariables);
                MarkEstimated(estimated, distKey, onEstimatedPath || distEstimate is not null ||
                    OperandIsEstimated(distParts[1], estimated, knownVariables) || OperandIsEstimated(distParts[2], estimated, knownVariables));
                if (distEstimate is not null)
                    limitations.Add($"getDist reads {distEstimate}, so what follows is an estimate (+0x{instruction.Offset:X4})");
                trace.Add($"{distKey} = {distance}: getDist {distParts[1]}, {distParts[2]}{(distEstimate is null ? "" : " (estimate)")}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "isSoundRunning")
            {
                // o5_isSoundRunning: 0 for sound 0, else whether the sound plays.
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (runtime is null || parts.Length != 2 || !TryGetVariableKey(parts[0], knownVariables, out var resultKey) ||
                    !TryReadValue(parts[1], knownVariables, out var sound, out _))
                {
                    unresolved = $"isSoundRunning needs a session and a known sound ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                // A sound started on this route is read as still playing, because its
                // length is not simulated: that value, and what follows from it, is an
                // estimate. A sound this route never started is silent in this preview.
                var running = sound != 0 && runtime.ActiveSounds.Contains(sound);
                knownVariables[resultKey] = running ? (ushort)1 : (ushort)0;
                RefreshBitAliases(resultKey, knownVariables);
                MarkEstimated(estimated, resultKey, onEstimatedPath || running ||
                    OperandIsEstimated(parts[1], estimated, knownVariables));
                if (sound != 0)
                    limitations.Add(running
                        ? $"isSoundRunning reads sound {sound} as playing because this route started it; its length is not simulated, so what follows is an estimate (+0x{instruction.Offset:X4})"
                        : $"isSoundRunning reads sound {sound} as silent because this route did not start it (+0x{instruction.Offset:X4})");
                trace.Add($"{resultKey} = {(running ? 1 : 0)}: sound {sound} {(running ? "started on this route (estimate)" : "not started on this route")}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "isScriptRunning")
            {
                // o5_isScriptRunning: whether a global or local script slot with
                // this number is running. Only a session knows its scripts.
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (runtime?.ScriptRunning is null || parts.Length != 2 ||
                    !TryGetVariableKey(parts[0], knownVariables, out var resultKey) ||
                    !TryReadValue(parts[1], knownVariables, out var queried, out _))
                {
                    unresolved = $"isScriptRunning needs a session that tracks scripts and a known script ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                var answer = runtime.ScriptRunning(queried);
                knownVariables[resultKey] = answer.Running ? (ushort)1 : (ushort)0;
                RefreshBitAliases(resultKey, knownVariables);
                MarkEstimated(estimated, resultKey, onEstimatedPath || answer.Estimate is not null ||
                    OperandIsEstimated(parts[1], estimated, knownVariables));
                if (answer.Estimate is not null)
                    limitations.Add($"isScriptRunning {queried}: {answer.Estimate}, so what follows is an estimate (+0x{instruction.Offset:X4})");
                trace.Add($"{resultKey} = {(answer.Running ? 1 : 0)}: script {queried} {(answer.Running ? "is running" : "is not running")} in this session{(answer.Estimate is null ? "" : " (estimate)")}");
                pc = next;
                continue;
            }

            if (instruction.Operation is "setCameraAt" or "panCameraTo" && runtime?.CameraRange is { } range &&
                TryReadValue(instruction.Operands.Trim(), knownVariables, out var cameraX, out _))
            {
                // setCameraAtEx: normal mode, the camera placed at once (kept inside
                // VAR_CAMERA_MIN_X/MAX_X) and published in VAR_CAMERA_POS_X (var 2).
                // panCameraTo only sets the destination and panning mode; moveCamera
                // then moves the camera 8 pixels a frame (the session's frame end).
                var cameraEstimated = onEstimatedPath || OperandIsEstimated(instruction.Operands.Trim(), estimated, knownVariables);
                runtime.Camera.DestX = (short)cameraX;
                runtime.Camera.MovingToActor = false;
                if (instruction.Operation == "panCameraTo")
                {
                    runtime.Camera.Mode = LoomCamera.Panning;
                    if (cameraEstimated) MarkEstimated(estimated, "var[0x002]", true);
                    trace.Add($"camera pans to x = {(short)cameraX} ({instruction.Operation} {instruction.Operands})");
                    pc = next;
                    continue;
                }
                runtime.Camera.Mode = LoomCamera.Normal;
                var clamped = (ushort)Math.Clamp(cameraX, range.Min, range.Max);
                knownVariables["var[0x002]"] = clamped;
                RefreshBitAliases("var[0x002]", knownVariables);
                MarkEstimated(estimated, "var[0x002]", cameraEstimated);
                trace.Add($"camera x = {clamped} ({instruction.Operation} {instruction.Operands})");
                pc = next;
                continue;
            }

            if (instruction.Operation == "findObject" && runtime is not null)
            {
                // o5_findObject: result = findObject(x, y) over the current room's objects.
                var find = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (find.Length != 3 || !TryGetVariableKey(find[0], knownVariables, out var foundKey) ||
                    !TryReadValue(find[1], knownVariables, out var findX, out _) ||
                    !TryReadValue(find[2], knownVariables, out var findY, out _))
                {
                    unresolved = $"findObject operands are unknown ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                var (found, findUnknown) = FindObjectAt(roomObjects, states, runtime, unchecked((short)findX), unchecked((short)findY));
                if (findUnknown is not null)
                {
                    unresolved = $"findObject at ({(short)findX}, {(short)findY}) {findUnknown} at +0x{instruction.Offset:X4}";
                    break;
                }
                knownVariables[foundKey] = (ushort)found;
                RefreshBitAliases(foundKey, knownVariables);
                MarkEstimated(estimated, foundKey, onEstimatedPath ||
                    OperandIsEstimated(find[1], estimated, knownVariables) || OperandIsEstimated(find[2], estimated, knownVariables));
                trace.Add($"{foundKey} = {found}: findObject at ({(short)findX}, {(short)findY})");
                if (runtime.VirtualMouse is { } mouse && mouse == ((short)findX, (short)findY) && runtime.VirtualMouseAssumption is { } assumption)
                    limitations.Add($"findObject at +0x{instruction.Offset:X4} reads the virtual mouse: {assumption}");
                pc = next;
                continue;
            }

            if (instruction.Operation == "roomOps" && runtime?.RoomCameraBounds is { } bounds &&
                instruction.Operands.StartsWith("roomScroll,", StringComparison.Ordinal))
            {
                // o5_roomOps SO_ROOM_SCROLL: both limits are kept half a screen from the
                // room's edges and stored in VAR_CAMERA_MIN_X/MAX_X (v3: vars 17 and 18).
                var scroll = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (scroll.Length == 3 && TryReadValue(scroll[1], knownVariables, out var scrollMin, out _) &&
                    TryReadValue(scroll[2], knownVariables, out var scrollMax, out _))
                {
                    var low = (ushort)Math.Clamp((int)scrollMin, bounds.Min, bounds.Max);
                    var high = (ushort)Math.Clamp((int)scrollMax, bounds.Min, bounds.Max);
                    knownVariables["var[0x011]"] = low;
                    knownVariables["var[0x012]"] = high;
                    RefreshBitAliases("var[0x011]", knownVariables);
                    RefreshBitAliases("var[0x012]", knownVariables);
                    MarkEstimated(estimated, "var[0x011]", onEstimatedPath || OperandIsEstimated(scroll[1], estimated, knownVariables));
                    MarkEstimated(estimated, "var[0x012]", onEstimatedPath || OperandIsEstimated(scroll[2], estimated, knownVariables));
                    // setCameraAt and moveCamera clamp to the minimum, then the maximum,
                    // so the maximum wins if the limits cross.
                    runtime.CameraRange = (Math.Min(low, high), high);
                    trace.Add($"camera limits = {low}..{high} (roomScroll)");
                    if (knownVariables.TryGetValue("var[0x002]", out var cameraNow) &&
                        (cameraNow < runtime.CameraRange.Value.Min || cameraNow > runtime.CameraRange.Value.Max))
                    {
                        var inside = (ushort)Math.Clamp((int)cameraNow, runtime.CameraRange.Value.Min, runtime.CameraRange.Value.Max);
                        knownVariables["var[0x002]"] = inside;
                        RefreshBitAliases("var[0x002]", knownVariables);
                        trace.Add($"camera x = {inside}: moved inside the new limits");
                        limitations.Add($"timing not simulated: the camera moves 8 pixels a frame into its new limits and reaches x = {inside} at once (+0x{instruction.Offset:X4})");
                    }
                    pc = next;
                    continue;
                }
            }

            if (instruction.Operation == "actorFollowCamera" && runtime?.CameraRange is { } followRange)
            {
                // o5_actorFollowCamera -> setCameraFollows (v3): the camera follows the
                // actor, and moves onto it at once when the actor is outside the
                // camera's triggers (strips 10 and 30 of the 40 on screen).
                var operand = instruction.Operands.Trim();
                string? cameraUnknown = null;
                if (!TryReadValue(operand, knownVariables, out var followed, out _) || !LoomActorTable.IsValid(followed))
                    cameraUnknown = $"it follows an actor the preview cannot read ({operand})";
                else
                {
                    var actor = runtime.Actors[followed];
                    if (actor.Room is null || actor.X is not int actorX)
                        cameraUnknown = $"it follows actor {followed}, whose position is unknown" +
                                        (actor.Unknown is null ? "" : $" ({actor.Unknown})");
                    else if (actor.Room != runtime.CurrentRoom)
                        cameraUnknown = $"it follows actor {followed} into Room {actor.Room:00}, which would load that room (not modelled)";
                    else if (!knownVariables.TryGetValue("var[0x002]", out var cameraBefore))
                        cameraUnknown = "the camera's position before it was unknown";
                    else
                    {
                        var strip = actorX / 8 - LoomCamera.ScreenStartStrip(cameraBefore);
                        var wasEstimated = estimated.Contains("var[0x002]");
                        runtime.Camera.Mode = LoomCamera.FollowActor;
                        runtime.Camera.Follows = followed;
                        if (strip is < LoomCamera.LeftTrigger or > LoomCamera.RightTrigger)
                        {
                            // setCameraAt in follow mode: the camera jumps only when the actor is more
                            // than half a screen away; otherwise moveCamera pans to the destination.
                            runtime.Camera.DestX = actorX;
                            if (Math.Abs(actorX - cameraBefore) > 160)
                            {
                                var clamped = (ushort)Math.Clamp(actorX, followRange.Min, followRange.Max);
                                knownVariables["var[0x002]"] = clamped;
                                RefreshBitAliases("var[0x002]", knownVariables);
                                trace.Add($"camera x = {clamped}: it follows actor {followed} at x = {actorX} (actorFollowCamera)");
                            }
                            else trace.Add($"camera pans from x = {cameraBefore} to actor {followed} at x = {actorX} (actorFollowCamera)");
                        }
                        else trace.Add($"camera x stays {cameraBefore}: actor {followed} at x = {actorX} is within its triggers (actorFollowCamera)");
                        MarkEstimated(estimated, "var[0x002]", onEstimatedPath || actor.Estimate is not null || wasEstimated ||
                            OperandIsEstimated(operand, estimated, knownVariables));
                        if (actor.Estimate is not null)
                            limitations.Add($"camera x follows actor {followed}, whose position is an estimate ({actor.Estimate}) (+0x{instruction.Offset:X4})");
                    }
                }
                if (cameraUnknown is not null)
                {
                    knownVariables.Remove("var[0x002]");
                    runtime.GlobalVariables.Remove("var[0x002]");
                    estimated.Remove("var[0x002]");
                    limitations.Add($"camera x (var[0x002]) is unknown after actorFollowCamera: {cameraUnknown} (+0x{instruction.Offset:X4})");
                }
                pc = next;
                continue;
            }

            if (instruction.Operation == "verbOps" && runtime is not null)
            {
                // o5_verbOps: changes one verb slot (see LoomVerbTable). This is game
                // state (script 006 and the close-up script 008 test it through
                // var[0x068]); drawing the verb area is left to presentation.
                var open = ArgumentListStart(instruction.Operands);
                var verbOperand = open > 0 ? instruction.Operands[..open].Trim().TrimEnd(',').Trim() : "";
                if (open < 0 || !instruction.Operands.EndsWith(']') ||
                    !TryReadValue(verbOperand, knownVariables, out var verbId, out _))
                {
                    runtime.Verbs.ForgetAll($"verbOps at +0x{instruction.Offset:X4} changes a verb the preview cannot read ({instruction.Operands})");
                    limitations.Add($"verbOps changes a verb the preview cannot read, so every verb slot is unknown (+0x{instruction.Offset:X4})");
                    pc = next;
                    continue;
                }
                var operations = new List<(string Name, int?[] Values, string? Text)>();
                var verbEstimated = onEstimatedPath || OperandIsEstimated(verbOperand, estimated, knownVariables);
                foreach (var item in instruction.Operands[(open + 1)..^1].Split("; ", StringSplitOptions.RemoveEmptyEntries))
                {
                    var comma = item.IndexOf(',');
                    var name = (comma < 0 ? item : item[..comma]).Trim();
                    var rest = comma < 0 ? "" : item[(comma + 1)..].Trim();
                    if (name == "name")
                    {
                        operations.Add((name, [], rest.Trim('"')));
                        continue;
                    }
                    var values = rest.Length == 0 ? [] : rest.Split(',', StringSplitOptions.TrimEntries)
                        .Select(operand =>
                        {
                            if (OperandIsEstimated(operand, estimated, knownVariables)) verbEstimated = true;
                            return TryReadValue(operand, knownVariables, out var value, out _) ? (int?)value : null;
                        }).ToArray();
                    operations.Add((name, values, null));
                }
                var applied = runtime.Verbs.Apply(verbId, operations,
                    objectId => roomObjects.Any(record => record.Id == objectId), runtime.CurrentRoom,
                    verbEstimated ? $"verbOps at +0x{instruction.Offset:X4} reads an estimate" : null);
                if (runtime.Verbs.LastCover is { } cover) applied += runtime.Messages.VerbDrawn(cover);
                trace.Add(applied + (verbEstimated ? " (estimate)" : ""));
                pc = next;
                continue;
            }

            if (instruction.Operation == "beginOverride")
            {
                // ScummEngine::beginOverride records the jump that follows and
                // steps over it. The jump runs only when the player skips the
                // cutscene; the preview never skips, so the normal path runs.
                if (!indexByOffset.TryGetValue(next, out var jumpIndex) || instructions[jumpIndex].Operation != "jumpRelative")
                {
                    unresolved = $"beginOverride at +0x{instruction.Offset:X4} is not followed by its skip jump";
                    break;
                }
                var jump = instructions[jumpIndex];
                trace.Add($"override set at +0x{instruction.Offset:X4}; its skip jump (+0x{jump.Offset:X4}) runs only if the player skips the cutscene, which the preview does not");
                runtime?.OverrideChanged?.Invoke(jump.Offset);
                pc = jump.Offset + EncodedLength(jump.Bytes);
                continue;
            }

            if (instruction.Operation == "endOverride")
            {
                // o5_beginOverride with 0: clears the recorded skip point.
                runtime?.OverrideChanged?.Invoke(null);
                pc = next;
                continue;
            }

            if (runtime is not null && instruction.Operation == "isActorInBox")
            {
                // o5_isActorInBox: jumpRelative(checkXYInBoxBounds(box, actor x, actor y)).
                if (!TryEvaluateActorInBox(instruction.Operands, runtime, knownVariables, out var inBox, out var boxDetail, out var boxEstimate))
                {
                    unresolved = $"{boxDetail} at +0x{instruction.Offset:X4}";
                    break;
                }
                if (!TryGetBranchTarget(instruction.Operands, out var boxTarget))
                {
                    unresolved = $"could not resolve branch at +0x{instruction.Offset:X4}";
                    break;
                }
                var boxOperands = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (!onEstimatedPath && (boxEstimate is not null || boxOperands.Take(2).Any(operand => OperandIsEstimated(operand, estimated, knownVariables))))
                {
                    onEstimatedPath = true;
                    if (boxEstimate is not null) limitations.Add($"isActorInBox reads {boxEstimate}, so what follows is an estimate (+0x{instruction.Offset:X4})");
                    trace.Add($"branch at +0x{instruction.Offset:X4} reads an estimated actor position; later steps are estimates");
                }
                pc = inBox ? next : boxTarget;
                continue;
            }

            if (runtime is not null && IsActorQuery(instruction.Operation))
            {
                var queryParts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (queryParts.Length != 2 || !TryGetVariableKey(queryParts[0], knownVariables, out var queryKey))
                {
                    unresolved = $"{instruction.Operation} operands are unknown ({instruction.Operands}) at +0x{instruction.Offset:X4}";
                    break;
                }
                if (!TryAnswerActorQuery(instruction.Operation, queryParts[1], runtime, roomObjects, knownVariables,
                        out var answer, out var queryDetail, out var queryEstimate))
                {
                    unresolved = $"{queryDetail} at +0x{instruction.Offset:X4}";
                    break;
                }
                knownVariables[queryKey] = unchecked((ushort)answer);
                RefreshBitAliases(queryKey, knownVariables);
                MarkEstimated(estimated, queryKey, onEstimatedPath || queryEstimate is not null ||
                    OperandIsEstimated(queryParts[1], estimated, knownVariables));
                if (queryEstimate is not null)
                    limitations.Add($"{instruction.Operation} reads {queryEstimate}, so what follows is an estimate (+0x{instruction.Offset:X4})");
                trace.Add($"{queryKey} = {answer}: {instruction.Operation} {queryParts[1]}{(queryEstimate is null ? "" : " (estimate)")}");
                pc = next;
                continue;
            }

            if (runtime is not null && IsActorPlacement(instruction.Operation))
            {
                var placementEstimated = onEstimatedPath || instruction.Operands
                    .Split(new[] { ',', '[', ']', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(operand => OperandIsEstimated(operand, estimated, knownVariables));
                ApplyActorPlacement(instruction, runtime, roomObjects, knownVariables, placementEstimated, trace);
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

        if (runtime is not null)
        {
            CopyGlobalVariables(knownVariables, runtime.GlobalVariables);
            CopyEstimatedGlobals(estimated, runtime.EstimatedVariables);
        }
        return new LoomBoundedScriptResult(new Dictionary<int, byte>(states),
            new Dictionary<int, RoomObjectPosition>(positions), new Dictionary<string, ushort>(knownVariables),
            trace, events, visited, reachedStop, unresolved, resumeOffset, drawQueue.ToArray(),
            picks, estimated.Where(key => !IsGlobalKey(key)).ToArray(), onEstimatedPath, delayJiffies, delayEstimate);
    }

    /// <summary>
    /// ScummVM <c>ScummEngine::findObject</c>: the first object in directory order
    /// that is not untouchable (class 24), whose parent chain is in the states it
    /// requires, and whose box contains the point; 0 when none. Unknown is set
    /// when an object whose box contains the point has a parent of unknown state.
    /// </summary>
    private static (int Found, string? Unknown) FindObjectAt(IReadOnlyList<LoomRoomObjectRecord> roomObjects,
        IReadOnlyDictionary<int, byte> states, LoomScriptRuntimeState runtime, int x, int y)
    {
        var byLocalIndex = roomObjects.GroupBy(item => item.LocalIndex).ToDictionary(group => group.Key, group => group.First());
        foreach (var record in roomObjects)
        {
            if (record.Id < 1 || runtime.ObjectClasses.TryGetValue(record.Id, out var classes) &&
                LoomObjectClasses.Has(classes, LoomObjectClasses.Untouchable)) continue;
            var at = runtime.ObjectPositions.TryGetValue(record.Id, out var moved) ? (moved.X, moved.Y) : (record.X, record.Y);
            if (!(at.Item1 <= x && at.Item1 + record.Width > x && at.Item2 <= y && at.Item2 + record.Height > y)) continue;
            var current = record;
            var parentsMatch = true;
            for (var remaining = roomObjects.Count; current.ParentLocalIndex != 0; remaining--)
            {
                if (remaining < 0 || !byLocalIndex.TryGetValue(current.ParentLocalIndex, out var parent))
                    return (0, $"hits object {record.Id}, whose parent chain cannot be read");
                if (!states.TryGetValue(parent.Id, out var parentState))
                    return (0, $"hits object {record.Id}, whose parent {parent.Id} has an unknown state");
                if ((parentState & 0x0F) != current.ParentRequiredState) { parentsMatch = false; break; }
                current = parent;
            }
            if (parentsMatch) return (record.Id, null);
        }
        return (0, null);
    }

    private static bool IsActorQuery(string operation) => operation is
        "getActorWalkBox" or "getActorX" or "getActorY" or "getActorRoom" or
        "getActorCostume" or "getActorElevation" or "getActorFacing" or "getActorMoving";

    private static bool IsActorPlacement(string operation) => operation is
        "putActor" or "putActorAtObject" or "putActorInRoom" or "walkActorTo" or "walkActorToObject" or
        "walkActorToActor" or "actorOps" or "setBoxFlags" or "animateActor" or "faceActor";

    /// <summary>ScummVM <c>OF_OWNER_ROOM</c>: the object lies in a room, not in an inventory.</summary>
    private const int RoomOwner = 0x0F;

    private static readonly Regex WalkToPattern = new(@"walkTo=\((-?\d+), (-?\d+)\)", RegexOptions.Compiled);

    /// <summary>
    /// The index of the <c>[</c> that opens an instruction's trailing argument list
    /// (<c>startScript 0x08, [local[0x002]]</c>), matched from its closing <c>]</c>
    /// so that <c>local[…]</c> operands before or inside it are skipped; −1 when there is none.
    /// </summary>
    internal static int ArgumentListStart(string operands)
    {
        var close = operands.LastIndexOf(']');
        var depth = 0;
        for (var index = close; index >= 0; index--)
        {
            if (operands[index] == ']') depth++;
            else if (operands[index] == '[' && --depth == 0) return index;
        }
        return -1;
    }

    private static (string Operands, List<int> EstimatedIndexes) SubstituteLocalArguments(string operands,
        IDictionary<string, ushort> variables, IReadOnlySet<string> estimated)
    {
        var estimatedIndexes = new List<int>();
        var open = ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) return (operands, estimatedIndexes);
        var arguments = operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((argument, index) =>
            {
                if (!argument.StartsWith("local[", StringComparison.Ordinal) || !variables.TryGetValue(argument, out var value))
                    return argument;
                if (estimated.Contains(argument)) estimatedIndexes.Add(index);
                return $"0x{value:X4}";
            }).ToArray();
        return ($"{operands[..(open + 1)]}{string.Join(", ", arguments)}{operands[close..]}", estimatedIndexes);
    }

    /// <summary>loadRoomWithEgo's object, room and walk destination (x = -1: no walk), each null when unknown.</summary>
    private static IReadOnlyList<int?> ResolveLoadRoomWithEgo(string operands, IDictionary<string, ushort> variables)
    {
        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
        int? Read(int index) => parts.Length > index && TryReadValue(parts[index], variables, out var value, out _) ? value : null;
        var walk = WalkToPattern.Match(operands);
        return
        [
            Read(0), Read(1),
            walk.Success ? int.Parse(walk.Groups[1].Value, CultureInfo.InvariantCulture) : null,
            walk.Success ? int.Parse(walk.Groups[2].Value, CultureInfo.InvariantCulture) : null
        ];
    }

    private static int? ReadSignedWord(string operand, IDictionary<string, ushort> variables) =>
        TryReadValue(operand, variables, out var value, out _) ? unchecked((short)value) : null;

    private static LoomActorTable.Placement PlacementOf(LoomScriptRuntimeState runtime) =>
        new(runtime.CurrentRoom, runtime.WalkBoxes, runtime.WalkBoxFlagsUnknown, runtime.ObjectClasses);

    /// <summary>
    /// ScummVM <c>whereIsObject</c> then <c>getObjectXYPos</c> for v3: an object of
    /// the current room gives its walk point. Found is false for an object that
    /// is in no room and no inventory; the point is null when that is unknown
    /// (an unknown number, or an object held in an inventory, which is not modelled).
    /// </summary>
    private static (bool Found, (int X, int Y)? Point) FindObjectWalkPoint(int? objectId,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects, LoomScriptRuntimeState runtime)
    {
        if (objectId is not int id) return (true, null);
        if (id < 1) return (false, null);
        var owned = runtime.ObjectOwners.TryGetValue(id, out var owner);
        if (owned && owner != RoomOwner) return (true, null);
        var record = roomObjects.FirstOrDefault(item => item.Id == id);
        if (record is not null) return (true, (record.WalkX, record.WalkY));
        return owned ? (false, null) : (true, null);
    }

    /// <summary>
    /// Applies an actor command to the session's actor table with its operands
    /// resolved now. An operand that cannot be read makes what it sets unknown;
    /// an unknown actor number makes every actor unknown. Values that follow a
    /// seeded pick are marked as estimates.
    /// </summary>
    private static void ApplyActorPlacement(LoomScriptInstruction instruction, LoomScriptRuntimeState runtime,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects, IDictionary<string, ushort> variables, bool estimate,
        List<string> trace)
    {
        var actors = runtime.Actors;
        var place = PlacementOf(runtime);
        int? ego = variables.TryGetValue("var[0x001]", out var egoValue) ? egoValue : null;
        var label = $"{instruction.Operation} at +0x{instruction.Offset:X4}";
        var operands = instruction.Operands;
        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
        int? Read(int index) => parts.Length > index && TryReadValue(parts[index], variables, out var value, out _) ? value : null;

        if (instruction.Operation == "setBoxFlags")
        {
            // o5_setBoxFlags: the current room's box gets the flags byte.
            if (Read(0) is not int box || Read(1) is not int flags)
                runtime.WalkBoxFlagsUnknown = $"changed by {label} with an unknown box ({operands})";
            else if (runtime.WalkBoxes?.SetFlags(box, (byte)flags) == true)
                trace.Add($"walk box {box} flags = 0x{flags:X2}");
            return;
        }

        var numberOperand = instruction.Operation == "actorOps" ? operands[..Math.Max(0, operands.IndexOf(", [", StringComparison.Ordinal))] : parts[0];
        if (!TryReadValue(numberOperand, variables, out var rawNumber, out _))
        {
            if (instruction.Operation is "animateActor" or "faceActor")
            {
                trace.Add($"{label}: unknown actor number; no actor's frame or facing is changed");
                return;
            }
            actors.ForgetAll($"{label} names an actor the preview cannot read ({numberOperand})");
            trace.Add($"{label}: unknown actor number; every actor's position is now unknown");
            return;
        }
        int number = rawNumber;
        if (!LoomActorTable.IsValid(number))
        {
            trace.Add($"{label}: actor {number} is not a valid actor; ignored");
            return;
        }
        switch (instruction.Operation)
        {
            case "putActor":
                actors.Put(number, ReadSignedWord(parts[1], variables), ReadSignedWord(parts[2], variables), place, ego);
                break;
            case "putActorAtObject":
            {
                // o5_putActorAtObject: the object's walk point, or (240, 120) when it is not found.
                var (found, point) = FindObjectWalkPoint(Read(1), roomObjects, runtime);
                if (!found) point = (240, 120);
                if (point is null) actors.Put(number, null, null, place, ego);
                else actors.Put(number, point.Value.X, point.Value.Y, place, ego);
                break;
            }
            case "putActorInRoom":
                actors.PutInRoom(number, Read(1), place, ego);
                break;
            case "walkActorTo":
                actors.Walk(number, ReadSignedWord(parts[1], variables), ReadSignedWord(parts[2], variables), place, $"actor {number}'s {label}");
                break;
            case "walkActorToObject":
            {
                // o5_walkActorToObject: walks only when the object is found.
                var (found, point) = FindObjectWalkPoint(Read(1), roomObjects, runtime);
                if (!found) break;
                // getObjectXYPos gives the object's actor direction (oldDirToNewDir(actordir & 3)) as the walk's end direction.
                var facing = roomObjects.FirstOrDefault(item => item.Id == Read(1)) is { } walkedTo
                    ? LoomCostume.OldDirToNewDir(walkedTo.Direction & 3) : -1;
                actors.Walk(number, point?.X, point?.Y, place, $"actor {number}'s {label}", facing);
                break;
            }
            case "walkActorToActor":
            {
                // o5_walkActorToActor: both actors must be in the current room; the walk
                // ends dist pixels to the near side of the other actor (0xFF: by width).
                var target = Read(1) is int other && LoomActorTable.IsValid(other) ? actors[other] : null;
                var walker = actors[number];
                if (walker.Room is null || Read(1) is null || (target is not null && target.Room is null))
                {
                    actors.Forget(number, $"{label}: whether both actors are in the room is unknown");
                    break;
                }
                if (target is null || walker.Room != place.CurrentRoom || target.Room != place.CurrentRoom) break;
                if (Read(2) is not int distance || distance == 0xFF || target.X is null || target.Y is null || walker.X is null)
                {
                    actors.Forget(number, $"{label}: the distance or an actor position is unknown");
                    break;
                }
                var x = target.X < walker.X ? target.X + distance : target.X - distance;
                actors.Walk(number, x, target.Y, place, $"actor {number}'s {label}");
                break;
            }
            case "actorOps":
                foreach (var item in operands[(operands.IndexOf(", [", StringComparison.Ordinal) + 3)..operands.LastIndexOf(']')]
                             .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (item == "init")
                    {
                        // initActor(0): also clears the actor's classes.
                        actors.Init(number);
                        runtime.ObjectClasses[number] = 0;
                    }
                    else if (item is "ignoreBoxes" or "followBoxes")
                    {
                        actors.SetIgnoreBoxes(number, item == "ignoreBoxes");
                        var room = actors[number].Room;
                        if (room is null) actors.Forget(number, $"{label}: whether it is in the current room is unknown");
                        else if (room == place.CurrentRoom) actors.Replace(number, place, ego);
                    }
                    else if (item.StartsWith("costume", StringComparison.Ordinal))
                        actors.CostumeChanged(number, ReadItem(item, 1), place);
                    else if (item.StartsWith("talkColor,", StringComparison.Ordinal) &&
                             TryReadValue(item["talkColor,".Length..].Trim(), variables, out var talkColor, out _))
                        actors.SetTalkColor(number, talkColor & 0xFF);
                    else if (item.StartsWith("elevation,", StringComparison.Ordinal))
                        actors.SetElevation(number, ReadItem(item, 1) is int elevation ? (short)elevation : null);
                    else if (item.StartsWith("walkSpeed,", StringComparison.Ordinal) && ReadItem(item, 1) is int speedX && ReadItem(item, 2) is int speedY)
                        actors.SetWalkSpeed(number, speedX & 0xFF, speedY & 0xFF);
                    else if (item.StartsWith("palette,", StringComparison.Ordinal) && ReadItem(item, 1) is int index && ReadItem(item, 2) is int color)
                        actors.SetPalette(number, index & 0xFF, color & 0xFF);
                    else if (item.StartsWith("talkAnimation,", StringComparison.Ordinal))
                    {
                        if (ReadItem(item, 1) is int talkStart) actors.SetFrame(number, "talkStart", talkStart & 0xFF);
                        if (ReadItem(item, 2) is int talkStop) actors.SetFrame(number, "talkStop", talkStop & 0xFF);
                    }
                    else if (item.Split(',')[0] is "initAnimation" or "walkAnimation" or "standAnimation" && ReadItem(item, 1) is int frame)
                        actors.SetFrame(number, item.Split(',')[0][..^"Animation".Length], frame & 0xFF);
                }
                break;
            case "animateActor":
                if (Read(1) is not int animation)
                {
                    trace.Add($"{label}: actor {number}'s animation value is unknown; its frame is not changed");
                    return;
                }
                trace.Add($"actor {number}: {label} 0x{animation & 0xFF:X2} ({actors.Animate(number, animation & 0xFF, place)})");
                return;
            case "faceActor":
            {
                // o5_faceActor: faceToObject(obj), with an actor's position and width (24) or the object's walk point and width.
                if (Read(1) is not int target) break;
                int? x = null, y = null, width = null;
                if (LoomActorTable.IsValid(target))
                {
                    (x, y, width) = (actors[target].X, actors[target].Y, 24);
                    if (actors[target].Room != place.CurrentRoom) break;
                }
                else
                {
                    var record = roomObjects.FirstOrDefault(item => item.Id == target);
                    if (record is null) break;
                    (x, y, width) = (record.WalkX, record.WalkY, record.Width);
                }
                if (x is int faceX && y is int faceY && width is int faceWidth) actors.FaceTo(number, faceX, faceY, faceWidth, place);
                break;
            }
        }
        if (estimate) actors.MarkEstimate(number, $"actor {number}'s {label} follows a seeded pick");
        var state = actors[number];
        trace.Add($"actor {number}: {label} → room {Show(state.Room)}, ({Show(state.X)}, {Show(state.Y)}), box {Show(state.WalkBox)}, " +
                  $"{(state.Visible is bool visible ? visible ? "visible" : "hidden" : "visibility unknown")}{(state.Estimate is null ? "" : " (estimate)")}");

        int? ReadItem(string item, int index)
        {
            var itemParts = item.Split(',', StringSplitOptions.TrimEntries);
            return itemParts.Length > index && TryReadValue(itemParts[index], variables, out var value, out _) ? value : null;
        }
    }

    /// <summary>
    /// Reads a <c>print</c> or <c>printEgo</c> as ScummVM's <c>o5_print</c> does:
    /// the actor (<c>getVarOrDirectByte</c>, or <c>VAR_EGO</c>), each subcommand's
    /// operands as they are now, and the text's bytes. An operand that cannot be
    /// read stays null; values that follow a seeded pick make the message an estimate.
    /// </summary>
    private static bool TryReadPrint(LoomScriptInstruction instruction, LoomScriptDisassembly script,
        IDictionary<string, ushort> variables, IReadOnlySet<string> estimated, bool onEstimatedPath,
        LoomScriptRuntimeState runtime, out LoomPrintRequest request, out string detail)
    {
        request = null!;
        var operands = instruction.Operands;
        var split = operands.IndexOf(", [", StringComparison.Ordinal);
        if (split < 0 || !operands.EndsWith(']'))
        {
            detail = $"print operands are not in the expected form ({operands})";
            return false;
        }
        var actorOperand = instruction.Operation == "printEgo" ? "var[0x001]" : operands[..split].Trim();
        if (!TryReadValue(actorOperand, variables, out var actor, out _))
        {
            detail = $"print names an actor the preview cannot read ({actorOperand})";
            return false;
        }
        var body = operands[(split + 3)..^1];
        byte[]? text = null;
        var textAt = body.IndexOf("text, \"", StringComparison.Ordinal);
        if (textAt >= 0)
        {
            var close = body.LastIndexOf('"');
            text = LoomScummTextDecoder.ParseEscaped(body[(textAt + 7)..close]);
            body = body[..textAt];
        }
        var estimate = onEstimatedPath || OperandIsEstimated(actorOperand, estimated, variables)
            ? "the print follows a seeded pick" : null;
        var operations = new List<LoomPrintOperation>();
        foreach (var item in body.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(',', StringSplitOptions.TrimEntries);
            int? Read(int index)
            {
                if (parts.Length <= index || !TryReadValue(parts[index], variables, out var value, out _)) return null;
                if (OperandIsEstimated(parts[index], estimated, variables)) estimate ??= $"the print's {parts[0]} follows a seeded pick";
                return (short)value;
            }
            operations.Add(new LoomPrintOperation(parts[0], Read(1), Read(2)));
        }
        request = new LoomPrintRequest(actor & 0xFF, operations, text, script.Context ?? $"Script {script.ScriptId:00}",
            instruction.Offset, runtime.CurrentRoom, estimate, new Dictionary<string, ushort>(variables, StringComparer.Ordinal));
        detail = "";
        return true;
    }

    /// <summary>An actor command on a branch the preview could not decide may have run: what it sets becomes unknown.</summary>
    private static void ForgetPossiblePlacement(LoomScriptInstruction possible, LoomScriptRuntimeState runtime,
        IDictionary<string, ushort> variables, int branchOffset)
    {
        var reason = $"{possible.Operation} at +0x{possible.Offset:X4} may run on a branch the preview could not decide (+0x{branchOffset:X4})";
        if (possible.Operation == "setBoxFlags")
        {
            runtime.WalkBoxFlagsUnknown ??= reason;
            return;
        }
        var operand = possible.Operands.Split(',', StringSplitOptions.TrimEntries)[0];
        if (TryReadValue(operand, variables, out var number, out _))
        {
            if (LoomActorTable.IsValid(number)) runtime.Actors.Forget(number, reason);
        }
        else runtime.Actors.ForgetAll(reason);
    }

    private static string Show(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";

    /// <summary>
    /// o5_getActorWalkBox (the actor's walk box, in any room), o5_getActorRoom,
    /// and o5_getActorX/Y through <c>getObjX</c>/<c>getObjY</c>: an actor number
    /// gives its position; another number gives the object's walk point, or -1
    /// when the object is not found. Fails when the value is unknown.
    /// </summary>
    private static bool TryAnswerActorQuery(string operation, string operand, LoomScriptRuntimeState runtime,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects, IDictionary<string, ushort> variables,
        out int answer, out string detail, out string? estimate)
    {
        answer = 0;
        estimate = null;
        if (!TryReadValue(operand, variables, out var raw, out var readDetail))
        {
            detail = $"{operation} names an actor the preview cannot read ({readDetail})";
            return false;
        }
        int number = raw;
        if (operation is "getActorX" or "getActorY")
        {
            if (number < 1)
            {
                detail = "known value";
                return true;
            }
            if (number >= LoomActorTable.Count)
            {
                var (found, point) = FindObjectWalkPoint(number, roomObjects, runtime);
                if (found && point is null)
                {
                    detail = $"{operation} reads object {number}, whose position the preview does not know (it is held or not in this room's records)";
                    return false;
                }
                answer = !found ? -1 : operation == "getActorX" ? point!.Value.X : point!.Value.Y;
                detail = "known value";
                return true;
            }
        }
        if (!LoomActorTable.IsValid(number))
        {
            if (operation == "getActorRoom")
            {
                detail = "known value";
                return true;
            }
            detail = $"{operation} names actor {number}, which is not a valid actor";
            return false;
        }
        var actor = runtime.Actors[number];
        int? value = operation switch
        {
            "getActorWalkBox" => actor.WalkBox,
            "getActorRoom" => actor.Room,
            "getActorX" => actor.X,
            // o5_getActorCostume/Elevation; o5_getActorFacing gives newDirToOldDir(facing).
            "getActorCostume" => actor.Look.Costume,
            "getActorElevation" => actor.Look.Elevation,
            "getActorFacing" => actor.Look.Direction,
            // o5_getActorMoving: the actor's _moving flags.
            "getActorMoving" => actor.Moving,
            _ => actor.Y
        };
        if (value is null)
        {
            var what = operation switch
            {
                "getActorWalkBox" => "walk box",
                "getActorRoom" => "room",
                "getActorCostume" => "costume",
                "getActorElevation" => "elevation",
                "getActorFacing" => "facing",
                _ => "position"
            };
            detail = $"{operation} reads actor {number}, whose {what} the preview does not know ({actor.Look.Estimate ?? actor.Unknown ?? "not known"})";
            return false;
        }
        if (operation is "getActorCostume" or "getActorElevation" or "getActorFacing" or "getActorMoving")
        {
            answer = value.Value;
            if (operation == "getActorFacing" && actor.Look.Estimate is not null) estimate = $"actor {number}: {actor.Look.Estimate}";
            detail = "known value";
            return true;
        }
        answer = value.Value;
        if (actor.Estimate is not null) estimate = $"actor {number}: {actor.Estimate}";
        detail = "known value";
        return true;
    }

    /// <summary>
    /// ScummVM <c>getObjActToObjActDist</c>: numbers below 13 are actors, others
    /// objects. Two actors in the same room that is not the current one are 0
    /// apart; an actor outside the current room or an object that is not found
    /// gives 0xFF. When the first is an actor and the second an object, the
    /// object's walk point is adjusted into the actor's walk boxes first. The
    /// distance is the larger of the x and y differences (<c>getDist</c>).
    /// </summary>
    private static bool TryMeasureDistance(string first, string second, LoomScriptRuntimeState runtime,
        IReadOnlyList<LoomRoomObjectRecord> roomObjects, IDictionary<string, ushort> variables,
        out int distance, out string detail, out string? estimate)
    {
        distance = 0;
        estimate = null;
        detail = "known value";
        if (!TryReadValue(first, variables, out var a, out _) || !TryReadValue(second, variables, out var b, out _))
        {
            detail = $"getDist reads an object or actor the preview cannot read ({first}, {second})";
            return false;
        }
        var actorA = LoomActorTable.IsValid(a) ? runtime.Actors[a] : null;
        var actorB = LoomActorTable.IsValid(b) ? runtime.Actors[b] : null;
        if (actorA is not null && actorB is not null)
        {
            if (actorA.Room is null || actorB.Room is null)
            {
                detail = $"getDist reads actors {a} and {b}, one of whose rooms the preview does not know";
                return false;
            }
            if (actorA.Room == actorB.Room && actorA.Room != 0 && actorA.Room != runtime.CurrentRoom) return true;
        }
        var estimates = new List<string>();
        (int X, int Y)? Locate(int number, LoomActorSnapshot? actor, out string? unknown)
        {
            unknown = null;
            if (number < LoomActorTable.Count)
            {
                // getObjectOrActorXY: an actor counts only in the current room.
                if (actor is null) return null;
                if (actor.Room is null) { unknown = $"getDist reads actor {number}, whose room the preview does not know ({actor.Unknown ?? "not known"})"; return null; }
                if (actor.Room != runtime.CurrentRoom) return null;
                if (actor.X is not int x || actor.Y is not int y) { unknown = $"getDist reads actor {number}, whose position the preview does not know ({actor.Unknown ?? "not known"})"; return null; }
                if (actor.Estimate is not null) estimates.Add($"actor {number}: {actor.Estimate}");
                return (x, y);
            }
            var (found, point) = FindObjectWalkPoint(number, roomObjects, runtime);
            if (found && point is null) unknown = $"getDist reads object {number}, whose position the preview does not know (it is held or not in this room's records)";
            return point;
        }
        var pointA = Locate(a, actorA, out var unknownA);
        if (unknownA is not null) { detail = unknownA; return false; }
        if (pointA is null) { distance = 0xFF; return true; }
        var pointB = Locate(b, actorB, out var unknownB);
        if (unknownB is not null) { detail = unknownB; return false; }
        if (pointB is null) { distance = 0xFF; return true; }
        var (x2, y2) = pointB.Value;
        if (actorA is not null && actorB is null)
        {
            if (runtime.Actors.AdjustPoint(a, x2, y2, PlacementOf(runtime)) is not { } adjusted)
            {
                detail = $"getDist adjusts object {b}'s position into actor {a}'s walk boxes, which the preview does not know";
                return false;
            }
            (x2, y2) = adjusted;
        }
        distance = Math.Max(Math.Abs(pointA.Value.X - x2), Math.Abs(pointA.Value.Y - y2));
        if (estimates.Count > 0) estimate = string.Join("; ", estimates);
        return true;
    }

    private static bool TryEvaluateActorInBox(string operands, LoomScriptRuntimeState runtime,
        IDictionary<string, ushort> variables, out bool inBox, out string detail, out string? estimate)
    {
        inBox = false;
        estimate = null;
        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || !TryReadValue(parts[0], variables, out var number, out _) ||
            !TryReadValue(parts[1], variables, out var box, out _))
        {
            detail = $"isActorInBox operands are unknown ({operands})";
            return false;
        }
        if (!LoomActorTable.IsValid(number))
        {
            detail = $"isActorInBox names actor {number}, which is not a valid actor";
            return false;
        }
        var actor = runtime.Actors[number];
        if (actor.X is not int x || actor.Y is not int y || runtime.WalkBoxes is null)
        {
            detail = runtime.WalkBoxes is null
                ? "isActorInBox needs the current room's walk boxes"
                : $"isActorInBox reads actor {number}, whose position the preview does not know ({actor.Unknown ?? "not known"})";
            return false;
        }
        inBox = runtime.WalkBoxes.Contains(box, x, y);
        if (actor.Estimate is not null) estimate = $"actor {number}: {actor.Estimate}";
        detail = "known value";
        return true;
    }

    private static bool IsGlobalKey(string key) =>
        key.StartsWith("var[", StringComparison.Ordinal) || key.StartsWith("bit[", StringComparison.Ordinal);

    private static void CopyEstimatedGlobals(IEnumerable<string> source, HashSet<string> destination)
    {
        var globals = source.Where(IsGlobalKey).ToArray();
        destination.Clear();
        destination.UnionWith(globals);
    }

    private static bool TryNormalizeKey(string operand, IDictionary<string, ushort> variables, out string key)
    {
        key = "";
        var match = VariablePattern.Match(ResolveIndexed(operand, variables));
        if (!match.Success) return false;
        key = $"{match.Groups[1].Value}[0x{int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]";
        return true;
    }

    private static bool IsEstimated(string key, IReadOnlySet<string> estimated)
    {
        if (estimated.Contains(key)) return true;
        // A bit reads its backing global word.
        return key.StartsWith("bit[0x", StringComparison.Ordinal) &&
               int.TryParse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit) &&
               estimated.Contains($"var[0x{bit >> 4:X3}]");
    }

    /// <summary>
    /// True when the operand's value, or for an indexed operand the index that
    /// chooses it, came from a seeded pick.
    /// </summary>
    private static bool OperandIsEstimated(string operand, IReadOnlySet<string> estimated,
        IDictionary<string, ushort> variables, bool indexOnly = false)
    {
        var indexed = IndexedPattern.Match(operand.Trim());
        if (indexed.Success && OperandIsEstimated(indexed.Groups[3].Value, estimated, variables)) return true;
        return !indexOnly && TryNormalizeKey(operand, variables, out var key) && IsEstimated(key, estimated);
    }

    private static void MarkEstimated(HashSet<string> estimated, string key, bool value)
    {
        if (value) estimated.Add(key);
        else estimated.Remove(key);
        // A word written as a whole replaces what its bits held.
        if (!key.StartsWith("var[0x", StringComparison.Ordinal) ||
            !int.TryParse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var word)) return;
        for (var bit = 0; bit < 16; bit++) estimated.Remove($"bit[0x{word * 16 + bit:X3}]");
    }

    private static bool ConditionReadsEstimate(LoomScriptInstruction instruction,
        IReadOnlySet<string> estimated, IReadOnlySet<int>? estimatedStates, IDictionary<string, ushort> variables)
    {
        var expression = instruction.Operands;
        var branchIndex = expression.IndexOf(", branch ", StringComparison.Ordinal);
        if (branchIndex >= 0) expression = expression[..branchIndex];
        var values = expression.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Any(value => OperandIsEstimated(value, estimated, variables))) return true;
        return instruction.Operation is "ifState" or "ifNotState" or "ifClassOfIs" && estimatedStates is not null &&
               values.Length > 0 && TryReadValue(values[0], variables, out var objectId, out _) && estimatedStates.Contains(objectId);
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
        "equalZero" or "notEqualZero" or "ifState" or "ifNotState" or "ifClassOfIs";

    /// <summary>Reads "obj, [c1, c2, ...]" as used by setClass and ifClassOfIs.</summary>
    private static bool TryReadClassOperands(string operands, IDictionary<string, ushort> variables,
        out int objectId, out IReadOnlyList<int> classes)
    {
        objectId = 0;
        classes = [];
        var open = operands.IndexOf(", [", StringComparison.Ordinal);
        var close = open < 0 ? -1 : operands.IndexOf(']', open + 3);
        if (open < 0 || close < 0 || !TryReadValue(operands[..open], variables, out var rawObject, out _)) return false;
        var values = new List<int>();
        foreach (var item in operands[(open + 3)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryReadValue(item, variables, out var value, out _)) return false;
            values.Add(value);
        }
        objectId = rawObject;
        classes = values;
        return true;
    }

    private static bool TryEvaluateCondition(LoomScriptInstruction instruction,
        IDictionary<string, ushort> variables, IReadOnlyDictionary<int, byte> objectStates,
        IReadOnlyDictionary<int, uint>? objectClasses, out bool result, out string detail)
    {
        result = false;
        var expression = instruction.Operands;
        var branchIndex = expression.IndexOf(", branch ", StringComparison.Ordinal);
        if (branchIndex >= 0) expression = expression[..branchIndex];
        if (instruction.Operation == "ifClassOfIs")
        {
            // o5_ifClassOfIs: true unless a listed class 0x80|n is missing or a listed n is present.
            if (!TryReadClassOperands(expression, variables, out var classObject, out var classes))
            {
                detail = $"class test operands ({expression})";
                return false;
            }
            if (objectClasses is null || !objectClasses.TryGetValue(classObject, out var bits))
            {
                detail = $"object {classObject} classes, which this run does not track";
                return false;
            }
            result = classes.All(cls => LoomObjectClasses.Has(bits, cls & 0x7F) == ((cls & 0x80) != 0));
            detail = "known object classes";
            return true;
        }
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
        var keyMatch = VariablePattern.Match(ResolveIndexed(parts[0], variables));
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
        "setObjectName" or "actorFollowCamera" or "drawBox";

    private static bool TryGetBranchTarget(string operands, out int target)
    {
        target = 0;
        var marker = operands.LastIndexOf("->", StringComparison.Ordinal);
        if (marker < 0) return false;
        var targetText = operands[(marker + 2)..].Trim();
        return targetText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(targetText.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out target);
    }

    /// <summary>
    /// Rewrites an indexed operand such as <c>var[0x0B3+local[0x002]]</c> as the
    /// plain variable it addresses now (ScummVM <c>readVar</c>/<c>writeVar</c>
    /// with 0x2000). Other operands, and indexes that are unknown, are returned unchanged.
    /// </summary>
    private static string ResolveIndexed(string operand, IDictionary<string, ushort>? variables)
    {
        var trimmed = operand.Trim();
        var match = IndexedPattern.Match(trimmed);
        if (!match.Success || variables is null) return trimmed;
        var inner = match.Groups[3].Value;
        if (!TryReadLiteral(inner, out var index) && !TryReadValue(inner, variables, out index, out _)) return trimmed;
        var address = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) + (index & 0xFFF);
        return $"{match.Groups[1].Value}[0x{address:X3}]";
    }

    private static bool TryReadValue(string operand, IDictionary<string, ushort> variables,
        out ushort value, out string detail)
    {
        value = 0;
        operand = ResolveIndexed(operand, variables);
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

    private static bool TryGetVariableKey(string operand, IDictionary<string, ushort> variables, out string variableKey)
    {
        variableKey = "";
        var match = VariablePattern.Match(ResolveIndexed(operand, variables));
        if (!match.Success || match.Groups[1].Value == "bit") return false;
        variableKey = $"{match.Groups[1].Value}[0x{int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]";
        return true;
    }

    /// <summary>
    /// Reads <c>setVarRange</c> operands as the decoder prints them
    /// (<c>var[0x0B3], count=5, [0x0294, ...]</c>): the variables written, in
    /// order, and their literal values. Signed words are kept as their 16-bit
    /// pattern, as SCUMM stores them.
    /// </summary>
    internal static bool TryReadVarRange(string operands, IDictionary<string, ushort> variables,
        out string destination, out IReadOnlyList<string> keys, out IReadOnlyList<ushort> values)
    {
        keys = [];
        values = [];
        var countAt = operands.IndexOf(", count=", StringComparison.Ordinal);
        var open = operands.IndexOf('[', Math.Max(countAt, 0));
        destination = countAt < 0 ? "" : operands[..countAt].Trim();
        if (countAt < 0 || open < 0 || !operands.EndsWith(']') ||
            !int.TryParse(operands[(countAt + 8)..open].Trim(' ', ','), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var count) ||
            !TryGetVariableKey(destination, variables, out var first)) return false;
        var literals = new List<ushort>();
        foreach (var item in operands[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries))
        {
            if (!TryReadLiteral(item, out var value)) return false;
            literals.Add(value);
        }
        if (literals.Count != count || count == 0) return false;
        var match = VariablePattern.Match(first);
        var kind = match.Groups[1].Value;
        var start = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        keys = Enumerable.Range(start, count).Select(number => $"{kind}[0x{number:X3}]").ToArray();
        values = literals;
        return true;
    }

    private static bool TryReadDelay(LoomScriptInstruction instruction, IDictionary<string, ushort> variables, out int jiffies)
    {
        jiffies = 0;
        if (instruction.Operation == "delayVariable")
        {
            if (!TryReadValue(instruction.Operands, variables, out var value, out _)) return false;
            jiffies = value;
            return true;
        }
        return int.TryParse(ReadDelay(instruction.Operands), NumberStyles.Integer, CultureInfo.InvariantCulture, out jiffies);
    }

    private static string ReadDelay(string operand) =>
        operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var jiffies)
            ? jiffies.ToString(CultureInfo.InvariantCulture) : operand;

    private static bool TryReadLiteral(string operand, out ushort value)
    {
        value = 0;
        return operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static int EncodedLength(string bytes) =>
        bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}

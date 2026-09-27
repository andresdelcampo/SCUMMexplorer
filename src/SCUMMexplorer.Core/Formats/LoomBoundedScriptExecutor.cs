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
    bool PathEstimated = false)
{
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
            if (scriptEvent is { TargetId: null, Kind: LoomScriptEventKind.MusicStarted or
                    LoomScriptEventKind.SoundStarted or LoomScriptEventKind.SoundStopped } &&
                TryReadValue(instruction.Operands.Trim(), knownVariables, out var soundId, out _))
                scriptEvent = scriptEvent with { TargetId = soundId };
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
                pc = jump.Offset + EncodedLength(jump.Bytes);
                continue;
            }

            if (instruction.Operation == "endOverride")
            {
                // o5_beginOverride with 0: clears the recorded skip point.
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
            picks, estimated.Where(key => !IsGlobalKey(key)).ToArray(), onEstimatedPath);
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

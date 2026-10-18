using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummBoundedScriptExecutor
{
    private const string V2IndirectReads = "engine:v2-indirect-reads";

    // script_v2.cpp dispatches 0x66/0xE6 to o5_getClosestObjActor (script_v5.cpp).
    // vars.cpp setupScummVars v2 uses raw words 19/20 for the inclusive actor range.
    // Read from ScummVM master, 18 October 2026. Descending search and strict comparison
    // intentionally retain the greatest id on a tie and reject distances of 255 or more.
    private static bool TryApplyV2Closest(ScummScriptInstruction instruction,
        Dictionary<string, ushort> variables, HashSet<string> estimated, ScummScriptRuntimeState runtime,
        IReadOnlyList<ScummRoomObjectRecord> objects, bool estimatedPath, List<string> trace,
        HashSet<string> limitations, out string? problem)
    {
        problem = $"getClosestObjActor operands or actor range are unknown at +0x{instruction.Offset:X4}";
        const string minimumKey = "var[0x013]", maximumKey = "var[0x014]";
        var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TryGetVariableKey(parts[0], variables, out var destination) ||
            !TryReadValue(parts[1], variables, out _, out _) ||
            !variables.TryGetValue(minimumKey, out var minimum) || !variables.TryGetValue(maximumKey, out var maximum)) return false;
        var closest = 255;
        var closestDistance = 255;
        var isEstimate = estimatedPath || estimated.Contains(minimumKey) || estimated.Contains(maximumKey) ||
            OperandIsEstimated(parts[1], estimated, variables);
        var positionEstimates = new HashSet<string>(StringComparer.Ordinal);
        // The native do/while examines maximum once even when minimum > maximum.
        for (var candidate = (int)maximum; ; candidate--)
        {
            if (!TryMeasureDistance(parts[1], $"0x{candidate:X4}", runtime, objects, variables,
                    out var distance, out var detail, out var positionEstimate))
            { problem = $"getClosestObjActor: {detail} at +0x{instruction.Offset:X4}"; return false; }
            if (positionEstimate is not null) { isEstimate = true; positionEstimates.Add(positionEstimate); }
            if (distance < closestDistance) { closestDistance = distance; closest = candidate; }
            if (candidate - 1 < minimum) break;
        }
        variables[destination] = (ushort)closest;
        RefreshBitAliases(destination, variables);
        MarkEstimated(estimated, destination, isEstimate);
        runtime.NotifyGlobalMutation(destination);
        foreach (var estimate in positionEstimates)
            limitations.Add($"getClosestObjActor reads {estimate}, so what follows is an estimate (+0x{instruction.Offset:X4})");
        trace.Add($"{destination} = {closest}: getClosestObjActor {parts[1]}, inclusive range {minimum}..{maximum}{(isEstimate ? " (estimate)" : "")}");
        problem = null;
        return true;
    }
    private static bool TryApplyV2Pickup(ScummScriptInstruction instruction, IDictionary<string, ushort> variables,
        IReadOnlyList<ScummRoomObjectRecord> objects, ScummScriptRuntimeState runtime, List<int> drawQueue,
        List<string> trace, ScummScriptEventCertainty certainty, out string problem)
    {
        problem = "v2 pickup needs a known object and ego";
        if (!TryReadValue(instruction.Operands.Trim(), variables, out var obj, out _) || obj < 1) return false;
        if (!objects.Any(o => o.Id == obj) || runtime.ObjectOwners.TryGetValue(obj, out var owner) && owner != RoomOwner)
        { trace.Add($"v2 pickup {obj}: absent from room or already in inventory"); problem = ""; return true; }
        if (!variables.TryGetValue(runtime.EgoVariable, out var ego) || !runtime.ObjectStates.TryGetValue(obj, out var state)) return false;
        if (!runtime.AddToInventorySlots(obj)) { problem = "v2 inventory is full"; return false; }
        runtime.CopyV2InventoryHeader?.Invoke(obj);
        if (runtime.CurrentRoom is int room) runtime.InventoryRooms[obj] = room;
        runtime.SetObjectOwner(obj, (byte)ego);
        runtime.AddInventoryObject(obj);
        runtime.SetObjectState(obj, (byte)(state | 8 | 2));
        runtime.StatesSetInRoom.Add(obj);
        if (certainty != ScummScriptEventCertainty.Reached) runtime.EstimatedStates.Add(obj);
        drawQueue.Clear();
        // ScummEngine_v2::runInventoryScript only redraws the inventory; no script 1 is launched.
        trace.Add($"v2 pickup {obj}: owner {ego}, intrinsic and untouchable flags set; inventory display redraw deferred");
        problem = "";
        return true;
    }
    private static IReadOnlyList<ScummScriptInstruction> ExpandV2Overrides(IReadOnlyList<ScummScriptInstruction> source)
    {
        var result = new List<ScummScriptInstruction>();
        foreach (var instruction in source)
        {
            var bytes = instruction.Bytes.Split(' ');
            if (instruction.Operation != "beginOverride" || bytes.Length != 4) { result.Add(instruction); continue; }
            var displacement = unchecked((short)(Convert.ToByte(bytes[2], 16) | Convert.ToByte(bytes[3], 16) << 8));
            result.Add(instruction with { Bytes = bytes[0], Operands = "" });
            result.Add(new(instruction.Offset + 1, 0x18, "jumpRelative", $"{displacement} -> 0x{instruction.Offset + 4 + displacement:X4}", string.Join(' ', bytes.Skip(1))));
        }
        return result;
    }
    // The v2 handlers in script_v2.cpp, ScummVM master read 17 October 2026. Only operations whose semantic form is identical
    // are handed to the shared executor; source offsets and encoded lengths remain those of the original instruction.
    private static bool PrepareV2Instruction(ScummScriptInstruction source, IDictionary<string, ushort> variables,
        IDictionary<int, byte> states, ScummScriptRuntimeState runtime, IReadOnlyList<ScummRoomObjectRecord> objects,
        List<string> trace, bool estimatedPath, out ScummScriptInstruction instruction, out bool handled, out int? branch, out string problem)
    {
        instruction = source;
        handled = false;
        branch = null;
        problem = $"unsupported v2 {source.Operation} ({source.Operands})";
        var p = source.Operands.Split(',', StringSplitOptions.TrimEntries);
        bool Read(int n, out ushort value) { value = 0; return n < p.Length && TryReadValue(p[n], variables, out value, out _); }
        string Value(int n, int scale = 1) => Read(n, out var value) ? $"0x{unchecked((ushort)(value * scale)):X4}" : p[n];
        ushort Global(string key) => variables.TryGetValue(key, out var value) ? value : (ushort)0;
        switch (source.Operation)
        {
            // script_v2.cpp o2_get/setObjPreposition: OBCD+12 high three bits.
            // Header access follows native room/inventory location; it is never guessed from an id.
            case "getObjPreposition":
                if (!Read(1, out var prepObject) || !TryGetVariableKey(p[0], variables, out var prepDestination) ||
                    runtime.ObjectPreposition?.Invoke(prepObject) is not int prepValue) return false;
                variables[prepDestination] = (ushort)prepValue;
                RefreshBitAliases(prepDestination, variables);
                runtime.NotifyGlobalMutation(prepDestination);
                trace.Add($"{prepDestination} = {prepValue} (native object {prepObject} preposition)");
                handled = true;
                return true;
            case "setObjPreposition":
                if (!Read(0, out var changedObject) || !Read(1, out var changedPrep) ||
                    runtime.SetObjectPreposition?.Invoke(changedObject, (byte)(changedPrep & 7)) != true) return false;
                trace.Add($"object {changedObject} preposition = {changedPrep & 7} (native header)");
                handled = true;
                return true;
            case "setObjectName":
                if (!Read(0, out var renamed)) return false;
                var quote = source.Operands.IndexOf('"');
                if (quote < 0 || !source.Operands.EndsWith('"')) return false;
                if (runtime.ObjectName?.Invoke(renamed) is not null)
                    runtime.ObjectNameOverrides[renamed] = ScummTextDecoder.ParseEscaped(source.Operands[(quote + 1)..^1]);
                handled = true;
                return true;
            case "drawSentence":
                if (runtime.IsClassicManiac && (runtime.V2UserState & 0x20) != 0 &&
                    variables.TryGetValue("var[0x01B]", out var sentenceObject) && sentenceObject > 0 &&
                    variables.TryGetValue("var[0x01D]", out var sentencePrep) && sentencePrep == 0 && variables.TryGetValue("var[0x01A]", out var sentenceVerbNumber))
                {
                    var sentenceVerb = runtime.Verbs.Snapshot().FirstOrDefault(v => v.VerbId == sentenceVerbNumber);
                    if (sentenceVerb is not null)
                    {
                        var prep = sentenceVerb.Preposition == 255 ? runtime.ObjectPreposition?.Invoke(sentenceObject) : sentenceVerb.Preposition;
                        if (prep is null) return false;
                        variables[ScummVars.V2.SentencePreposition] = (ushort)prep;
                        RefreshBitAliases(ScummVars.V2.SentencePreposition, variables);
                        runtime.NotifyGlobalMutation(ScummVars.V2.SentencePreposition);
                    }
                }
                if ((runtime.V2UserState & 0x20) != 0)
                    runtime.V2DrawnSentence = (Global(ScummVars.V2.SentenceVerb), Global(ScummVars.V2.SentenceObject1),
                        Global(ScummVars.V2.SentenceObject2), Global(ScummVars.V2.SentencePreposition));
                trace.Add("v2 native sentence line redrawn");
                handled = true;
                return true;
            case "dummy" when source.Opcode == 0xEE: handled = true; return true;
            case "pseudoRoom":
                if (!Read(0, out var realRoom)) return false;
                var aliases = new List<int>();
                foreach (var token in p.Skip(1))
                {
                    if (!TryReadValue(token.Trim('[', ']', ' '), variables, out var alias, out _)) return false;
                    if ((alias & 128) != 0) aliases.Add(alias & 127);
                }
                instruction = source with { Operands = $"{realRoom}, [{string.Join(", ", aliases)}]" };
                return true;
            case "roomOps":
                if (!Read(0, out var roomA) || !Read(1, out var roomB) || p.Length != 3) return false;
                if (p[2] == "sub=0x02")
                {
                    if (runtime.EngineVersion == SCUMMexplorer.Core.Domain.ScummVersion.V1)
                    { runtime.RoomColors[0] = 255; runtime.RoomColors[1] = (byte)roomA; runtime.RoomColors[2] = (byte)roomB; }
                    else runtime.RoomColors[roomB & 255] = (byte)roomA;
                    trace.Add($"v2 room color operation {roomA},{roomB}");
                }
                else if (p[2] == "sub=0x01" && runtime.RoomCameraBounds is { } roomBounds)
                {
                    variables[runtime.CameraMinVariable] = (ushort)Math.Clamp(roomA * 8, roomBounds.Min, roomBounds.Max);
                    variables[runtime.CameraMaxVariable] = (ushort)Math.Clamp(roomB * 8, roomBounds.Min, roomBounds.Max);
                }
                else return false;
                handled = true;
                return true;
            case "verbOps":
                if (!Read(0, out var verbNumber)) return false;
                var bracket = source.Operands.IndexOf('[');
                if (bracket < 0 || !source.Operands.EndsWith(']')) return false;
                var verbBody = source.Operands[(bracket + 1)..^1];
                if (verbBody.StartsWith("delete ") && TryReadValue(verbBody[7..], variables, out var deletedSlot, out _))
                {
                    if (deletedSlot >= 99) return false;
                    trace.Add(runtime.Verbs.DeleteV2(deletedSlot));
                }
                else if (verbBody.StartsWith("state ") && TryReadValue(verbBody[6..], variables, out var mode, out _))
                    trace.Add(runtime.Verbs.SetV2Mode(verbNumber, mode));
                else
                {
                    var fields = verbBody.Split(';', 4, StringSplitOptions.TrimEntries);
                    if (fields.Length != 4 || !fields[0].StartsWith("position ") || !fields[1].StartsWith("slot ") ||
                        !fields[2].StartsWith("preposition ") || !fields[3].StartsWith("name, \"")) return false;
                    var xy = fields[0][9..].Split(',', StringSplitOptions.TrimEntries);
                    if (xy.Length != 2 || !TryReadValue(xy[0], variables, out var vx, out _) || !TryReadValue(xy[1], variables, out var vy, out _) ||
                        !TryReadValue(fields[1][5..], variables, out var slot, out _) || !TryReadValue(fields[2][12..], variables, out var prep, out _)) return false;
                    if (slot >= 99) return false;
                    trace.Add(runtime.Verbs.CreateV2(verbNumber, slot, vx, vy, prep, fields[3][7..^1],
                        charset: runtime.Charset ?? 0, yOffset: runtime.V2VerbYOffset, estimate: estimatedPath ? "verb setup follows an estimated route" : null));
                }
                handled = true;
                return true;
            case "setCameraAt": case "panCameraTo":
                if (!Read(0, out _)) return false;
                instruction = source with { Operands = Value(0, 8) };
                return true;
            case "setVarRange":
                var listStart = source.Operands.IndexOf(", [", StringComparison.Ordinal);
                if (listStart < 0) return false;
                instruction = source with { Operands = source.Operands[..listStart] + $", count={p.Length - 1}" + source.Operands[listStart..] };
                return true;
            case "findObject":
                if (!Read(1, out var findX) || !Read(2, out var findY)) return false;
                var locals = objects.ToDictionary(o => o.LocalIndex);
                var found = 0;
                foreach (var record in objects)
                {
                    if (!states.TryGetValue(record.Id, out var objectState)) return false;
                    if ((objectState & 2) != 0) continue;
                    var at = runtime.ObjectPositions.TryGetValue(record.Id, out var moved) ? (moved.X, moved.Y) : (record.X, record.Y);
                    if (findX * 8 < at.Item1 || findX * 8 >= at.Item1 + record.Width || findY * 2 < at.Item2 || findY * 2 >= at.Item2 + record.Height) continue;
                    var current = record;
                    var matches = true;
                    for (var remaining = objects.Count; current.ParentLocalIndex != 0; remaining--)
                    {
                        if (remaining <= 0 || !locals.TryGetValue(current.ParentLocalIndex, out var parent) || !states.TryGetValue(parent.Id, out var parentState)) return false;
                        if ((parentState & 8) != current.ParentRequiredState) { matches = false; break; }
                        current = parent;
                    }
                    if (matches) { found = record.Id; break; }
                }
                instruction = source with { Operation = "move", Operands = $"{p[0]} = 0x{found:X4}" };
                return true;
            case "lights":
                if (!Read(0, out var light) || !Read(1, out var flashY) || !Read(2, out var lightMode)) return false;
                if (lightMode == 0) { variables[runtime.CurrentLightsVariable] = runtime.IsClassicManiac ? (ushort)(light == 2 ? 11 : light == 1 ? 4 : 0) : light; RefreshBitAliases(runtime.CurrentLightsVariable, variables); }
                else if (lightMode == 1) runtime.Flashlight = (light, flashY);
                handled = true;
                return true;
            case "isGreaterEqual": case "isLess": case "isGreater": case "isLessEqual":
                if (!Read(0, out var left) || !Read(1, out var rhs)) return false;
                var condition = source.Operation switch { "isGreaterEqual" => rhs >= left, "isLess" => rhs < left, "isGreater" => rhs > left, _ => rhs <= left };
                if (!condition) { if (!TryGetBranchTarget(source.Operands, out var jump)) return false; branch = jump; }
                handled = true;
                return true;
            case "move": case "assignVarByte": case "add": case "subtract":
                if (p.Length != 2) return false;
                instruction = source with { Operation = source.Operation == "assignVarByte" ? "move" : source.Operation,
                    Operands = $"{p[0]} {(source.Operation == "add" ? "+=" : source.Operation == "subtract" ? "-=" : "=")} {p[1]}" };
                return true;
            case "assignVarWordIndirect": case "addIndirect": case "subIndirect":
                if (!variables.TryGetValue(p[0], out var target) || target >= 256 || !Read(1, out var right)) return false;
                var key = $"var[0x{target:X3}]";
                instruction = source with { Operation = source.Operation == "assignVarWordIndirect" ? "move" : source.Operation == "addIndirect" ? "add" : "subtract",
                    Operands = $"{key} {(source.Operation == "assignVarWordIndirect" ? "=" : source.Operation == "addIndirect" ? "+=" : "-=")} 0x{right:X4}" };
                return true;
            case "setBitVar": case "getBitVar":
                var shift = source.Operation == "getBitVar" ? 1 : 0;
                if (!Read(shift, out var bitBase) || !Read(shift + 1, out var bitIndex)) return false;
                var bit = bitBase + bitIndex;
                var backing = $"var[0x{bit >> 4:X3}]";
                if (!variables.TryGetValue(backing, out var word)) return false;
                if (shift == 0)
                {
                    if (!Read(2, out var value)) return false;
                    variables[backing] = value != 0 ? (ushort)(word | (1 << (bit & 15))) : (ushort)(word & ~(1 << (bit & 15)));
                    RefreshBitAliases(backing, variables);
                }
                else { variables[p[0]] = (ushort)((word >> (bit & 15)) & 1); RefreshBitAliases(p[0], variables); }
                trace.Add($"{source.Operation} {source.Operands}");
                handled = true;
                return true;
            case "setState": case "ifState":
                if (!Read(0, out var obj) || !states.TryGetValue(obj, out var state) || p.Length < 2) return false;
                var (mask, on) = p[1] switch
                {
                    "IntrinsicOn" => (8, true), "IntrinsicOff" => (8, false),
                    "Locked" => (4, true), "Unlocked" => (4, false),
                    "Touchable" => (2, false), "Untouchable" => (2, true),
                    "Pickupable" => (1, true), "Unpickupable" => (1, false), _ => (0, false)
                };
                if (mask == 0) return false;
                if (source.Operation == "ifState")
                {
                    // o2_ifStateZero/NotZero encode the named state as the jump case,
                    // unlike v3's ifState equality fall-through convention.
                    if (((state & mask) != 0) == on)
                    { if (!TryGetBranchTarget(source.Operands, out var jump)) return false; branch = jump; }
                    handled = true;
                }
                else instruction = source with { Operands = $"{p[0]}, 0x{(on ? state | mask : state & ~mask):X2}" };
                return true;
            case "putActor": case "walkActorTo":
                if (p.Length != 3 || !Read(1, out _) || !Read(2, out _)) return false;
                instruction = source with { Operands = $"{p[0]}, {Value(1, 8)}, {Value(2, 2)}" };
                return true;
            case "walkActorToActor":
                // script_v5.cpp walkActorToActor: <=v2 multiplies every distance by 8,
                // including 0xFF (the actor-width sentinel belongs only to later engines).
                if (p.Length != 3 || !Read(2, out _)) return false;
                instruction = source with { Operands = $"{p[0]}, {p[1]}, {Value(2, 8)}" };
                return true;
            case "startScript": case "chainScript":
                instruction = source with { Operands = source.Operands + ", []" };
                return true;
            case "waitForMessage": instruction = source with { Operation = "waitFor", Operands = "message" }; return true;
            case "printEgo":
                instruction = source with { Operation = "print", Operands = (runtime.EgoVariable + source.Operands[3..]).Replace("[text \"", "[text, \"", StringComparison.Ordinal) };
                return true;
            case "print":
                instruction = source with { Operands = source.Operands.Replace("[text \"", "[text, \"", StringComparison.Ordinal) };
                return true;
            case "actorOps":
                if (p.Length < 3 || !Read(0, out var actor) || !Read(1, out var argument) || !p[2].StartsWith("sub=0x") ||
                    !int.TryParse(p[2].AsSpan(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var sub)) return false;
                if (actor == 0 && sub == 5) { runtime.V2DefaultTalkColor = argument; handled = true; return true; }
                var operation = sub switch { 1 => $"sound, 0x{argument:X2}", 4 => $"costume, 0x{argument:X2}", 5 => $"talkColor, 0x{argument:X2}", _ => null };
                if (sub == 2 && runtime.EngineVersion == SCUMMexplorer.Core.Domain.ScummVersion.V1)
                    operation = $"palette, 0x{actor:X2}, 0x{argument:X2}";
                if (sub == 2 && p.Length > 3 && p[3].StartsWith("palette=0x") && int.TryParse(p[3].AsSpan(10), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color))
                    operation = $"palette, 0x{color:X2}, 0x{argument:X2}";
                if (sub == 3)
                {
                    var name = source.Operands.IndexOf("name=", StringComparison.Ordinal);
                    if (name >= 0 && runtime.Actors.Contains(actor))
                    {
                        var names = ScummTextDecoder.DecodeLiterals(source.Operands[(name + 5)..]);
                        if (names.Count != 1) return false;
                        runtime.ActorNames[actor] = names[0];
                        runtime.NotifyNativeMutation("actor-name", actor);
                        handled = true;
                        return true;
                    }
                }
                if (operation is null || actor == 0) return false;
                instruction = source with { Operands = $"0x{actor:X2}, [{operation}]" };
                return true;
            case "cutscene": instruction = source with { Operands = "[]" }; return true;
            case "endCutscene": return true;
            case "cursorCommand":
                if (!Read(0, out var command)) return false;
                if ((command & 255) != 0) variables[ScummVars.V2.CursorState] = (ushort)(command & 255);
                runtime.ApplyV2UserState(command >> 8);
                handled = true;
                return true;
            case "getActorX": case "getActorY": case "getActorElevation":
                if (p.Length != 2 || !Read(1, out var queryActor) || !runtime.Actors.Contains(queryActor)) return false;
                var snapshot = runtime.Actors[queryActor];
                int? result = source.Operation switch { "getActorX" => snapshot.X / 8, "getActorY" => snapshot.Y / 2, _ => snapshot.Look.Elevation };
                if (result is null) return false;
                variables[p[0]] = unchecked((ushort)result.Value);
                RefreshBitAliases(p[0], variables);
                handled = true;
                return true;
            case "setActorElevation":
                if (!Read(0, out var raised) || !Read(1, out var elevation)) return false;
                instruction = source with { Operation = "actorOps", Operands = $"0x{raised:X2}, [elevation, 0x{unchecked((ushort)(sbyte)elevation):X4}]" };
                return true;
            case "doSentence":
                if (!Read(0, out var verb)) return false;
                if (verb == 0xFC)
                {
                    runtime.Sentences.Clear();
                    instruction = source with { Operation = "stopScript", Operands = "0x02" };
                    return true;
                }
                if (verb == 0xFB)
                {
                    if (!variables.TryGetValue("var[0x026]", out var backupVerb)) return false;
                    variables["var[0x01A]"] = backupVerb;
                    variables["var[0x01B]"] = variables["var[0x01C]"] = variables["var[0x01D]"] = 0;
                    handled = true;
                    return true;
                }
                if (p.Length != 4 || !Read(1, out var objectA) || !Read(2, out var objectB)) return false;
                if (p[3] == "sub=0x00") { instruction = source with { Operands = string.Join(", ", p.Take(3)) }; return true; }
                if (p[3] == "sub=0x01")
                {
                    if (verb is not (254 or 250 or 253))
                    {
                        variables["var[0x008]"] = verb;
                        variables["var[0x009]"] = objectA;
                        variables["var[0x00A]"] = objectB;
                    }
                    instruction = source with { Operation = "startObject", Operands = $"0x{objectA:X4}, 0x{(verb == 250 ? 253 : verb):X2}, []" };
                    return true;
                }
                if (p[3] != "sub=0x02") return false;
                variables["var[0x01A]"] = verb;
                variables["var[0x01B]"] = objectA;
                variables["var[0x01C]"] = objectB;
                  return PrepareV2Instruction(source with { Operation = "drawSentence" }, variables, states, runtime, objects,
                      trace, estimatedPath, out instruction, out handled, out branch, out problem);
            case "stopObjectCode": case "breakHere": case "jumpRelative": case "increment": case "decrement":
            case "isNotEqual": case "isEqual": case "beginOverride":
            case "equalZero": case "notEqualZero": case "delay": case "delayVariable": case "getRandomNr":
            case "startSound": case "stopSound": case "startMusic": case "stopMusic": case "stopScript": case "loadRoom": case "loadRoomWithEgo":
            case "isScriptRunning": case "isSoundRunning": case "getObjectOwner": case "setOwnerOf":
            case "putActorInRoom": case "putActorAtObject": case "walkActorToObject": case "waitForActor":
            case "getActorRoom": case "getActorCostume": case "getActorMoving": case "getActorWalkBox": case "getActorFacing":
            case "animateActor": case "faceActor": case "setBoxFlags":
            case "resourceRoutines": case "actorFollowCamera": case "drawObject": case "waitForSentence": case "pickupObject": case "getDist":
                return true;
            default: return false;
        }
    }
}

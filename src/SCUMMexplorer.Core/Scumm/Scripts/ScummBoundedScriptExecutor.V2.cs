using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummBoundedScriptExecutor
{
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
        List<string> trace, out ScummScriptInstruction instruction, out bool handled, out int? branch, out string problem)
    {
        instruction = source;
        handled = false;
        branch = null;
        problem = $"unsupported v2 {source.Operation} ({source.Operands})";
        var p = source.Operands.Split(',', StringSplitOptions.TrimEntries);
        bool Read(int n, out ushort value) { value = 0; return n < p.Length && TryReadValue(p[n], variables, out value, out _); }
        string Value(int n, int scale = 1) => Read(n, out var value) ? $"0x{unchecked((ushort)(value * scale)):X4}" : p[n];
        switch (source.Operation)
        {
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
                if (lightMode == 0) { variables[runtime.CurrentLightsVariable] = light; RefreshBitAliases(runtime.CurrentLightsVariable, variables); }
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
                if (!Read(0, out var target) || target >= 256 || !Read(1, out var right)) return false;
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
                var operation = sub switch { 1 => $"sound, {argument}", 4 => $"costume, {argument}", 5 => $"talkColor, {argument}", _ => null };
                if (sub == 2 && runtime.EngineVersion == SCUMMexplorer.Core.Domain.ScummVersion.V1)
                    operation = $"palette, {actor}, {argument}";
                if (sub == 2 && p.Length > 3 && p[3].StartsWith("palette=0x") && int.TryParse(p[3].AsSpan(10), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color))
                    operation = $"palette, {color}, {argument}";
                if (sub == 3)
                {
                    var name = source.Operands.IndexOf("name=", StringComparison.Ordinal);
                    if (name >= 0) operation = "actorName, " + source.Operands[(name + 5)..];
                }
                if (operation is null || actor == 0) return false;
                instruction = source with { Operands = $"{actor}, [{operation}]" };
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
                instruction = source with { Operation = "actorOps", Operands = $"{raised}, [elevation, 0x{unchecked((ushort)(sbyte)elevation):X4}]" };
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
                trace.Add("v2 sentence display state updated; sentence rendering is not implemented");
                handled = true;
                return true;
            case "stopObjectCode": case "breakHere": case "jumpRelative": case "increment": case "decrement":
            case "isNotEqual": case "isEqual": case "beginOverride":
            case "equalZero": case "notEqualZero": case "delay": case "delayVariable": case "getRandomNr":
            case "startSound": case "stopSound": case "startMusic": case "stopMusic": case "stopScript": case "loadRoom":
            case "isScriptRunning": case "isSoundRunning": case "getObjectOwner": case "setOwnerOf":
            case "putActorInRoom": case "putActorAtObject": case "walkActorToObject": case "waitForActor":
            case "getActorRoom": case "getActorCostume": case "getActorMoving": case "getActorWalkBox": case "getActorFacing":
            case "animateActor": case "faceActor": case "walkActorToActor": case "setBoxFlags":
            case "resourceRoutines": case "pseudoRoom": case "actorFollowCamera": case "drawObject": case "waitForSentence":
                return true;
            default: return false;
        }
    }
}

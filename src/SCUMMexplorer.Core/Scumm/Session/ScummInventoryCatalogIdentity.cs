using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Separates a scene representation from the retained item that its pickup returns.
/// Names alone never establish identity; unresolved source closures keep their candidates.</summary>
internal static class ScummInventoryCatalogIdentity
{
    internal static IReadOnlyDictionary<int, int> FindSceneAliases(
        IReadOnlyList<ScummInventoryCatalogItem> catalog,
        IReadOnlySet<int> sourceEffectTargets,
        IReadOnlyDictionary<int, ScummScriptDisassembly?> globals,
        Func<int, int, int, ScummScriptDisassembly?> objectScript,
        Func<int, int, int, bool>? knownEmptyObjectVerb = null,
        Func<int, bool>? retainedItemCode = null,
        Action<string>? note = null)
    {
        var items = catalog.ToDictionary(x => x.ObjectId);
        var result = new Dictionary<int, int>();
        foreach (var item in catalog)
        {
            if (sourceEffectTargets.Contains(item.ObjectId)) continue;
            var owners = new HashSet<int>();
            var returnsToEgo = false;
            var seen = new HashSet<string>();
            var active = new HashSet<string>();
            if (!Scan(objectScript(item.SourceRoomId, item.ObjectId, 14), item.SourceRoomId,
                    new() { [ScummVars.V2.ActiveVerb] = 14, [ScummVars.V2.ActiveObject1] = item.ObjectId })) continue;
            if (owners.Count != 1 || !returnsToEgo) continue;
            var canonical = owners.Single();
            if (canonical == item.ObjectId || !items.TryGetValue(canonical, out var retained) ||
                retainedItemCode?.Invoke(canonical) != true || string.IsNullOrWhiteSpace(item.Name) ||
                !string.Equals(item.Name, retained.Name, StringComparison.OrdinalIgnoreCase)) continue;
            result[item.ObjectId] = canonical;

            bool Fail(string reason) { note?.Invoke($"Alias candidate {item.ObjectId}: {reason}"); return false; }

            bool Scan(ScummScriptDisassembly? script, int room, Dictionary<string, int> incoming)
            {
                if (script is null || !script.HasCompleteControlFlow || script.DecodeNote is not null ||
                    script.Issues?.Count > 0) return Fail("unresolved or incomplete script");
                var label = script.Context ?? $"Script {script.ScriptId}";
                var key = label + "/" + script.EntryOffset + "/" + room + "/" + string.Join(';', incoming.OrderBy(x => x.Key));
                if (seen.Contains(key)) return true;
                if (!active.Add(key)) return Fail($"{label}: recursive call cycle");
                try
                {
                    var instructions = script.Instructions.ToDictionary(x => x.Offset);
                    var states = new HashSet<string>();
                    var queue = new Queue<(int Offset, Dictionary<string, int> Values)>();
                    queue.Enqueue((script.EntryOffset, new(incoming)));
                    while (queue.TryDequeue(out var work))
                    {
                        if (!instructions.TryGetValue(work.Offset, out var instruction)) return Fail($"{label}: missing offset {work.Offset:X4}");
                        var values = work.Values;
                        if (!states.Add(work.Offset + "/" + string.Join(';', values.OrderBy(x => x.Key)))) continue;
                        if (states.Count > 4096 || seen.Count > 256) return Fail($"{label}: analysis bound");
                        var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                        int? Read(string value)
                        {
                            if (Literal(value) is int literal) return literal;
                            if (!values.TryGetValue(value, out var n)) return null;
                            if (value is "var[0x00E]" or "var[0x00F]" or "var[0x010]")
                                return values.TryGetValue($"var[0x{n:X3}]", out var pointed) ? pointed : null;
                            return n;
                        }
                        var next = instruction.Offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                        void Enqueue(int offset) => queue.Enqueue((offset, new(values)));
                        var branch = Regex.Match(instruction.Operands, @"->\s*0x([0-9A-Fa-f]+)");
                        if (branch.Success)
                        {
                            var target = int.Parse(branch.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            if (instruction.Operation == "jumpRelative") { Enqueue(target); continue; }
                            bool? condition = null;
                            if (parts.Length >= 2 && Read(parts[0]) is int left)
                            {
                                if (instruction.Operation is "equalZero" or "notEqualZero") condition = instruction.Operation == "equalZero" ? left == 0 : left != 0;
                                else if (Read(parts[1]) is int right) condition = instruction.Operation switch
                                {
                                    "isEqual" => left == right,
                                    "isNotEqual" => left != right,
                                    "isLess" => (ushort)right < (ushort)left,
                                    "isLessEqual" => (ushort)right <= (ushort)left,
                                    "isGreater" => (ushort)right > (ushort)left,
                                    "isGreaterEqual" => (ushort)right >= (ushort)left,
                                    _ => null
                                };
                            }
                            if (condition != false) Enqueue(next);
                            if (condition != true) Enqueue(target);
                            continue;
                        }
                        if (instruction.Operation == "pickupObject" || instruction.Operation == "setState" &&
                            parts.Length > 1 && parts[1] is "Pickupable" or "Unpickupable") return Fail($"{label} +{instruction.Offset:X4}: inventory effect");
                        if (instruction.Operation is "loadRoom" or "loadRoomWithEgo" or "restart") return Fail($"{label} +{instruction.Offset:X4}: changes source context");
                        if (instruction.Operation == "setOwnerOf")
                        {
                            if (Literal(parts[0]) is not int target) return Fail($"{label} +{instruction.Offset:X4}: unknown owner target");
                            owners.Add(target);
                            if (parts.Length > 1 && parts[1] == ScummVars.V2.Ego) returnsToEgo = true;
                        }
                        if (instruction.Operation is "startScript" or "chainScript")
                        {
                            if (Literal(parts[0]) is not int target || !globals.TryGetValue(target, out var helper) ||
                                !Scan(helper, room, values)) return Fail($"{label} +{instruction.Offset:X4}: unresolved global call");
                            values.Clear(); // A called script may overwrite bindings before this caller resumes.
                        }
                        if (instruction.Operation == "doSentence")
                        {
                            if (parts.Length == 1 && Literal(parts[0]) is 251 or 252)
                            {
                                if (Literal(parts[0]) == 251)
                                    foreach (var changed in new[] { ScummVars.V2.SentenceVerb, ScummVars.V2.SentenceObject1, ScummVars.V2.SentenceObject2, ScummVars.V2.SentencePreposition }) values.Remove(changed);
                                if (Literal(parts[0]) == 252 && ReferenceEquals(script, globals.GetValueOrDefault(2))) continue;
                                Enqueue(next); continue;
                            }
                            if (parts.Length < 4 || Read(parts[0]) is not int verb || Read(parts[1]) is not int target) return Fail($"{label} +{instruction.Offset:X4}: unknown sentence {instruction.Operands}");
                            if (verb == 254 && parts[3] == "sub=0x01") { Enqueue(next); continue; } // Immediate native FE stops the target; no handler runs.
                            var context = new Dictionary<string, int>(values);
                            if (verb is not (250 or 253 or 254))
                            {
                                context[ScummVars.V2.ActiveVerb] = verb; context[ScummVars.V2.ActiveObject1] = target;
                                if (Read(parts[2]) is int other) context[ScummVars.V2.ActiveObject2] = other;
                                else context.Remove(ScummVars.V2.ActiveObject2);
                            }
                            if (parts[3] == "sub=0x00")
                            {
                                // v1/v2 queues ordinary sentences through engine-fixed Script 2.
                                // Follow its actual source/default branches instead of discarding the queued call.
                                context = new() { [ScummVars.V2.ActiveVerb] = verb, [ScummVars.V2.ActiveObject1] = target };
                                if (Read(parts[2]) is int other) context[ScummVars.V2.ActiveObject2] = other;
                                if (knownEmptyObjectVerb?.Invoke(room, target, verb) == true) context[ScummVars.V2.VerbAllowed] = 0;
                                if (!globals.TryGetValue(2, out var dispatch) || !Scan(dispatch, room, context)) return Fail($"{label} +{instruction.Offset:X4}: unresolved queued closure");
                            }
                            else if (parts[3] == "sub=0x01")
                            {
                                var requested = verb == 250 ? 253 : verb;
                                var child = objectScript(room, target, requested);
                                if (child is null ? knownEmptyObjectVerb?.Invoke(room, target, requested) != true : !Scan(child, room, context)) return Fail($"{label} +{instruction.Offset:X4}: unresolved object {target}/{requested}");
                                if (child is not null) values.Clear();
                            }
                            else return Fail($"{label} +{instruction.Offset:X4}: unknown sentence mode");
                        }
                        if (instruction.Operation is "stopObjectCode" or "chainScript" || instruction.Operation == "stopScript" &&
                            (Literal(parts[0]) == 0 || Literal(parts[0]) == script.ScriptId && ReferenceEquals(script, globals.GetValueOrDefault(script.ScriptId)))) continue;
                        if (instruction.Operation is "setVarRange" or "setBitVar" or "assignVarWordIndirect" or "addIndirect" or "subIndirect") values.Clear();
                        else if (parts[0].StartsWith("var[", StringComparison.Ordinal))
                        {
                            if (instruction.Operation == "move" && parts.Length == 2 && Read(parts[1]) is int value) values[parts[0]] = value;
                            else if (instruction.Operation is "move" or "increment" or "decrement" or "add" or "subtract" or "multiply" or "divide" or "assignVarByte" || instruction.Operation.StartsWith("get", StringComparison.Ordinal) || instruction.Operation is "isScriptRunning" or "isSoundRunning") values.Remove(parts[0]);
                        }
                        Enqueue(next);
                    }
                    seen.Add(key);
                    return true;
                }
                finally { active.Remove(key); }
            }
        }
        // A provisional scene header cannot be the retained endpoint of another
        // alias proof. Keep uncertain chains/cycles visible rather than merging them.
        var aliases = result.Keys.ToHashSet();
        foreach(var alias in result.Where(x=>aliases.Contains(x.Value)).Select(x=>x.Key).ToArray())result.Remove(alias);
        return result;
    }

    private static int? Literal(string operand) => operand.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
}

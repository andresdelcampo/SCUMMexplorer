using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A source-flow inventory census, never a running-game preview. Unknown conditions
/// explore both edges; known caller arguments prune impossible helper pickups.
/// SCUMM v1/v2 jumps on false; special doSentence handlers retain the sentence's
/// ActiveObject1 rather than replacing it with their resident handler object.
/// </summary>
internal sealed class ScummInventoryCatalogAnalysis(
    IReadOnlyDictionary<int, ScummScriptDisassembly?> globals,
    Func<int, int, int, ScummScriptDisassembly?> objectScript,
    Action<ScummScriptDisassembly, ScummScriptInstruction, int?, int?, bool> pickup,
    Action<string> note,
    IReadOnlyDictionary<int, bool?>? pickupable = null,
    Action<ScummScriptDisassembly, ScummScriptInstruction, int?, int?>? ownerWrite = null)
{
    private readonly HashSet<string> _calls = [];

    public void Scan(ScummScriptDisassembly? script, int? activeObject = null, int? room = null,
        Dictionary<string, int>? incoming = null, int depth = 0)
    {
        if (script is null) return;
        if (!MayAffectInventory(script, room, [])) return;
        var initial = incoming is null ? new Dictionary<string, int>() : new(incoming);
        if (activeObject is int id && incoming is null) initial[ScummVars.V2.ActiveObject1] = id;
        var label = script.Context ?? $"Script {script.ScriptId}";
        var key = label + "/" + room + "/" + string.Join(';', initial.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
        if (!_calls.Add(key)) return;
        if (depth > 16) { note($"Catalog call-depth bound: {label}."); return; }
        var instructions = script.Instructions.ToDictionary(x => x.Offset);
        var inventoryOffsets = InventoryOffsets(script, room);
        var visited = new HashSet<string>();
        var queue = new Queue<(int Offset, Dictionary<string, int> Values)>();
        queue.Enqueue((script.EntryOffset, initial));
        void Enqueue(int offset, Dictionary<string, int> values) => queue.Enqueue((offset, new(values)));
        while (queue.TryDequeue(out var work))
        {
            if (!inventoryOffsets.Contains(work.Offset) || !instructions.TryGetValue(work.Offset, out var instruction)) continue;
            var values = work.Values;
            var state = work.Offset + "/" + string.Join(';', values.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
            if (!visited.Add(state)) continue;
            // Keep disjoint finite targets at joins. Intersecting bindings here
            // would erase one branch's item before a shared pickup instruction.
            if (visited.Count > 16384) { note($"Catalog path-state bound: {label} +0x{work.Offset:X4}; {string.Join(';', values.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}"))}."); break; }
            int? Read(string value) => ReadValue(value, values);
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            var next = instruction.Offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var operation = instruction.Operation;
            var target = BranchTarget(instruction.Operands);
            bool? condition = null;
            var conditional = target.HasValue && operation != "jumpRelative";
            if (conditional && parts.Length >= 2 && Read(parts[0]) is int left)
            {
                if (operation is "equalZero" or "notEqualZero") condition = operation == "equalZero" ? left == 0 : left != 0;
                else if (Read(parts[1]) is int right) condition = operation switch
                {
                    "isEqual" => left == right, "isNotEqual" => left != right,
                    "isLess" => (ushort)right < (ushort)left, "isLessEqual" => (ushort)right <= (ushort)left,
                    "isGreater" => (ushort)right > (ushort)left, "isGreaterEqual" => (ushort)right >= (ushort)left, _ => null
                };
                else if (operation == "ifState" && parts[1] is "Pickupable" or "Unpickupable" &&
                    pickupable?.TryGetValue(left, out var possible) == true && possible is bool flag)
                    // Unlike numeric comparisons, o2_ifState* names the jump case.
                    condition = parts[1] == "Pickupable" ? !flag : flag;
            }
            if (operation == "jumpRelative") { if (target is int jump) Enqueue(jump, values); continue; }
            if (conditional)
            {
                if (condition != false) Enqueue(next, values);
                if (condition != true) Enqueue(target!.Value, values);
                continue;
            }
            if (operation == "pickupObject" || operation == "setState" && parts.Length > 1 && parts[1] is "Pickupable" or "Unpickupable")
                pickup(script, instruction, Read(parts[0]), room, operation == "setState");
            if (operation == "setOwnerOf" && parts.Length == 2)
                ownerWrite?.Invoke(script, instruction, Read(parts[0]), Read(parts[1]));
            if (operation is "startScript" or "chainScript" && Read(parts[0]) is int called && globals.TryGetValue(called, out var helper))
            {
                Scan(helper, values.GetValueOrDefault(ScummVars.V2.ActiveObject1) is > 0 and var active ? active : null, room, values, depth + 1);
                InvalidateWrites(helper, room, values, []);
            }
            if (operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01" &&
                Read(parts[0]) is int verb && verb != 254 && Read(parts[1]) is int obj && room is int sourceRoom)
            {
                var context = new Dictionary<string, int>(values);
                if (verb is not (250 or 253 or 254))
                {
                    context[ScummVars.V2.ActiveVerb] = verb;
                    context[ScummVars.V2.ActiveObject1] = obj;
                    if (Read(parts[2]) is int other) context[ScummVars.V2.ActiveObject2] = other;
                }
                var handler = objectScript(sourceRoom, obj, verb == 250 ? 253 : verb);
                Scan(handler,
                    context.GetValueOrDefault(ScummVars.V2.ActiveObject1) is > 0 and var active ? active : null,
                    room, context, depth + 1);
                values = context;
                InvalidateWrites(handler, room, values, []);
            }
            if (operation is "stopObjectCode" or "chainScript" || operation == "stopScript" && Literal(parts[0]) == 0) continue;
            if (operation is "assignVarWordIndirect" or "addIndirect" or "subIndirect")
            {
                if (values.TryGetValue(parts[0], out var index)) values.Remove($"var[0x{index:X3}]");
                else values.Clear();
            }
            else if (operation == "setBitVar") InvalidateBitWord(parts, values);
            else if (operation == "setVarRange")
                InvalidateRange(parts, values);
            else if (parts.Length > 0 && parts[0].StartsWith("var[", StringComparison.Ordinal))
            {
                var destination = parts[0];
                int? current = values.TryGetValue(destination, out var raw) ? raw : null;
                int? result = operation switch
                {
                    "move" when parts.Length == 2 => Read(parts[1]),
                    "increment" when current.HasValue => (ushort)(current.Value + 1),
                    "decrement" when current.HasValue => (ushort)(current.Value - 1),
                    "add" when current.HasValue && parts.Length == 2 && Read(parts[1]) is int operand => (ushort)(current.Value + operand),
                    "subtract" when current.HasValue && parts.Length == 2 && Read(parts[1]) is int operand => (ushort)(current.Value - operand),
                    _ => null
                };
                // Query/random/indirect writes invalidate their result. Operations whose
                // first operand is merely read must preserve the caller's known binding.
                if (operation is "move" or "increment" or "decrement" or "add" or "subtract" or "multiply" or "divide" or
                    "assignVarByte" or "assignVarWordIndirect" or "getBitVar" or "getRandomNr" or "findObject" or "actorFromPos" ||
                    operation.StartsWith("get", StringComparison.Ordinal) || operation is "isScriptRunning" or "isSoundRunning")
                {
                    if (result is int known) values[destination] = known; else values.Remove(destination);
                }
            }
            Enqueue(next, values);
        }
    }

    private void InvalidateWrites(ScummScriptDisassembly? script, int? room, Dictionary<string, int> values, HashSet<string> visited)
    {
        if (script is null || !visited.Add((script.Context ?? $"Script {script.ScriptId}") + "/" + script.EntryOffset)) return;
        foreach (var instruction in script.Instructions)
        {
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            if (instruction.Operation is "assignVarWordIndirect" or "addIndirect" or "subIndirect")
            {
                if (values.TryGetValue(parts[0], out var index)) values.Remove($"var[0x{index:X3}]");
                else { values.Clear(); return; }
            }
            else if (instruction.Operation == "setVarRange") InvalidateRange(parts, values);
            else if (instruction.Operation == "setBitVar") InvalidateBitWord(parts, values);
            else if (instruction.Operation is "move" or "increment" or "decrement" or "add" or "subtract" or "multiply" or "divide" or "assignVarByte" ||
                instruction.Operation.StartsWith("get", StringComparison.Ordinal) || instruction.Operation is "isScriptRunning" or "isSoundRunning")
                values.Remove(parts[0]);
            if (instruction.Operation is "startScript" or "chainScript" && Literal(parts[0]) is int called && globals.TryGetValue(called, out var child))
                InvalidateWrites(child, room, values, visited);
            if (instruction.Operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01")
            {
                if (Literal(parts[0]) == 254) continue;
                if (Literal(parts[0]) is not int verb || Literal(parts[1]) is not int obj || room is not int sourceRoom)
                { values.Clear(); return; }
                if (verb is not (250 or 253 or 254))
                {
                    values.Remove(ScummVars.V2.ActiveVerb);
                    values.Remove(ScummVars.V2.ActiveObject1);
                    values.Remove(ScummVars.V2.ActiveObject2);
                }
                InvalidateWrites(objectScript(sourceRoom, obj, verb == 250 ? 253 : verb), room, values, visited);
            }
        }
    }

    private bool MayAffectInventory(ScummScriptDisassembly? script, int? room, HashSet<string> visited)
    {
        if (script is null || !visited.Add((script.Context ?? $"Script {script.ScriptId}") + "/" + script.EntryOffset)) return false;
        foreach (var instruction in script.Instructions)
        {
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            if (instruction.Operation == "pickupObject" || ownerWrite is not null && instruction.Operation == "setOwnerOf" ||
                instruction.Operation == "setState" && parts.Length > 1 && parts[1] is "Pickupable" or "Unpickupable") return true;
            if (instruction.Operation is "startScript" or "chainScript")
            {
                if (Literal(parts[0]) is not int id) return true;
                if (globals.TryGetValue(id, out var child) && MayAffectInventory(child, room, visited)) return true;
            }
            if (instruction.Operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01")
            {
                if (Literal(parts[0]) == 254) continue; // stopObjectScript, not a handler invocation.
                if (Literal(parts[0]) is not int verb || Literal(parts[1]) is not int obj) return true;
                if (MayAffectInventory(objectScript(room ?? -1, obj, verb == 250 ? 253 : verb), room, visited)) return true;
            }
        }
        return false;
    }

    private HashSet<int> InventoryOffsets(ScummScriptDisassembly script, int? room)
    {
        var backwards = new Dictionary<int, List<int>>();
        var targets = new HashSet<int>();
        foreach (var instruction in script.Instructions)
        {
            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            var operation = instruction.Operation;
            var effect = operation == "pickupObject" || ownerWrite is not null && operation == "setOwnerOf" ||
                operation == "setState" && parts.Length > 1 && parts[1] is "Pickupable" or "Unpickupable";
            if (operation is "startScript" or "chainScript")
                effect |= Literal(parts[0]) is not int id || globals.TryGetValue(id, out var child) && MayAffectInventory(child, room, []);
            if (operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01" && Literal(parts[0]) != 254)
                effect |= Literal(parts[0]) is not int verb || Literal(parts[1]) is not int obj ||
                    MayAffectInventory(objectScript(room ?? -1, obj, verb == 250 ? 253 : verb), room, []);
            if (effect) targets.Add(instruction.Offset);
            void Edge(int offset)
            {
                if (!backwards.TryGetValue(offset, out var incoming)) backwards[offset] = incoming = [];
                incoming.Add(instruction.Offset);
            }
            if (BranchTarget(instruction.Operands) is int target) Edge(target);
            if (operation is not ("stopObjectCode" or "chainScript" or "jumpRelative") && !(operation == "stopScript" && Literal(parts[0]) == 0))
                Edge(instruction.Offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        }
        var queue = new Queue<int>(targets);
        while (queue.TryDequeue(out var offset))
            if (backwards.TryGetValue(offset, out var incoming))
                foreach (var before in incoming) if (targets.Add(before)) queue.Enqueue(before);
        return targets;
    }

    private static int? ReadValue(string operand, Dictionary<string, int> values)
    {
        if (Literal(operand) is int literal) return literal;
        if (!values.TryGetValue(operand, out var value)) return null;
        // Native v1/v2 readVar aliases words 14..16; result destinations remain raw.
        return operand is "var[0x00E]" or "var[0x00F]" or "var[0x010]"
            ? value < 256 && values.TryGetValue($"var[0x{value:X3}]", out var pointed) ? pointed : null
            : value;
    }

    private static void InvalidateBitWord(string[] parts, Dictionary<string, int> values)
    {
        if (parts.Length >= 3 && ReadValue(parts[0], values) is int start && ReadValue(parts[1], values) is int bit)
            values.Remove($"var[0x{(start + bit) >> 4:X3}]");
        else values.Clear();
    }

    private static void InvalidateRange(string[] parts, Dictionary<string, int> values)
    {
        if (parts[0].StartsWith("var[0x", StringComparison.Ordinal) &&
            int.TryParse(parts[0].AsSpan(6, parts[0].Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index))
            for (var n = 0; n < parts.Length - 1; n++) values.Remove($"var[0x{index + n:X3}]");
        else values.Clear();
    }

    private static int? Literal(string value) => value.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static int? BranchTarget(string operands)
    {
        var arrow = operands.LastIndexOf("->", StringComparison.Ordinal);
        return arrow < 0 ? null : Literal(operands[(arrow + 2)..].Trim());
    }
}

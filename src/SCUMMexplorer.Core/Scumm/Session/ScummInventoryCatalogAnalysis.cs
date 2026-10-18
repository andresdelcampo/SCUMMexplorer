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
    IReadOnlyDictionary<int, bool?>? pickupable = null)
{
    private readonly HashSet<string> _calls = [];

    public void Scan(ScummScriptDisassembly? script, int? activeObject = null, int? room = null,
        Dictionary<string, int>? incoming = null, int depth = 0)
    {
        if (script is null) return;
        var initial = incoming is null ? new Dictionary<string, int>() : new(incoming);
        if (activeObject is int id && incoming is null) initial[ScummVars.V2.ActiveObject1] = id;
        var label = script.Context ?? $"Script {script.ScriptId}";
        var key = label + "/" + room + "/" + string.Join(';', initial.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
        if (!_calls.Add(key)) return;
        if (depth > 16) { note($"Catalog call-depth bound: {label}."); return; }
        var instructions = script.Instructions.ToDictionary(x => x.Offset);
        var merged = new Dictionary<int, Dictionary<string, int>>();
        var queue = new Queue<(int Offset, Dictionary<string, int> Values)>();
        queue.Enqueue((script.EntryOffset, initial));
        void Enqueue(int offset, Dictionary<string, int> values) => queue.Enqueue((offset, new(values)));
        while (queue.TryDequeue(out var work))
        {
            if (!instructions.TryGetValue(work.Offset, out var instruction)) continue;
            var values = work.Values;
            if (merged.TryGetValue(work.Offset, out var previous))
            {
                var common = previous.Where(x => values.TryGetValue(x.Key, out var v) && x.Value == v).ToDictionary();
                if (common.Count == previous.Count) continue;
                values = common;
            }
            merged[work.Offset] = new(values);
            int? Read(string value) => Literal(value) ?? (values.TryGetValue(value, out var v) ? v : null);
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
                    "isLess" => (short)left < (short)right, "isLessEqual" => (short)left <= (short)right,
                    "isGreater" => (short)left > (short)right, "isGreaterEqual" => (short)left >= (short)right, _ => null
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
            if (operation is "startScript" or "chainScript" && Read(parts[0]) is int called && globals.TryGetValue(called, out var helper))
                Scan(helper, values.GetValueOrDefault(ScummVars.V2.ActiveObject1) is > 0 and var active ? active : null, room, values, depth + 1);
            if (operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01" &&
                Read(parts[0]) is int verb && Read(parts[1]) is int obj && room is int sourceRoom)
            {
                var context = new Dictionary<string, int>(values);
                if (verb is not (250 or 253 or 254))
                {
                    context[ScummVars.V2.ActiveVerb] = verb;
                    context[ScummVars.V2.ActiveObject1] = obj;
                    if (Read(parts[2]) is int other) context[ScummVars.V2.ActiveObject2] = other;
                }
                Scan(objectScript(sourceRoom, obj, verb == 250 ? 253 : verb),
                    context.GetValueOrDefault(ScummVars.V2.ActiveObject1) is > 0 and var active ? active : null,
                    room, context, depth + 1);
            }
            if (operation is "stopObjectCode" or "chainScript" || operation == "stopScript" && Literal(parts[0]) == 0) continue;
            if (parts.Length > 0 && parts[0].StartsWith("var[", StringComparison.Ordinal))
            {
                var destination = parts[0];
                var current = Read(destination);
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

    private static int? Literal(string value) => value.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static int? BranchTarget(string operands)
    {
        var arrow = operands.LastIndexOf("->", StringComparison.Ordinal);
        return arrow < 0 ? null : Literal(operands[(arrow + 2)..].Trim());
    }
}

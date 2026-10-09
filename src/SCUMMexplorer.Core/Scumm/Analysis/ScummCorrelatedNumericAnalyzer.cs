using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A number or an opaque value in a hypothetical source world, with its dependencies.</summary>
public sealed record ScummNumericValue(ushort? Number, string? Symbol, IReadOnlyList<string> Sources)
{
    public static ScummNumericValue Hypothesis(ushort number, string source) => new(number, null, [source]);
    public static ScummNumericValue Opaque(string symbol, string source) => new(null, symbol, [source]);
}

/// <summary>
/// One complete correlated assignment. Assumptions describe its bounds; this is never session state.
/// Keep related words, indexes and input tuples together instead of taking component products.
/// </summary>
public sealed record ScummNumericWorld(string Id, string Assumptions,
    IReadOnlyDictionary<string, ScummNumericValue> Globals, IReadOnlyList<ScummNumericValue>? Arguments = null);

public sealed record ScummNumericSite(int Script, string? Context, int Container, int ResourceOffset, int Offset);
public sealed record ScummNumericWrite(ScummNumericSite Site, string Key, ScummNumericValue Value);
public sealed record ScummNumericBranch(ScummNumericSite Site, bool Condition, IReadOnlyDictionary<string, ScummNumericValue> Locals);
public sealed record ScummNumericBoundary(ScummNumericSite Site, string Operation, ushort? Target,
    IReadOnlyList<ScummNumericValue> Arguments);
public sealed record ScummNumericWorldResult(string Id, string Assumptions,
    IReadOnlyDictionary<string, ScummNumericValue> Globals, IReadOnlyDictionary<string, ScummNumericValue> Locals,
    IReadOnlyList<ScummNumericWrite> Writes, IReadOnlyList<ScummNumericBranch> Branches,
    ScummNumericBoundary? Boundary, bool ReachedStop, string? UnresolvedReason, int InstructionsVisited);

/// <summary>
/// Bounded, source-only numeric interpretation. Every world retains its own globals, locals,
/// arguments and branch history. Complete numeric startScript callees may return; chainScript,
/// yields, effects, missing values and unsupported operations are boundaries. There is no
/// scheduler, cancellation, selected-input, movement or effect-certainty claim, and no runtime
/// state/estimate is read or written. Source-world conclusions must keep their assumptions.
/// </summary>
public sealed class ScummCorrelatedNumericAnalyzer
{
    private static readonly Regex Variable = new(@"^(var|local|bit)\[0x([0-9A-Fa-f]+)(?:\+(.+))?\]$", RegexOptions.Compiled);
    private static readonly Regex Target = new(@"->\s*0x([0-9A-Fa-f]+)$", RegexOptions.Compiled);

    public IReadOnlyList<ScummNumericWorldResult> Analyze(ScummScriptDisassembly script,
        IReadOnlyList<ScummNumericWorld> worlds, Func<int, ScummScriptDisassembly?>? resolveScript = null,
        int startOffset = 0, int instructionLimit = 10_000, int callLimit = 16)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(worlds);
        if (worlds.Count is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(worlds));
        if (worlds.Select(w => w.Id).Distinct(StringComparer.Ordinal).Count() != worlds.Count)
            throw new ArgumentException("World identities must be distinct.", nameof(worlds));
        if (instructionLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(instructionLimit));
        if (callLimit is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(callLimit));
        return worlds.Select(world => new Interpreter(world, resolveScript, instructionLimit, callLimit)
            .Run(script, startOffset)).ToArray();
    }

    private sealed class Interpreter(ScummNumericWorld world, Func<int, ScummScriptDisassembly?>? resolveScript,
        int instructionLimit, int callLimit)
    {
        private readonly Dictionary<string, ScummNumericValue> globals = world.Globals.ToDictionary(
            p => p.Key, p => Copy(p.Value), StringComparer.Ordinal);
        private readonly List<ScummNumericWrite> writes = [];
        private readonly List<ScummNumericBranch> branches = [];
        private int visited;

        public ScummNumericWorldResult Run(ScummScriptDisassembly script, int start)
        {
            var locals = NewLocals(world.Arguments ?? []);
            var (stop, boundary, reason) = Execute(script, start, locals, 0);
            return new(world.Id, world.Assumptions, new Dictionary<string, ScummNumericValue>(globals),
                locals, writes.ToArray(), branches.ToArray(), boundary, stop, reason, visited);
        }

        private (bool Stop, ScummNumericBoundary? Boundary, string? Reason) Execute(ScummScriptDisassembly script,
            int start, Dictionary<string, ScummNumericValue> locals, int depth)
        {
            if (depth >= callLimit) return (false, null, "numeric call limit reached");
            var instructions = script.Instructions.ToDictionary(i => i.Offset);
            var pc = start;
            while (instructions.TryGetValue(pc, out var instruction))
            {
                var site = new ScummNumericSite(script.ScriptId, script.Context, script.ContainerFileNumber, script.ResourceOffset, pc);
                var source = $"{script.Context ?? $"Script {script.ScriptId:00}"} ({script.ContainerFileNumber:00}.LFL +0x{script.ResourceOffset:X4}) +0x{pc:X4}";
                if (++visited > instructionLimit) return (false, null, $"numeric instruction limit at {source}");
                var next = pc + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (next == pc) return (false, null, $"missing encoded instruction length at {source}");
                var op = instruction.Operation;
                var arg = instruction.Operands;
                try
                {
                    if (op == "stopObjectCode") return (true, null, null);
                    if (op == "jumpRelative") { pc = BranchTarget(arg); continue; }
                    if (op is "equalZero" or "notEqualZero" or "isEqual" or "isNotEqual" or
                        "isLess" or "isLessEqual" or "isGreater" or "isGreaterEqual")
                    {
                        var parts = arg.Split(", branch ", StringSplitOptions.None)[0].Split(',', StringSplitOptions.TrimEntries);
                        var a = Number(Read(parts[0], locals));
                        var b = parts.Length > 1 ? Number(Read(parts[1], locals)) : (ushort)0;
                        // Old-v3 evaluates signed b OP a, and jumps on false.
                        var condition = op switch
                        {
                            "equalZero" => a == 0, "notEqualZero" => a != 0,
                            "isEqual" => a == b, "isNotEqual" => a != b,
                            "isLess" => (short)b < (short)a, "isLessEqual" => (short)b <= (short)a,
                            "isGreater" => (short)b > (short)a, _ => (short)b >= (short)a
                        };
                        branches.Add(new(site, condition, new Dictionary<string, ScummNumericValue>(locals)));
                        pc = condition ? next : BranchTarget(arg);
                        continue;
                    }
                    if (op is "startScript" or "chainScript")
                    {
                        var marker = arg.IndexOf(", [", StringComparison.Ordinal);
                        var open = marker < 0 ? -1 : marker + 2;
                        if (open < 0 || !arg.EndsWith(']')) throw new NumericFailure("malformed call arguments");
                        var head = arg[..open].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        // Freeze/recursive flags need scheduler semantics, outside this subprogram.
                        if (head.Length != 1) throw new NumericFailure("call flags need scheduler semantics");
                        var target = Number(Read(head[0], locals));
                        var args = arg[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => Derived(Read(a, locals), source)).ToArray();
                        var boundary = new ScummNumericBoundary(site, op, target, args);
                        if (op == "chainScript") return (false, boundary, null);
                        var callee = resolveScript?.Invoke(target);
                        if (callee is null) return (false, boundary, $"unresolved numeric callee {target} at {source}");
                        var child = Execute(callee, 0, NewLocals(args), depth + 1);
                        if (!child.Stop) return (false, child.Boundary ?? boundary, child.Reason ?? $"callee did not return at {source}");
                    }
                    else if (op == "stopScript")
                    {
                        var target = Number(Read(arg, locals));
                        // Self-stop is a source terminal, not a completion/cancellation summary.
                        if (target == 0) return (true, new(site, op, target, []), null);
                        return (false, new(site, op, target, []), $"numbered stop needs scheduler semantics at {source}");
                    }
                    else if (op is "move" or "add" or "subtract" or "multiply" or "divide" or "and" or "or")
                    {
                        var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3) throw new NumericFailure("malformed arithmetic");
                        if (Variable.Match(Key(parts[0], locals)).Groups[1].Value == "bit" && op != "move")
                            throw new NumericFailure("unsupported bit arithmetic");
                        var right = Read(parts[2], locals);
                        var result = right;
                        if (op != "move")
                        {
                            var left = Read(parts[0], locals);
                            var a = Number(left); var b = Number(right);
                            var number = op switch
                            {
                                "add" => unchecked((ushort)(a + b)), "subtract" => unchecked((ushort)(a - b)),
                                "multiply" => unchecked((ushort)(a * b)), "divide" when b != 0 => (ushort)(a / b),
                                "and" => (ushort)(a & b), "or" => (ushort)(a | b),
                                _ => throw new NumericFailure("numeric division by zero")
                            };
                            result = new(number, null, left.Sources.Concat(right.Sources).Distinct().ToArray());
                        }
                        Write(parts[0], Derived(result, source), locals, site);
                    }
                    else if (op is "increment" or "decrement")
                    {
                        var old = Read(arg, locals);
                        Write(arg, Derived(new(unchecked((ushort)(Number(old) + (op == "increment" ? 1 : -1))), null, old.Sources), source), locals, site);
                    }
                    else if (op == "setVarRange")
                    {
                        var range = Regex.Match(arg, @"^(.+), count=(\d+), \[(.*)\]$");
                        if (!range.Success) throw new NumericFailure("malformed variable range");
                        var key = Key(range.Groups[1].Value, locals);
                        var match = Variable.Match(key);
                        var address = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        var values = range.Groups[3].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        var count = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                        if (values.Length != count) throw new NumericFailure("malformed variable range");
                        for (var i = 0; i < values.Length; i++)
                            Write($"{match.Groups[1].Value}[0x{address + i:X3}]", Derived(Read(values[i], locals), source), locals, site);
                    }
                    else return (false, new(site, op, null, []), $"{op} needs effects or unsupported numeric semantics at {source}");
                    pc = next;
                }
                catch (NumericFailure failure) { return (false, null, $"{failure.Message} at {source}"); }
                catch (Exception failure) when (failure is FormatException or OverflowException or ArgumentOutOfRangeException)
                { return (false, null, $"malformed numeric operands: {failure.Message} at {source}"); }
            }
            return (false, null, $"no decoded instruction at +0x{pc:X4}");
        }

        private string Key(string operand, Dictionary<string, ScummNumericValue> locals)
        {
            var match = Variable.Match(operand.Trim());
            if (!match.Success) throw new NumericFailure($"unresolved variable {operand}");
            var address = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (match.Groups[3].Success) address += Number(Read(match.Groups[3].Value, locals)) & 0xFFF;
            return $"{match.Groups[1].Value}[0x{address:X3}]";
        }

        private ScummNumericValue Read(string operand, Dictionary<string, ScummNumericValue> locals)
        {
            operand = operand.Trim();
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
                return new(literal, null, []);
            var key = Key(operand, locals);
            var variables = key.StartsWith("local[", StringComparison.Ordinal) ? locals : globals;
            ScummNumericValue value;
            if (!variables.TryGetValue(key, out value!))
            {
                var match = Variable.Match(key);
                var address = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (match.Groups[1].Value != "bit" || !globals.TryGetValue($"var[0x{address >> 4:X3}]", out var backing))
                    throw new NumericFailure($"unknown external value {key}");
                value = new((ushort)((Number(backing) >> (address & 15)) & 1), null, backing.Sources);
            }
            var indexed = Variable.Match(operand);
            return indexed.Groups[3].Success
                ? value with { Sources = value.Sources.Concat(Read(indexed.Groups[3].Value, locals).Sources).Distinct().ToArray() }
                : value;
        }

        private void Write(string operand, ScummNumericValue value, Dictionary<string, ScummNumericValue> locals, ScummNumericSite site)
        {
            var key = Key(operand, locals);
            var indexed = Variable.Match(operand.Trim());
            if (indexed.Groups[3].Success)
                value = value with { Sources = value.Sources.Concat(Read(indexed.Groups[3].Value, locals).Sources).Distinct().ToArray() };
            var match = Variable.Match(key);
            var address = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (match.Groups[1].Value == "bit")
            {
                value = value with { Number = (ushort)(Number(value) == 0 ? 0 : 1) };
                var backingKey = $"var[0x{address >> 4:X3}]";
                if (globals.TryGetValue(backingKey, out var backing))
                {
                    if (backing.Number is ushort number)
                        globals[backingKey] = new((ushort)(value.Number == 0 ? number & ~(1 << (address & 15)) : number | (1 << (address & 15))),
                            null, backing.Sources.Concat(value.Sources).Distinct().ToArray());
                    else globals.Remove(backingKey);
                }
            }
            else if (match.Groups[1].Value == "var")
                for (var bit = 0; bit < 16; bit++) globals.Remove($"bit[0x{address * 16 + bit:X3}]");
            (key.StartsWith("local[", StringComparison.Ordinal) ? locals : globals)[key] = value;
            writes.Add(new(site, key, value));
        }

        private static Dictionary<string, ScummNumericValue> NewLocals(IReadOnlyList<ScummNumericValue> arguments)
        {
            if (arguments.Count > 26) throw new ArgumentException("Too many numeric arguments.", nameof(arguments));
            return Enumerable.Range(0, 26).ToDictionary(i => $"local[0x{i:X3}]",
                i => i < arguments.Count ? Copy(arguments[i]) : new ScummNumericValue(0, null, []), StringComparer.Ordinal);
        }
        private static ScummNumericValue Copy(ScummNumericValue value) => value with { Sources = value.Sources.ToArray() };
        private static ScummNumericValue Derived(ScummNumericValue value, string source) =>
            value with { Sources = value.Sources.Append(source).Distinct().ToArray() };
        private static ushort Number(ScummNumericValue value) => value.Number ?? throw new NumericFailure($"opaque numeric operand {value.Symbol}");
        private static int BranchTarget(string operand)
        {
            var match = Target.Match(operand);
            return match.Success ? int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : throw new NumericFailure("unresolved branch target");
        }
    }

    private sealed class NumericFailure(string message) : Exception(message);
}

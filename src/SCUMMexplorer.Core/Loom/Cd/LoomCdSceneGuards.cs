using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// One test the code makes on the way to a cutscene. <see cref="MustHold"/> is true when the cutscene sits on the branch taken while the
/// test holds, false when it sits on the other one. <see cref="Subject"/> is the first operand (a story bit, a variable or a local);
/// <see cref="Origin"/> says what last wrote a local before the test (an actor's walk box, a random pick, a copy).
/// </summary>
public sealed record LoomCdGuard(string Script, int Offset, string Operation, string Operands, bool MustHold, string Subject, string Origin, int? WalkBoxOfActor = null)
{
    /// <summary>The constant a <c>isEqual</c> test compares its subject with, when the second operand is a literal.</summary>
    public int? Constant => Operation == "isEqual" && Operands.Split(",", StringSplitOptions.TrimEntries) is [_, var second, ..] && second.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(second.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    public override string ToString() => $"{Script} +0x{Offset:X4} {Operation} {Operands} must {(MustHold ? "hold" : "fail")}{(Origin.Length > 0 ? $" [{Subject}: {Origin}]" : "")}";
}

public enum LoomCdGuardVerdict { Satisfied, Unsatisfied, Unknown }

/// <summary>A way into one cutscene span: the scripts from the trigger to it and every test the code makes on the way (call sites included).</summary>
public sealed record LoomCdScenePath(IReadOnlyList<string> Chain, int SpanOffset, IReadOnlyList<LoomCdGuard> Guards);

/// <summary>
/// Reads, from the disassembly alone, what the code tests between a trigger and each <c>cutscene</c> instruction it reaches: for a conditional
/// jump <c>op ..., branch -> target</c> (which jumps when the test is false), the cutscene is guarded by it when it lies between the jump and
/// its target, and by its negation when it lies in the else part (after a <c>jumpRelative</c> that ends the then part). A backward jump is a wait
/// loop, not a guard. Nothing here is run, so the result says what a branch asks, not that the game can get there.
/// </summary>
public static partial class LoomCdSceneGuards
{
    private static readonly HashSet<string> Conditionals =
    [
        "equalZero", "notEqualZero", "isEqual", "isNotEqual", "isGreater", "isGreaterEqual", "isLess", "isLessEqual", "ifClassOfIs", "ifState", "isScriptRunning"
    ];

    [GeneratedRegex(@"-> 0x([0-9A-Fa-f]+)\s*$")]
    private static partial Regex Target();

    [GeneratedRegex(@"^(?:0x)?([0-9A-Fa-f]+),")]
    private static partial Regex LiteralScriptNumber();

    [GeneratedRegex(@"^(bit|var|local)\[0x([0-9A-Fa-f]+)\]")]
    private static partial Regex FirstVariable();

    public static IReadOnlyList<LoomCdScenePath> Find(IScummDataSource data, int room, ScummScriptDisassembly root, string rootLabel, int depth = LoomCutsceneIndex.Depth)
    {
        var paths = new List<LoomCdScenePath>();
        Walk(data, room, root, rootLabel, [rootLabel], [], depth, [], paths);
        return paths;
    }

    private static int? TargetOf(ScummScriptInstruction i) =>
        Target().Match(i.Operands) is { Success: true } m ? int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : null;

    /// <summary>The guards of the code at <paramref name="offset"/> in <paramref name="script"/>, outermost first.</summary>
    public static IReadOnlyList<LoomCdGuard> GuardsAt(ScummScriptDisassembly script, string label, int offset)
    {
        var ins = script.Instructions;
        var guards = new List<LoomCdGuard>();
        foreach (var i in ins)
        {
            if (i.Offset >= offset) break;
            if (!Conditionals.Contains(i.Operation) || TargetOf(i) is not int target || target <= i.Offset) continue;
            if (offset < target)
            {
                guards.Add(Make(script, label, i, true));
                continue;
            }
            // The else part: the instruction just before the target is the jump that ends the then part.
            var before = ins.LastOrDefault(x => x.Offset < target);
            if (before is { Operation: "jumpRelative" } && TargetOf(before) is int end && end > target && offset < end)
                guards.Add(Make(script, label, i, false));
        }
        return guards;
    }

    /// <summary>Whether the session's state satisfies the guard now: the test is read as ScummVM reads it (the code falls through when the test is true).</summary>
    public static LoomCdGuardVerdict Evaluate(LoomCdGuard guard, Func<string, ushort?> variable, Func<int, uint?> classesOf)
    {
        var text = guard.Operands;
        var cut = text.IndexOf(", branch ", StringComparison.Ordinal);
        if (cut >= 0) text = text[..cut];
        // ifClassOfIs object, [classes]: every listed class must agree (bit 7 set = the object has it, clear = it does not).
        if (guard.Operation == "ifClassOfIs")
        {
            var open = text.IndexOf('[');
            if (open < 0 || !TryValue(text[..open].TrimEnd(' ', ','), variable, out var obj) || classesOf((int)obj) is not uint bits) return LoomCdGuardVerdict.Unknown;
            var holds = text[(open + 1)..].TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => int.Parse(item.AsSpan(item.StartsWith("0x", StringComparison.Ordinal) ? 2 : 0), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .All(cls => (cls & 0x80) != 0 == ScummObjectClasses.Has(bits, cls & 0x7F));
            return holds == guard.MustHold ? LoomCdGuardVerdict.Satisfied : LoomCdGuardVerdict.Unsatisfied;
        }
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (!TryValue(parts[0], variable, out var a)) return LoomCdGuardVerdict.Unknown;
        bool result;
        switch (guard.Operation)
        {
            case "equalZero": result = a == 0; break;
            case "notEqualZero": result = a != 0; break;
            default:
                if (parts.Length < 2 || !TryValue(parts[1], variable, out var b)) return LoomCdGuardVerdict.Unknown;
                result = guard.Operation switch
                {
                    "isEqual" => a == b,
                    "isNotEqual" => a != b,
                    "isGreater" => b > a,
                    "isGreaterEqual" => b >= a,
                    "isLess" => b < a,
                    "isLessEqual" => b <= a,
                    _ => throw new InvalidOperationException(guard.Operation)
                };
                break;
        }
        return result == guard.MustHold ? LoomCdGuardVerdict.Satisfied : LoomCdGuardVerdict.Unsatisfied;
    }

    private static bool TryValue(string operand, Func<string, ushort?> variable, out int value)
    {
        value = 0;
        if (operand.StartsWith("0x", StringComparison.Ordinal))
            return int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (operand.StartsWith("bit[", StringComparison.Ordinal) || operand.StartsWith("var[", StringComparison.Ordinal))
        {
            if (operand.Contains('+')) return false; // an indexed variable
            if (variable(operand) is ushort v) { value = (short)v; return true; }
            value = 0; return true; // a variable nothing has set is 0 at the start
        }
        return false;
    }

    private static LoomCdGuard Make(ScummScriptDisassembly script, string label, ScummScriptInstruction i, bool mustHold)
    {
        var subject = i.Operands.Split(',')[0].Trim();
        var origin = "";
        int? boxActor = null;
        if (FirstVariable().Match(subject) is { Success: true } m && m.Groups[1].Value == "local")
        {
            var source = LocalSource(script, subject, i.Offset);
            origin = source is null ? "" : $"{source.Operation} {source.Operands} at +0x{source.Offset:X4}";
            if (source is { Operation: "getActorWalkBox" } && source.Operands.Split(",", StringSplitOptions.TrimEntries) is [_, var actor, ..] &&
                actor.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(actor.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
                boxActor = number;
        }
        return new(label, i.Offset, i.Operation, i.Operands, mustHold, subject, origin, boxActor);
    }

    /// <summary>What last wrote a local before the test: the assignment's text (a walk box of an actor, a random pick, a copy).</summary>
    private static ScummScriptInstruction? LocalSource(ScummScriptDisassembly script, string local, int before)
    {
        for (var depth = 0; depth < 4; depth++)
        {
            var last = script.Instructions.LastOrDefault(i => i.Offset < before && (i.Operands.StartsWith(local + ",", StringComparison.Ordinal) && !i.Operands.StartsWith(local + ", branch", StringComparison.Ordinal) || i.Operands.StartsWith(local + " = ", StringComparison.Ordinal)) &&
                i.Operation is "move" or "getRandomNr" or "getActorWalkBox" or "getActorX" or "getActorY" or "getActorRoom" or "getObjectOwner" or "getObjectState" or "isScriptRunning" or "getActorElevation");
            // A copy of another local: follow it back to where that one was set.
            if (last is { Operation: "move" } && last.Operands.Split(" = ", 2) is [_, var rhs] && rhs.StartsWith("local[", StringComparison.Ordinal))
            {
                local = rhs; before = last.Offset; continue;
            }
            return last;
        }
        return null;
    }

    private static void Walk(IScummDataSource data, int room, ScummScriptDisassembly script, string label, List<string> chain, List<LoomCdGuard> carried,
        int depthLeft, HashSet<(bool, int)> visited, List<LoomCdScenePath> paths)
    {
        foreach (var span in script.Instructions.Where(i => i.Operation == "cutscene"))
            paths.Add(new(chain.ToArray(), span.Offset, [.. carried, .. GuardsAt(script, label, span.Offset)]));
        if (depthLeft <= 0) return;
        foreach (var call in script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
        {
            if (LiteralScriptNumber().Match(call.Operands) is not { Success: true } m) continue;
            var id = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var local = id >= data.GlobalScriptCount;
            if (!visited.Add((local, id))) continue;
            ScummScriptDisassembly? next;
            try { next = local ? data.RoomLocalScript(room, id) : data.GlobalScript(id); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { continue; }
            if (next is null) continue;
            var nextLabel = local ? $"Room {room} local script {id}" : $"global script {id}";
            Walk(data, room, next, nextLabel, [.. chain, nextLabel], [.. carried, .. GuardsAt(script, label, call.Offset)], depthLeft - 1, visited, paths);
        }
    }
}

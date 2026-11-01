using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummSceneSetupSearch
{
    /// <summary>
    /// For each test of a script that guards the way to the scene (1 November 2026): true when only falling through the test leads on to the scene's
    /// <c>cutscene</c> instruction (or, in a calling script, to the call of the script that holds it), false when only its branch does. Tests with both
    /// or neither way to it are left out. ScummVM's <c>jumpRelative(cond)</c> jumps when the condition is false, so falling through means the test held.
    /// Indy 4 Room 44's entry tests <c>var[0x07A]</c> against 502 to 510 in turn and plays its scene on one branch only: that value is the one to try.
    /// </summary>
    private Dictionary<int, bool> GuardSides(string label, ScummScriptDisassembly script, (string Label, int Offset)? cutscene, IReadOnlySet<string> toward,
        ScummCutscene trigger)
    {
        var sides = new Dictionary<int, bool>();
        if (cutscene is not { } scene) return sides;
        var instructions = script.Instructions;
        var index = new Dictionary<int, int>();
        for (var i = 0; i < instructions.Count; i++) index.TryAdd(instructions[i].Offset, i);
        var targets = new HashSet<int>();
        if (label == scene.Label)
        {
            if (index.TryGetValue(scene.Offset, out var at)) targets.Add(at);
        }
        else
        {
            // A call of a script on the way to the cutscene's script (the cutscene's script itself, or one that calls it).
            for (var i = 0; i < instructions.Count; i++)
                if (instructions[i].Operation is "startScript" or "chainScript" && Literal(instructions[i].Operands.Split(',')[0].Trim()) is int id &&
                    (toward.Contains($"Room {trigger.Room} local script {id}") || toward.Contains($"global script {id}"))) targets.Add(i);
        }
        if (targets.Count == 0) return sides;

        int? Branch(int i) => BranchPattern().Match(instructions[i].Operands) is { Success: true } m &&
                              index.TryGetValue(int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), out var to) ? to : null;
        IEnumerable<int> Next(int i)
        {
            var ins = instructions[i];
            if (ins.Operation == "jumpRelative")
            {
                if (Branch(i) is int to) yield return to;
                yield break;
            }
            if (ins.Operation is "stopObjectCode" or "chainScript" || ins.Operation == "stopScript" && (ins.Operands.Length == 0 || Literal(ins.Operands.Trim()) == 0)) yield break;
            if (i + 1 < instructions.Count) yield return i + 1;
            if (Branch(i) is int branch) yield return branch;
        }
        // The instructions from which a target can be reached (a walk back over the edges).
        var into = new List<int>[instructions.Count];
        for (var i = 0; i < instructions.Count; i++)
            foreach (var to in Next(i)) (into[to] ??= []).Add(i);
        var reaches = new HashSet<int>(targets);
        var work = new Stack<int>(targets);
        while (work.Count > 0)
            foreach (var from in into[work.Pop()] ?? [])
                if (reaches.Add(from)) work.Push(from);
        for (var i = 0; i + 1 < instructions.Count; i++)
        {
            if (instructions[i].Operation == "jumpRelative" || Branch(i) is not int branch || !reaches.Contains(i)) continue;
            var through = reaches.Contains(i + 1);
            var jumped = reaches.Contains(branch);
            if (through != jumped) sides[instructions[i].Offset] = through;
        }
        return sides;
    }

    /// <summary>
    /// The scripts the trigger's own code starts or chains to (by literal number, a few calls deep) that lead on to the script holding the scene's
    /// cutscene, which the scan does not list (it lists the scripts the scene reaches): Monkey Island 2 Room 56's entry starts local script 200, whose
    /// bit tests decide whether it starts local 207. With them, every script from which the cutscene's script is called (<paramref name="toward"/>).
    /// </summary>
    private List<string> OnTheWay(string own, ScummCutscene trigger, string? sceneLabel, out HashSet<string> toward)
    {
        toward = [];
        if (sceneLabel is null) return [];
        var calls = new Dictionary<string, List<string>>();
        var queue = new Queue<(string Label, int Depth)>([(own, 0)]);
        foreach (var listed in trigger.Scripts) queue.Enqueue((listed, 0));
        while (queue.Count > 0 && calls.Count < 24)
        {
            var (label, depth) = queue.Dequeue();
            if (calls.ContainsKey(label)) continue;
            var called = new List<string>();
            calls[label] = called;
            if (Read(label, trigger) is not { } script) continue;
            foreach (var ins in script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
            {
                if (Literal(ins.Operands.Split(',')[0].Trim()) is not int id) continue;
                var local = $"Room {trigger.Room} local script {id}";
                var target = Read(local, trigger) is not null ? local : $"global script {id}";
                if (!called.Contains(target)) called.Add(target);
                if (depth < 3) queue.Enqueue((target, depth + 1));
            }
        }
        // Walk back from the cutscene's script over the calls.
        toward.Add(sceneLabel);
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var (label, called) in calls)
                if (!toward.Contains(label) && called.Any(toward.Contains)) changed = toward.Add(label) || changed;
        }
        var found = toward;
        return calls.Keys.Where(label => found.Contains(label) && label != own && !trigger.Scripts.Contains(label)).ToList();
    }

    /// <summary>Whether a test falls through (holds) when its tested value is <paramref name="value"/>, as ScummVM's o5 comparisons read; null when unknown.</summary>
    private static bool? Holds(string operation, int? constant, int value) => operation switch
    {
        "isEqual" or "ifState" when constant is int c => value == c,
        "isNotEqual" or "ifNotState" when constant is int c => value != c,
        // o5_isLess: jumpRelative(b < a), a the variable and b the constant.
        "isLess" when constant is int c => c < value,
        "isGreater" when constant is int c => c > value,
        "isLessEqual" when constant is int c => c <= value,
        "isGreaterEqual" when constant is int c => c >= value,
        "equalZero" => value == 0,
        "notEqualZero" => value != 0,
        "ifClassOfIs" => value == 1,
        _ => null,
    };

    [GeneratedRegex(@"->\s*0x([0-9A-F]+)\s*$")]
    private static partial Regex BranchPattern();
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// Turns a decoded script into plain language: names from the <see cref="LoomGlossary"/>, one phrase per opcode family, and
/// forward jumps recovered into <c>if / else / while</c> blocks. It only re-reads what the disassembler decoded; the raw view
/// stays the evidence. SCUMM conditionals jump when the condition is false, so a block is the code that runs when it is true.
/// Anything without a phrase keeps its raw line.
/// </summary>
public sealed partial class LoomScriptPresenter(LoomGlossary glossary, Func<int, string?>? verbLabel = null)
{
    /// <summary>Object numbers below this are actors (ScummVM <c>putClass</c> and <c>_numActors</c> for v3).</summary>
    private const int NumActors = 13;

    private enum Role { Number, Actor, Object, Room, Script, Sound, Verb }

    private readonly Dictionary<string, (GlossaryEntry Entry, string Ref)> _used = new();
    private readonly Dictionary<int, int> _loopEnds = new();
    private readonly HashSet<int> _openLoops = [];
    private string? _scope;
    private string? _roomScope;

    public string Present(LoomScriptDisassembly script)
    {
        _used.Clear();
        _scope = script.Context is null ? $"script:{script.ScriptId}" : null;
        _roomScope = RoomScope(script.Context);
        var output = new StringBuilder();
        output.AppendLine(script.Context is null ? ScriptTitle(script) : $"{script.Context}  ·  {script.ContainerFileNumber:00}.LFL");
        output.AppendLine("Readable view: the decoded instructions in plain language. Static paths, not proof of execution. " +
                          "Names carry their evidence (listed at the end); anything unnamed keeps its raw form. Switch to Raw for the instructions as decoded.");
        output.AppendLine();

        var list = script.Instructions;
        var index = new Dictionary<int, int>();
        for (var i = 0; i < list.Count; i++) index[list[i].Offset] = i;
        _loopEnds.Clear();
        _openLoops.Clear();
        foreach (var ins in list)
            if (IsConditional(ins.Operation) && TryBranch(ins, out var back) && back <= ins.Offset && index.ContainsKey(back))
                _loopEnds.TryAdd(back, ins.Offset);
        var lines = new List<string>();
        Emit(list, index, script.CodeLength, 0, list.Count, 0, lines);
        foreach (var line in lines) output.AppendLine(line);

        if (script.Issues is { Count: > 0 })
        {
            output.AppendLine();
            foreach (var issue in script.Issues)
                output.AppendLine($"Not decoded: path at +0x{issue.Offset:X4}: {issue.Reason}");
        }
        if (_used.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Names used (evidence: decoded = read from instructions or game data, engine = ScummVM, reading = interpretation, play = seen in play):");
            foreach (var (name, (entry, reference)) in _used.OrderBy(pair => pair.Value.Ref, StringComparer.Ordinal))
                output.AppendLine($"  {reference,-16} {name}  [{entry.EvidenceLabel}]" + (entry.Source.Length > 0 ? $"  {entry.Source}" : ""));
        }
        return output.ToString();
    }

    private string ScriptTitle(LoomScriptDisassembly script)
    {
        var named = glossary.Script(script.ScriptId);
        return $"Script {script.ScriptId:00}" + (named is null ? "" : $" · {named.Name}") + $"  ·  {script.ContainerFileNumber:00}.LFL";
    }

    // ---- structure -------------------------------------------------------------------------------------------------

    private void Emit(IReadOnlyList<LoomScriptInstruction> list, Dictionary<int, int> index, int codeLength, int lo, int hi, int depth, List<string> lines)
    {
        var i = lo;
        while (i < hi)
        {
            var ins = list[i];
            if (!_openLoops.Contains(ins.Offset) && _loopEnds.TryGetValue(ins.Offset, out var back) && index[back] is int tail && tail >= i && tail < hi)
            {
                // A backward conditional jumps when its condition is false: the code from its target runs again until the condition holds.
                _openLoops.Add(ins.Offset);
                var single = tail == i + 1 && ins.Operation == "breakHere";
                if (single) lines.Add(Line(ins.Offset, depth, "wait until " + Condition(list[tail])));
                else
                {
                    lines.Add(Line(ins.Offset, depth, "repeat"));
                    Emit(list, index, codeLength, i, tail, depth + 1, lines);
                    lines.Add(Line(list[tail].Offset, depth, "until " + Condition(list[tail])));
                }
                _openLoops.Remove(ins.Offset);
                i = tail + 1;
                continue;
            }
            if (IsConditional(ins.Operation) && TryBranch(ins, out var target) && target > ins.Offset && IndexOf(index, list.Count, codeLength, target) is int end && end > i && end <= hi)
            {
                var bodyLo = i + 1;
                var bodyHi = end;
                var elseLo = -1;
                var elseHi = -1;
                var isLoop = false;
                if (bodyHi > bodyLo && list[bodyHi - 1].Operation == "jumpRelative" && TryBranch(list[bodyHi - 1], out var jump))
                {
                    if (jump == ins.Offset) { isLoop = true; bodyHi--; }
                    else if (jump > target && IndexOf(index, list.Count, codeLength, jump) is int elseEnd && elseEnd <= hi)
                    {
                        bodyHi--;
                        elseLo = end;
                        elseHi = elseEnd;
                    }
                }
                if (elseLo >= 0 && bodyHi == bodyLo)
                {
                    lines.Add(Line(ins.Offset, depth, "if " + Condition(ins, negate: true)));
                    Emit(list, index, codeLength, elseLo, elseHi, depth + 1, lines);
                    lines.Add(Line(null, depth, "end if"));
                    i = elseHi;
                    continue;
                }
                lines.Add(Line(ins.Offset, depth, (isLoop ? "while " : "if ") + Condition(ins)));
                Emit(list, index, codeLength, bodyLo, bodyHi, depth + 1, lines);
                if (elseLo >= 0)
                {
                    lines.Add(Line(null, depth, "else"));
                    Emit(list, index, codeLength, elseLo, elseHi, depth + 1, lines);
                    lines.Add(Line(null, depth, "end if"));
                    i = elseHi;
                    continue;
                }
                lines.Add(Line(null, depth, isLoop ? "end while" : "end if"));
                i = end;
                continue;
            }
            // The jump that follows beginOverride is the skip target, not a jump of the normal path.
            if (ins.Operation == "beginOverride" && i + 1 < hi && list[i + 1].Operation == "jumpRelative" && TryBranch(list[i + 1], out var skipTo))
            {
                lines.Add(Line(ins.Offset, depth, $"begin the skippable part (if the player skips, the game jumps to {skipTo:X4})"));
                i += 2;
                continue;
            }
            lines.Add(Line(ins.Offset, depth, Phrase(ins)));
            i++;
        }
    }

    private static int? IndexOf(Dictionary<int, int> index, int count, int codeLength, int offset) =>
        index.TryGetValue(offset, out var found) ? found : offset >= codeLength ? count : null;

    private static string Line(int? offset, int depth, string text) =>
        (offset is int value ? $"{value:X4}" : "    ") + "  " + new string(' ', depth * 4) + text;

    private static bool TryBranch(LoomScriptInstruction ins, out int target)
    {
        target = 0;
        if (!IsConditional(ins.Operation) && ins.Operation != "jumpRelative") return false;
        var match = ArrowRegex().Match(ins.Operands);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out target);
    }

    private static bool IsConditional(string operation) => operation is
        "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or "equalZero" or
        "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState";

    // ---- conditions ------------------------------------------------------------------------------------------------

    private string Condition(LoomScriptInstruction ins, bool negate = false)
    {
        var args = Arguments(ins);
        if (args.Count < (ins.Operation is "equalZero" or "notEqualZero" ? 1 : 2)) return $"[raw] {ins.Operation} {ins.Operands}";
        switch (ins.Operation)
        {
            case "equalZero": return Truth(args[0], negate);
            case "notEqualZero": return Truth(args[0], !negate);
            case "ifState": return $"{Value(args[0], Role.Object)} is {(negate ? "not " : "")}in state {Value(args[1])}";
            case "ifNotState": return $"{Value(args[0], Role.Object)} is {(negate ? "" : "not ")}in state {Value(args[1])}";
            case "ifClassOfIs": return ClassTest(args[0], args[1], negate);
            case "isActorInBox": return $"{Value(args[0], Role.Actor)} is {(negate ? "not " : "")}in walk box {Value(args[1])}";
        }
        // ScummVM jumps when (value OP variable) is false, so the code that follows runs while the variable is on the other side.
        var relation = ins.Operation switch
        {
            "isEqual" => "==", "isNotEqual" => "!=", "isGreater" => "<", "isGreaterEqual" => "<=", "isLess" => ">", _ => ">="
        };
        if (negate) relation = relation switch { "==" => "!=", "!=" => "==", "<" => ">=", "<=" => ">", ">" => "<=", _ => "<" };
        if (args.Count >= 2 && VarKind(args[0]) == "bit" && Literal(args[1]) is 0 or 1 && relation is "==" or "!=")
            return Truth(args[0], (Literal(args[1]) == 1) == (relation == "=="));  // relation was already negated when asked to
        var role = VarIndex(args[0]) switch { 0x004 or 0x070 => Role.Room, 0x001 => Role.Actor, _ => Role.Number };
        return $"{Value(args[0])} {relation} {Value(args[1], role)}";
    }

    /// <summary>ScummVM <c>o5_ifClassOfIs</c>: a class value with 0x80 must be set, one without must be clear.</summary>
    private string ClassTest(string subject, string classes, bool negate)
    {
        var who = Value(subject, Literal(subject) < NumActors ? Role.Actor : Role.Object);
        var parts = List(classes).Select(c => Literal(c) is int v ? $"{((v & 0x80) != 0 ? "in" : "not in")} class {v & 0x7F}" : $"in the class in {Value(c)}");
        var text = $"{who} is {string.Join(" and ", parts)}";
        return negate ? $"not ({text})" : text;
    }

    private string Truth(string token, bool wanted)
    {
        if (VarKind(token) == "bit") return wanted ? Value(token) : $"not ({Value(token)})";
        return $"{Value(token)} {(wanted ? "!=" : "==")} 0";
    }

    // ---- phrases ---------------------------------------------------------------------------------------------------

    private string Phrase(LoomScriptInstruction ins)
    {
        var a = Arguments(ins);
        try
        {
            switch (ins.Operation)
            {
                case "stopObjectCode": return "end of script";
                case "breakHere": return "pause until the next frame";
                case "endCutscene": return "end cutscene";
                case "cutscene": return "begin cutscene" + (List(a[0]).Count > 0 ? $" (arguments {string.Join(", ", List(a[0]).Select(x => Value(x)))})" : "");
                case "beginOverride": return "begin the skippable part (if the player skips, the game jumps to the end of the cutscene)";
                case "endOverride": return "end of the skippable part";
                case "stopMusic": return "stop the music";
                case "jumpRelative": return TryBranch(ins, out var target) ? (target <= ins.Offset ? $"go back to {target:X4}" : $"go to {target:X4}") : Raw(ins);
                case "startScript": return $"start {Value(a[0], Role.Script)}{With(a[1])}";
                case "chainScript": return $"continue in {Value(a[0], Role.Script)}{With(a[1])} (this script ends)";
                case "stopScript": return Literal(a[0]) == 0 ? "stop this script" : $"stop {Value(a[0], Role.Script)}";
                case "startObject": return $"run {VerbText(a[1])} of {Value(a[0], Role.Object)}{With(a[2])}";
                case "stopObjectScript": return $"stop the script of {Value(a[0], Role.Object)}";
                case "isScriptRunning": return $"{Value(a[0])} = whether {Value(a[1], Role.Script)} is running";
                case "isSoundRunning": return $"{Value(a[0])} = whether {Value(a[1], Role.Sound)} is playing";
                case "loadRoom": return $"go to {Value(a[0], Role.Room)}";
                case "loadRoomWithEgo": return $"go to {Value(a[1], Role.Room)} through {Value(a[0], Role.Object)}; Bobbin walks to {a[2].Replace("walkTo=", "")}";
                case "putActor": return $"place {Value(a[0], Role.Actor)} at ({Value(a[1])}, {Value(a[2])})";
                case "putActorInRoom": return $"put {Value(a[0], Role.Actor)} in {Value(a[1], Role.Room)}";
                case "putActorAtObject": return $"place {Value(a[0], Role.Actor)} at {Value(a[1], Role.Object)}";
                case "walkActorTo": return $"{Value(a[0], Role.Actor)} walks to ({Value(a[1])}, {Value(a[2])})";
                case "walkActorToObject": return $"{Value(a[0], Role.Actor)} walks to {Value(a[1], Role.Object)}";
                case "walkActorToActor": return $"{Value(a[0], Role.Actor)} walks to {Value(a[1], Role.Actor)} (stopping {Value(a[2])} away)";
                case "faceActor": return $"{Value(a[0], Role.Actor)} turns to face {Value(a[1], Role.Object)}";
                case "animateActor": return $"{Value(a[0], Role.Actor)}: {Animation(a[1])}";
                case "actorFollowCamera": return $"the camera follows {Value(a[0], Role.Actor)}";
                case "setCameraAt": return $"the camera jumps to x={Value(a[0])}";
                case "panCameraTo": return $"the camera pans to x={Value(a[0])}";
                case "actorOps": return $"{Value(a[0], Role.Actor)}: {string.Join("; ", Items(a[1]).Select(ActorItem))}";
                case "print": return Print(a, false);
                case "printEgo": return Print(a, true);
                case "startMusic": return $"start {Value(a[0], Role.Sound)} (music)";
                case "startSound": return $"start {Value(a[0], Role.Sound)}";
                case "stopSound": return $"stop {Value(a[0], Role.Sound)}";
                case "delay": return $"wait {Convert.ToInt32(a[0], 16)} jiffies (60 jiffies = 1 second)";
                case "delayVariable": return $"wait {Value(a[0])} jiffies";
                case "waitFor": return WaitFor(a);
                case "waitForSentence": return "wait for the sentence to finish";
                case "waitForActor": return a[0] == "LOOM no-op" ? "(no operation in LOOM)" : $"wait until {Value(a[0], Role.Actor)} stops";
                case "freezeScripts": return $"freeze other scripts (level {Value(a[0])})";
                case "setState": return $"set {Value(a[0], Role.Object)} to state {Value(a[1])}";
                case "drawObject": return $"draw {Value(a[0], Role.Object)} at ({Value(a[2])}, {Value(a[3])})";
                case "pickupObject": return $"pick up {Value(a[0], Role.Object)}";
                case "setClass": return $"{Value(a[0], Literal(a[0]) < NumActors ? Role.Actor : Role.Object)}: {string.Join(", ", List(a[1]).Select(ClassItem))}";
                case "drawBox": return $"fill the box ({Value(a[0])}, {Value(a[1])}) to ({Value(a[2])}, {Value(a[3])}) with colour {Value(a[4])}";
                case "setBoxFlags": return $"walk box {Value(a[0])}: flags {a[1]}";
                case "move": return Move(a[0], a[1]);
                case "add": return $"{Value(a[0])} += {Value(Strip(a[1], "+="))}";
                case "subtract": return $"{Value(a[0])} -= {Value(Strip(a[1], "-="))}";
                case "multiply": return $"{Value(a[0])} *= {Value(Strip(a[1], "*="))}";
                case "divide": return $"{Value(a[0])} /= {Value(Strip(a[1], "/="))}";
                case "and": return $"{Value(a[0])} &= {Value(Strip(a[1], "&="))}";
                case "or": return $"{Value(a[0])} |= {Value(Strip(a[1], "|="))}";
                case "increment": return $"{Value(a[0])} += 1";
                case "decrement": return $"{Value(a[0])} -= 1";
                case "expression": return $"{Value(a[0])} = {Expression(a[1])}";
                case "getRandomNr": return $"{Value(a[0])} = a random number from 0 to {Value(a[1])}";
                case "getActorX": return $"{Value(a[0])} = x of {Value(a[1], Role.Actor)}";
                case "getActorY": return $"{Value(a[0])} = y of {Value(a[1], Role.Actor)}";
                case "getActorFacing": return $"{Value(a[0])} = direction {Value(a[1], Role.Actor)} faces";
                case "getActorRoom": return $"{Value(a[0])} = room of {Value(a[1], Role.Actor)}";
                case "getActorCostume": return $"{Value(a[0])} = costume of {Value(a[1], Role.Actor)}";
                case "getActorWalkBox": return $"{Value(a[0])} = walk box of {Value(a[1], Role.Actor)}";
                case "getActorMoving": return $"{Value(a[0])} = whether {Value(a[1], Role.Actor)} is moving";
                case "getActorElevation": return $"{Value(a[0])} = elevation of {Value(a[1], Role.Actor)}";
                case "getActorWidth": return $"{Value(a[0])} = width of {Value(a[1], Role.Actor)}";
                case "getDist": return $"{Value(a[0])} = distance between {Value(a[1], Role.Object)} and {Value(a[2], Role.Object)}";
                case "findObject": return $"{Value(a[0])} = the object at ({Value(a[1])}, {Value(a[2])})";
                case "getObjectOwner": return $"{Value(a[0])} = owner of {Value(a[1], Role.Object)}";
                case "setVarRange": return $"fill {Value(a[0])} with {a[1].Replace("count=", "")} values";
                case "cursorCommand": return "cursor: " + CursorText(ins.Operands);
                case "resourceRoutines": return Resource(a[0], a);
                case "verbOps": return $"verb {Value(a[0])}: {string.Join("; ", Items(a[1]))}";
                default: return Raw(ins);
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException or ArgumentOutOfRangeException)
        {
            return Raw(ins);
        }
    }

    private static string Resource(string routine, List<string> a)
    {
        var words = Regex.Replace(routine, "([A-Z])", " $1").ToLowerInvariant();
        return a.Count > 1 ? $"{words} {string.Join(", ", a.Skip(1).Select(x => Literal(x)?.ToString(CultureInfo.InvariantCulture) ?? x))}" : words;
    }

    /// <summary>The plain-language phrase for an event the executor recorded (it keeps the instruction's operation and operands).</summary>
    public string Describe(LoomScriptEvent scriptEvent)
    {
        _scope = null;
        _roomScope = RoomScope(scriptEvent.SourceLabel);
        return Phrase(new LoomScriptInstruction(scriptEvent.Offset, 0, scriptEvent.Operation, scriptEvent.Operands, ""));
    }

    private static string? RoomScope(string? label) =>
        label is not null && RoomLabelRegex().Match(label) is { Success: true } m ? $"room:{int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)}" : null;

    [GeneratedRegex(@"^Room (\d+)")]
    private static partial Regex RoomLabelRegex();

    private static string Raw(LoomScriptInstruction ins) => $"[raw] {ins.Operation}{(ins.Operands.Length > 0 ? " " + ins.Operands : "")}";

    private static string Strip(string text, string symbol) => text.StartsWith(symbol, StringComparison.Ordinal) ? text[symbol.Length..].Trim() : text;

    private string With(string list) => List(list) is { Count: > 0 } items ? $" with {string.Join(", ", items.Select(x => Value(x)))}" : "";

    private string VerbText(string token)
    {
        if (Literal(token) is int verb)
            return verbLabel?.Invoke(verb) is { Length: > 0 } label ? $"verb \"{label}\"" : $"verb 0x{verb:X2}";
        return $"the verb in {Value(token)}";
    }

    private static string Animation(string token)
    {
        if (Literal(token) is not int n) return $"animation {token}";
        var direction = (n & 3) switch { 0 => "left", 1 => "right", 2 => "towards the screen", _ => "away from the screen" };
        return n switch
        {
            >= 0xFC => $"stops and stands facing {direction}",
            >= 0xF8 => $"faces {direction}",
            >= 0xF4 => $"turns to face {direction}",
            _ => $"plays animation {n}"
        };
    }

    private string WaitFor(List<string> a)
    {
        var text = a[0];
        if (text == "actor" && a.Count > 1) return $"wait until {Value(a[1], Role.Actor)} stops walking";
        return text switch
        {
            "message" => "wait until the message is gone",
            "camera" => "wait until the camera arrives",
            "sentence" => "wait for the sentence to finish",
            _ => $"wait for {text}"
        };
    }

    private string Print(List<string> a, bool ego)
    {
        var who = ego ? "Bobbin" : Literal(a[0]) switch
        {
            0xFF => "text with no actor",
            0xFE => "text in the verb area",
            _ => Value(a[0], Role.Actor)
        };
        string? text = null;
        var extras = new List<string>();
        foreach (var item in Items(a[1]))
        {
            if (item.StartsWith("text, ", StringComparison.Ordinal)) text = CleanText(item["text, ".Length..]);
            else if (item.StartsWith("position, ", StringComparison.Ordinal))
                extras.Add("at " + string.Join(", ", Split(item["position, ".Length..], ',').Select(x => Value(x))));
            else if (item.StartsWith("color, ", StringComparison.Ordinal)) extras.Add("colour " + Value(item["color, ".Length..]));
        }
        var spoken = Literal(a[0]) is 0xFF or 0xFE ? who + ":" : who + " says:";
        return text is null ? $"{who}: {string.Join(", ", Items(a[1]))}" : $"{spoken} {text}{(extras.Count > 0 ? $"  ({string.Join(", ", extras)})" : "")}";
    }

    private static string CleanText(string quoted)
    {
        var text = LoomScummTextDecoder.DecodeLiterals(quoted).FirstOrDefault() ?? quoted;
        return "“" + text.Replace("\r\n", " / ").Replace("\n", " / ") + "”";
    }

    private string ActorItem(string item)
    {
        var parts = Split(item, ',');
        var name = parts[0];
        var rest = parts.Skip(1).ToList();
        string One(int i = 0) => Value(rest[i]);
        try
        {
            return name switch
            {
                "costume" => $"costume {One()}",
                "init" => "initialise",
                "walkSpeed" => $"walk speed {One()}, {One(1)}",
                "talkColor" => $"talk colour {One()}",
                "elevation" => $"elevation {One()}",
                "walkAnimation" => $"walk animation {One()}",
                "standAnimation" => $"stand animation {One()}",
                "talkAnimation" => $"talk animation {One()}, {One(1)}",
                "initAnimation" => $"init animation {One()}",
                "ignoreBoxes" => "ignore walk boxes",
                "followBoxes" => "follow walk boxes",
                "actorName" => $"name {rest[0]}",
                _ => item
            };
        }
        catch (ArgumentOutOfRangeException) { return item; }
    }

    private string ClassItem(string token)
    {
        if (Literal(token) is not int value) return $"class {Value(token)}";
        if (value == 0) return "clear all classes";
        return $"{((value & 0x80) != 0 ? "set" : "clear")} class {value & 0x7F}";
    }

    private static string CursorText(string command) => command switch
    {
        "cursorOn" => "show", "cursorOff" => "hide", "userInputOn" => "player input on", "userInputOff" => "player input off",
        "cursorSoftOn" => "show (soft)", "cursorSoftOff" => "hide (soft)", "userInputSoftOn" => "player input on (soft)",
        "userInputSoftOff" => "player input off (soft)", _ => command
    };

    private string Move(string destination, string value)
    {
        value = Strip(value, "=");
        if (VarKind(destination) == "bit" && Literal(value) is 0 or 1)
            return (Literal(value) == 1 ? "set: " : "clear: ") + Value(destination);
        return $"{Value(destination)} = {Value(value)}";
    }

    /// <summary>A postfix SCUMM expression as an infix one.</summary>
    private string Expression(string terms)
    {
        var stack = new Stack<string>();
        foreach (var term in Split(terms.Trim('[', ']'), ' '))
        {
            if (term is "+" or "-" or "*" or "/" && stack.Count >= 2)
            {
                var right = stack.Pop();
                var left = stack.Pop();
                stack.Push($"({left} {term} {right})");
            }
            else if (term.Contains('(') && term.EndsWith(')'))
            {
                var open = term.IndexOf('(');
                var arguments = Split(term[(open + 1)..^1], ',');
                stack.Push($"{term[..open]}({string.Join(", ", arguments.Skip(1).Select(x => Value(x)))})");
            }
            else stack.Push(Value(term));
        }
        if (stack.Count != 1) return string.Join(" ", stack.Reverse());
        var result = stack.Pop();
        return result.StartsWith('(') && result.EndsWith(')') && Balanced(result[1..^1]) ? result[1..^1] : result;
    }

    private static bool Balanced(string text)
    {
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '(') depth++;
            else if (c == ')' && --depth < 0) return false;
        }
        return depth == 0;
    }

    // ---- values and names ------------------------------------------------------------------------------------------

    private string Value(string token, Role role = Role.Number)
    {
        token = token.Trim();
        if (Literal(token) is int value)
        {
            switch (role)
            {
                case Role.Actor:
                    return glossary.Actor(value, _scope, _roomScope) is { } actor ? Use(actor, $"actor {value}") : $"actor {value}";
                case Role.Object:
                    return glossary.Object(0, value) is { } named ? Use(named, $"object {value}", $"object {value} “{named.Name}”") : $"object {value}";
                case Role.Room:
                    return glossary.Room(value) is { } room ? Use(room, $"room {value}", $"Room {value:00} · {room.Name}") : $"Room {value:00}";
                case Role.Script:
                    return glossary.Script(value) is { } script ? Use(script, $"script {value}", $"Script {value:00} · {script.Name}") : $"Script {value:00}";
                case Role.Sound:
                    return glossary.Sound(value) is { } sound ? Use(sound, $"sound {value}", $"Sound {value} · {sound.Name}") : $"Sound {value}";
            }
            return value.ToString(CultureInfo.InvariantCulture);
        }
        var match = VariableRegex().Match(token);
        if (!match.Success) return token;
        var kind = match.Groups[1].Value;
        var index = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (kind == "local") return $"local {index}";
        if (role == Role.Actor && kind == "var" && index == 0x001) return "Bobbin";
        var entry = kind == "var" ? glossary.Variable(index) : glossary.Bit(index);
        var phrase = entry is null ? token : Use(entry, token);
        return role == Role.Actor && entry is null ? $"the actor in {phrase}" : phrase;
    }

    private string Use(GlossaryEntry entry, string reference, string? text = null)
    {
        _used.TryAdd(entry.Name, (entry, reference));
        return text ?? entry.Name;
    }

    private static string VarKind(string token) => VariableRegex().Match(token.Trim()) is { Success: true } m ? m.Groups[1].Value : "";

    private static int? VarIndex(string token) => VariableRegex().Match(token.Trim()) is { Success: true } m && VarKind(token) == "var"
        ? int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : null;

    private static int? Literal(string token) =>
        LiteralRegex().IsMatch(token.Trim()) ? int.Parse(token.Trim()[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : null;

    // ---- operand parsing -------------------------------------------------------------------------------------------

    private static List<string> Arguments(LoomScriptInstruction ins)
    {
        var text = BranchRegex().Replace(ins.Operands, "").TrimEnd(',', ' ');
        // The decoder writes variable operations as "dest = value" (and +=, -=...), not as a comma list.
        var assign = AssignRegex().Match(text);
        if (assign.Success && ins.Operation is "move" or "add" or "subtract" or "multiply" or "divide" or "and" or "or")
            return [assign.Groups[1].Value, assign.Groups[2].Value + " " + assign.Groups[3].Value];
        return Split(text, ',');
    }

    private static List<string> List(string token) =>
        token.StartsWith('[') && token.EndsWith(']') ? Split(token[1..^1], ',').Where(x => x.Length > 0).ToList() : [];

    private static List<string> Items(string token) =>
        token.StartsWith('[') && token.EndsWith(']') ? Split(token[1..^1], ';').Where(x => x.Length > 0).ToList() : [];

    /// <summary>Splits at a separator outside quotes and brackets.</summary>
    private static List<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '\\') i++;
                else if (c == '"') quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c is '[' or '(') depth++;
            else if (c is ']' or ')') depth--;
            else if (c == separator && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(text[start..].Trim());
        return parts;
    }

    [GeneratedRegex(@"->\s*0x([0-9A-Fa-f]+)")]
    private static partial Regex ArrowRegex();

    [GeneratedRegex(@",?\s*branch \+0x[0-9A-Fa-f]+\s*->\s*0x[0-9A-Fa-f]+")]
    private static partial Regex BranchRegex();

    [GeneratedRegex(@"^(\S+) (\+=|-=|\*=|/=|&=|\|=|=) (.+)$")]
    private static partial Regex AssignRegex();

    [GeneratedRegex(@"^(var|bit|local)\[0x([0-9A-Fa-f]+)\]$")]
    private static partial Regex VariableRegex();

    [GeneratedRegex(@"^0x[0-9A-Fa-f]+$")]
    private static partial Regex LiteralRegex();
}

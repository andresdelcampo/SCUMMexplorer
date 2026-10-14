using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>
    /// True while the boot is still running: only after a session built with <c>stopAtOpening</c>, until the frames
    /// played since reach the point where the constructor would have stopped (script 001 has ended or chained on and no
    /// cutscene is open).
    /// </summary>
    public bool BootRunning => _booting;

    protected bool BootGoesOn => (_pending.Any(item => item.ScriptNumber == BootScript) || InCutscene) && _pending.Count > 0;

    protected void FinishBoot(int frames)
    {
        _booting = false;
        BootFrames = frames;
        if (_pending.Any(item => item.ScriptNumber == BootScript))
            _uncertainties.Add($"The boot (script 001) was still running after {frames} frames; the preview stopped following it.");
        _visitTrace.Add($"boot: {frames} frames after script 001's first room load; now in Room {_currentRoom?.ResourceId:00}");
    }

    /// <summary>Frames the boot may run after script 001's first room load.</summary>
    public const int BootFrameLimit = 5000;

    /// <summary>
    /// The boot's skill screen (Room 69) waits for the player: script 001 waits on a bit that one local script of the room sets once an
    /// answer is given. This starts that script as the player's answer would and returns the note that says so (an estimate: the choice
    /// is not simulated), or null when the boot is not waiting for such an input.
    /// </summary>
    public string? AssumeBootAnswer()
    {
        if (FindAwaitedInput() is not { } input || _currentRoom is null) return null;
        var room = _currentRoom.ResourceId!.Value;
        // A screen whose answer the game computes first (a code wheel): the player's first click starts the questions, and the answer is given
        // only once the game's own scripts have computed it (S5, 14 October 2026).
        if (!_preparationCache.TryGetValue((room, input.LocalScript), out var chain))
            _preparationCache[(room, input.LocalScript)] = chain = FindAnswerPreparation(room, input.LocalScript);
        if (chain is { } prepared)
        {
            if (_answerPrepared.Add(room))
            {
                var note = $"Player input assumed: a first click on Room {room:00} object {prepared.Object.Id} (verb {(prepared.Verb.IsFallback ? "fallback" : $"0x{prepared.Verb.VerbId:X2}")}) " +
                           $"starts the screen's questions: its code starts {string.Join(", ", prepared.Chain.Select(item => $"local script {item}"))}, which compute the answers {string.Join(", ", prepared.Comparands)} that local script {input.LocalScript} checks.";
                _uncertainties.Add(note);
                _visitTrace.Add($"boot: runs Room {room:00} object {prepared.Object.Id}'s code as the player's first click would");
                _dispatchScope++;
                try { RunScript(_data.ReadObjectVerb(room, prepared.Object, prepared.Verb.VerbId, out _), _currentObjects, 0, null, 0); }
                finally { _dispatchScope--; }
                DispatchRequestedLoads("during the boot");
                return note;
            }
            // The questions are asked once the scripts that compute the answers have run.
            if (_pending.Any(item => item.ScriptNumber is int number && prepared.Chain.Contains(number) && !item.Frame.Stopped)) return null;
        }
        _uncertainties.Add(input.Note);
        var (answer, answerNote) = chain is null ? ((ushort)0, null) : DeriveAcceptedAnswer(room, input.LocalScript);
        if (answerNote is not null) _uncertainties.Add(answerNote);
        _visitTrace.Add($"boot: starts Room {_currentRoom.ResourceId:00} local script {input.LocalScript} as the player's answer would");
        _dispatchScope++;
        try
        {
            // The engine starts a script with every local at 0; a script started as an answer takes the answer from local 0.
            RunStartedScript(input.LocalScript, 0, new Dictionary<string, ushort> { ["local[0x000]"] = answer }, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
        return input.Note;
    }

    private readonly HashSet<int> _answerPrepared = [];
    private readonly Dictionary<(int Room, int Script), AnswerPreparation?> _preparationCache = [];

    protected sealed record AnswerPreparation(ScummRoomObjectRecord Object, ScummObjectVerbEntry Verb, IReadOnlyList<int> Chain, IReadOnlyList<string> Comparands);

    /// <summary>
    /// The click that makes the game compute the answers its answering script checks. Read from the room's own scripts: the answering script
    /// compares local 0 with variables (<c>isNotEqual var[K], local[0x000]</c>); room scripts that write those variables, the scripts that start them
    /// (to four levels) and the one object whose verb code starts any of those scripts. Null when the answers need no click (no comparison
    /// of that shape, or nothing in the room computes them) or the object is not unique.
    /// </summary>
    protected AnswerPreparation? FindAnswerPreparation(int room, int answering)
    {
        try
        {
            var code = _data.RoomLocalScript(room, answering);
            if (code is null) return null;
            var comparands = code.Instructions.Where(item => item.Operation == "isNotEqual" && item.Operands.Contains(", local[0x000],", StringComparison.Ordinal))
                .Select(item => item.Operands[..item.Operands.IndexOf(',')].Trim()).Where(item => item.StartsWith("var[", StringComparison.Ordinal)).Distinct().ToArray();
            if (comparands.Length == 0) return null;
            var locals = _data.RoomLocalScriptIds(room).Where(item => item != answering).ToDictionary(item => item, item => _data.RoomLocalScript(room, item)?.Instructions ?? []);
            var chain = locals.Where(pair => pair.Value.Any(item => item.Operation == "move" && comparands.Any(name => item.Operands.StartsWith(name + " =", StringComparison.Ordinal))))
                .Select(pair => pair.Key).ToHashSet();
            if (chain.Count == 0) return null;
            for (var level = 0; level < 4; level++)
            {
                var more = locals.Where(pair => !chain.Contains(pair.Key) && pair.Value.Any(item => item.Operation == "startScript" && StartsAny(item, chain))).Select(pair => pair.Key).ToArray();
                if (more.Length == 0) break;
                foreach (var item in more) chain.Add(item);
            }
            var data = _data.ReadRoom(room);
            var tables = _data.ReadObjectVerbs(room, data.Objects);
            var found = new List<(ScummRoomObjectRecord, ScummObjectVerbEntry)>();
            foreach (var record in data.Objects)
                foreach (var verb in (tables.GetValueOrDefault(record.Id) ?? []).DistinctBy(item => item.ScriptOffset))
                    if (_data.ReadObjectVerb(room, record, verb.VerbId, out _).Instructions.Any(item => item.Operation == "startScript" && StartsAny(item, chain)))
                        found.Add((record, verb));
            return found.Count == 1 ? new AnswerPreparation(found[0].Item1, found[0].Item2, chain.Order().ToArray(), comparands) : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FormatException) { return null; }

        static bool StartsAny(ScummScriptInstruction item, HashSet<int> scripts)
        {
            var head = item.Operands.Split(',', 2, StringSplitOptions.TrimEntries)[0];
            return head.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(head.AsSpan(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var number) && scripts.Contains(number);
        }
    }

    /// <summary>
    /// The answer the answering script accepts at this point, read from the script itself (S5, 14 October 2026): the script checks the answer in
    /// local 0 with <c>isEqual var[S], n</c> (which question is asked) followed at once by <c>isNotEqual var[K], local[0x000]</c> (the right answer
    /// of that question). The right answer is then the current value of var[K], which the game's own code has computed. A script of another
    /// shape gives 0, the answer the engine starts a script with (a wrong one). The pick is an assumed player input, labelled, never a state edit.
    /// </summary>
    protected (ushort Answer, string? Note) DeriveAcceptedAnswer(int room, int script)
    {
        const string? Wrong = null;
        ScummScriptDisassembly? code;
        try { code = _data.RoomLocalScript(room, script); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return (0, Wrong); }
        if (code is null) return (0, Wrong);
        var instructions = code.Instructions;
        for (var i = 1; i < instructions.Count; i++)
        {
            var test = instructions[i];
            if (test.Operation != "isNotEqual" || !test.Operands.Contains(", local[0x000],", StringComparison.Ordinal)) continue;
            var comparand = test.Operands[..test.Operands.IndexOf(',')].Trim();
            var question = instructions[i - 1];
            if (question.Operation != "isEqual" || !comparand.StartsWith("var[", StringComparison.Ordinal)) continue;
            var parts = question.Operands.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !parts[0].StartsWith("var[", StringComparison.Ordinal) || !parts[1].StartsWith("0x", StringComparison.Ordinal)) continue;
            var selector = _runtime.GlobalVariables.GetValueOrDefault(parts[0]);
            if (selector != Convert.ToInt32(parts[1][2..], 16)) continue;
            var answer = _runtime.GlobalVariables.GetValueOrDefault(comparand);
            return (answer, $"Player answer assumed: {answer}, the value of {comparand} that Room {room:00} local script {script} +0x{test.Offset:X4} accepts while {parts[0]} is {selector}: " +
                            "a player who reads the code correctly gives it (Advanced; the choice is an assumption, not a state edit).");
        }
        return (0, Wrong);
    }

    /// <summary>Frames the completed boot ran after script 001's first room load.</summary>
    public int BootFrames { get; protected set; }

    /// <summary>
    /// Script 001 waiting (<c>breakHere</c>, then a branch back while a bit is 0)
    /// for a bit that exactly one local script of the current room sets to 1.
    /// Returns that local script and a note naming the room code that starts it.
    /// </summary>
    protected (int LocalScript, string Note)? FindAwaitedInput()
    {
        if (_currentRoom is null || _pending.FirstOrDefault(item => item.ScriptNumber == BootScript) is not { } boot) return null;
        var wait = boot.Script.Instructions.FirstOrDefault(item => item.Offset == boot.Offset);
        // The boot waits on a bit or a variable that the room's own code sets once the player has answered.
        if (wait is not { Operation: "notEqualZero" } ||
            !(wait.Operands.StartsWith("bit[", StringComparison.Ordinal) || wait.Operands.StartsWith("var[", StringComparison.Ordinal))) return null;
        var bit = wait.Operands[..(wait.Operands.IndexOf(']') + 1)];
        var isBit = bit.StartsWith("bit[", StringComparison.Ordinal);
        // A variable that is already set has been answered; the boot only has not run again to see it.
        if (!isBit && _runtime.GlobalVariables.GetValueOrDefault(bit) != 0) return null;
        var room = _currentRoom.ResourceId!.Value;
        if (!_awaitedInputCache.TryGetValue((room, bit), out var found))
            _awaitedInputCache[(room, bit)] = found = ScanForAnswer(room, bit, isBit);
        if (found is null) return null;
        var (setter, setOffset, startedBy) = found.Value;
        // A copy of the answering script that is still running has not finished: nothing to answer again.
        if (_pending.Any(item => item.ScriptNumber == setter && !item.Frame.Stopped)) return null;
        return (setter, $"Player input assumed: script 001 +0x{wait.Offset:X4} waits for {bit}, which only Room {room:00} " +
                        $"local script {setter} sets (+0x{setOffset:X4}). The room's own code starts it after the player's input " +
                        $"({string.Join(", ", startedBy)}), so the preview starts it as if the player had answered. Choices made on that screen were not simulated.");
    }

    private readonly Dictionary<(int Room, string Variable), (int Setter, int SetOffset, List<string> StartedBy)?> _awaitedInputCache = [];

    // The one local script of the room that sets the awaited bit or variable, and the room code that starts it (null when ambiguous or unstarted).
    private (int Setter, int SetOffset, List<string> StartedBy)? ScanForAnswer(int room, string bit, bool isBit)
    {
        var setters = new List<(int Local, int Offset)>();
        var starters = new List<string>();
        for (var local = _globalScriptCount; local < 256; local++)
        {
            ScummScriptDisassembly? script;
            try { script = _data.RoomLocalScript(room, local); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
            if (script is null) continue;
            foreach (var item in script.Instructions)
            {
                if (IsSettingNonZero(item, bit, isBit)) setters.Add((local, item.Offset));
                if (item.Operation == "startScript" && item.Operands.StartsWith("0x", StringComparison.Ordinal))
                    starters.Add($"local {local} +0x{item.Offset:X4}:{Convert.ToInt32(item.Operands[..4], 16)}");
            }
        }
        // Several instructions of one script may set it (a right answer and the last wrong one); more than one script is ambiguous.
        if (setters.Select(item => item.Local).Distinct().Count() != 1) return null;
        var (setter, setOffset) = setters[0];
        var startedBy = starters.Where(item => int.Parse(item[(item.LastIndexOf(':') + 1)..], CultureInfo.InvariantCulture) == setter)
            .Select(item => item[..item.LastIndexOf(':')]).ToList();
        // The room's objects start answers too (a click on an answer is an object's verb code).
        try
        {
            var data = _data.ReadRoom(room);
            var verbTables = _data.ReadObjectVerbs(room, data.Objects);
            foreach (var record in data.Objects)
                foreach (var verb in verbTables.GetValueOrDefault(record.Id) ?? [])
                {
                    var code = _data.ReadObjectVerb(room, record, verb.VerbId, out _);
                    if (code.Instructions.Any(item => item.Operation == "startScript" && item.Operands.StartsWith($"0x{setter:X2}", StringComparison.Ordinal)))
                        startedBy.Add($"object {record.Id} verb 0x{verb.VerbId:X2}");
                }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        return startedBy.Count == 0 ? null : (setter, setOffset, startedBy);
    }

    // A move that sets the awaited bit or variable to a nonzero literal.
    private static bool IsSettingNonZero(ScummScriptInstruction item, string target, bool isBit)
    {
        if (item.Operation != "move" || !item.Operands.StartsWith($"{target} = 0x", StringComparison.Ordinal)) return false;
        var literal = int.Parse(item.Operands[(target.Length + 3 + 2)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return isBit ? literal == 1 : literal != 0;
    }

    /// <summary><c>VAR_SOUNDCARD</c>: 0 in a session by default, 3 for AdLib in ScummVM and 4 for the Roland upgrade, whose sounds (33 to 64) the scripts then choose.</summary>
    public const string SoundCardVariable = ScummVars.SoundCard;

    /// <summary>The sound card the game's scripts see. Changing it takes effect at the scripts' next read, as it would in the game.</summary>
    /// <summary>The shadow palette of the room on screen: slot i is shown as colour <c>ShadowPalette[i]</c> (identity unless a script faded something).</summary>
    public IReadOnlyList<byte> ShadowPalette => _runtime.ShadowPalette;

    public int SoundCard
    {
        get => _runtime.GlobalVariables.GetValueOrDefault(SoundCardVariable);
        set => _runtime.SetGlobalVariable(SoundCardVariable, (ushort)value);
    }

    /// <summary>ScummVM runs the game's boot script (indexed script 1) at start.</summary>
    public const int BootScript = 1;

    /// <summary>
    /// Runs the boot script from its start to its first pause, entering the room it loads. The session is booting (<see cref="BootRunning"/>)
    /// from then on; the caller continues the boot by frames or ends it with <see cref="FinishBoot"/>.
    /// </summary>
    protected void RunBootScript()
    {
        _booting = true;
        _dispatchScope++;
        try
        {
            RunIndexedScript(BootScript, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
    }
}


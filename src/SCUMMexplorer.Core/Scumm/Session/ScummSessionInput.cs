using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>ScummVM <c>VAR_VERB_SCRIPT</c> (v3: var 32): the script <c>runInputScript</c> runs for every click. The boot sets it to 5 (script 001 +0x016B).</summary>
    protected const string VerbScriptVariable = ScummVars.VerbScript;
    /// <summary>ScummVM <c>VAR_TMR_2</c> (v3: var 12), jiffies the engine adds every frame; script 006 resets it on a click and reads it to find a double click.</summary>
    protected const string ClickTimerVariable = ScummVars.Timer2;
    /// <summary>ScummVM <c>kSceneClickArea</c>: a click in the room picture.</summary>
    public const int SceneClickArea = 2;
    /// <summary>Assumed time since the previous click for a single click: one second (60 jiffies), past script 006's 40-jiffy double-click window.</summary>
    public const int SingleClickGap = 60;
    /// <summary>Assumed time between the two clicks of a double click: 10 jiffies, inside script 006's 40-jiffy window.</summary>
    public const int DoubleClickGap = 10;

    /// <summary>
    /// Sends a left click at a room point to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does for a click in the room: <c>runInputScript(2, 0, 0)</c>
    /// runs the script in <c>VAR_VERB_SCRIPT</c> (script 005) with those arguments,
    /// which chains a room click into the input loop (script 006, <c>local[0]</c> = 1).
    /// Script 006 then does what it does with the object under the pointer: verb
    /// 0x37, the ego's walk, and script 007's sentence. This is assumed input: the
    /// point, and the time since the previous click (<see cref="SingleClickGap"/>, or
    /// <see cref="DoubleClickGap"/> between the two clicks of a double click), which
    /// the engine's timer would give. The click is not sent while user input is off
    /// (<c>cursorCommand</c>), or when no input script is set. The pointer stays at
    /// the point afterwards. Walks end at once, as elsewhere; a <c>waitFor actor</c>
    /// makes the script go on on the next frame, so the sentence and anything it
    /// loads follow in later frames (<see cref="AdvanceFrame"/>).
    /// </summary>
    public ScummObjectActionResult Click(int x, int y, bool doubleClick = false)
    {
        BeforeInput();
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in it.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var input = $"A {(doubleClick ? "double click" : "click")} at ({x}, {y}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (_roomSize is not { } size || x < 0 || y < 0 || x >= size.Width || y >= size.Height)
            refused = "the point is outside the room picture";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM runInputScript({SceneClickArea}, 0, {InputMode}) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT, set by script 001 +0x016B); the click's point and timing are the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            if (doubleClick) SendSceneClick(verbScript, x, y, SingleClickGap, "the first click of a double click");
            SendSceneClick(verbScript, x, y, doubleClick ? DoubleClickGap : SingleClickGap,
                doubleClick ? "the second click of a double click" : "a single click");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>
    /// The third argument of <c>runInputScript</c>: ScummVM's <c>checkExecVerbs</c>
    /// passes the mouse button only for versions above 3 (<c>_game.version &gt; 3 ? code : 0</c>),
    /// so LOOM's input script always gets 0. Script 005 +0x0000 sets <c>local[2]</c> = 0 first anyway.
    /// </summary>
    public const int InputMode = 0;
    /// <summary>ScummVM <c>kVerbClickArea</c>: a click in the verb screen.</summary>
    public const int VerbClickArea = 1;
    /// <summary>ScummVM <c>VAR_MOUSE_X</c>/<c>VAR_MOUSE_Y</c> (v3: vars 44 and 45), the pointer's screen point; script 005 reads x for verb 0x31.</summary>
    protected const string MouseXVariable = ScummVars.ScreenMouseX;
    protected const string MouseYVariable = ScummVars.ScreenMouseY;


    /// <summary>
    /// Sends a left click at a screen point in the verb area (rows
    /// <see cref="ScummVerbArea.Top"/>–199) to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does: <c>findVerbAtPos</c> over the verb slots
    /// (<see cref="ScummVerbArea.FindVerbAt"/>), then <c>runInputScript(1, verb, 0)</c>
    /// on a verb that is on, or <c>runInputScript(1, 0, 0)</c> elsewhere in the verb
    /// screen. Script 005 (area 1) turns a distaff note into script 010, verb 0x35
    /// into script 009 and the close-up (0x36) into script 007. This is assumed
    /// input: the point, which the session puts in <c>VAR_MOUSE_X</c>/<c>Y</c>
    /// (<c>var[0x02C]</c>/<c>var[0x02D]</c>), is the explorer's. The pointer is off the
    /// room picture then, so the room pointer is taken off. Refused while user input
    /// is off or when no input script is set, as <see cref="Click"/> is. Verbs the
    /// session cannot draw (unknown mode, image or position, text verbs) cannot be found.
    /// </summary>
    public ScummObjectActionResult ClickVerbArea(int x, int y)
    {
        BeforeInput();
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in the verb area.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var area = VerbArea();
        var verb = ScummVerbArea.FindVerbAt(area.Items, x, y);
        var input = verb is null
            ? $"A click at screen ({x}, {y}) in the verb area, on no verb that is on, sent to the game's input script as assumed input"
            : $"A click at screen ({x}, {y}) on verb 0x{verb.VerbId:X2} (slot {verb.Slot}) in the verb area, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (x < 0 || x >= ScummVerbArea.Width || y < ScummVerbArea.Top || y >= ScummVerbArea.Top + ScummVerbArea.Height)
            refused = "the point is outside the verb area";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        var verbId = verb?.VerbId ?? 0;
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: findVerbAtPos, then runInputScript({VerbClickArea}, 0x{verbId:X2}, {InputMode}) runs script {verbScript} " +
                           "(var[0x020], VAR_VERB_SCRIPT); the click's point is the explorer's, not the game's, and is put in var[0x02C]/var[0x02D] (VAR_MOUSE_X/Y).");
        if (verb?.Estimate is { } estimate)
            _uncertainties.Add($"Verb 0x{verbId:X2}'s slot follows an estimate: {estimate}.");
        if (area.Notes.Count > 0)
            _uncertainties.Add($"Verbs the session cannot draw cannot be found by the click: {string.Join("; ", area.Notes)}.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            // The pointer is over the verb screen, below the room picture (VAR_VIRT_MOUSE_Y past the room).
            _pointer = null;
            PlaceVirtualMouse();
            _runtime.SetGlobalVariable(MouseXVariable, (ushort)x);
            _runtime.SetGlobalVariable(MouseYVariable, (ushort)y);
            _runtime.EstimatedVariables.Remove(MouseXVariable);
            _runtime.EstimatedVariables.Remove(MouseYVariable);
            _visitTrace.Add($"click: verb area at screen ({x}, {y}), " + (verb is null ? "on no verb" : $"verb 0x{verbId:X2} (slot {verb.Slot})") +
                            $", runs script {verbScript} with [{VerbClickArea}, {verbId}, {InputMode}] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = VerbClickArea,
                ["local[0x001]"] = (ushort)verbId,
                ["local[0x002]"] = InputMode
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>ScummVM <c>kKeyClickArea</c>: a key press.</summary>
    public const int KeyClickArea = 4;
    /// <summary>ScummVM <c>VAR_RESTART_KEY</c> (v3: var 42); the boot sets it to 0x142, F8 (script 001 +0x0131).</summary>
    protected const string RestartKeyVariable = ScummVars.RestartKey;
    /// <summary>ScummVM <c>VAR_PAUSE_KEY</c> (v3: var 43); the boot sets it to 0x20, space (script 001 +0x0136).</summary>
    protected const string PauseKeyVariable = ScummVars.PauseKey;

    /// <summary>
    /// A key as ScummVM's <c>processKeyboard</c> hands it to scripts: printable
    /// characters as their ASCII code, Escape 0x1B, F1–F9 as 315–323 (0x13B–0x143),
    /// Ctrl+A–Z as 1–26.
    /// </summary>
    public static string DescribeKey(int key) => key switch
    {
        0x1B => "Escape",
        >= 0x13B and <= 0x143 => $"F{key - 0x13B + 1}",
        >= 1 and <= 26 => $"Ctrl+{(char)('A' + key - 1)}",
        0x20 => "space",
        > 0x20 and < 0x7F => $"'{(char)key}'",
        _ => $"key 0x{key:X2}"
    };

    /// <summary>
    /// Sends a key press to the game, as ScummVM's <c>processKeyboard</c> and
    /// <c>checkExecVerbs</c> do for v3: the restart key (<c>VAR_RESTART_KEY</c>) and
    /// the pause key (<c>VAR_PAUSE_KEY</c>) and '.' (talk stop) are the engine's and are not sent;
    /// Escape, while <c>VAR_CUTSCENEEXIT_KEY</c> is nonzero, first runs <c>abortCutscene</c>
    /// (the script that set the open cutscene's override continues at its skip jump
    /// on the next frame, with <c>VAR_OVERRIDE</c> = 1) and then goes on as that
    /// variable's key; otherwise, with user input on,
    /// <c>runInputScript(4, key, 1)</c> runs <c>VAR_VERB_SCRIPT</c> (script 005, area 4:
    /// note letters, Escape, text speed, F5–F7, Ctrl+V). A verb whose key matches would
    /// be clicked instead (<c>runInputScript(1, verb, 1)</c>), but no decoded
    /// <c>verbOps</c> in the release sets a key, so the session does not keep keys.
    /// The key is assumed input.
    /// </summary>
    public ScummObjectActionResult PressKey(int key)
    {
        BeforeInput();
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before pressing a key.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var name = DescribeKey(key);
        var input = $"The key {name} (0x{key:X2}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        bool Is(string variable) => _runtime.GlobalVariables.TryGetValue(variable, out var value) && value != 0 && value == key;
        string? refused = null;
        ushort verbScript = 0;
        // processKeyboard: Escape, while VAR_CUTSCENEEXIT_KEY is nonzero, runs
        // abortCutscene, then passes VAR_CUTSCENEEXIT_KEY's value on as the key.
        string? aborted = null;
        if (key == 0x1B && _runtime.GlobalVariables.TryGetValue(CutsceneExitKeyVariable, out var exitKey) && exitKey != 0)
        {
            aborted = AbortCutscene();
            _uncertainties.Add($"{input}: ScummVM processKeyboard runs abortCutscene first ({CutsceneExitKeyVariable}, VAR_CUTSCENEEXIT_KEY, is 0x{exitKey:X2}): {aborted}.");
            key = exitKey;
        }
        if (key <= 0 || key > 0x1FF)
            refused = "it is not a key code the engine sends";
        else if (Is(RestartKeyVariable))
            refused = $"it is the restart key ({RestartKeyVariable}, VAR_RESTART_KEY): the engine restarts the game, which the session does not model";
        else if (Is(PauseKeyVariable))
            refused = $"it is the pause key ({PauseKeyVariable}, VAR_PAUSE_KEY): the engine pauses the game and the scripts never see it";
        else if (key == '.')
            refused = "'.' is the engine's talk-stop key (v3 has no VAR_TALKSTOP_KEY, so it is always on): it ends the current message and the scripts never see it";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no key then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            // After abortCutscene the key was acted on, even when no script reads it.
            _uncertainties.Add(aborted is null
                ? $"{input}: not sent, because {refused}."
                : $"{input}: the key did not reach the input script after abortCutscene, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: runInputScript({KeyClickArea}, 0x{key:X2}, 1) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT); the key is the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            _visitTrace.Add($"key: {name} runs script {verbScript} with [{KeyClickArea}, 0x{key:X2}, 1] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = KeyClickArea,
                ["local[0x001]"] = (ushort)key,
                ["local[0x002]"] = 1
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    // After an input script ran: dispatch the loads it requested, or visit the room a reached load names.
    protected ScummObjectActionResult FinishInput(int beforeUncertainties, int beforeEvents)
    {
        var action = LastAction! with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        LastAction = action;
        if (DispatchRequestedLoads("during the click"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        var target = roomLoad?.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is not null)
        {
            action = action with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
            VisitReachedRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    protected void SendSceneClick(int verbScript, int x, int y, int gap, string which)
    {
        _pointer = (x, y);
        _pointerNote = "where the explorer clicked; it stays there after the click";
        PlaceVirtualMouse();
        _runtime.VirtualMouseAssumption = $"assumed input: {which} at ({x}, {y}), sent by the explorer";
        _runtime.SetGlobalVariable(ClickTimerVariable, (ushort)gap);
        _runtime.EstimatedVariables.Remove(ClickTimerVariable);
        _uncertainties.Add($"Assumed input timing for {which}: var[0x00C] (VAR_TMR_2, jiffies since script 006 last reset it) = {gap}; the frame loop counts it on from there, but the player's time before the click is not known.");
        _visitTrace.Add($"click: {which} at ({x}, {y}) runs script {verbScript} with [{SceneClickArea}, 0, {InputMode}] (runInputScript)");
        RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
        {
            ["local[0x000]"] = SceneClickArea,
            ["local[0x001]"] = 0,
            ["local[0x002]"] = InputMode
        }, 0);
    }


    public ScummObjectActionResult ActivateObjectVerb(int objectId, int verbId)
    {
        BeforeInput();
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before activating an object verb.");
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId) ??
                     throw new InvalidDataException($"Object {objectId} has no code record in Room {sourceRoomId:00}.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _uncertainties.Add($"Object {objectId} verb 0x{verbId:X2} was activated directly; input dispatch, actor walk, and class-based hit selection were not simulated.");
        if (Snapshot.IsUntouchable(objectId))
            _uncertainties.Add($"Object {objectId} is untouchable (class {ScummObjectClasses.Untouchable}) in this preview, so the game's findObject would not select it; its verb ran only as an exploration action.");
        ScummScriptDisassembly? script = null;
        ScummBoundedScriptResult? run = null;
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            script = _data.ReadObjectVerb(_currentRoom.ResourceId!.Value, record, verbId, out _);
            run = RunScript(script, _currentObjects, 0, null, 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {sourceRoomId:00} Object {objectId} verb 0x{verbId:X2}: {ex.Message}");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        LastAction = new ScummObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, null, null,
            _uncertainties.Skip(beforeUncertainties).ToArray());
        // A global script the verb started may have loaded a room and carried on.
        if (DispatchRequestedLoads("during the action"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        var targetId = roomLoad?.TargetId;
        var target = targetId is int id
            ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is null)
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
        var action = new ScummObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, roomLoad,
            target?.ResourceId, _uncertainties.Skip(beforeUncertainties).ToArray());
        if (target is not null)
        {
            VisitReachedRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    /// <summary>
    /// Starts the current room's local script <paramref name="scriptId"/> at instruction offset
    /// <paramref name="offset"/>, as a bonus preview of a scene whose trigger the game's own flow
    /// has no decoded way to meet (for example a watcher that needs the ego to step from one walk
    /// box straight into another). It is an exploration action like <see cref="ActivateObjectVerb"/>:
    /// the watcher's test is skipped, everything after the offset is the game's own code, and
    /// any running copy of the script is ended first, as <c>runScript</c> does.
    /// </summary>
    public ScummObjectActionResult RunLocalScriptAt(int scriptId, int offset)
    {
        BeforeInput();
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before starting one of its scripts.");
        if (scriptId < _globalScriptCount)
            throw new ArgumentOutOfRangeException(nameof(scriptId), "Only a room-local script can be started at an offset.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var input = $"Room {sourceRoomId:00} local script {scriptId} was started directly at +0x{offset:X4}";
        _uncertainties.Add($"{input}: an exploration action, not the game's flow. The condition that guards this offset was not met or tested; input dispatch and the ego's walk were not simulated.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try { RunStartedScript(scriptId, offset, new Dictionary<string, ushort>(), 0); }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>Called first by every input the game receives (a click, a key, a direct verb); the derived session ends any explorer-side browsing there.</summary>
    protected virtual void BeforeInput() { }
}


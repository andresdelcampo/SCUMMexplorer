using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One input the player gave, as the engine hands it to the input script (<c>runInputScript(area, value, mode)</c>), and why.</summary>
public sealed record ScummPlayInput(int Area, int Value, int Mode, string Description);

/// <summary>
/// Play in a verb-based game (REQUIREMENTS §7.1, Proposed 15 October 2026): the player's pointer, clicks and keys reach the game as ScummVM's
/// <c>scummLoop</c> and <c>checkExecVerbs</c> deliver them (engines/scumm verbs.cpp, input.cpp, read 15 October 2026), and the verb area is drawn from the
/// game's own verbs. One press is one click, delivered after the frame's scripts and before the sentence script (<c>runAllScripts</c>,
/// <c>checkExecVerbs</c>, <c>checkAndRunSentenceScript</c>). The app never chooses a verb, an object or a sentence.
/// </summary>
public abstract partial class ScummSession
{
    // The player's pointer in screen coordinates (0..319, 0..199), or null when the pointer is off the game screen.
    protected (int X, int Y)? _screenPointer;
    private readonly Queue<ScummPlayInput> _queuedInput = new();
    private readonly List<ScummPlayInput> _deliveredInput = [];
    private readonly Dictionary<int, ScummV3Charset?> _charsets = [];
    private readonly Dictionary<int, IReadOnlyDictionary<int, byte[]>> _inventoryNameRooms = [];

    /// <summary>The inputs delivered to the input script so far (Advanced trace, R-7.17).</summary>
    public IReadOnlyList<ScummPlayInput> DeliveredInput => _deliveredInput;

    /// <summary>The room screen's rows (<c>initScreens</c>): the text screen is above, the verb screen from <c>Bottom</c> down.</summary>
    public (int Top, int Bottom) MainScreen => _runtime.MainScreen;

    /// <summary>ScummVM <c>_cursor.state &gt; 0</c>: the game shows its cursor.</summary>
    public bool CursorShown => _runtime.CursorState > 0;

    /// <summary>ScummVM <c>_userPut &gt; 0</c>: the game reads clicks and keys.</summary>
    public bool UserInputOn => _runtime.UserPut > 0;

    /// <summary>The pointer in screen coordinates the frames read, or null.</summary>
    public (int X, int Y)? ScreenPointer => _screenPointer;

    /// <summary>The left edge of the room shown on screen (ScummVM <c>virtscr[0].xstart</c> = <c>_screenStartStrip</c> × 8, from the camera).</summary>
    public int ViewLeft
    {
        get
        {
            var width = _roomSize?.Width ?? 320;
            var camera = CameraX ?? 160;
            return Math.Clamp((camera / 8 - 20) * 8, 0, Math.Max(0, width - 320));
        }
    }

    /// <summary>A screen point as the room point the engine's virtual mouse gives (<c>_mouse + (xstart, -topline)</c>).</summary>
    public (int X, int Y) VirtualPoint(int screenX, int screenY) => (screenX + ViewLeft, screenY - _runtime.MainScreen.Top);

    /// <summary>
    /// The player's pointer over the game screen (screen coordinates), or null when it left it. From the next frame <c>VAR_MOUSE_X/Y</c> and
    /// <c>VAR_VIRT_MOUSE_X/Y</c> read it (scripts such as Indy3's "What is" loop and its object finder read them every frame).
    /// </summary>
    public void PointAtScreen((int X, int Y)? point)
    {
        _screenPointer = point is { } p && p.X is >= 0 and < 320 && p.Y is >= 0 and < 200 ? p : null;
        PlaceVirtualMouse();
    }

    /// <summary>The charset a text verb or message is drawn with (the game's charset files, v3 only), cached; null when it cannot be read.</summary>
    public ScummV3Charset? PlayCharset(int? number = null)
    {
        var chosen = number ?? _runtime.Charset ?? 0;
        if (!_charsets.TryGetValue(chosen, out var charset))
            _charsets[chosen] = charset = _game.Version == ScummVersion.V3 ? ScummV3Charset.TryLoad(_game.FolderPath, chosen, out _) : null;
        return charset;
    }

    /// <summary>A verb's name as the engine draws it: <c>convertMessageToString</c> with variables, object names (code 6) and verb names (code 5).</summary>
    public IReadOnlyList<byte> VerbGlyphs(ScummVerbSnapshot verb)
    {
        ArgumentNullException.ThrowIfNull(verb);
        if (verb.Name is null) return [];
        var raw = ScummTextDecoder.ParseEscaped(verb.Name);
        return ScummTextDecoder.ConvertMessage(raw, _runtime.GlobalVariables, _runtime.ObjectName, PlayVerbName, out _);
    }

    // convertVerbMessage: the verb in use (not saved, a text verb) with that number.
    protected byte[]? PlayVerbName(int verbId) =>
        _runtime.Verbs.Snapshot().FirstOrDefault(item => item.VerbId == verbId && item.ImageObject is null && item.Name is not null) is { } verb
            ? ScummTextDecoder.ParseEscaped(verb.Name!) : null;

    /// <summary>
    /// getObjOrActorName for an object the ego holds: the inventory keeps the object's code block (its name) from the room it was taken in.
    /// </summary>
    protected byte[]? InventoryObjectName(int objectId)
    {
        if (!_runtime.InventoryRooms.TryGetValue(objectId, out var room)) return null;
        if (!_inventoryNameRooms.TryGetValue(room, out var names))
        {
            try { names = _data.ReadRoom(room).ObjectNames; }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { names = new Dictionary<int, byte[]>(); }
            _inventoryNameRooms[room] = names;
        }
        return names.TryGetValue(objectId, out var name) ? name : null;
    }

    /// <summary>
    /// The verb area as the game draws it for play (R-7.8): image verbs as <see cref="VerbArea"/> does, text verbs with the charset and their colours,
    /// the verb under the pointer in its highlight colour while the cursor shows (<c>verbMouseOver</c>). Null charset: text verbs are not drawn.
    /// </summary>
    public ScummVerbAreaPicture PlayVerbArea()
    {
        var charset = PlayCharset(0);
        var verbs = _runtime.Verbs.Snapshot().Where(v => !HidesVerb(v)).ToArray();
        if (charset is null) return VerbArea();
        var plain = ScummVerbArea.Compose(verbs, VerbImage, _runtime.Verbs.Erases(), AlignVerbRows, new ScummTextVerbDrawing(charset, VerbGlyphs, null));
        if (!CursorShown || _screenPointer is not { } pointer || pointer.Y < ScummVerbArea.Top) return plain;
        var over = ScummVerbArea.FindVerbAt(plain.Items, pointer.X, pointer.Y);
        return over is null ? plain
            : ScummVerbArea.Compose(verbs, VerbImage, _runtime.Verbs.Erases(), AlignVerbRows, new ScummTextVerbDrawing(charset, VerbGlyphs, over.Slot));
    }

    /// <summary>
    /// One press of the left button at a screen point (R-7.10), queued for the next frame's <c>checkExecVerbs</c>: on a verb that is on,
    /// <c>runInputScript(1, verb, mode)</c>; elsewhere outside the room screen <c>(1, 0, mode)</c>; in the room screen <c>(2, 0, mode)</c>. The pointer moves
    /// there first. Returns the queued input, or null with the reason when the game reads no click now (user input off, no input script).
    /// </summary>
    public ScummPlayInput? ClickScreen(int x, int y, out string? refused)
    {
        PointAtScreen((x, y));
        refused = InputRefusal();
        if (refused is not null) return null;
        var mode = ScummProfile.For(_game).ClickInputMode;
        ScummPlayInput input;
        if (y >= _runtime.MainScreen.Top && y < _runtime.MainScreen.Bottom)
        {
            var (roomX, roomY) = VirtualPoint(x, y);
            input = new(SceneClickArea, 0, mode, $"click in the room at screen ({x}, {y}), room ({roomX}, {roomY})");
        }
        else
        {
            var verb = ScummVerbArea.FindVerbAt(PlayVerbArea().Items, x, y);
            input = verb is null
                ? new(VerbClickArea, 0, mode, $"click at screen ({x}, {y}) outside the room, on no verb")
                : new(VerbClickArea, verb.VerbId, mode, $"click on verb 0x{verb.VerbId:X2} at screen ({x}, {y})");
        }
        _queuedInput.Enqueue(input);
        return input;
    }

    /// <summary>
    /// A key the player pressed (R-7.11), queued for the next frame. The engine's own keys are the app's controls and are not queued here: the pause key,
    /// '.', Esc (see <see cref="PressKey"/>), the restart key and the profile's keys that leave the session (save/load). A key that is the shortcut of a
    /// verb that is on is that verb's click <c>(1, verb, 1)</c>; any other key goes as <c>(4, key, 1)</c>.
    /// </summary>
    public ScummPlayInput? PressPlayKey(int key, out string? refused)
    {
        bool Is(string variable) => _runtime.GlobalVariables.TryGetValue(variable, out var value) && value != 0 && value == key;
        refused = key <= 0 || key > 0x1FF ? "it is not a key code the engine sends"
            : Is(RestartKeyVariable) ? "it is the engine's restart key (Restart is the app's)"
            : Is(PauseKeyVariable) ? "it is the pause key (the app's Pause)"
            : key is '.' or 0x1B ? "it is an engine key (the app's Skip line or Skip)"
            : ScummProfile.For(_game).UndeliveredKeys.Contains(key) ? "the game would save or load with it, and the explorer's state is the session's"
            : InputRefusal();
        if (refused is not null) return null;
        var verb = _runtime.Verbs.Snapshot().FirstOrDefault(item => item.Mode == 1 && item.Key != 0 && item.Key == key);
        var input = verb is not null
            ? new ScummPlayInput(VerbClickArea, verb.VerbId, 1, $"key {DescribeKey(key)}, the shortcut of verb 0x{verb.VerbId:X2}")
            : new ScummPlayInput(KeyClickArea, key, 1, $"key {DescribeKey(key)}");
        _queuedInput.Enqueue(input);
        return input;
    }

    private string? InputRefusal() =>
        _currentRoom is null ? "no room is on screen"
        : _runtime.UserPut <= 0 ? "user input is off (cursorCommand)"
        : !_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out var script) || script == 0 ? "no input script is set (VAR_VERB_SCRIPT is 0)"
        : null;

    // checkExecVerbs: each queued input runs the script VAR_VERB_SCRIPT holds now, with (area, value, mode) as its first locals.
    private void DeliverQueuedInput()
    {
        while (_queuedInput.Count > 0)
        {
            var input = _queuedInput.Dequeue();
            if (InputRefusal() is { } refused)
            {
                _visitTrace.Add($"input: {input.Description} not delivered: {refused}");
                continue;
            }
            var script = _runtime.GlobalVariables[VerbScriptVariable];
            _deliveredInput.Add(input);
            _visitTrace.Add($"input: {input.Description} runs script {script} with [{input.Area}, {input.Value}, {input.Mode}] (runInputScript)");
            RunStartedScript(script, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = (ushort)input.Area,
                ["local[0x001]"] = (ushort)input.Value,
                ["local[0x002]"] = (ushort)input.Mode
            }, 0);
            DispatchRequestedLoads("after the player's input");
        }
    }

    /// <summary>'.' (the engine's talk-stop key, R-7.11): the current message ends at once.</summary>
    public void SkipMessage() => _runtime.Messages.SkipTalkDelay();
}

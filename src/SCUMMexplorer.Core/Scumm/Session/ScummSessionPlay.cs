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
    private readonly Dictionary<int, IScummTextCharset?> _charsets = [];
    private readonly Dictionary<int, IReadOnlyDictionary<int, byte[]>> _inventoryNameRooms = [];

    /// <summary>The inputs delivered to the input script so far (Advanced trace, R-7.17).</summary>
    public IReadOnlyList<ScummPlayInput> DeliveredInput => _deliveredInput;
    public bool HasPendingPlayInput => _queuedInput.Count != 0;
    /// <summary>The native input script: fixed script 4 before v3, VAR_VERB_SCRIPT thereafter.</summary>
    public int PlayInputScript => _runtime.IsV2 ? 4 : _runtime.GlobalVariables.GetValueOrDefault(VerbScriptVariable);

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

    /// <summary>
    /// The charset a text verb or message is drawn with, cached; null when it cannot be read: the old-v3 charset files (<c>99 - n</c>.LFL), or from
    /// version 4 the charset blocks of the game's archive or disks (<c>CHAR</c>, <c>CharsetRendererClassic</c>).
    /// </summary>
    public IScummTextCharset? PlayCharset(int? number = null)
    {
        var chosen = number ?? _runtime.Charset ?? 0;
        if (!_charsets.TryGetValue(chosen, out var charset))
            // A version 3 engine reads 99.LFL - n (the 256-colour Indiana Jones too: one file per room, the same charset files as EGA; S6 Indy3Vga).
            _charsets[chosen] = charset = _runtime.IsV2 ? new ScummV2Charset((int)_runtime.EngineVersion, OldPlayLanguage,
                nativeFont: ScummV2NativeFont.TryLoad(_game.FolderPath) ?? ReadOnlyMemory<byte>.Empty)
                : ScummProfile.For(_game).EngineVersion == ScummVersion.V3 ? ScummV3Charset.TryLoad(_game.FolderPath, chosen, out _) : ReadArchiveCharset(chosen);
        // cursorCommand charsetColors (S6 MonkeyCd, 17 October 2026: Script 22 gives the verbs' charset 6 the entries 0, 6, 2): the colours the game set replace
        // the file's own map, as ScummVM's _charsetData does.
        if (charset is ScummV4Charset v4 && _runtime.CharsetColors.TryGetValue(chosen, out var entries) && entries.Count > 0)
        {
            var key = (chosen, string.Join(",", entries.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
            if (!_coloredCharsets.TryGetValue(key, out var colored)) _coloredCharsets[key] = colored = v4.WithColors(entries);
            return colored;
        }
        return charset;
    }

    private readonly Dictionary<(int Number, string Colors), ScummV4Charset> _coloredCharsets = [];

    private ScummV4Charset? ReadArchiveCharset(int number)
    {
        IEnumerable<ScummV4Archive> archives = _game.V4Archive is { } single ? [single] : _game.V4Disks?.Disks.OfType<ScummV4Archive>() ?? [];
        foreach (var archive in archives)
        {
            try { if (archive.ReadCharset(number) is { } charset) return charset; }
            catch (InvalidDataException) { }
        }
        // Version 5 (S6 MonkeyCd, 17 October 2026): the CHAR blocks the index's DCHR directory points at, the same font after their 8-byte block header.
        foreach (var archive in _game.V4Disks?.Disks.OfType<ScummV5Archive>() ?? [])
        {
            try { if (archive.ReadCharset(number) is { } charset) return charset; }
            catch (InvalidDataException) { }
        }
        return null;
    }

    /// <summary>The charset the play screen's text starts from: the one the game set (<c>initCharset</c>), else charset 0.</summary>
    public IScummTextCharset? CurrentPlayCharset() => PlayCharset(_runtime.Charset ?? 0) ?? PlayCharset(0);

    /// <summary>A verb's name as the engine draws it: <c>convertMessageToString</c> with variables, object names (code 6) and verb names (code 5).</summary>
    public IReadOnlyList<byte> VerbGlyphs(ScummVerbSnapshot verb)
    {
        ArgumentNullException.ThrowIfNull(verb);
        if (verb.Name is null) return [];
        var raw = ScummTextDecoder.ParseEscaped(verb.Name);
        return ScummTextDecoder.ConvertMessage(raw, _runtime.GlobalVariables, _runtime.ObjectName, PlayVerbName, PlayStringText, out _);
    }

    // convertStringMessage (code 7): the string resource stringOps loaded; version 3 reads the number from a variable first, versions 4 and 5 take it as it is
    // (ScummVM charset.cpp/string.cpp, master read 17 October 2026; Monkey Island CD's sentence line, Script 12, puts string 49 between verb and object).
    protected byte[]? PlayStringText(int operand)
    {
        var number = operand;
        if (ScummProfile.For(_game).EngineVersion == ScummVersion.V3)
        {
            if (!_runtime.GlobalVariables.TryGetValue($"var[0x{operand & 0x1FFF:X3}]", out var read)) return null;
            number = read;
        }
        return number != 0 && _runtime.Strings.TryGetValue(number & 0xFF, out var text) ? text : null;
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
        var charset = PlayCharset(0) ?? CurrentPlayCharset();
        var verbs = _runtime.Verbs.Snapshot().Where(v => !HidesVerb(v) && (!_runtime.IsV2 || (_runtime.V2UserState & 0x80) != 0)).ToArray();
        if (charset is null) return VerbArea();
        var plain = ScummVerbArea.Compose(verbs, VerbImage, _runtime.Verbs.Erases(), AlignVerbRows, new ScummTextVerbDrawing(charset, VerbGlyphs, null, n => PlayCharset(n)));
        if (!CursorShown || _screenPointer is not { } pointer || pointer.Y < ScummVerbArea.Top) return DrawOldInterface(plain, charset);
        var over = ScummVerbArea.FindVerbAt(plain.Items, pointer.X, pointer.Y);
        return DrawOldInterface(over is null ? plain
            : ScummVerbArea.Compose(verbs, VerbImage, _runtime.Verbs.Erases(), AlignVerbRows, new ScummTextVerbDrawing(charset, VerbGlyphs, over.Slot, n => PlayCharset(n))), charset);
    }

    /// <summary>
    /// One press of the left button at a screen point (R-7.10), queued for the next frame's <c>checkExecVerbs</c>: on a verb that is on,
    /// <c>runInputScript(1, verb, mode)</c>; elsewhere outside the room screen <c>(1, 0, mode)</c>; in the room screen <c>(2, 0, mode)</c>. The pointer moves
    /// there first. Returns the queued input, or null with the reason when the game reads no click now (user input off, no input script).
    /// </summary>
    public ScummPlayInput? ClickScreen(int x, int y, out string? refused) => ClickScreen(x, y, false, out refused);

    /// <summary>
    /// One press of the left or right button (R-7.10, R-7.16). A right press reaches the game only where <see cref="ScummProfile.RightClickReachesGame"/>
    /// (from version 4: the same click with mode 2, ScummVM <c>checkExecVerbs</c>); elsewhere it is refused and the right button stays the app's.
    /// </summary>
    public ScummPlayInput? ClickScreen(int x, int y, bool rightButton, out string? refused)
    {
        var profile = ScummProfile.For(_game);
        if (rightButton && !profile.RightClickReachesGame)
        {
            refused = "the right button is the app's in this game (R-7.16): its scripts never see a right click";
            return null;
        }
        PointAtScreen((x, y));
        refused = InputRefusal();
        if (refused is not null) return null;
        var mode = rightButton ? profile.RightClickInputMode : _runtime.IsV2 ? 1 : profile.ClickInputMode;
        var press = rightButton ? "right click" : "click";
        ScummPlayInput input;
        if (_runtime.IsV2 && y >= _runtime.MainScreen.Bottom && y <= _runtime.MainScreen.Bottom + 8)
            input = new(5, 0, 0, "click on the native sentence line");
        else if (_runtime.IsV2 && y >= _runtime.MainScreen.Bottom + 32)
        {
            var relativeY = y - _runtime.MainScreen.Bottom - 32;
            if (relativeY >= 16) return null;
            if (x is >= 144 and < 176)
            {
                ScrollV2Inventory(relativeY < 8 ? -2 : 2);
                return null;
            }
            var slot = (relativeY / 8) * 2 + (x >= 176 ? 1 : 0);
            var obj = V2Inventory().Skip(_v2InventoryOffset + slot).FirstOrDefault();
            if (obj == 0) return null;
            input = new(3, obj, 0, $"click on inventory object {obj}");
        }
        else if (y >= _runtime.MainScreen.Top && y < _runtime.MainScreen.Bottom)
        {
            var (roomX, roomY) = VirtualPoint(x, y);
            input = new(SceneClickArea, 0, mode, $"{press} in the room at screen ({x}, {y}), room ({roomX}, {roomY})");
        }
        else
        {
            var verb = ScummVerbArea.FindVerbAt(PlayVerbArea().Items, x, y);
            input = verb is null
                ? new(VerbClickArea, 0, mode, $"{press} at screen ({x}, {y}) outside the room, on no verb")
                : new(VerbClickArea, verb.VerbId, mode, $"{press} on verb 0x{verb.VerbId:X2} at screen ({x}, {y})");
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
            : _runtime.IsV2 && key is 319 or 322 or 32 ? "it is an engine save/load, restart or pause key (the app's controls)"
            : Is(RestartKeyVariable) ? "it is the engine's restart key (Restart is the app's)"
            : Is(PauseKeyVariable) ? "it is the pause key (the app's Pause)"
            : key is '.' or 0x1B ? "it is an engine key (the app's Skip line or Skip)"
            : ScummProfile.For(_game).MainMenuKeyVariable is { } menu && Is(menu) ? "it is the engine's save/load menu key (VAR_MAINMENU_KEY), and the explorer's state is the session's"
            : ScummProfile.For(_game).UndeliveredKeys.Contains(key) ? "the game would save or load with it, and the explorer's state is the session's"
            : InputRefusal();
        if (refused is not null) return null;
        var verb = _runtime.Verbs.Snapshot().FirstOrDefault(item => item.Mode == 1 && item.Key != 0 && item.Key == key);
        if (_runtime.IsV2 && verb is null)
        {
            if (key is 'u' or 'j') { ScrollV2Inventory(key == 'u' ? -2 : 2); return null; }
            var slot = key switch { 'i' => 0, 'o' => 1, 'k' => 2, 'l' => 3, _ => -1 };
            if (slot >= 0)
            {
                var obj = V2Inventory().Skip(_v2InventoryOffset + slot).FirstOrDefault();
                if (obj == 0) return null;
                var inventoryInput = new ScummPlayInput(3, obj, 0, $"inventory key {DescribeKey(key)}, object {obj}");
                _queuedInput.Enqueue(inventoryInput);
                return inventoryInput;
            }
        }
        var input = verb is not null
            ? new ScummPlayInput(VerbClickArea, verb.VerbId, 1, $"key {DescribeKey(key)}, the shortcut of verb 0x{verb.VerbId:X2}")
            : new ScummPlayInput(KeyClickArea, key, 1, $"key {DescribeKey(key)}");
        _queuedInput.Enqueue(input);
        return input;
    }

    private string? InputRefusal() =>
        _currentRoom is null ? "no room is on screen"
        : _runtime.UserPut <= 0 ? "user input is off (cursorCommand)"
        : !_runtime.IsV2 && (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out var script) || script == 0) ? "no input script is set (VAR_VERB_SCRIPT is 0)"
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
            var script = _runtime.IsV2 ? 4 : _runtime.GlobalVariables[VerbScriptVariable];
            if (_runtime.IsV2)
            {
                _runtime.SetGlobalVariable(ScummVars.V2.ClickArea, (ushort)input.Area);
                if (input.Area == VerbClickArea) _runtime.SetGlobalVariable(ScummVars.V2.ClickVerb, (ushort)input.Value);
                if (input.Area == 3) _runtime.SetGlobalVariable(ScummVars.V2.ClickObject, (ushort)input.Value);
                // processKeyboard converts DOS F1..F9 to the old engine's 1..9 key values.
                if (input.Area == KeyClickArea) _runtime.SetGlobalVariable(ScummVars.V2.Keypress, (ushort)(input.Value is >= 315 and <= 323 ? input.Value - 314 : input.Value));
            }
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

    private int _v2InventoryOffset;
    private int _v2InventoryEgo = -1;
    private (int Id, byte Owner)[] _v2InventorySeen = [];
    private int[] V2Inventory()
    {
        var ego = _runtime.GlobalVariables.GetValueOrDefault(EgoVariable);
        var contents = _runtime.InventorySlots.Where(id => id != 0).Select(id => (Id: id, Owner: _runtime.ObjectOwners.GetValueOrDefault(id))).ToArray();
        // handleMouseOver(updateInventory): native inventory updates reset scrolling,
        // including actor switches and owner/slot changes, before drawing or picking.
        if (_v2InventoryEgo != ego || !_v2InventorySeen.SequenceEqual(contents))
        {
            _v2InventoryOffset = 0;
            _v2InventoryEgo = ego;
            _v2InventorySeen = contents;
        }
        return contents.Where(item => item.Owner == ego).Select(item => item.Id).ToArray();
    }
    private void ScrollV2Inventory(int delta)
    {
        var count = V2Inventory().Length;
        if (delta < 0 && _v2InventoryOffset >= 2 || delta > 0 && _v2InventoryOffset + 4 < count)
            _v2InventoryOffset += delta;
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A transactional, offscreen exploration preview; never an earned doorway transition.</summary>
public sealed record ScummManualRoomResult(ScummSession? Session, string? Reason, int Frames = 0,
    ScummRoomCapture? State = null, bool Cancelled = false, ScummAmbientScripts? Ambient = null)
{
    public bool Succeeded => Session is not null;
}

public abstract partial class ScummSession
{
    /// <summary>The frames looked ahead on a copy before a rested room is handed over (ten seconds).</summary>
    private const int LookAheadFrames = 600;

    private HashSet<ScriptFrame>? _manualEntryRoots;
    private bool _capturingManualEntryRoots;

    /// <summary>
    /// Clones this concrete session, retires the departed scene's slots, then runs the selected
    /// room's native entry offscreen. Only a settled, controllable requested room is returned.
    /// The live session is never modified; the caller atomically adopts a successful result.
    /// No input, story flag, override jump or script instruction is fabricated or skipped.
    /// </summary>
    public ScummManualRoomResult PrepareManualRoom(GameResource room, int frameLimit = 2400,
        int quietFrames = 90, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId ||
            !_game.Rooms.Any(r => r.ResourceId == roomId)) throw new ArgumentException("Choose an indexed room of this game.", nameof(room));
        if (frameLimit < 1 || quietFrames < 1) throw new ArgumentOutOfRangeException(nameof(frameLimit));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shared = ObjectGraphCopy.Reachable(_game);
            // A room's own watcher can take the hero away at once where the assumed arrival stands him (Indy 3 Room 32: a guard patrols through the door he came
            // by): the game's other ways in are tried before the room is given up as a still picture (6 November 2026, user: the castle plans live).
            var first = PrepareManualRoomFrom(room, roomId, null, shared, frameLimit, quietFrames, cancellationToken, out var takenAway);
            if (first.Succeeded || !takenAway) return first;
            foreach (var door in OtherArrivalObjects(roomId))
            {
                var other = PrepareManualRoomFrom(room, roomId, door, shared, frameLimit, quietFrames, cancellationToken, out _);
                if (other.Succeeded) return other;
            }
            return first;
        }
        catch (OperationCanceledException) { return new(null, "Manual room preparation was canceled.", Cancelled: true); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        { return new(null, $"Native room preparation is unavailable: {ex.Message}"); }
    }

    /// <summary>The objects of the room's other decoded arrivals, for a retry of <see cref="PrepareManualRoom"/>; a game's session supplies them.</summary>
    protected virtual IEnumerable<int> OtherArrivalObjects(int roomId) => [];

    private ScummManualRoomResult PrepareManualRoomFrom(GameResource room, int roomId, int? door, HashSet<object> shared, int frameLimit, int quietFrames,
        CancellationToken cancellationToken, out bool takenAway)
    {
        takenAway = false;
            var copy = ObjectGraphCopy.Copy(this, shared);
            cancellationToken.ThrowIfCancellationRequested();
            // Existing source-derived party bindings distinguish filling the selection slots
            // from pressing native Start. Catalog preparation never chooses a party for the user.
            if (copy._runtime.IsV2)
            {
                copy.InventoryCharacters();
                if (copy._inventoryPartyBindings is { Length: > 0 } && !copy._inventoryPartyEstablished)
                    return new(null, "The native character-selection choice has not been completed.");
            }
            copy.ManualNavigationBegan();
            copy.CancelDepartedManualRoute(roomId, cancellationToken);
            copy.TakeControlScreen();
            if (door is int by) copy.WantedArrivalObject = by;
            copy._manualEntryRoots = [];
            copy._capturingManualEntryRoots = true;
            try { copy.VisitManualRoomCore(room); }
            finally { copy._capturingManualEntryRoots = false; }
            cancellationToken.ThrowIfCancellationRequested();
            var possibleScenes = new Dictionary<(ScummScriptDisassembly, int), bool>();
            bool InitializationPending() => copy._pending.Any(p =>
                copy.DescendsFrom(p.Frame, copy._manualEntryRoots) &&
                (ReferenceEquals(p.Script, copy.LastEntryScript) ||
                 copy.RemainingSourceMayBeginScene(p.Script, p.Offset, possibleScenes, [], 0) && !RemainingSourceWatches(p.Script, p.Offset)));
            var settled = copy.SettleRoom(frameLimit, quietFrames, cancellationToken, InitializationPending, restlessActors: true);
            var initializationPending = InitializationPending();
            copy._manualEntryRoots = null;
            cancellationToken.ThrowIfCancellationRequested();
            takenAway = settled.Outcome == ScummSettleOutcome.RoomLeft || copy.InCutscene;
            if (settled.Outcome != ScummSettleOutcome.Settled || copy.CurrentRoomId != roomId ||
                copy.InCutscene || !copy.UserInputOn || initializationPending)
                return new(null, settled.Outcome == ScummSettleOutcome.RoomLeft
                    ? "Native entry left the selected room; no live preview was adopted."
                    : copy.InCutscene ? "Native entry still has an active cutscene within the preparation bound."
                    : !copy.UserInputOn ? "Native entry has not enabled player input within the preparation bound."
                    : initializationPending ? "Native entry still has pending scene initialization within the preparation bound."
                    : "Native entry did not reach a quiet, controllable room within the bound.", settled.Frames, settled.State);
            // A room that rests only for a moment and then goes on by itself (Indy 3 Room 70, the plane: three frames on it crashes into Room 71, a scene of 16 lines)
            // is not a room to hand over: a look ahead on a copy (6 November 2026, user: "we should not play full room entry cutscenes on entry").
            var ahead = ObjectGraphCopy.Copy(copy, shared);
            for (var frame = 1; frame <= LookAheadFrames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ahead.StepFrame();
                takenAway = ahead.CurrentRoomId != roomId || ahead.InCutscene;
                if (takenAway)
                    return new(null, ahead.CurrentRoomId != roomId ? $"Native entry leaves the selected room by itself {frame} frames after it rests; no live preview was adopted."
                        : $"Native entry begins a cutscene by itself {frame} frames after it rests; no live preview was adopted.", settled.Frames, settled.State);
            }
            copy.TakeBootShownState(roomId);
            copy._uncertainties.Add($"Room {roomId:00} was selected manually: the previous scene was canceled, " +
                "and native entry ran offscreen on an independent copy. This is an exploration preview, not a played route.");
            return new(copy, null, settled.Frames, copy.CaptureSessionRoom());
    }

    /// <summary>
    /// R-8.5b static preview with its ambient scripts (rule confirmed by the user on 20 October 2026): for a manually picked room whose native entry
    /// cannot be prepared live (<see cref="PrepareManualRoom"/> failed), an independent copy enters the room as <c>startScene</c> loads it but runs
    /// none of its entry code (no global entry hook, no room entry script), ends every other script slot, and starts only the scripts the entry
    /// would start that <see cref="ScummAmbientScripts"/> finds ambient, with the entry's literal arguments. Its frames then move the room's
    /// permanent animation and nothing else: no story, no print, no room change, no input. The ego is not placed (no arrival is assumed). The
    /// session is a state preview, never a reached outcome; the live session is not modified. Returns no session when the room has no ambient script,
    /// unless <paramref name="requireAmbient"/> is false (a still room whose objects an explicit Interact action can then act on).
    /// </summary>
    /// <param name="standing">The actors of the room's first-visit picture (<see cref="ScummRoomScene"/>): they stand where the picture showed them, so the
    /// animation does not take them away (6 November 2026, user report: Indy 3 Room 15 lost Indy when its fountains began to play).</param>
    public ScummManualRoomResult PrepareAmbientRoom(GameResource room, CancellationToken cancellationToken = default, bool requireAmbient = true,
        IReadOnlyList<ScummActorSnapshot>? standing = null)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId ||
            !_game.Rooms.Any(r => r.ResourceId == roomId)) throw new ArgumentException("Choose an indexed room of this game.", nameof(room));
        try
        {
            var ambient = ScummAmbientScripts.Classify(_data, roomId);
            if (ambient.Ambient.Count == 0 && requireAmbient) return new(null, "The room's entry starts no ambient script.", Ambient: ambient);
            cancellationToken.ThrowIfCancellationRequested();
            var copy = ObjectGraphCopy.Copy(this, ObjectGraphCopy.Reachable(_game));
            copy.ManualNavigationBegan();
            copy.CancelDepartedManualRoute(roomId, cancellationToken);
            copy.EnterRoom(room, $"Room {roomId:00} selected manually as a static preview: its entry code did not run (R-8.5b); only its ambient scripts run.",
                runEntry: false);
            // Only the ambient scripts run from here: every other slot (the previous room's exit work, the game's global loops) ends without
            // executing further; the room's own story stays unplayed.
            var stopped = copy.EndEverySlot();
            copy.TakeControlScreen();
            copy.TakeEntryScreen(roomId);
            copy.TakeBootShownState(roomId);
            var egoNumber = copy._runtime.GlobalVariables.TryGetValue(copy.EgoVariable, out var hero) ? (int?)hero : null;
            foreach (var actor in standing ?? [])
            {
                if (actor.Room != roomId || actor.X is null || actor.Y is null || !copy._runtime.Actors.Contains(actor.Number)) continue;
                if (copy._runtime.Actors[actor.Number].Look.Costume != actor.Look.Costume) copy._runtime.Actors.CostumeChanged(actor.Number, actor.Look.Costume, copy.CurrentPlacement());
                copy._runtime.Actors.Put(actor.Number, actor.X, actor.Y, roomId, copy.CurrentPlacement(), egoNumber);
            }
            foreach (var script in ambient.Ambient)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var locals = script.Arguments.Select((value, index) => (Key: $"local[0x{index:X3}]", Value: (ushort)value))
                    .ToDictionary(item => item.Key, item => item.Value);
                copy.RunStartedScript(script.ScriptId, 0, locals, 0);
            }
            copy._uncertainties.Add($"Room {roomId:00} is a static preview (R-8.5b): its entry code did not run; {stopped} other script slot(s) were ended; " +
                $"only its ambient script(s) {string.Join(", ", ambient.Ambient.Select(a => a.ScriptId))} run, started as the entry would start them " +
                $"(an exploration preview, not a played route; {(standing is null ? "the ego is not placed" : "its actors stand where the room's first-visit picture shows them")}).");
            return new(copy, null, 0, copy.CaptureSessionRoom(), Ambient: ambient);
        }
        catch (OperationCanceledException) { return new(null, "Ambient preview was canceled.", Cancelled: true); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        { return new(null, $"The ambient preview is unavailable: {ex.Message}"); }
    }

    // 2 November 2026 (user report, Indy 3 Rooms 69, 74, 75, 80): the screen rows are how the room is shown, not its story. A static preview runs no entry
    // code, yet a room whose entry gives it the whole screen (roomOps setScreen 0,200: diary pages, the zeppelin) must not be cut at the verb area and
    // show the verbs, so the entry's first setScreen with constant rows is taken (initScreens).
    private void TakeEntryScreen(int roomId)
    {
        ScummScriptDisassembly? entry;
        try { entry = _data.RoomEntryScript(roomId); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { return; }
        var setScreen = entry?.Instructions.FirstOrDefault(i => i.Operation == "roomOps" && i.Operands.StartsWith("setScreen,", StringComparison.Ordinal));
        if (setScreen is null) return;
        var parts = setScreen.Operands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 3 && TryConstant(parts[1], out var top) && TryConstant(parts[2], out var bottom) && top < bottom && bottom <= 200)
        {
            _runtime.MainScreen = (top, bottom);
            _visitTrace.Add($"static preview: the entry's {setScreen.Operands} gives the room screen rows {top} to {bottom - 1}");
        }
        static bool TryConstant(string text, out int value) =>
            text.StartsWith("0x", StringComparison.Ordinal) ? int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value) : int.TryParse(text, out value);
    }

    // A room only the game's opening shows (Indy 3's logo, Room 76: only the boot script loads it, before any verb exists) is seen with the screen rows
    // and the verbs it had then, not those the game has since: the verbs that were not on then are switched off (2 November 2026, user report). Rooms
    // the opening passes through that play also loads (Indy 3's college) keep the play state.
    private readonly Dictionary<int, ((int Top, int Bottom) Screen, HashSet<int> Verbs)> _bootShown = [];

    /// <summary>A first-visit picture of a room only the opening shows takes the opening's screen and verbs, as the app's pick does (Indy 3 Room 76).</summary>
    public void TakeOpeningState(int roomId) => TakeBootShownState(roomId);

    private void TakeBootShownState(int roomId)
    {
        if (!_bootShown.TryGetValue(roomId, out var shown) || !Facts().OpeningOnly(roomId)) return;
        _runtime.MainScreen = shown.Screen;
        var hidden = _runtime.Verbs.SwitchOffAllBut(shown.Verbs);
        _visitTrace.Add($"static preview: the game's opening showed Room {roomId:00} with screen rows {shown.Screen.Top} to {shown.Screen.Bottom - 1}" +
            $"{(hidden > 0 ? $" and without {hidden} verb(s) on now" : "")}");
    }

    /// <summary>The screen rows the game had when the boot handed over control (Indy 3: 16 to 143), or null before that.</summary>
    private (int Top, int Bottom)? _controlScreen;

    /// <summary>The costume the hero wore when the boot handed over control.</summary>
    private int? _controlHeroCostume;

    /// <summary>
    /// 3 November 2026 (user report: a picked room jumped in size a moment after it showed): a room picked without its route starts from the screen
    /// the game has in play, not the one the previous room left (Indy 3 Room 13 after the diary page, Room 74, kept its 0 to 199). The room's own
    /// entry may still set its own (Room 85: 8 to 199).
    /// </summary>
    /// <summary>A still preview shows the room with the screen its first visit reached (its scripts ran there; the still preview runs none).</summary>
    public void UseScreen((int Top, int Bottom) screen) => _runtime.MainScreen = screen;

    protected void TakeControlScreen()
    {
        if (_controlScreen is { } screen) _runtime.MainScreen = screen;
    }

    // Ends every script slot without executing anything further (no override jump, no end hook); returns how many there were.
    private int EndEverySlot()
    {
        var active = _pending.Select(p => p.Frame).Concat(_executing).Concat(_loadingFrames)
            .Concat(_frameScripts.Select(p => p.Frame)).Where(f => !f.Stopped).ToHashSet();
        foreach (var frame in active) frame.Stopped = true;
        _pending.Clear();
        _frameScripts.Clear();
        _loadingFrames.Clear();
        _requestedLoad = null;
        _chained = null;
        _actionLoadFrame = null;
        _queuedInput.Clear();
        _runtime.Sentences.Clear();
        _booting = false;
        if (active.Count > 0) _visitTrace.Add($"static preview: ended {active.Count} script slot(s) without executing them further");
        return active.Count;
    }

    protected virtual void ManualNavigationBegan() { }

    protected virtual ScummSessionSnapshot VisitManualRoomCore(GameResource room) =>
        EnterRoom(room, "Room selected manually; native entry ran offscreen (exploration preview).");

    /// <summary>
    /// 2 November 2026 (user report, Indy 3 Room 13): the boot script can go on after it hands the player control (Indy 3's Script 1 +0x040D: "I really
    /// have to get out of these wet clothes!", a walk, then loadRoom 73 to change). A room or scene the user picks does not continue the opening's route:
    /// its remaining work (and the scripts it started) ends. Indy 3's walk runs inside the boot's own cutscene (+0x023A to +0x04B0): that cutscene is
    /// closed too, as a picked room's preparation closes a departed route's (<see cref="CancelDepartedManualRoute"/>).
    /// </summary>
    protected void EndOpeningRoute()
    {
        if (_booting) return;
        var active = _pending.Select(p => p.Frame).Concat(_frameScripts.Select(p => p.Frame)).Where(f => !f.Stopped).ToHashSet();
        // The boot script's slot, also once it has chained on (Indy 3's Script 1 +0x04B1 chains to Script 119, the "Welcome to my game" tutorial lines).
        static bool Opening(ScriptFrame frame)
        {
            for (var current = frame; current is not null; current = current.ChainedFrom ? current.Parent : null)
                if (current.Number == BootScript) return true;
            return false;
        }
        var roots = active.Where(Opening).ToHashSet();
        if (roots.Count == 0) return;
        if (_cutscenes.Any(c => c.Owner is ScriptFrame owner && DescendsFrom(owner, roots)))
        {
            CancelDepartedManualRoute(_currentRoom?.ResourceId ?? 0, CancellationToken.None);
            _uncertainties.Add($"The game's opening (Script {BootScript}) was still in its cutscene after giving control; a picked room or scene does not continue it, so it and its cutscene were ended.");
            return;
        }
        foreach (var frame in active.Where(f => DescendsFrom(f, roots))) frame.Stopped = true;
        _pending.RemoveAll(p => p.Frame.Stopped);
        _frameScripts.RemoveAll(p => p.Frame.Stopped);
        _uncertainties.Add($"The game's opening (Script {BootScript}) had not finished its route after giving control; a picked room or scene does not continue it, so it was ended.");
    }

    private bool DescendsFrom(ScriptFrame frame, HashSet<ScriptFrame>? roots)
    {
        if (roots is null) return false;
        for (var current = frame; current is not null; current = current.Parent)
            if (roots.Contains(current)) return true;
        return false;
    }

    private void CancelDepartedManualRoute(int target, CancellationToken cancellationToken)
    {
        var active = _pending.Select(p => p.Frame).Concat(_executing).Concat(_loadingFrames)
            .Concat(_frameScripts.Select(p => p.Frame)).Where(f => !f.Stopped).ToHashSet();
        var roots = _cutscenes.Select(c => c.Owner).OfType<ScriptFrame>().ToHashSet();
        foreach (var owner in roots.ToArray())
        {
            var ancestor = owner;
            while (ancestor.Parent is { } parent && active.Contains(parent)) ancestor = parent;
            roots.Add(ancestor);
        }
        foreach (var frame in active)
            if (frame.OriginRoom == _currentRoom?.ResourceId && frame.OriginRoom is not null ||
                frame.Number == BootScript) roots.Add(frame);
        foreach (var frame in active.Where(f => DescendsFrom(f, roots))) frame.Stopped = true;
        _pending.RemoveAll(p => p.Frame.Stopped);
        _frameScripts.RemoveAll(p => p.Frame.Stopped);
        _loadingFrames.RemoveAll(f => f.Stopped);
        if (_requestedLoad is { Frame.Stopped: true }) _requestedLoad = null;
        if (_chained is { Frame.Stopped: true }) _chained = null;
        if (_actionLoadFrame?.Stopped == true) _actionLoadFrame = null;
        _queuedInput.Clear();
        _runtime.Sentences.Clear();
        _booting = false;
        var scopes = 0;
        while (_cutscenes.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++scopes > MaxNestedScripts) throw new InvalidOperationException("Cutscene cleanup did not finish within its scope bound.");
            CloseTopCutscene();
        }
        _overrides.Clear();
        _cutsceneOwner = null;
        _visitTrace.Add($"manual selection of Room {target:00}: retired departed scene slots without executing their remaining story or an override jump");
    }

    // Closes the innermost cutscene without its override jump: version 2 restores its saved interface; later versions run the game's cutscene-end hook.
    private void CloseTopCutscene()
    {
        _overrides.Remove(_cutscenes.Count);
        var closed = _cutscenes.Pop();
        if (_runtime.IsV2)
        {
            // Canceling an exploration route restores its saved UI scope, not its saved
            // room/camera route: the caller is explicitly selecting another room next.
            if (_v2CutsceneState.TryPop(out var saved))
            {
                _runtime.SetGlobalVariable(ScummVars.V2.CursorState, saved.Cursor);
                _runtime.ApplyV2UserState(saved.UserState | 4 | 2 | 1);
            }
        }
        else
        {
            _runtime.SetGlobalVariable(OverrideVariable, 0);
            _runtime.EstimatedVariables.Remove(OverrideVariable);
            if (!_runtime.GlobalVariables.TryGetValue(CutsceneEndScriptVariable, out var end))
                throw new InvalidOperationException("The native cutscene-end hook is unavailable.");
            if (end != 0) RunStartedScript(end, 0, new Dictionary<string, ushort> { ["local[0x000]"] = closed.Data }, 0);
        }
        if (_cutscenes.Count == 0) _cutsceneOwner = null;
    }

    /// <summary>
    /// 6 November 2026 (user: the castle plans live; decision 7): a script that can go round for ever without beginning its scene is a watcher, a part of the living
    /// room, not initialization still under way (Indy 3's Script 58, a guard's patrol: a scene only when Indy comes near). From <paramref name="start"/>, the code is
    /// followed up to whatever may begin a scene; it watches when a jump back closes a loop on the way.
    /// </summary>
    private static bool RemainingSourceWatches(ScummScriptDisassembly script, int start)
    {
        var instructions = script.Instructions.ToDictionary(i => i.Offset);
        var pending = new Stack<int>();
        var seen = new HashSet<int>();
        pending.Push(start);
        while (pending.TryPop(out var offset))
        {
            if (!seen.Add(offset) || !instructions.TryGetValue(offset, out var instruction)) continue;
            if (instruction.Operation is "cutscene" or "startObject" or "doSentence" or "stopObjectCode" or "chainScript") continue;
            var jump = Regex.Match(instruction.Operands, @"->\s*0x([0-9A-F]+)", RegexOptions.IgnoreCase);
            if (jump.Success)
            {
                var target = int.Parse(jump.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (target <= offset && seen.Contains(target)) return true;
                pending.Push(target);
            }
            if (instruction.Operation == "jumpRelative") continue;
            var next = offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (instruction.Operation == "beginOverride" && instructions.TryGetValue(next, out var skipped))
                next += skipped.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            pending.Push(next);
        }
        return false;
    }

    private bool RemainingSourceMayBeginScene(ScummScriptDisassembly script, int start,
        Dictionary<(ScummScriptDisassembly, int), bool> cache, HashSet<(ScummScriptDisassembly, int)> path, int depth)
    {
        var key = (script, start);
        if (cache.TryGetValue(key, out var known)) return known;
        if (depth >= MaxNestedScripts) return true;
        if (!path.Add(key)) return true; // An unresolved recursive call cannot prove quiet initialization.
        var instructions = script.Instructions.ToDictionary(i => i.Offset);
        var pending = new Stack<int>();
        var seen = new HashSet<int>();
        pending.Push(start);
        while (pending.TryPop(out var offset))
        {
            if (!seen.Add(offset)) continue;
            if (!instructions.TryGetValue(offset, out var instruction)) return cache[key] = true;
            if (instruction.Operation == "cutscene") return cache[key] = true;
            var next = offset + instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (instruction.Operation is "startScript" or "chainScript")
            {
                var literal = Regex.Match(instruction.Operands, @"^0x([0-9A-F]+)", RegexOptions.IgnoreCase);
                if (!literal.Success) return cache[key] = true;
                var number = int.Parse(literal.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var called = number < _globalScriptCount ? _data.GlobalScript(number)
                    : _currentRoom?.ResourceId is int room ? _data.RoomLocalScript(room, number) : null;
                if (called is null || RemainingSourceMayBeginScene(called, called.EntryOffset, cache, path, depth + 1)) return cache[key] = true;
            }
            if (instruction.Operation is "startObject" or "doSentence") return cache[key] = true;
            if (instruction.Operation is "stopObjectCode" or "chainScript") continue;
            var jump = Regex.Match(instruction.Operands, @"->\s*0x([0-9A-F]+)", RegexOptions.IgnoreCase);
            if (jump.Success) pending.Push(int.Parse(jump.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (instruction.Operation == "jumpRelative") continue;
            if (instruction.Operation == "beginOverride" && instructions.TryGetValue(next, out var skipped))
                next += skipped.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            pending.Push(next);
        }
        path.Remove(key);
        return cache[key] = false;
    }
}


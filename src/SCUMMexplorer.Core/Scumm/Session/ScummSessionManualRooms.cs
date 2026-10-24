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
            copy._manualEntryRoots = [];
            copy._capturingManualEntryRoots = true;
            try { copy.VisitManualRoomCore(room); }
            finally { copy._capturingManualEntryRoots = false; }
            cancellationToken.ThrowIfCancellationRequested();
            var possibleScenes = new Dictionary<(ScummScriptDisassembly, int), bool>();
            bool InitializationPending() => copy._pending.Any(p =>
                copy.DescendsFrom(p.Frame, copy._manualEntryRoots) &&
                (ReferenceEquals(p.Script, copy.LastEntryScript) ||
                 copy.RemainingSourceMayBeginScene(p.Script, p.Offset, possibleScenes, [], 0)));
            var settled = copy.SettleRoom(frameLimit, quietFrames, cancellationToken, InitializationPending);
            var initializationPending = InitializationPending();
            copy._manualEntryRoots = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (settled.Outcome != ScummSettleOutcome.Settled || copy.CurrentRoomId != roomId ||
                copy.InCutscene || !copy.UserInputOn || initializationPending)
                return new(null, settled.Outcome == ScummSettleOutcome.RoomLeft
                    ? "Native entry left the selected room; no live preview was adopted."
                    : copy.InCutscene ? "Native entry still has an active cutscene within the preparation bound."
                    : !copy.UserInputOn ? "Native entry has not enabled player input within the preparation bound."
                    : initializationPending ? "Native entry still has pending scene initialization within the preparation bound."
                    : "Native entry did not reach a quiet, controllable room within the bound.", settled.Frames, settled.State);
            copy._uncertainties.Add($"Room {roomId:00} was selected manually: the previous scene was canceled, " +
                "and native entry ran offscreen on an independent copy. This is an exploration preview, not a played route.");
            return new(copy, null, settled.Frames, copy.CaptureSessionRoom());
        }
        catch (OperationCanceledException) { return new(null, "Manual room preparation was canceled.", Cancelled: true); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        { return new(null, $"Native room preparation is unavailable: {ex.Message}"); }
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
    public ScummManualRoomResult PrepareAmbientRoom(GameResource room, CancellationToken cancellationToken = default, bool requireAmbient = true)
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
            foreach (var script in ambient.Ambient)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var locals = script.Arguments.Select((value, index) => (Key: $"local[0x{index:X3}]", Value: (ushort)value))
                    .ToDictionary(item => item.Key, item => item.Value);
                copy.RunStartedScript(script.ScriptId, 0, locals, 0);
            }
            copy._uncertainties.Add($"Room {roomId:00} is a static preview (R-8.5b): its entry code did not run; {stopped} other script slot(s) were ended; " +
                $"only its ambient script(s) {string.Join(", ", ambient.Ambient.Select(a => a.ScriptId))} run, started as the entry would start them " +
                "(an exploration preview, not a played route; the ego is not placed).");
            return new(copy, null, 0, copy.CaptureSessionRoom(), Ambient: ambient);
        }
        catch (OperationCanceledException) { return new(null, "Ambient preview was canceled.", Cancelled: true); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        { return new(null, $"The ambient preview is unavailable: {ex.Message}"); }
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
                _booting && frame.Number == BootScript) roots.Add(frame);
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
        }
        _overrides.Clear();
        _cutsceneOwner = null;
        _visitTrace.Add($"manual selection of Room {target:00}: retired departed scene slots without executing their remaining story or an override jump");
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


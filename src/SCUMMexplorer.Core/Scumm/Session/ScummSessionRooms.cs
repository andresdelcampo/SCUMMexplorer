using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>ScummVM <c>VAR_EGO</c>.</summary>
    protected const string EgoVariable = ScummVars.Ego;

    /// <summary>Actor rooms, positions and walk boxes as reached code left them (actors 1–12).</summary>
    public IReadOnlyList<ScummActorSnapshot> Actors => _runtime.Actors.Snapshot();

    /// <summary>The game's costume with this number (EGA or CD data), or null when it is not indexed.</summary>
    public ScummCostume? ReadCostume(int id) => _data.ReadCostume(id);

    protected ScummActorTable.Placement CurrentPlacement() =>
        new(_runtime.CurrentRoom, _runtime.WalkBoxes, _runtime.WalkBoxFlagsUnknown, _runtime.ObjectClasses);

    protected ScummSessionSnapshot EnterRoom(GameResource room, string arrival, ScummScriptEvent? load = null, string? assumedArrival = null,
        Action<int>? placeAfterLoadRoom = null)
    {
        var eventsBeforeVisit = _visitEvents.Count;
        var estimatedLoad = load is { Certainty: not ScummScriptEventCertainty.Reached, Executed: true };
        if (estimatedLoad)
            _uncertainties.Add($"Room {room.ResourceId:00} entered by estimated execution at {load!.SourceLabel} +0x{load.Offset:X4}; execution continues without promoting the transition's certainty.");
        var withEgo = load is { Operation: "loadRoomWithEgo", ResolvedArguments: { Count: 4 } } ? load : null;
        int? ego = _runtime.GlobalVariables.TryGetValue(EgoVariable, out var egoValue) && ScummActorTable.IsValid(egoValue) ? egoValue : null;
        if (withEgo is not null)
        {
            // o5_loadRoomWithEgo: ego->putActor(room) before startScene; the ego
            // keeps its coordinates and is hidden, since the room is not current yet.
            if (ego is int egoNumber)
            {
                var before = _runtime.Actors[egoNumber];
                _runtime.Actors.Put(egoNumber, before.X, before.Y, room.ResourceId, CurrentPlacement(), ego);
            }
            else _runtime.Actors.ForgetAll($"{load!.SourceLabel} +0x{load.Offset:X4} moves the ego, whose actor number (var[0x001]) is unknown");
        }
        if (_currentRoom is not null)
        {
            RunGlobalHook(30, "before room exit", estimatedLoad);
            RunEmbeddedRoomScript(_currentRoom, exit: true, pathEstimated: estimatedLoad);
            RunGlobalHook(31, "after room exit", estimatedLoad);
            // Room-scoped scripts cannot continue into a different room.
            _pending.RemoveAll(item => item.RoomId == _currentRoom.ResourceId);
        }

        _currentRoom = room;
        _runtime.CurrentRoom = room.ResourceId;
        if (room.ResourceId is int enteredRoom) _roomsEntered.Add(enteredRoom);
        RoomEntrySequence = _visitEvents.Count;
        // startScene clears the draw queue before the entry scripts run.
        _runtime.DrawQueue.Clear();
        _runtime.StatesSetInRoom.Clear();
        _runtime.EstimatedStates.Clear();
        // startScene hides every actor before the new room is loaded.
        _runtime.Actors.HideAll();
        // startScene stops the talk and the room redraw erases drawn strings.
        _runtime.Messages.RoomEntered(_runtime);
        // CD keeps verb-screen strings across room loads. This menu must also erase the previous room's note letters,
        // before its own entry prints the skill choices; the possession bit is preserved.
        if (ErasesVerbScreenOnEntry) _runtime.Messages.VerbDrawn(new ScummScreenBox(0, ScummVerbArea.Top, 319, 199, 0));
        var roomData = _data.ReadRoom(room.ResourceId!.Value);
        _currentObjects = roomData.Objects;
        // getObjOrActorName for text code 6: the stored names of this room's objects.
        var objectNames = roomData.ObjectNames;
        _runtime.ObjectName = id => objectNames.TryGetValue(id, out var name) ? name : null;
        _runtime.WalkBoxFlagsUnknown = null;
        _runtime.WalkBoxes = roomData.WalkBoxes;
        if (roomData.WalkBoxError is not null)
            _uncertainties.Add($"Room {room.ResourceId:00} walk boxes could not be read: {roomData.WalkBoxError}");
        // showActors: actors whose room is the new one are shown, adjusted into its boxes.
        _runtime.Actors.ShowActorsIn(CurrentPlacement());
        // startScene centres the camera on the first screen (VAR_CAMERA_POS_X = 160).
        var roomWidth = roomData.Width;
        _runtime.RoomCameraBounds = _runtime.CameraRange = (160, Math.Max(160, roomWidth - 160));
        _runtime.ResetShadowPalette();
        _runtime.SetGlobalVariable(CameraVariable, 160);
        _runtime.EstimatedVariables.Remove(CameraVariable);
        _runtime.Camera.Reset(160);
        // startScene resets VAR_CAMERA_MIN_X and VAR_CAMERA_MAX_X (v3: vars 17 and 18).
        _runtime.SetGlobalVariable(ScummVars.CameraMinX, 160);
        _runtime.SetGlobalVariable(ScummVars.CameraMaxX, (ushort)Math.Max(160, roomWidth - 160));
        _runtime.EstimatedVariables.Remove(ScummVars.CameraMinX);
        _runtime.EstimatedVariables.Remove(ScummVars.CameraMaxX);
        foreach (var item in _currentObjects)
            _runtime.TryAddObjectPosition(item.Id, new RoomObjectPosition(item.X, item.Y));
        _runtime.SetGlobalVariable("var[0x004]", (ushort)room.ResourceId!.Value);
        _roomSize = (roomWidth, roomData.Height);
        _pointer = null;
        PlaceVirtualMouse();
        _uncertainties.Add(arrival);

        RunGlobalHook(28, "before room entry", estimatedLoad);
        RunEmbeddedRoomScript(room, exit: false, pathEstimated: estimatedLoad);
        RunGlobalHook(29, "after room entry", estimatedLoad);
        if (withEgo is not null && ego is int placedEgo) PlaceEgoAfterEntry(withEgo, placedEgo, assumedArrival);
        else if (placeAfterLoadRoom is not null && ego is int putEgo) placeAfterLoadRoom(putEgo);
        else if (withEgo is not null) ForgetCamera($"{withEgo.SourceLabel} +0x{withEgo.Offset:X4} puts the camera on the ego, whose actor number is unknown");
        LastVisitEvents = _visitEvents.Skip(eventsBeforeVisit).ToArray();
        return Snapshot;
    }

    /// <summary>
    /// o5_loadRoomWithEgo after <c>startScene</c> (v3/v4): unless the entry code
    /// put the ego somewhere, it stands at the object's walk point; the camera is
    /// put on the ego and follows it; then, when the instruction gives a
    /// destination, the ego walks there. The walk is taken to end at once, an
    /// estimate (see <see cref="ScummActorTable.Walk"/>).
    /// </summary>
    protected void PlaceEgoAfterEntry(ScummScriptEvent load, int ego, string? assumedArrival = null)
    {
        var arguments = load.ResolvedArguments!;
        var source = $"{load.SourceLabel} +0x{load.Offset:X4}";
        if (!_runtime.Actors.EgoPositioned)
        {
            var record = arguments[0] is int objectId ? _currentObjects.FirstOrDefault(item => item.Id == objectId) : null;
            if (record is null)
                _runtime.Actors.Forget(ego, arguments[0] is null
                    ? $"{source} brings the ego in at an object the preview cannot read"
                    : $"{source} brings the ego in at object {arguments[0]}, which is not in Room {_currentRoom?.ResourceId:00}");
            else
            {
                _runtime.Actors.Put(ego, record.WalkX, record.WalkY, _currentRoom?.ResourceId, CurrentPlacement(), ego);
                if (assumedArrival is not null)
                    _runtime.Actors.MarkEstimate(ego, $"assumed arrival at {source} after a catalog visit");
                else if (_runtime.EstimatedVariables.Contains(EgoVariable) || load.Certainty != ScummScriptEventCertainty.Reached)
                    _runtime.Actors.MarkEstimate(ego, $"the ego's arrival at {source} follows a seeded pick");
            }
        }
        // camera._cur.x = ego x (o5_loadRoomWithEgo, "based on disassembly"), then
        // setCameraFollows(ego); VAR_CAMERA_POS_X takes the value on the next loop.
        var placed = _runtime.Actors[ego];
        if (placed.X is not int egoX || placed.Room != _currentRoom?.ResourceId)
            ForgetCamera($"{source} puts the camera on the ego, whose position is unknown" +
                         (placed.Unknown is null ? "" : $" ({placed.Unknown})"));
        else SetCamera(egoX, placed.Estimate is null ? null : $"the camera is put on the ego at {source}, whose position is an estimate ({placed.Estimate})",
            $"{source}: camera x = {{0}} (the ego's x)");
        // setCameraFollows(ego): the camera is already on the ego, so it only enters follow mode.
        _runtime.Camera.Mode = ScummCamera.FollowActor;
        _runtime.Camera.Follows = ego;
        if (arguments[2] is int x && x != -1 && arguments[3] is int y)
        {
            // A catalog visit's assumed arrival shows the state after it, so its walk ends at once (estimate).
            _runtime.Actors.Walk(ego, x, y, CurrentPlacement(), $"the ego's walk from {source}", atOnce: assumedArrival is not null);
            // The walk's destination comes from the same assumed or picked arrival.
            if (assumedArrival is not null)
                _runtime.Actors.MarkEstimate(ego, $"assumed arrival at {source} after a catalog visit");
            else if (_runtime.EstimatedVariables.Contains(EgoVariable) || load.Certainty != ScummScriptEventCertainty.Reached)
                _runtime.Actors.MarkEstimate(ego, $"the ego's arrival at {source} follows a seeded pick");
            // A stepped walk moves the camera frame by frame (StepWalksAndCamera); one taken to end at once moves it at once.
            if (_runtime.Actors.Moving(ego) == 0) FollowEgoWalk(_runtime.Actors[ego], source);
        }
        var state = _runtime.Actors[ego];
        _visitTrace.Add($"{source}: the ego (actor {ego}) stands at ({state.X?.ToString() ?? "?"}, {state.Y?.ToString() ?? "?"}), box {state.WalkBox?.ToString() ?? "?"}" +
                        (state.Estimate is null ? "" : $" (estimate: {state.Estimate})"));
    }

    /// <summary>ScummVM <c>VAR_CAMERA_POS_X</c>.</summary>
    protected const string CameraVariable = ScummVars.CameraPosX;

    // The current room's width and height in pixels.
    protected (int Width, int Height)? _roomSize;

    /// <summary>
    /// ScummVM's main loop puts the pointer's room position in <c>VAR_VIRT_MOUSE_X/Y</c>
    /// (vars 20 and 21) every frame. The session has no pointer, so it states one:
    /// the pointer rests just below the room picture (over the verb area), under
    /// the camera's centre. No room object reaches that row, so <c>findObject</c>
    /// finds nothing there, and the input loop idles as it would with the pointer
    /// off the room. This is assumed input, and every read of it is labelled.
    /// </summary>
    protected void PlaceVirtualMouse()
    {
        if (_roomSize is not { } size) return;
        if (_pointer is { } pointer)
        {
            _runtime.SetGlobalVariable(ScummVars.MouseX, (ushort)pointer.X);
            _runtime.SetGlobalVariable(ScummVars.MouseY, (ushort)pointer.Y);
            _runtime.EstimatedVariables.Remove(ScummVars.MouseX);
            _runtime.EstimatedVariables.Remove(ScummVars.MouseY);
            _runtime.VirtualMouse = pointer;
            _runtime.VirtualMouseAssumption = $"assumed input: the pointer rests over the room at ({pointer.X}, {pointer.Y}), {_pointerNote}";
            return;
        }
        var x = CameraX ?? size.Width / 2;
        var y = size.Height;
        _runtime.SetGlobalVariable(ScummVars.MouseX, (ushort)x);
        _runtime.SetGlobalVariable(ScummVars.MouseY, (ushort)y);
        _runtime.EstimatedVariables.Remove(ScummVars.MouseX);
        _runtime.EstimatedVariables.Remove(ScummVars.MouseY);
        _runtime.VirtualMouse = (x, y);
        _runtime.VirtualMouseAssumption = $"assumed input: no pointer over the room, so the virtual mouse (var[0x014], var[0x015]) is taken to rest just below the room picture at ({x}, {y})";
    }

    // The pointer over the current room given by SetPointer or Click, or null (no pointer over the room).
    protected (int X, int Y)? _pointer;
    protected string _pointerNote = HoverNote;
    protected const string HoverNote = "placed by the explorer's hover, with no click";

    /// <summary>
    /// Puts the pointer over the current room at a room-pixel point, or takes it
    /// off (null). This is assumed input: from the next frame the virtual mouse
    /// (<c>var[0x014]</c>/<c>var[0x015]</c>) reads it, so the input loop (script 006)
    /// sees what is under it; no click is made. Points outside the room picture
    /// count as no pointer. Entering another room takes the pointer off.
    /// </summary>
    public void SetPointer((int X, int Y)? point)
    {
        _pointer = point is { } p && _roomSize is { } size && p.X >= 0 && p.Y >= 0 && p.X < size.Width && p.Y < size.Height
            ? p : null;
        _pointerNote = HoverNote;
        PlaceVirtualMouse();
    }

    /// <summary>The pointer the session reads as the virtual mouse, or null when it assumes none over the room.</summary>
    public (int X, int Y)? Pointer => _pointer;

    /// <summary>Verb slots as reached <c>verbOps</c> left them: the verb bar and the verb-area close-up (verb 0x36).</summary>
    public IReadOnlyList<ScummVerbSnapshot> Verbs => _runtime.Verbs.Snapshot();

    /// <summary>The camera's centre x in room pixels (<c>var[0x002]</c>), or null when it is unknown.</summary>
    public int? CameraX => _runtime.GlobalVariables.TryGetValue(CameraVariable, out var x) ? x : null;

    /// <summary>True when <see cref="CameraX"/> comes from an estimate (a walk end, a seeded pick, an instant pan).</summary>
    public bool CameraEstimated => _runtime.EstimatedVariables.Contains(CameraVariable);

    /// <summary>
    /// The camera's limits in the current room (<c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c>): half a screen from each edge,
    /// narrowed by <c>roomOps roomScroll</c> (Room 41's entry: 176..184); null when no room is loaded.
    /// </summary>
    public (int Min, int Max)? CameraRange => _runtime.CameraRange;

    /// <summary>Puts the camera at <paramref name="x"/>, kept half a screen from the room's edges (<c>cameraMoved</c>).</summary>
    protected void SetCamera(int x, string? estimate, string trace)
    {
        var (min, max) = _runtime.CameraRange ?? (160, 160);
        var clamped = (ushort)Math.Clamp(x, min, max);
        _runtime.SetGlobalVariable(CameraVariable, clamped);
        _runtime.Camera.DestX = x;
        _runtime.Camera.MovingToActor = false;
        if (estimate is null) _runtime.EstimatedVariables.Remove(CameraVariable);
        else
        {
            _runtime.EstimatedVariables.Add(CameraVariable);
            _uncertainties.Add($"Camera x (var[0x002]) = {clamped} is an estimate: {estimate}.");
        }
        _visitTrace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, trace, clamped) +
                        (estimate is null ? "" : " (estimate)"));
    }

    protected void ForgetCamera(string reason)
    {
        _runtime.RemoveGlobalVariable(CameraVariable);
        _runtime.EstimatedVariables.Remove(CameraVariable);
        _uncertainties.Add($"Camera x (var[0x002]) is unknown: {reason}.");
    }

    /// <summary>
    /// ScummVM <c>moveCamera</c> in follow mode: once the followed actor is more
    /// than 10 strips (80 pixels) from the camera's centre strip, the camera moves
    /// to the actor. The walk ends at once in this preview, so the camera takes the
    /// end point at once too, and both are estimates.
    /// </summary>
    protected void FollowEgoWalk(ScummActorSnapshot walked, string source)
    {
        if (!_runtime.GlobalVariables.TryGetValue(CameraVariable, out var camera)) return;
        if (walked.X is not int endX || walked.Room != _currentRoom?.ResourceId)
        {
            ForgetCamera($"it follows the ego's walk from {source}, whose end is unknown");
            return;
        }
        // _screenStartStrip = camera / 8 - 20; the triggers are strips 10 and 30.
        var strip = endX / 8 - (camera / 8 - 20);
        var reason = $"it follows the ego's walk from {source}, which is taken to end at once ({walked.Estimate ?? "walk timing and path not simulated"})";
        if (strip is < 10 or > 30)
            SetCamera(endX, reason, $"{source}: the camera follows the ego's walk to x = {{0}}");
        else if (_runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {camera} is an estimate: {reason}; the walk's end stays within the camera's triggers.");
    }

    /// <summary>
    /// The verb area as the session's verb slots would draw it, with each image
    /// verb's rectangle from its object's image in the room it was taken from
    /// (<see cref="ScummVerbArea.Compose"/>).
    /// </summary>
    public ScummVerbAreaPicture VerbArea() => ScummVerbArea.Compose(
        _runtime.Verbs.Snapshot().Where(v => !HidesVerb(v)),
        VerbImage, _runtime.Verbs.Erases(), alignRows: AlignVerbRows);

    protected readonly Dictionary<int, IReadOnlyList<RoomObjectImage>> _verbImageRooms = [];

    protected RoomObjectImage? VerbImage(int roomId, int objectId)
    {
        if (!_verbImageRooms.TryGetValue(roomId, out var images))
        {
            try { images = _data.ReadObjectImages(roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { images = []; }
            _verbImageRooms[roomId] = images;
        }
        return images.FirstOrDefault(item => item.Id == objectId);
    }

    /// <summary>A game whose room entry must also erase the previous room's strings from the verb screen (a menu room) says so here.</summary>
    protected virtual bool ErasesVerbScreenOnEntry => false;

    /// <summary>A verb slot that is on in the game's state but that the verb area does not display.</summary>
    protected virtual bool HidesVerb(ScummVerbSnapshot verb) => false;

    /// <summary>Whether the verb area aligns its rows to the v4 layout.</summary>
    protected virtual bool AlignVerbRows => false;
}


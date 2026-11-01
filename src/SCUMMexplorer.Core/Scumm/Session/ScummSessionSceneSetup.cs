namespace SCUMMexplorer.Core.Scumm;

/// <summary>What one value of a picked scene's setup sets (REQUIREMENTS §8.6, R-8.36).</summary>
public enum ScummSetupKind
{
    /// <summary>A bit variable (<c>bit[0x…]</c>) set to 1.</summary>
    Bit,
    /// <summary>A global variable (<c>var[0x…]</c>) set to the value its test compares with.</summary>
    Variable,
    /// <summary>An object's state (<c>ifState</c>, <c>getObjectState</c>).</summary>
    ObjectState,
    /// <summary>An object given to the hero (owner, inventory, its code taken from the room it lies in).</summary>
    Held,
    /// <summary>The second object of a two-object verb (Use X with Y): the verb's code tests its argument; given to the hero and passed as object B.</summary>
    SecondObject,
    /// <summary>An actor dressed in a costume (<c>getActorCostume</c> then compared): a magic costume change.</summary>
    ActorCostume,
    /// <summary>An actor put in a room (<c>getActorRoom</c> then compared): a teleport.</summary>
    ActorRoom,
    /// <summary>An object's class set (Value &gt; 0) or cleared (Value &lt; 0) (<c>ifClassOfIs</c>): an open cupboard, a lit lamp.</summary>
    ObjectClass,
    /// <summary>An actor stood in a walk box of the room (<c>getActorWalkBox</c> then compared): the code waits for the hero to walk there (LOOM Room 10's entry, local script 200).</summary>
    WalkBox,
    /// <summary>
    /// The hero came from room <c>Value</c>: the variable <c>Target</c> is the one the game's exit hook fills with the room being left
    /// (<c>move var[X] = VAR_ROOM</c>; Indy 4 Script 23 +0x0014, Monkey Island and Indy 3 Script 7), so that room is visited first and the scene's room
    /// entered from it.
    /// </summary>
    CameFrom,
    /// <summary>
    /// The scene's room is entered under pseudo room number <c>Value</c> (0x80 and up, <c>o5_pseudoRoom</c>): its entry code tells the uses of the room
    /// apart by VAR_ROOM (Indy 3 Room 21, the castle corridors, entered as 129 to 131).
    /// </summary>
    EnteredAs,
    /// <summary>
    /// The hero comes into the scene's room by object <c>Value</c> (a door): the entry code compares <c>VAR_WALKTO_OBJ</c> with it (Indy 3 Room 21,
    /// +0x00DB: the corridor door 311), so the visit takes an arrival of the game's own code through that object.
    /// </summary>
    EnteredBy,
    /// <summary>Object <c>Target</c> owned by actor <c>Value</c> (<c>getObjectOwner</c> compared with an actor: Indy 3's Grail held by Elsa, actor 3).</summary>
    OwnedBy,
    /// <summary>An actor of the scene's room stood at x <c>Value</c> (a script waits for it to walk past a point: <c>getActorX</c> then compared).</summary>
    ActorX,
    /// <summary>Global or room script <c>Target</c> running when the scene starts (<c>isScriptRunning</c> then compared): started in the scene's room.</summary>
    ScriptRunning,
    /// <summary>The camera at x <c>Value</c> (a script waits for <c>VAR_CAMERA_POS_X</c> to pass a point): the hero stood there and the camera taken to him.</summary>
    CameraX,
}

/// <summary>
/// One value of a scene's setup: what it sets, to what, and the instruction that tests it (R-2.1). <paramref name="Room"/> is the room an object
/// given to the hero lies in (its code is that room's copy, as <c>addObjectToInventory</c> takes it).
/// </summary>
public sealed record ScummSetupEdit(ScummSetupKind Kind, string Target, int Value, string Where, int? Room = null)
{
    public string Describe() => Kind switch
    {
        ScummSetupKind.Bit => $"{Target} = 1",
        ScummSetupKind.Variable => $"{Target} = {Value}",
        ScummSetupKind.ObjectState => $"object {Target} state {Value}",
        ScummSetupKind.SecondObject => $"object {Target} used with it (held)",
        ScummSetupKind.ActorCostume => $"{(Target == "ego" ? "the hero" : Target.StartsWith("var[", StringComparison.Ordinal) ? $"the actor in {Target}" : $"actor {Target}")} in costume {Value}",
        ScummSetupKind.ActorRoom => $"{(Target == "ego" ? "the hero" : Target.StartsWith("var[", StringComparison.Ordinal) ? $"the actor in {Target}" : $"actor {Target}")} in Room {Value}",
        ScummSetupKind.CameFrom => $"arrived from Room {Value} ({Target})",
        ScummSetupKind.EnteredAs => $"the room entered as pseudo room {Value}",
        ScummSetupKind.EnteredBy => $"the hero comes in by object {Value}",
        ScummSetupKind.OwnedBy => $"object {Target} owned by actor {Value}",
        ScummSetupKind.ActorX => $"{(Target == "ego" ? "the hero" : $"actor {Target}")} at x {Value}",
        ScummSetupKind.WalkBox => $"{(Target == "ego" ? "the hero" : $"actor {Target}")} in walk box {Value}",
        ScummSetupKind.ScriptRunning => $"script {Target} running",
        ScummSetupKind.CameraX => $"the camera at x {Value}",
        ScummSetupKind.ObjectClass => Value > 0 ? $"object {Target} in class {Value}" : $"object {Target} out of class {-Value}",
        _ => $"object {Target} held",
    } + $" (tested at {Where})";
}

public abstract partial class ScummSession
{
    /// <summary>
    /// Brings the session to a picked scene's setup (R-1.9 exception, §8.6): each value is set only when the session lacks it, so state the session
    /// already has is kept (a bit is only raised; a variable is set to the value its test compares with; an object already held stays held). Returns what was set.
    /// Never used by Play or a room's first visit.
    /// </summary>
    public IReadOnlyList<ScummSetupEdit> ApplySceneSetup(IEnumerable<ScummSetupEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var applied = new List<ScummSetupEdit>();
        foreach (var edit in edits)
            if (ApplySetupEdit(edit)) applied.Add(edit);
        if (applied.Count > 0) _uncertainties.Add("A picked scene's setup set " + string.Join("; ", applied.Select(edit => edit.Describe())) +
                                                  ": the game sets these in earlier scenes that were not played (REQUIREMENTS §8.6).");
        return applied;
    }

    private bool ApplySetupEdit(ScummSetupEdit edit)
    {
        var variables = _runtime.GlobalVariables;
        switch (edit.Kind)
        {
            case ScummSetupKind.Bit:
            {
                if (variables.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage))
                {
                    if (variables.GetValueOrDefault(edit.Target) != 0) return false;
                    _runtime.SetGlobalVariable(edit.Target, 1);
                    return true;
                }
                // Old v3: bit n is bit (n & 15) of global word n >> 4.
                var bit = Convert.ToInt32(edit.Target[6..^1], 16);
                var word = $"var[0x{bit >> 4:X3}]";
                var mask = (ushort)(1 << (bit & 15));
                var value = variables.GetValueOrDefault(word);
                if ((value & mask) != 0) return false;
                _runtime.SetGlobalVariable(word, (ushort)(value | mask));
                return true;
            }
            case ScummSetupKind.Variable:
                // Set whenever it differs: a story variable is compared with a value (Indy 4 Room 10's entry branches on var[0x0E0], the room the hero
                // came from, which a booted session never has at 0), so "only while it is 0" left most such tests unreachable.
                if (variables.GetValueOrDefault(edit.Target) == edit.Value) return false;
                _runtime.SetGlobalVariable(edit.Target, (ushort)edit.Value);
                return true;
            case ScummSetupKind.ObjectState:
            {
                var id = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (_runtime.ObjectStates.GetValueOrDefault(id) == edit.Value) return false;
                _runtime.SetObjectState(id, (byte)edit.Value);
                return true;
            }
            case ScummSetupKind.ObjectClass:
            {
                var id = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                var bit = 1u << (Math.Abs(edit.Value) - 1);
                var classes = _runtime.ObjectClasses.GetValueOrDefault(id);
                var wanted = edit.Value > 0 ? classes | bit : classes & ~bit;
                if (wanted == classes) return false;
                _runtime.SetObjectClasses(id, wanted);
                return true;
            }
            case ScummSetupKind.CameFrom:
            {
                // Only before the scene's room is entered, and only when the variable does not already say so: visiting that room first lets the
                // game's own exit hook set the variable as the scene's room is entered next.
                if (edit.Room is int sceneRoom && _runtime.CurrentRoom == sceneRoom) return false;
                if (variables.GetValueOrDefault(edit.Target) == edit.Value && _runtime.CurrentRoom == edit.Value) return false;
                // A pseudo room number is visited as its real room under that number (the exit hook then copies the number).
                var real = edit.Value >= 0x80 && _runtime.PseudoRooms.TryGetValue(edit.Value, out var mapped) ? mapped : edit.Value;
                if (_game.Rooms.FirstOrDefault(r => r.ResourceId == real) is not { } from) return false;
                if (real != edit.Value) WantedPseudoRoom = edit.Value;
                VisitRoom(from);
                return true;
            }
            case ScummSetupKind.EnteredAs:
            {
                // Only before the scene's room is entered (the next visit or entry of it loads it under this number), and only when the game maps it there.
                if (edit.Room is int pseudoOf && (_runtime.CurrentRoom == pseudoOf && variables.GetValueOrDefault(ScummVars.Room) == edit.Value ||
                                                  _runtime.PseudoRooms.GetValueOrDefault(edit.Value) != pseudoOf)) return false;
                if (WantedPseudoRoom == edit.Value) return false;
                WantedPseudoRoom = edit.Value;
                return true;
            }
            case ScummSetupKind.ActorX:
            {
                // Only in the scene's room, after its entry (the waiting script reads the position frame by frame).
                if (edit.Room is int xRoom && _runtime.CurrentRoom != xRoom) return false;
                var heroNumber = variables.TryGetValue(_runtime.EgoVariable, out var heroValue) ? (int?)heroValue : null;
                var mover = edit.Target == "ego" ? heroNumber ?? 0 : int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (!_runtime.Actors.Contains(mover) || _runtime.Actors[mover].Room != _runtime.CurrentRoom || _runtime.Actors[mover].X == edit.Value) return false;
                _runtime.Actors.Put(mover, edit.Value, _runtime.Actors[mover].Y, _runtime.CurrentRoom, CurrentPlacement(), heroNumber);
                return true;
            }
            case ScummSetupKind.ScriptRunning:
            {
                // Only in the scene's room, after its entry (a room's script runs only there; the entry would end a local one).
                var number = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (edit.Room is int runRoom && _runtime.CurrentRoom != runRoom || IsScriptRunning(number).Running) return false;
                return StartScriptForSetup(number);
            }
            case ScummSetupKind.CameraX:
            {
                // Only in the scene's room: the hero stood at that x and the camera taken to him (it follows him there, as his walk would take it).
                if (edit.Room is int cameraRoom && _runtime.CurrentRoom != cameraRoom) return false;
                var hero = variables.TryGetValue(_runtime.EgoVariable, out var heroActor) ? (int?)heroActor : null;
                if (hero is not int walker || !_runtime.Actors.Contains(walker) || _runtime.Actors[walker].Room != _runtime.CurrentRoom ||
                    variables.GetValueOrDefault(ScummVars.CameraPosX) == edit.Value) return false;
                _runtime.Actors.Put(walker, edit.Value, _runtime.Actors[walker].Y, _runtime.CurrentRoom, CurrentPlacement(), hero);
                _runtime.Camera.Mode = ScummCamera.FollowActor;
                _runtime.Camera.Follows = walker;
                _runtime.Camera.MovingToActor = true;
                return true;
            }
            case ScummSetupKind.OwnedBy:
            {
                var owned = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (_runtime.ObjectOwners.GetValueOrDefault(owned) == edit.Value) return false;
                _runtime.SetObjectOwner(owned, (byte)edit.Value);
                return true;
            }
            case ScummSetupKind.EnteredBy:
                // Only before the scene's room is entered; the visit uses it when the game's own code has an arrival through that object.
                if (edit.Room is int byRoom && _runtime.CurrentRoom == byRoom || WantedArrivalObject == edit.Value) return false;
                WantedArrivalObject = edit.Value;
                return true;
            case ScummSetupKind.WalkBox:
            {
                // Only in the scene's room, and a few frames later: the waiting script first notes the box the hero stands in and then waits for it
                // to change (LOOM Room 10's local script 200 +0x0001 to +0x000A), as the walk there would change it.
                if (edit.Room is int boxRoom && _runtime.CurrentRoom != boxRoom) return false;
                var actor = edit.Target == "ego" ? variables.GetValueOrDefault(_runtime.EgoVariable) : int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (_deferredBoxPuts.Any(d => d.Actor == actor && d.Box == edit.Value)) return false;
                _deferredBoxPuts.Add((actor, edit.Value, BoxPutDelayFrames));
                return true;
            }
            case ScummSetupKind.ActorCostume or ScummSetupKind.ActorRoom:
            {
                // The actor: a number, the hero ("ego"), or the actor a variable holds now (Indy 4's var[0x0B6], Sophia).
                var actor = edit.Target == "ego" ? variables.GetValueOrDefault(_runtime.EgoVariable)
                    : edit.Target.StartsWith("var[", StringComparison.Ordinal) ? variables.GetValueOrDefault(edit.Target)
                    : int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (!_runtime.Actors.Contains(actor)) return false;
                var ego = variables.TryGetValue(_runtime.EgoVariable, out var egoValue) ? (int?)egoValue : null;
                // The hero in no room (Monkey Island 2 Room 72's entry, +0x001C: getActorRoom of actor 1 compared with 0): the next visit loads the
                // room without him, as a loadRoom rather than a loadRoomWithEgo would.
                if (edit.Kind == ScummSetupKind.ActorRoom && edit.Value == 0 && actor == ego && !WantedHeroAway && _runtime.Actors[actor].Room != 0)
                    WantedHeroAway = true;
                // The hero in a room he is not in yet (Monkey Island 1 Room 72's entry, +0x002C: getActorRoom of the hero compared with VAR_ROOM):
                // the next visit of that room puts him in it before its entry, as loadRoomWithEgo would, where no decoded arrival brings him.
                if (edit.Kind == ScummSetupKind.ActorRoom && edit.Value > 0 && actor == ego && _runtime.CurrentRoom != edit.Value)
                    WantedHeroIn = edit.Value;
                if (edit.Kind == ScummSetupKind.ActorCostume)
                {
                    if (_runtime.Actors[actor].Look.Costume == edit.Value) return false;
                    _runtime.Actors.CostumeChanged(actor, edit.Value, CurrentPlacement());
                }
                else
                {
                    if (_runtime.Actors[actor].Room == edit.Value) return false;
                    _runtime.Actors.PutInRoom(actor, edit.Value, CurrentPlacement(), ego);
                }
                return true;
            }
            default:
            {
                var id = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (Array.IndexOf(_runtime.InventorySlots, id) >= 0) return false;
                var ego = variables.GetValueOrDefault(_runtime.EgoVariable);
                if (ego == 0 || !_runtime.AddToInventorySlots(id)) return false;
                _runtime.SetObjectOwner(id, (byte)ego);
                if (edit.Room is int room) _runtime.InventoryRooms[id] = room;
                return true;
            }
        }
    }

    /// <summary>
    /// Stands an actor of the current room in walk box <paramref name="box"/> (the box's middle, or the nearest point of it), as a walk there would leave
    /// it. A box that is only a line (a doorway) holds no standing actor: the actor then starts a walk to a box whose route crosses it, so the frames
    /// step it through. False before the room is entered or when the actor is elsewhere or already in the box.
    /// </summary>
    private const int BoxPutDelayFrames = 4;

    /// <summary>The pseudo room number a picked scene's setup wants its room entered under (<see cref="ScummSetupKind.EnteredAs"/>), until it is used.</summary>
    protected int? WantedPseudoRoom { get; set; }

    /// <summary>True when a picked scene's setup wants its room entered without the hero (<see cref="ScummSetupKind.ActorRoom"/> 0 for the hero), until used.</summary>
    protected bool WantedHeroAway { get; set; }

    /// <summary>The room a picked scene's setup wants the hero in as it is entered (<see cref="ScummSetupKind.ActorRoom"/> for the hero), until a visit uses it.</summary>
    protected int? WantedHeroIn { get; set; }

    /// <summary>
    /// Readies the next entry of the scene's room for a setup's values about how it is entered (<see cref="ScummSetupKind.EnteredBy"/>,
    /// <see cref="ScummSetupKind.EnteredAs"/>), which <see cref="ApplySceneSetup"/> leaves alone in the room itself: the caller then enters the room
    /// again (<see cref="EnterRoomAgain"/>). True when there is such a value.
    /// </summary>
    public bool PrepareEntry(IEnumerable<ScummSetupEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var any = false;
        foreach (var edit in edits)
        {
            if (edit.Kind == ScummSetupKind.EnteredBy) { WantedArrivalObject = edit.Value; any = true; }
            else if (edit.Kind == ScummSetupKind.EnteredAs && _runtime.PseudoRooms.GetValueOrDefault(edit.Value) == edit.Room) { WantedPseudoRoom = edit.Value; any = true; }
        }
        return any;
    }

    /// <summary>The object a picked scene's setup wants the hero to come in by (<see cref="ScummSetupKind.EnteredBy"/>), until a visit uses it.</summary>
    protected int? WantedArrivalObject { get; set; }
    private readonly List<(int Actor, int Box, int Frames)> _deferredBoxPuts = [];

    /// <summary>Counts down the walk-box placements of a picked scene's setup and makes those that are due (called at the start of each frame).</summary>
    protected void StepDeferredBoxPuts()
    {
        for (var i = _deferredBoxPuts.Count - 1; i >= 0; i--)
        {
            var (actor, box, frames) = _deferredBoxPuts[i];
            if (frames > 1) { _deferredBoxPuts[i] = (actor, box, frames - 1); continue; }
            _deferredBoxPuts.RemoveAt(i);
            PutActorInBox(actor, box);
        }
    }

    private bool PutActorInBox(int actor, int box)
    {
        var ego = _runtime.GlobalVariables.TryGetValue(_runtime.EgoVariable, out var egoValue) ? (int?)egoValue : null;
        // The hero may be elsewhere (a picked room's entry): it is brought in, a teleport; another actor must be in the room already.
        if (_runtime.WalkBoxes is not { } boxes || _runtime.CurrentRoom is not int room || !_runtime.Actors.Contains(actor) ||
            _runtime.Actors[actor].Room != room && actor != ego || _runtime.Actors[actor].Room == room && _runtime.Actors[actor].WalkBox == box ||
            boxes.Coordinates(box) is not { } shape) return false;
        var (x, y) = (shape.Corners.Sum(c => c.X) / 4, shape.Corners.Sum(c => c.Y) / 4);
        if (!boxes.Contains(box, x, y)) (_, x, y) = ScummWalkBoxSet.ClosestPointOnBox(shape, x, y);
        var start = (_runtime.Actors[actor].X, _runtime.Actors[actor].Y, _runtime.Actors[actor].WalkBox);
        var place = CurrentPlacement();
        var elsewhere = _runtime.Actors[actor].Room != room;
        _runtime.Actors.Put(actor, x, y, room, place, ego);
        if (elsewhere) { _runtime.Actors.ShowActorsIn(place); start = (x, y, _runtime.Actors[actor].WalkBox); }
        if (_runtime.Actors[actor].WalkBox != box)
        {
            _runtime.Actors.Put(actor, start.X, start.Y, room, place, ego);
            var from = start.WalkBox ?? 0;
            foreach (var target in boxes.Boxes.Where(b => b.Index != box && (boxes.GetFlags(b.Index) & ScummWalkBox.InvisibleFlag) == 0))
            {
                var route = new List<int> { from };
                for (var hop = 0; hop < 24 && route[^1] != target.Index; hop++)
                {
                    var next = boxes.GetNextBox(route[^1], target.Index);
                    if (next < 0 || route.Contains(next)) break;
                    route.Add(next);
                }
                if (route[^1] != target.Index || !route.Contains(box)) continue;
                _runtime.Actors.Walk(actor, target.Corners.Sum(c => c.X) / 4, target.Corners.Sum(c => c.Y) / 4, place,
                    $"Picked scene's setup: actor {actor} walks across walk box {box}");
                break;
            }
        }
        _visitTrace.Add($"picked scene's setup: actor {actor} put in walk box {box} at ({_runtime.Actors[actor].X}, {_runtime.Actors[actor].Y})");
        return true;
    }

    /// <summary>
    /// A picked interaction (R-8.38): in a game with a sentence script (<c>VAR_SENTENCE_SCRIPT</c>, versions 3 to 5), the hero is put at the object's
    /// walk point (a teleport instead of the walk) and the sentence (verb, object A, object B) is queued as <c>doSentence</c> does, so the game's own
    /// sentence script runs it on the next frame (it sets the object variables, faces the hero, runs the verb's code with its arguments). Elsewhere the
    /// verb's code is run directly (<see cref="ActivateObjectVerb"/>). An exploration action, never the player's click.
    /// </summary>
    public void PickInteraction(int objectA, int verb, int objectB = 0)
    {
        var sentence = _runtime.IsV2 ? 0 : _runtime.GlobalVariables.GetValueOrDefault(ScummVars.SentenceScript);
        // The hero is put at the object on every path (LOOM has no sentence script: left in the boot's room, the scene's own actorFollowCamera of
        // the hero would start that room's scene, Script 3 +0x001B; found by the comparison with LOOM English's catalog, 31 October 2026).
        // Not yet for versions 1 and 2: Maniac Mansion and Zak McKracken keep their own scene setup until they move onto this one (ROADMAP 2b).
        if (!_runtime.IsV2) PutHeroAtObject(objectA);
        if (sentence == 0)
        {
            ActivateObjectVerb(objectA, verb);
            return;
        }
        _runtime.Sentences.Add((verb, objectA, objectB));
        _uncertainties.Add($"Object {objectA} verb 0x{verb:X2}{(objectB != 0 ? $" with object {objectB}" : "")} was picked: the hero was put at the object and the " +
                           $"sentence given to the game's sentence script {sentence} (an exploration action, not the player's click).");
        _visitTrace.Add($"picked interaction: verb 0x{verb:X2} on object {objectA} and {objectB} queued for sentence script {sentence}");
    }

    /// <summary>
    /// The last way to show a picked scene (R-8.35): the script that holds its cutscene (a scan label: <c>global script 52</c>, <c>Room 13 local script
    /// 204</c>, <c>Room 7 object 134 verb 0x07</c>, <c>Room 13 entry script</c>) started directly at <paramref name="offset"/>, its <c>cutscene</c>
    /// instruction. The tests before it are skipped and its locals start at 0; everything from there on is the game's own code. A room's script
    /// starts only in that room. An exploration action, labelled as such.
    /// </summary>
    public bool StartSceneAt(string label, int offset, IReadOnlyList<int>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        BeforeInput();
        if (_currentRoom?.ResourceId is not int here) return false;
        var global = System.Text.RegularExpressions.Regex.Match(label, @"^global script (\d+)$");
        var room = ScummCutsceneRun.RoomOf(label);
        if (!global.Success && room != here) return false;
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _uncertainties.Add($"{label} was started directly at its cutscene (+0x{offset:X4}): an exploration action. The tests before it were skipped and " +
                           (arguments is { Count: > 0 } ? $"its locals are the arguments its caller passes ({string.Join(", ", arguments)})" : "its local values start at 0") +
                           ", so what it shows may differ from the game's own way to it.");
        _visitTrace.Add($"picked scene: {label} started at +0x{offset:X4}");
        LastAction = new ScummObjectActionResult(here, 0, 0, 0, null, null, null, null, [], label);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            var local = System.Text.RegularExpressions.Regex.Match(label, @"^Room \d+ local script (\d+)$");
            if (global.Success || local.Success)
                RunStartedScript(int.Parse((global.Success ? global : local).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), offset,
                    (arguments ?? []).Select((value, index) => (Key: $"local[0x{index:X3}]", Value: (ushort)value)).ToDictionary(p => p.Key, p => p.Value), 0);
            else
            {
                var verb = System.Text.RegularExpressions.Regex.Match(label, @"^Room \d+ object (\d+) verb 0x([0-9A-F]+)$");
                ScummScriptDisassembly? script = null;
                if (verb.Success && _currentObjects.FirstOrDefault(item => item.Id == int.Parse(verb.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) is { } record)
                    script = _data.ReadObjectVerb(here, record, Convert.ToInt32(verb.Groups[2].Value, 16), out _);
                else if (label.EndsWith(" entry script", StringComparison.Ordinal))
                    script = _data.RoomEntryScript(here);
                if (script is null) return false;
                RunScript(script, _currentObjects, offset, null, 0, keepState: true);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { _uncertainties.Add($"{label}: {ex.Message}"); return false; }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        FinishInput(beforeUncertainties, beforeEvents);
        return true;
    }

    /// <summary>A script a picked scene's setup wants running (<see cref="ScummSetupKind.ScriptRunning"/>), started from its first instruction.</summary>
    private bool StartScriptForSetup(int number)
    {
        BeforeInput();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _visitTrace.Add($"picked scene's setup: script {number} started");
        LastAction = new ScummObjectActionResult(_runtime.CurrentRoom ?? 0, 0, 0, 0, null, null, null, null, [], $"script {number}");
        _dispatchScope++;
        try { RunStartedScript(number, 0, null, 0); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { _uncertainties.Add($"Script {number}: {ex.Message}"); return false; }
        finally { _dispatchScope--; }
        FinishInput(beforeUncertainties, beforeEvents);
        return IsScriptRunning(number).Running;
    }

    /// <summary>
    /// A picked object that is not in the room yet (R-8.36, user 31 October 2026: "if the object is not yet in the room but might be later, doesn't
    /// matter"): the room is made its owner again (unless the hero holds it), its untouchable class is cleared, a parent object is put in the state
    /// the object needs (<c>RoomHotspotRules</c>), and a state of 0 becomes its first image when it has one. An object that is a hotspot now is left alone, so one
    /// shown in its state 0 (a closed door) is never changed. Returns what was changed.
    /// </summary>
    public IReadOnlyList<string> BringObjectIntoRoom(int objectId)
    {
        var changed = new List<string>();
        if (_currentObjects.FirstOrDefault(item => item.Id == objectId) is not { } record) return changed;
        // Present already (owned by the room, touchable, its parent in the right state): left as it is, whatever its state.
        if (RoomHotspotRules.IsHotspot(record, _currentObjects, Snapshot, _runtime.IsV2 ? (byte)8 : (byte)15)) return changed;
        if (_runtime.ObjectOwners.TryGetValue(objectId, out var owner) && owner != 0x0F && Array.IndexOf(_runtime.InventorySlots, objectId) < 0)
        {
            _runtime.SetObjectOwner(objectId, 0x0F);
            changed.Add($"object {objectId} given back to the room (owner {owner})");
        }
        if (_runtime.ObjectClasses.TryGetValue(objectId, out var classes) && ScummObjectClasses.Has(classes, _runtime.UntouchableClass))
        {
            _runtime.SetObjectClasses(objectId, classes & ~(1u << (_runtime.UntouchableClass - 1)));
            changed.Add($"object {objectId} made touchable (class {_runtime.UntouchableClass} cleared)");
        }
        var current = record;
        for (var steps = 0; current.ParentLocalIndex != 0 && steps < _currentObjects.Count; steps++)
        {
            if (_currentObjects.FirstOrDefault(item => item.LocalIndex == current.ParentLocalIndex) is not { } parent) break;
            if ((_runtime.ObjectStates.GetValueOrDefault(parent.Id) & 0x0F) != current.ParentRequiredState)
            {
                _runtime.SetObjectState(parent.Id, (byte)current.ParentRequiredState);
                changed.Add($"parent object {parent.Id} in state {current.ParentRequiredState}");
            }
            current = parent;
        }
        if (_runtime.ObjectStates.GetValueOrDefault(objectId) == 0 && record.ImageOffset is not null)
        {
            _runtime.SetObjectState(objectId, 1);
            changed.Add($"object {objectId} shown in state 1");
        }
        if (changed.Count > 0)
            _uncertainties.Add($"Object {objectId} was not in the room; for the picked interaction: {string.Join("; ", changed)} (an exploration action, REQUIREMENTS R-8.36).");
        return changed;
    }

    /// <summary>
    /// The hero teleported to a picked object (R-8.38): put in the current room when elsewhere, at the object's walk point (or under the object when it
    /// has none), shown, and followed by the camera, so the object's response is said and seen. Returns false when the game has no hero or the object
    /// is not in the room's data.
    /// </summary>
    public bool PutHeroAtObject(int objectId)
    {
        if (_runtime.IsV2) return false;
        var ego = _runtime.GlobalVariables.TryGetValue(_runtime.EgoVariable, out var egoValue) && _runtime.Actors.Contains(egoValue) ? egoValue : 0;
        if (ego == 0 || _runtime.CurrentRoom is not int room || _currentObjects.FirstOrDefault(item => item.Id == objectId) is not { } record) return false;
        var (x, y) = (record.WalkX, record.WalkY) != (0, 0) ? (record.WalkX, record.WalkY) : (record.X + record.Width / 2, record.Y + record.Height);
        var place = CurrentPlacement();
        var elsewhere = _runtime.Actors[ego].Room != room;
        _runtime.Actors.Put(ego, x, y, room, place, ego);
        if (elsewhere)
        {
            _runtime.Actors.ShowActorsIn(place);
            _uncertainties.Add($"The hero (actor {ego}) was not in Room {room:00}; for the picked interaction it was put at object {objectId} (an exploration action, REQUIREMENTS R-8.38).");
        }
        _runtime.Camera.Mode = ScummCamera.FollowActor;
        _runtime.Camera.Follows = ego;
        _runtime.Camera.MovingToActor = true;
        return true;
    }
}

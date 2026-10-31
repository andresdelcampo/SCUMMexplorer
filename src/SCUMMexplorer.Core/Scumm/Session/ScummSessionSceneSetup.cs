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
        ScummSetupKind.ActorCostume => $"actor {Target} in costume {Value}",
        ScummSetupKind.ActorRoom => $"actor {Target} in Room {Value}",
        _ => $"object {Target} held",
    } + $" (tested at {Where})";
}

public abstract partial class ScummSession
{
    /// <summary>
    /// Brings the session to a picked scene's setup (R-1.9 exception, §8.6): each value is set only when the session lacks it, so state the session
    /// already has is kept (a bit is only raised; a variable is set only while it is 0; an object already held stays held). Returns what was set.
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
                if (variables.GetValueOrDefault(edit.Target) != 0) return false;
                _runtime.SetGlobalVariable(edit.Target, (ushort)edit.Value);
                return true;
            case ScummSetupKind.ObjectState:
            {
                var id = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (_runtime.ObjectStates.GetValueOrDefault(id) == edit.Value) return false;
                _runtime.SetObjectState(id, (byte)edit.Value);
                return true;
            }
            case ScummSetupKind.ActorCostume or ScummSetupKind.ActorRoom:
            {
                var actor = int.Parse(edit.Target, System.Globalization.CultureInfo.InvariantCulture);
                if (!_runtime.Actors.Contains(actor)) return false;
                var ego = variables.TryGetValue(_runtime.EgoVariable, out var egoValue) ? (int?)egoValue : null;
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
    /// A picked interaction (R-8.38): in a game with a sentence script (<c>VAR_SENTENCE_SCRIPT</c>, versions 3 to 5), the hero is put at the object's
    /// walk point (a teleport instead of the walk) and the sentence (verb, object A, object B) is queued as <c>doSentence</c> does, so the game's own
    /// sentence script runs it on the next frame (it sets the object variables, faces the hero, runs the verb's code with its arguments). Elsewhere the
    /// verb's code is run directly (<see cref="ActivateObjectVerb"/>). An exploration action, never the player's click.
    /// </summary>
    public void PickInteraction(int objectA, int verb, int objectB = 0)
    {
        var sentence = _runtime.IsV2 ? 0 : _runtime.GlobalVariables.GetValueOrDefault(ScummVars.SentenceScript);
        if (sentence == 0)
        {
            ActivateObjectVerb(objectA, verb);
            return;
        }
        var ego = _runtime.GlobalVariables.TryGetValue(_runtime.EgoVariable, out var egoValue) && _runtime.Actors.Contains(egoValue) ? egoValue : 0;
        if (ego != 0 && _runtime.Actors[ego].Room == _runtime.CurrentRoom && _currentObjects.FirstOrDefault(item => item.Id == objectA) is { } record &&
            (record.WalkX, record.WalkY) != (0, 0))
            _runtime.Actors.Put(ego, record.WalkX, record.WalkY, CurrentPlacement(), ego);
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
    public bool StartSceneAt(string label, int offset)
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
                           "its local values start at 0, so what it shows may differ from the game's own way to it.");
        _visitTrace.Add($"picked scene: {label} started at +0x{offset:X4}");
        LastAction = new ScummObjectActionResult(here, 0, 0, 0, null, null, null, null, [], label);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            var local = System.Text.RegularExpressions.Regex.Match(label, @"^Room \d+ local script (\d+)$");
            if (global.Success || local.Success)
                RunStartedScript(int.Parse((global.Success ? global : local).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), offset,
                    new Dictionary<string, ushort>(), 0);
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
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A picked scene's setup (§8.6): the values kept, the fresh run and the run with them, and the note for Advanced.</summary>
public sealed record ScummScenePlan(IReadOnlyList<ScummSetupEdit> Edits, ScummCutsceneRunResult Base, ScummCutsceneRunResult Result, string Note)
{
    public bool HasEdits => Edits.Count > 0;
}

/// <summary>
/// Finds the state a picked scene needs (REQUIREMENTS §8.6, R-8.36; 31 October 2026), as LOOM's <c>LoomCutsceneReadiness</c> does for its story
/// bits, for any game: the trigger's own code (the scripts the scan reached for it) is read for what its tests compare (a bit, a story variable with a
/// constant, an object's state, an object the hero should hold), and each candidate is tried in a fresh run, one at a time; a value is kept when the
/// scene then begins, or goes on to more rooms, or prints more lines. Two passes, for values that matter only after another. The result is an estimate
/// of the story point the scene belongs to, never a played route.
/// </summary>
public sealed partial class ScummSceneSetupSearch
{
    /// <summary>Candidates tried per scene, in code order (each is one fresh run).</summary>
    public const int MaxCandidates = 16;

    /// <summary>Variables below this are the engine's own (ego, room, timers, input) in versions 3 to 5; their tests are not story state.</summary>
    public const int FirstStoryVariable = 0x40;

    private readonly GameLibrary _game;
    private readonly IScummDataSource _data;
    private readonly Func<ScummCutscene, IReadOnlyList<ScummSetupEdit>?, ScummCutsceneRunResult> _run;
    private readonly ConcurrentDictionary<(int, int, int), Lazy<ScummScenePlan>> _plans = [];
    private readonly Lazy<IReadOnlyDictionary<int, int>> _objectRooms;

    /// <param name="run">A headless run of the trigger from a fresh start with the given setup (<see cref="ScummCutsceneRun.Run"/>).</param>
    public ScummSceneSetupSearch(GameLibrary game, Func<ScummCutscene, IReadOnlyList<ScummSetupEdit>?, ScummCutsceneRunResult> run)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(run);
        _game = game;
        _data = ScummDataSources.For(game);
        _run = run;
        _objectRooms = new(ObjectRooms);
    }

    /// <summary>True when the trigger's plan is worked out (so the UI thread can ask without waiting).</summary>
    public bool IsReady(ScummCutscene trigger) => _plans.TryGetValue(Key(trigger), out var lazy) && lazy.IsValueCreated;

    /// <summary>The trigger's plan, worked out once and cached; <paramref name="fresh"/> is its run without setup.</summary>
    public ScummScenePlan Plan(ScummCutscene trigger, ScummCutsceneRunResult fresh) =>
        _plans.GetOrAdd(Key(trigger), _ => new(() => Search(trigger, fresh))).Value;

    private static (int, int, int) Key(ScummCutscene t) => (t.Room, t.ObjectId, t.VerbId);

    private ScummScenePlan Search(ScummCutscene trigger, ScummCutsceneRunResult fresh)
    {
        var candidates = Candidates(trigger).Take(MaxCandidates).ToList();
        var best = fresh;
        var kept = new List<ScummSetupEdit>();
        for (var pass = 0; pass < 2; pass++)
        {
            var changed = false;
            foreach (var candidate in candidates.Where(c => !kept.Any(k => k.Kind == c.Kind && k.Target == c.Target)))
            {
                var trial = new List<ScummSetupEdit>(kept) { candidate };
                var result = _run(trigger, trial);
                if (result.Failure is not null || result.Score <= best.Score) continue;
                best = result; kept = trial; changed = true;
            }
            if (!changed) break;
        }
        var plan = new ScummScenePlan(kept, fresh, best, "");
        return plan with { Note = Describe(plan, candidates.Count) };
    }

    private static string Describe(ScummScenePlan plan, int tried)
    {
        if (!plan.HasEdits)
            return plan.Base.Begun ? "" : tried == 0
                ? "No state its own code tests could be found to set, so it does not begin from here."
                : $"None of the {tried} values its own code tests made it begin; nothing was set.";
        var effect = !plan.Base.Begun ? "without them it does not begin"
            : $"from a fresh start it prints {plan.Base.Lines} lines, with them {plan.Result.Lines}";
        return $"Setup (estimate): {string.Join("; ", plan.Edits.Select(edit => edit.Describe()))}; a headless search kept what made the scene begin or " +
               $"go further ({effect}). The game sets these in earlier scenes that were not played; state the session already has is kept.";
    }

    /// <summary>
    /// What the trigger's own code tests, in code order: a bit (tried at 1), a story variable compared with a constant (tried at that constant, or 1 for a
    /// zero test), an object's state (<c>ifState</c>/<c>ifNotState</c>, or <c>getObjectState</c> into a variable then compared), and an object asked for
    /// its owner (given to the hero). Variables the same code writes are its own scratch, not state to set.
    /// </summary>
    public IReadOnlyList<ScummSetupEdit> Candidates(ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var units = trigger.Scripts.Select(label => (label, script: Read(label, trigger))).Where(u => u.script is not null).ToList();
        var written = new HashSet<string>();
        foreach (var (_, script) in units)
            foreach (var ins in script!.Instructions)
                if (WrittenVariable(ins) is { } key) written.Add(key);
        var found = new List<ScummSetupEdit>();
        void Add(ScummSetupEdit edit)
        {
            if (!found.Any(f => f.Kind == edit.Kind && f.Target == edit.Target && f.Value == edit.Value)) found.Add(edit);
        }
        foreach (var (label, script) in units)
        {
            var stateOf = new Dictionary<string, int>();
            var costumeOf = new Dictionary<string, int>();
            var roomOf = new Dictionary<string, int>();
            var objectCode = label.Contains(" object ", StringComparison.Ordinal);
            foreach (var ins in script!.Instructions)
            {
                var where = $"{label} +0x{ins.Offset:X4}";
                var parts = ins.Operands.Split(',', StringSplitOptions.TrimEntries);
                switch (ins.Operation)
                {
                    case "getObjectState" when parts.Length >= 2 && Literal(parts[1]) is int obj:
                        stateOf[parts[0]] = obj;
                        break;
                    case "getObjectState" when parts.Length >= 2 && objectCode && trigger.ObjectId != 0 && VariablePattern().Match(parts[1]) is { Success: true } self &&
                                               self.Value == parts[1] && !written.Contains(parts[1]):
                        // Version 5: the state of "this object" read through a variable the sentence script set (Monkey Island 2: var[0x007]).
                        stateOf[parts[0]] = trigger.ObjectId;
                        break;
                    case "getActorCostume" when parts.Length >= 2 && Literal(parts[1]) is int dressed:
                        costumeOf[parts[0]] = dressed;
                        break;
                    case "getActorRoom" when parts.Length >= 2 && Literal(parts[1]) is int placed:
                        roomOf[parts[0]] = placed;
                        break;
                    case "ifState" or "ifNotState" when parts.Length >= 2 && Literal(parts[1]) is int state &&
                                                        Literal(parts[0]) is int obj:
                        // ScummVM jumps when the test is false, so the scene may sit on either side: both values are tried.
                        Add(new(ScummSetupKind.ObjectState, obj.ToString(CultureInfo.InvariantCulture), state, where));
                        Add(new(ScummSetupKind.ObjectState, obj.ToString(CultureInfo.InvariantCulture), state == 0 ? 1 : 0, where));
                        break;
                    case "ifState" or "ifNotState" when parts.Length >= 2 && Literal(parts[1]) is int ownState && objectCode && trigger.ObjectId != 0 &&
                                                        VariablePattern().Match(parts[0]) is { Success: true } held && held.Value == parts[0] && !written.Contains(parts[0]):
                        // Object code that tests the state of a variable it does not set (Indy 3: var[0x007], "this object", which the sentence script
                        // sets, Script 2 +0x010A): the picked object's own states are tried; the trial runs keep only what makes the scene begin.
                        Add(new(ScummSetupKind.ObjectState, trigger.ObjectId.ToString(CultureInfo.InvariantCulture), ownState, where));
                        Add(new(ScummSetupKind.ObjectState, trigger.ObjectId.ToString(CultureInfo.InvariantCulture), ownState == 0 ? 1 : 0, where));
                        break;
                    case "getObjectOwner" when parts.Length >= 2 && Literal(parts[1]) is int held && _objectRooms.Value.TryGetValue(held, out var room):
                        Add(new(ScummSetupKind.Held, held.ToString(CultureInfo.InvariantCulture), 1, where, room));
                        break;
                    case "isEqual" or "isNotEqual" or "isLess" or "isGreater" or "isLessEqual" or "isGreaterEqual" or "equalZero" or "notEqualZero":
                    {
                        var tested = parts[0];
                        var value = ins.Operation is "equalZero" or "notEqualZero" ? 1 : parts.Length >= 2 && Literal(parts[1]) is int c ? c : (int?)null;
                        if (value is null) break;
                        if (stateOf.TryGetValue(tested, out var stateObject))
                            Add(new(ScummSetupKind.ObjectState, stateObject.ToString(CultureInfo.InvariantCulture), value.Value, where));
                        else if (costumeOf.TryGetValue(tested, out var dressedActor))
                            Add(new(ScummSetupKind.ActorCostume, dressedActor.ToString(CultureInfo.InvariantCulture), value.Value, where));
                        else if (roomOf.TryGetValue(tested, out var placedActor))
                            Add(new(ScummSetupKind.ActorRoom, placedActor.ToString(CultureInfo.InvariantCulture), value.Value, where));
                        else if (objectCode && tested == "local[0x000]" && value.Value != 0)
                            // An object no room holds (an inventory-only item, Monkey Island 2) is given without a room of its own code.
                            Add(new(ScummSetupKind.SecondObject, value.Value.ToString(CultureInfo.InvariantCulture), 1, where,
                                _objectRooms.Value.TryGetValue(value.Value, out var withRoom) ? withRoom : null));
                        else if (BitPattern().IsMatch(tested) && tested == BitPattern().Match(tested).Value)
                            Add(new(ScummSetupKind.Bit, tested, 1, where));
                        else if (VariablePattern().Match(tested) is { Success: true } v && tested == v.Value && !written.Contains(tested) &&
                                 int.Parse(v.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) >= FirstStoryVariable && value.Value != 0)
                            Add(new(ScummSetupKind.Variable, tested, value.Value, where));
                        break;
                    }
                }
            }
        }
        return found;
    }

    private static string? WrittenVariable(ScummScriptInstruction ins)
    {
        // The first operand of an assignment, an arithmetic step or a get* query is written.
        if (ins.Operation is not ("move" or "add" or "subtract" or "increment" or "decrement" or "expression" or "setVarRange" or "getRandomNr" or
            "getDist" or "findObject" or "getObjectOwner" or "getInventoryCount" or "findInventory" or "getObjectState" or "getVerbEntrypoint" or
            "isScriptRunning" or "isSoundRunning" or "actorFromPos" or "getClosestObjActor" or "getStringWidth") &&
            !ins.Operation.StartsWith("getActor", StringComparison.Ordinal)) return null;
        var first = ins.Operands.Split(',', 2)[0].Trim();
        return VariablePattern().Match(first) is { Success: true } m && m.Value == first ? first : null;
    }

    /// <summary>
    /// The first <c>cutscene</c> instruction in the trigger's scripts, in scan order (the label and the offset), for the last way to show a scene
    /// (<see cref="ScummSession.StartSceneAt"/>); null when none of them holds one.
    /// </summary>
    public static (string Label, int Offset)? FirstCutscene(GameLibrary game, ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var reader = new ScummSceneSetupSearch(game, (_, _) => new(false, [], 0));
        foreach (var label in trigger.Scripts)
            if (reader.Read(label, trigger)?.Instructions.FirstOrDefault(ins => ins.Operation == "cutscene") is { } start) return (label, start.Offset);
        return null;
    }

    private ScummScriptDisassembly? Read(string label, ScummCutscene trigger)
    {
        try
        {
            if (Regex.Match(label, @"^global script (\d+)$") is { Success: true } g) return _data.GlobalScript(int.Parse(g.Groups[1].Value, CultureInfo.InvariantCulture));
            if (Regex.Match(label, @"^Room (\d+) local script (\d+)$") is { Success: true } l)
                return _data.RoomLocalScript(int.Parse(l.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(l.Groups[2].Value, CultureInfo.InvariantCulture));
            if (Regex.Match(label, @"^Room (\d+) entry script$") is { Success: true } e) return _data.RoomEntryScript(int.Parse(e.Groups[1].Value, CultureInfo.InvariantCulture));
            if (Regex.Match(label, @"^Room (\d+) object (\d+) verb 0x([0-9A-F]+)$") is { Success: true } o)
            {
                var room = int.Parse(o.Groups[1].Value, CultureInfo.InvariantCulture);
                var id = int.Parse(o.Groups[2].Value, CultureInfo.InvariantCulture);
                var record = _data.ReadRoom(room).Objects.FirstOrDefault(r => r.Id == id);
                return record is null ? null : _data.ReadObjectVerb(room, record, Convert.ToInt32(o.Groups[3].Value, 16), out _);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
        return null;
    }

    /// <summary>The room each object lies in (its first room in index order), for an object given to the hero.</summary>
    private IReadOnlyDictionary<int, int> ObjectRooms()
    {
        var rooms = new Dictionary<int, int>();
        foreach (var room in _game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
        {
            try
            {
                foreach (var record in _data.ReadRoom(room).Objects) rooms.TryAdd(record.Id, room);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        }
        return rooms;
    }

    private static int? Literal(string text) =>
        text.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    [GeneratedRegex(@"^bit\[0x[0-9A-F]+\]")]
    private static partial Regex BitPattern();

    [GeneratedRegex(@"^var\[0x([0-9A-F]+)\]")]
    private static partial Regex VariablePattern();
}

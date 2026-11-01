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
    private readonly Lazy<IReadOnlySet<string>> _roomLeftVariables;

    /// <param name="run">A headless run of the trigger from a fresh start with the given setup (<see cref="ScummCutsceneRun.Run"/>).</param>
    public ScummSceneSetupSearch(GameLibrary game, Func<ScummCutscene, IReadOnlyList<ScummSetupEdit>?, ScummCutsceneRunResult> run)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(run);
        _game = game;
        _data = ScummDataSources.For(game);
        _run = run;
        _objectRooms = new(ObjectRooms);
        _roomLeftVariables = new(RoomLeftVariables);
    }

    /// <summary>
    /// The variables a global script fills with the current room (<c>move var[X] = VAR_ROOM</c>): the game's record of the room the hero left (its exit
    /// hook runs it as a room is left). A test of one of these against a room number is a test of where the hero came from.
    /// </summary>
    private IReadOnlySet<string> RoomLeftVariables()
    {
        var found = new HashSet<string>();
        foreach (var id in _data.GlobalScriptIds())
        {
            try
            {
                if (_data.GlobalScript(id) is not { } script) continue;
                foreach (var ins in script.Instructions.Where(i => i.Operation == "move"))
                    if (ins.Operands.Split(" = ") is [var target, var source] && source.Trim() == ScummVars.Room && VariablePattern().Match(target.Trim()) is { Success: true } m && m.Value == target.Trim())
                        found.Add(target.Trim());
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        }
        return found;
    }

    /// <summary>True when the trigger's plan is worked out (so the UI thread can ask without waiting).</summary>
    public bool IsReady(ScummCutscene trigger) => _plans.TryGetValue(Key(trigger), out var lazy) && lazy.IsValueCreated;

    /// <summary>The trigger's plan, worked out once and cached; <paramref name="fresh"/> is its run without setup.</summary>
    public ScummScenePlan Plan(ScummCutscene trigger, ScummCutsceneRunResult fresh) =>
        _plans.GetOrAdd(Key(trigger), _ => new(() => Search(trigger, fresh))).Value;

    private static (int, int, int) Key(ScummCutscene t) => (t.Room, t.ObjectId, t.VerbId);

    private ScummScenePlan Search(ScummCutscene trigger, ScummCutsceneRunResult fresh)
    {
        var ranked = Ranked(trigger);
        var candidates = ranked.Select(r => r.Edit).Take(MaxCandidates).ToList();
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
        // A scene that needs several values at once begins with none of them alone (Indy 4: a state and an item together): when it still does not
        // begin, all candidates are tried together, one value per target (the first found), as a last try before its cutscene is started directly.
        // First the values on the scene's side of every test that guards the way to its cutscene (Indy 4 Room 44: the room's variable and the
        // objects its branch tests, together), then all.
        var guards = ranked.Where(r => r.Rank == 0).Select(r => r.Edit).GroupBy(c => (c.Kind, c.Target)).Select(g => g.First()).ToList();
        if (!best.Begun && guards.Count > 1)
        {
            var result = _run(trigger, guards);
            if (result.Failure is null && result.Begun) { best = result; kept = guards; }
        }
        if (!best.Begun && candidates.Count > 1)
        {
            // Values known to lead away from the scene are left out.
            var together = ranked.Take(MaxCandidates).Where(r => r.Rank != 2).Select(r => r.Edit).GroupBy(c => (c.Kind, c.Target)).Select(g => g.First()).ToList();
            var result = _run(trigger, together);
            if (result.Failure is null && result.Begun) { best = result; kept = together; }
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
    public IReadOnlyList<ScummSetupEdit> Candidates(ScummCutscene trigger) => Ranked(trigger).Select(r => r.Edit).ToList();

    /// <summary>
    /// The values on the scene's side of the tests that guard the way to its cutscene, and of those after it that let more of it run (rank 3), one per
    /// target (1 November 2026), for a start at the cutscene.
    /// </summary>
    public IReadOnlyList<ScummSetupEdit> GuardValues(ScummCutscene trigger) =>
        Ranked(trigger).Where(r => r.Rank is 0 or 3).Select(r => r.Edit).GroupBy(c => (c.Kind, c.Target)).Select(g => g.First()).ToList();

    /// <summary>
    /// The candidates with their rank (1 November 2026): 0 for a value on the side of a test that leads on to the scene's cutscene, 2 for one on the
    /// side that leads away from it, 3 for one that lets more of the scene run after its cutscene instruction, 1 for the rest; ordered by rank (3 as
    /// 1), then code order.
    /// </summary>
    private List<(ScummSetupEdit Edit, int Rank)> Ranked(ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        // The trigger's own code first (the scan may list only the scripts it reaches: Indy 4's bread, Room 39 object 425, chains to local 208 after
        // its own owner tests), then the scripts the scan found.
        var own = trigger.IsRoomEntry ? $"Room {trigger.Room} entry script" : $"Room {trigger.Room} object {trigger.ObjectId} verb 0x{trigger.VerbId:X2}";
        (string Label, int Offset)? cutscene = null;
        foreach (var label in trigger.Scripts)
            if (Read(label, trigger)?.Instructions.FirstOrDefault(ins => ins.Operation == "cutscene") is { } start) { cutscene = (label, start.Offset); break; }
        // Then the scripts on the way from the trigger's code to the cutscene's script that the scan does not list.
        var between = OnTheWay(own, trigger, cutscene?.Label, out var toward);
        var units = new[] { own }.Concat(trigger.Scripts.Where(label => label != own)).Concat(between).Select(label => (label, script: Read(label, trigger)))
            .Where(u => u.script is not null).ToList();
        var written = new HashSet<string>();
        foreach (var (_, script) in units)
            foreach (var ins in script!.Instructions)
                if (WrittenVariable(ins) is { } key) written.Add(key);
        var found = new List<(ScummSetupEdit Edit, int Rank)>();
        bool? guard = null;
        var afterCutscene = false;
        var operation = "";
        int? constant = null;
        // tested: the value the test sees once the edit is applied (null when the edit is not the tested value itself).
        void Add(ScummSetupEdit edit, int? tested = null)
        {
            var rank = guard is bool need && tested is int v && Holds(operation, constant, v) is bool holds ? holds == need ? 0 : 2
                : afterCutscene && tested is int after && Holds(operation, constant, after) == true ? 3 : 1;
            var at = found.FindIndex(f => f.Edit.Kind == edit.Kind && f.Edit.Target == edit.Target && f.Edit.Value == edit.Value);
            if (at < 0) found.Add((edit, rank));
            else if (rank < found[at].Rank) found[at] = (found[at].Edit, rank);
        }
        foreach (var (label, script) in units)
        {
            var guardSides = GuardSides(label, script!, cutscene, toward, trigger, out var moreScene);
            var stateOf = new Dictionary<string, int>();
            var costumeOf = new Dictionary<string, string>();
            var roomOf = new Dictionary<string, string>();
            var boxOf = new Dictionary<string, string>();
            var ownerOf = new Dictionary<string, int>();
            var xOf = new Dictionary<string, string>();
            var runningOf = new Dictionary<string, int>();
            var objectCode = label.Contains(" object ", StringComparison.Ordinal);
            // A script the trigger's code starts or chains to reads its arguments as its locals: literals (Indy 4 Room 7's cat figurine:
            // chainScript 206, [0x0074, 0x0009], whose local 206 tests the class of local[0x000], the object 116), "this object" (Monkey Island 2's
            // coffin, Room 18: startScript 203, [var[0x007]]) and the second object of a two-object verb (Indy 4 Room 43: chainScript 210,
            // [0x0206, local[0x000]], whose local[0x001] is compared with the shovel).
            var arguments = label == own ? null : CallOperands(label, trigger);
            // Locals that hold "this object" (Indy 4 Script 63 +0x0005: move local[0x000] = var[0x007] when no argument gave one).
            var selfLocals = new HashSet<string>();
            string? Argument(string operand) =>
                arguments is not null && LocalPattern().Match(operand) is { Success: true } local && local.Value == operand &&
                int.Parse(local.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) is var index && index < arguments.Count ? arguments[index] : null;
            int? Known(string operand) => Literal(operand) ?? (Argument(operand) is { } given ? Resolve(given, trigger) : null) ??
                (selfLocals.Contains(operand) ? trigger.ObjectId : null);
            foreach (var ins in script!.Instructions)
            {
                var where = $"{label} +0x{ins.Offset:X4}";
                var parts = ins.Operands.Split(',', StringSplitOptions.TrimEntries).Select(Flatten).ToArray();
                // A variable written again no longer holds what it held (Indy 4 Room 44's entry reads an object's state into var[0x000], then
                // Sophia's room: the later comparison is about her).
                if (WrittenVariable(ins) is { } rewritten)
                {
                    stateOf.Remove(rewritten); costumeOf.Remove(rewritten); roomOf.Remove(rewritten); boxOf.Remove(rewritten); ownerOf.Remove(rewritten); xOf.Remove(rewritten);
                    runningOf.Remove(rewritten);
                }
                guard = guardSides.TryGetValue(ins.Offset, out var need) ? need : null;
                afterCutscene = moreScene.Contains(ins.Offset);
                operation = ins.Operation;
                constant = parts.Length >= 2 && Literal(parts[1]) is int k ? k : parts.Length >= 2 && parts[1] == ScummVars.Room ? trigger.Room : null;
                switch (ins.Operation)
                {
                    case "move" when !objectCode && trigger.ObjectId != 0 && !trigger.IsRoomEntry && ins.Operands.Split(" = ") is [var into, var from] &&
                                     from.Trim() == ThisObject && into.Trim().StartsWith("local[", StringComparison.Ordinal):
                        selfLocals.Add(into.Trim());
                        break;
                    case "isScriptRunning" when parts.Length >= 2 && Literal(parts[1]) is int checkedScript:
                        runningOf[parts[0]] = checkedScript;
                        break;
                    case "getObjectState" when parts.Length >= 2 && Known(parts[1]) is int obj:
                        stateOf[parts[0]] = obj;
                        break;
                    case "getObjectState" when parts.Length >= 2 && objectCode && trigger.ObjectId != 0 && VariablePattern().Match(parts[1]) is { Success: true } self &&
                                               self.Value == parts[1] && !written.Contains(parts[1]):
                        // Version 5: the state of "this object" read through a variable the sentence script set (Monkey Island 2: var[0x007]).
                        stateOf[parts[0]] = trigger.ObjectId;
                        break;
                    case "getActorCostume" when parts.Length >= 2 && Actor(parts[1]) is { } dressed:
                        costumeOf[parts[0]] = dressed;
                        break;
                    case "getActorWalkBox" when parts.Length >= 2 && (Literal(parts[1])?.ToString(CultureInfo.InvariantCulture) ?? Ego(parts[1])) is { } boxed:
                        boxOf[parts[0]] = boxed;
                        break;
                    case "expression" when ExpressionWalkBox().Match(ins.Operands) is { Success: true } boxed &&
                                           (Literal(boxed.Groups[2].Value)?.ToString(CultureInfo.InvariantCulture) ?? Ego(boxed.Groups[2].Value)) is { } inBox:
                        // The same read inside an expression (Indy 4 Room 44's local script 203 +0x0012: local[0x001] = getActorWalkBox(…, 0x01)).
                        boxOf[boxed.Groups[1].Value] = inBox;
                        break;
                    case "getActorX" when parts.Length >= 2 && (Literal(parts[1])?.ToString(CultureInfo.InvariantCulture) ?? Ego(parts[1])) is { } across:
                        xOf[parts[0]] = across;
                        break;
                    case "getActorRoom" when parts.Length >= 2 && Actor(parts[1]) is { } placed:
                        roomOf[parts[0]] = placed;
                        break;
                    case "ifState" or "ifNotState" when parts.Length >= 2 && Literal(parts[1]) is int state &&
                                                        Known(parts[0]) is int obj:
                        // ScummVM jumps when the test is false, so the scene may sit on either side: both values are tried.
                        Add(new(ScummSetupKind.ObjectState, obj.ToString(CultureInfo.InvariantCulture), state, where), state);
                        Add(new(ScummSetupKind.ObjectState, obj.ToString(CultureInfo.InvariantCulture), state == 0 ? 1 : 0, where), state == 0 ? 1 : 0);
                        break;
                    case "ifState" or "ifNotState" when parts.Length >= 2 && Literal(parts[1]) is int ownState && objectCode && trigger.ObjectId != 0 &&
                                                        VariablePattern().Match(parts[0]) is { Success: true } held && held.Value == parts[0] && !written.Contains(parts[0]):
                        // Object code that tests the state of a variable it does not set (Indy 3: var[0x007], "this object", which the sentence script
                        // sets, Script 2 +0x010A): the picked object's own states are tried; the trial runs keep only what makes the scene begin.
                        Add(new(ScummSetupKind.ObjectState, trigger.ObjectId.ToString(CultureInfo.InvariantCulture), ownState, where), ownState);
                        Add(new(ScummSetupKind.ObjectState, trigger.ObjectId.ToString(CultureInfo.InvariantCulture), ownState == 0 ? 1 : 0, where), ownState == 0 ? 1 : 0);
                        break;
                    case "getObjectOwner" when parts.Length >= 2 && objectCode && trigger.ObjectId != 0 && Literal(parts[1]) is null &&
                                               VariablePattern().Match(parts[1]) is { Success: true } selfOwner && selfOwner.Value == parts[1] && !written.Contains(parts[1]):
                        // The owner of "this object" (a variable the sentence script set: Indy 3 var[0x007]): the picked object itself held (Indy 3
                        // Room 17's books: Look shows the inscription only once the book is held, +0x0000 getObjectOwner then isEqual 0x0F).
                        Add(new(ScummSetupKind.Held, trigger.ObjectId.ToString(CultureInfo.InvariantCulture), 1, where, trigger.Room));
                        break;
                    case "getObjectOwner" when parts.Length >= 2 && Known(parts[1]) is int held:
                        ownerOf[parts[0]] = held;
                        // An item no room holds (inventory-only: Indy 4's sandwich makings) is given without a room of its own code.
                        Add(new(ScummSetupKind.Held, held.ToString(CultureInfo.InvariantCulture), 1, where,
                            _objectRooms.Value.TryGetValue(held, out var room) ? room : null));
                        break;
                    case "ifClassOfIs" when ins.Operands.IndexOf(", [", StringComparison.Ordinal) + 2 is var open and > 1 &&
                                            (Known(parts[0]) ?? (objectCode && trigger.ObjectId != 0 && parts[0] == "var[0x007]" ? trigger.ObjectId : null)) is int classed and > 0:
                    {
                        // The list is the bracket after the object (not the one in var[0x007]). ScummVM o5_ifClassOfIs: a class with bit 0x80 must be set, one without must be clear, and the code after the test runs when
                        // all hold; the scene may sit on either side, so both are tried (Indy 4's cupboard, Room 92 object 1194 +0x0000).
                        foreach (var item in ins.Operands[(open + 1)..ins.Operands.IndexOf(']', open)].Split(',', StringSplitOptions.TrimEntries))
                            if (Literal(item) is int cls && (cls & 0x7F) is > 0 and <= 32)
                            {
                                var number = cls & 0x7F;
                                // The first holds the test's condition for this class (tested 1), the second breaks it (0).
                                Add(new(ScummSetupKind.ObjectClass, classed.ToString(CultureInfo.InvariantCulture), (cls & 0x80) != 0 ? number : -number, where), 1);
                                Add(new(ScummSetupKind.ObjectClass, classed.ToString(CultureInfo.InvariantCulture), (cls & 0x80) != 0 ? -number : number, where), 0);
                            }
                        break;
                    }
                    case "isEqual" or "isNotEqual" or "isLess" or "isGreater" or "isLessEqual" or "isGreaterEqual" or "equalZero" or "notEqualZero":
                    {
                        var tested = parts[0];
                        // A comparison with VAR_ROOM is one with the scene's room (Indy 4 Room 48's ladder: getActorRoom of var[0x0B6], Sophia, then
                        // isEqual var[0x004]: is she here).
                        var value = ins.Operation is "equalZero" or "notEqualZero" ? 1 : parts.Length >= 2 && Literal(parts[1]) is int c ? c
                            : parts.Length >= 2 && parts[1] == ScummVars.Room ? trigger.Room : (int?)null;
                        if (value is null) break;
                        // Both sides of the test (ScummVM jumps when it is false, and the scene may be on either side): the constant and, for an
                        // inequality, a value on its other side (Indy 4's closet door, Room 65 object 875: getObjectState then isNotEqual 0, so state 1).
                        var sides = ins.Operation switch
                        {
                            "isLess" or "isGreater" or "isLessEqual" or "isGreaterEqual" => new[] { value.Value, value.Value + 1, value.Value - 1 },
                            "equalZero" or "notEqualZero" => [1, 0],
                            _ => [value.Value, value.Value == 0 ? 1 : 0],
                        };
                        if (ownerOf.TryGetValue(tested, out var ownedObject))
                        {
                            // Owned by another actor (Indy 3 Room 82's cracked seal, +0x0005: the Grail's owner is 3, Elsa); 15 is the room. Only on
                            // the way to the cutscene (an owner test of the hero is the "held" value already).
                            if (ins.Operation is "isEqual" or "isNotEqual" && value.Value is > 0 and < 15 &&
                                guard is bool toScene && Holds(ins.Operation, value.Value, value.Value) == toScene)
                                Add(new(ScummSetupKind.OwnedBy, ownedObject.ToString(CultureInfo.InvariantCulture), value.Value, where), value.Value);
                        }
                        else if (stateOf.TryGetValue(tested, out var stateObject))
                            foreach (var side in sides.Where(v => v >= 0).Distinct())
                                Add(new(ScummSetupKind.ObjectState, stateObject.ToString(CultureInfo.InvariantCulture), side, where), side);
                        else if (costumeOf.TryGetValue(tested, out var dressedActor))
                            Add(new(ScummSetupKind.ActorCostume, dressedActor, value.Value, where), value.Value);
                        else if (boxOf.TryGetValue(tested, out var boxedActor))
                        {
                            // A script that waits for the hero to walk into a box (LOOM Room 10's local script 200: getActorWalkBox of actor 1, then
                            // isEqual 3): the box it waits for, the actor stood in it after the room is entered.
                            if (ins.Operation is "isEqual" or "isNotEqual" && value.Value > 0)
                                Add(new(ScummSetupKind.WalkBox, boxedActor, value.Value, where, trigger.Room), value.Value);
                        }
                        else if (xOf.TryGetValue(tested, out var acrossActor))
                        {
                            // A wait for the actor to walk past a point (Monkey Island 1 Room 72's local script 201: getActorX of the hero, then
                            // isGreater 0xB4): only on the way to the cutscene, the actor stood on the scene's side.
                            foreach (var side in sides.Where(v => v > 0).Distinct())
                                if (guard is bool toScene && Holds(ins.Operation, value.Value, side) == toScene)
                                    Add(new(ScummSetupKind.ActorX, acrossActor, side, where, trigger.Room), side);
                        }
                        else if (runningOf.TryGetValue(tested, out var runningScript))
                        {
                            // A script that must be running (Indy 3 Room 32's painting, object 489 verb 0x0A: isScriptRunning 114, then the
                            // cutscene's Script 115 only while it runs): only on the way to the cutscene, started in the scene's room.
                            if (guard is bool toScene && Holds(ins.Operation, value.Value, 1) == toScene)
                                Add(new(ScummSetupKind.ScriptRunning, runningScript.ToString(CultureInfo.InvariantCulture), 1, where, trigger.Room), 1);
                        }
                        else if (tested == ScummVars.CameraPosX)
                        {
                            // A wait for the camera to scroll past a point (Indy 3 Room 86's local script 201 +0x0026: isGreater VAR_CAMERA_POS_X,
                            // 0x186, back until it holds): the camera taken there, on the scene's side only.
                            foreach (var side in sides.Where(v => v > 0).Distinct())
                                if (guard is bool toScene && Holds(ins.Operation, value.Value, side) == toScene)
                                    Add(new(ScummSetupKind.CameraX, ScummVars.CameraPosX, side, where, trigger.Room), side);
                        }
                        else if (roomOf.TryGetValue(tested, out var placedActor))
                            Add(new(ScummSetupKind.ActorRoom, placedActor, value.Value, where), value.Value);
                        else if ((objectCode && tested == "local[0x000]" || Argument(tested) == SecondObjectArgument) && value.Value != 0)
                            // An object no room holds (an inventory-only item, Monkey Island 2) is given without a room of its own code.
                            Add(new(ScummSetupKind.SecondObject, value.Value.ToString(CultureInfo.InvariantCulture), 1, where,
                                _objectRooms.Value.TryGetValue(value.Value, out var withRoom) ? withRoom : null), value.Value);
                        else if (tested == ScummVars.WalkToObject && value.Value > 0 && ins.Operation is "isEqual" or "isNotEqual" && trigger.IsRoomEntry)
                            // The door the hero came in by (VAR_WALKTO_OBJ, set while loadRoomWithEgo enters the room).
                            Add(new(ScummSetupKind.EnteredBy, tested, value.Value, where, trigger.Room), value.Value);
                        else if (tested == ScummVars.Room && value.Value >= 0x80 && ins.Operation is "isEqual" or "isNotEqual")
                            // The room under a pseudo room number (Indy 3 Room 21's entry: isEqual VAR_ROOM, 0x81): applied only where the game maps it here.
                            Add(new(ScummSetupKind.EnteredAs, ScummVars.Room, value.Value, where, trigger.Room), value.Value);
                        else if (BitPattern().IsMatch(tested) && tested == BitPattern().Match(tested).Value)
                            Add(new(ScummSetupKind.Bit, tested, 1, where), 1);
                        else if (_roomLeftVariables.Value.Contains(tested) && ins.Operation is "isEqual" or "isNotEqual" &&
                                 (_game.Rooms.Any(r => r.ResourceId == value.Value) || value.Value >= 0x80) &&
                                 (value.Value != trigger.Room || trigger.IsRoomEntry))
                            // A room, a pseudo room (Indy 4 Room 57's entry: 163), or the scene's own room left and entered again (Room 33).
                            Add(new(ScummSetupKind.CameFrom, tested, value.Value, where, trigger.Room), value.Value);
                        else if (VariablePattern().Match(tested) is { Success: true } v && tested == v.Value && !written.Contains(tested) &&
                                 int.Parse(v.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) >= FirstStoryVariable)
                            foreach (var side in sides.Where(v => v > 0).Distinct())
                                Add(new(ScummSetupKind.Variable, tested, side, where), side);
                        break;
                    }
                }
            }
        }
        // An object's scene may depend on how its room was entered: the doors the room's entry compares VAR_WALKTO_OBJ with (Indy 3 Room 82's
        // entry +0x0072: the grail chamber set up differently by door 861 or 862) are tried last.
        if (!trigger.IsRoomEntry && Read($"Room {trigger.Room} entry script", trigger) is { } entry)
            foreach (var ins in entry.Instructions.Where(i => i.Operation is "isEqual" or "isNotEqual"))
                if (ins.Operands.Split(',', StringSplitOptions.TrimEntries) is [var tested, var door, ..] && tested == ScummVars.WalkToObject &&
                    Literal(door) is int byDoor and > 0 &&
                    !found.Any(f => f.Edit.Kind == ScummSetupKind.EnteredBy && f.Edit.Value == byDoor))
                    found.Add((new(ScummSetupKind.EnteredBy, tested, byDoor, $"Room {trigger.Room} entry script +0x{ins.Offset:X4}", trigger.Room), 1));
        // A value that lets more of the scene run (3) keeps its place among the others (1) in the search.
        return found.OrderBy(f => f.Rank == 3 ? 1 : f.Rank).ToList();
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
    public static (string Label, int Offset, IReadOnlyList<int>? Arguments)? FirstCutscene(GameLibrary game, ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var reader = new ScummSceneSetupSearch(game, (_, _) => new(false, [], 0));
        foreach (var label in trigger.Scripts)
            if (reader.Read(label, trigger)?.Instructions.FirstOrDefault(ins => ins.Operation == "cutscene") is { } start)
                return (label, start.Offset, reader.CallArguments(label, trigger));
        return null;
    }

    /// <summary>
    /// The arguments the trigger's own code starts a script with (<c>startScript</c>/<c>chainScript N, [a, b, …]</c>), so a scene started at its
    /// cutscene gets the locals its caller would have given it (Indy 3 Room 13's entry starts Script 131 with 0xCB): literals and "this object" (the
    /// picked object); the second object of a two-object verb is not known here and is 0. Null when no call gives arguments the reading knows.
    /// </summary>
    private IReadOnlyList<int>? CallArguments(string label, ScummCutscene trigger) =>
        CallOperands(label, trigger)?.Select(operand => Resolve(operand, trigger) ?? 0).ToArray();

    /// <summary>The operand of a call argument that is the second object of the trigger's two-object verb (its code's <c>local[0x000]</c>).</summary>
    private const string SecondObjectArgument = "second object";

    /// <summary>The variable the sentence script sets to "this object" before an object's verb code runs (Indy 3, Monkey Island 2, Indy 4).</summary>
    private const string ThisObject = "var[0x007]";

    /// <summary>A call argument's value: a literal, or "this object" (the picked object); null for the second object or anything else.</summary>
    private static int? Resolve(string operand, ScummCutscene trigger) =>
        Literal(operand) ?? (operand == ThisObject && trigger.ObjectId != 0 && !trigger.IsRoomEntry ? trigger.ObjectId : null);

    /// <summary>
    /// The operands of the first call of the script <paramref name="label"/> by the trigger's code (its own, then the scripts the scan lists) whose
    /// arguments are all known: literals, "this object" (<c>var[0x007]</c> in object code) and the verb's second object (<c>local[0x000]</c> in
    /// object code, returned as <see cref="SecondObjectArgument"/>).
    /// </summary>
    private IReadOnlyList<string>? CallOperands(string label, ScummCutscene trigger)
    {
        var target = Regex.Match(label, @"^(?:global script|Room \d+ local script) (\d+)$");
        if (!target.Success) return null;
        var id = int.Parse(target.Groups[1].Value, CultureInfo.InvariantCulture);
        // The trigger's own code first (the scan may list only the scripts it reaches).
        var own = trigger.IsRoomEntry ? $"Room {trigger.Room} entry script" : $"Room {trigger.Room} object {trigger.ObjectId} verb 0x{trigger.VerbId:X2}";
        foreach (var caller in new[] { own }.Concat(trigger.Scripts.Where(label => label != own)))
        {
            if (Read(caller, trigger) is not { } script) continue;
            var objectCode = caller.Contains(" object ", StringComparison.Ordinal);
            foreach (var ins in script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
            {
                var open = ins.Operands.IndexOf('[');
                if (open < 0 || Literal(ins.Operands[..open].Split(',')[0].Trim()) != id) continue;
                // Only the list's own closing bracket (an argument may be var[0x007]).
                var inside = ins.Operands[(open + 1)..].TrimEnd() is var list && list.EndsWith(']') ? list[..^1] : ins.Operands[(open + 1)..];
                if (inside.Length == 0) return [];
                var values = inside.Split(',', StringSplitOptions.TrimEntries)
                    .Select(operand => Literal(operand) is not null || objectCode && operand == ThisObject ? operand
                        : objectCode && operand == "local[0x000]" ? SecondObjectArgument : null).ToArray();
                if (values.All(v => v is not null)) return values!;
            }
        }
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

    /// <summary>"ego" for the variable that holds the hero's actor number (<c>VAR_EGO</c>), so a setup stands whoever the hero is; null otherwise.</summary>
    private static string? Ego(string text) => text == ScummVars.Ego ? "ego" : null;

    /// <summary>
    /// The actor an operand names: a number, "ego", or a global variable that holds the actor's number (Indy 4 var[0x0B6], Sophia), read when the
    /// setup is applied; null for anything else (a local, an expression).
    /// </summary>
    private static string? Actor(string text) =>
        Literal(text)?.ToString(CultureInfo.InvariantCulture) ?? Ego(text) ??
        (text.StartsWith("var[", StringComparison.Ordinal) && VariablePattern().Match(text) is { Success: true } m && m.Value == text
            ? text : null);


    private static int? Literal(string text) =>
        text.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>A variable indexed by a literal (<c>bit[0x057+0x007]</c>, Indy 4 Room 67's rope ladder) as the one it names (<c>bit[0x05E]</c>).</summary>
    private static string Flatten(string operand) =>
        LiteralIndexed().Match(operand) is { Success: true } m
            ? $"{m.Groups[1].Value}[0x{int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) + int.Parse(m.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture):X3}]"
            : operand;

    [GeneratedRegex(@"^(bit|var|local)\[0x([0-9A-F]+)\+0x([0-9A-F]+)\]$")]
    private static partial Regex LiteralIndexed();

    [GeneratedRegex(@"^(\S+), \[getActorWalkBox\([^,]+, ([^)]+)\)\]$")]
    private static partial Regex ExpressionWalkBox();

    [GeneratedRegex(@"^bit\[0x[0-9A-F]+\]")]
    private static partial Regex BitPattern();

    [GeneratedRegex(@"^local\[0x([0-9A-F]+)\]")]
    private static partial Regex LocalPattern();

    [GeneratedRegex(@"^var\[0x([0-9A-F]+)\]")]
    private static partial Regex VariablePattern();
}

using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The game-independent exploration session: a conservative, route-scoped run of a SCUMM game's own scripts. Only instructions reached
/// by bounded execution change the snapshot; unsupported state or timing is recorded and prevents the snapshot from being labelled
/// faithful. This part holds the script runner with child-script tracking (start, chain, stop, freeze, cutscenes and overrides), the
/// script data and the route records. A game's session derives from it and adds the game's boot, input and state
/// (<c>SCUMMexplorer.Core.Loom.LoomExplorationSession</c> is the one so far). It never tests which game it runs.
/// </summary>
public abstract partial class ScummSession
{
    protected const int MaxNestedScripts = 8;
    protected readonly GameLibrary _game;
    protected readonly ScummSoundClock _soundClock;
    protected readonly IScummDataSource _data;
    protected readonly ScummBoundedScriptExecutor _executor = new();
    protected readonly ScummScriptObserver? _observer;
    protected readonly ScummScriptRuntimeState _runtime;
    protected readonly int _globalScriptCount;
    protected readonly UncertaintyList _uncertainties = [];
    protected readonly List<PendingScript> _pending = [];
    protected GameResource? _currentRoom;
    protected IReadOnlyList<ScummRoomObjectRecord> _currentObjects = [];

    /// <summary>The soundcard-independent part of the session's set-up: data, runtime state and the script clock hooks.</summary>
    /// <param name="soundLifetime">Reads how long a native sound resource plays (the game's own sound formats are the caller's knowledge).</param>
    protected ScummSession(GameLibrary game, ScummIndexedObjectSnapshot indexedObjects, int randomSeed,
        ScummScriptObserver? observer, IScummDataSource? data, Func<GameResource, ScummSoundLifetime> soundLifetime)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(indexedObjects);
        ArgumentNullException.ThrowIfNull(soundLifetime);
        _game = game;
        _observer = observer;
        var soundResources = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
            .GroupBy(r => r.ResourceId!.Value).ToDictionary(group => group.Key, group => group.First());
        var soundLifetimes = new Dictionary<int, ScummSoundLifetime>();
        _soundClock = new ScummSoundClock(id =>
        {
            if (!soundLifetimes.TryGetValue(id, out var lifetime))
                soundLifetimes[id] = lifetime = soundResources.TryGetValue(id, out var resource)
                    ? soundLifetime(resource)
                    : new(ScummSoundLifetimeKind.Unknown, null, false, "no indexed native sound resource");
            return lifetime;
        });
        _data = data ?? ScummDataSources.For(game);
        _runtime = new ScummScriptRuntimeState(indexedObjects.States, indexedObjects.Owners, indexedObjects.Classes,
            useV4Walking: ScummProfile.For(game).UsesV4Walking)
        {
            Random = new ScummSeededRandom(randomSeed),
            MutationObserver = observer
        };
        _globalScriptCount = _data.GlobalScriptCount;
        _runtime.ScriptRunning = IsScriptRunning;
        _runtime.SoundRunning = _soundClock.Status;
        _runtime.OverrideChanged = RecordOverride;
    }

    /// <summary>True for the verb of a look or use sentence an object script starts (counted in <see cref="ObjectSentencesBegun"/>); the game's verb numbers are the derived session's knowledge.</summary>
    protected virtual bool IsObjectSentence(int verbId) => false;


    /// <summary>
    /// True while a cutscene begun on this route is open: its script is still
    /// running and has not reached <c>endCutscene</c> (ScummVM's cutscene stack).
    /// The player has no control then, so a click's outcome is where it ends.
    /// </summary>
    public bool InCutscene => _cutscenes.Any(item => IsAlive(item.Owner));

    /// <summary>
    /// How many cutscenes this session has begun (<c>beginCutscene</c> reached). The app compares it
    /// before and after an input, with <see cref="EgoPuts"/>, to tell whether the game answered it with a change of scene.
    /// </summary>
    public int CutscenesBegun { get; protected set; }

    /// <summary>Look/use sentences actually started by startObject (input script 007), excluding the preliminary walk-to verb.</summary>
    public int ObjectSentencesBegun { get; protected set; }

    /// <summary>
    /// How many times a script has put the ego at a point (<see cref="ScummActorTable.EgoPuts"/>). With
    /// <see cref="CutscenesBegun"/> it marks a change of scene: Room 02's script 045 begins a cutscene and
    /// <c>putActor</c>s the ego on the next stretch of the path, which ends the click's walk.
    /// </summary>
    public int EgoPuts => _runtime.Actors.EgoPuts;

    /// <summary>
    /// Test seam: gives a global word a value, as reached code would, so tests
    /// can exercise hooks the boot has not set. Never used by the app.
    /// </summary>
    internal void SetGlobalWord(int variable, ushort value) =>
        _runtime.SetGlobalVariable($"var[0x{variable:X3}]", value);

    /// <summary>Test seam: runs a synthetic script at the top level, as a frame would. Never used by the app.</summary>
    internal ScummBoundedScriptResult RunTopLevelScript(ScummScriptDisassembly script) =>
        RunScript(script, _currentObjects, 0, null, 0);

    protected bool IsAlive(ScriptFrame? frame) =>
        frame is not null && (_executing.Contains(frame) || _pending.Any(item => item.Frame == frame));

    /// <summary>Seed of the stand-in random source; every <c>getRandomNr</c> value is a labelled pick from it.</summary>
    public int RandomSeed => _runtime.Random.Seed;
    public ScummScriptDisassembly? LastEntryScript { get; protected set; }

    /// <summary>
    /// The current room's walk boxes, read afresh on each entry as ScummVM's
    /// <c>resetRoomSubBlocks</c> does; <c>setBoxFlags</c> changes this copy. Null
    /// when no room is loaded or its box table could not be read.
    /// </summary>
    public ScummWalkBoxSet? CurrentWalkBoxes => _runtime.WalkBoxes;

    /// <summary>The room the session is in now (the room the game last loaded), without building a snapshot.</summary>
    public int? CurrentRoomId => _runtime.CurrentRoom;

    protected readonly List<int> _roomsEntered = [];

    /// <summary>Every room the session has loaded, in order (a room a script enters and leaves within one frame is here too).</summary>
    public IReadOnlyList<int> RoomsEntered => _roomsEntered;

    /// <summary>The current class bits per object or actor number (a live view; the snapshot holds a copy).</summary>
    public IReadOnlyDictionary<int, uint> ObjectClasses => _runtime.ObjectClasses;
    public ScummBoundedScriptResult? LastEntryRun { get; protected set; }
    public ScummObjectActionResult? LastAction { get; protected set; }
    public IReadOnlyList<ScummScriptEvent> LastVisitEvents { get; protected set; } = [];
    public IReadOnlyList<ScummScriptEvent> ReachedSoundEvents => _visitEvents.Where(item =>
        item.Certainty == ScummScriptEventCertainty.Reached && item.Kind is
            ScummScriptEventKind.MusicStarted or ScummScriptEventKind.SoundStarted or
            ScummScriptEventKind.MusicStopped or ScummScriptEventKind.SoundStopped).ToArray();

    /// <summary>
    /// Every sound command the session met, in route order, with the room that was
    /// current and the room whose code started the chain of scripts that ran it.
    /// </summary>
    public IReadOnlyList<ScummSessionSoundEvent> SoundEvents => _soundEvents;

    /// <summary>The route position at which the current room was last entered (after the previous room's exit code).</summary>
    public int RoomEntrySequence { get; protected set; }
    public ScummSessionSnapshot Snapshot => new(
        _currentRoom?.ResourceId,
        new Dictionary<int, byte>(_runtime.ObjectStates),
        new Dictionary<int, RoomObjectPosition>(_runtime.ObjectPositions),
        new Dictionary<int, byte>(_runtime.ObjectOwners),
        new Dictionary<string, ushort>(_runtime.GlobalVariables),
        KnownInventory(),
        _runtime.GlobalVariables.ContainsKey(ScummVars.Ego),
        _runtime.ActiveSounds.Order().ToArray(),
        _pending.Count,
        _uncertainties.ToArray(),
        _runtime.DrawQueue.ToArray(),
        _runtime.StatesSetInRoom.Order().ToArray(),
        _picks.ToArray(),
        _runtime.EstimatedStates.Order().ToArray(),
        new Dictionary<int, uint>(_runtime.ObjectClasses),
        _runtime.Messages.OnScreen);

    /// <summary>The message state: the talk message, its delay and <c>VAR_HAVE_MSG</c>, and drawn strings.</summary>
    public ScummMessageTable Messages => _runtime.Messages;

    protected int[] KnownInventory() => _runtime.GlobalVariables.TryGetValue(ScummVars.Ego, out var ego)
        ? _runtime.ObjectOwners.Where(pair => pair.Value == ego).Select(pair => pair.Key).Order().ToArray()
        : [];

    // A selected execution may carry estimates; a merely discovered load is not execution.
    protected static bool IsExecutedRoomLoad(ScummScriptEvent item) =>
        item.Kind == ScummScriptEventKind.RoomLoaded && item.Executed &&
        item.Certainty is ScummScriptEventCertainty.Reached or ScummScriptEventCertainty.Conditional;

    /// <summary>ScummVM <c>VAR_CUTSCENEEXIT_KEY</c> (v3: var 24); the boot sets it to 0x1B (script 001 +0x012C).</summary>
    protected const string CutsceneExitKeyVariable = "var[0x018]";

    /// <summary>
    /// ScummEngine::isScriptRunning: a global or local script slot with this
    /// number is running (executing, waiting for a later frame, or frozen).
    /// A script that ended at a step the preview does not model might still
    /// run in the game, so that answer is an estimate.
    /// </summary>
    protected (bool Running, string? Estimate) IsScriptRunning(int number)
    {
        var running = _executing.Any(frame => frame.Number == number && !frame.Stopped) ||
                      _pending.Any(item => item.ScriptNumber == number && !item.Frame.Stopped) ||
                      _frameScripts.Any(item => item.ScriptNumber == number && !item.Frame.Stopped);
        return running || !_endedUnmodelled.Contains(number)
            ? (running, null)
            : (false, $"script {number} ended at a step the preview does not model, so it may still run in the game");
    }

    protected readonly List<ScummScriptEvent> _visitEvents = [];
    protected readonly List<ScummSessionSoundEvent> _soundEvents = [];
    protected readonly List<string> _visitTrace = [];
    protected readonly List<ScummRandomPick> _picks = [];
    // Scripts running now, outermost first; the last one is the current script.
    protected readonly List<ScriptFrame> _executing = [];
    // Slots still awaiting their turn this frame remain visible to nested
    // freeze/unfreeze and isScriptRunning, as vm.slot does in runAllScripts.
    protected readonly List<PendingScript> _frameScripts = [];
    // beginCutscene's vm.cutSceneData stack, and the script whose cutscene start
    // script is running (vm.cutSceneScriptIndex), which freezeScripts leaves running.
    protected readonly Stack<(ushort Data, ScriptFrame? Owner)> _cutscenes = new();
    // ScummVM vm.cutScenePtr/cutSceneScript per cutscene depth (0: no cutscene open).
    protected readonly Dictionary<int, (ScriptFrame Frame, int JumpOffset)> _overrides = new();
    protected ScriptFrame? _cutsceneOwner;
    // Room loads by global scripts: the script pauses after the load, the session
    // enters the room, and the script resumes there (only inside an action or frame).
    protected int _dispatchScope;
    // Inside an action, input or frame advance, the session enters the first
    // reached room load once the scripts have run (ActivateObjectVerb, FinishInput, AdvanceFrame).
    protected int _entersRoomLoads;
    // True while the constructor runs the boot; its loads are not player actions.
    protected bool _booting;
    protected (ScummScriptEvent Event, ScriptFrame Frame)? _requestedLoad;
    protected readonly HashSet<ScummScriptEvent> _dispatchedLoads = new(ReferenceEqualityComparer.Instance);
    protected ScriptFrame? _actionLoadFrame;
    // chainScript: the script that chained, and what it starts once it has ended.
    protected (ScriptFrame Frame, int Script, IReadOnlyDictionary<string, ushort> Arguments,
        IReadOnlyCollection<string> EstimatedArguments, bool PathEstimated)? _chained;
    // Script numbers whose last run ended at a step the preview does not model.
    protected readonly HashSet<int> _endedUnmodelled = [];

    /// <summary>ScummVM <c>VAR_CUTSCENE_START_SCRIPT</c>, <c>VAR_CUTSCENE_END_SCRIPT</c> and <c>VAR_OVERRIDE</c>.</summary>
    protected const string CutsceneStartScriptVariable = "var[0x023]";
    protected const string CutsceneEndScriptVariable = "var[0x024]";
    protected const string OverrideVariable = "var[0x005]";

    /// <summary>Session-level steps (such as stopped scripts) in the order they were applied.</summary>
    public IReadOnlyList<string> SessionTrace => _visitTrace;

    protected void RunGlobalHook(int variableId, string phase, bool pathEstimated = false)
    {
        var key = $"var[0x{variableId:X3}]";
        if (!_runtime.GlobalVariables.TryGetValue(key, out var scriptId))
        {
            _uncertainties.Add($"Global {phase} hook {key} is unknown because startup has not established it.");
            return;
        }
        if (scriptId != 0) RunIndexedScript(scriptId, 0, null, 0, pathEstimated: pathEstimated);
    }

    protected void RunEmbeddedRoomScript(GameResource room, bool exit, bool pathEstimated = false)
    {
        ScummScriptDisassembly? script;
        try
        {
            script = exit ? _data.RoomExitScript(room.ResourceId!.Value) : _data.RoomEntryScript(room.ResourceId!.Value);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {room.ResourceId:00} {(exit ? "exit" : "entry")} script unavailable: {ex.Message}");
            return;
        }
        if (!exit) { LastEntryScript = script; LastEntryRun = null; }
        if (script is null) return;
        var result = RunScript(script, _currentObjects, 0, null, 0, pathEstimated: pathEstimated);
        if (!exit) LastEntryRun = result;
    }

    protected ScummBoundedScriptResult? RunIndexedScript(int scriptId, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        ScriptFrame? frame = null)
    {
        try
        {
            if (_data.GlobalScript(scriptId) is not { } indexed)
            {
                _uncertainties.Add($"Indexed script {scriptId:00} is absent from the parsed resource list.");
                return null;
            }
            return RunScript(indexed, _currentObjects, startOffset, locals, depth, scriptId,
                estimatedLocals, pathEstimated, frame);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Indexed script {scriptId:00} could not be decoded: {ex.Message}");
            return null;
        }
    }

    protected ScummBoundedScriptResult? RunStartedScript(int scriptId, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        bool freezeResistant = false, ScriptFrame? parent = null, bool recursive = false,
        ScummObservedCall? observedCall = null)
    {
        // A started script carries the room origin of the script that started it.
        parent ??= _executing.Count > 0 ? _executing[^1] : null;
        // runScript: a start that is not recursive first ends the running copies (stopScript).
        if (!recursive)
        {
            var ended = StopRunningScript(scriptId);
            if (ended > 0)
            {
                _visitTrace.Add($"script {scriptId} started again: runScript ended its running copy first");
            }
        }
        var frame = new ScriptFrame { FreezeResistant = freezeResistant, Recursive = recursive, OriginRoom = parent?.OriginRoom,
            ObservationParent = parent?.ObservationSlot, ObservationCall = observedCall ?? _observer?.CurrentCall };
        if (frame.ObservationCall?.Slot != frame.ObservationParent) frame.ObservationCall = null;
        if (scriptId < _globalScriptCount)
            return RunIndexedScript(scriptId, startOffset, locals, depth, estimatedLocals, pathEstimated, frame);
        if (_currentRoom is null)
        {
            _uncertainties.Add($"Local script {scriptId:00} cannot run before a room is loaded.");
            return null;
        }
        try
        {
            var script = _data.RoomLocalScript(_currentRoom.ResourceId!.Value, scriptId);
            if (script is null)
            {
                _uncertainties.Add($"Room {_currentRoom.ResourceId:00} has no local script {scriptId:00}.");
                return null;
            }
            return RunScript(script, _currentObjects, startOffset, locals, depth, scriptId,
                estimatedLocals, pathEstimated, frame);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {_currentRoom.ResourceId:00} local script {scriptId:00} could not be decoded: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// o5_startObject → runObjectScript (v3): nothing happens for object 0 or an
    /// object that is neither in the room nor held (<c>WIO_NOT_FOUND</c>), or
    /// when the object has no entry for the verb and no fallback
    /// (<c>getVerbEntrypoint</c> 0). Otherwise the verb's code runs at once with
    /// the arguments as locals (<c>runScriptNested</c>). Held objects' code is
    /// not modelled; stopping the object's earlier script (<c>stopObjectScript</c>) is not modelled.
    /// </summary>
    protected void StartObjectScript(ScummScriptEvent scriptEvent, int depth)
    {
        var source = $"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}";
        if (scriptEvent.ResolvedArguments is not [int objectId, int verbId])
        {
            _uncertainties.Add($"{source}: startObject reads an object or verb the preview does not know ({scriptEvent.Operands}); the parent continues without its effects.");
            return;
        }
        if (objectId == 0 || _currentRoom is null) return;
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId);
        if (record is null)
        {
            _uncertainties.Add($"{source}: startObject {objectId}, verb 0x{verbId:X2}: the object is not in Room {_currentRoom.ResourceId:00}; its code runs only if it is held, and inventory code is not modelled.");
            return;
        }
        if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var locals))
        {
            _uncertainties.Add($"{source}: startObject {objectId} has arguments depending on unknown variables; the parent continues without its effects.");
            return;
        }
        ScummScriptDisassembly script;
        ScummObjectVerbEntry selectedEntry;
        try { script = _data.ReadObjectVerb(_currentRoom.ResourceId!.Value, record, verbId, out selectedEntry); }
        catch (InvalidDataException ex) when (ex.Message.Contains("has no verb", StringComparison.Ordinal))
        {
            _visitTrace.Add($"{source}: object {objectId} has no verb 0x{verbId:X2} and no fallback, so startObject does nothing");
            return;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"{source}: object {objectId} verb 0x{verbId:X2} could not be decoded: {ex.Message}");
            return;
        }
        var parent = _executing.Count > 0 ? _executing[^1] : null;
        _observer?.ObjectCalleeSelected(objectId, verbId, selectedEntry, script);
        _visitTrace.Add($"{source}: startObject runs object {objectId} verb 0x{verbId:X2}");
        if (IsObjectSentence(verbId)) ObjectSentencesBegun++;
        RunScript(script, _currentObjects, 0, locals, depth + 1, null,
            EstimatedArguments(scriptEvent),
            scriptEvent.Certainty != ScummScriptEventCertainty.Reached,
            new ScriptFrame { OriginRoom = parent?.OriginRoom, ObservationCall = _observer?.CurrentCall });
    }

    protected ScummBoundedScriptResult RunScript(ScummScriptDisassembly script,
        IReadOnlyList<ScummRoomObjectRecord> objects, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth, int? scriptNumber = null,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        ScriptFrame? frame = null)
    {
        if (depth >= MaxNestedScripts)
        {
            _uncertainties.Add($"Nested script limit reached in {script.Context ?? $"Script {script.ScriptId:00}"}.");
            return new ScummBoundedScriptResult(_runtime.ObjectStates, _runtime.ObjectPositions,
                _runtime.GlobalVariables, [], [], 0, false, "nested script limit reached");
        }
        frame ??= new ScriptFrame();
        if (_observer is not null && frame.ObservationSlot == 0)
        {
            frame.ObservationSlot = _observer.NewSlot();
            frame.ObservationParent ??= _executing.Count > 0 ? _executing[^1].ObservationSlot : null;
            frame.ObservationCall ??= _observer.CurrentCall;
            if (frame.ObservationCall?.Slot != frame.ObservationParent) frame.ObservationCall = null;
        }
        frame.Label = script.Context ?? $"Script {script.ScriptId:00}";
        frame.Number = scriptNumber;
        // Indexed scripts run in global slots; room entry, exit, local and verb code is room-owned.
        frame.Global = script.Context is null;
        if (!frame.Global) frame.OriginRoom = _currentRoom?.ResourceId;
        _executing.Add(frame);
        ScummBoundedScriptResult result;
        try
        {
            result = _executor.Execute(script, _runtime.ObjectStates, objects,
                runtime: _runtime, startOffset: startOffset, initialLocals: locals,
                onEvent: (scriptEvent, _) =>
                {
                    var handling = HandleEvent(scriptEvent, depth);
                    // stopScript also kills executing/nested slots. A stopped caller
                    // cannot resume after its child returns, even if a new copy of
                    // the same script number was started in the meantime.
                    if (frame.Stopped) return ScummBoundedScriptExecutor.StopAfterEvent;
                    // runScriptNested restores a caller only when its freezeCount is zero.
                    // A child may freeze its still-executing parent; save the parent's next
                    // instruction instead of letting it open another scope while frozen.
                    if (string.IsNullOrEmpty(handling) && frame.FreezeCount > 0)
                    {
                        _visitTrace.Add($"{frame.Label} pauses after {scriptEvent.Operation} +0x{scriptEvent.Offset:X4}: nested execution froze its caller");
                        return ScummBoundedScriptExecutor.PauseAfterEvent;
                    }
                    return handling;
                },
                estimatedLocals: estimatedLocals, pathEstimated: pathEstimated,
                observation: _observer is null ? null : new(_observer, frame.ObservationSlot, frame.ObservationParent,
                    frame.ObservationCall, frame.ObservationRan));
            frame.ObservationRan = true;
        }
        finally
        {
            _executing.RemoveAt(_executing.Count - 1);
        }
        var label = script.Context ?? $"Script {script.ScriptId:00}";
        // o5_delay leaves the slot ssPaused with its delay; any other pause or end clears it.
        frame.Delay = result.ResumeOffset is null ? null : result.Delay;
        if (frame.Delay is int delay) _visitTrace.Add($"{label} waits {delay} jiffies (delay) before +0x{result.ResumeOffset:X4}");
        if (result.DelayEstimate is not null) _uncertainties.Add($"{label}: {result.DelayEstimate}; when it runs again is an estimate.");
        foreach (var pick in result.RandomPicks)
        {
            _picks.Add(pick);
            _uncertainties.Add($"{label} +0x{startOffset:X4}: {pick.Describe()}.");
        }
        if (result.UnresolvedReason is not null)
            _uncertainties.Add($"{label} +0x{startOffset:X4}: {result.UnresolvedReason}");
        if (scriptNumber is int number)
        {
            if (!result.ReachedStop && result.ResumeOffset is null) _endedUnmodelled.Add(number);
            else _endedUnmodelled.Remove(number);
        }
        if (_chained is { } chain && chain.Frame == frame)
        {
            _chained = null;
            _visitTrace.Add($"{label} chains to script {chain.Script}");
            RunStartedScript(chain.Script, 0, chain.Arguments, depth, chain.EstimatedArguments, chain.PathEstimated,
                frame.FreezeResistant, frame, frame.Recursive, frame.ObservationChainCall);
        }
        if (result.ResumeOffset is int next && !frame.Stopped)
        {
            var preservedLocals = result.KnownVariables
                .Where(pair => pair.Key.StartsWith("local[", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _pending.Add(new PendingScript(script, next, preservedLocals,
                script.Context is null ? null : _currentRoom?.ResourceId, scriptNumber,
                result.EstimatedLocals, result.PathEstimated, frame));
        }
        return result;
    }

    protected string? HandleEvent(ScummScriptEvent scriptEvent, int depth)
    {
        _visitEvents.Add(scriptEvent);
        if (scriptEvent.Kind is ScummScriptEventKind.MusicStarted or ScummScriptEventKind.SoundStarted or
            ScummScriptEventKind.MusicStopped or ScummScriptEventKind.SoundStopped)
            _soundEvents.Add(new ScummSessionSoundEvent(scriptEvent, _visitEvents.Count - 1, _runtime.CurrentRoom,
                _executing.Count > 0 ? _executing[^1].OriginRoom : null));
        switch (scriptEvent.Kind)
        {
            case ScummScriptEventKind.ScriptStarted:
                if (scriptEvent.TargetId is not int scriptId)
                {
                    _uncertainties.Add("A started script has a dynamic target; the parent continues without its effects.");
                    return "";
                }
                if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var locals))
                {
                    _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: started script {scriptId:00} has arguments depending on unknown variables; the parent continues without its effects.");
                    return "";
                }
                // startScript launches a child. An unresolved child does not
                // cancel instructions remaining in the caller. A child started
                // behind a seeded pick, or given a picked value, runs as an estimate.
                RunStartedScript(scriptId, 0, locals, depth + 1,
                    EstimatedArguments(scriptEvent),
                    scriptEvent.Certainty != ScummScriptEventCertainty.Reached, scriptEvent.FreezeResistant,
                    recursive: scriptEvent.Recursive);
                return "";
            case ScummScriptEventKind.CutsceneStarted:
                return BeginCutscene(scriptEvent, depth);
            case ScummScriptEventKind.CutsceneEnded:
                return EndCutscene(scriptEvent, depth);
            case ScummScriptEventKind.ScriptsFrozen:
                return FreezeScripts(scriptEvent);
            case ScummScriptEventKind.ScriptChained:
                // o5_chainScript ends the current script and starts the target in
                // its place, with the arguments and its freeze resistance.
                if (scriptEvent.TargetId is not int chained)
                    return "chained script depends on an unknown variable";
                if (_executing.Count == 0 || !TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var chainArguments))
                    return "chained script arguments depend on unknown variables";
                _chained = (_executing[^1], chained, chainArguments,
                    EstimatedArguments(scriptEvent),
                    scriptEvent.Certainty != ScummScriptEventCertainty.Reached);
                _executing[^1].ObservationChainCall = _observer?.CurrentCall;
                return ScummBoundedScriptExecutor.StopAfterEvent;
            case ScummScriptEventKind.ScriptStopped:
                if (scriptEvent.TargetId is not int stoppedScript)
                    return "stopped script depends on an unknown variable";
                // ScummEngine::stopScript ends every running slot with this
                // number; a script that is not running is left as it is.
                var pendingStopped = _pending.Any(item => item.ScriptNumber == stoppedScript && !item.Frame.Stopped);
                var removed = StopRunningScript(stoppedScript);
                _visitTrace.Add(removed == 0
                    ? $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: script {stoppedScript} was not running"
                    : $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: ended its {(pendingStopped ? "pending" : "running")} run");
                return "";
            case ScummScriptEventKind.ObjectScriptStarted:
                StartObjectScript(scriptEvent, depth);
                return "";
            case ScummScriptEventKind.MusicStarted:
            case ScummScriptEventKind.SoundStarted:
                if (scriptEvent.TargetId is not int soundId) return "sound target depends on an unknown variable";
                _runtime.ActiveSounds.Add(soundId);
                _soundClock.Start(soundId, scriptEvent.Certainty != ScummScriptEventCertainty.Reached);
                var soundStatus = _soundClock.Status(soundId);
                if (soundStatus.Estimate is not null)
                    _uncertainties.Add($"Sound {soundId:00} status is an estimate: {soundStatus.Estimate}.");
                return "";
            case ScummScriptEventKind.MusicStopped:
                _runtime.ActiveSounds.Clear();
                _soundClock.StopAll();
                return "";
            case ScummScriptEventKind.SoundStopped:
                if (scriptEvent.TargetId is not int stoppedId) return "stopped sound target depends on an unknown variable";
                _runtime.ActiveSounds.Remove(stoppedId);
                _soundClock.Stop(stoppedId, scriptEvent.Certainty != ScummScriptEventCertainty.Reached);
                return "";
            case ScummScriptEventKind.RoomLoaded when !IsExecutedRoomLoad(scriptEvent):
                return "room load was not executed; a discovered possibility cannot change the session room";
            case ScummScriptEventKind.RoomLoaded when _dispatchScope > 0 && _requestedLoad is null &&
                                                      _executing.Count > 0 && _executing[^1].Global &&
                                                      scriptEvent.TargetId is int loadedRoom &&
                                                      _game.Rooms.Any(room => room.ResourceId == loadedRoom):
                // A global script survives startScene: pause it here, enter the
                // room, then resume it there (DispatchRequestedLoads).
                _requestedLoad = (scriptEvent, _executing[^1]);
                return ScummBoundedScriptExecutor.PauseAfterEvent;
            case ScummScriptEventKind.RoomLoaded when _entersRoomLoads > 0 && _executing.Count > 0 && !_executing[^1].Global &&
                                                      scriptEvent.TargetId is int enteredRoom &&
                                                      _game.Rooms.Any(room => room.ResourceId == enteredRoom):
                // ScummVM startScene ends the room, object or local script that
                // loads the room (_currentScript = 0xFF); the session enters the room next.
                _visitTrace.Add($"{scriptEvent.Operation} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} ends this room-owned script (startScene); the session enters Room {enteredRoom:00}");
                return ScummBoundedScriptExecutor.StopAfterEvent;
            case ScummScriptEventKind.RoomLoaded:
                return $"script requests Room {scriptEvent.TargetId?.ToString("00", CultureInfo.InvariantCulture) ?? "dynamic"}; room transition awaits bounded session dispatch";
            default:
                return null;
        }
    }

    /// <summary>
    /// ScummEngine::beginCutscene: pushes the first argument as the cutscene's
    /// data and runs the script in <c>VAR_CUTSCENE_START_SCRIPT</c> with the
    /// arguments. The calling script stays exempt from a freeze while it runs.
    /// </summary>
    protected string BeginCutscene(ScummScriptEvent scriptEvent, int depth)
    {
        if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var arguments))
            return "cutscene arguments depend on unknown variables";
        _cutscenes.Push((arguments.GetValueOrDefault("local[0x000]"), _executing.Count > 0 ? _executing[^1] : null));
        CutscenesBegun++;
        _overrides.Remove(_cutscenes.Count);
        if (!_runtime.GlobalVariables.TryGetValue(CutsceneStartScriptVariable, out var startScript))
        {
            _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: the cutscene start script ({CutsceneStartScriptVariable}) is unknown; its effects were not applied.");
            return "";
        }
        _visitTrace.Add($"cutscene at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} runs script {startScript}");
        if (startScript == 0) return "";
        var owner = _cutsceneOwner;
        _cutsceneOwner = _executing.Count > 0 ? _executing[^1] : null;
        try
        {
            RunStartedScript(startScript, 0, arguments, depth + 1,
                EstimatedArguments(scriptEvent),
                scriptEvent.Certainty != ScummScriptEventCertainty.Reached);
        }
        finally
        {
            _cutsceneOwner = owner;
        }
        return "";
    }

    /// <summary>
    /// ScummEngine::endCutscene: clears <c>VAR_OVERRIDE</c>, pops the cutscene's
    /// data and runs the script in <c>VAR_CUTSCENE_END_SCRIPT</c> with it.
    /// </summary>
    protected string EndCutscene(ScummScriptEvent scriptEvent, int depth)
    {
        var estimate = scriptEvent.Certainty != ScummScriptEventCertainty.Reached;
        _runtime.SetGlobalVariable(OverrideVariable, 0);
        if (estimate) _runtime.EstimatedVariables.Add(OverrideVariable);
        else _runtime.EstimatedVariables.Remove(OverrideVariable);
        ushort data = 0;
        _overrides.Remove(_cutscenes.Count);
        if (_cutscenes.Count > 0) data = _cutscenes.Pop().Data;
        else _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: endCutscene without a cutscene begun on this route; its data is taken as 0.");
        if (!_runtime.GlobalVariables.TryGetValue(CutsceneEndScriptVariable, out var endScript))
        {
            _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: the cutscene end script ({CutsceneEndScriptVariable}) is unknown; its effects were not applied.");
            return "";
        }
        _visitTrace.Add($"endCutscene at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} runs script {endScript}");
        if (endScript != 0)
            RunStartedScript(endScript, 0, new Dictionary<string, ushort> { ["local[0x000]"] = data }, depth + 1,
                [], estimate);
        return "";
    }

    /// <summary>
    /// ScummEngine::beginOverride and endOverride: the running script's skip
    /// jump is recorded for the open cutscene depth, or cleared.
    /// </summary>
    protected void RecordOverride(int? jumpOffset)
    {
        if (_executing.Count == 0) return;
        if (jumpOffset is int offset) _overrides[_cutscenes.Count] = (_executing[^1], offset);
        else _overrides.Remove(_cutscenes.Count);
    }

    /// <summary>
    /// ScummEngine::abortCutscene: the script that set the override at the open
    /// cutscene depth continues at its skip jump, unfrozen, with
    /// <c>VAR_OVERRIDE</c> = 1, and the override is cleared. With no override it
    /// does nothing. Returns what happened, for the key's label.
    /// </summary>
    /// <summary>
    /// True when Escape would skip now (ScummVM <c>processKeyboard</c> → <c>abortCutscene</c>): <c>VAR_CUTSCENEEXIT_KEY</c>
    /// is set, and the open cutscene has an override (<c>beginOverride</c>) whose script waits for a later frame.
    /// Before a cutscene reaches its <c>beginOverride</c> (script 052 sets it at +0x00C6, in Room 11), Escape does nothing.
    /// </summary>
    public bool CutsceneSkippable =>
        _runtime.GlobalVariables.TryGetValue(CutsceneExitKeyVariable, out var exitKey) && exitKey != 0 &&
        _overrides.TryGetValue(_cutscenes.Count, out var point) && _pending.Any(item => item.Frame == point.Frame);

    protected string AbortCutscene()
    {
        var depth = _cutscenes.Count;
        if (!_overrides.TryGetValue(depth, out var point))
            return depth == 0
                ? "no cutscene is open and no override is set, so it does nothing"
                : "the open cutscene has no override set (beginOverride), so it does nothing";
        var index = _pending.FindIndex(item => item.Frame == point.Frame);
        if (index < 0)
            return $"the override's script ({point.Frame.Label}) is not waiting for a later frame, so the session does not move it to its skip jump";
        _overrides.Remove(depth);
        _pending[index] = _pending[index] with { Offset = point.JumpOffset };
        point.Frame.FreezeCount = 0;
        _runtime.SetGlobalVariable(OverrideVariable, 1);
        _runtime.EstimatedVariables.Remove(OverrideVariable);
        _visitTrace.Add($"abortCutscene: {point.Frame.Label} continues at its skip jump +0x{point.JumpOffset:X4}, {OverrideVariable} = 1");
        return $"{point.Frame.Label} continues at its skip jump (+0x{point.JumpOffset:X4}) on the next frame, unfrozen, with {OverrideVariable} (VAR_OVERRIDE) = 1";
    }

    /// <summary>
    /// ScummEngine::freezeScripts and unfreezeScripts. A nonzero flag freezes
    /// every other running script, except freeze-resistant ones when the flag
    /// is below 0x80, and leaves the cutscene's owner running; 0 undoes one
    /// freeze. A frozen script keeps its place and does not run on later frames.
    /// </summary>
    protected string FreezeScripts(ScummScriptEvent scriptEvent)
    {
        var operand = scriptEvent.Operands.Trim();
        ushort flag;
        if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
            flag = literal;
        else if (!_runtime.GlobalVariables.TryGetValue(operand, out flag))
            return "freezeScripts flag depends on an unknown variable";
        var current = _executing.Count > 0 ? _executing[^1] : null;
        var slots = _executing.Where(frame => frame != current && !frame.Stopped)
            .Concat(_pending.Select(item => item.Frame))
            .Concat(_frameScripts.Where(item => !item.Frame.Stopped).Select(item => item.Frame))
            .Distinct().ToArray();
        if (flag != 0)
        {
            foreach (var slot in slots.Where(slot => !slot.FreezeResistant || flag >= 0x80))
                slot.FreezeCount++;
            if (_cutsceneOwner is not null) _cutsceneOwner.FreezeCount = 0;
        }
        else
        {
            foreach (var slot in slots.Where(slot => slot.FreezeCount > 0))
                slot.FreezeCount--;
        }
        var frozen = slots.Where(slot => slot.FreezeCount > 0).Select(slot => slot.Label).ToArray();
        _visitTrace.Add($"freezeScripts {operand} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: " +
                        (frozen.Length == 0 ? "no script is frozen" : $"frozen: {string.Join(", ", frozen)}"));
        return "";
    }

    /// <summary>
    /// The child's locals that hold an estimate: an argument read from an
    /// estimated global, or an estimated local the executor passed by value.
    /// </summary>
    protected string[] EstimatedArguments(ScummScriptEvent scriptEvent)
    {
        var operands = scriptEvent.Operands;
        var open = ScummBoundedScriptExecutor.ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) return [];
        var passed = scriptEvent.EstimatedArgumentIndexes ?? [];
        return operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((argument, index) => (argument, index))
            .Where(item => _runtime.EstimatedVariables.Contains(item.argument) || passed.Contains(item.index))
            .Select(item => $"local[0x{item.index:X3}]").ToArray();
    }

    protected static bool TryReadLocalArguments(string operands,
        IReadOnlyDictionary<string, ushort> globals, out IReadOnlyDictionary<string, ushort> locals)
    {
        var values = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var open = ScummBoundedScriptExecutor.ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) { locals = values; return true; }
        var arguments = operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < arguments.Length; index++)
        {
            var arg = arguments[index];
            if (arg.StartsWith("var[", StringComparison.Ordinal))
            {
                if (!globals.TryGetValue(arg, out var dynamicValue))
                {
                    var highScriptVariable = globals.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage) &&
                        int.TryParse(arg.AsSpan(6, arg.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) && number >= ScummBoundedScriptExecutor.FirstScriptOnlyVariable;
                    if (!highScriptVariable) { locals = values; return false; }
                    dynamicValue = 0;
                }
                values[$"local[0x{index:X3}]"] = dynamicValue;
            }
            else if (arg.StartsWith("bit[", StringComparison.Ordinal))
            {
                // A bit variable: its own value, or in a game whose bits are kept apart from the variables (the CD game) 0 until something sets it.
                if (globals.TryGetValue(arg, out var bitValue)) values[$"local[0x{index:X3}]"] = bitValue;
                else if (globals.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage)) values[$"local[0x{index:X3}]"] = 0;
                else { locals = values; return false; }
            }
            else if (arg.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                     ushort.TryParse(arg.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
                values[$"local[0x{index:X3}]"] = literal;
            else { locals = values; return false; }
        }
        locals = values;
        return true;
    }

    /// <summary>
    /// The route's uncertainties in order. A script that loops for many frames
    /// reports the same limitation each frame; within one step (a visit, an
    /// action or a run of frames, see <see cref="StartStep"/>) it is kept once.
    /// </summary>
    protected sealed class UncertaintyList : List<string>
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        public new void Add(string item)
        {
            if (_seen.Add(item)) base.Add(item);
        }
        public void StartStep() => _seen.Clear();
    }

    protected sealed record PendingScript(ScummScriptDisassembly Script, int Offset,
        IReadOnlyDictionary<string, ushort> Locals, int? RoomId, int? ScriptNumber,
        IReadOnlyCollection<string> EstimatedLocals, bool PathEstimated, ScriptFrame Frame);

    /// <summary>stopScript kills existing global/local slots, including nested
    /// callers and slots awaiting their turn in this frame. It does not kill a
    /// replacement created later with the same number (runScriptNested).</summary>
    protected int StopRunningScript(int number)
    {
        var slots = _executing.Concat(_pending.Select(item => item.Frame))
            .Concat(_frameScripts.Select(item => item.Frame))
            .Where(frame => frame.Number == number && !frame.Stopped).Distinct().ToArray();
        if (slots.Length > 0)
            _observer?.Cancel(number, _executing.Count > 0 ? _executing[^1].ObservationSlot : 0,
                slots.Select(slot => slot.ObservationSlot));
        foreach (var slot in slots) slot.Stopped = true;
        _pending.RemoveAll(item => item.Frame.Stopped);
        return slots.Length;
    }

    /// <summary>
    /// The freeze state of one running script slot (ScummVM <c>ScriptSlot</c>'s
    /// <c>freezeResistant</c> and <c>freezeCount</c>). It stays with the script
    /// while it runs and while it waits for a later frame.
    /// </summary>
    protected sealed class ScriptFrame
    {
        public bool FreezeResistant { get; init; }
        /// <summary>ScummVM <c>ScriptSlot.recursive</c>: a <c>chainScript</c> from this slot passes it on.</summary>
        public bool Recursive { get; init; }
        /// <summary>Global or local script number; null for room entry, exit and verb code.</summary>
        public int? Number { get; set; }
        /// <summary>Cancellation belongs to this slot instance, never to a later copy of its number.</summary>
        public bool Stopped { get; set; }
        public long ObservationSlot { get; set; }
        public long? ObservationParent { get; set; }
        public ScummObservedCall? ObservationCall { get; set; }
        public ScummObservedCall? ObservationChainCall { get; set; }
        public bool ObservationRan { get; set; }
        public bool Global { get; set; }
        public int FreezeCount { get; set; }
        /// <summary>ScummVM <c>ScriptSlot.delay</c> while the slot is <c>ssPaused</c> by <c>delay</c>/<c>delayVariable</c>; null otherwise.</summary>
        public int? Delay { get; set; }
        public string Label { get; set; } = "";
        /// <summary>
        /// The room whose entry, exit, local or verb code started this chain of
        /// scripts; null when it began in a global script the engine started
        /// (the boot or a room hook).
        /// </summary>
        public int? OriginRoom { get; set; }
    }
}


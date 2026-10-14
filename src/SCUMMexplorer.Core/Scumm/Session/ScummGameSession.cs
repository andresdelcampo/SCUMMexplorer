using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The data-driven concrete session: any game whose boot is the engine's (indexed script 1 runs at start, ScummVM
/// <c>runBootscript</c>) and whose behaviour comes from its own scripts and the <see cref="ScummProfile"/>. It adds nothing a
/// game-specific session would: no staff, drafts or catalog. LOOM keeps <c>LoomExplorationSession</c> because of those; the other
/// games use this class (S4b, 14 October 2026; first game: Indiana Jones and the Last Crusade EGA).
/// </summary>
public sealed class ScummGameSession : ScummSession
{
    public ScummGameSession(GameLibrary game, ScummIndexedObjectSnapshot indexedObjects, int randomSeed = 0,
        ScummScriptObserver? observer = null, IScummDataSource? data = null, bool boot = true)
        : base(game, indexedObjects, randomSeed, observer, data,
            AdlibLifetime(game))
    {
        // ScummEngine::resetScummVars (vars.cpp): the sound card the session plays through (AdLib is 3), the video mode, the text speed.
        var profile = ScummProfile.For(game);
        SoundCard = AdlibCard;
        if (profile.VideoMode is int videoMode) _runtime.SetGlobalVariable(ScummVars.VideoMode, (ushort)videoMode);
        _runtime.SetGlobalVariable(ScummVars.CharInc, ScummProfile.CharIncAtStart);
        foreach (var (variable, value) in profile.EngineVariablesAtStart) _runtime.SetGlobalVariable(variable, value);
        // The engine allocates every global variable zeroed (scumm.cpp: _scummVars = calloc), so a variable no script has written yet reads 0, not "unknown".
        for (var variable = 0; variable < profile.GlobalVariableCount; variable++)
        {
            var key = $"var[0x{variable:X3}]";
            if (!_runtime.GlobalVariables.ContainsKey(key)) _runtime.SetGlobalVariable(key, 0);
        }
        _runtime.CameraFollowStartsScene = profile.CameraFollowStartsScene;
        _bootInput = profile.BootInput;
        _waitsForControl = profile.BootEndsWhenPlayerHasControl;
        if (boot) Boot();
    }

    /// <summary><c>VAR_SOUNDCARD</c> for AdLib (vars.cpp: 0 PC speaker, 1 Tandy, 2 CMS, 3 AdLib, 4 Roland).</summary>
    private const int AdlibCard = 3;

    // The session's device is AdLib (ScummVM's Player_AD for the old-bundle games): the native clock of a sound is the AdLib resource's own.
    // A resource that is not an old-bundle AdLib part has no established lifetime.
    private static Func<GameResource, ScummSoundLifetime> AdlibLifetime(GameLibrary game)
    {
        var timing = ScummProfile.For(game).AdlibTiming;
        var adlib = new ScummAdlibAudio();
        // A game on several v4 disks stores its sounds in the disks' SO blocks (the AD part is the AdLib song); the browsed disks play at half the LOOM tempo
        // (by ear, 11 October, the explorer's own S3 decision), so the session's music timer follows the same estimate.
        if (game.V4Disks is { } disks && disks.Disks.OfType<ScummV4Archive>().Any())
        {
            var adlibParts = new Dictionary<int, byte[]?>();
            foreach (var archive in disks.Disks.OfType<ScummV4Archive>())
                foreach (var sound in archive.ReadSounds())
                    adlibParts.TryAdd(sound.Id, sound.Adlib);
            return resource => resource.ResourceId is int id && adlibParts.GetValueOrDefault(id) is { } part
                ? adlib.ReadLifetime(part, timing, halfTempo: true)
                : new(ScummSoundLifetimeKind.Unknown, null, false, "no AdLib part in the disks' sound blocks");
        }
        return resource => adlib.ReadLifetime(resource, timing);
    }

    private readonly ScummBootInputPlan? _bootInput;
    private readonly bool _waitsForControl;

    /// <summary>The player can act: a room is current, no cutscene is open and the game has user input on (<c>cursorCommand userInputOn</c>).</summary>
    public bool PlayerHasControl => _currentRoom is not null && !InCutscene && _runtime.UserPut > 0;

    /// <summary>Frames in which nothing changes before the boot is taken to wait for the player.</summary>
    public const int BootStallFrames = 400;

    private const int MaxBootKeys = 40;

    // The labelled assumed input of a waiting boot (R-15.5, R-1.11): the digits of the answer the game itself computed (read from its variable,
    // never written), else the continue key. The keys reach the game through VAR_VERB_SCRIPT, like a player's.
    private int PressBootKeys(ScummBootInputPlan plan)
    {
        var answer = plan.AnswerVariable is null ? 0 : _runtime.GlobalVariables.GetValueOrDefault(plan.AnswerVariable);
        var keys = answer != 0 ? answer.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (int)digit).ToArray() : [plan.ContinueKey];
        _visitTrace.Add($"boot: Player input assumed: {(answer != 0 ? $"the digits of {plan.AnswerVariable} = {answer}, the answer the game's own script derived" : $"key {DescribeKey(plan.ContinueKey)}")} " +
                        "after the boot stood still " + BootStallFrames + " frames");
        foreach (var key in keys) _bootKeys.Enqueue(key);
        return keys.Length;
    }

    // One key per frame: a game's input script keeps the last key in a variable that its waiting script reads on its next turn.
    private readonly Queue<int> _bootKeys = new();

    /// <summary>What the boot did: the frames it ran and why it stopped.</summary>
    public ScummBootReport? BootReport { get; private set; }

    protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
        EnterRoom(target, $"Room {target.ResourceId:00} reached by {action.Evidence}", action.ReachedRoomLoad);

    /// <summary>Runs the boot script, answers the input it waits for (if any, labelled), and plays frames until the script has ended or chained on.</summary>
    private const int MaxBootAnswers = 24;

    private void Boot()
    {
        RunBootScript();
        var answers = AssumeBootAnswer() is null ? 0 : 1;
        var frames = 0;
        var stalled = 0;
        string? lastState = null;
        var keysPressed = 0;
        // A game whose script 1 hands over to an opening (Monkey Island: title music in a room of its own, then the first room) is booted until the
        // player has control: user input on, no cutscene open, a room on screen.
        var untilControl = _waitsForControl;
        while (frames < BootFrameLimit && (BootGoesOn || untilControl && !PlayerHasControl && _pending.Count > 0))
        {
            if (_bootKeys.Count > 0) PressKey(_bootKeys.Dequeue());
            AdvanceFrame();
            frames++;
            // A boot that waits for the player (a key, a typed answer) changes nothing for a long time: answer it as a player would, through the game's input script.
            var state = $"{_currentRoom?.ResourceId}|{string.Join(",", _pending.Select(item => $"{item.ScriptNumber}:{item.Offset}"))}";
            stalled = state == lastState && _bootKeys.Count == 0 ? stalled + 1 : 0;
            lastState = state;
            if (_bootInput is not null && stalled >= BootStallFrames && keysPressed < MaxBootKeys)
            {
                keysPressed += PressBootKeys(_bootInput);
                stalled = 0;
            }
            // A screen that asks again after a wrong answer (the answering script ended without releasing the boot) is answered again, a few times.
            if (answers < MaxBootAnswers && _pending.Any(item => item.ScriptNumber == BootScript) && AssumeBootAnswer() is not null) answers++;
        }
        FinishBoot(frames);
        BootReport = new ScummBootReport(frames, frames >= BootFrameLimit, _currentRoom?.ResourceId, _roomsEntered.ToArray());
        LastAction = null;
    }
}

/// <summary>The outcome of a generic boot: frames played, whether the frame limit stopped it, the room it ended in and the rooms it entered.</summary>
public sealed record ScummBootReport(int Frames, bool HitFrameLimit, int? FinalRoom, IReadOnlyList<int> RoomsEntered);

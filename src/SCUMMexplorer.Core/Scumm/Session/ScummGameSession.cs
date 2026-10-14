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
        // resetScummVars (scumm.cpp): the lights start as base palette, actors' own colours and room lights on (1 | 8 | 2).
        _runtime.SetGlobalVariable(ScummVars.CurrentLights, 11);
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
        return resource => adlib.ReadLifetime(resource, timing);
    }

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
        while (frames < BootFrameLimit && BootGoesOn)
        {
            AdvanceFrame();
            frames++;
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

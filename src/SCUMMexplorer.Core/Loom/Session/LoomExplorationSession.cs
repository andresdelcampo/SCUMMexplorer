using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// Keeps a conservative, route-scoped LOOM state. Only instructions reached by
/// bounded execution change the snapshot. Unsupported state or timing is
/// recorded and prevents the snapshot from being labelled faithful.
/// </summary>
public sealed partial class LoomExplorationSession
{
    private const int MaxNestedScripts = 8;
    private readonly GameLibrary _game;
    private readonly ScummSoundClock _soundClock;
    private readonly IScummDataSource _data;
    private readonly ScummBoundedScriptExecutor _executor = new();
    private readonly ScummScriptObserver? _observer;
    private readonly ScummScriptRuntimeState _runtime;
    private readonly bool _demoPreview;
    private readonly int _globalScriptCount;
    private readonly UncertaintyList _uncertainties = [];
    private readonly List<PendingScript> _pending = [];
    private GameResource? _currentRoom;
    private IReadOnlyList<ScummRoomObjectRecord> _currentObjects = [];

    /// <param name="completeBoot">
    /// When true (the default), the boot runs past the intro screen and the
    /// opening cutscene until script 001 hands over to the input loop; see
    /// <see cref="CompleteBoot"/>. When false, it stops at script 001's first
    /// room load, as before 27 September.
    /// </param>
    /// <param name="stopAtOpening">
    /// With <paramref name="completeBoot"/>, stops the boot on the frame script 001 starts the opening cutscene
    /// (script 044, +0x0222), after the intro screen's assumed answer. <see cref="BootRunning"/> stays true, and
    /// <see cref="AdvanceFrame"/> plays the rest of the boot frame by frame, exactly as the constructor would.
    /// </param>
    /// <param name="stopAtBootInput">With completeBoot, enters the native intro and awaits actual input,
    /// before the explorer's assumed challenge answer. Frames continue the waiting boot after input.</param>
    /// <param name="playCdIntro">The CD game only, with <paramref name="completeBoot"/> false: keep script 001 paused where it starts the logo and intro, so <see cref="AdvanceFrame"/> plays them.</param>
    /// <param name="demoPreview">
    /// A LOOM demo's boot (script 001, then Room 01's entry) starts a director script that shows the rooms in turn and
    /// starts over. A demo session leaves it stopped, so the rooms can be explored like the full game's, unless this is
    /// true: then it runs (frames advance it; <see cref="AdvanceFrame"/> walks the self-running preview).
    /// </param>
    public LoomExplorationSession(GameLibrary game, ScummIndexedObjectSnapshot indexedObjects, int randomSeed = 0,
        bool completeBoot = true, bool stopAtOpening = false, bool stopAtBootInput = false,
        ScummScriptObserver? observer = null, bool demoPreview = false, int soundCard = 0, IScummDataSource? data = null,
        bool playCdIntro = false)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(indexedObjects);
        if (game.Version != ScummVersion.V3 && game.Id != GameId.LoomVga)
            throw new InvalidDataException("Only old-v3 LOOM and the LOOM CD game have a session interpreter.");
        _game = game;
        _demoPreview = demoPreview;
        _observer = observer;
        var soundResources = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
            .GroupBy(r => r.ResourceId!.Value).ToDictionary(group => group.Key, group => group.First());
        var soundLifetimes = new Dictionary<int, ScummSoundLifetime>();
        _soundClock = new ScummSoundClock(id =>
        {
            if (!soundLifetimes.TryGetValue(id, out var lifetime))
                soundLifetimes[id] = lifetime = soundResources.TryGetValue(id, out var resource)
                    ? game.Id == GameId.Loom
                        ? ScummRolandSong.IsRoland(resource) ? ScummRolandSong.ReadLifetime(resource) : new ScummAdlibAudio().ReadLifetime(resource)
                        : new(ScummSoundLifetimeKind.Unknown, null, false, "native LOOM AdLib clock does not establish this game's sound lifetime")
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
        _runtime.Actors.CostumeSource = _data.ReadCostume;
        _runtime.Verbs.ImageSize = (room, image) => VerbImage(room, image) is { } picture ? (picture.Width, picture.Height) : null;
        _runtime.Messages.TalkAnimation = (actor, start) => _runtime.Actors.TalkAnimation(actor, start, CurrentPlacement());
        // The engine starts with every variable at 0; the frame loop adds to the timers from there.
        foreach (var timer in EngineTimerVariables.Append(TimerTotalVariable))
            _runtime.SetGlobalVariable(timer, 0);
        // Script 017 starts the notes script 018 only while the distaff is not yet on (var[0x064] != 2) and 018 alone prints the note
        // letters, so a room change must leave the verb screen's strings alone in the EGA game too: erasing them left bare staves after
        // cutscenes and walks (user reports, 11 October).
        _runtime.Messages.KeepVerbScreenOnRoomEntry = true;
        if (game.Id == GameId.LoomVga)
        {
            _runtime.GlobalVariables[ScummBoundedScriptExecutor.SeparateBitStorage] = 1;
            _runtime.EstimatedVariables.Add(ScummBoundedScriptExecutor.SeparateBitStorage);
        }
        if (soundCard != 0) _runtime.SetGlobalVariable(SoundCardVariable, (ushort)soundCard);
        // The plain EGA editions' script 001 turns the music off (bit 0xD76) unless var[0x28] is above 250, a host-reported speed/memory figure;
        // ScummVM plays their music, so a modern host reports a fast machine. The Roland upgrade and the CD game do not test it.
        if (game.Id == GameId.Loom) _runtime.SetGlobalVariable(MachinePerformanceVariable, 1000);
        // ScummVM runs indexed script 1 at boot for this release family.
        if (!completeBoot)
        {
            // playCdIntro leaves script 001 where the intro begins: the frames play the logo and the intro, then the game's start.
            var playIntro = game.Id == GameId.LoomVga && playCdIntro;
            if (playIntro) { _dispatchScope++; _booting = true; }
            try { RunIndexedScript(1, 0, null, 0); }
            finally { if (playIntro) _dispatchScope--; }
            if (playIntro)
            {
                DispatchRequestedLoads("during the CD intro");
                // The frames play the rest as a boot (BootRunning): the skill screen, the assumed answer, the overture and the opening.
                _playingBoot = true;
                _cdIntro = true;
                LastAction = null;
            }
            else if (game.Id == GameId.LoomVga) ContinueCdBootAfterIntro();
            return;
        }
        _booting = true;
        _dispatchScope++;
        try
        {
            RunIndexedScript(1, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
        if (game.IsDemo && !demoPreview)
        {
            _pending.Clear();
            _uncertainties.Add("This is a LOOM demo: the preview's director script (started by Room 01's entry) is not running, so the rooms can be explored; 'Play the preview' runs it.");
        }
        if (stopAtBootInput)
        {
            // Keep script 001 waiting on the intro's source-owned D6B gate.
            // Room 69 input must run local 203/205; CompleteBoot's assumed answer is omitted.
            _booting = false;
            LastAction = null;
            return;
        }
        CompleteBoot(stopAtOpening);
        LastAction = null;
    }

    /// <summary>
    /// The CD game's script 001 plays the logo and intro first (local[0] = 0), then falls into the code a start with a room
    /// argument jumps to: the ego (<c>var[0x001]</c>), its costume numbers, the starting vars, then the first room load.
    /// A first-visit session does not play the intro, so after the intro's first room load it drops that paused run and runs
    /// the script from the jump target of its <c>equalZero local[0x000]</c> (found in the code, not by a fixed offset), which
    /// stops at the room load that opens the game. The vars the game has when the intro ends are then in place.
    /// </summary>
    private void ContinueCdBootAfterIntro()
    {
        ScummScriptDisassembly? boot;
        try { boot = _data.GlobalScript(1); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return; }
        var skip = boot?.Instructions.FirstOrDefault(item => item.Operation == "equalZero" && item.Operands.StartsWith("local[0x000], branch ", StringComparison.Ordinal));
        var arrow = skip?.Operands.LastIndexOf("-> 0x", StringComparison.Ordinal) ?? -1;
        if (skip is null || arrow < 0 ||
            !int.TryParse(skip.Operands.AsSpan(arrow + 5), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var target))
        {
            _uncertainties.Add("The CD boot's jump past the intro was not found in script 001, so the ego and the starting variables were not set.");
            return;
        }
        _pending.RemoveAll(item => item.ScriptNumber == 1);
        _requestedLoad = null;
        AssumeCdLayoutVariable(boot!, target);
        RunIndexedScript(1, target, new Dictionary<string, ushort> { ["local[0x000]"] = 0 }, 0);
        _pending.RemoveAll(item => item.ScriptNumber == 1);
        _requestedLoad = null;
        _visitTrace.Add($"boot: the intro was not played; script 001 ran on from +0x{target:X4}, where the intro ends, to the room load that opens the game");
        RunCdBootTail(boot!, target);
        _runtime.Messages.AssumeCdTimerRunning();
        _uncertainties.Add("The CD intro was not played, so no disc cue has started the music timer (var[0x00E]) in this session. In the game the intro's cues have, and it counts on from the last cue's start; here it counts from 0 at the start of the session (an estimate, the longest wait for scripts that wait on it).");
        if (_runtime.UserPut <= 0)
        {
            // The intro's own cursorCommand calls (input on once the opening ends) were skipped with it; a game that has started takes clicks.
            _runtime.UserPut = 1;
            _runtime.UserPutEstimate = "the CD game's intro, which turns user input on when it ends, was not played";
        }
        // The room load the boot ends on (Room 01) was dropped above, and Room 01's entry is what starts the game's set-up (local 211 creates the verb
        // slots, the staff notes among them): without it a room visited first has no staff to draw or edit. Enter it as the game's first load.
        if (_runtime.Verbs.Snapshot().Count == 0 && _game.Rooms.FirstOrDefault(room => room.ResourceId == 1) is { } first)
        {
            VisitRoom(first);
            _visitTrace.Add("boot: Room 01 was entered as the game's first load (its entry starts local 211, which creates the verb slots)");
        }
    }

    /// <summary>
    /// The staff's layout variable (0x0AD in the CD scripts; script 018 draws the staff 8 pixels lower unless it is 2, and the note letters at fixed rows)
    /// is set by script 001 before the intro (to the standard layout) and by the skill screen's answer; the jump past the intro skips both, so it stayed 0
    /// and the staff came out 8 pixels off its notes. The variable is found in script 018 (the one tested before it adds 8 to the staff's row) and the
    /// value in script 001's own move into it before the jump target: the standard skill, as the Room 69 answer is assumed elsewhere.
    /// </summary>
    private void AssumeCdLayoutVariable(ScummScriptDisassembly boot, int target)
    {
        ScummScriptDisassembly? staff;
        try { staff = _data.GlobalScript(18); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return; }
        var code = staff?.Instructions;
        var add = code?.ToList().FindIndex(item => item.Operation == "add" && item.Operands == "local[0x004] += 0x0008") ?? -1;
        if (code is null || add < 1 || code[add - 1].Operation != "isEqual" || System.Text.RegularExpressions.Regex.Match(code[add - 1].Operands, @"^(var[0x[0-9A-F]+]), 0x0002,") is not { Success: true } layout) return;
        var variable = layout.Groups[1].Value;
        var set = boot.Instructions.FirstOrDefault(item => item.Offset < target && item.Operation == "move" &&
            System.Text.RegularExpressions.Regex.IsMatch(item.Operands, "^" + System.Text.RegularExpressions.Regex.Escape(variable) + @" = 0x[0-9A-F]+$"));
        if (set is null || !ushort.TryParse(set.Operands.AsSpan(set.Operands.LastIndexOf("0x", StringComparison.Ordinal) + 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return;
        _runtime.SetGlobalVariable(variable, value);
        _runtime.EstimatedVariables.Add(variable);
        _uncertainties.Add($"The CD intro and its skill screen were not played: the staff's layout ({variable}) is {value}, the value script 001 +0x{set.Offset:X4} gives it before the intro (the standard skill; the skill screen's answer is assumed).");
    }

    /// <summary>
    /// What script 001 does once the opening is over: the code after its room loads and the wait for the opening script ends the override, unfreezes
    /// the scripts, sets the camera on the ego, and clears the start-up lock (<c>bit[0x00E]</c>, set a few instructions before the room loads, which
    /// the entry scripts of many rooms test before they start their monitors) before it chains to the input loop. A start that skips the opening
    /// dropped all of that, so bit 0x00E stayed set and those monitors never started. This runs the tail from its <c>endOverride</c> (the first one
    /// after the room loads, found in the code); the chained input loop is not kept, since this session takes clicks itself.
    /// </summary>
    private void RunCdBootTail(ScummScriptDisassembly boot, int target)
    {
        var ins = boot.Instructions;
        var load = ins.FirstOrDefault(i => i.Offset >= target && i.Operation is "loadRoom" or "loadRoomWithEgo");
        var tail = load is null ? null : ins.FirstOrDefault(i => i.Offset > load.Offset && i.Operation == "endOverride");
        if (tail is null)
        {
            _uncertainties.Add("The CD boot's tail (after the opening) was not found in script 001, so the start-up lock (bit[0x00E]) was left as the intro sets it.");
            return;
        }
        RunIndexedScript(1, tail.Offset, new Dictionary<string, ushort> { ["local[0x000]"] = 0 }, 0);
        _pending.RemoveAll(item => item.ScriptNumber is 1 or 6);
        _requestedLoad = null;
        _visitTrace.Add($"boot: the opening was skipped; script 001's tail from +0x{tail.Offset:X4} (after the opening's wait) ran: scripts unfrozen, start-up lock cleared, input on");
    }

    /// <summary>
    /// The opening cutscene: script 001 +0x0222 starts script 044 and waits while it runs (+0x0225–0x022A). Decoded.
    /// </summary>
    public const int OpeningScript = 44;

    /// <summary><c>VAR_SOUNDCARD</c>: 0 in a session by default, 3 for AdLib in ScummVM and 4 for the Roland upgrade, whose sounds (33 to 64) the scripts then choose.</summary>
    public const string SoundCardVariable = "var[0x030]";
    private const string MachinePerformanceVariable = "var[0x028]";
    public const int RolandSoundCard = 4;

    /// <summary>The sound card the game's scripts see. Changing it takes effect at the scripts' next read, as it would in the game.</summary>
    /// <summary>The shadow palette of the room on screen: slot i is shown as colour <c>ShadowPalette[i]</c> (identity unless a script faded something).</summary>
    public IReadOnlyList<byte> ShadowPalette => _runtime.ShadowPalette;

    public int SoundCard
    {
        get => _runtime.GlobalVariables.GetValueOrDefault(SoundCardVariable);
        set => _runtime.SetGlobalVariable(SoundCardVariable, (ushort)value);
    }

    /// <summary>
    /// True while the boot is still running: only after a session built with <c>stopAtOpening</c>, until the frames
    /// played since reach the point where the constructor would have stopped (script 001 has ended or chained on and no
    /// cutscene is open).
    /// </summary>
    public bool BootRunning => _booting;

    /// <summary>Frames the boot ran before it stopped at the opening (0 when it ran to the end).</summary>
    public int BootFramesBeforeOpening { get; private set; }

    private bool BootGoesOn => (_pending.Any(item => item.ScriptNumber == 1) || InCutscene) && _pending.Count > 0;

    private void FinishBoot(int frames)
    {
        _booting = false;
        BootFrames = frames;
        if (_pending.Any(item => item.ScriptNumber == 1))
            _uncertainties.Add($"The boot (script 001) was still running after {frames} frames; the preview stopped following it.");
        _visitTrace.Add($"boot: {frames} frames after script 001's first room load; now in Room {_currentRoom?.ResourceId:00}");
    }

    /// <summary>Frames the boot may run after script 001's first room load.</summary>
    public const int BootFrameLimit = 5000;

    /// <summary>
    /// Continues the boot after script 001 has entered its first room. When
    /// script 001 waits for a bit that only a local script of that room sets,
    /// and the room's own code starts that script after player input (the
    /// intro screen's difficulty and draft check: Room 69 local 205 sets
    /// <c>bit[0xD6B]</c>, and local 203 starts it after a correct draft or a
    /// third wrong one), the session starts that script as if the player had
    /// answered, and labels it. Frames then run until script 001 has ended or
    /// chained on and no cutscene is open, at most <see cref="BootFrameLimit"/>.
    /// </summary>
    private void CompleteBoot(bool stopAtOpening)
    {
        if (_currentRoom is null)
        {
            _booting = false;
            return;
        }
        AssumeBootAnswer();
        var frames = 0;
        while (frames < BootFrameLimit && BootGoesOn)
        {
            if (stopAtOpening && _pending.Any(item => item.ScriptNumber == OpeningScript))
            {
                // The rest of the boot is played by AdvanceFrame, which counts on from here.
                _bootFramesPlayed = BootFramesBeforeOpening = frames;
                _playingBoot = true;
                _visitTrace.Add($"boot: stopped after {frames} frames, where script 001 started the opening (script {OpeningScript:000}); " +
                                "the rest plays frame by frame");
                return;
            }
            AdvanceFrame();
            frames++;
        }
        FinishBoot(frames);
    }

    /// <summary>
    /// The boot's skill screen (Room 69) waits for the player: script 001 waits on a bit that one local script of the room sets once an
    /// answer is given. This starts that script as the player's answer would and returns the note that says so (an estimate: the choice
    /// is not simulated), or null when the boot is not waiting for such an input.
    /// </summary>
    public string? AssumeBootAnswer()
    {
        if (FindAwaitedInput() is not { } input || _currentRoom is null) return null;
        _uncertainties.Add(input.Note);
        _visitTrace.Add($"boot: starts Room {_currentRoom.ResourceId:00} local script {input.LocalScript} as the player's answer would");
        _dispatchScope++;
        try
        {
            RunStartedScript(input.LocalScript, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
        return input.Note;
    }

    // True while a boot stopped at the opening is played frame by frame; the frames of the boot so far.
    private bool _playingBoot;
    private bool _cdIntro;

    /// <summary>
    /// The CD intro's skill screen (Room 69) waits for the player. Playing it unattended, the preview answers
    /// (<see cref="AssumeBootAnswer"/>) this many frames in, 3 s at the game's 6 jiffies a frame, after the spoken line "Please choose your
    /// skill level" (2.4 s). The moment is an estimate.
    /// </summary>
    public const int CdIntroAnswerFrame = 30;

    /// <summary>
    /// Set by a window that lets the player press a skill button on the CD intro's Room 69 screen: the screen then stays until a click, instead of
    /// answering by itself at <see cref="CdIntroAnswerFrame"/>.
    /// </summary>
    public bool WaitForSkillClick { get; set; }
    private int? _skillClickFrame;
    private const int SkillAnswerDelayFrames = 20;

    /// <summary>True while the CD intro's skill screen waits for the player's click (see <see cref="WaitForSkillClick"/>).</summary>
    public bool WaitingForSkillClick => WaitForSkillClick && _playingBoot && _cdIntro && _skillClickFrame is null && _bootFramesPlayed >= 2 &&
        _currentRoom is not null && FindAwaitedInput() is not null;

    /// <summary>
    /// A click on the skill screen: it goes through the game's input script as any click does (the button objects' own code runs). The challenge
    /// that follows a skill choice in the game is not simulated: the answer is assumed a moment after a click that started a script.
    /// </summary>
    public ScummObjectActionResult ClickSkillScreen(int x, int y)
    {
        var before = _pending.Count;
        var result = Click(x, y);
        if (_pending.Count > before && !result.Uncertainties.Any(line => line.Contains("not sent, because", StringComparison.Ordinal))) _skillClickFrame = _bootFramesPlayed;
        return result;
    }
    private int _bootFramesPlayed;

    /// <summary>After a frame of a boot stopped at the opening: ends the boot where the constructor would have.</summary>
    private void ContinuePlayedBoot()
    {
        if (!_playingBoot) return;
        _bootFramesPlayed++;
        if (_cdIntro && !WaitForSkillClick && _bootFramesPlayed == CdIntroAnswerFrame) AssumeBootAnswer();
        // With the player's click awaited, the answer follows the click (the game would ask a draft question next; that is not simulated).
        if (_skillClickFrame is int clicked && _bootFramesPlayed == clicked + SkillAnswerDelayFrames) AssumeBootAnswer();
        if (_bootFramesPlayed < BootFrameLimit && BootGoesOn) return;
        _playingBoot = false;
        FinishBoot(_bootFramesPlayed);
    }

    /// <summary>
    /// Jiffies (1/60 s) per frame: ScummVM's main loop waits <c>VAR_TIMER_NEXT</c>
    /// (<c>var[0x013]</c>) jiffies, at least 1. The boot sets 6 (script 001 +0x0017).
    /// </summary>
    public int FrameJiffies => Math.Max(1, _runtime.GlobalVariables.TryGetValue(TimerNextVariable, out var next) ? next : 0);

    private const string TimerNextVariable = "var[0x013]";

    /// <summary>ScummVM v3 <c>VAR_TMR_1</c>..<c>VAR_TMR_3</c> (vars 11–13): jiffies the frame loop adds every frame.</summary>
    private static readonly string[] EngineTimerVariables = ["var[0x00B]", "var[0x00C]", "var[0x00D]"];

    /// <summary>ScummVM v3 <c>VAR_TIMER</c> (var 46): the jiffies of the last frame.</summary>
    private const string TimerVariable = "var[0x02E]";

    /// <summary>ScummVM v3 <c>VAR_TIMER_TOTAL</c> (var 47): jiffies since the start.</summary>
    private const string TimerTotalVariable = "var[0x02F]";

    /// <summary>ScummVM <c>scummLoop</c> clamps the jiffies it counts script delays and talk down by to 15.</summary>
    private const int MaxFrameDelta = 15;

    /// <summary>
    /// The start of ScummVM's <c>scummLoop(delta)</c> for v3: <c>VAR_TIMER</c> = delta, <c>VAR_TIMER_TOTAL</c> and
    /// <c>VAR_TMR_1..3</c> += delta, then <c>decreaseScriptDelay(min(delta, 15))</c>: each paused slot's delay goes down,
    /// and a slot whose delay falls below 0 runs again this frame.
    /// </summary>
    private void UpdateTimersAndDelays(int delta)
    {
        _runtime.SetGlobalVariable(TimerVariable, (ushort)delta);
        foreach (var timer in EngineTimerVariables.Append(TimerTotalVariable))
            if (_runtime.GlobalVariables.TryGetValue(timer, out var value))
                _runtime.SetGlobalVariable(timer, (ushort)(value + delta));
        var counted = Math.Min(delta, MaxFrameDelta);
        foreach (var item in _pending)
        {
            if (item.Frame.Delay is not int delay) continue;
            delay -= counted;
            item.Frame.Delay = delay < 0 ? null : delay;
            if (delay < 0) _visitTrace.Add($"{item.Frame.Label}'s delay ends; it runs again at +0x{item.Offset:X4}");
        }
    }

    /// <summary>Frames a continued cutscene may run (about 33 minutes at 10 frames a second) before the preview stops following it.</summary>
    public const int CutsceneFrameLimit = 20000;

    /// <summary>Frames the completed boot ran after script 001's first room load.</summary>
    public int BootFrames { get; private set; }

    /// <summary>
    /// Script 001 waiting (<c>breakHere</c>, then a branch back while a bit is 0)
    /// for a bit that exactly one local script of the current room sets to 1.
    /// Returns that local script and a note naming the room code that starts it.
    /// </summary>
    private (int LocalScript, string Note)? FindAwaitedInput()
    {
        if (_currentRoom is null || _pending.FirstOrDefault(item => item.ScriptNumber == 1) is not { } boot) return null;
        var wait = boot.Script.Instructions.FirstOrDefault(item => item.Offset == boot.Offset);
        if (wait is not { Operation: "notEqualZero" } || !wait.Operands.StartsWith("bit[", StringComparison.Ordinal)) return null;
        var bit = wait.Operands[..(wait.Operands.IndexOf(']') + 1)];
        var setters = new List<(int Local, int Offset)>();
        var starters = new List<string>();
        for (var local = _globalScriptCount; local < 256; local++)
        {
            ScummScriptDisassembly? script;
            try { script = _data.RoomLocalScript(_currentRoom.ResourceId!.Value, local); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
            if (script is null) continue;
            foreach (var item in script.Instructions)
            {
                if (item.Operation == "move" && item.Operands == $"{bit} = 0x0001") setters.Add((local, item.Offset));
                if (item.Operation == "startScript" && item.Operands.StartsWith("0x", StringComparison.Ordinal))
                    starters.Add($"{local}:{item.Operands[..4]}:{item.Offset:X4}");
            }
        }
        if (setters.Count != 1) return null;
        var (setter, setOffset) = setters[0];
        var startedBy = starters.Where(item => Convert.ToInt32(item.Split(':')[1], 16) == setter)
            .Select(item => $"local {item.Split(':')[0]} +0x{item.Split(':')[2]}").ToArray();
        if (startedBy.Length == 0) return null;
        return (setter, $"Player input assumed: script 001 +0x{wait.Offset:X4} waits for {bit}, which only Room {_currentRoom.ResourceId:00} " +
                        $"local script {setter} sets (+0x{setOffset:X4}). The room's own code starts it after the player's input " +
                        $"({string.Join(", ", startedBy)}), so the preview starts it as if the player had answered. Choices made on that screen were not simulated.");
    }

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
    public int CutscenesBegun { get; private set; }

    /// <summary>Look/use sentences actually started by startObject (input script 007), excluding the preliminary walk-to verb.</summary>
    public int ObjectSentencesBegun { get; private set; }

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

    private bool IsAlive(ScriptFrame? frame) =>
        frame is not null && (_executing.Contains(frame) || _pending.Any(item => item.Frame == frame));

    /// <summary>Seed of the stand-in random source; every <c>getRandomNr</c> value is a labelled pick from it.</summary>
    public int RandomSeed => _runtime.Random.Seed;
    public ScummScriptDisassembly? LastEntryScript { get; private set; }

    /// <summary>
    /// The current room's walk boxes, read afresh on each entry as ScummVM's
    /// <c>resetRoomSubBlocks</c> does; <c>setBoxFlags</c> changes this copy. Null
    /// when no room is loaded or its box table could not be read.
    /// </summary>
    public ScummWalkBoxSet? CurrentWalkBoxes => _runtime.WalkBoxes;

    /// <summary>The room the session is in now (the room the game last loaded), without building a snapshot.</summary>
    public int? CurrentRoomId => _runtime.CurrentRoom;

    private readonly List<int> _roomsEntered = [];

    /// <summary>Every room the session has loaded, in order (a room a script enters and leaves within one frame is here too).</summary>
    public IReadOnlyList<int> RoomsEntered => _roomsEntered;

    /// <summary>The current class bits per object or actor number (a live view; the snapshot holds a copy).</summary>
    public IReadOnlyDictionary<int, uint> ObjectClasses => _runtime.ObjectClasses;
    public ScummBoundedScriptResult? LastEntryRun { get; private set; }
    public ScummObjectActionResult? LastAction { get; private set; }
    public IReadOnlyList<ScummScriptEvent> LastVisitEvents { get; private set; } = [];
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
    public int RoomEntrySequence { get; private set; }
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

    private int[] KnownInventory() => _runtime.GlobalVariables.TryGetValue(ScummVars.Ego, out var ego)
        ? _runtime.ObjectOwners.Where(pair => pair.Value == ego).Select(pair => pair.Key).Order().ToArray()
        : [];

    /// <summary>Discloses a reviewed catalog prelude after the target visit; supplies no game state.</summary>
    public void RecordCatalogPreparation(string note)
    {
        var description = "Catalog preparation (explicit): " + note;
        _uncertainties.Add(description);
        _visitTrace.Add(description);
    }

    public ScummSessionSnapshot VisitRoom(GameResource room, ScummObjectActionResult? reachedAction = null, bool preserveStaffLevel = false)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is null)
            throw new InvalidDataException("The session can only visit an indexed room.");
        if (_currentRoom?.ResourceId == room.ResourceId) return Snapshot;
        _browsingStaffFloor = null;
        if (preserveStaffLevel && reachedAction is null) BeginBrowsingStaffFloor();
        _uncertainties.StartStep();
        if (reachedAction is { IsGameInput: true })
            return EnterRoom(room, $"{reachedAction.Evidence} The explorer's input (assumed) went on through the game's own scripts; walks were taken to end at once.", reachedAction.ReachedRoomLoad);
        if (reachedAction is not null)
            return EnterRoom(room, $"{reachedAction.Evidence} Actor walk and input dispatch were not simulated.", reachedAction.ReachedRoomLoad);
        // The scenes end before the room is entered, so its entry runs outside a cutscene (bit[0xD6E] = 0): LOOM's
        // entries skip the ego's costume size (script 017) and their walk-box watchers while a cutscene is open.
        var closedScenes = CloseScenesForCatalogVisit(room.ResourceId.Value);
        if (closedScenes.Count > 0) EndClosedScenes(room.ResourceId.Value, closedScenes);
        CatalogEnter(room);
        ApplyBrowsingStaffFloor();
        _staffVerbSyncFrames = StaffVerbSyncWindow;
        _staffDisplayNeedsRefresh = true;
        SyncStaffVerbs();
        return Snapshot;
    }

    private ScummSessionSnapshot CatalogEnter(GameResource room)
    {
        var arrival = $"Room {room.ResourceId:00} selected from the catalog; the route to it was not executed from game input.";
        _runtime.Messages.RouteSkipped($"Room {room.ResourceId:00} was selected from the catalog, so prints on the route to it did not run");
        if (!_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !ScummActorTable.IsValid(ego))
            return EnterRoom(room, arrival);
        var entered = CatalogEnterWithEgo(room, arrival, ego);
        // A demo has no script 017, so the ego keeps the look the last room left; the demo's own preview says which look the room has.
        if (_game.IsDemo && !_demoPreview && LoomDemoLooks.EgoCostumes(_game).TryGetValue(room.ResourceId!.Value, out var costume))
        {
            _runtime.Actors.CostumeChanged(ego, costume, CurrentPlacement());
            _uncertainties.Add($"Room {room.ResourceId:00} was selected from the catalog: the ego wears costume {costume}, the one this demo's preview gives him when it first enters the room (the demo has no script 017 to choose it).");
        }
        return entered;
    }

    private ScummSessionSnapshot CatalogEnterWithEgo(GameResource room, string arrival, ushort ego)
    {
        var (assumed, label, chosen) = PickCatalogArrival(room.ResourceId!.Value, ego);
        // The ego's position before the arrival (where the route would have left it) is not known.
        _runtime.Actors.Forget(ego, assumed is null
            ? $"Room {room.ResourceId:00} was selected from the catalog, and no decoded loadRoomWithEgo, or loadRoom followed by putting the ego in the room, with literal operands brings the ego into it, so where the ego would arrive is unknown"
            : $"Room {room.ResourceId:00} was selected from the catalog, so where the ego stood before its assumed arrival is unknown");
        if (chosen is not null)
        {
            EstimateEgoSize(room.ResourceId.Value, chosen);
            ApplyArrivalClasses(room.ResourceId.Value, chosen);
        }
        return EnterRoom(room, label is null ? arrival : $"{arrival} {label}", assumed, label,
            chosen is { IsLoadRoomWithEgo: false } ? chosen : null);
    }

    /// <summary>
    /// A catalog visit while a cutscene plays leaves the scene, which the game never does: the scene's scripts would
    /// go on loading their own rooms, and input (off since the cutscene start script, script 002's
    /// <c>cursorCommand userInputSoftOff</c>) would stay off in the chosen room, so no click would be read. The session
    /// stops the script that began each open cutscene (innermost first) before the room is entered, and
    /// <see cref="EndClosedScenes"/> then ends them before the room is entered. Returns each closed cutscene's data and its script, or nothing
    /// when no cutscene is open.
    /// </summary>
    private List<(ushort Data, string Script)> CloseScenesForCatalogVisit(int roomId)
    {
        var closed = new List<(ushort, string)>();
        if (!InCutscene) return closed;
        while (_cutscenes.Count > 0)
        {
            _overrides.Remove(_cutscenes.Count);
            var (data, owner) = _cutscenes.Pop();
            var ownerScripts = _pending.Where(item => item.Frame == owner).ToArray();
            var script = ownerScripts.Length == 0
                ? "its script (no longer running)"
                : string.Join(", ", ownerScripts.Select(item => item.ScriptNumber is int number ? $"script {number:000}" : $"a Room {item.RoomId:00} script"));
            foreach (var item in ownerScripts)
                item.Frame.Stopped = true;
            _pending.RemoveAll(item => item.Frame == owner);
            _visitTrace.Add($"catalog visit to Room {roomId:00}: stops {script}, which began a cutscene (data {data})");
            closed.Add((data, script));
        }
        return closed;
    }

    /// <summary>
    /// Ends the cutscenes a catalog visit closed as <c>endCutscene</c> would (ScummVM: <c>VAR_OVERRIDE</c> = 0, then the
    /// script in <c>VAR_CUTSCENE_END_SCRIPT</c> with the cutscene's data), before the chosen room is entered: LOOM's
    /// script 003 turns the cursor and input back on, unfreezes the scripts and makes the camera follow the ego.
    /// An explorer action, labelled as such.
    /// </summary>
    private void EndClosedScenes(int roomId, IReadOnlyList<(ushort Data, string Script)> closed)
    {
        _runtime.SetGlobalVariable(OverrideVariable, 0);
        _runtime.EstimatedVariables.Remove(OverrideVariable);
        var known = _runtime.GlobalVariables.TryGetValue(CutsceneEndScriptVariable, out var endScript);
        foreach (var (data, script) in closed)
        {
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog while a cutscene played: the session stopped {script}, " +
                               (known
                                   ? $"and ran the cutscene end script ({CutsceneEndScriptVariable} = {endScript}) with data {data} as endCutscene would, so input is on again. "
                                   : $"but the cutscene end script ({CutsceneEndScriptVariable}) is unknown, so its effects were not applied. ") +
                               "An explorer action: the game never leaves a scene this way, and the scene's remaining room loads and texts did not run.");
            if (!known || endScript == 0) continue;
            _visitTrace.Add($"catalog visit to Room {roomId:00}: endCutscene runs script {endScript} with data {data}");
            RunStartedScript(endScript, 0, new Dictionary<string, ushort> { ["local[0x000]"] = data }, 0, []);
        }
    }

    /// <summary>
    /// A catalog visit does not run the route into the room. When decoded arrivals
    /// with literal operands bring the ego in (<c>loadRoomWithEgo</c>, or
    /// <c>loadRoom</c> followed by putting the ego in the room), one of them is
    /// taken as an assumed arrival. <c>loadRoomWithEgo</c> arrivals (the doorways)
    /// come first; <c>loadRoom</c> arrivals (mostly cutscene scripts) are used only
    /// when a room has none. Within that kind, those in the previous room's own
    /// code come first, then a seeded pick among the distinct placements. Returns a synthetic load
    /// event (for <see cref="PlaceEgoAfterEntry"/> or <see cref="PlaceEgoAfterLoadRoom"/>),
    /// the label that says so and the arrival, or nulls when no arrival is source-backed.
    /// </summary>
    private (ScummScriptEvent? Load, string? Label, LoomEgoArrival? Arrival) PickCatalogArrival(int roomId, int ego)
    {
        // A loadRoom arrival places a literal actor; it is the ego's only when var[0x001] holds it.
        var candidates = LoomEgoArrivalIndex.For(_game, _globalScriptCount).Into(roomId)
            .Where(item => item.EgoActor is null || item.EgoActor == ego).ToArray();
        if (candidates.Length == 0) return (null, null, null);
        var withEgo = candidates.Where(item => item.IsLoadRoomWithEgo).ToArray();
        var all = withEgo.Length > 0 ? withEgo : candidates;
        var previous = _currentRoom?.ResourceId;
        var fromPrevious = all.Where(item => item.SourceRoom is int source && source == previous).ToArray();
        var pool = fromPrevious.Length > 0 ? fromPrevious : all;
        var choices = pool.GroupBy(item => item.PlacementKey).Select(group => group.ToArray()).ToArray();
        var index = 0;
        string how;
        if (choices.Length == 1)
            how = fromPrevious.Length > 0
                ? $"the only one in Room {previous:00}'s own code"
                : $"the only source-backed arrival (Room {previous?.ToString("00", CultureInfo.InvariantCulture) ?? "none"} has none into it)";
        else
        {
            var pick = _runtime.Random.Pick($"catalog arrival into Room {roomId:00}", 0, choices.Length - 1);
            index = pick.Value;
            how = $"seeded pick {index + 1} of {choices.Length} (seed {pick.Seed})" +
                  (fromPrevious.Length > 0 ? $" among those in Room {previous:00}'s own code" : "") + "; alternatives: " +
                  string.Join("; ", choices.Where((_, i) => i != index).Select(Sources));
        }
        if (withEgo.Length == 0) how += "; no decoded loadRoomWithEgo brings the ego in, so this is a loadRoom followed by putting the ego in the room";
        var chosen = choices[index];
        var first = chosen[0];
        var label = $"Assumed arrival: the ego enters as {Sources(chosen)} would bring it in ({how}). " +
                    "Which route led here was not executed; the arrival is a possible outcome, not a reached one.";
        var load = first.IsLoadRoomWithEgo
            ? new ScummScriptEvent(0, first.Source, first.Offset, ScummScriptEventKind.RoomLoaded, "loadRoomWithEgo",
                $"assumed arrival into Room {roomId:00}", ResourceKind.Room, roomId, ScummScriptEventCertainty.Conditional,
                ResolvedArguments: [first.ObjectId, roomId, first.WalkX, first.WalkY])
            : new ScummScriptEvent(0, first.Source, first.Offset, ScummScriptEventKind.RoomLoaded, "loadRoom",
                $"assumed arrival into Room {roomId:00}", ResourceKind.Room, roomId, ScummScriptEventCertainty.Conditional,
                ResolvedArguments: [roomId]);
        return (load, label, first);

        static string Sources(LoomEgoArrival[] group) =>
            string.Join(" or ", group.Select(item => $"{item.Source} +0x{item.Offset:X4}")) + $" ({group[0].DescribePlacement()})";
    }

    /// <summary>
    /// LOOM's script 017 gives the ego one of three costume sizes: <c>startScript 0x11, [n]</c> sets
    /// <c>var[0x07C]</c> = n and the costume from <c>var[0x077..0x079]</c> (decoded). Most room entries pass a
    /// literal size; Rooms 02, 15, 17, 18, 22, 23, 24 and 36 pass <c>var[0x07C]</c>, the size the ego had in the room
    /// it came from. A catalog visit skips that room, so the size left by the room shown before is not the route's.
    /// The route the assumed arrival stands for passed through its source room, whose entry set the size: that
    /// size (followed back through rooms that pass <c>var[0x07C]</c> on) is taken as an estimate, unless the arrival's
    /// own code set it before the room load, as most doors do (<c>move var[0x07C] = n</c>) and some scripts do. With no literal size on the route, the kept
    /// value is labelled as unknown.
    /// </summary>
    private void EstimateEgoSize(int roomId, LoomEgoArrival arrival)
    {
        if (_game.Id is not (GameId.Loom or GameId.LoomVga)) return;
        var index = LoomEgoArrivalIndex.For(_game, _globalScriptCount);
        var starts = index.StartsOf(EgoSizeScript).ToArray();
        var sizeVariable = SizeVariableOf(starts);
        if (sizeVariable is null) return;
        // Only rooms whose entry keeps the size of the room before read it; the others set their own.
        if (!starts.Any(item => item.IsRoomEntry && item.SourceRoom == roomId && item.FirstArgument == sizeVariable)) return;
        var (size, how, own) = SizeOnRoute(arrival);
        // No size of its own on the arrival: the size left by rooms further back says nothing about the depth the ego
        // arrives at. The room's own samples (a size and the place it comes with) do: take the nearest in depth.
        if (!own && arrival.WalkX != -1 && NearestDepthSample(roomId, arrival, index, starts, sizeVariable) is { } sample)
        {
            size = sample.Size;
            how = $"the arrival stands at y {arrival.WalkY}; the room's own nearest sample in depth is size {sample.Size} at y {sample.Y} ({sample.Source}); the size carried from other rooms was not used";
        }
        _runtime.EstimatedVariables.Add(sizeVariable);
        if (size is int value)
        {
            _runtime.SetGlobalVariable(sizeVariable, (ushort)value);
            _visitTrace.Add($"catalog visit to Room {roomId:00}: {sizeVariable} = {value} (estimate, {how})");
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: the ego's size ({sizeVariable}, script {EgoSizeScript:000}) is estimated as {value} from the assumed arrival's route: {how}.");
        }
        else
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: no literal size for the ego ({sizeVariable}, script {EgoSizeScript:000}) is on the assumed arrival's route, so the size left by the room shown before is kept and is unknown.");

        (int? Size, string How, bool Own) SizeOnRoute(LoomEgoArrival first)
        {
            // The arrival's own code first (the size it set before the load: most doors move a literal into var[0x07C],
            // Room 16's six give 0, 1 or 2), then the entries of the rooms it came through.
            var own = starts.Where(item => item.Source == first.Source && item.Offset < first.Offset && Literal(item) is not null)
                .Select(item => (item.Offset, Size: Literal(item)!.Value, How: $"{item.Source} +0x{item.Offset:X4} startScript {EgoSizeScript:000}, [{item.FirstArgument}] before the load"))
                .Concat(index.MovesTo(sizeVariable).Where(item => item.Source == first.Source && item.Offset < first.Offset)
                    .Select(item => (item.Offset, Size: item.Value, How: $"{item.Source} +0x{item.Offset:X4} sets {sizeVariable} = {item.Value} before the load")))
                .OrderByDescending(item => item.Offset).ToArray();
            if (own.Length > 0) return (own[0].Size, own[0].How, true);
            var queue = new Queue<(int Room, string Path)>();
            var seen = new HashSet<int> { roomId };
            if (first.SourceRoom is int source && seen.Add(source)) queue.Enqueue((source, $"the route came through Room {source:00}"));
            while (queue.Count > 0)
            {
                var (room, path) = queue.Dequeue();
                var entry = starts.FirstOrDefault(item => item.IsRoomEntry && item.SourceRoom == room);
                if (entry is null) continue;
                if (Literal(entry) is int literal)
                    return (literal, $"{path}, whose entry sets it (Room {room:00} entry +0x{entry.Offset:X4} startScript {EgoSizeScript:000}, [{entry.FirstArgument}])", false);
                if (entry.FirstArgument != sizeVariable) continue;
                foreach (var before in index.Into(room))
                    if (before.SourceRoom is int earlier && seen.Add(earlier))
                        queue.Enqueue((earlier, $"{path}, which keeps the size of the room before it; a route came through Room {earlier:00}"));
            }
            return (null, "", false);
        }

        static int? Literal(LoomScriptStart start) =>
            start.FirstArgument is { } argument && argument.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(argument.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// The variable script 017 is started with by the room entries that keep the size of the room before (var 0x07C in the EGA game, 0x0AE
    /// on the CD): the one most room entries pass, found in the code and not by number.
    /// </summary>
    private static string? SizeVariableOf(LoomScriptStart[] starts) =>
        starts.Where(item => item.IsRoomEntry && item.FirstArgument is { } argument && argument.StartsWith("var[", StringComparison.Ordinal))
            .GroupBy(item => item.FirstArgument!).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();

    private sealed record DepthSample(int Y, int Size, string Source);

    /// <summary>
    /// Samples of (depth, size) inside <paramref name="roomId"/>, from the game's own code: the other arrivals into the room
    /// that set a size themselves and walk to a point, and the room's local scripts that start script 017 with a literal
    /// size and then <c>putActor</c> the ego (the walk-box watchers' jumps, as Room 17's local 200). Returns the sample
    /// nearest in y to the arrival (the smaller size on a tie), or null when the room has none.
    /// </summary>
    private DepthSample? NearestDepthSample(int roomId, LoomEgoArrival arrival, LoomEgoArrivalIndex index, LoomScriptStart[] starts, string sizeVariable)
    {
        var samples = new List<DepthSample>();
        foreach (var other in index.Into(roomId).Where(a => a.WalkX != -1 && a.PlacementKey != arrival.PlacementKey))
        {
            var set = index.MovesTo(sizeVariable).Where(m => m.Source == other.Source && m.Offset < other.Offset).OrderByDescending(m => m.Offset).FirstOrDefault();
            if (set is not null) samples.Add(new(other.WalkY, set.Value, $"{other.Source} +0x{other.Offset:X4} walks to ({other.WalkX}, {other.WalkY}) after {sizeVariable} = {set.Value}"));
        }
        if (_game.Rooms.Any(r => r.ResourceId == roomId))
            foreach (var id in _data.RoomLocalScriptIds(roomId))
            {
                ScummScriptDisassembly? script;
                try { script = _data.RoomLocalScript(roomId, id); }
                catch (Exception ex) when (ex is InvalidDataException or IOException) { continue; }
                if (script is null) continue;
                int? size = null; var since = 0;
                foreach (var ins in script.Instructions)
                {
                    since++;
                    if (ins.Operation == "startScript" && ins.Operands.StartsWith($"0x{EgoSizeScript:X2}, [0x", StringComparison.Ordinal) &&
                        int.TryParse(ins.Operands.AsSpan(ins.Operands.IndexOf("[0x", StringComparison.Ordinal) + 3).TrimEnd(']').TrimEnd(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
                    { size = literal; since = 0; }
                    else if (size is int known && since <= 3 && ins.Operation == "putActor" && ins.Operands.StartsWith("0x01, ", StringComparison.Ordinal))
                    {
                        var parts = ins.Operands.Split(", ");
                        if (parts.Length == 3 && int.TryParse(parts[2].AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var y))
                            samples.Add(new(y, known, $"Room {roomId:00} local {id} +0x{ins.Offset:X4} puts the ego there after startScript {EgoSizeScript:000} [{known}]"));
                    }
                }
            }
        return samples.OrderBy(sample => Math.Abs(sample.Y - arrival.WalkY)).ThenBy(sample => sample.Size).Cast<DepthSample?>().FirstOrDefault();
    }

    /// <summary>
    /// A catalog visit skips the arrival's own code, which may change the classes of the room's objects before its
    /// load; the room's entry then tests them. Room 12 is reached only through script 053, which clears object 322's
    /// class 24 (+0x022C) before its <c>loadRoom</c> (+0x0242); Room 12's entry puts the boat (actor 9) and gives
    /// Bobbin costume 17 only while that class is clear (+0x0013). The arrival's literal <c>setClass</c>es on the
    /// target room's objects that come before its load in the same code are applied, labelled as an estimate: which
    /// branch led to the load was not executed.
    /// </summary>
    private void ApplyArrivalClasses(int roomId, LoomEgoArrival arrival)
    {
        var index = LoomEgoArrivalIndex.For(_game, _globalScriptCount);
        foreach (var setClass in index.SetClassesIn(arrival.Source)
                     .Where(item => item.Offset < arrival.Offset && index.RoomHasObject(roomId, item.ObjectId)))
        {
            _runtime.ObjectClasses.TryGetValue(setClass.ObjectId, out var bits);
            foreach (var value in setClass.Values)
            {
                var cls = value & 0x7F;
                if (value == 0) bits = 0;
                else if (cls is >= 1 and <= 32)
                    bits = (value & 0x80) != 0 ? bits | (1u << (cls - 1)) : bits & ~(1u << (cls - 1));
            }
            _runtime.SetObjectClasses(setClass.ObjectId, bits);
            _visitTrace.Add($"catalog visit to Room {roomId:00}: object {setClass.ObjectId} classes set to 0x{bits:X6} ({arrival.Source} +0x{setClass.Offset:X4}, estimate)");
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: {arrival.Source} +0x{setClass.Offset:X4} sets object {setClass.ObjectId}'s classes " +
                               $"before the assumed arrival's load (+0x{arrival.Offset:X4}), so they were applied (estimate: the branch that led to the load was not executed).");
        }
    }

    /// <summary>LOOM's script 017 (the ego's costume size) and the size it keeps (decoded).</summary>
    private const int EgoSizeScript = 0x11;

    /// <summary>ScummVM <c>VAR_EGO</c>.</summary>
    private const string EgoVariable = ScummVars.Ego;

    /// <summary>Actor rooms, positions and walk boxes as reached code left them (actors 1–12).</summary>
    public IReadOnlyList<ScummActorSnapshot> Actors => _runtime.Actors.Snapshot();

    /// <summary>The game's costume with this number (EGA or CD data), or null when it is not indexed.</summary>
    public ScummCostume? ReadCostume(int id) => _data.ReadCostume(id);

    private ScummActorTable.Placement CurrentPlacement() =>
        new(_runtime.CurrentRoom, _runtime.WalkBoxes, _runtime.WalkBoxFlagsUnknown, _runtime.ObjectClasses);

    private ScummSessionSnapshot EnterRoom(GameResource room, string arrival, ScummScriptEvent? load = null, string? assumedArrival = null,
        LoomEgoArrival? placeAfterLoadRoom = null)
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
        if (IsStaffMenu) _runtime.Messages.VerbDrawn(new ScummScreenBox(0, ScummVerbArea.Top, 319, 199, 0));
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
        else if (placeAfterLoadRoom is not null && ego is int putEgo) PlaceEgoAfterLoadRoom(placeAfterLoadRoom, putEgo);
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
    private void PlaceEgoAfterEntry(ScummScriptEvent load, int ego, string? assumedArrival = null)
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

    /// <summary>
    /// An assumed <c>loadRoom</c> arrival: the source script carries on after the
    /// load (ScummVM <c>startScene</c> runs the entry first), puts the ego in the
    /// room (<c>putActorInRoom</c>) and applies its decoded puts and walks. Only
    /// those steps and the camera are applied, not the script's other effects.
    /// <c>startScene</c> leaves the camera in normal mode (it does not follow the
    /// ego), so the camera is the script's literal <c>setCameraAt</c> when it has
    /// one, else where the entry left it. Walks end at once; every value is an
    /// estimate labelled with the arrival.
    /// </summary>
    private void PlaceEgoAfterLoadRoom(LoomEgoArrival arrival, int ego)
    {
        var source = $"{arrival.Source} +0x{arrival.Offset:X4}";
        var estimate = $"assumed arrival at {source} after a catalog visit";
        _runtime.Actors.PutInRoom(ego, _currentRoom?.ResourceId, CurrentPlacement(), ego);
        foreach (var step in arrival.Steps!)
        {
            switch (step.Kind)
            {
                case LoomEgoArrivalStepKind.Put:
                    _runtime.Actors.Put(ego, step.A, step.B, CurrentPlacement(), ego);
                    break;
                case LoomEgoArrivalStepKind.PutAtObject:
                {
                    // o5_putActorAtObject: the object's walk point (the index keeps only objects in the room).
                    var record = _currentObjects.FirstOrDefault(item => item.Id == step.A);
                    if (record is null) _runtime.Actors.Put(ego, 240, 120, CurrentPlacement(), ego);
                    else _runtime.Actors.Put(ego, record.WalkX, record.WalkY, CurrentPlacement(), ego);
                    break;
                }
                case LoomEgoArrivalStepKind.Walk:
                    _runtime.Actors.Walk(ego, step.A, step.B, CurrentPlacement(), $"the ego's walk at {arrival.Source} +0x{step.Offset:X4}", atOnce: true);
                    break;
            }
            _runtime.Actors.MarkEstimate(ego, estimate);
        }
        if (arrival.CameraX is int cameraX)
        {
            _runtime.Camera.Mode = ScummCamera.Normal;
            SetCamera(cameraX, $"setCameraAt in {source}'s assumed arrival", $"{source}: setCameraAt, camera x = {{0}}");
        }
        var state = _runtime.Actors[ego];
        _visitTrace.Add($"{source}: the ego (actor {ego}) stands at ({state.X?.ToString() ?? "?"}, {state.Y?.ToString() ?? "?"}), box {state.WalkBox?.ToString() ?? "?"}" +
                        (state.Estimate is null ? "" : $" (estimate: {state.Estimate})"));
    }

    /// <summary>ScummVM <c>VAR_CAMERA_POS_X</c>.</summary>
    private const string CameraVariable = ScummVars.CameraPosX;

    // The current room's width and height in pixels.
    private (int Width, int Height)? _roomSize;

    /// <summary>
    /// ScummVM's main loop puts the pointer's room position in <c>VAR_VIRT_MOUSE_X/Y</c>
    /// (vars 20 and 21) every frame. The session has no pointer, so it states one:
    /// the pointer rests just below the room picture (over the verb area), under
    /// the camera's centre. No room object reaches that row, so <c>findObject</c>
    /// finds nothing there, and the input loop idles as it would with the pointer
    /// off the room. This is assumed input, and every read of it is labelled.
    /// </summary>
    private void PlaceVirtualMouse()
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
    private (int X, int Y)? _pointer;
    private string _pointerNote = HoverNote;
    private const string HoverNote = "placed by the explorer's hover, with no click";

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

    /// <summary>ScummVM <c>VAR_VERB_SCRIPT</c> (v3: var 32): the script <c>runInputScript</c> runs for every click. The boot sets it to 5 (script 001 +0x016B).</summary>
    private const string VerbScriptVariable = "var[0x020]";
    /// <summary>ScummVM <c>VAR_TMR_2</c> (v3: var 12), jiffies the engine adds every frame; script 006 resets it on a click and reads it to find a double click.</summary>
    private const string ClickTimerVariable = "var[0x00C]";
    /// <summary>ScummVM <c>kSceneClickArea</c>: a click in the room picture.</summary>
    public const int SceneClickArea = 2;
    /// <summary>Assumed time since the previous click for a single click: one second (60 jiffies), past script 006's 40-jiffy double-click window.</summary>
    public const int SingleClickGap = 60;
    /// <summary>Assumed time between the two clicks of a double click: 10 jiffies, inside script 006's 40-jiffy window.</summary>
    public const int DoubleClickGap = 10;

    /// <summary>
    /// Sends a left click at a room point to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does for a click in the room: <c>runInputScript(2, 0, 0)</c>
    /// runs the script in <c>VAR_VERB_SCRIPT</c> (script 005) with those arguments,
    /// which chains a room click into the input loop (script 006, <c>local[0]</c> = 1).
    /// Script 006 then does what it does with the object under the pointer: verb
    /// 0x37, the ego's walk, and script 007's sentence. This is assumed input: the
    /// point, and the time since the previous click (<see cref="SingleClickGap"/>, or
    /// <see cref="DoubleClickGap"/> between the two clicks of a double click), which
    /// the engine's timer would give. The click is not sent while user input is off
    /// (<c>cursorCommand</c>), or when no input script is set. The pointer stays at
    /// the point afterwards. Walks end at once, as elsewhere; a <c>waitFor actor</c>
    /// makes the script go on on the next frame, so the sentence and anything it
    /// loads follow in later frames (<see cref="AdvanceFrame"/>).
    /// </summary>
    public ScummObjectActionResult Click(int x, int y, bool doubleClick = false)
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null; // Subsequent input belongs to gameplay, beyond catalog arrival.
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in it.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var input = $"A {(doubleClick ? "double click" : "click")} at ({x}, {y}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (_roomSize is not { } size || x < 0 || y < 0 || x >= size.Width || y >= size.Height)
            refused = "the point is outside the room picture";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM runInputScript({SceneClickArea}, 0, {InputMode}) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT, set by script 001 +0x016B); the click's point and timing are the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            if (doubleClick) SendSceneClick(verbScript, x, y, SingleClickGap, "the first click of a double click");
            SendSceneClick(verbScript, x, y, doubleClick ? DoubleClickGap : SingleClickGap,
                doubleClick ? "the second click of a double click" : "a single click");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>
    /// The third argument of <c>runInputScript</c>: ScummVM's <c>checkExecVerbs</c>
    /// passes the mouse button only for versions above 3 (<c>_game.version &gt; 3 ? code : 0</c>),
    /// so LOOM's input script always gets 0. Script 005 +0x0000 sets <c>local[2]</c> = 0 first anyway.
    /// </summary>
    public const int InputMode = 0;
    /// <summary>ScummVM <c>kVerbClickArea</c>: a click in the verb screen.</summary>
    public const int VerbClickArea = 1;
    /// <summary>ScummVM <c>VAR_MOUSE_X</c>/<c>VAR_MOUSE_Y</c> (v3: vars 44 and 45), the pointer's screen point; script 005 reads x for verb 0x31.</summary>
    private const string MouseXVariable = "var[0x02C]";
    private const string MouseYVariable = "var[0x02D]";

    /// <summary>
    /// The verb area as the session's verb slots would draw it, with each image
    /// verb's rectangle from its object's image in the room it was taken from
    /// (<see cref="ScummVerbArea.Compose"/>).
    /// </summary>
    // Product rule: the skill menu never displays the distaff; possession is kept for the next room.
    private bool IsStaffMenu => _currentRoom?.ResourceId == 69 && !_game.IsDemo;

    public ScummVerbAreaPicture VerbArea() => ScummVerbArea.Compose(
        _runtime.Verbs.Snapshot().Where(v => !IsStaffMenu || !(v.VerbId is >= 1 and <= 8 or >= 0x31 and <= 0x35 or >= 0x5A and <= 0x61)),
        VerbImage, _runtime.Verbs.Erases(), alignRows: _game.Id == GameId.LoomVga);

    private readonly Dictionary<int, IReadOnlyList<RoomObjectImage>> _verbImageRooms = [];

    private RoomObjectImage? VerbImage(int roomId, int objectId)
    {
        if (!_verbImageRooms.TryGetValue(roomId, out var images))
        {
            try { images = _data.ReadObjectImages(roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { images = []; }
            _verbImageRooms[roomId] = images;
        }
        return images.FirstOrDefault(item => item.Id == objectId);
    }

    /// <summary>
    /// Sends a left click at a screen point in the verb area (rows
    /// <see cref="ScummVerbArea.Top"/>–199) to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does: <c>findVerbAtPos</c> over the verb slots
    /// (<see cref="ScummVerbArea.FindVerbAt"/>), then <c>runInputScript(1, verb, 0)</c>
    /// on a verb that is on, or <c>runInputScript(1, 0, 0)</c> elsewhere in the verb
    /// screen. Script 005 (area 1) turns a distaff note into script 010, verb 0x35
    /// into script 009 and the close-up (0x36) into script 007. This is assumed
    /// input: the point, which the session puts in <c>VAR_MOUSE_X</c>/<c>Y</c>
    /// (<c>var[0x02C]</c>/<c>var[0x02D]</c>), is the explorer's. The pointer is off the
    /// room picture then, so the room pointer is taken off. Refused while user input
    /// is off or when no input script is set, as <see cref="Click"/> is. Verbs the
    /// session cannot draw (unknown mode, image or position, text verbs) cannot be found.
    /// </summary>
    public ScummObjectActionResult ClickVerbArea(int x, int y)
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in the verb area.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var area = VerbArea();
        var verb = ScummVerbArea.FindVerbAt(area.Items, x, y);
        var input = verb is null
            ? $"A click at screen ({x}, {y}) in the verb area, on no verb that is on, sent to the game's input script as assumed input"
            : $"A click at screen ({x}, {y}) on verb 0x{verb.VerbId:X2} (slot {verb.Slot}) in the verb area, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (x < 0 || x >= ScummVerbArea.Width || y < ScummVerbArea.Top || y >= ScummVerbArea.Top + ScummVerbArea.Height)
            refused = "the point is outside the verb area";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        var verbId = verb?.VerbId ?? 0;
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: findVerbAtPos, then runInputScript({VerbClickArea}, 0x{verbId:X2}, {InputMode}) runs script {verbScript} " +
                           "(var[0x020], VAR_VERB_SCRIPT); the click's point is the explorer's, not the game's, and is put in var[0x02C]/var[0x02D] (VAR_MOUSE_X/Y).");
        if (verb?.Estimate is { } estimate)
            _uncertainties.Add($"Verb 0x{verbId:X2}'s slot follows an estimate: {estimate}.");
        if (area.Notes.Count > 0)
            _uncertainties.Add($"Verbs the session cannot draw cannot be found by the click: {string.Join("; ", area.Notes)}.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            // The pointer is over the verb screen, below the room picture (VAR_VIRT_MOUSE_Y past the room).
            _pointer = null;
            PlaceVirtualMouse();
            _runtime.SetGlobalVariable(MouseXVariable, (ushort)x);
            _runtime.SetGlobalVariable(MouseYVariable, (ushort)y);
            _runtime.EstimatedVariables.Remove(MouseXVariable);
            _runtime.EstimatedVariables.Remove(MouseYVariable);
            _visitTrace.Add($"click: verb area at screen ({x}, {y}), " + (verb is null ? "on no verb" : $"verb 0x{verbId:X2} (slot {verb.Slot})") +
                            $", runs script {verbScript} with [{VerbClickArea}, {verbId}, {InputMode}] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = VerbClickArea,
                ["local[0x001]"] = (ushort)verbId,
                ["local[0x002]"] = InputMode
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>ScummVM <c>kKeyClickArea</c>: a key press.</summary>
    public const int KeyClickArea = 4;
    /// <summary>ScummVM <c>VAR_CUTSCENEEXIT_KEY</c> (v3: var 24); the boot sets it to 0x1B (script 001 +0x012C).</summary>
    private const string CutsceneExitKeyVariable = "var[0x018]";
    /// <summary>ScummVM <c>VAR_RESTART_KEY</c> (v3: var 42); the boot sets it to 0x142, F8 (script 001 +0x0131).</summary>
    private const string RestartKeyVariable = "var[0x02A]";
    /// <summary>ScummVM <c>VAR_PAUSE_KEY</c> (v3: var 43); the boot sets it to 0x20, space (script 001 +0x0136).</summary>
    private const string PauseKeyVariable = "var[0x02B]";

    /// <summary>
    /// A key as ScummVM's <c>processKeyboard</c> hands it to scripts: printable
    /// characters as their ASCII code, Escape 0x1B, F1–F9 as 315–323 (0x13B–0x143),
    /// Ctrl+A–Z as 1–26.
    /// </summary>
    public static string DescribeKey(int key) => key switch
    {
        0x1B => "Escape",
        >= 0x13B and <= 0x143 => $"F{key - 0x13B + 1}",
        >= 1 and <= 26 => $"Ctrl+{(char)('A' + key - 1)}",
        0x20 => "space",
        > 0x20 and < 0x7F => $"'{(char)key}'",
        _ => $"key 0x{key:X2}"
    };

    /// <summary>
    /// Sends a key press to the game, as ScummVM's <c>processKeyboard</c> and
    /// <c>checkExecVerbs</c> do for v3: the restart key (<c>VAR_RESTART_KEY</c>) and
    /// the pause key (<c>VAR_PAUSE_KEY</c>) and '.' (talk stop) are the engine's and are not sent;
    /// Escape, while <c>VAR_CUTSCENEEXIT_KEY</c> is nonzero, first runs <c>abortCutscene</c>
    /// (the script that set the open cutscene's override continues at its skip jump
    /// on the next frame, with <c>VAR_OVERRIDE</c> = 1) and then goes on as that
    /// variable's key; otherwise, with user input on,
    /// <c>runInputScript(4, key, 1)</c> runs <c>VAR_VERB_SCRIPT</c> (script 005, area 4:
    /// note letters, Escape, text speed, F5–F7, Ctrl+V). A verb whose key matches would
    /// be clicked instead (<c>runInputScript(1, verb, 1)</c>), but no decoded
    /// <c>verbOps</c> in the release sets a key, so the session does not keep keys.
    /// The key is assumed input.
    /// </summary>
    public ScummObjectActionResult PressKey(int key)
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before pressing a key.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var name = DescribeKey(key);
        var input = $"The key {name} (0x{key:X2}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        bool Is(string variable) => _runtime.GlobalVariables.TryGetValue(variable, out var value) && value != 0 && value == key;
        string? refused = null;
        ushort verbScript = 0;
        // processKeyboard: Escape, while VAR_CUTSCENEEXIT_KEY is nonzero, runs
        // abortCutscene, then passes VAR_CUTSCENEEXIT_KEY's value on as the key.
        string? aborted = null;
        if (key == 0x1B && _runtime.GlobalVariables.TryGetValue(CutsceneExitKeyVariable, out var exitKey) && exitKey != 0)
        {
            aborted = AbortCutscene();
            _uncertainties.Add($"{input}: ScummVM processKeyboard runs abortCutscene first ({CutsceneExitKeyVariable}, VAR_CUTSCENEEXIT_KEY, is 0x{exitKey:X2}): {aborted}.");
            key = exitKey;
        }
        if (key <= 0 || key > 0x1FF)
            refused = "it is not a key code the engine sends";
        else if (Is(RestartKeyVariable))
            refused = $"it is the restart key ({RestartKeyVariable}, VAR_RESTART_KEY): the engine restarts the game, which the session does not model";
        else if (Is(PauseKeyVariable))
            refused = $"it is the pause key ({PauseKeyVariable}, VAR_PAUSE_KEY): the engine pauses the game and the scripts never see it";
        else if (key == '.')
            refused = "'.' is the engine's talk-stop key (v3 has no VAR_TALKSTOP_KEY, so it is always on): it ends the current message and the scripts never see it";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no key then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            // After abortCutscene the key was acted on, even when no script reads it.
            _uncertainties.Add(aborted is null
                ? $"{input}: not sent, because {refused}."
                : $"{input}: the key did not reach the input script after abortCutscene, because {refused}.");
            LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: runInputScript({KeyClickArea}, 0x{key:X2}, 1) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT); the key is the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            _visitTrace.Add($"key: {name} runs script {verbScript} with [{KeyClickArea}, 0x{key:X2}, 1] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = KeyClickArea,
                ["local[0x001]"] = (ushort)key,
                ["local[0x002]"] = 1
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    // After an input script ran: dispatch the loads it requested, or visit the room a reached load names.
    private ScummObjectActionResult FinishInput(int beforeUncertainties, int beforeEvents)
    {
        var action = LastAction! with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        LastAction = action;
        if (DispatchRequestedLoads("during the click"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        var target = roomLoad?.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is not null)
        {
            action = action with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
            VisitRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    private void SendSceneClick(int verbScript, int x, int y, int gap, string which)
    {
        _pointer = (x, y);
        _pointerNote = "where the explorer clicked; it stays there after the click";
        PlaceVirtualMouse();
        _runtime.VirtualMouseAssumption = $"assumed input: {which} at ({x}, {y}), sent by the explorer";
        _runtime.SetGlobalVariable(ClickTimerVariable, (ushort)gap);
        _runtime.EstimatedVariables.Remove(ClickTimerVariable);
        _uncertainties.Add($"Assumed input timing for {which}: var[0x00C] (VAR_TMR_2, jiffies since script 006 last reset it) = {gap}; the frame loop counts it on from there, but the player's time before the click is not known.");
        _visitTrace.Add($"click: {which} at ({x}, {y}) runs script {verbScript} with [{SceneClickArea}, 0, {InputMode}] (runInputScript)");
        RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
        {
            ["local[0x000]"] = SceneClickArea,
            ["local[0x001]"] = 0,
            ["local[0x002]"] = InputMode
        }, 0);
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
    private void SetCamera(int x, string? estimate, string trace)
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

    private void ForgetCamera(string reason)
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
    private void FollowEgoWalk(ScummActorSnapshot walked, string source)
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

    public ScummObjectActionResult ActivateObjectVerb(int objectId, int verbId)
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before activating an object verb.");
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId) ??
                     throw new InvalidDataException($"Object {objectId} has no code record in Room {sourceRoomId:00}.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _uncertainties.Add($"Object {objectId} verb 0x{verbId:X2} was activated directly; input dispatch, actor walk, and class-based hit selection were not simulated.");
        if (Snapshot.IsUntouchable(objectId))
            _uncertainties.Add($"Object {objectId} is untouchable (class {ScummObjectClasses.Untouchable}) in this preview, so the game's findObject would not select it; its verb ran only as an exploration action.");
        ScummScriptDisassembly? script = null;
        ScummBoundedScriptResult? run = null;
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            script = _data.ReadObjectVerb(_currentRoom.ResourceId!.Value, record, verbId, out _);
            run = RunScript(script, _currentObjects, 0, null, 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {sourceRoomId:00} Object {objectId} verb 0x{verbId:X2}: {ex.Message}");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        LastAction = new ScummObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, null, null,
            _uncertainties.Skip(beforeUncertainties).ToArray());
        // A global script the verb started may have loaded a room and carried on.
        if (DispatchRequestedLoads("during the action"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        var targetId = roomLoad?.TargetId;
        var target = targetId is int id
            ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is null)
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
        var action = new ScummObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, roomLoad,
            target?.ResourceId, _uncertainties.Skip(beforeUncertainties).ToArray());
        if (target is not null)
        {
            VisitRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    /// <summary>
    /// A catalog visit by room number (<see cref="VisitRoom"/>), for a replay sequence that moves on to a
    /// room without the game's own route, keeping the story state reached so far.
    /// </summary>
    public ScummSessionSnapshot VisitRoomById(int roomId) =>
        VisitRoom(_game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) ??
                  throw new ArgumentOutOfRangeException(nameof(roomId), $"Room {roomId:00} is not in this game."));

    /// <summary>
    /// A labelled setup edit: stands the ego at (<paramref name="x"/>, <paramref name="y"/>) in the current room
    /// (<c>putActor</c>, adjusted into the walk boxes) with the camera put on it as <c>loadRoomWithEgo</c> does (the
    /// ego's x, rounded to a strip, inside the camera limits) and following it. It states where a scene may start when
    /// the explorer's own walker cannot reach that place by the game's route; the result is an estimate, listed as such.
    /// Needs a settled session (no scene, movement or dialogue), like the Game Status edits.
    /// </summary>
    public void PlaceEgo(int x, int y)
    {
        _staffVerbSyncFrames = 0;
        if (!StatusEditable) throw new InvalidOperationException("Finish the current scene, draft, movement or dialogue before placing the ego.");
        if (_currentRoom?.ResourceId is not int room || !_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !ScummActorTable.IsValid(ego))
            throw new InvalidOperationException("Visit a room with a known ego before placing it.");
        _browsingStaffFloor = null;
        _uncertainties.StartStep();
        var label = $"Explorer setup (explicit): the ego was placed at ({x}, {y}) in Room {room:00} and the camera put on it; where the game's route would have left them is not simulated";
        _runtime.Actors.Put(ego, x, y, room, CurrentPlacement(), ego);
        _runtime.Actors.MarkEstimate(ego, label);
        var (min, max) = _runtime.CameraRange ?? _runtime.RoomCameraBounds ?? (160, 160);
        var placed = _runtime.Actors[ego];
        var camera = Math.Clamp((placed.X ?? x) & 0xFFF8, min, Math.Max(min, max));
        _runtime.SetGlobalVariable(CameraVariable, (ushort)camera);
        _runtime.EstimatedVariables.Add(CameraVariable);
        _runtime.Camera.DestX = camera;
        _runtime.Camera.MovingToActor = false;
        _runtime.Camera.Mode = ScummCamera.FollowActor;
        _runtime.Camera.Follows = ego;
        _uncertainties.Add($"{label}; camera x = {camera} is an estimate that follows from it.");
        _visitTrace.Add($"{label}: the ego stands at ({placed.X}, {placed.Y}), box {placed.WalkBox?.ToString() ?? "?"}, camera {camera} (estimate)");
    }

    /// <summary>
    /// A labelled setup edit: the ego starts a walk to (<paramref name="x"/>, <paramref name="y"/>) in the current room as the game's <c>walkActorTo</c>
    /// does, stepped by the frames along the walk boxes' route (unlike a click, whose walk ends at once), so a script that watches the ego's box sees
    /// every box on the way. Where the walk began is where the room's entry or the arrival left the ego. Needs a settled session, like <see cref="PlaceEgo"/>.
    /// </summary>
    public void WalkEgoTo(int x, int y)
    {
        _staffVerbSyncFrames = 0;
        if (!StatusEditable) throw new InvalidOperationException("Finish the current scene, draft, movement or dialogue before walking the ego.");
        if (_currentRoom?.ResourceId is not int room || !_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !ScummActorTable.IsValid(ego))
            throw new InvalidOperationException("Visit a room with a known ego before walking it.");
        _uncertainties.StartStep();
        var label = $"Explorer setup (explicit): the ego walks to ({x}, {y}) in Room {room:00}; the walk starts where the room's entry left him";
        _runtime.Actors.Walk(ego, x, y, CurrentPlacement(), label);
        _uncertainties.Add(label + ".");
        _visitTrace.Add(label);
    }

    /// <summary>
    /// A labelled setup edit: brings <paramref name="actor"/> into the current room, as <c>putActorInRoom</c> then
    /// <c>putActor</c> (or <c>putActorAtObject</c>, the object's walk point) in the game's own code at <paramref name="basis"/>
    /// does, for a scene whose lines or moves need that actor present (<c>actorTalk</c> shows nothing for an actor outside
    /// the room). Nothing happens when the actor is already in the room. The place is an estimate: which of the game's
    /// placements preceded the scene was not executed. Needs a settled session, like <see cref="PlaceEgo"/>.
    /// </summary>
    public void PlaceActorInRoom(int actor, int x, int y, int atObject, string basis)
    {
        if (!StatusEditable) throw new InvalidOperationException("Finish the current scene, draft, movement or dialogue before placing an actor.");
        if (_currentRoom?.ResourceId is not int room || !ScummActorTable.IsValid(actor))
            throw new InvalidOperationException("Visit a room before placing an actor in it.");
        if (_runtime.Actors[actor].Room == room) return;
        if (atObject != 0)
        {
            var record = _currentObjects.FirstOrDefault(item => item.Id == atObject);
            (x, y) = record is null ? (240, 120) : (record.WalkX, record.WalkY);
        }
        _uncertainties.StartStep();
        var label = $"Explorer setup (explicit, from {basis}): actor {actor} was brought into Room {room:00} at ({x}, {y}); which earlier scene left it there is not simulated";
        // The game dresses the actor next to the placement (costume, elevation, talk colour before it, the stand frame after it):
        // without that he keeps whatever the last room left in his actor slot (El Muerto came out as Room 02's floor shimmer).
        var dress = LoomSceneStateScanner.For(_game).FindActorPlacers(actor, room).FirstOrDefault(p => p.Where == basis)?.Dress;
        if (dress?.Costume is int costume) _runtime.Actors.CostumeChanged(actor, costume, CurrentPlacement());
        if (dress?.Elevation is int elevation) _runtime.Actors.SetElevation(actor, elevation);
        if (dress?.TalkColor is int talk) _runtime.Actors.SetTalkColor(actor, talk);
        _runtime.Actors.PutInRoom(actor, room, CurrentPlacement(), null);
        _runtime.Actors.Put(actor, x, y, room, CurrentPlacement(), null);
        if (dress?.Animation is int frame) _runtime.Actors.Animate(actor, frame, CurrentPlacement());
        _runtime.Actors.MarkEstimate(actor, label);
        _uncertainties.Add(label + " (estimate).");
        _visitTrace.Add($"{label}: actor {actor} stands at ({x}, {y}) (estimate)");
    }

    /// <summary>
    /// Starts the current room's local script <paramref name="scriptId"/> at instruction offset
    /// <paramref name="offset"/>, as a bonus preview of a scene whose trigger the game's own flow
    /// has no decoded way to meet (for example a watcher that needs the ego to step from one walk
    /// box straight into another). It is an exploration action like <see cref="ActivateObjectVerb"/>:
    /// the watcher's test is skipped, everything after the offset is the game's own code, and
    /// any running copy of the script is ended first, as <c>runScript</c> does.
    /// </summary>
    public ScummObjectActionResult RunLocalScriptAt(int scriptId, int offset)
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before starting one of its scripts.");
        if (scriptId < _globalScriptCount)
            throw new ArgumentOutOfRangeException(nameof(scriptId), "Only a room-local script can be started at an offset.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var input = $"Room {sourceRoomId:00} local script {scriptId} was started directly at +0x{offset:X4}";
        _uncertainties.Add($"{input}: an exploration action, not the game's flow. The condition that guards this offset was not met or tested; input dispatch and the ego's walk were not simulated.");
        LastAction = new ScummObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try { RunStartedScript(scriptId, offset, new Dictionary<string, ushort>(), 0); }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>
    /// Continues every pending script by one frame. When a continued script
    /// reaches a literal room load, the session visits that room, as
    /// <see cref="ActivateObjectVerb"/> does for a load reached directly, and
    /// the last action records the continued load as its outcome.
    /// </summary>
    public ScummSessionSnapshot AdvanceFrame()
    {
        var snapshot = AdvanceOneFrame();
        if (_staffVerbSyncFrames > 0 && StatusEditable) { SyncStaffVerbs(); _staffVerbSyncFrames--; snapshot = Snapshot; }
        if (_browsingStaffFloor.HasValue) { ApplyBrowsingStaffFloor(); snapshot = Snapshot; }
        if (!_playingBoot) return snapshot;
        ContinuePlayedBoot();
        return Snapshot;
    }

    private int _musicTimer;

    private const int StaffVerbSyncWindow = 32;
    private int _staffVerbSyncFrames;
    private bool _staffDisplayNeedsRefresh;

    /// <summary>
    /// After a catalog visit the staff in the verb area follows the staff bit, as the ego's staff animation does (script 017 picks it from
    /// the same bit): a room that was entered without script 018 or 019 running left the note verbs off with the staff held, or on without it.
    /// It waits while a cutscene runs (scenes hide and show the verbs themselves).
    /// </summary>
    private void SyncStaffVerbs()
    {
        if (InCutscene || DraftPlaying || GameStatus.StaffHeld is not bool held) return;
        if (IsStaffMenu) return;
        var notesOn = _runtime.Verbs.Snapshot().Any(verb => verb.VerbId is >= 1 and <= 8 && verb.Mode is 1 or 2);
        // startScene erases printed note letters while leaving the image verb slots on.
        // Rebuild on entry even if those slots already agree with possession.
        if (_staffDisplayNeedsRefresh)
        {
            // The strip is independent of entry walks/camera motion. Refresh it before returning the catalog visit;
            // the ego's costume can wait until entry movement has settled.
            _staffDisplayNeedsRefresh = !StatusEditable;
            _uncertainties.Add("Room entered from the catalog: the distaff appearance and note controls are refreshed through scripts 017–019 from the current possession state (explorer adjustment; the route was not played).");
            if (StatusEditable) RefreshStatusEgo(held);
            RefreshStatusNotes(held);
            return;
        }
        if (held == notesOn) return;
        _uncertainties.Add($"Room entered from the catalog: the staff verbs were {(notesOn ? "on" : "off")} while the staff is {(held ? "held" : "not held")}; they follow the staff bit (explorer adjustment).");
        RefreshStatusNotes(held);
    }

    /// <summary>
    /// scummLoop's <c>updateMusicTimer</c>: <c>VAR_MUSIC_TIMER</c> (var[0x00E]) follows the markers of the playing Roland music, which the opening
    /// (script 044) waits on to time its fades. Cards whose sounds carry no markers leave the variable alone.
    /// </summary>
    private void UpdateMusicTimer()
    {
        if (_game.Id == GameId.LoomVga)
        {
            // The CD game's timer follows the disc cue (Sound::updateMusicTimer with GF_AUDIOTRACKS): see ScummMessageTable.CdMusicTimer.
            _runtime.Messages.AdvanceCdTimer(FrameJiffies);
            if (_runtime.Messages.CdTimerRunning)
                _runtime.SetGlobalVariable(MusicTimerVariable, (ushort)Math.Min(_runtime.Messages.CdMusicTimer, ushort.MaxValue));
            return;
        }
        var timer = _soundClock.MusicTimer();
        if (timer == 0 && _musicTimer == 0) return;
        _musicTimer = timer;
        _runtime.SetGlobalVariable(MusicTimerVariable, (ushort)timer);
    }

    private const string MusicTimerVariable = ScummVars.MusicTimer;

    private ScummSessionSnapshot AdvanceOneFrame()
    {
        // scummLoop (v3): the talk delay goes down by the frame's jiffies and displayDialog
        // runs before the scripts; VAR_HAVE_MSG takes _haveMsg.
        // processActors ends the previous frame: animateCostume steps the limbs of the actors in the room.
        _runtime.Actors.AnimateCostumes(CurrentPlacement());
        UpdateTimersAndDelays(FrameJiffies);
        foreach (var sound in _soundClock.Advance(FrameJiffies))
        {
            _runtime.ActiveSounds.Remove(sound);
            _visitTrace.Add($"Sound {sound:00} completed on its native AdLib resource clock.");
        }
        UpdateMusicTimer();
        _runtime.Messages.StartFrame(FrameJiffies, _runtime);
        PlaceVirtualMouse();
        var beforeEvents = _visitEvents.Count;
        var runnable = _pending.ToArray();
        _pending.Clear();
        _frameScripts.AddRange(runnable);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            foreach (var item in runnable)
            {
                _frameScripts.Remove(item);
                // A room load earlier in this frame ended the room's scripts.
                if (item.RoomId.HasValue && item.RoomId != _currentRoom?.ResourceId) continue;
                // An earlier script in this frame may have stopped this one.
                if (item.Frame.Stopped) continue;
                // runAllScripts skips frozen and paused (delayed) slots; they keep their place.
                if (item.Frame.FreezeCount > 0 || item.Frame.Delay is not null)
                {
                    _pending.Add(item);
                    continue;
                }
                // Every script works on the current room's objects (ScummVM _objs): a global
                // script pended in an earlier room must not see that room's objects.
                RunScript(item.Script, _currentObjects, item.Offset, item.Locals, 0, item.ScriptNumber,
                    item.EstimatedLocals, item.PathEstimated, item.Frame);
                DispatchRequestedLoads("after a frame advance");
            }
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
            _frameScripts.Clear();
        }
        StepWalksAndCamera();
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        if (roomLoad is null || _currentRoom is null) return Snapshot;
        var target = roomLoad.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (target is null)
        {
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
            return Snapshot;
        }
        EnterReachedLoad(roomLoad, target, null, "after a frame advance");
        return Snapshot;
    }

    /// <summary>
    /// The end of ScummVM's <c>scummLoop</c> after the scripts: <c>walkActors</c> steps every walking or turning actor
    /// in the room (<see cref="ScummActorTable.StepWalks"/>), then <c>moveCamera</c> moves the camera up to 8 pixels
    /// (<see cref="ScummCamera.Move"/>); the next frame's <c>processActors</c> draws and animates them.
    /// </summary>
    private void StepWalksAndCamera()
    {
        if (_currentRoom is null) return;
        var place = CurrentPlacement();
        _runtime.Actors.StepWalks(place);
        if (!_runtime.GlobalVariables.TryGetValue(CameraVariable, out var camera) || _runtime.RoomCameraBounds is not { } bounds) return;
        var (min, max) = _runtime.CameraRange ?? bounds;
        var cameraState = _runtime.Camera;
        ScummActorSnapshot? followed = null;
        if (cameraState.Mode == ScummCamera.FollowActor || cameraState.MovingToActor)
            followed = ScummActorTable.IsValid(cameraState.Follows) ? _runtime.Actors[cameraState.Follows] : null;
        var moved = cameraState.Move(camera, min, max, bounds, followed?.X);
        if (moved == camera) return;
        _runtime.SetGlobalVariable(CameraVariable, (ushort)moved);
        if (followed?.Estimate is not null && _runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {moved} is an estimate: it follows actor {followed.Number}, whose position is an estimate ({followed.Estimate}).");
    }

    /// <summary>True while an actor in the current room walks or turns (ScummVM <c>_moving</c>).</summary>
    public bool ActorsMoving => _runtime.Actors.Snapshot().Any(actor => actor.Moving != 0 && actor.Room == _currentRoom?.ResourceId);

    /// <summary>
    /// True while the ego (<c>VAR_EGO</c>) walks or turns. Playback waits for this, not for every actor: ambient
    /// actors (script 042's swan speck in Room 02) walk from point to point without end.
    /// </summary>
    public bool EgoMoving => _runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) && ScummActorTable.IsValid(ego) &&
                             _runtime.Actors[ego] is { Moving: not 0 } actor && actor.Room == _currentRoom?.ResourceId;

    /// <summary>
    /// True while <c>moveCamera</c> would still move the camera: it is not on the strip of its target (the followed
    /// actor while moving to it, else its destination), with the target kept inside the camera's limits. A camera held
    /// at a room edge short of its actor does not count, although ScummVM keeps <c>_movingToActor</c> set then.
    /// </summary>
    public bool CameraMoving
    {
        get
        {
            if (CameraX is not int x || _runtime.RoomCameraBounds is not { } bounds) return false;
            var (min, max) = _runtime.CameraRange ?? bounds;
            var camera = _runtime.Camera;
            var target = camera.DestX;
            if (camera.MovingToActor && ScummActorTable.IsValid(camera.Follows) && _runtime.Actors[camera.Follows].X is int actorX)
                target = actorX;
            target = Math.Clamp(Math.Clamp(target, min, Math.Max(min, max)), bounds.Min, bounds.Max);
            return x / 8 != target / 8;
        }
    }

    /// <summary>
    /// Advances frames while scripts are pending, until the session has left
    /// the room and no cutscene is open. Frames outside a cutscene count
    /// against <paramref name="maxFrames"/>; frames inside one count against
    /// <paramref name="maxCutsceneFrames"/>, because the player has no control
    /// until the cutscene ends. Returns true when the session is in another room.
    /// Since messages last their talk delay (<see cref="ScummMessageTable"/>),
    /// cutscenes take the game's number of frames: the Elders' trial (script 046,
    /// from Room 08's door) runs 2,120 frames, about 3.5 minutes at 10 frames a
    /// second, so the default limit is <see cref="CutsceneFrameLimit"/>.
    /// </summary>
    public bool ContinueUntilRoomChanges(int maxFrames, int maxCutsceneFrames = CutsceneFrameLimit)
    {
        _uncertainties.StartStep();
        var room = _currentRoom?.ResourceId;
        var frames = 0;
        var cutsceneFrames = 0;
        while (_pending.Count > 0)
        {
            var inCutscene = InCutscene;
            if (!inCutscene && _currentRoom?.ResourceId != room) break;
            if (inCutscene ? cutsceneFrames++ >= maxCutsceneFrames : frames++ >= maxFrames)
            {
                if (inCutscene)
                    _uncertainties.Add($"A cutscene was still open after {maxCutsceneFrames} continued frames; the preview stopped following it.");
                break;
            }
            AdvanceFrame();
        }
        return _currentRoom?.ResourceId != room;
    }

    // A selected execution may carry estimates; a merely discovered load is not execution.
    private static bool IsExecutedRoomLoad(ScummScriptEvent item) =>
        item.Kind == ScummScriptEventKind.RoomLoaded && item.Executed &&
        item.Certainty is ScummScriptEventCertainty.Reached or ScummScriptEventCertainty.Conditional;

    /// <summary>
    /// Enters the room a global script loaded and resumes that script there in
    /// the same frame. In ScummVM, <c>startScene</c> ends room, object and local
    /// scripts, but a global script returns from <c>loadRoom</c> and carries on
    /// (a cutscene can pass through several rooms). Returns true when a room was entered.
    /// </summary>
    private bool DispatchRequestedLoads(string when)
    {
        var entered = false;
        for (var hop = 0; hop < MaxNestedScripts && _requestedLoad is { } request; hop++)
        {
            _requestedLoad = null;
            var item = _pending.FirstOrDefault(pending => pending.Frame == request.Frame);
            if (item is null) continue;
            _pending.Remove(item);
            _dispatchedLoads.Add(request.Event);
            var target = _game.Rooms.First(room => room.ResourceId == request.Event.TargetId);
            EnterReachedLoad(request.Event, target, request.Frame, when);
            entered = true;
            _visitTrace.Add($"{request.Event.SourceLabel} continues at +0x{item.Offset:X4} in Room {target.ResourceId:00} after {request.Event.Operation}");
            _dispatchScope++;
            try
            {
                RunScript(item.Script, _currentObjects, item.Offset, item.Locals, 0, item.ScriptNumber,
                    item.EstimatedLocals, item.PathEstimated, item.Frame);
            }
            finally
            {
                _dispatchScope--;
            }
        }
        return entered;
    }

    /// <summary>
    /// Visits the room a reached load names and records it as the outcome of
    /// the last action when it follows from it: the action's own script, or a
    /// later load by the same global script that carried the action's first load.
    /// </summary>
    private void EnterReachedLoad(ScummScriptEvent roomLoad, GameResource target, ScriptFrame? frame, string when)
    {
        if (_currentRoom is null || _booting)
        {
            _actionLoadFrame = frame;
            _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} {when}.");
            if (target.ResourceId != _currentRoom?.ResourceId)
                EnterRoom(target, $"Room {target.ResourceId:00} entered by {roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} ({roomLoad.Operation}) {when}.", roomLoad);
            return;
        }
        var sourceRoomId = _currentRoom.ResourceId ?? 0;
        var beforeUncertainties = _uncertainties.Count;
        ScummObjectActionResult action;
        if (LastAction is { ReachedRoomLoad: null } pending && pending.SourceRoomId == sourceRoomId)
            action = pending with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        else if (frame is not null && frame == _actionLoadFrame &&
                 LastAction is { ReachedRoomLoad: { } earlier } last && last.DestinationRoomId == sourceRoomId)
        {
            _uncertainties.Add($"{earlier.SourceLabel} passed through Room {sourceRoomId:00} ({earlier.Operation} at +0x{earlier.Offset:X4}) before this load.");
            action = last with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        }
        else
            action = new ScummObjectActionResult(sourceRoomId, 0, 0, roomLoad.Offset, null, null, roomLoad,
                target.ResourceId, []);
        _actionLoadFrame = frame;
        _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} {when}.");
        VisitRoom(target, action);
        LastAction = action with { Uncertainties = [.. action.Uncertainties, .. _uncertainties.Skip(beforeUncertainties)] };
    }

    /// <summary>
    /// ScummEngine::isScriptRunning: a global or local script slot with this
    /// number is running (executing, waiting for a later frame, or frozen).
    /// A script that ended at a step the preview does not model might still
    /// run in the game, so that answer is an estimate.
    /// </summary>
    private (bool Running, string? Estimate) IsScriptRunning(int number)
    {
        var running = _executing.Any(frame => frame.Number == number && !frame.Stopped) ||
                      _pending.Any(item => item.ScriptNumber == number && !item.Frame.Stopped) ||
                      _frameScripts.Any(item => item.ScriptNumber == number && !item.Frame.Stopped);
        return running || !_endedUnmodelled.Contains(number)
            ? (running, null)
            : (false, $"script {number} ended at a step the preview does not model, so it may still run in the game");
    }

    private readonly List<ScummScriptEvent> _visitEvents = [];
    private readonly List<ScummSessionSoundEvent> _soundEvents = [];
    private readonly List<string> _visitTrace = [];
    private readonly List<ScummRandomPick> _picks = [];
    // Scripts running now, outermost first; the last one is the current script.
    private readonly List<ScriptFrame> _executing = [];
    // Slots still awaiting their turn this frame remain visible to nested
    // freeze/unfreeze and isScriptRunning, as vm.slot does in runAllScripts.
    private readonly List<PendingScript> _frameScripts = [];
    // beginCutscene's vm.cutSceneData stack, and the script whose cutscene start
    // script is running (vm.cutSceneScriptIndex), which freezeScripts leaves running.
    private readonly Stack<(ushort Data, ScriptFrame? Owner)> _cutscenes = new();
    // ScummVM vm.cutScenePtr/cutSceneScript per cutscene depth (0: no cutscene open).
    private readonly Dictionary<int, (ScriptFrame Frame, int JumpOffset)> _overrides = new();
    private ScriptFrame? _cutsceneOwner;
    // Room loads by global scripts: the script pauses after the load, the session
    // enters the room, and the script resumes there (only inside an action or frame).
    private int _dispatchScope;
    // Inside an action, input or frame advance, the session enters the first
    // reached room load once the scripts have run (ActivateObjectVerb, FinishInput, AdvanceFrame).
    private int _entersRoomLoads;
    // True while the constructor runs the boot; its loads are not player actions.
    private bool _booting;
    private (ScummScriptEvent Event, ScriptFrame Frame)? _requestedLoad;
    private readonly HashSet<ScummScriptEvent> _dispatchedLoads = new(ReferenceEqualityComparer.Instance);
    private ScriptFrame? _actionLoadFrame;
    // chainScript: the script that chained, and what it starts once it has ended.
    private (ScriptFrame Frame, int Script, IReadOnlyDictionary<string, ushort> Arguments,
        IReadOnlyCollection<string> EstimatedArguments, bool PathEstimated)? _chained;
    // Script numbers whose last run ended at a step the preview does not model.
    private readonly HashSet<int> _endedUnmodelled = [];

    /// <summary>ScummVM <c>VAR_CUTSCENE_START_SCRIPT</c>, <c>VAR_CUTSCENE_END_SCRIPT</c> and <c>VAR_OVERRIDE</c>.</summary>
    private const string CutsceneStartScriptVariable = "var[0x023]";
    private const string CutsceneEndScriptVariable = "var[0x024]";
    private const string OverrideVariable = "var[0x005]";

    /// <summary>Session-level steps (such as stopped scripts) in the order they were applied.</summary>
    public IReadOnlyList<string> SessionTrace => _visitTrace;

    private void RunGlobalHook(int variableId, string phase, bool pathEstimated = false)
    {
        var key = $"var[0x{variableId:X3}]";
        if (!_runtime.GlobalVariables.TryGetValue(key, out var scriptId))
        {
            _uncertainties.Add($"Global {phase} hook {key} is unknown because startup has not established it.");
            return;
        }
        if (scriptId != 0) RunIndexedScript(scriptId, 0, null, 0, pathEstimated: pathEstimated);
    }

    private void RunEmbeddedRoomScript(GameResource room, bool exit, bool pathEstimated = false)
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

    private ScummBoundedScriptResult? RunIndexedScript(int scriptId, int startOffset,
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

    private ScummBoundedScriptResult? RunStartedScript(int scriptId, int startOffset,
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
    private void StartObjectScript(ScummScriptEvent scriptEvent, int depth)
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
        if (verbId is 0x36 or 0x38) ObjectSentencesBegun++;
        RunScript(script, _currentObjects, 0, locals, depth + 1, null,
            EstimatedArguments(scriptEvent),
            scriptEvent.Certainty != ScummScriptEventCertainty.Reached,
            new ScriptFrame { OriginRoom = parent?.OriginRoom, ObservationCall = _observer?.CurrentCall });
    }

    private ScummBoundedScriptResult RunScript(ScummScriptDisassembly script,
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

    private string? HandleEvent(ScummScriptEvent scriptEvent, int depth)
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
    private string BeginCutscene(ScummScriptEvent scriptEvent, int depth)
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
    private string EndCutscene(ScummScriptEvent scriptEvent, int depth)
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
    private void RecordOverride(int? jumpOffset)
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

    private string AbortCutscene()
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
    private string FreezeScripts(ScummScriptEvent scriptEvent)
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
    private string[] EstimatedArguments(ScummScriptEvent scriptEvent)
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

    private static bool TryReadLocalArguments(string operands,
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
    private sealed class UncertaintyList : List<string>
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        public new void Add(string item)
        {
            if (_seen.Add(item)) base.Add(item);
        }
        public void StartStep() => _seen.Clear();
    }

    private sealed record PendingScript(ScummScriptDisassembly Script, int Offset,
        IReadOnlyDictionary<string, ushort> Locals, int? RoomId, int? ScriptNumber,
        IReadOnlyCollection<string> EstimatedLocals, bool PathEstimated, ScriptFrame Frame);

    /// <summary>stopScript kills existing global/local slots, including nested
    /// callers and slots awaiting their turn in this frame. It does not kill a
    /// replacement created later with the same number (runScriptNested).</summary>
    private int StopRunningScript(int number)
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
    private sealed class ScriptFrame
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

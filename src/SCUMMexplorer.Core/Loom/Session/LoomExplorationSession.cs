using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// Keeps a conservative, route-scoped LOOM state. Only instructions reached by
/// bounded execution change the snapshot. Unsupported state or timing is
/// recorded and prevents the snapshot from being labelled faithful.
/// </summary>
public sealed partial class LoomExplorationSession : ScummSession
{
    private readonly bool _demoPreview;

    // The session accepts old-v3 LOOM and the LOOM CD game only; checked before the generic core is built.
    private static GameLibrary Supported(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != ScummVersion.V3 && game.Id != GameId.LoomVga)
            throw new InvalidDataException("Only old-v3 LOOM and the LOOM CD game have a session interpreter.");
        return game;
    }

    // LOOM's sound lifetimes: the EGA game's native AdLib or Roland resources; the CD game's native clock does not establish one.
    private static Func<GameResource, ScummSoundLifetime> LoomSoundLifetime(GameLibrary game) => resource =>
        game.Id == GameId.Loom
            ? ScummRolandSong.IsRoland(resource) ? ScummRolandSong.ReadLifetime(resource) : new ScummAdlibAudio().ReadLifetime(resource)
            : new(ScummSoundLifetimeKind.Unknown, null, false, "native LOOM AdLib clock does not establish this game's sound lifetime");

    protected override bool IsObjectSentence(int verbId) => verbId is 0x36 or 0x38;

    // Subsequent input belongs to gameplay, beyond a catalog arrival: the staff browsing and the verb sync window end.
    protected override void BeforeInput()
    {
        _staffVerbSyncFrames = 0;
        _browsingStaffFloor = null;
    }

    protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) => VisitRoom(target, action);

    /// <summary>
    /// scummLoop's <c>updateMusicTimer</c>: <c>VAR_MUSIC_TIMER</c> (var[0x00E]) follows the markers of the playing Roland music, which the opening
    /// (script 044) waits on to time its fades. The CD game's timer follows the disc cue instead.
    /// </summary>
    protected override void UpdateMusicTimer()
    {
        if (_game.Id == GameId.LoomVga)
        {
            // The CD game's timer follows the disc cue (Sound::updateMusicTimer with GF_AUDIOTRACKS): see ScummMessageTable.CdMusicTimer.
            _runtime.Messages.AdvanceCdTimer(FrameJiffies);
            if (_runtime.Messages.CdTimerRunning)
                _runtime.SetGlobalVariable(MusicTimerVariable, (ushort)Math.Min(_runtime.Messages.CdMusicTimer, ushort.MaxValue));
            return;
        }
        base.UpdateMusicTimer();
    }

    // Product rule: the skill menu never displays the distaff; possession is kept for the next room.
    private bool IsStaffMenu => _currentRoom?.ResourceId == 69 && !_game.IsDemo;

    protected override bool ErasesVerbScreenOnEntry => IsStaffMenu;

    protected override bool HidesVerb(ScummVerbSnapshot verb) =>
        IsStaffMenu && (verb.VerbId is >= 1 and <= 8 or >= 0x31 and <= 0x35 or >= 0x5A and <= 0x61);

    protected override bool AlignVerbRows => _game.Id == GameId.LoomVga;

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
        : base(Supported(game), indexedObjects, randomSeed, observer, data, LoomSoundLifetime(game))
    {
        _demoPreview = demoPreview;
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
            chosen is { IsLoadRoomWithEgo: false } placed ? putEgo => PlaceEgoAfterLoadRoom(placed, putEgo) : null);
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
    /// Continues every pending script by one frame. When a continued script
    /// reaches a literal room load, the session visits that room, as
    /// <see cref="ActivateObjectVerb"/> does for a load reached directly, and
    /// the last action records the continued load as its outcome.
    /// </summary>
    public override ScummSessionSnapshot AdvanceFrame()
    {
        var snapshot = AdvanceOneFrame();
        if (_staffVerbSyncFrames > 0 && StatusEditable) { SyncStaffVerbs(); _staffVerbSyncFrames--; snapshot = Snapshot; }
        if (_browsingStaffFloor.HasValue) { ApplyBrowsingStaffFloor(); snapshot = Snapshot; }
        if (!_playingBoot) return snapshot;
        ContinuePlayedBoot();
        return Snapshot;
    }


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


}

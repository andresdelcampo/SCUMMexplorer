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
            AdlibLifetime(game), ScummCdAudioTracks.For(game) is { } cd ? cd.Lifetime : null)
    {
        _cdTracks = ScummCdAudioTracks.For(game);
        // ScummEngine::resetScummVars (vars.cpp): the sound card the session plays through (AdLib is 3), the video mode, the text speed.
        var profile = ScummProfile.For(game);
        // ScummEngine_v2 constructor (scumm.cpp): inherited by DOS v1 as well.
        if (_runtime.IsV2) _runtime.Flashlight = (6, 4);
        if (_runtime.IsV2) _runtime.Actors.InitializeV2Game(profile.Dialect == ScummScriptDialect.V1);
        _runtime.V2VerbYOffset = profile.V2VerbYOffset;
        _runtime.IsClassicManiac = profile.RestoresClassicCutsceneScene;
        if (_runtime.IsClassicManiac) _runtime.MainScreen = (16, 152); // resetScumm: classic DOS Maniac's 136-row room.
        if (profile.SeparateBitVariables)
        {
            // readVar/writeVar: from version 4 the bit variables are _bitVars, not bits of the word variables.
            _runtime.GlobalVariables[ScummBoundedScriptExecutor.SeparateBitStorage] = 1;
            _runtime.EstimatedVariables.Add(ScummBoundedScriptExecutor.SeparateBitStorage);
        }
        if (!_runtime.IsV2) SoundCard = AdlibCard;
        if (!_runtime.IsV2 && profile.VideoMode is int videoMode) _runtime.SetGlobalVariable(ScummVars.VideoMode, (ushort)videoMode);
        if (!_runtime.IsV2) _runtime.SetGlobalVariable(ScummVars.CharInc, ScummProfile.CharIncAtStart);
        // resetScummVars (scumm.cpp): the lights start as base palette, actors' own colours and room lights on (1 | 8 | 2).
        _runtime.SetGlobalVariable(_runtime.CurrentLightsVariable, 11);
        foreach (var (variable, value) in profile.EngineVariablesAtStart) _runtime.SetGlobalVariable(variable, value);
        // The engine allocates every global variable zeroed (scumm.cpp: _scummVars = calloc), so a variable no script has written yet reads 0, not "unknown".
        for (var variable = 0; variable < profile.GlobalVariableCount; variable++)
        {
            var key = $"var[0x{variable:X3}]";
            if (!_runtime.GlobalVariables.ContainsKey(key)) _runtime.SetGlobalVariable(key, 0);
        }
        _runtime.CameraFollowStartsScene = profile.CameraFollowStartsScene;
        _runtime.PublishesCursorVariables = profile.PublishesCursorVariables;
        _runtime.UntouchableClass = profile.UntouchableClass;
        _runtime.Verbs.ImagesFromNamedRoom = profile.VerbImagesFromNamedRoom;
        // getActorFromPos, from the actors as the last frame drew them (S6: Indiana Jones's Script 20 finds the person under the pointer with it). An engine
        // query, not a click: since S4b MonkeyCd (17 October 2026) every generic session answers it (Monkey Island CD's interface loop, Script 23, asks it from the boot on).
        _runtime.ActorFromPosition = ActorFromPosition;
        if (profile.PlaysThroughVerbs)
        {
            // convertVerbMessage for prints: Script 18 says a dialogue choice through code 5 of the chosen verb (+0x0068).
            _runtime.VerbName = PlayVerbName;
        }
        _musicStart = profile.MusicStart;
        _bootInput = profile.BootInput;
        _waitsForControl = profile.BootEndsWhenPlayerHasControl;
        if (boot) Boot();
    }

    /// <summary>
    /// Boots a session for play with the starting object states of the game's own index: <c>00.LFL</c> for the old-bundle games, the v4-style index
    /// (one file per room or several disks) with the game's data source otherwise, as the first visit does (S6 Indy3Vga, 16 October 2026).
    /// </summary>
    public static ScummGameSession BootForPlay(GameLibrary game, out ScummIndexedObjectSnapshot indexedObjects)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (ScummDataSources.For(game) is ScummV2DataSource v2)
        {
            indexedObjects = v2.ReadIndexedObjects();
            return new ScummGameSession(game, indexedObjects, data: v2);
        }
        if (!ScummProfile.For(game).UsesV4Archive)
        {
            indexedObjects = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
            return new ScummGameSession(game, indexedObjects);
        }
        // S6 MonkeyCd (17 October 2026): a v5 game reads its starting objects from its own index (MONKEY.000's DOBJ), as its first visit does.
        indexedObjects = (game.V4Archive as ScummV4Archive ?? game.V4Disks?.Disks.OfType<ScummV4Archive>().FirstOrDefault())?.ReadIndexedObjects()
            ?? game.V4Disks?.Disks.OfType<ScummV5Archive>().FirstOrDefault()?.ReadIndexedObjects()
            ?? throw new InvalidDataException("The game has no v4 or v5 index to read its objects from.");
        return new ScummGameSession(game, indexedObjects, 0, null, ScummDataSources.For(game));
    }

    // S6 sound (16 October 2026): a music start stops what the game's AdLib driver stops (ScummProfile.MusicStart). The session's device is AdLib whatever
    // the app plays through, so this follows Player_AD or iMuse even when the user listens on the PC speaker (Player_V2 is not modelled).
    private readonly ScummMusicStart _musicStart;

    // S4b MonkeyCd (17 October 2026): a game whose sounds play tracks of its disc (ScummCdAudioTracks; ScummVM GF_AUDIOTRACKS). The drive plays one track at a
    // time, and VAR_MUSIC_TIMER follows the CD timer, which playCDTrack sets to 0 and which then adds one per jiffy (soundcd.cpp, sound.cpp updateMusicTimer).
    private readonly ScummCdAudioTracks? _cdTracks;
    private int? _cdSound;
    private int _cdTimer;
    private bool _cdTimerRunning;

    protected override void UpdateMusicTimer()
    {
        if (_cdTracks is null)
        {
            base.UpdateMusicTimer();
            return;
        }
        // The timer keeps counting after the track has ended on its own (only stopCDSound removes it).
        if (_cdTimerRunning) _cdTimer += FrameJiffies;
        if (_cdTimerRunning || _cdTimer > 0) _runtime.SetGlobalVariable(MusicTimerVariable, (ushort)Math.Min(_cdTimer, ushort.MaxValue));
    }

    // stopCDSound / stopAllCDSounds (soundcd.cpp): a script's stop of the playing CD sound removes the timer; its value stays.
    protected override void AfterScriptStopsSound(int? soundId)
    {
        if (_cdTracks is null || _cdSound is not int playing || soundId is int stopped && stopped != playing) return;
        _cdTimerRunning = false;
        _cdSound = null;
    }

    protected override void BeforeSoundStarts(ScummScriptEvent start, int soundId)
    {
        if (_cdTracks?.Plays(soundId) == true)
        {
            if (_cdSound is int previousTrack && previousTrack != soundId && _soundClock.Playing.Contains(previousTrack))
                EngineStopsSounds(start, [previousTrack],"playCDTrack", $"{_cdTracks.Describe(soundId)}; the drive plays one track at a time (SoundCD::playCDTrack)");
            _cdSound = soundId;
            _cdTimer = 0;
            _cdTimerRunning = true;
            _visitTrace.Add($"CD: {_cdTracks.Describe(soundId)}; VAR_MUSIC_TIMER restarts at 0 and counts jiffies (SoundCD::playCDTrack, cdTimerHandler).");
            return;
        }
        var rule = _musicStart;
        if (rule == ScummMusicStart.StopsNothing || !_soundClock.IsMusic(soundId)) return;
        if (rule == ScummMusicStart.StopsAllSounds)
        {
            if (_soundClock.Playing.Count > 0 || _runtime.ActiveSounds.Count > 0)
                EngineStopsSounds(start, null, "triggerSound", $"music {soundId} starts; iMuse's Monkey Island workaround stops every sound first (Sound::triggerSound)");
            return;
        }
        var previous = _soundClock.Playing.Where(id => id != soundId && _soundClock.IsMusic(id)).Order().ToArray();
        if (previous.Length > 0)
            EngineStopsSounds(start, previous, "Player_AD startSound", $"music {soundId} starts; the AdLib player has one music slot (Player_AD::startSound stopMusic)");
    }

    /// <summary><c>VAR_SOUNDCARD</c> for AdLib (vars.cpp: 0 PC speaker, 1 Tandy, 2 CMS, 3 AdLib, 4 Roland).</summary>
    private const int AdlibCard = 3;

    // The session's device is AdLib (ScummVM's Player_AD for the old-bundle games): the native clock of a sound is the AdLib resource's own.
    // A resource that is not an old-bundle AdLib part has no established lifetime.
    private static Func<GameResource, ScummSoundLifetime> AdlibLifetime(GameLibrary game)
    {
        if (game.Version is ScummVersion.V1 or ScummVersion.V2)
            return new ScummPcSpeakerAudio().ReadV2Lifetime;
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
        // S4b Indy4 and S4b Monkey2 (17 October 2026, written independently and reconciled on this one): a v5 game plays its music through iMUSE; the AdLib part's own timeline (ScummImuseTimeline, the S3 player's default
        // path: hooks at 0, markers not acted on) gives how long a sound plays. A song whose timeline jumps back loops until a script stops it.
        if (game.V4Disks is { } v5Disks && v5Disks.Disks.OfType<ScummV5Archive>().FirstOrDefault() is { } v5)
        {
            var parts = new Dictionary<int, byte[]?>();
            foreach (var sound in v5.ReadSounds()) parts.TryAdd(sound.Id, sound.ImuseAdlib);
            var lifetimes = new Dictionary<int, ScummSoundLifetime>();
            return resource => resource.ResourceId is not int id ? new(ScummSoundLifetimeKind.Unknown, null, false, "no sound id")
                : lifetimes.TryGetValue(id, out var known) ? known : lifetimes[id] = ImuseLifetime(parts.GetValueOrDefault(id));
        }
        return resource => adlib.ReadLifetime(resource, timing);
    }

    private static ScummSoundLifetime ImuseLifetime(byte[]? part)
    {
        if (part is null) return new(ScummSoundLifetimeKind.Unknown, null, false, "no AdLib iMUSE part in the sound block");
        try
        {
            var timeline = ScummImuseTimeline.Build(ScummImuseSong.Parse(part));
            const string why = "iMUSE default path (hooks 0, markers and script-set loops not simulated): an estimate";
            return timeline.Loops
                ? new(ScummSoundLifetimeKind.Looping, null, true, why)
                : new(ScummSoundLifetimeKind.Finite, (int)Math.Min(int.MaxValue, Math.Ceiling(timeline.EndSeconds * 472)), true, why);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return new(ScummSoundLifetimeKind.Unknown, null, false, $"the iMUSE part could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// A room visit that does not play the route to the room (S5, 14 October 2026): the room is entered from the state the boot left, as ScummVM's
    /// <c>room</c> debugger command would, and its global entry hook, entry script and child scripts run. The ego arrives as one decoded
    /// <c>loadRoomWithEgo</c> into the room would bring it (a seeded, labelled pick among the distinct placements, those in the room the session
    /// is in first); no story state is written. The visit is a state preview, never a reached outcome.
    /// </summary>
    public ScummSessionSnapshot VisitRoom(GameResource room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId) throw new InvalidDataException("The session can only visit an indexed room.");
        if (_currentRoom?.ResourceId == roomId) return Snapshot;
        _uncertainties.StartStep();
        var arrival = $"Room {roomId:00} selected without playing the route to it (state preview): its entry scripts ran from the state the boot left.";
        _runtime.Messages.RouteSkipped($"Room {roomId:00} was entered without its route, so prints on the route to it did not run");
        var arrivals = ScummEgoArrivalIndex.For(_game);
        if (!_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !_runtime.Actors.Contains(ego)) return EnterWithoutEgo(room, arrival, arrivals);
        var candidates = arrivals.Into(roomId);
        if (candidates.Count == 0)
        {
            _uncertainties.Add($"Room {roomId:00} has no decoded loadRoomWithEgo with literal operands that brings the ego into it, so the ego is not placed in it.");
            return EnterWithoutEgo(room, arrival, arrivals);
        }
        var previous = _currentRoom?.ResourceId;
        var fromPrevious = candidates.Where(item => item.SourceRoom is int source && source == previous).ToArray();
        // A first visit comes into the room from elsewhere: an arrival in the room's own code (a pseudo room's cells leading into each other) moves
        // within it, so it is a candidate only when the room has no other.
        var fromElsewhere = candidates.Where(item => item.SourceRoom != roomId).ToArray();
        var pool = fromPrevious.Length > 0 ? fromPrevious : fromElsewhere.Length > 0 ? fromElsewhere : candidates.ToArray();
        var choices = pool.GroupBy(item => item.PlacementKey).Select(group => group.ToArray()).ToArray();
        var index = 0;
        string how;
        if (choices.Length == 1) how = "the only source-backed arrival" + (fromPrevious.Length > 0 ? $" in Room {previous:00}'s own code" : "");
        else
        {
            var pick = _runtime.Random.Pick($"arrival into Room {roomId:00}", 0, choices.Length - 1);
            index = pick.Value;
            how = $"seeded pick {index + 1} of {choices.Length} (seed {pick.Seed})" + (fromPrevious.Length > 0 ? $" among those in Room {previous:00}'s own code" : "");
        }
        var chosen = choices[index];
        var first = chosen[0];
        var label = $"Assumed arrival: the ego enters as {string.Join(" or ", chosen.Select(item => item.Describe()))} would bring it in ({how}). " +
                    "Which route led here was not played; the arrival is a possible outcome, not a reached one.";
        var load = new ScummScriptEvent(0, first.Source, first.Offset, ScummScriptEventKind.RoomLoaded, "loadRoomWithEgo",
            $"assumed arrival into Room {roomId:00}", ResourceKind.Room, first.LoadedNumber, ScummScriptEventCertainty.Conditional,
            ResolvedArguments: [first.ObjectId, first.LoadedNumber, first.WalkX, first.WalkY]);
        // o5_loadRoomWithEgo puts the ego in the room with the position it had (putActor(room)): the entry code sees it where the previous room left it.
        _uncertainties.Add($"Before its assumed arrival the ego keeps the position it had in the room the session was in (estimate: the route that would have moved it was not played).");
        return EnterRoom(room, $"{arrival} {label}", load, label);
    }

    /// <summary>
    /// Plays a room's entry again (S7, 17 October 2026, to play a room's entry cutscene): a room the session is not in is visited as
    /// <see cref="VisitRoom"/> does; the room it is in is loaded again as a <c>loadRoom</c> of the current room would (ScummVM <c>startScene</c>: the
    /// exit code, then the entry hook and entry script from the state the session has). An exploration action and a state preview, never a reached outcome.
    /// </summary>
    public ScummSessionSnapshot EnterRoomAgain(GameResource room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (_currentRoom?.ResourceId != room.ResourceId) return VisitRoom(room);
        _uncertainties.StartStep();
        var label = $"Room {room.ResourceId:00} entered again to play its entry scripts (an exploration action, as a loadRoom of the current room would; state preview).";
        _uncertainties.Add(label);
        return EnterRoom(room, label);
    }

    // A visit with no arrival to follow. A room the game's pseudoRoom instructions map other numbers to is loaded under one of them, as every load of
    // it in the game's own code would be (its entry code tells the uses apart by VAR_ROOM, so under its own number it would draw none of them):
    // a seeded, labelled pick among those numbers, since no literal arrival says which comes first.
    private ScummSessionSnapshot EnterWithoutEgo(GameResource room, string arrival, ScummEgoArrivalIndex arrivals)
    {
        var roomId = room.ResourceId!.Value;
        var numbers = arrivals.PseudoNumbersOf(roomId).Where(number => _runtime.PseudoRooms.GetValueOrDefault(number) == roomId).ToArray();
        if (numbers.Length == 0) return EnterRoom(room, arrival);
        var index = numbers.Length == 1 ? 0 : _runtime.Random.Pick($"pseudo room of Room {roomId:00}", 0, numbers.Length - 1).Value;
        var number = numbers[index];
        var label = $"Assumed pseudo room: Room {roomId:00} is loaded as pseudo room {number} (VAR_ROOM), " +
                    (numbers.Length == 1 ? "the only number the game's pseudoRoom instructions give it" : $"a seeded pick {index + 1} of {numbers.Length} among {string.Join(", ", numbers)}") +
                    "; no decoded arrival says which comes first.";
        _uncertainties.Add(label);
        var load = new ScummScriptEvent(0, "explorer visit", 0, ScummScriptEventKind.RoomLoaded, "loadRoom",
            $"assumed load of pseudo room {number}", ResourceKind.Room, number, ScummScriptEventCertainty.Conditional, ResolvedArguments: [number]);
        return EnterRoom(room, $"{arrival} {label}", load);
    }

    /// <summary>
    /// Plays frames until the room has settled and says how it ended (S5): no cutscene, message or movement and no change in object states,
    /// positions, draw order or actors for <paramref name="quietFrames"/> frames; or the room was left (a script loaded another room); or the
    /// frame limit was reached (ambient scripts that never rest). The state of the room is the last one captured while the room was current.
    /// </summary>
    public ScummSettleResult Settle(int frameLimit = 2400, int quietFrames = 90)
    {
        var room = _currentRoom?.ResourceId ?? throw new InvalidOperationException("Visit a room before settling it.");
        var capture = Capture();
        // An object whose state or position changes four times within 240 frames is an ambient animator (a crowd, a flame): it never rests in a
        // living room, so it does not keep the room from settling; it is shown in the phase of its loop the room was in when the rest began.
        var changes = new Dictionary<int, Queue<int>>();
        var previous = ObjectValues();
        var signature = Signature(changes, 0);
        var quiet = 0;
        for (var frame = 1; frame <= frameLimit; frame++)
        {
            AdvanceFrame();
            if (_currentRoom?.ResourceId != room) return new(capture, ScummSettleOutcome.RoomLeft, frame, _currentRoom?.ResourceId);
            var now = ObjectValues();
            foreach (var (id, value) in now)
                if (!previous.TryGetValue(id, out var before) || before != value)
                {
                    if (!changes.TryGetValue(id, out var queue)) changes[id] = queue = new();
                    queue.Enqueue(frame);
                }
            foreach (var queue in changes.Values)
                while (queue.Count > 0 && queue.Peek() < frame - 240) queue.Dequeue();
            previous = now;
            var current = Signature(changes, frame);
            var busy = InCutscene || Messages.HaveMsg != 0 || EgoMoving || CameraMoving;
            capture = Capture();
            if (current != signature) { signature = current; quiet = 0; }
            else quiet = busy ? 0 : quiet + 1;
            if (quiet >= quietFrames) return new(capture, ScummSettleOutcome.Settled, frame, null);
        }
        return new(Capture(), ScummSettleOutcome.FrameLimit, frameLimit, null);
    }

    private Dictionary<int, int> ObjectValues()
    {
        var values = new Dictionary<int, int>(_runtime.ObjectStates.Count);
        var roomObjects = _runtime.IsV2 ? _currentObjects.Select(item => item.Id).ToHashSet() : null;
        foreach (var pair in _runtime.ObjectStates)
            if (roomObjects is null || roomObjects.Contains(pair.Key)) values[pair.Key] = pair.Value;
        foreach (var pair in _runtime.ObjectPositions)
            if (roomObjects is null || roomObjects.Contains(pair.Key)) values[pair.Key + 100000] = pair.Value.X * 1000 + pair.Value.Y;
        return values;
    }

    // ScummVM getActorFromPos (actor.cpp): for i from 1, an actor drawn on the point's 8-pixel strip (the strip usage bits), not untouchable,
    // with _top <= y <= _bottom of its drawing. The drawing is the session's own (the costume renderer).
    private int ActorFromPosition(int x, int y)
    {
        if (_currentRoom?.ResourceId is not int room || _roomSize is not { } size) return 0;
        var layer = ScummActorRenderer.Render(_runtime.Actors.Snapshot(), room, size.Width, size.Height, ReadCostume, classes: _runtime.ObjectClasses, engineVersion: (int)_runtime.EngineVersion);
        foreach (var actor in layer.Actors.OrderBy(item => item.Number))
        {
            if (actor.Bounds is not { } bounds || actor.Pixels == 0) continue;
            if (_runtime.ObjectClasses.TryGetValue(actor.Number, out var bits) && ScummObjectClasses.Has(bits, _runtime.UntouchableClass)) continue;
            if (x / 8 >= bounds.Left / 8 && x / 8 <= (bounds.Right - 1) / 8 && y >= bounds.Top && y <= bounds.Bottom) return actor.Number;
        }
        return 0;
    }

    /// <summary>Everything the picture of the current room needs, at this frame (the live play view composes it every frame).</summary>
    public ScummRoomCapture CaptureRoom() => Capture();

    private ScummRoomCapture Capture() => new(Snapshot, Actors, CameraX, _runtime.ShadowPalette.ToArray(), _runtime.RoomColors.ToArray(),
        _runtime.GlobalVariables.GetValueOrDefault(_runtime.CurrentLightsVariable), _runtime.Flashlight, CurrentWalkBoxes)
        { RoomPointer = ScreenPointer is { } pointer ? (pointer.X, pointer.Y - MainScreen.Top)
              : Pointer is { } roomPointer ? (roomPointer.X - ViewLeft, roomPointer.Y) : (0, 0),
          ViewLeft = ViewLeft, ViewWidth = 320, ViewHeight = MainScreen.Bottom - MainScreen.Top };

    private long Signature(Dictionary<int, Queue<int>> changes, int frame)
    {
        bool Ambient(int id) => changes.TryGetValue(id, out var queue) && queue.Count >= 4;
        var hash = new HashCode();
        // v1/v2 background scripts animate objects in rooms that are not loaded. Their flags still
        // belong to persistent state, but cannot keep the current room's picture from settling.
        var roomObjects = _runtime.IsV2 ? _currentObjects.Select(item => item.Id).ToHashSet() : null;
        foreach (var pair in _runtime.ObjectStates.OrderBy(item => item.Key))
            if ((roomObjects is null || roomObjects.Contains(pair.Key)) && !Ambient(pair.Key)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        foreach (var pair in _runtime.ObjectPositions.OrderBy(item => item.Key))
            if ((roomObjects is null || roomObjects.Contains(pair.Key)) && !Ambient(pair.Key + 100000)) { hash.Add(pair.Key); hash.Add(pair.Value.X); hash.Add(pair.Value.Y); }
        foreach (var actor in Actors) { hash.Add(actor.Number); hash.Add(actor.Room); hash.Add(actor.X); hash.Add(actor.Y); hash.Add(actor.Visible); hash.Add(actor.Look.Costume); }
        foreach (var colour in _runtime.ShadowPalette.Take(16)) hash.Add(colour);
        foreach (var colour in _runtime.RoomColors.Take(16)) hash.Add(colour);
        hash.Add(CameraX);
        return hash.ToHashCode();
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
        // A screen of choices (S4b Monkey2): a click in the middle of the chosen object's box, delivered to the game's input script like a player's.
        if (plan.ClickObject is int clicked && _currentObjects.FirstOrDefault(item => item.Id == clicked) is { } choice)
        {
            var (x, y) = _runtime.ObjectPositions.TryGetValue(clicked, out var moved) ? (moved.X, moved.Y) : (choice.X, choice.Y);
            var screen = (X: x + choice.Width / 2 - ViewLeft, Y: y + choice.Height / 2 + _runtime.MainScreen.Top);
            PointAtScreen(screen);
            var input = ClickScreen(screen.X, screen.Y, out var refused);
            _visitTrace.Add($"boot: Player input assumed: a click on object {clicked} in Room {_currentRoom?.ResourceId} at screen {screen} (the choice the explorer takes), " +
                            $"after the boot stood still {BootStallFrames} frames{(input is null ? $"; not delivered: {refused}" : "")}");
            return 1;
        }
        // A plan that answers with a click presses no key: a later stall (a long delay, music) is not a question.
        if (plan.ClickObject is not null) return 0;
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

/// <summary>How a settle ended: the room rested, a script left it, or the frame limit came first.</summary>
public enum ScummSettleOutcome { Settled, RoomLeft, FrameLimit }

/// <summary>Everything the picture of a room needs, taken at one frame while the room was current.</summary>
public sealed record ScummRoomCapture(ScummSessionSnapshot Snapshot, IReadOnlyList<ScummActorSnapshot> Actors, int? CameraX,
    byte[] ShadowPalette, byte[] RoomColors, int Lights, (int X, int Y) Flashlight, ScummWalkBoxSet? WalkBoxes)
{
    public (int X, int Y) RoomPointer { get; init; }
    public int ViewLeft { get; init; }
    public int ViewWidth { get; init; } = 320;
    public int ViewHeight { get; init; } = 200;
}

/// <summary>The settled state of a visited room, the frames it took and, when a script left the room, the room it went to.</summary>
public sealed record ScummSettleResult(ScummRoomCapture State, ScummSettleOutcome Outcome, int Frames, int? LeftTo);

/// <summary>The outcome of a generic boot: frames played, whether the frame limit stopped it, the room it ended in and the rooms it entered.</summary>
public sealed record ScummBootReport(int Frames, bool HitFrameLimit, int? FinalRoom, IReadOnlyList<int> RoomsEntered);

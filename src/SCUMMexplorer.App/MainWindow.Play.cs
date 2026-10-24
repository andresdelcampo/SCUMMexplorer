using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// Play in a verb-based game (REQUIREMENTS §7.1, Proposed R-7.7 to R-7.18; S6 Indy3Ega, 15–16 October 2026; S6 MonkeyFloppy, 16 October): the room view becomes the game's own screen,
/// composed every frame from a live <see cref="ScummGameSession"/> (<see cref="ScummPlayScreen"/>): the room, its objects and actors, the messages, the verb
/// area with the sentence line and inventory the game draws, and the game's cursor (the v3 crosshair, the v4 bitmap cross-hair) in place of the system pointer. Each press of the left button
/// is one click for the game's input script (<see cref="ScummSession.ClickScreen"/>); keys go as R-7.11 says. Space pauses, Esc skips a cutscene from
/// its override point, '.' ends the current line. The app chooses no verb, object or sentence.
/// </summary>
public partial class MainWindow
{
    private ScummGameSession? _play;
    private ScummPlayScreen? _playScreen;
    private IReadOnlyDictionary<int, byte> _playIndexedStates = new Dictionary<int, byte>();
    /// <summary>The <see cref="GameLibrary.Key"/> (folder, and language when the game holds several) of the game the play session runs.</summary>
    private string? _playFolder;
    private DispatcherTimer? _playTimer;
    private bool _playPaused;
    private int _playFrame;
    private ScummPlayFrame? _playShown;
    private bool _followingPlayRoom;
    private WriteableBitmap? _playBitmap;
    // The play game's sounds that play a track of its disc (null for a game without them).
    private ScummCdAudioTracks? _playCdTracks;
    // The play game's iMUSE songs (v5: Monkey Island 2, Fate of Atlantis), for where a looping song goes on (null for a game without them).
    private ScummImuseMusic? _playImuse;
    // S6 Indy4 (17 October 2026): the spoken lines of a talkie, played as the game's texts start and stop them.
    private readonly TalkieSpeechPlayer _playSpeech = new();

    /// <summary>The session's speech events before the last room visit from the list (a cutscene played from the list speaks its entry from there).</summary>
    private int _playSpeechBeforeVisit;

    private bool PlayActive => _playTimer is not null && _play is not null;

    /// <summary>
    /// R-8.5b (rule confirmed by the user 20 Oct): the play session is a static preview's, running only the room's ambient scripts
    /// (<see cref="ScummSession.PrepareAmbientRoom"/>); no click or key reaches the game. <see cref="_playLiveSource"/> is the session it was made from.
    /// </summary>
    private bool _playAmbient;
    private ScummGameSession? _playLiveSource;

    /// <summary>Play that delivers the user's clicks and keys to the game (not a static preview's ambient frames).</summary>
    private bool PlayTakesInput => PlayActive && !_playAmbient;

    /// <summary>Advanced: which of the room's entry starts are ambient and why, from <see cref="ScummAmbientScripts"/>.</summary>
    private static string AmbientNote(ScummManualRoomResult ambient) => ambient.Ambient is not { } found
        ? ambient.Reason ?? ""
        : (found.Ambient.Count == 0 ? "Static preview: the room's entry starts no ambient script, so nothing runs." :
              "Static preview: only the room's ambient scripts run (its entry code does not): " + string.Join(" ", found.Ambient.Select(a => a.Evidence + "."))) +
          (found.Rejected.Count == 0 ? "" : " Not ambient: " + string.Join("; ", found.Rejected.Select(r => $"script {r.ScriptId} ({r.Reason})")) + ".");

    /// <summary>A static preview gives way to the session it was made from before an explicit scene or intro (the stated start of R-8.13).</summary>
    private void LeaveAmbientPreview()
    {
        if (!_playAmbient || _game is not { } game || _playLiveSource is not { } live) return;
        _sceneSound.StopAll("an explicit scene leaves the static preview");
        _playAmbient = false;
        _catalogPreviewOnly = false;
        _play = live;
        _playScreen = new ScummPlayScreen(game, live, _playIndexedStates);
        _sceneSound.Attach(game, live, RenderPlaySound, PlaySoundLoop);
        if (game.Speech is { } speech) _playSpeech.Attach(speech, live.Messages);
        _playSpeechBeforeVisit = live.Messages.SpeechEvents.Count;
    }

    // The pause state the play sounds were last given (S6 sound).
    private bool _playSoundPaused;

    /// <summary>
    /// S6 sound (16 October 2026): a sound the play session's scripts started, rendered as the sound player renders it for the device the user picked
    /// (the same device setting as browsing; AdLib at its output level). Which ids play, and when, is the scripts' (<see cref="ScummSession.SoundEvents"/>).
    /// </summary>
    private (byte[] Wave, bool Music) RenderPlaySound(GameResource resource, SceneSoundPlayer.SoundMode mode, CancellationToken token)
    {
        var game = _game;
        var cdTracks = _playCdTracks;
        token.ThrowIfCancellationRequested();
        if (game is null) throw new InvalidOperationException("No game is open for this sound.");
        // S6 MonkeyCd (17 October 2026): a sound that plays a track of the game's disc (ScummVM playCDTrackFromSoundID) plays that stretch of the disc's audio,
        // the same samples the room's music panel plays for the track, whatever the device (a CD drive is not a sound card).
        if (cdTracks is { } cd && resource.ResourceId is int cdSound && cd.Plays(cdSound))
        {
            var cdWave = cd.ReadWave(cdSound);
            if (cdWave.Length == 0) throw new System.IO.InvalidDataException($"Sound {cdSound}'s CD track is not on the disc image.");
            return (cdWave, true);
        }
        var wave = RenderSoundWave(resource, mode, token, game);
        if (mode == SceneSoundPlayer.SoundMode.Adlib) wave = ScummAdlibAudio.ApplyOutputGain(wave, SceneSoundPlayer.AdlibGainDb);
        var music = game.V4ArchiveFor(resource) is { } archive
            ? archive.ReadSounds().FirstOrDefault(sound => sound.Id == resource.ResourceId)?.IsMusic == true
            : _adlibDecoder.HasMusic(resource);
        return (wave, music);
    }

    /// <summary>
    /// Where a sound the play session started goes on until a script stops it (S6 Monkey2, 17 October 2026): a CD track with loop count 0xFF from its beginning;
    /// an iMUSE song (v5) through the S3 player of the chosen device from the loop point of that device's part (<see cref="ScummImuseMusic.Loop"/>, an estimate:
    /// the markers, hooks and player commands that scripts send to a playing song are not followed). Null: the sound ends by itself.
    /// </summary>
    private SceneSoundPlayer.Loop? PlaySoundLoop(int soundId, SceneSoundPlayer.SoundMode mode)
    {
        if (_playCdTracks is { } cd && cd.Plays(soundId)) return cd.LoopsForever(soundId) ? new SceneSoundPlayer.Loop(TimeSpan.Zero, null) : null;
        // Sound polish (19 October 2026): a version 3 or 4 song that loops (Player_AD or iMUSE's jump back for AdLib, a repeated channel state for the
        // PC speaker) goes on from its loop target instead of ending with its rendered wave.
        if (_playImuse is null && _game is { } game && OldSoundLoop(game, soundId, mode) is { } oldLoop)
            return new SceneSoundPlayer.Loop(TimeSpan.FromSeconds(oldLoop.RestartSeconds), TimeSpan.FromSeconds(oldLoop.EndSeconds));
        if (_playImuse is not { } imuse || PlayImuseDevice(mode) is not { } device) return null;
        return imuse.Loop(soundId, device) is { } loop ? new SceneSoundPlayer.Loop(TimeSpan.FromSeconds(loop.RestartSeconds), TimeSpan.FromSeconds(loop.EndSeconds)) : null;
    }

    private ScummImuseLoop? OldSoundLoop(GameLibrary game, int soundId, SceneSoundPlayer.SoundMode mode)
    {
        if (game.Version is not (ScummVersion.V3 or ScummVersion.V4) || mode is not (SceneSoundPlayer.SoundMode.Adlib or SceneSoundPlayer.SoundMode.PcSpeaker)) return null;
        var adlib = mode == SceneSoundPlayer.SoundMode.Adlib;
        var resource = game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Sound && item.ResourceId == soundId);
        try
        {
            if (resource is not null && game.V4ArchiveFor(resource) is ScummV4Archive archive)
            {
                var sound = archive.ReadSounds().FirstOrDefault(item => item.Id == soundId);
                return adlib
                    ? sound?.Adlib is { } part ? _adlibDecoder.ReadLoop(part, ScummProfile.For(game).AdlibTiming) : null
                    : sound?.Speaker is { } speaker ? _audioDecoder.ReadLoop(speaker, 6) : null;
            }
            if (resource is null || game.Version != ScummVersion.V3 || ScummRolandSong.IsRoland(resource)) return null;
            return adlib ? _adlibDecoder.ReadLoop(resource, ScummProfile.For(game).AdlibTiming) : _audioDecoder.ReadLoop(ScummPcSpeakerAudio.ReadOldBundle(resource), 4);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private static ScummImuseDevice? PlayImuseDevice(SceneSoundPlayer.SoundMode mode) => mode switch
    {
        SceneSoundPlayer.SoundMode.Roland => ScummImuseDevice.Roland,
        SceneSoundPlayer.SoundMode.Adlib => ScummImuseDevice.Adlib,
        SceneSoundPlayer.SoundMode.PcSpeaker => ScummImuseDevice.Speaker,
        _ => null
    };

    /// <summary>Starts (or continues) play in the room the user opened: the session boots once per game; another room is visited from its state (state preview).</summary>
    private Task StartPlayAsync(GameResource resource, CancellationToken token) => StartPlayCoreAsync(resource, token, RoomOpenKind.Catalog);

    private async Task StartPlayCoreAsync(GameResource resource, CancellationToken token, RoomOpenKind navigationKind)
    {
        if (_game is not { } game || resource.ResourceId is not int roomId) return;
        var launch = _cutsceneLaunchGeneration;
        bool CurrentRequest() => !token.IsCancellationRequested && ReferenceEquals(_game, game) && launch == _cutsceneLaunchGeneration;
        try
        {
            if (_play is null || _playFolder != game.Key)
            {
                StopPlay();
                _playAmbient = false;
                _playLiveSource = null;
                ScummIndexedObjectSnapshot? objects = null;
                var session = await Task.Run(() => ScummGameSession.BootForPlay(game, out objects), token);
                if (!CurrentRequest()) return;
                _play = session;
                UpdatePlaySoundCard();
                _playIndexedStates = objects!.States;
                _playScreen = new ScummPlayScreen(game, session, _playIndexedStates);
                _playFolder = game.Key;
                _playCdTracks = ScummCdAudioTracks.For(game);
                _playImuse = ScummImuseMusic.For(game);
            }
            if (!CurrentRequest()) return;
            // A static preview's session runs only its room's ambient scripts; the session it was made from carries the game on.
            if (_playAmbient && _playLiveSource is { } live) _play = live;
            _playAmbient = false;
            if (navigationKind == RoomOpenKind.Catalog)
            {
                var source = _play;
                var prepared = await Task.Run(() => source.PrepareManualRoom(resource, cancellationToken: token), token);
                if (!CurrentRequest()) return;
                if (prepared.Session is not ScummGameSession ready || ready.PlayInputScript == 0)
                {
                    _catalogPreviewOnly = true;
                    var reason = prepared.Reason ?? "The native startup has not established the game's input script.";
                    StopPlay();
                    // R-8.5b (rule confirmed by the user 20 Oct): the static preview keeps the room's controls, and the room's ambient scripts (its
                    // permanent animation, decided from its code) run on a play clock; none of its other entry code runs, and no input reaches the game.
                    // Maniac Mansion and Zak keep their own startup and static-preview rules (R-8.34, 19 Oct): no ambient session there.
                    var ambient = UsesManiacCutsceneSetup(game) ? new ScummManualRoomResult(null, "Maniac Mansion and Zak keep their startup rules.")
                        : await Task.Run(() => source.PrepareAmbientRoom(resource, token), token);
                    if (!CurrentRequest()) return;
                    _playLiveSource = source;
                    StatusText.Text = "Room preview: its story does not play here. Play Cutscene plays this room's scenes.";
                    StatusText.ToolTip = IsAdvancedMode ? reason + Environment.NewLine + AmbientNote(ambient) : null;
                    if (ambient.Session is not ScummGameSession still)
                    {
                        UpdatePlaybackControls();
                        return;
                    }
                    _play = still;
                    _playAmbient = true;
                    _playScreen = new ScummPlayScreen(game, still, _playIndexedStates);
                    _sceneSound.StopAll("a static preview's ambient scripts start");
                }
                else
                {
                    _catalogPreviewOnly = false;
                    _play = ready;
                    _playScreen = new ScummPlayScreen(game, ready, _playIndexedStates);
                }
            }
            else _catalogPreviewOnly = false;
            UpdatePlaySoundCard();
            // S6 sound: from here on, the sounds the game's scripts start and stop play through the device the user picked (the boot's are not played).
            // A CD track that loops for ever (loop count 0xFF) starts again when it ends, until a script stops it (S6 MonkeyCd); an iMUSE song that jumps back
            // goes on from its loop point (S6 Monkey2).
            if (!_sceneSound.Follows(_play)) _sceneSound.Attach(game, _play, RenderPlaySound, PlaySoundLoop,
                replayReachedEvents: navigationKind == RoomOpenKind.Intro);
            if (game.Speech is { } speech && !_playSpeech.Follows(_play.Messages))
                _playSpeech.Attach(speech, _play.Messages, navigationKind == RoomOpenKind.Intro ? 0 : _play.Messages.SpeechEvents.Count);
            // A room the session is not in is entered as a state preview (the route to it is not played), as the room view's first visit is.
            // Explicit intro playback keeps the native opening and player choices.
            // Catalog navigation has already prepared a quiet preview on a detached session.
            _playSpeechBeforeVisit = _play.Messages.SpeechEvents.Count;
            if (navigationKind != RoomOpenKind.Catalog && _play.CurrentRoomId != roomId)
            {
                // The sounds of the room the user leaves through the list stop; the new room's entry starts its own.
                _sceneSound.StopAll("another room was picked from the list during play");
                _play.VisitRoom(resource);
                // The visit's entry scripts are a state preview: their lines are not spoken.
                if (game.Speech is { } visitSpeech) _playSpeech.Attach(visitSpeech, _play.Messages);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            if (!CurrentRequest()) return;
            StopPlay();
            if (IsAdvancedMode) StatusText.ToolTip = $"Play could not start: {ex.Message}";
            return;
        }
        if (!CurrentRequest()) return;
        StartPlayClock();
    }

    private void StartPlayClock()
    {
        if (_play is null) return;
        _playPaused = false;
        _playTimer ??= new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _playTimer.Tick -= PlayTimer_Tick;
        _playTimer.Tick += PlayTimer_Tick;
        _playTimer.Interval = TimeSpan.FromMilliseconds(_play.FrameJiffies * 1000.0 / 60);
        _playTimer.Start();
        RoomDisplayGroup.Visibility = Visibility.Collapsed;
        PlayScreenImage.Visibility = Visibility.Visible;
        ObjectsNote.Text = "";
        ShowPlayFrame();
        UpdateGameStatusButton();
        PlayScreenImage.Focus();
        UpdatePlaybackControls();
    }

    /// <summary>Stops the frames and gives the room view back (another game, another kind of resource).</summary>
    private void StopPlay()
    {
        _playTimer?.Stop();
        _playTimer = null;
        if (_play is not null && _sceneSound.Follows(_play)) _sceneSound.StopAll("play was left");
        _playSpeech.Detach();
        PlayScreenImage.Visibility = Visibility.Collapsed;
        ClearPlayOverlays();
        PlayScreenImage.Cursor = null;
        RoomDisplayGroup.Visibility = Visibility.Visible;
        UpdateGameStatusButton();
        if (PlayCutscenes) UpdatePlayCutsceneControls();
    }

    /// <summary>Step ▸ (Advanced, as in LOOM): while paused, plays exactly one frame of the game's clock.</summary>
    private void StepPlayFrame()
    {
        if (!PlayActive || !_playPaused || _play is null || _sceneSound.IsPreparing) return;
        _play.AdvanceFrame();
        _sceneSound.Update();
        _playSpeech.Update();
        _playFrame++;
        ShowPlayFrame();
        RefreshPlayControlsOnFrame();
        StatusText.Text = $"Paused after one frame (Room {_play.CurrentRoomId:00}). Play goes on.";
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (_play is null) return;
        // R-7.16 Pause stops the game's sounds with its clock and lets them go on with it (the app's Pause key only flips _playPaused).
        if (_playPaused != _playSoundPaused)
        {
            _playSoundPaused = _playPaused;
            if (_playPaused) { _sceneSound.Pause(); _playSpeech.Pause(); } else { _sceneSound.Resume(); _playSpeech.Resume(); }
        }
        // As in LOOM, a sound the game just started holds the frames while it is rendered, so music and scene stay in step.
        if (_playPaused || _sceneSound.IsPreparing) return;
        _play.AdvanceFrame();
        _sceneSound.Update();
        _playSpeech.Update();
        _playFrame++;
        if (_playTimer is { } timer) timer.Interval = TimeSpan.FromMilliseconds(_play.FrameJiffies * 1000.0 / 60);
        ShowPlayFrame();
        RefreshPlayControlsOnFrame();
    }

    private void ShowPlayFrame()
    {
        if (_playScreen is null || _play is null) return;
        var frame = _playScreen.Compose(_playFrame);
        _playShown = frame;
        if (frame.RoomId is int shownRoom && _currentRoomResource?.ResourceId != shownRoom &&
            _game?.Rooms.FirstOrDefault(r => r.ResourceId == shownRoom) is { } nativeRoom)
        {
            // Follow a native load without re-entering it through catalog preparation.
            _currentRoomResource = nativeRoom;
            _lastViewedRoomResource = nativeRoom;
            _selectedResource = nativeRoom;
            _followingPlayRoom = true;
            try { SelectResourceInTree(nativeRoom); }
            finally { _followingPlayRoom = false; }
            RoomCardList.Children.OfType<System.Windows.Controls.Button>()
                .FirstOrDefault(card => Equals(card.Tag, nativeRoom))?.BringIntoView();
            RefreshPlayRoomActions();
        }
        _playBitmap ??= new WriteableBitmap(ScummPlayFrame.Width, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null);
        _playBitmap.WritePixels(new Int32Rect(0, 0, ScummPlayFrame.Width, ScummPlayFrame.Height), frame.Pixels, ScummPlayFrame.Width * 4, 0);
        PlayScreenImage.Source = _playBitmap;
        // R-7.9: the game's cursor replaces the system pointer while the game shows it, and nothing shows while it is off.
        PlayScreenImage.Cursor = Cursors.None;
        if (IsAdvancedMode)
            PlayScreenImage.ToolTip = $"Room {frame.RoomId:00} · input script {_play.PlayInputScript} · " +
                                      (_play.DeliveredInput.LastOrDefault() is { } last ? $"last input runInputScript({last.Area}, {last.Value}, {last.Mode}): {last.Description}" : "no input yet") +
                                      (_playPaused ? " · paused" : "");
        else PlayScreenImage.ToolTip = null;
        TryUpdateGenericRoomSounds();
        RefreshPlayOverlays(frame);
        RefreshPlayOverlayLists();
    }

    // The image's point as a game screen point (0..319, 0..199).
    private (int X, int Y)? PlayPoint(MouseEventArgs e)
    {
        return PlayPointAt(e.GetPosition(PlayScreenImage), PlayScreenImage.ActualWidth, PlayScreenImage.ActualHeight);
    }

    // Match Image.Stretch=Uniform, including the unused bands around the native screen.
    private static (int X, int Y)? PlayPointAt(Point position, double width, double height)
    {
        if (width <= 0 || height <= 0) return null;
        var scale = Math.Min(width / ScummPlayFrame.Width, height / ScummPlayFrame.Height);
        var offsetX = (width - ScummPlayFrame.Width * scale) / 2;
        var offsetY = (height - ScummPlayFrame.Height * scale) / 2;
        var nativeX = (position.X - offsetX) / scale;
        var nativeY = (position.Y - offsetY) / scale;
        if (nativeX < 0 || nativeY < 0) return null;
        var x = (int)nativeX;
        var y = (int)nativeY;
        return x is >= 0 and < ScummPlayFrame.Width && y is >= 0 and < ScummPlayFrame.Height ? (x, y) : null;
    }

    private void PlayScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!PlayActive) return;
        if (_playAmbient) { UpdatePlayOverlayHover(PlayPoint(e)); return; }
        _play!.PointAtScreen(PlayPoint(e));
        UpdatePlayOverlayHover(PlayPoint(e));
        if (_playPaused) ShowPlayFrame();
    }

    private void PlayScreenImage_MouseLeave(object sender, MouseEventArgs e)
    {
        if (PlayActive) _play!.PointAtScreen(null);
        UpdatePlayOverlayHover(null);
    }

    // R-7.10: one press, one click, delivered at the next frame.
    private void PlayScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayTakesInput || PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        SelectGenericInteractAt(point);
        // R-1.5 / R-6.25.2: where the game's own input leaves a double click free (ScummProfile.DoubleClickLookVerb), the second press of a double click
        // looks at the object under it through the game's own sentence path; the first press was the normal click. Elsewhere it is just a second click.
        if (e.ClickCount == 2 && PlayObjectAt(point) && _play!.DoubleClickLook(point.X, point.Y, out _) is not null)
        {
            UpdatePlaybackControls();
            return;
        }
        if (_play!.ClickScreen(point.X, point.Y, out var refused) is null && IsAdvancedMode) StatusText.ToolTip = $"Click not delivered: {refused}";
        UpdatePlaybackControls();
    }

    // R-7.16 (Rule, confirmed by the user 16 Oct): the right button goes to the game only where its scripts read it (ScummProfile.RightClickReachesGame:
    // version 4 and later, mode 2; Monkey Island's two-button default verb). Elsewhere it is not taken here and stays the app's.
    private void PlayScreenImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayRightClickReachesGame)
        {
            // Where the right button stays the app's (R-7.16), it opens LOOM's Interact menu on the object under it.
            if (PlayTakesInput && TryOpenGenericInteractAt(PlayPoint(e))) e.Handled = true;
            return;
        }
        if (PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        PlayRightClick(point.X, point.Y);
    }

    private bool PlayRightClickReachesGame => PlayTakesInput && _game is { } game && ScummProfile.For(game).RightClickReachesGame;

    private void UpdatePlaySoundCard()
    {
        // vars.cpp's device numbers (also ScummGameSession.AdlibCard): v1/v2
        // have no VAR_SOUNDCARD at var[0x030]; that word is their game's state.
        if (_play is null || _game is not { } game || ScummProfile.For(game).EngineVersion < ScummVersion.V3) return;
        // Sound polish (19 October 2026): with the PC speaker, the scripts' isSoundRunning follows that driver's lengths (ScummVM Player_V2).
        _play.PcSpeakerTiming = _sceneSound.Mode == SceneSoundPlayer.SoundMode.PcSpeaker;
        _play.SoundCard = _sceneSound.Mode switch
        {
            SceneSoundPlayer.SoundMode.Roland => 4,
            SceneSoundPlayer.SoundMode.Adlib => 3,
            SceneSoundPlayer.SoundMode.Pcjr => 1,
            _ => 0
        };
    }

    // One right press at a game screen point, as the image's handler delivers it after mapping the pointer.
    private ScummPlayInput? PlayRightClick(int x, int y)
    {
        if (!PlayRightClickReachesGame) return null;
        var input = _play!.ClickScreen(x, y, true, out var refused);
        if (input is null && IsAdvancedMode) StatusText.ToolTip = $"Right click not delivered: {refused}";
        UpdatePlaybackControls();
        return input;
    }

    /// <summary>The engine's keys and the game's keys while playing; true when the key was taken.</summary>
    private bool PlayKeyDown(KeyEventArgs e)
    {
        if (!PlayActive || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F11) return false;
        if (key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)
        {
            // The game's pause key (VAR_PAUSE_KEY, Space in Indiana Jones) is the app's Pause.
            if (CurrentRoomControls.ReadTransport().PauseEnabled) _playPaused = !_playPaused; // R-6.32: the key acts under the button's condition
            ShowPlayFrame();
            UpdatePlaybackControls();
            e.Handled = true;
            return true;
        }
        if (_playAmbient) return false; // A static preview takes no keys but its Pause.
        if (key == Key.Escape && !_fullscreen)
        {
            // R-7.16: Esc skips a cutscene only from its override point (abortCutscene); otherwise the game reads it as its key.
            SkipPlayCutscene();
            e.Handled = true;
            return true;
        }
        int? code = key switch
        {
            >= Key.F1 and <= Key.F9 => 315 + (key - Key.F1),
            Key.Enter => 13,
            Key.Back => 8,
            >= Key.A and <= Key.Z when Keyboard.Modifiers == ModifierKeys.Control => 1 + (key - Key.A),
            _ => null
        };
        if (code is not int value) return false;
        _play!.PressPlayKey(value, out _);
        UpdatePlaybackControls();
        e.Handled = true;
        return true;
    }

    // Printable keys as the characters they type (verb shortcuts are lower-case letters; Script 24 reads '<' and '>').
    private void PlayTextInput(TextCompositionEventArgs e)
    {
        if (!PlayTakesInput || string.IsNullOrEmpty(e.Text) || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
        var character = e.Text[0];
        if (character is < ' ' or > '~' || character == ' ') return;
        e.Handled = true;
        if (character == '.') { _play!.SkipMessage(); UpdatePlaybackControls(); return; }
        _play!.PressPlayKey(character, out _);
        UpdatePlaybackControls();
    }
}

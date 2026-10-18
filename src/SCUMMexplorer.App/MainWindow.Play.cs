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
    private string? _playFolder;
    private DispatcherTimer? _playTimer;
    private bool _playPaused;
    private int _playFrame;
    private ScummPlayFrame? _playShown;
    private WriteableBitmap? _playBitmap;
    // The play game's sounds that play a track of its disc (null for a game without them).
    private ScummCdAudioTracks? _playCdTracks;
    // The play game's iMUSE songs (v5: Monkey Island 2, Fate of Atlantis), for where a looping song goes on (null for a game without them).
    private ScummImuseMusic? _playImuse;

    private bool PlayActive => _playTimer is not null && _play is not null;

    // The pause state the play sounds were last given (S6 sound).
    private bool _playSoundPaused;

    /// <summary>
    /// S6 sound (16 October 2026): a sound the play session's scripts started, rendered as the sound player renders it for the device the user picked
    /// (the same device setting as browsing; AdLib at its output level). Which ids play, and when, is the scripts' (<see cref="ScummSession.SoundEvents"/>).
    /// </summary>
    private (byte[] Wave, bool Music) RenderPlaySound(GameResource resource, SceneSoundPlayer.SoundMode mode, CancellationToken token)
    {
        // S6 MonkeyCd (17 October 2026): a sound that plays a track of the game's disc (ScummVM playCDTrackFromSoundID) plays that stretch of the disc's audio,
        // the same samples the room's music panel plays for the track, whatever the device (a CD drive is not a sound card).
        if (_playCdTracks is { } cd && resource.ResourceId is int cdSound && cd.Plays(cdSound))
        {
            var cdWave = cd.ReadWave(cdSound);
            if (cdWave.Length == 0) throw new System.IO.InvalidDataException($"Sound {cdSound}'s CD track is not on the disc image.");
            return (cdWave, true);
        }
        var wave = RenderSoundWave(resource, mode, token);
        if (mode == SceneSoundPlayer.SoundMode.Adlib) wave = ScummAdlibAudio.ApplyOutputGain(wave, SceneSoundPlayer.AdlibGainDb);
        var music = _game?.V4ArchiveFor(resource) is { } archive
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
        if (_playImuse is not { } imuse || PlayImuseDevice(mode) is not { } device) return null;
        return imuse.Loop(soundId, device) is { } loop ? new SceneSoundPlayer.Loop(TimeSpan.FromSeconds(loop.RestartSeconds), TimeSpan.FromSeconds(loop.EndSeconds)) : null;
    }

    private static ScummImuseDevice? PlayImuseDevice(SceneSoundPlayer.SoundMode mode) => mode switch
    {
        SceneSoundPlayer.SoundMode.Roland => ScummImuseDevice.Roland,
        SceneSoundPlayer.SoundMode.Adlib => ScummImuseDevice.Adlib,
        SceneSoundPlayer.SoundMode.PcSpeaker => ScummImuseDevice.Speaker,
        _ => null
    };

    /// <summary>Starts (or continues) play in the room the user opened: the session boots once per game; another room is visited from its state (state preview).</summary>
    private async Task StartPlayAsync(GameResource resource, CancellationToken token)
    {
        if (_game is not { } game || resource.ResourceId is not int roomId) return;
        var startingSession = _play is null || _playFolder != game.FolderPath;
        try
        {
            if (_play is null || _playFolder != game.FolderPath)
            {
                StopPlay();
                ScummIndexedObjectSnapshot? objects = null;
                var session = await Task.Run(() => ScummGameSession.BootForPlay(game, out objects), token);
                if (token.IsCancellationRequested) return;
                _play = session;
                _playScreen = new ScummPlayScreen(game, session, objects!.States);
                _playFolder = game.FolderPath;
                _playCdTracks = ScummCdAudioTracks.For(game);
                _playImuse = ScummImuseMusic.For(game);
            }
            if (token.IsCancellationRequested) return;
            // S6 sound: from here on, the sounds the game's scripts start and stop play through the device the user picked (the boot's are not played).
            // A CD track that loops for ever (loop count 0xFF) starts again when it ends, until a script stops it (S6 MonkeyCd); an iMUSE song that jumps back
            // goes on from its loop point (S6 Monkey2).
            if (!_sceneSound.Follows(_play)) _sceneSound.Attach(game, _play, RenderPlaySound, PlaySoundLoop);
            // A room the session is not in is entered as a state preview (the route to it is not played), as the room view's first visit is.
            // The old engine's opening can wait on the player's party choice.
            // Its first Play screen must retain that live input, not visit the
            // browsed room or reuse the first-visit preview's assumed choices.
            var keepOpening = startingSession && ScummProfile.For(game).EngineVersion <= ScummVersion.V2;
            if (!keepOpening && _play.CurrentRoomId != roomId)
            {
                // The sounds of the room the user leaves through the list stop; the new room's entry starts its own.
                _sceneSound.StopAll("another room was picked from the list during play");
                _play.VisitRoom(resource);
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            StopPlay();
            if (IsAdvancedMode) StatusText.ToolTip = $"Play could not start: {ex.Message}";
            return;
        }
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
        PlayScreenImage.Visibility = Visibility.Collapsed;
        PlayScreenImage.Cursor = null;
        RoomDisplayGroup.Visibility = Visibility.Visible;
        UpdateGameStatusButton();
        if (PlayCutscenes) UpdatePlayCutsceneControls();
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (_play is null) return;
        // R-7.16 Pause stops the game's sounds with its clock and lets them go on with it (the app's Pause key only flips _playPaused).
        if (_playPaused != _playSoundPaused)
        {
            _playSoundPaused = _playPaused;
            if (_playPaused) _sceneSound.Pause(); else _sceneSound.Resume();
        }
        // As in LOOM, a sound the game just started holds the frames while it is rendered, so music and scene stay in step.
        if (_playPaused || _sceneSound.IsPreparing) return;
        _play.AdvanceFrame();
        _sceneSound.Update();
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
    }

    // The image's point as a game screen point (0..319, 0..199).
    private (int X, int Y)? PlayPoint(MouseEventArgs e)
    {
        if (PlayScreenImage.ActualWidth <= 0 || PlayScreenImage.ActualHeight <= 0) return null;
        var position = e.GetPosition(PlayScreenImage);
        var x = (int)(position.X / PlayScreenImage.ActualWidth * ScummPlayFrame.Width);
        var y = (int)(position.Y / PlayScreenImage.ActualHeight * ScummPlayFrame.Height);
        return x is >= 0 and < ScummPlayFrame.Width && y is >= 0 and < ScummPlayFrame.Height ? (x, y) : null;
    }

    private void PlayScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!PlayActive) return;
        _play!.PointAtScreen(PlayPoint(e));
        if (_playPaused) ShowPlayFrame();
    }

    private void PlayScreenImage_MouseLeave(object sender, MouseEventArgs e)
    {
        if (PlayActive) _play!.PointAtScreen(null);
    }

    // R-7.10: one press, one click, delivered at the next frame.
    private void PlayScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayActive || PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        if (_play!.ClickScreen(point.X, point.Y, out var refused) is null && IsAdvancedMode) StatusText.ToolTip = $"Click not delivered: {refused}";
    }

    // R-7.16 (Rule, confirmed by the user 16 Oct): the right button goes to the game only where its scripts read it (ScummProfile.RightClickReachesGame:
    // version 4 and later, mode 2; Monkey Island's two-button default verb). Elsewhere it is not taken here and stays the app's.
    private void PlayScreenImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayRightClickReachesGame || PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        PlayRightClick(point.X, point.Y);
    }

    private bool PlayRightClickReachesGame => PlayActive && _game is { } game && ScummProfile.For(game).RightClickReachesGame;

    // One right press at a game screen point, as the image's handler delivers it after mapping the pointer.
    private ScummPlayInput? PlayRightClick(int x, int y)
    {
        if (!PlayRightClickReachesGame) return null;
        var input = _play!.ClickScreen(x, y, true, out var refused);
        if (input is null && IsAdvancedMode) StatusText.ToolTip = $"Right click not delivered: {refused}";
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
            _playPaused = !_playPaused;
            ShowPlayFrame();
            e.Handled = true;
            return true;
        }
        if (key == Key.Escape && !_fullscreen)
        {
            // R-7.16: Esc skips a cutscene only from its override point (abortCutscene); otherwise the game reads it as its key.
            if (_play!.CutsceneSkippable) _play.PressKey(0x1B);
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
        e.Handled = true;
        return true;
    }

    // Printable keys as the characters they type (verb shortcuts are lower-case letters; Script 24 reads '<' and '>').
    private void PlayTextInput(TextCompositionEventArgs e)
    {
        if (!PlayActive || string.IsNullOrEmpty(e.Text) || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
        var character = e.Text[0];
        if (character is < ' ' or > '~' || character == ' ') return;
        e.Handled = true;
        if (character == '.') { _play!.SkipMessage(); return; }
        _play!.PressPlayKey(character, out _);
    }
}

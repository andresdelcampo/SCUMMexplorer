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
    // S6 Indy4 (17 October 2026): the spoken lines of a talkie, played as the game's texts start and stop them.
    private readonly TalkieSpeechPlayer _playSpeech = new();


    private bool PlayActive => _ctx.PlayTimer is not null && _ctx.Play is not null;


    /// <summary>Play that delivers the user's clicks and keys to the game (not a static preview's ambient frames).</summary>
    private bool PlayTakesInput => PlayActive && !_ctx.PlayAmbient;

    /// <summary>Advanced: which of the room's entry starts are ambient and why, from <see cref="ScummAmbientScripts"/>.</summary>
    private static string AmbientNote(ScummManualRoomResult ambient) => ambient.Ambient is not { } found
        ? ambient.Reason ?? ""
        : (found.Ambient.Count == 0 ? "Static preview: the room's entry starts no ambient script, so nothing runs." :
              "Static preview: only the room's ambient scripts run (its entry code does not): " + string.Join(" ", found.Ambient.Select(a => a.Evidence + "."))) +
          (found.Rejected.Count == 0 ? "" : " Not ambient: " + string.Join("; ", found.Rejected.Select(r => $"script {r.ScriptId} ({r.Reason})")) + ".");

    /// <summary>A static preview gives way to the session it was made from before an explicit scene or intro (the stated start of R-8.13).</summary>
    private void LeaveAmbientPreview()
    {
        if (!_ctx.PlayAmbient || _ctx.Game is not { } game || _ctx.PlayLiveSource is not { } live) return;
        _sceneSound.StopAll("an explicit scene leaves the static preview");
        _ctx.PlayAmbient = false;
        _ctx.CatalogPreviewOnly = false;
        _ctx.Play = live;
        _ctx.PlayScreen = new ScummPlayScreen(game, live, _ctx.PlayIndexedStates);
        _sceneSound.Attach(game, live, RenderPlaySound, PlaySoundLoop);
        if (game.Speech is { } speech) _playSpeech.Attach(speech, live.Messages);
        _ctx.PlaySpeechBeforeVisit = live.Messages.SpeechEvents.Count;
    }


    /// <summary>
    /// S6 sound (16 October 2026): a sound the play session's scripts started, rendered as the sound player renders it for the device the user picked
    /// (the same device setting as browsing; AdLib at its output level). Which ids play, and when, is the scripts' (<see cref="ScummSession.SoundEvents"/>).
    /// </summary>
    private (byte[] Wave, bool Music) RenderPlaySound(GameResource resource, SceneSoundPlayer.SoundMode mode, CancellationToken token)
    {
        var game = _ctx.Game;
        var cdTracks = _ctx.PlayCdTracks;
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
        if (_ctx.PlayCdTracks is { } cd && cd.Plays(soundId)) return cd.LoopsForever(soundId) ? new SceneSoundPlayer.Loop(TimeSpan.Zero, null) : null;
        // Sound polish (19 October 2026): a version 3 or 4 song that loops (Player_AD or iMUSE's jump back for AdLib, a repeated channel state for the
        // PC speaker) goes on from its loop target instead of ending with its rendered wave.
        if (_ctx.PlayImuse is null && _ctx.Game is { } game && OldSoundLoop(game, soundId, mode) is { } oldLoop)
            return new SceneSoundPlayer.Loop(TimeSpan.FromSeconds(oldLoop.RestartSeconds), TimeSpan.FromSeconds(oldLoop.EndSeconds));
        if (_ctx.PlayImuse is not { } imuse || PlayImuseDevice(mode) is not { } device) return null;
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
        if (_ctx.Game is not { } game || resource.ResourceId is not int roomId) return;
        var launch = _cutsceneLaunchGeneration;
        bool CurrentRequest() => !token.IsCancellationRequested && ReferenceEquals(_ctx.Game, game) && launch == _cutsceneLaunchGeneration;
        try
        {
            if (_ctx.Play is null || _ctx.PlayFolder != game.Key)
            {
                StopPlay();
                _ctx.PlayAmbient = false;
                _ctx.PlayLiveSource = null;
                ScummIndexedObjectSnapshot? objects = null;
                var session = await Task.Run(() => ScummGameSession.BootForPlay(game, out objects), token);
                if (!CurrentRequest()) return;
                _ctx.Play = session;
                _ctx.PlayBoot = UsesManiacCutsceneSetup(game) ? null : await Task.Run(() => ScummBootSnapshot.Of(session), token);
                if (!CurrentRequest()) return;
                UpdatePlaySoundCard();
                _ctx.PlayIndexedStates = objects!.States;
                _ctx.PlayScreen = new ScummPlayScreen(game, session, _ctx.PlayIndexedStates);
                _ctx.PlayFolder = game.Key;
                _ctx.PlayCdTracks = ScummCdAudioTracks.For(game);
                _ctx.PlayImuse = ScummImuseMusic.For(game);
            }
            if (!CurrentRequest()) return;
            // 7 November 2026 (user report: the castle plans after another room, "Indy won't move"): the session a pick is prepared from is copied on another
            // thread, so its clock and input stop until the new room's session takes over (StartPlayClock); a frame stepped during the copy left a session
            // that took clicks and never ran them. The waiting picture stays on the screen meanwhile.
            if (navigationKind == RoomOpenKind.Catalog) { _ctx.PlayTimer?.Stop(); _ctx.PlayTimer = null; }
            // A static preview's session runs only its room's ambient scripts; the session it was made from carries the game on.
            if (_ctx.PlayAmbient && _ctx.PlayLiveSource is { } live) _ctx.Play = live;
            _ctx.PlayAmbient = false;
            if (navigationKind == RoomOpenKind.Catalog)
            {
                var source = _ctx.Play;
                // R-6.52 (user, 25 Oct 2026): a Maniac Mansion room picked before the kids are chosen uses the reviewed, labelled assumed party (Syd, Bernard)
                // so the room is live; kids already chosen are kept. The assumption is listed under Assumptions in Advanced.
                if (UsesManiacCutsceneSetup(game) && roomId != 45)
                {
                    try { if (await Task.Run(() => SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.PrepareIfNeeded(game, source), token)) _ctx.PlayPartyAssumed = true; }
                    catch (InvalidOperationException) { /* the player has begun choosing: keep the choices as they are */ }
                    catch (System.IO.InvalidDataException) { /* no reviewed recipe for this menu: the room stays a static preview */ }
                }
                if (!CurrentRequest()) return;
                RoomHint = "Getting the room ready to play…";
                var prepared = await Task.Run(() => source.PrepareManualRoom(resource, cancellationToken: token), token);
                RoomHint = null;
                if (!CurrentRequest()) return;
                if (prepared.Session is not ScummGameSession ready || ready.PlayInputScript == 0)
                {
                    _ctx.CatalogPreviewOnly = true;
                    var reason = prepared.Reason ?? "The native startup has not established the game's input script.";
                    // The waiting picture stays on the screen while the still preview is made (no flash of the room view).
                    StopPlay(keepScreen: PlayScreenImage.Visibility == Visibility.Visible && _ctx.BrowsedScene is not null);
                    // R-8.5b (rule confirmed by the user 20 Oct): the static preview keeps the room's controls, and the room's ambient scripts (its
                    // permanent animation, decided from its code) run on a play clock; none of its other entry code runs, and no input reaches the game.
                    // Maniac Mansion and Zak keep their own startup and static-preview rules (R-8.34, 19 Oct): no ambient session there.
                    var ambient = UsesManiacCutsceneSetup(game) ? new ScummManualRoomResult(null, "Maniac Mansion and Zak keep their startup rules.")
                        : await Task.Run(() => source.PrepareAmbientRoom(resource, token, requireAmbient: false, picture: _ctx.BrowsedScene is { } picture && picture.RoomId == roomId ? picture.State : null), token);
                    if (!CurrentRequest()) return;
                    _ctx.PlayLiveSource = source;
                    StatusText.Text = "Room preview: its story does not play here. Play Cutscene plays this room's scenes.";
                    RoomHint = "A still picture: this room's own scene does not settle into a state you can play here. Play Cutscene shows its scenes; Play Game Intro starts the game.";
                    StatusText.ToolTip = IsAdvancedMode ? reason + Environment.NewLine + AmbientNote(ambient) : null;
                    if (ambient.Session is not ScummGameSession still)
                    {
                        UpdatePlaybackControls();
                        // The still picture stays in the play frame's layout, as it waited (no size change).
                        if (_ctx.BrowsedScene is { } waiting && ScummProfile.For(game).PlaysThroughVerbs) ShowWaitingPlayFrame(waiting);
                        return;
                    }
                    // The screen rows the room's first visit reached, as its waiting picture showed them (Indy 3 Room 80: no jump when the frames start).
                    if (_ctx.BrowsedScene?.MainScreen is { } firstVisitScreen) still.UseScreen(firstVisitScreen);
                    _ctx.Play = still;
                    _ctx.PlayAmbient = true;
                    _ctx.PlayScreen = new ScummPlayScreen(game, still, _ctx.PlayIndexedStates);
                    _sceneSound.StopAll("a static preview's ambient scripts start");
                }
                else
                {
                    _ctx.CatalogPreviewOnly = false;
                    _ctx.Play = ready;
                    _ctx.PlayScreen = new ScummPlayScreen(game, ready, _ctx.PlayIndexedStates);
                }
                // A room the game shows through one script of its own (a page, a manual: Indy 3's Room 74 and Script 90) is shown as that script shows it.
                if (_ctx.Play.ShownOnlyBy(roomId) is { } viewer) _ctx.Play.StartSceneAt(viewer, 0);
            }
            else _ctx.CatalogPreviewOnly = false;
            UpdatePlaySoundCard();
            // S6 sound: from here on, the sounds the game's scripts start and stop play through the device the user picked (the boot's are not played).
            // A CD track that loops for ever (loop count 0xFF) starts again when it ends, until a script stops it (S6 MonkeyCd); an iMUSE song that jumps back
            // goes on from its loop point (S6 Monkey2).
            if (!_sceneSound.Follows(_ctx.Play)) _sceneSound.Attach(game, _ctx.Play, RenderPlaySound, PlaySoundLoop,
                replayReachedEvents: navigationKind == RoomOpenKind.Intro);
            if (game.Speech is { } speech && !_playSpeech.Follows(_ctx.Play.Messages))
                _playSpeech.Attach(speech, _ctx.Play.Messages, navigationKind == RoomOpenKind.Intro ? 0 : _ctx.Play.Messages.SpeechEvents.Count);
            // A room the session is not in is entered as a state preview (the route to it is not played), as the room view's first visit is.
            // Explicit intro playback keeps the native opening and player choices.
            // Catalog navigation has already prepared a quiet preview on a detached session.
            _ctx.PlaySpeechBeforeVisit = _ctx.Play.Messages.SpeechEvents.Count;
            if (navigationKind != RoomOpenKind.Catalog && _ctx.Play.CurrentRoomId != roomId)
            {
                // The sounds of the room the user leaves through the list stop; the new room's entry starts its own.
                _sceneSound.StopAll("another room was picked from the list during play");
                if (navigationKind == RoomOpenKind.Cutscene && _ctx.SceneOpening is { } scene)
                {
                    _ctx.Play.ArrivalsSkippedFor = scene.IsRoomEntry ? null : scene;
                    _ctx.Play.WantHeroForScene(scene);
                }
                try { _ctx.Play.VisitRoom(resource); }
                finally { _ctx.Play.ArrivalsSkippedFor = null; }
                // The visit's entry scripts are a state preview: their lines are not spoken.
                if (game.Speech is { } visitSpeech) _playSpeech.Attach(visitSpeech, _ctx.Play.Messages);
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
        if (_ctx.Play is null) return;
        _ctx.PlayPaused = false;
        _ctx.PlayTimer ??= new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _ctx.PlayTimer.Tick -= PlayTimer_Tick;
        _ctx.PlayTimer.Tick += PlayTimer_Tick;
        _ctx.PlayTimer.Interval = TimeSpan.FromMilliseconds(_ctx.Play.FrameJiffies * 1000.0 / 60);
        _ctx.PlayTimer.Start();
        RoomDisplayGroup.Visibility = Visibility.Collapsed;
        PlayScreenImage.Visibility = Visibility.Visible;
        RoomHint = null;
        ObjectsNote.Text = "";
        ShowPlayFrame();
        UpdateGameStatusButton();
        PlayScreenImage.Focus();
        UpdatePlaybackControls();
    }


    /// <summary>
    /// While a picked room's play session is prepared, its first-visit picture is shown on the play screen in the play frame's layout (the rows the
    /// last frame had), so the room keeps its size when the session's frames take over (2 November 2026, user report). LOOM plays in the room view and
    /// never switches.
    /// </summary>
    private void ShowWaitingPlayFrame(ScummRoomScene scene)
    {
        _ctx.KeepPlayScreen = false;
        // The room's own screen rows, from its first visit (Indy 3 Room 85: 8 to 199); the previous room's rows made the picture jump when play began.
        var (top, bottom) = scene.MainScreen ?? (_ctx.PlayShown is { } last ? (last.RoomTop, last.RoomBottom) : (16, 144));
        var frame = ScummPlayScreen.Still(scene, top, bottom, wide: NormalRoomWidthToggle.IsChecked != true);
        _ctx.PlayShown = frame;
        if (_ctx.PlayBitmap is null || _ctx.PlayBitmap.PixelWidth != frame.PixelWidth) _ctx.PlayBitmap = new WriteableBitmap(frame.PixelWidth, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null);
        _ctx.PlayBitmap.WritePixels(new Int32Rect(0, 0, frame.PixelWidth, ScummPlayFrame.Height), frame.Pixels, frame.PixelWidth * 4, 0);
        PlayScreenImage.Source = _ctx.PlayBitmap;
        RoomDisplayGroup.Visibility = Visibility.Collapsed;
        PlayScreenImage.Visibility = Visibility.Visible;
    }

    /// <summary>Stops the frames and gives the room view back (another game, another kind of resource).</summary>
    private void StopPlay(bool keepScreen = false)
    {
        _ctx.PlayTimer?.Stop();
        _ctx.PlayTimer = null;
        if (_ctx.Play is not null && _sceneSound.Follows(_ctx.Play)) _sceneSound.StopAll("play was left");
        _playSpeech.Detach();
        // A room pick in a played game keeps the screen showing until the new room's picture replaces it (no flicker).
        if (_ctx.KeepPlayScreen || keepScreen) { PlayScreenImage.Cursor = null; UpdateGameStatusButton(); if (PlayCutscenes) UpdatePlayCutsceneControls(); return; }
        PlayScreenImage.Visibility = Visibility.Collapsed;
        PlayScrollButtonsPanel.Visibility = ScrollRowWhenIdle;
        ClearPlayOverlays();
        PlayScreenImage.Cursor = null;
        RoomDisplayGroup.Visibility = Visibility.Visible;
        UpdateGameStatusButton();
        if (PlayCutscenes) UpdatePlayCutsceneControls();
    }

    /// <summary>Step ▸ (Advanced, as in LOOM): while paused, plays exactly one frame of the game's clock.</summary>
    private void StepPlayFrame()
    {
        if (!PlayActive || !_ctx.PlayPaused || _ctx.Play is null || _sceneSound.IsPreparing) return;
        _ctx.Play.AdvanceFrame();
        _sceneSound.Update();
        _playSpeech.Update();
        _ctx.PlayFrame++;
        ShowPlayFrame();
        RefreshPlayControlsOnFrame();
        StatusText.Text = $"Paused after one frame (Room {_ctx.Play.CurrentRoomId:00}). Play goes on.";
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (_ctx.Play is null) return;
        // R-7.16 Pause stops the game's sounds with its clock and lets them go on with it (the app's Pause key only flips _ctx.PlayPaused).
        if (_ctx.PlayPaused != _ctx.PlaySoundPaused)
        {
            _ctx.PlaySoundPaused = _ctx.PlayPaused;
            if (_ctx.PlayPaused) { _sceneSound.Pause(); _playSpeech.Pause(); } else { _sceneSound.Resume(); _playSpeech.Resume(); }
        }
        // As in LOOM, a sound the game just started holds the frames while it is rendered, so music and scene stay in step.
        if (_ctx.PlayPaused || _sceneSound.IsPreparing) return;
        _ctx.Play.AdvanceFrame();
        _sceneSound.Update();
        _playSpeech.Update();
        _ctx.PlayFrame++;
        if (_ctx.PlayTimer is { } timer) timer.Interval = TimeSpan.FromMilliseconds(_ctx.Play.FrameJiffies * 1000.0 / 60);
        ShowPlayFrame();
        RefreshPlayControlsOnFrame();
    }

    private void ShowPlayFrame()
    {
        if (_ctx.PlayScreen is null || _ctx.Play is null) return;
        var frame = _ctx.PlayScreen.Compose(_ctx.PlayFrame, wide: NormalRoomWidthToggle.IsChecked != true);
        _ctx.PlayShown = frame;
        if (frame.RoomId is int shownRoom && _ctx.CurrentRoomResource?.ResourceId != shownRoom &&
            _ctx.Game?.Rooms.FirstOrDefault(r => r.ResourceId == shownRoom) is { } nativeRoom)
        {
            // Follow a native load without re-entering it through catalog preparation.
            _ctx.CurrentRoomResource = nativeRoom;
            _ctx.LastViewedRoomResource = nativeRoom;
            _ctx.SelectedResource = nativeRoom;
            _ctx.FollowingPlayRoom = true;
            try { SelectResourceInTree(nativeRoom); }
            finally { _ctx.FollowingPlayRoom = false; }
            RoomCardList.Children.OfType<System.Windows.Controls.Button>()
                .FirstOrDefault(card => Equals(card.Tag, nativeRoom))?.BringIntoView();
            RefreshPlayRoomActions();
        }
        if (_ctx.PlayBitmap is null || _ctx.PlayBitmap.PixelWidth != frame.PixelWidth) _ctx.PlayBitmap = new WriteableBitmap(frame.PixelWidth, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null);
        _ctx.PlayBitmap.WritePixels(new Int32Rect(0, 0, frame.PixelWidth, ScummPlayFrame.Height), frame.Pixels, frame.PixelWidth * 4, 0);
        PlayScreenImage.Source = _ctx.PlayBitmap;
        // R-7.9: the game's cursor replaces the system pointer while the game shows it, and nothing shows while it is off.
        PlayScreenImage.Cursor = Cursors.None;
        if (IsAdvancedMode)
            PlayScreenImage.ToolTip = $"Room {frame.RoomId:00} · input script {_ctx.Play.PlayInputScript} · " +
                                      (_ctx.Play.DeliveredInput.LastOrDefault() is { } last ? $"last input runInputScript({last.Area}, {last.Value}, {last.Mode}): {last.Description}" : "no input yet") +
                                      (_ctx.PlayPaused ? " · paused" : "");
        else PlayScreenImage.ToolTip = null;
        TryUpdateGenericRoomSounds();
        UpdatePlayScrollButtons();
        RefreshPlayOverlays(frame);
        RefreshPlayOverlayLists();
    }

    // The image's point as a game screen point (0..319, 0..199).
    private (int X, int Y)? PlayPoint(MouseEventArgs e)
    {
        return PlayPointAt(e.GetPosition(PlayScreenImage), PlayScreenImage.ActualWidth, PlayScreenImage.ActualHeight, _ctx.PlayShown?.PixelWidth ?? ScummPlayFrame.Width);
    }

    // Match Image.Stretch=Uniform, including the unused bands around the native screen.
    private static (int X, int Y)? PlayPointAt(Point position, double width, double height, int frameWidth = ScummPlayFrame.Width)
    {
        if (width <= 0 || height <= 0) return null;
        var scale = Math.Min(width / frameWidth, height / ScummPlayFrame.Height);
        var offsetX = (width - frameWidth * scale) / 2;
        var offsetY = (height - ScummPlayFrame.Height * scale) / 2;
        var nativeX = (position.X - offsetX) / scale;
        var nativeY = (position.Y - offsetY) / scale;
        if (nativeX < 0 || nativeY < 0) return null;
        var x = (int)nativeX;
        var y = (int)nativeY;
        return x >= 0 && x < frameWidth && y is >= 0 and < ScummPlayFrame.Height ? (x, y) : null;
    }

    /// <summary>
    /// A point of the shown frame as a point of the game's own 320 × 200 screen. In wide mode (R-6.49) the room rows show the whole room: a point the game's
    /// camera shows maps straight; one it does not show is, when <paramref name="jump"/> (a click), first shown by putting the camera there at once, as
    /// <c>setCameraAt</c> would, so the game never receives a click for a place it is not showing. The verb and text rows are the screen's own.
    /// </summary>
    private (int X, int Y)? GamePoint((int X, int Y)? point, bool jump)
    {
        if (point is not { } p || _ctx.PlayShown is not { } frame || !frame.Wide || _ctx.Play is not { } play) return point;
        if (p.Y >= frame.RoomTop && p.Y < frame.RoomBottom)
        {
            var screenX = p.X - frame.CameraLeft;
            if (screenX is >= 0 and < ScummPlayFrame.Width) return (screenX, p.Y);
            if (!jump || !PlayTakesInput) return null;
            play.JumpCameraTo(p.X);
            screenX = p.X - play.ViewLeft;
            return screenX is >= 0 and < ScummPlayFrame.Width ? (screenX, p.Y) : null;
        }
        var verbX = p.X - frame.VerbLeft;
        return verbX is >= 0 and < ScummPlayFrame.Width ? (verbX, p.Y) : null;
    }

    private void PlayScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!PlayActive) return;
        var shownPoint = PlayPoint(e);
        if (_ctx.PlayAmbient) { UpdatePlayOverlayHover(shownPoint); return; }
        _ctx.Play!.PointAtScreen(GamePoint(shownPoint, jump: false));
        UpdatePlayOverlayHover(shownPoint);
        if (_ctx.PlayPaused) ShowPlayFrame();
    }

    private void PlayScreenImage_MouseLeave(object sender, MouseEventArgs e)
    {
        if (PlayActive) _ctx.Play!.PointAtScreen(null);
        UpdatePlayOverlayHover(null);
    }

    // R-7.10: one press, one click, delivered at the next frame.
    private void PlayScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayTakesInput || PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        PlayScreenPress(point, e.ClickCount);
    }

    // A left press on the shown frame's point (0..frame width, 0..199).
    private void PlayScreenPress((int X, int Y) point, int clickCount)
    {
        SelectGenericInteractAt(point);
        // R-1.5 / R-6.25.2: where the game's own input leaves a double click free (ScummProfile.DoubleClickLookVerb), the second press of a double click
        // looks at the object under it through the game's own sentence path; the first press was the normal click. Elsewhere it is just a second click.
        if (GamePoint(point, jump: true) is not { } gamePoint) return;
        if (clickCount == 2 && PlayObjectAt(point) && _ctx.Play!.DoubleClickLook(gamePoint.X, gamePoint.Y, out _) is not null)
        {
            UpdatePlaybackControls();
            return;
        }
        if (_ctx.Play!.ClickScreen(gamePoint.X, gamePoint.Y, out var refused) is null && IsAdvancedMode) StatusText.ToolTip = $"Click not delivered: {refused}";
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
        if (GamePoint(PlayPoint(e), jump: true) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        PlayRightClick(point.X, point.Y);
    }

    private bool PlayRightClickReachesGame => PlayTakesInput && _ctx.Game is { } game && ScummProfile.For(game).RightClickReachesGame;

    private void UpdatePlaySoundCard()
    {
        // vars.cpp's device numbers (also ScummGameSession.AdlibCard): v1/v2
        // have no VAR_SOUNDCARD at var[0x030]; that word is their game's state.
        if (_ctx.Play is null || _ctx.Game is not { } game || ScummProfile.For(game).EngineVersion < ScummVersion.V3) return;
        // Sound polish (19 October 2026): with the PC speaker, the scripts' isSoundRunning follows that driver's lengths (ScummVM Player_V2).
        _ctx.Play.PcSpeakerTiming = _sceneSound.Mode == SceneSoundPlayer.SoundMode.PcSpeaker;
        _ctx.Play.SoundCard = _sceneSound.Mode switch
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
        var input = _ctx.Play!.ClickScreen(x, y, true, out var refused);
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
        if (_ctx.PlayAmbient) return false; // A static preview takes no keys but its Pause.
        int? code = key switch
        {
            >= Key.F1 and <= Key.F9 => 315 + (key - Key.F1),
            Key.Enter => 13,
            Key.Back => 8,
            >= Key.A and <= Key.Z when Keyboard.Modifiers == ModifierKeys.Control => 1 + (key - Key.A),
            _ => null
        };
        if (code is not int value) return false;
        _ctx.Play!.PressPlayKey(value, out _);
        UpdatePlaybackControls();
        e.Handled = true;
        return true;
    }

    // Printable keys as the characters they type (verb shortcuts are lower-case letters; Script 24 reads '<' and '>').
    private void PlayTextInput(TextCompositionEventArgs e)
    {
        if (!PlayTakesInput || string.IsNullOrEmpty(e.Text) || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
        var character = e.Text[0];
        if (character is < ' ' or > '~' || character is ' ' or '.') return; // '.' is Skip line, a shared key (SharedTextInput)
        e.Handled = true;
        _ctx.Play!.PressPlayKey(character, out _);
        UpdatePlaybackControls();
    }
}

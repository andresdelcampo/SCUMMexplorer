using System.IO;
using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private enum RoomOpenKind { Catalog, Native, Cutscene, Intro }
    private RoomOpenKind _roomOpenKind;
    private int _cutsceneLaunchGeneration;
    private bool _catalogPreviewOnly;
    private bool _introPreparing;
    // R-8.5b (rule confirmed by the user 20 Oct): a LOOM static preview explores an ambient-only copy (ScummSession.PrepareAmbientRoom); this is the
    // session it was made from, which carries the game on when another room or a scene is opened.
    private LoomExplorationSession? _loomAmbientSource;
    private string? _pendingStaticPreviewNote;

    private void LeaveLoomAmbientPreview()
    {
        if (_loomAmbientSource is not { } source) return;
        _loomAmbientSource = null;
        StopScenePlayback();
        _sceneSound.StopAll("the static preview's ambient scripts were left");
        _session = source;
    }
    private Task? _roomOpenTask;

    // Capture the source in the tree handler before its first await. Never infer a doorway
    // from the room number: manual navigation and native script loads can name the same room.
    private void SelectRoomWithIntent(GameResource room, RoomOpenKind kind, bool reloadSelected = false)
    {
        var previous = _roomOpenKind;
        var selected = ResourceTree.SelectedItem as System.Windows.Controls.TreeViewItem;
        var same = Equals(selected?.Tag, room);
        _roomOpenKind = kind;
        try
        {
            SelectResourceInTree(room);
            if (reloadSelected && same && selected is not null)
                ResourceTree_SelectedItemChanged(ResourceTree, new RoutedPropertyChangedEventArgs<object>(selected, selected));
        }
        finally { _roomOpenKind = previous; }
    }

    /// <summary>A played game whose own scripts start an opening (script 1), offered in the header until Room 01's list has it (R-6.38).</summary>
    private bool NativeIntroOffered => _game is { } game && !HasDedicatedSession && ScummProfile.For(game).PlaysThroughVerbs &&
        GameSupport.For(game).LiveSession && game.Resources.Any(r => r.Kind == ResourceKind.Script && r.ResourceId == 1 && r.ScriptRoom is null);

    private void UpdateGameIntroButton()
    {
        if (GameIntroButton is null) return;
        var entry = CurrentRoomControls.GameIntro();
        GameIntroButton.Visibility = entry.Offered ? Visibility.Visible : Visibility.Collapsed;
        GameIntroButton.Content = entry.Caption;
        GameIntroButton.IsEnabled = entry.Enabled;
        GameIntroButton.ToolTip = IsAdvancedMode
            ? "Start the game's native opening in a fresh session, replacing the exploration state. Required startup choices remain yours."
            : "Start the game's opening.";
    }

    private async void GameIntro_Click(object sender, RoutedEventArgs e)
    {
        if (_game is not { } game || !GameIntroButton.IsEnabled) return;
        var launch = ++_cutsceneLaunchGeneration;
        _introPreparing = true;
        UpdateGameIntroButton();
        StopPlay();
        _playAmbient = false;
        _playLiveSource = null;
        _loomAmbientSource = null; // the intro starts a fresh session either way
        StopScenePlayback();
        _sceneSound.StopAll("the game intro was explicitly started");
        try
        {
            // A previous catalog request must finish cancellation before its continuation
            // could publish a prepared session over this deliberate fresh start.
            _previewCancellation?.Cancel();
            if (_roomOpenTask is { } previousOpen) await previousOpen;
            if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration) return;
            await CurrentRoomControls.GameIntro().Start(launch);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            if (ReferenceEquals(_game, game) && launch == _cutsceneLaunchGeneration && IsAdvancedMode)
                StatusText.ToolTip = $"The native intro could not start: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_game, game) && launch == _cutsceneLaunchGeneration) _introPreparing = false;
            UpdatePlaybackControls();
        }
    }

    /// <summary>The native opening of a game played through <see cref="ScummGameSession"/>: a fresh session from the game's own start, nothing answered for the player (R-7.20).</summary>
    private async Task StartGenericIntroAsync(GameLibrary game, int launch)
    {
        _catalogPreviewOnly = false;
        ScummIndexedObjectSnapshot? objects = null;
        var intro = await Task.Run(() => ScummGameSession.StartIntro(game, out objects));
        if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration) return;
        _play = intro;
        _playFolder = game.Key;
        _playIndexedStates = objects!.States;
        _playScreen = new ScummPlayScreen(game, intro, _playIndexedStates);
        _playCdTracks = ScummCdAudioTracks.For(game);
        _playImuse = ScummImuseMusic.For(game);
        UpdatePlaySoundCard();
        if (intro.CurrentRoomId is int roomId && game.Rooms.FirstOrDefault(r => r.ResourceId == roomId) is { } room)
        {
            SelectRoomWithIntent(room, RoomOpenKind.Intro, reloadSelected: true);
            if (_roomOpenTask is { } opening) await opening;
        }
        else
        {
            // Native boots can yield before loading any room. Show their real black
            // screen/messages and advance normally, without guessing a room to enter.
            _currentRoom = null;
            _currentRoomResource = null;
            UpdateRoomCardSelection(null);
            ShowPageHeader(false);
            PreviewMessage.Visibility = Visibility.Collapsed;
            RoomDisplayLayout.Visibility = Visibility.Visible;
            _sceneSound.Attach(game, intro, RenderPlaySound, PlaySoundLoop, replayReachedEvents: true);
            if (game.Speech is { } speech) _playSpeech.Attach(speech, intro.Messages, 0);
            StartPlayClock();
        }
        if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration) return;
        StatusText.ToolTip = IsAdvancedMode ? "Native boot started explicitly; fresh session, no assumed player answers." : null;
    }
}

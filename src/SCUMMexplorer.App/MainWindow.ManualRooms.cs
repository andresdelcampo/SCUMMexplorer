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

    private void UpdateGameIntroButton()
    {
        if (GameIntroButton is null) return;
        var game = _game;
        var knownLoomIntro = game is { Id: GameId.Loom or GameId.LoomVga, IsDemo: false };
        var nativeIntro = game is not null && !IsLoomGame && ScummProfile.For(game).PlaysThroughVerbs &&
            GameSupport.For(game).LiveSession && game.Resources.Any(r => r.Kind == ResourceKind.Script && r.ResourceId == 1 && r.ScriptRoom is null);
        // Established LOOM entries already provide the intro in Room 01. Generic native
        // startup is a whole-game action, separate from unrelated entry scenes in Room 01.
        var inRoomOne = knownLoomIntro && (game!.Id == GameId.LoomVga ? CdIntroPlaysIn(1)
            : ScannedCutscenes || (_replayCatalog?.Membership ?? []).Any(m => m.Id == "opening" && (m.StartRoom == 1 || m.Rooms.Contains(1))));
        GameIntroButton.Visibility = (knownLoomIntro || nativeIntro) && (!inRoomOne || _catalogPreviewOnly) ? Visibility.Visible : Visibility.Collapsed;
        GameIntroButton.IsEnabled = !_introPreparing && (IsLoomGame ? CanStartCutscene || _catalogPreviewOnly : !PlayActive || !_play!.InCutscene);
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
        StopScenePlayback();
        _sceneSound.StopAll("the game intro was explicitly started");
        try
        {
            // A previous catalog request must finish cancellation before its continuation
            // could publish a prepared session over this deliberate fresh start.
            _previewCancellation?.Cancel();
            if (_roomOpenTask is { } previousOpen) await previousOpen;
            if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration) return;
            if (game.Id == GameId.Loom) { PlayOpening(); return; }
            if (game.Id == GameId.LoomVga) { PlayCdIntro(); return; }
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
}

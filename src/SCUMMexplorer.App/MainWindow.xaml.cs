using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Relationships;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.App;

public partial class MainWindow : Window
{
    private readonly GameFolderScanner _scanner = new();
    private readonly OldV3EgaRoomDecoder _roomDecoder = new();
    private readonly LoomRoomObjects _objectDecoder = new();
    private readonly LoomRoomObjectDirectory _objectDirectory = new();
    private readonly LflObjectStateReader _stateReader = new();
    private readonly LoomCostumeReader _costumeReader = new();
    private readonly LoomCostumeRenderer _costumeRenderer = new();
    private LoomScriptReader _scriptReader = new();
    private LoomObjectVerbReader _verbReader = new();
    private readonly LoomRoomSoundCandidates _roomSoundCandidates = new();
    private readonly LoomBoundedScriptExecutor _scriptExecutor = new();
    private readonly LoomRelationshipIndexer _relationshipIndexer = new();
    private readonly LoomPcSpeakerAudio _audioDecoder = new();
    private readonly LoomAdlibAudio _adlibDecoder = new();
    private readonly MediaPlayer _audioPlayer = new();
    // The sounds the scripts start while the playback clock runs (cutscenes); separate from the room music panel.
    private readonly SceneSoundPlayer _sceneSound = new();
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _roomPanTimer = new() { Interval = TimeSpan.FromMilliseconds(75) };
    private readonly DispatcherTimer _adlibGainDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private static readonly string LastFolderFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SCUMMexplorer", "last-folder.txt");
    private GameLibrary? _game;
    private LoomRelationshipIndex? _relationshipIndex;
    private GameResource? _selectedResource;
    private GameResource? _lastViewedRoomResource;
    private int _relationshipBuildGeneration;
    private int _roomCardGeneration;
    private CancellationTokenSource? _roomCardCancellation;
    private CancellationTokenSource? _previewCancellation;
    private RoomImage? _currentRoom;
    private GameResource? _currentRoomResource;
    private LoomWalkBoxSet? _currentWalkBoxes;
    private IReadOnlyList<LoomRoomObjectRecord> _currentRoomObjectRecords = [];
    private IReadOnlyDictionary<int, string> _roomObjectNames = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> _roomVerbs =
        new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
    private IReadOnlyList<RoomVerbDestination> _roomNavigationCandidates = [];
    private IReadOnlyList<RoomObjectImage> _objects = [];
    private RoomObjectSelection _objectSelection = new([]);
    private IReadOnlySet<int> _estimatedObjectIds = new HashSet<int>();
    private IReadOnlyDictionary<int, byte> _indexedStates = new Dictionary<int, byte>();
    private LoomIndexedObjectSnapshot? _indexedObjectSnapshot;
    private LoomExplorationSession? _session;
    // Charset 0 (99.LFL) for messages, or why it could not be read (then a labelled UI font is used).
    private LoomCharset? _charset;
    // Costumes read for the actor layer (M2c), by id; null when a costume cannot be read.
    private readonly Dictionary<int, LoomCostume?> _actorCostumes = [];
    private string? _charsetError;
    private int? _selectedObject;
    private string? _audioFile;
    private byte[]? _adlibBaseWave;
    private CancellationTokenSource? _audioCancellation;
    private bool _audioReady;
    private int _adlibGainRequest;
    private bool _restoreAudioAfterGainChange;
    private bool _resumeAudioAfterGainChange;
    private TimeSpan _audioPositionAfterGainChange;
    private GameResource? _selectedSound;
    private bool _changingAudioFormat;
    private bool _playWhenReady;
    private AudioSurface _audioSurface;
    private GameResource? _roomSelectedCue;
    private GameResource? _roomRenderedCue;
    private int? _roomCueSourceRoomId;
    private bool _roomCueReached;
    private bool _roomCueManuallySelected;
    private readonly Dictionary<int, TimeSpan> _roomCueDurations = [];
    private readonly Dictionary<int, (bool IsMusic, TimeSpan? Duration)> _roomCueTrackCache = [];
    private int _roomMusicUpdateGeneration;
    private enum AudioSurface { None, Resource, Room }
    private sealed record RoomVerbDestination(LoomRoomObjectRecord Object, LoomObjectVerbEntry Verb,
        int TargetRoomId, int InstructionOffset, string Path = "direct");
    private LoomSceneDrawBatches? _sceneBatches;
    private LoomDoorwayFinder? _doorwayFinder;
    private IReadOnlyList<RoomArtworkEstimate> _artworkEstimates = [];
    private sealed record RoomVerbChoice(LoomRoomObjectRecord Object, LoomObjectVerbEntry Verb);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ReopenLastFolder();
        Closed += (_, _) => { _roomCardCancellation?.Cancel(); StopRoomPan(); StopAudio(); };
        Deactivated += (_, _) => StopRoomPan();
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewTextInput += MainWindow_PreviewTextInput;
        _roomPanTimer.Tick += RoomPanTimer_Tick;
        _cameraPanTimer.Tick += CameraPanTimer_Tick;
        _audioTimer.Tick += (_, _) => UpdateAudioTime();
        _adlibGainDebounceTimer.Tick += async (_, _) =>
        {
            _adlibGainDebounceTimer.Stop();
            await ApplyAdlibGainAsync();
        };
        _audioPlayer.MediaOpened += AudioPlayer_MediaOpened;
        _audioPlayer.MediaOpened += (_, _) => _sceneSound.StopAll("another sound was started in the app");
        _audioPlayer.MediaEnded += (_, _) => StopPlayback();
        _audioPlayer.MediaFailed += (_, args) =>
        {
            var surface = _audioSurface;
            var message = args.ErrorException?.Message ?? "Audio playback failed.";
            StopAudio();
            if (surface == AudioSurface.Room)
            {
                RoomCueStatus.Text = $"Playback unavailable: {message}";
                RoomCuePlayButton.ToolTip = RoomCueStatus.Text;
            }
            else
            {
                AudioDescription.Text = message;
                PlayButton.IsEnabled = false;
                PauseButton.IsEnabled = false;
                StopButton.IsEnabled = false;
            }
        };
        UpdateAdvancedMode();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Open a SCUMM game folder", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;

        try
        {
            OpenGameFolder(picker.FolderName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open game folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GameTexts_Click(object sender, RoutedEventArgs e)
    {
        if (_game is null)
        {
            MessageBox.Show(this, "Open a LOOM game folder first.", "Game texts", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var extraction = new LoomTextExtractor().Extract(_game);
            var gallery = new TextGalleryWindow(_game, extraction, roomId =>
            {
                if (_game?.Rooms.FirstOrDefault(room => room.ResourceId == roomId) is { } room)
                    SelectResourceInTree(room);
            }, GalleryCutscenes()) { Owner = this };
            gallery.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not read game texts", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenGameFolder(string folder)
    {
        var game = _scanner.Scan(folder);
        var indexedSnapshot = game.Id == GameId.Loom && game.Version == ScummVersion.V3
            ? _stateReader.ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"))
            : null;
        _game = game;
        _scriptReader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        _verbReader = new LoomObjectVerbReader(ScummScriptDialects.For(game.Id));
        _sceneBatches = null;
        _doorwayFinder = null;
        _indexedObjectSnapshot = indexedSnapshot;
        _indexedStates = indexedSnapshot?.States ?? new Dictionary<int, byte>();
        StopScenePlayback();
        _sceneSound.StopAll("another game was opened");
        _session = indexedSnapshot is null ? null : new LoomExplorationSession(game, indexedSnapshot);
        _charsetError = null;
        _charset = game.Id == GameId.Loom ? LoomCharset.TryLoad(game.FolderPath, 0, out _charsetError) : null;
        _actorCostumes.Clear();
        ShowGame(game);
        StartRelationshipIndexBuild(game);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastFolderFile)!);
            File.WriteAllText(LastFolderFile, game.FolderPath);
        }
        catch (IOException) { StatusText.Text = "Game opened; last folder could not be saved."; }
        catch (UnauthorizedAccessException) { StatusText.Text = "Game opened; last folder could not be saved."; }
    }

    private void ReopenLastFolder()
    {
        try
        {
            if (!File.Exists(LastFolderFile)) return;
            var folder = File.ReadAllText(LastFolderFile).Trim();
            if (Directory.Exists(folder)) OpenGameFolder(folder);
            else StatusText.Text = "Last game folder is unavailable. Choose a folder to open.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = "Last game folder could not be reopened. Choose a folder to open.";
        }
    }

    private void ShowGame(GameLibrary game)
    {
        Interlocked.Increment(ref _relationshipBuildGeneration);
        _relationshipIndex = null;
        _selectedResource = null;
        _lastViewedRoomResource = null;
        ConnectionsButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        StopAudio();
        AudioPanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        CostumePanel.Visibility = Visibility.Collapsed;
        CostumePoseGallery.Children.Clear();
        RoomDisplayLayout.Visibility = Visibility.Collapsed;
        StopRoomPan();
        RoomObjectPanel.Visibility = Visibility.Visible;
        Grid.SetColumnSpan(ContentPanel, 1);
        _previewCancellation?.Cancel();
        _currentRoom = null;
        _currentRoomResource = null;
        _currentRoomObjectRecords = [];
        _roomVerbs = new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
        _roomNavigationCandidates = [];
        _objects = [];
        _objectSelection = new([]);
        _estimatedObjectIds = new HashSet<int>();
        _selectedObject = null;
        ClearObjectList();
        ObjectsNote.Text = "Choose a room.";
        SessionModeToggle.Visibility = _session is null ? Visibility.Collapsed : Visibility.Visible;
        SessionActions.Visibility = _session is null ? Visibility.Collapsed : Visibility.Visible;
        SessionStatusText.Text = _session is null ? "" : "Session ready. Select a room to visit it.";
        AdvanceFrameButton.IsEnabled = _session?.Snapshot.PendingScripts > 0;
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        RoomEntryTraceOutput.Text = "";
        RoomEntryEventButtons.Children.Clear();
        RoomNavigationButtons.Children.Clear();
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomNavigationOverlay.Visibility = Visibility.Collapsed;
        RoomNavigationToggle.Visibility = Visibility.Collapsed;
        ObjectActionPanel.Visibility = Visibility.Collapsed;
        ObjectVerbButtons.Children.Clear();
        ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
        RoomMusicButtons.Children.Clear();
        RoomMusicPanel.Visibility = Visibility.Collapsed;
        _roomSelectedCue = null;
        _roomRenderedCue = null;
        _roomCueSourceRoomId = null;
        _roomCueManuallySelected = false;
        _roomCueDurations.Clear();
        _roomCueTrackCache.Clear();
        RoomPreview.ToolTip = null;
        RoomPreview.Cursor = Cursors.Arrow;
        ScriptEventButtons.Children.Clear();
        ObjectAssetPreview.Source = null;
        ObjectAssetPreview.Visibility = Visibility.Collapsed;
        ObjectAssetDescription.Text = "";
        RoomPreview.Source = null;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.IsEnabled = false;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        WalkBoxToggle.Visibility = Visibility.Collapsed;
        WalkBoxOverlay.Visibility = Visibility.Collapsed;
        PreviewMessage.Visibility = Visibility.Visible;
        GameTitle.Text = game.Title;
        GameInfo.Text = game.Platform == "DOS" ? "DOS edition" : game.Platform;
        GameInfo.ToolTip = $"{game.Version switch { ScummVersion.Unknown => "SCUMM version unconfirmed", _ => $"SCUMM {game.Version}" }} · {game.Platform}";
        ResourceTree.Items.Clear();
        RoomCardList.Children.Clear();
        RoomCardScroll.Visibility = Visibility.Visible;

        var rooms = new TreeViewItem { Header = $"Rooms  ({game.Rooms.Count()})", IsExpanded = false };
        foreach (var room in game.Rooms)
            rooms.Items.Add(new TreeViewItem { Header = room.Name, Tag = room });
        ResourceTree.Items.Add(rooms);

        foreach (var (kind, label) in new[]
        {
            (ResourceKind.Costume, "Costumes"), (ResourceKind.Script, "Scripts"),
            (ResourceKind.Sound, "Sounds & music"), (ResourceKind.Executable, "Game files")
        })
        {
            if (game.Id != GameId.Loom && kind != ResourceKind.Executable) continue;
            var group = game.Resources.Where(resource => resource.Kind == kind).ToArray();
            if (group.Length == 0) continue;
            var node = new TreeViewItem { Header = $"{label}  ({group.Length})", IsExpanded = false };
            foreach (var item in group) node.Items.Add(new TreeViewItem { Header = item.Name, Tag = item });
            ResourceTree.Items.Add(node);
        }

        PopulateRoomCards(game);
        UpdateAdvancedMode();

        PageTitle.Text = game.Title;
        PageSubtitle.Text = game.Id switch
        {
            GameId.IndianaJones3 when game.Version == ScummVersion.V3 =>
                "EGA room background preview · object and script decoding is not yet verified.",
            GameId.Loom when game.Version == ScummVersion.V3 => "Explore rooms, objects, and sounds.",
            _ => "The folder index was read, but room and resource inspection is not supported for this game yet."
        };
        PageSubtitle.ToolTip = game.DetectionNote;
        PreviewTitle.Text = "Choose a room";
        PreviewDescription.Text = game.Id == GameId.IndianaJones3 && game.Version == ScummVersion.V3
            ? "Select an EGA room to preview its background."
            : game.Id == GameId.Loom && game.Version == ScummVersion.V3
                ? "Select a room to view its EGA background."
                : "This game layout does not yet have a supported room background decoder.";
        StatusText.Text = "Choose a room to begin.";
        StatusText.ToolTip = $"{game.Rooms.Count()} indexed rooms found.";
        FooterText.Text = $"{game.Title} · {game.Platform}";
        FooterText.ToolTip = game.FolderPath;
    }

    private void PopulateRoomCards(GameLibrary game)
    {
        _roomCardCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _roomCardCancellation = cancellation;
        var generation = ++_roomCardGeneration;
        var rooms = game.Rooms.ToArray();
        var indexedSnapshot = _indexedObjectSnapshot is null ? null : new LoomIndexedObjectSnapshot(
            _indexedObjectSnapshot.States.ToDictionary(pair => pair.Key, pair => pair.Value),
            _indexedObjectSnapshot.Owners.ToDictionary(pair => pair.Key, pair => pair.Value),
            _indexedObjectSnapshot.Classes?.ToDictionary(pair => pair.Key, pair => pair.Value));
        var indexedStates = new Dictionary<int, byte>(_indexedStates);

        // Create every card before starting any file reads or script execution.
        foreach (var roomResource in rooms)
        {
            var placeholder = new TextBlock
            {
                Text = roomResource.ResourceId?.ToString("00") ?? "?",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var button = new Button
            {
                Width = 96,
                Height = 42.7,
                Padding = new Thickness(3, 0, 3, 0),
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(11, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 82, 96)),
                BorderThickness = new Thickness(2),
                Content = placeholder,
                ToolTip = $"Room {roomResource.ResourceId:00} · loading preview",
                Tag = roomResource
            };
            System.Windows.Automation.AutomationProperties.SetName(button, $"Open room {roomResource.ResourceId:00}");
            button.Click += (_, _) => SelectResourceInTree(roomResource);
            if (_selectedResource?.ResourceId == roomResource.ResourceId ||
                _lastViewedRoomResource?.ResourceId == roomResource.ResourceId)
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(183, 132, 66));
            RoomCardList.Children.Add(button);
        }

        _ = LoadRoomCardThumbnailsAsync(game, rooms, indexedSnapshot, indexedStates,
            generation, cancellation.Token);
    }

    private async Task LoadRoomCardThumbnailsAsync(GameLibrary game, IReadOnlyList<GameResource> rooms,
        LoomIndexedObjectSnapshot? indexedSnapshot, IReadOnlyDictionary<int, byte> indexedStates,
        int generation, CancellationToken cancellationToken)
    {
        var sceneBatches = LoomSceneDrawBatches.Empty;
        if (game.Id == GameId.Loom && game.Version == ScummVersion.V3 && indexedSnapshot is not null)
        {
            try
            {
                sceneBatches = await Task.Run(() => LoomSceneDrawBatches.Build(game), cancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception)
            {
                sceneBatches = LoomSceneDrawBatches.Empty;
            }
        }

        foreach (var roomResource in rooms)
        {
            if (cancellationToken.IsCancellationRequested) return;
            BitmapSource? bitmap = null;
            string? failure = null;
            try
            {
                bitmap = await Task.Run(() => RenderRoomCardThumbnail(game, roomResource,
                    indexedSnapshot, indexedStates, sceneBatches, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                failure = ex.Message;
            }

            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested || generation != _roomCardGeneration ||
                        !ReferenceEquals(_game, game)) return;
                    if (bitmap is not null) SetRoomCardThumbnail(roomResource, bitmap);
                    else SetRoomCardFailure(roomResource, failure ?? "Room preview unavailable.");
                }, DispatcherPriority.Background);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested || Dispatcher.HasShutdownStarted) { return; }
            catch (Exception) { return; }
        }
    }

    private static BitmapSource RenderRoomCardThumbnail(GameLibrary game, GameResource roomResource,
        LoomIndexedObjectSnapshot? indexedSnapshot, IReadOnlyDictionary<int, byte> indexedStates,
        LoomSceneDrawBatches sceneBatches, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = File.ReadAllBytes(roomResource.FilePath);
        var decoder = new OldV3EgaRoomDecoder();
        var room = decoder.Decode(bytes);
        var pixels = room.Pixels.ToArray();
        var cameraX = 0;
        if (game.Id == GameId.Loom && game.Version == ScummVersion.V3 && indexedSnapshot is not null)
        {
            var records = new LoomRoomObjectDirectory().Decode(bytes);
            var objects = new LoomRoomObjects().Decode(bytes);
            var session = new LoomExplorationSession(game, indexedSnapshot);
            var snapshot = session.VisitRoom(roomResource);
            var initialized = CreateSessionRoomSelection(roomResource, room, records, objects,
                snapshot, indexedStates, sceneBatches);
            pixels = initialized.Selection.Compose(room);
            cameraX = session.CameraX ?? 0;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return CreateRoomCardBitmap(room, pixels, cameraX);
    }

    private static Image CreateRoomCardImage(BitmapSource source, string name)
    {
        var preview = new Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 86,
            Height = 38.7,
            SnapsToDevicePixels = true,
            ToolTip = name,
            Source = source
        };
        RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);
        return preview;
    }

    private void SetRoomCardFailure(GameResource resource, string message)
    {
        var card = RoomCardList.Children.OfType<Button>().FirstOrDefault(item => Equals(item.Tag, resource));
        if (card is not null && card.Content is not Image)
            card.ToolTip = $"Room {resource.ResourceId:00} · preview unavailable: {message}";
    }

    private bool IsAdvancedMode => AdvancedModeToggle.IsChecked == true;

    private void AdvancedModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdvancedMode && _selectedResource is { Kind: not ResourceKind.Room })
        {
            var returnRoom = _lastViewedRoomResource ?? _game?.Rooms.FirstOrDefault();
            if (returnRoom is not null) SelectResourceInTree(returnRoom);
        }
        UpdateAdvancedMode();
    }

    private void UpdateAdvancedMode()
    {
        var advanced = IsAdvancedMode;
        var rootGrid = ContentPanel.Parent as Grid;
        var showRoomDetails = advanced && _selectedResource?.Kind == ResourceKind.Room;
        if (rootGrid is not null && rootGrid.ColumnDefinitions.Count > 2)
            rootGrid.ColumnDefinitions[2].Width = new GridLength(showRoomDetails ? 290 : 0);
        RoomObjectPanel.Visibility = showRoomDetails ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(ContentPanel, showRoomDetails ? 1 : 2);
        ResourceTree.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        RoomCardScroll.Visibility = _game is null || advanced ? Visibility.Collapsed : Visibility.Visible;
        CameraPanToggle.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        GamePointerToggle.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        GameKeysToggle.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        VerbAreaToggle.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        RoomMaskToggle.Visibility = advanced && _currentRoom is not null ? Visibility.Visible : Visibility.Collapsed;
        WalkBoxToggle.Visibility = advanced && _currentRoom is not null && _currentWalkBoxes is not null
            ? Visibility.Visible : Visibility.Collapsed;
        SessionStatusText.Visibility = advanced && _session is not null
            ? Visibility.Visible : Visibility.Collapsed;
        SessionActions.Visibility = advanced && _session is not null
            ? Visibility.Visible : Visibility.Collapsed;
        RoomCueTime.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        RoomEntryTraceExpander.Visibility = advanced && !string.IsNullOrWhiteSpace(RoomEntryTraceOutput.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        ObjectActionTraceExpander.Visibility = advanced && !string.IsNullOrWhiteSpace(ObjectActionTraceOutput.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        ObjectListSection.Visibility = advanced && ObjectList.Children.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        RoomNavigationPanel.Visibility = advanced && RoomNavigationButtons.Children.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        ObjectVerbButtons.Visibility = Visibility.Visible;
        UpdateConnectionsButton();
    }

    private async void ResourceTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Opening another view stops the playback clock, unless the playback itself entered the room.
        if (!_playbackEnteringRoom) StopScenePlayback();
        if (e.NewValue is not TreeViewItem { Tag: GameResource resource })
        {
            _selectedResource = null;
            UpdateConnectionsButton();
            UpdateAdvancedMode();
            if (AudioPanel.Visibility == Visibility.Visible || CostumePanel.Visibility == Visibility.Visible || ScriptPanel.Visibility == Visibility.Visible)
            {
                StopAudio();
                AudioPanel.Visibility = Visibility.Collapsed;
                ScriptPanel.Visibility = Visibility.Collapsed;
                CostumePanel.Visibility = Visibility.Collapsed;
                PreviewMessage.Visibility = Visibility.Visible;
                PreviewTitle.Text = "Choose a room or sound";
                PreviewDescription.Text = "Select a room, costume, or sound to inspect.";
            }
            UpdateAdvancedMode();
            return;
        }
        StopRoomPan();
        RoomDisplayLayout.Visibility = Visibility.Collapsed;
        _currentRoomResource = null;
        _currentRoomObjectRecords = [];
        _roomVerbs = new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
        _roomNavigationCandidates = [];
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        RoomEntryTraceOutput.Text = "";
        RoomEntryEventButtons.Children.Clear();
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomNavigationOverlay.Visibility = Visibility.Collapsed;
        RoomNavigationToggle.Visibility = Visibility.Collapsed;
        RoomMusicPanel.Visibility = Visibility.Collapsed;
        RoomPreview.ToolTip = null;
        RoomPreview.Cursor = Cursors.Arrow;
        ScriptEventButtons.Children.Clear();
        _selectedResource = resource;
        UpdateRoomCardSelection(resource);
        UpdateAdvancedMode();
        StatusText.Visibility = Visibility.Visible;
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        UpdateConnectionsButton();
        if (resource.Kind == ResourceKind.Sound)
        {
            await ShowAudio(resource);
            return;
        }
        StopAudio();
        AudioPanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        if (resource.Kind == ResourceKind.Costume)
        {
            ShowCostume(resource);
            return;
        }
        if (resource.Kind == ResourceKind.Script)
        {
            _previewCancellation?.Cancel();
            _currentRoom = null;
            RoomDisplayLayout.Visibility = Visibility.Collapsed;
            CostumePanel.Visibility = Visibility.Collapsed;
            RoomObjectPanel.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(ContentPanel, 2);
            RoomPreview.Visibility = Visibility.Collapsed;
            RoomMaskToggle.Visibility = Visibility.Collapsed;
            WalkBoxToggle.Visibility = Visibility.Collapsed;
            WalkBoxOverlay.Visibility = Visibility.Collapsed;
            RoomNavigationToggle.Visibility = Visibility.Collapsed;
            PreviewMessage.Visibility = Visibility.Collapsed;
            ScriptPanel.Visibility = Visibility.Visible;
            PageTitle.Text = resource.Name;
            PageSubtitle.Text = "Indexed script resource · DOS LOOM old-v3";
            ScriptOutput.Text = "";
            ScriptNote.Text = "Reading DOS LOOM old-v3 bytecode…";
            try
            {
                var disassembly = _scriptReader.Decode(resource);
                ScriptOutput.Text = disassembly.Format();
                var events = disassembly.Instructions
                    .Select(instruction => LoomScriptEvent.FromInstruction(disassembly, instruction,
                        LoomScriptEventCertainty.StaticPotential))
                    .Where(item => item is not null)
                    .Cast<LoomScriptEvent>()
                    .ToArray();
                ShowScriptEvents(ScriptEventButtons, events);
                ScriptNote.Text = $"Control-flow decode: {disassembly.DecodedBytes}/{disassembly.CodeLength} resource bytes, " +
                    $"{disassembly.Issues?.Count ?? 0} unresolved paths. Event links are static candidates; branch choices and script order are not executed in this view.";
                StatusText.Text = $"{resource.Name} · {disassembly.Instructions.Count} instructions · " +
                    $"{disassembly.Issues?.Count ?? 0} decode issues";
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                ScriptNote.Text = "This indexed script resource could not be decoded.";
                ScriptOutput.Text = ex.Message;
                StatusText.Text = $"{resource.Name} · bytecode unavailable";
            }
            return;
        }
        if (resource.Kind != ResourceKind.Room) return;
        CostumePanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        RoomObjectPanel.Visibility = Visibility.Visible;
        Grid.SetColumnSpan(ContentPanel, 1);
        _previewCancellation?.Cancel();
        _previewCancellation = new CancellationTokenSource();
        var token = _previewCancellation.Token;
        PageTitle.Text = "Explore the scene";
        PageTitle.ToolTip = resource.Name;
        PageSubtitle.Text = "Original LOOM EGA artwork";
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        PageSubtitle.ToolTip = resource.FilePath;
        PreviewTitle.Text = "Scene";
        PreviewTitle.ToolTip = resource.Name;
        PreviewDescription.Text = "Reading the room background…";
        PreviewMessage.Visibility = Visibility.Visible;
        StatusText.Visibility = Visibility.Visible;
        StopCameraPan();
        _pendingCameraX = null;
        RoomViewport.ScrollToHorizontalOffset(0);
        RoomPreview.Source = null;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.IsEnabled = false;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        WalkBoxToggle.Visibility = Visibility.Collapsed;
        WalkBoxOverlay.Visibility = Visibility.Collapsed;
        RoomNavigationToggle.Visibility = Visibility.Collapsed;
        ClearObjectList();
        ObjectsNote.Text = "Reading room objects…";
        _currentRoom = null;
        _objects = [];
        _objectSelection = new([]);
        _currentRoomResource = null;
        _currentWalkBoxes = null;
        _currentRoomObjectRecords = [];
        _roomObjectNames = new Dictionary<int, string>();
        _roomVerbs = new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
        _roomNavigationCandidates = [];
        RoomNavigationButtons.Children.Clear();
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomNavigationOverlay.Visibility = Visibility.Collapsed;
        ObjectActionPanel.Visibility = Visibility.Collapsed;
        _selectedObject = null;
        ObjectAssetPreview.Source = null;
        ObjectAssetPreview.Visibility = Visibility.Collapsed;
        ObjectAssetDescription.Text = "";
        StatusText.Text = $"Opening {resource.Name}…";
        StatusText.ToolTip = resource.FilePath;

        if (_game is not { Version: ScummVersion.V3, Id: GameId.Loom or GameId.IndianaJones3 } ||
            !_roomDecoder.CanDecode(resource.FilePath, 3))
        {
            PreviewDescription.Text = "This room format is not supported yet.";
            return;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(resource.FilePath, token);
            var room = _roomDecoder.Decode(bytes);
            _currentWalkBoxes = TryReadWalkBoxes(bytes);
            if (_game?.Id == GameId.IndianaJones3)
            {
                if (token.IsCancellationRequested) return;
                _currentRoom = room;
                _currentRoomResource = resource;
                _lastViewedRoomResource = resource;
                _currentRoomObjectRecords = [];
                _roomVerbs = new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
                _roomNavigationCandidates = [];
                _objects = [];
                _objectSelection = new RoomObjectSelection([], background: room);
                _estimatedObjectIds = new HashSet<int>();
                _selectedObject = null;
                ClearObjectList();
                ObjectsNote.Text = "Background preview only. Object art, scripts, and room routes are not decoded for this game yet.";
                ObjectActionPanel.Visibility = Visibility.Collapsed;
                RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
                RoomMusicPanel.Visibility = Visibility.Collapsed;
                UpdateRoomNavigationActions(resource);
                RefreshRoomPreview();
                UpdateCurrentRoomCardThumbnail();
                RoomDisplayLayout.Visibility = Visibility.Visible;
                UpdateRoomViewportLayout();
                StatusText.HorizontalAlignment = HorizontalAlignment.Center;
                StatusText.Visibility = Visibility.Collapsed;
                RoomPreview.Visibility = Visibility.Visible;
                RoomMaskToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
                ShowWalkBoxToggle();
                PreviewMessage.Visibility = Visibility.Collapsed;
                UpdateAdvancedMode();
                PageSubtitle.Text = "EGA background preview · object and script decoding is not yet verified.";
                StatusText.Text = "Background ready";
                StatusText.ToolTip = "Room background decoded from its individual LFL resource.";
                return;
            }
            var objectRecords = _objectDirectory.Decode(bytes);
            var objects = _objectDecoder.Decode(bytes);
            if (token.IsCancellationRequested) return;
            _currentRoom = room;
            _currentRoomResource = resource;
            _lastViewedRoomResource = resource;
            _currentRoomObjectRecords = objectRecords;
            _roomVerbs = _verbReader.ReadAll(bytes, objectRecords);
            _roomObjectNames = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), objectRecords);
            _objects = objects;
            ApplyRoomState(resource, objectRecords, objects, visit: true);
            UpdateRoomNavigationActions(resource);
            RefreshRoomPreview();
            UpdateCurrentRoomCardThumbnail();
            RoomDisplayLayout.Visibility = Visibility.Visible;
            UpdateRoomViewportLayout();
            StatusText.HorizontalAlignment = HorizontalAlignment.Center;
            StatusText.Visibility = Visibility.Collapsed;
            RoomPreview.Visibility = Visibility.Visible;
            RoomMaskToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
            ShowWalkBoxToggle();
            PreviewMessage.Visibility = Visibility.Collapsed;
            UpdateAdvancedMode();
            StatusText.Text = "Scene ready";
            StatusText.ToolTip = $"{room.Width} × {room.Height} · {objectRecords.Count} object records · {objects.Count} images";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested)
            {
                PreviewTitle.Text = "Could not read this room";
                PreviewDescription.Text = ex.Message;
                StatusText.Text = $"{resource.Name} · background unavailable";
            }
        }
    }

    private void ApplyRoomState(GameResource resource, IReadOnlyList<LoomRoomObjectRecord> records,
        IReadOnlyList<RoomObjectImage> images, bool visit)
    {
        _estimatedObjectIds = new HashSet<int>();
        RoomEntryEventButtons.Children.Clear();
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            var snapshot = visit ? _session.VisitRoom(resource) : _session.Snapshot;
            var sceneBatches = snapshot.IsFaithful ? _sceneBatches ?? LoomSceneDrawBatches.Empty : EnsureSceneBatches();
            var initialized = CreateSessionRoomSelection(resource, _currentRoom!, records, images, snapshot,
                _indexedStates, sceneBatches);
            _objectSelection = initialized.Selection;
            _artworkEstimates = initialized.Estimates;
            _estimatedObjectIds = _artworkEstimates.Select(item => item.ObjectId).ToHashSet();
            var run = _session.LastEntryRun;
            var script = _session.LastEntryScript;
            RoomEntryTraceOutput.Text = (script?.Format() ?? "No embedded entry script.") +
                                        Environment.NewLine + "SESSION INTERPRETATION" + Environment.NewLine +
                                        (run?.FormatSummary() ?? "No entry run recorded.") +
                                        Environment.NewLine + "Known global variables: " + snapshot.KnownGlobalVariables.Count +
                                        "; inventory: " + (snapshot.InventoryKnown ? snapshot.Inventory.Count.ToString() : "unknown") +
                                        "; script-started sounds: " +
                                        (snapshot.ActiveSounds.Count == 0 ? "none known" : string.Join(", ", snapshot.ActiveSounds)) +
                                        "; camera x (var[0x002]): " + (_session.CameraX?.ToString() ?? "unknown") +
                                        (_session.CameraEstimated ? " (estimate)" : "");
            // A room entered in the session opens where its camera is, not at the left edge.
            if (visit) _pendingCameraX = _session.CameraX;
            RoomEntryTraceExpander.Header = "Advanced room details";
            RoomEntryTraceExpander.Visibility = Visibility.Visible;
            ShowScriptEvents(RoomEntryEventButtons, _session.LastVisitEvents);
            UpdateRoomMusicActions();
            SessionStatusText.Text = snapshot.IsFaithful
                ? "Exploration state ready."
                : "Exploration state is incomplete. Hover for details.";
            SessionStatusText.ToolTip = snapshot.Uncertainties.Count == 0
                ? "A script may be waiting for the next frame."
                : string.Join(Environment.NewLine, snapshot.Uncertainties.TakeLast(8));
            AdvanceFrameButton.IsEnabled = snapshot.PendingScripts > 0;
            UpdatePlaybackControls();
            ObjectsNote.Text = _estimatedObjectIds.Count == 0
                ? "Click the scene to explore it. Hover to discover objects and exits."
                : $"Some scene details are previews; the game state remains unresolved ({_estimatedObjectIds.Count} details).";
            ShowLastObjectAction(resource.ResourceId);
            UpdateAdvancedMode();
            return;
        }

        ObjectActionPanel.Visibility = Visibility.Collapsed;
        SessionStatusText.Text = "Inspection mode: a click cycles an object's stored images; it is not the game's click and runs no script. Each room starts from the index; changes are local to this view.";
        SessionStatusText.ToolTip = null;
        RoomEntryTraceExpander.Header = "Advanced room details";
        AdvanceFrameButton.IsEnabled = false;
        UpdatePlaybackControls();
        _objectSelection = new RoomObjectSelection(images, _indexedStates, background: _currentRoom);
        _artworkEstimates = [];
        ObjectsNote.Text = images.Count == 0
            ? "This room has no decoded object art yet."
            : "Click the artwork to browse stored scene details.";
        try
        {
            var entryScript = _scriptReader.DecodeRoomEntry(resource);
            if (entryScript is null)
                RoomEntryTraceOutput.Text = "This room has no embedded entry-script pointer.";
            else
            {
                var run = _scriptExecutor.Execute(entryScript, _indexedStates, records);
                ShowScriptEvents(RoomEntryEventButtons, run.Events);
                _objectSelection = new RoomObjectSelection(images, run.ObjectStates,
                    run.ObjectPositions, background: _currentRoom, drawQueue: run.DrawOrder);
                if (run.UnresolvedReason is not null || run.ResumeOffset.HasValue)
                    CloseArtworkGaps(resource, images);
                RoomEntryTraceOutput.Text = entryScript.Format() + Environment.NewLine +
                                            "BOUNDED INTERPRETATION" + Environment.NewLine + run.FormatSummary();
                RoomEntryTraceExpander.Visibility = Visibility.Visible;
                ObjectsNote.Text = run.UnresolvedReason is null && !run.ResumeOffset.HasValue
                    ? $"{records.Count} local object records, {images.Count} stored images. Entry bytecode reached stopObjectCode; the bounded result is shown in the room-entry trace."
                    : $"{records.Count} local object records, {images.Count} stored images. {_estimatedObjectIds.Count} unresolved details use an artwork estimate; see the trace for script limits.";
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            CloseArtworkGaps(resource, images);
            RoomEntryTraceOutput.Text = $"Room entry script could not be interpreted: {ex.Message}";
            RoomEntryTraceExpander.Visibility = Visibility.Visible;
            ObjectsNote.Text = $"{records.Count} local object records, {images.Count} stored images. The entry script is unavailable; {_estimatedObjectIds.Count} details use an artwork estimate.";
        }
        UpdateRoomMusicActions();
        UpdateAdvancedMode();
    }

    /// <summary>
    /// Fills gaps the reached state leaves open: first with script draw batches
    /// (the opening frame of a room loaded by a script is preferred), then with
    /// a seeded random stored image. If nothing is visible at all, empty
    /// positions are shown from script draw batches with the same priorities,
    /// and only positions no batch covers use their first stored image.
    /// Estimates stay labelled. Verb-area images are never used, and
    /// positions whose state reached code set keep it.
    /// </summary>
    private void CloseArtworkGaps(GameResource resource, IReadOnlyList<RoomObjectImage> images,
        IReadOnlyCollection<int>? statesSetByCode = null)
    {
        EnsureSceneBatches();
        _artworkEstimates = FillArtworkGaps(_objectSelection, resource, images, statesSetByCode,
            _sceneBatches ?? LoomSceneDrawBatches.Empty);
        _estimatedObjectIds = _artworkEstimates.Select(item => item.ObjectId).ToHashSet();
    }

    private LoomSceneDrawBatches EnsureSceneBatches()
    {
        if (_sceneBatches is not null) return _sceneBatches;
        try { _sceneBatches = _game is null ? LoomSceneDrawBatches.Empty : LoomSceneDrawBatches.Build(_game); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _sceneBatches = LoomSceneDrawBatches.Empty;
        }
        return _sceneBatches;
    }

    private static (RoomObjectSelection Selection, IReadOnlyList<RoomArtworkEstimate> Estimates) CreateSessionRoomSelection(
        GameResource resource, RoomImage room, IReadOnlyList<LoomRoomObjectRecord> records,
        IReadOnlyList<RoomObjectImage> images, LoomSessionSnapshot snapshot,
        IReadOnlyDictionary<int, byte> indexedStates, LoomSceneDrawBatches sceneBatches)
    {
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates,
            snapshot.ObjectPositions, records, room, indexedStates, snapshot.DrawOrder);
        IReadOnlyList<RoomArtworkEstimate> estimates = snapshot.IsFaithful
            ? Array.Empty<RoomArtworkEstimate>()
            : FillArtworkGaps(selection, resource, images, snapshot.StatesSetByCode, sceneBatches);
        return (selection, estimates);
    }

    private static IReadOnlyList<RoomArtworkEstimate> FillArtworkGaps(RoomObjectSelection selection,
        GameResource resource, IReadOnlyList<RoomObjectImage> images,
        IReadOnlyCollection<int>? statesSetByCode, LoomSceneDrawBatches sceneBatches)
    {
        var roomId = resource.ResourceId ?? 0;
        selection.ExcludeInterfaceImages(sceneBatches.InterfaceImages.Keys);
        var estimates = selection.FillGaps(sceneBatches.ForRoom(roomId), seed: roomId);
        if (!images.Any(selection.IsActive))
            estimates = [.. estimates, .. selection.ShowUnresolvedArtwork(sceneBatches.ForRoom(roomId), seed: roomId, statesSetByCode,
                sceneBatches.EntryStatesForRoom(roomId))];
        return estimates;
    }

    private static BitmapSource CreateRoomCardBitmap(RoomImage room, byte[] pixels, int cameraX)
    {
        var visibleWidth = Math.Min(320, room.Width);
        var left = Math.Clamp(cameraX - visibleWidth / 2, 0, room.Width - visibleWidth);
        var full = CreateBitmap(room.Width, room.Height, pixels, room.Palette);
        var cropped = new CroppedBitmap(full, new Int32Rect(left, 0, visibleWidth, room.Height));
        cropped.Freeze();
        return cropped;
    }

    private void UpdateCurrentRoomCardThumbnail()
    {
        if (_currentRoom is not { } room || _currentRoomResource is not { } resource) return;
        var cameraX = SessionModeToggle.IsChecked == true && _session is not null
            ? _session.CameraX ?? 0
            : _pendingCameraX ?? 0;
        SetRoomCardThumbnail(resource, CreateRoomCardBitmap(room, _objectSelection.Compose(room), cameraX));
    }

    private void SetRoomCardThumbnail(GameResource resource, BitmapSource bitmap)
    {
        if (Equals(_currentRoomResource, resource) && _currentRoom is { } liveRoom)
        {
            var liveCameraX = SessionModeToggle.IsChecked == true && _session is not null
                ? _session.CameraX ?? 0
                : _pendingCameraX ?? 0;
            bitmap = CreateRoomCardBitmap(liveRoom, _objectSelection.Compose(liveRoom), liveCameraX);
        }
        var card = RoomCardList.Children.OfType<Button>().FirstOrDefault(item => Equals(item.Tag, resource));
        if (card is null) return;
        if (card.Content is Image preview) preview.Source = bitmap;
        else card.Content = CreateRoomCardImage(bitmap, resource.Name);
        card.ToolTip = $"Open {resource.Name}";
    }

    private string EstimateSummary()
    {
        var sources = _artworkEstimates.Select(item => item.Reason).Distinct().ToArray();
        return sources.Length == 0 ? "" : $" (from {string.Join("; ", sources.Take(2))}{(sources.Length > 2 ? "; …" : "")})";
    }

    private void SessionMode_Changed(object sender, RoutedEventArgs e)
    {
        StopScenePlayback();
        if (SessionModeToggle.IsChecked != true) _sceneSound.StopAll("the interactive mode was turned off");
        if (_currentRoomResource is not { } room || _currentRoom is null) return;
        ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: SessionModeToggle.IsChecked == true);
        UpdateRoomNavigationActions(room);
        RefreshRoomPreview();
        // The interactive mode keeps the verb screen under the room (VerbScreenShown).
        UpdateVerbStrip();
        UpdateRoomViewportLayout();
    }

    private void ResetSession_Click(object sender, RoutedEventArgs e)
    {
        if (_game is null || _indexedObjectSnapshot is null) return;
        StopScenePlayback();
        _sceneSound.StopAll("the session was reset");
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot);
        if (_currentRoomResource is { } room && _currentRoom is not null)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: SessionModeToggle.IsChecked == true);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview();
        }
        else SessionStatusText.Text = "Session reset. Select a room to visit it.";
    }

    private void AdvanceFrame_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackPaused)
        {
            StepPausedPlayback();
            return;
        }
        StopScenePlayback();
        AdvanceOneFrame();
    }

    private void AdvanceOneFrame()
    {
        if (_session is null || _currentRoomResource is not { } room || _currentRoom is null) return;
        var snapshot = _session.AdvanceFrame();
        // A continued script can reach a room load; the session has already
        // entered that room, so show it as ActivateObjectVerb does.
        if (snapshot.RoomId is int reached && reached != room.ResourceId &&
            _game?.Rooms.FirstOrDefault(item => item.ResourceId == reached) is { } targetRoom)
        {
            StatusText.Text = "You step into another scene.";
            StatusText.ToolTip = $"Room {reached:00} · " + (_session.LastAction?.Evidence ?? "");
            SelectResourceInTree(targetRoom);
            return;
        }
        ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
        UpdateRoomNavigationActions(room);
        RefreshRoomPreview();
    }

    /// <summary>Frames an exit click may continue outside a cutscene before falling back to browsing.</summary>
    /// <remarks>
    /// Delays and actor waits take one session frame; a message lasts its talk
    /// delay. Frames inside a cutscene do not count: the session follows it until
    /// it ends (up to <see cref="LoomExplorationSession.CutsceneFrameLimit"/> frames),
    /// so Room 10 object 295 (script 052) passes through Room 11 and ends in Room 13,
    /// and Room 08's door plays the Elders' trial (script 046, 2,120 frames) to
    /// Room 09. A click that falls short leaves its script pending, and
    /// that load would later fire during an unrelated click.
    /// </remarks>
    private const int ExitContinuationFrames = 32;

    private bool ContinueToReachedRoom()
    {
        if (_session is null || _game is null || _currentRoomResource is not { } room ||
            _session.Snapshot.PendingScripts == 0 || !_session.ContinueUntilRoomChanges(ExitContinuationFrames))
            return false;
        if (_session.Snapshot.RoomId is not int reached ||
            _game.Rooms.FirstOrDefault(item => item.ResourceId == reached) is not { } targetRoom || targetRoom == room)
            return false;
        StatusText.Text = "You step into another scene.";
        StatusText.ToolTip = $"Room {reached:00} · " + (_session.LastAction?.Evidence ?? "");
        SelectResourceInTree(targetRoom);
        return true;
    }

    private void ShowLastObjectAction(int? roomId)
    {
        var action = _session?.LastAction;
        if (action is null || roomId is null ||
            action.SourceRoomId != roomId && action.DestinationRoomId != roomId)
        {
            ObjectActionPanel.Visibility = Visibility.Collapsed;
            return;
        }
        ObjectActionPanel.Visibility = Visibility.Visible;
        ObjectVerbButtons.Children.Clear();
        var actionName = _currentRoomObjectRecords.FirstOrDefault(record => record.Id == action.ObjectId) is { } actionObject &&
                         _roomObjectNames.TryGetValue(actionObject.Id, out var storedName)
            ? storedName : "Something in the scene";
        ObjectActionNote.Text = action.DestinationRoomId is int target
            ? action.IsGameInput
                ? $"{actionName}: a new scene opened. The game walk remains a preview."
                : $"{actionName}: a new scene opened in this state preview."
            : _session!.Snapshot.PendingScripts > 0
                ? $"{actionName}: action tried; the result is still unresolved."
            : $"{actionName}: action tried; no scene change was reached.";
        ObjectActionNote.ToolTip = (action.DestinationRoomId is int destination ? $"Room {destination:00} · " : "") + action.Evidence;
        ObjectActionTraceOutput.Text = action.Evidence + Environment.NewLine +
            string.Join(Environment.NewLine, action.Uncertainties.Select(item => "Unresolved: " + item)) +
            Environment.NewLine + (action.Script?.Format() ?? "Object bytecode unavailable.") +
            Environment.NewLine + (action.Run?.FormatSummary() ?? "No object bytecode was run.");
        ObjectActionTraceExpander.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateAdvancedMode();
    }

    private void ShowObjectVerbChoices(IReadOnlyList<RoomVerbChoice> choices)
    {
        ObjectActionPanel.Visibility = Visibility.Visible;
        ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
        ObjectVerbButtons.Children.Clear();
        var objectName = choices.Count > 0 && _roomObjectNames.TryGetValue(choices[0].Object.Id, out var storedObjectName)
            ? storedObjectName : "This detail";
        ObjectActionNote.Text = choices.Count == 0
            ? "No action is available here yet."
            : $"{objectName} is part of this scene. Choose an action to explore it.";
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            var button = new Button
            {
                Content = $"Interaction {index + 1}",
                ToolTip = $"Object {choice.Object.Id} · {choice.Verb.Label} · code 0x{choice.Verb.ScriptOffset:X4}. " +
                          "This directly runs the object verb; input dispatch and walking are unresolved.",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += (_, _) => ActivateObjectVerb(choice.Object, choice.Verb);
            ObjectVerbButtons.Children.Add(button);
        }
    }

    private bool ActivateObjectVerb(LoomRoomObjectRecord record, LoomObjectVerbEntry verb)
    {
        if (_session is null || _currentRoomResource is not { } sourceRoom || _game is null) return false;
        var action = _session.ActivateObjectVerb(record.Id, verb.VerbId);
        StatusText.Text = action.DestinationRoomId is int destination
            ? $"Entered Room {destination:00}"
            : _session.Snapshot.PendingScripts > 0
                ? "No room load reached; the session has pending scripts."
                : "Interaction finished";
        StatusText.ToolTip = action.Evidence + (action.Uncertainties.Count == 0 ? "" :
            Environment.NewLine + string.Join(Environment.NewLine, action.Uncertainties));
        if (action.DestinationRoomId is int targetId &&
            _game.Rooms.FirstOrDefault(item => item.ResourceId == targetId) is { } targetRoom &&
            targetRoom != sourceRoom)
        {
            SelectResourceInTree(targetRoom);
            return true;
        }
        ApplyRoomState(sourceRoom, _currentRoomObjectRecords, _objects, visit: false);
        UpdateRoomNavigationActions(sourceRoom);
        RefreshRoomPreview();
        return false;
    }

    private void ShowScriptEvents(StackPanel container, IReadOnlyList<LoomScriptEvent> events)
    {
        container.Children.Clear();
        foreach (var scriptEvent in events)
        {
            var target = FindEventTarget(scriptEvent);
            if (target is null)
            {
                container.Children.Add(new TextBlock
                {
                    Text = scriptEvent.Format(),
                    Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 2, 2, 2),
                    FontSize = 11
                });
                continue;
            }

            var action = target.Kind switch
            {
                ResourceKind.Sound => "Open ",
                ResourceKind.Script => "Inspect ",
                ResourceKind.Room => "Open ",
                _ => "Open "
            };
            var certainty = scriptEvent.Certainty switch
            {
                LoomScriptEventCertainty.Reached => "",
                LoomScriptEventCertainty.Conditional => "Possible · ",
                _ => "Potential · "
            };
            var button = new Button
            {
                Content = $"{certainty}{action}{target.Name}",
                ToolTip = scriptEvent.Format(),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 2, 0, 2),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += (_, _) => SelectResourceInTree(target);
            container.Children.Add(button);
        }
    }

    private GameResource? FindEventTarget(LoomScriptEvent scriptEvent)
    {
        if (scriptEvent.TargetKind is not (ResourceKind.Sound or ResourceKind.Script or ResourceKind.Room) ||
            scriptEvent.TargetId is not int resourceId) return null;
        return _game?.Resources.FirstOrDefault(resource =>
            resource.Kind == scriptEvent.TargetKind && resource.ResourceId == resourceId);
    }

    private void StartRelationshipIndexBuild(GameLibrary game)
    {
        var generation = Interlocked.Increment(ref _relationshipBuildGeneration);
        if (game.Id != GameId.Loom || game.Version != ScummVersion.V3)
        {
            _relationshipIndex = LoomRelationshipIndex.Empty;
            UpdateConnectionsButton();
            return;
        }

        UpdateConnectionsButton();
        _ = BuildRelationshipIndexAsync(game, generation);
    }

    private async Task BuildRelationshipIndexAsync(GameLibrary game, int generation)
    {
        try
        {
            var index = await Task.Run(() => _relationshipIndexer.Build(game));
            if (generation != Volatile.Read(ref _relationshipBuildGeneration) || !ReferenceEquals(_game, game)) return;
            _relationshipIndex = index;
            UpdateConnectionsButton();
        }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _relationshipBuildGeneration) || !ReferenceEquals(_game, game)) return;
            _relationshipIndex = null;
            ConnectionsButton.Content = "Explore links unavailable";
            ConnectionsButton.ToolTip = exception.Message;
            ConnectionsButton.IsEnabled = false;
        }
    }

    private void UpdateConnectionsButton()
    {
        if (!IsAdvancedMode || _selectedResource is null || _game is not { Id: GameId.Loom, Version: ScummVersion.V3 })
        {
            ConnectionsButton.Visibility = Visibility.Collapsed;
            return;
        }

        ConnectionsButton.Visibility = Visibility.Visible;
        ConnectionsButton.ToolTip = null;
        if (_relationshipIndex is null)
        {
            ConnectionsButton.Content = "Finding links…";
            ConnectionsButton.IsEnabled = false;
            return;
        }

        var relationships = _relationshipIndex.ForResource(_selectedResource);
        var unresolved = relationships.Count(item => item.Status == LoomRelationshipStatus.Unresolved);
        ConnectionsButton.Content = "Explore links";
        ConnectionsButton.ToolTip = unresolved == 0
            ? $"{relationships.Count} source relationships"
            : $"{relationships.Count} source relationships · {unresolved} unresolved";
        ConnectionsButton.IsEnabled = true;
    }

    private void ConnectionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedResource is not GameResource selected) return;
        if (_relationshipIndex is null)
        {
            MessageBox.Show(this, "The relationship index is still being built. Try again in a moment.",
                "Connections", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var relationships = _relationshipIndex.ForResource(selected);
        var window = new Window
        {
            Title = $"Connections · {selected.Name}",
            Owner = this,
            Width = 620,
            Height = 620,
            MinWidth = 480,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(16, 24, 33)),
            Foreground = Brushes.Beige
        };

        var layout = new DockPanel { Margin = new Thickness(18) };
        var note = new TextBlock
        {
            Text = "Links come from the parsed index, room records, or recognized script instructions. " +
                   "Script links are static candidates; conditions and script order are not executed. Dynamic targets and decode issues are marked unresolved.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(note, Dock.Top);
        layout.Children.Add(note);

        var entries = new StackPanel();
        if (relationships.Count == 0)
        {
            entries.Children.Add(new TextBlock
            {
                Text = "No relationships were established for this resource from the data currently parsed.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)),
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        foreach (var relationship in relationships)
        {
            var isOutgoing = relationship.SourceResource == selected;
            var otherResource = isOutgoing ? relationship.TargetResource : relationship.SourceResource;
            var heading = $"{relationship.SourceResource.Name}  →  {relationship.Relationship}  →  {relationship.TargetLabel}";
            var content = new StackPanel { Margin = new Thickness(2) };
            content.Children.Add(new TextBlock
            {
                Text = heading,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 32))
            });
            content.Children.Add(new TextBlock
            {
                Text = relationship.Evidence,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(57, 67, 74)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            if (relationship.Status == LoomRelationshipStatus.Unresolved)
                content.Children.Add(new TextBlock
                {
                    Text = "Unresolved",
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 75, 0)),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            else if (relationship.Status == LoomRelationshipStatus.StaticCandidate)
                content.Children.Add(new TextBlock
                {
                    Text = "Static script reference · execution unverified",
                    Foreground = new SolidColorBrush(Color.FromRgb(91, 81, 115)),
                    Margin = new Thickness(0, 4, 0, 0)
                });

            FrameworkElement entry;
            if (otherResource is GameResource destination)
            {
                var button = new Button
                {
                    Content = content,
                    Tag = destination,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10, 8, 10, 8),
                    Background = new SolidColorBrush(Color.FromRgb(244, 241, 233)),
                    Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 32)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(183, 176, 162))
                };
                button.Click += (_, _) =>
                {
                    window.Close();
                    SelectResourceInTree(destination);
                };
                entry = button;
            }
            else
            {
                entry = new Border
                {
                    Child = content,
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10, 8, 10, 8),
                    Background = new SolidColorBrush(Color.FromRgb(244, 241, 233)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(183, 176, 162)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };
            }
            entries.Children.Add(entry);
        }

        layout.Children.Add(new ScrollViewer
        {
            Content = entries,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        window.Content = layout;
        window.ShowDialog();
    }

    private void SelectResourceInTree(GameResource resource)
    {
        UpdateRoomCardSelection(resource);
        foreach (var group in ResourceTree.Items.OfType<TreeViewItem>())
        {
            foreach (var item in group.Items.OfType<TreeViewItem>())
            {
                if (item.Tag is not GameResource candidate || candidate != resource) continue;
                group.IsExpanded = true;
                item.IsSelected = true;
                item.BringIntoView();
                return;
            }
        }
    }

    private void UpdateRoomCardSelection(GameResource? resource)
    {
        foreach (var card in RoomCardList.Children.OfType<Button>())
        {
            var selected = Equals(card.Tag, resource);
            card.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(183, 132, 66))
                : new SolidColorBrush(Color.FromRgb(64, 82, 96));
            card.BorderThickness = new Thickness(2);
        }
    }

    private void ShowCostume(GameResource resource)
    {
        _previewCancellation?.Cancel();
        _currentRoom = null;
        _objects = [];
        _selectedObject = null;
        ClearObjectList();
        CostumePoseGallery.Children.Clear();
        CostumePoseScrollViewer.ScrollToTop();
        RoomDisplayLayout.Visibility = Visibility.Collapsed;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        WalkBoxToggle.Visibility = Visibility.Collapsed;
        WalkBoxOverlay.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        PreviewMessage.Visibility = Visibility.Collapsed;
        CostumePanel.Visibility = Visibility.Visible;
        RoomObjectPanel.Visibility = Visibility.Collapsed;
        Grid.SetColumnSpan(ContentPanel, 2);

        PageTitle.Text = resource.Name;
        PageSubtitle.Text = "Static pose gallery · all stored decodable poses at the same pixel scale";
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        CostumeMessage.Text = "Reading costume…";
        CostumeMessage.Visibility = Visibility.Visible;
        StatusText.Text = $"{resource.Name} · reading costume";

        try
        {
            if (_game is not { Id: GameId.Loom, Version: ScummVersion.V3 } || resource.Offset is not int resourceOffset)
                throw new NotSupportedException("This costume has no supported DOS LOOM resource offset.");

            var container = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
            var summary = _costumeReader.Decode(container, resourceOffset);
            var availableCount = 0;
            var damagedPoseCount = 0;
            foreach (var slot in summary.AnimationSlots.Where(slot => slot.HasData))
            {
                try
                {
                    var pose = _costumeRenderer.DecodePose(container, resourceOffset, slot.Index);
                    if (pose is null) continue;
                    var poseCard = new StackPanel
                    {
                        Margin = new Thickness(16, 14, 16, 22),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    poseCard.Children.Add(new TextBlock
                    {
                        Text = $"POSE {slot.Index:00}",
                        Foreground = new SolidColorBrush(Color.FromRgb(183, 132, 66)),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                    var poseImage = new Image
                    {
                        Source = CreateCostumeBitmap(pose.Width, pose.Height, pose.Pixels.ToArray(), pose.Palette),
                        Width = pose.Width,
                        Height = pose.Height,
                        Stretch = Stretch.Uniform,
                        SnapsToDevicePixels = true,
                        ToolTip = $"Pose {slot.Index:00} · {pose.Width} × {pose.Height} pixels"
                    };
                    RenderOptions.SetBitmapScalingMode(poseImage, BitmapScalingMode.NearestNeighbor);
                    poseCard.Children.Add(poseImage);
                    CostumePoseGallery.Children.Add(poseCard);
                    availableCount++;
                }
                catch (InvalidDataException)
                {
                    damagedPoseCount++;
                }
            }
            if (availableCount > 0)
            {
                CostumeMessage.Visibility = Visibility.Collapsed;
                CostumePoseScrollViewer.Dispatcher.BeginInvoke(ApplyCostumePoseScale, DispatcherPriority.Loaded);
                StatusText.Text = $"{resource.Name} · {availableCount} stored poses";
            }
            else
            {
                CostumeMessage.Text = summary.AnimationSlots.Any(slot => slot.HasData) || damagedPoseCount > 0
                    ? "This costume's image data is incomplete, so no picture could be drawn."
                    : "No stored pose data is available for this costume.";
                StatusText.Text = $"{resource.Name} · no picture available";
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            CostumeMessage.Text = CostumeErrorMessage(ex);
            CostumeMessage.Visibility = Visibility.Visible;
            StatusText.Text = $"{resource.Name} · picture unavailable";
        }
    }

    private static string CostumeErrorMessage(Exception exception)
    {
        if (exception is NotSupportedException)
            return "This costume image format is not supported yet.";
        if (exception is InvalidDataException && exception.Message.Contains("invalid format byte", StringComparison.OrdinalIgnoreCase))
            return "This costume entry has an invalid format byte. No image is available.";
        if (exception is InvalidDataException && exception.Message.Contains("offset is outside", StringComparison.OrdinalIgnoreCase))
            return "This costume entry points past the end of its file. No image is available.";
        if (exception is InvalidDataException && exception.Message.Contains("length is invalid", StringComparison.OrdinalIgnoreCase))
            return "The costume data in this file is incomplete. No image is available.";
        return "The costume image data is incomplete or unreadable. No image is available.";
    }

    private void NormalRoomWidthToggle_Changed(object sender, RoutedEventArgs e)
    {
        StopRoomPan();
        UpdateRoomViewportLayout();
    }

    private void CostumePoseScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyCostumePoseScale();

    private void ApplyCostumePoseScale()
    {
        var scale = Math.Clamp(CostumePoseScrollViewer.ViewportWidth / 250.0, 2, 4);
        foreach (var card in CostumePoseGallery.Children.OfType<StackPanel>())
        {
            if (card.Children.OfType<Image>().FirstOrDefault() is not { Source: BitmapSource bitmap } image) continue;
            image.Width = bitmap.PixelWidth * scale;
            image.Height = bitmap.PixelHeight * scale;
        }
    }

    private void RoomDisplayLayout_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateRoomViewportLayout();

    private void RoomViewportHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateRoomViewportLayout();

    private void RoomViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRoomScrollButtons();
    }

    private void RoomViewport_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // A camera kept while the view could not scroll is applied once it can.
        if (_pendingCameraX is not null && (e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0) && RoomViewport.ScrollableWidth > 0.5)
            ApplyPendingCamera();
        UpdateRoomScrollButtons();
    }

    private double _roomDisplayScale = 1;
    private readonly Stopwatch _roomPanStopwatch = new();
    private bool _roomPanActive;
    private int _roomPanDirection;
    private double _roomPanMaximumStep;
    private Key? _roomPanKey;
    private Button? _roomPanMouseButton;
    private bool _roomPanMouseGesture;

    /// <summary>
    /// The room screen's height in the game. <c>redrawBGStrip</c> draws the main virtual screen's rows of the room
    /// image. ScummVM's default <c>initScreens(16, 144)</c> gives it 128 rows (versions 1–6 outside the C64/NES
    /// ports); LOOM's boot (script 001 +0x0000, <c>roomOps setScreen 0, 144</c>) calls <c>initScreens(0, 144)</c>,
    /// so LOOM shows all 144 rows of its rooms from screen row 0. Indy3 keeps the default (not checked).
    /// </summary>
    private int RoomScreenRows => _game?.Id == GameId.Loom ? LoomVerbArea.Top - LoomMessageRenderer.RoomScreenTop : 128;

    private int ShownRoomRows(RoomImage room) => Math.Min(room.Height, RoomScreenRows);

    private void UpdateRoomViewportLayout()
    {
        if (_currentRoom is not { } room || RoomViewport is null) return;
        var normalSize = NormalRoomWidthToggle.IsChecked == true;
        var availableWidth = Math.Max(1, RoomViewportHost.ActualWidth);
        var availableHeight = Math.Max(1, RoomViewportHost.ActualHeight);
        var visibleSourceWidth = normalSize ? Math.Min(320, room.Width) : room.Width;
        // The pan row keeps its height in the 320-pixel view even where the room fits or a cutscene hides the
        // arrows, so the room keeps one size from room to room (UpdateRoomScrollButtons shows or hides them).
        var usableHeight = Math.Max(1, availableHeight);
        if (normalSize)
            usableHeight = Math.Max(1, usableHeight - RoomScrollButtonsPanel.Height - RoomScrollButtonsPanel.Margin.Top - RoomScrollButtonsPanel.Margin.Bottom);
        // The optional verb strip shares the height: 56 screen rows at the room's scale, and its note.
        var verbStrip = VerbScreenShown();
        if (verbStrip) usableHeight = Math.Max(1, usableHeight - VerbStripNoteHeight);
        var shownRows = ShownRoomRows(room);
        var oneScreen = OneGameScreen();
        _roomDisplayScale = Math.Min(availableWidth / visibleSourceWidth,
            usableHeight / (shownRows + (verbStrip ? LoomVerbArea.Height : 0)));
        _roomDisplayScale = Math.Max(0.1, _roomDisplayScale);
        RoomViewport.HorizontalScrollBarVisibility = normalSize
            ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        RoomViewport.VerticalAlignment = VerticalAlignment.Center;

        RoomViewport.Width = visibleSourceWidth * _roomDisplayScale;
        RoomSceneSurface.Width = room.Width * _roomDisplayScale;
        RoomSceneSurface.Height = room.Height * _roomDisplayScale;
        // The viewport shows the rows the game shows; the surface keeps the whole image, so every overlay keeps
        // the room's coordinates, and the viewport (no vertical scrolling) clips the rest.
        RoomViewport.Height = shownRows * _roomDisplayScale;
        VerbStripImage.Width = LoomVerbArea.Width * _roomDisplayScale;
        VerbStripImage.Height = LoomVerbArea.Height * _roomDisplayScale;
        VerbMessageOverlay.Width = VerbStripImage.Width;
        VerbMessageOverlay.Height = VerbStripImage.Height;
        // In the interactive LOOM mode the room and verb screens read as the game's one 200-row screen: no gap or rule.
        VerbStripPanel.Margin = new Thickness(0, oneScreen ? 0 : 2, 0, 0);
        VerbStripBorder.BorderThickness = new Thickness(0, oneScreen ? 0 : 1, 0, 0);
        VerbStripNote.MaxWidth = Math.Max(160, RoomViewport.Width);
        RoomViewport.Dispatcher.BeginInvoke(() =>
        {
            ApplyPendingCamera();
            UpdateRoomScrollButtons();
        }, DispatcherPriority.Loaded);
        RefreshWalkBoxOverlay();
        RefreshRoomNavigationOverlay();
        ShowHotspotHighlight(_highlightedHotspot);
    }

    // The session's camera x for the room just entered, applied once the view is laid out.
    private int? _pendingCameraX;

    /// <summary>
    /// In the 320-pixel view, scrolls so the camera's x (<c>var[0x002]</c>, the
    /// centre of the game's screen) is in the middle, as the game would show it.
    /// </summary>
    private void ApplyPendingCamera()
    {
        if (_pendingCameraX is not int cameraX || _currentRoom is null) return;
        if (NormalRoomWidthToggle.IsChecked != true)
        {
            _pendingCameraX = null;
            StopCameraPan();
            return;
        }
        RoomViewport.UpdateLayout();
        // The first room opened is not always laid out yet: keep the camera until
        // the view can scroll (RoomViewport_ScrollChanged applies it then).
        if (_currentRoom.Width > 320 && RoomViewport.ScrollableWidth < 0.5) return;
        _pendingCameraX = null;
        StopCameraPan();
        var left = Math.Clamp(cameraX - Math.Min(320, _currentRoom.Width) / 2.0, 0,
            RoomViewport.ScrollableWidth / _roomDisplayScale);
        if (CameraPanToggle.IsChecked == true && left > 0)
        {
            // Presentation only: slide from the left edge to the camera.
            RoomViewport.ScrollToHorizontalOffset(0);
            _cameraPanTargetSourceX = left;
            _cameraPanTimer.Start();
            return;
        }
        RoomViewport.ScrollToHorizontalOffset(left * _roomDisplayScale);
    }

    // The optional entry pan: ScummVM moveCamera's 8-pixel step, one per tick.
    private readonly DispatcherTimer _cameraPanTimer = new() { Interval = TimeSpan.FromMilliseconds(75) };
    private double _cameraPanTargetSourceX;

    private void CameraPanTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentRoom is null || NormalRoomWidthToggle.IsChecked != true)
        {
            StopCameraPan();
            return;
        }
        var target = _cameraPanTargetSourceX * _roomDisplayScale;
        var next = Math.Min(target, RoomViewport.HorizontalOffset + 8 * _roomDisplayScale);
        RoomViewport.ScrollToHorizontalOffset(next);
        // ScrollViewer applies the offset on its next layout pass; make it now.
        RoomViewport.UpdateLayout();
        UpdateRoomScrollButtons();
        if (next >= target - 0.01) StopCameraPan();
    }

    private void StopCameraPan() => _cameraPanTimer.Stop();

    private void UpdateRoomScrollButtons()
    {
        var normalSize = NormalRoomWidthToggle.IsChecked == true;
        var canScroll = normalSize && RoomViewport.ScrollableWidth > 0.5;
        // No arrows while the player has no control (a cutscene or the boot): the game offers no scrolling then.
        RoomScrollButtonsPanel.Visibility = canScroll && !SessionInScene ? Visibility.Visible
            : normalSize ? Visibility.Hidden : Visibility.Collapsed;
        ScrollRoomLeftButton.IsEnabled = canScroll && RoomViewport.HorizontalOffset > 0.5;
        ScrollRoomRightButton.IsEnabled = canScroll && RoomViewport.HorizontalOffset < RoomViewport.ScrollableWidth - 0.5;
    }

    internal static double GetRoomPanStep(TimeSpan elapsed, double maximumStep)
    {
        const double initialStep = 2;
        const double accelerationSeconds = 1.75;
        var progress = Math.Clamp(elapsed.TotalSeconds / accelerationSeconds, 0, 1);
        return initialStep + (Math.Max(initialStep, maximumStep) - initialStep) * progress;
    }

    private void PanButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button) return;
        var direction = ReferenceEquals(button, ScrollRoomLeftButton) ? -1 : 1;
        BeginRoomPan(direction, maximumStep: 160, button: button);
        _roomPanMouseGesture = true;
        e.Handled = true;
    }

    private void PanButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(sender, _roomPanMouseButton)) StopRoomPan();
        _roomPanMouseGesture = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _roomPanMouseGesture = false));
        e.Handled = true;
    }

    private void PanButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!ReferenceEquals(sender, _roomPanMouseButton)) return;
        StopRoomPan();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _roomPanMouseGesture = false));
    }

    private void PanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_roomPanMouseGesture)
        {
            _roomPanMouseGesture = false;
            return;
        }
        var direction = ReferenceEquals(sender, ScrollRoomLeftButton) ? -1 : 1;
        PanRoomBySourcePixels(direction * 2);
    }

    private void BeginRoomPan(int direction, double maximumStep, Button? button = null, Key? key = null)
    {
        StopRoomPan();
        StopCameraPan();
        if (NormalRoomWidthToggle.IsChecked != true || RoomViewport.ScrollableWidth <= 0.5 ||
            (direction < 0 && RoomViewport.HorizontalOffset <= 0.5) ||
            (direction > 0 && RoomViewport.HorizontalOffset >= RoomViewport.ScrollableWidth - 0.5)) return;

        RoomViewport.Focus();
        _roomPanActive = true;
        _roomPanDirection = Math.Sign(direction);
        _roomPanMaximumStep = Math.Max(2, maximumStep);
        _roomPanMouseButton = button;
        _roomPanKey = key;
        _roomPanStopwatch.Restart();
        if (button is not null && !button.CaptureMouse())
        {
            StopRoomPan();
            return;
        }
        PanRoomBySourcePixels(_roomPanDirection * 2);
        if (!_roomPanActive) return;
        _roomPanTimer.Start();
    }

    private void RoomPanTimer_Tick(object? sender, EventArgs e)
    {
        if (!_roomPanActive || _currentRoom is null || NormalRoomWidthToggle.IsChecked != true)
        {
            StopRoomPan();
            return;
        }
        PanRoomBySourcePixels(_roomPanDirection * GetRoomPanStep(_roomPanStopwatch.Elapsed, _roomPanMaximumStep));
    }

    private void PanRoomBySourcePixels(double sourcePixels)
    {
        var previous = RoomViewport.HorizontalOffset;
        var requested = previous + sourcePixels * _roomDisplayScale;
        var target = Math.Clamp(requested, 0, RoomViewport.ScrollableWidth);
        RoomViewport.ScrollToHorizontalOffset(target);
        UpdateRoomScrollButtons();
        if (Math.Abs(target - previous) < 0.01) StopRoomPan();
    }

    private void StopRoomPan()
    {
        _roomPanTimer.Stop();
        _roomPanStopwatch.Reset();
        _roomPanActive = false;
        _roomPanKey = null;
        _roomPanMouseGesture = false;
        var capturedButton = _roomPanMouseButton;
        _roomPanMouseButton = null;
        if (capturedButton?.IsMouseCaptured == true) capturedButton.ReleaseMouseCapture();
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_roomPanKey != e.Key) return;
        StopRoomPan();
        e.Handled = true;
    }

    // "Keys as game input": only in interactive mode, and never while typing in a text box.
    private bool GameKeysActive => GameKeysToggle.IsChecked == true && SessionModeToggle.IsChecked == true &&
                                   _session is not null && _currentRoomResource is not null && _game?.Id == GameId.Loom &&
                                   Keyboard.FocusedElement is not (TextBox or ComboBox);

    /// <summary>
    /// The key code ScummVM's <c>processKeyboard</c> gives scripts for a key that is
    /// not text: Escape 0x1B, F1–F9 as 315–323, Ctrl+A–Z as 1–26; null otherwise.
    /// </summary>
    private static int? GameKeyCode(Key key, ModifierKeys modifiers) => key switch
    {
        Key.Escape => 0x1B,
        // Space here, not as text, so a focused check box does not toggle too.
        Key.Space when modifiers == ModifierKeys.None => 0x20,
        >= Key.F1 and <= Key.F9 => 315 + (key - Key.F1),
        >= Key.A and <= Key.Z when modifiers == ModifierKeys.Control => 1 + (key - Key.A),
        _ => null
    };

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Playback controls in the explore mode, with or without "Keys as game input": Escape skips a cutscene
        // (abortCutscene, as the game's key does) and space pauses or plays the scene (the game's pause key).
        if (GameClickActive && Keyboard.FocusedElement is not (TextBox or ComboBox) && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Escape && SessionInScene)
            {
                e.Handled = true;
                SkipCutscene();
                return;
            }
            if (e.Key == Key.Space && _playback is not null)
            {
                e.Handled = true;
                PlaybackPause_Click(this, new RoutedEventArgs());
                return;
            }
        }
        if (!GameKeysActive) return;
        // F10 arrives as a system key.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (GameKeyCode(key, Keyboard.Modifiers) is not int code) return;
        e.Handled = true;
        SendKeyToGame(code);
    }

    private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Printable characters go as their ASCII code, as ScummVM passes lastKeyHit.ascii.
        if (!GameKeysActive || e.Text is not { Length: 1 } text || text[0] is < ' ' or > '~') return;
        e.Handled = true;
        SendKeyToGame(text[0]);
    }

    /// <summary>
    /// "Keys as game input": sends the key to the session as the game's own input
    /// path would receive it (<see cref="LoomExplorationSession.PressKey"/>).
    /// </summary>
    private void SendKeyToGame(int key) =>
        SendInputToGame(() => _session!.PressKey(key), $"Key {LoomExplorationSession.DescribeKey(key)}");

    private void RoomViewport_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_roomPanKey is not null) StopRoomPan();
    }

    private void RoomViewport_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!RoomViewport.IsKeyboardFocusWithin || NormalRoomWidthToggle.IsChecked != true) return;
        var direction = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (direction == 0) return;
        if (!_roomPanActive || _roomPanKey != e.Key)
            BeginRoomPan(direction, maximumStep: 64, key: e.Key);
        e.Handled = true;
    }

    private async Task ShowAudio(GameResource resource, bool preserveFormat = false, bool autoPlay = false)
    {
        StopAudio();
        _audioSurface = AudioSurface.Resource;
        CostumePanel.Visibility = Visibility.Collapsed;
        RoomDisplayLayout.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        RoomObjectPanel.Visibility = Visibility.Collapsed;
        Grid.SetColumnSpan(ContentPanel, 2);
        _selectedSound = resource;
        var hasAdlibMusic = _adlibDecoder.HasMusic(resource);
        _changingAudioFormat = true;
        AudioFormatCombo.Visibility = hasAdlibMusic ? Visibility.Visible : Visibility.Collapsed;
        if (!preserveFormat || !hasAdlibMusic)
            AudioFormatCombo.SelectedIndex = hasAdlibMusic ? 1 : 0;
        _changingAudioFormat = false;
        var adlib = hasAdlibMusic && AudioFormatCombo.SelectedIndex == 1;
        var format = adlib ? "AdLib FM" : "PC speaker";
        AdlibGainPanel.Visibility = adlib ? Visibility.Visible : Visibility.Collapsed;
        _previewCancellation?.Cancel();
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        WalkBoxToggle.Visibility = Visibility.Collapsed;
        WalkBoxOverlay.Visibility = Visibility.Collapsed;
        PreviewMessage.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Visible;
        ClearObjectList();
        ObjectsNote.Text = "Choose a room to inspect its objects.";
        ObjectAssetPreview.Source = null;
        ObjectAssetPreview.Visibility = Visibility.Collapsed;
        ObjectAssetDescription.Text = "";
        PageTitle.Text = resource.Name;
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        PageSubtitle.Text = "Play this sound on demand.";
        PageSubtitle.ToolTip = resource.FilePath;
        AudioTitle.Text = resource.Name;
        AudioDescription.Text = $"Preparing {format} audio…";
        AudioTime.Text = "0:00 / 0:00";
        PlayButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        AdlibGainSlider.IsEnabled = false;
        StatusText.Text = $"{resource.Name} · reading sound";
        _audioCancellation = new CancellationTokenSource();
        var token = _audioCancellation.Token;
        try
        {
            var wave = await RenderAudioWaveAsync(resource, adlib, token);
            if (token.IsCancellationRequested) return;
            _adlibBaseWave = adlib ? wave : null;
            if (adlib) wave = LoomAdlibAudio.ApplyOutputGain(wave, AdlibGainSlider.Value);
            var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(path, wave, token);
            if (token.IsCancellationRequested)
            {
                File.Delete(path);
                return;
            }
            _audioFile = path;
            _playWhenReady = autoPlay;
            _audioPlayer.Open(new Uri(path));
            _audioReady = true;
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            AdlibGainSlider.IsEnabled = adlib;
            AudioDescription.Text = wave.Length >= 44 + (adlib ? 49716 * 4 : 22050 * 2) * 179
                ? $"DOS {format} rendition · preview limited to three minutes"
                : $"DOS {format} rendition · select Play to listen";
            StatusText.Text = $"{resource.Name} · {format} track ready";
            UpdateAudioTime();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            if (token.IsCancellationRequested) return;
            AudioDescription.Text = ex.Message;
            StatusText.Text = $"{resource.Name} · playback unavailable";
        }
    }

    private Task<byte[]> RenderAudioWaveAsync(GameResource resource, bool adlib,
        CancellationToken token) => Task.Run(() => adlib
            ? _adlibDecoder.RenderWave(resource, token)
            : _audioDecoder.RenderWave(resource, token), token);

    private async void RoomCuePlay_Click(object sender, RoutedEventArgs e) => await PlayRoomCueAsync();

    private async Task PlayRoomCueAsync()
    {
        if (_roomSelectedCue is not { } sound) return;
        if (_audioSurface == AudioSurface.Room && _audioReady && _roomRenderedCue == sound)
        {
            _audioPlayer.Play();
            _audioTimer.Start();
            RoomCueStatus.Text = "Playing";
            return;
        }
        StopAudio();
        _audioSurface = AudioSurface.Room;
        _roomRenderedCue = sound;
        RoomCueStatus.Text = "Preparing audio…";
        RoomCuePlayButton.IsEnabled = false;
        RoomCuePauseButton.IsEnabled = false;
        RoomCueStopButton.IsEnabled = true;
        RoomCueTime.Text = "0:00 / 0:00";
        _audioCancellation = new CancellationTokenSource();
        var token = _audioCancellation.Token;
        try
        {
            var adlib = _adlibDecoder.HasMusic(sound);
            var wave = await RenderAudioWaveAsync(sound, adlib, token);
            if (token.IsCancellationRequested) return;
            if (adlib) wave = LoomAdlibAudio.ApplyOutputGain(wave, 6);
            var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(path, wave, token);
            if (token.IsCancellationRequested)
            {
                File.Delete(path);
                return;
            }
            _audioFile = path;
            _playWhenReady = true;
            _audioPlayer.Open(new Uri(path));
            _audioReady = true;
            RoomCuePlayButton.IsEnabled = true;
            RoomCuePauseButton.IsEnabled = true;
            RoomCueStatus.Text = "Ready to play";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            if (token.IsCancellationRequested) return;
            RoomCueStatus.Text = $"Playback unavailable: {ex.Message}";
            RoomCuePlayButton.ToolTip = RoomCueStatus.Text;
            RoomCuePlayButton.IsEnabled = true;
            RoomCuePauseButton.IsEnabled = false;
            RoomCueStopButton.IsEnabled = false;
        }
    }

    private void RoomCuePause_Click(object sender, RoutedEventArgs e)
    {
        if (_audioSurface != AudioSurface.Room || !_audioReady) return;
        _audioPlayer.Pause();
        _audioTimer.Stop();
        UpdateAudioTime();
        RoomCueStatus.Text = "Paused";
    }

    private void RoomCueStop_Click(object sender, RoutedEventArgs e)
    {
        if (_audioSurface != AudioSurface.Room) return;
        if (_audioReady) StopPlayback();
        else
        {
            StopAudio();
            RoomCuePlayButton.IsEnabled = _roomSelectedCue is not null;
            RoomCuePauseButton.IsEnabled = false;
            RoomCueStopButton.IsEnabled = false;
            RoomCueStatus.Text = "Stopped";
        }
    }

    private async void AudioFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingAudioFormat || _selectedSound is null || !IsLoaded ||
            _audioSurface != AudioSurface.Resource) return;
        await ShowAudio(_selectedSound, preserveFormat: true);
    }

    private void AdlibGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AdlibGainValue is not null) AdlibGainValue.Text = $"+{AdlibGainSlider.Value:F1} dB";
        _adlibGainRequest++;
        _adlibGainDebounceTimer.Stop();
        if (_adlibBaseWave is null || !_audioReady || _audioSurface != AudioSurface.Resource) return;
        _adlibGainDebounceTimer.Start();
    }

    private async Task ApplyAdlibGainAsync()
    {
        var sourceWave = _adlibBaseWave;
        if (sourceWave is null || !_audioReady || _audioSurface != AudioSurface.Resource) return;

        var request = _adlibGainRequest;
        var gainDb = AdlibGainSlider.Value;
        try
        {
            var wave = await Task.Run(() => LoomAdlibAudio.ApplyOutputGain(sourceWave, gainDb));
            if (request != _adlibGainRequest || !ReferenceEquals(sourceWave, _adlibBaseWave)) return;

            var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(path, wave);
            if (request != _adlibGainRequest || !ReferenceEquals(sourceWave, _adlibBaseWave))
            {
                File.Delete(path);
                return;
            }

            var oldPath = _audioFile;
            var position = _audioPlayer.Position;
            var resumePlaying = _audioTimer.IsEnabled;
            _audioTimer.Stop();
            _audioReady = false;
            PlayButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            AdlibGainSlider.IsEnabled = false;
            _audioPlayer.Close();
            if (oldPath is not null)
            {
                try { File.Delete(oldPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            _audioFile = path;
            _audioPositionAfterGainChange = position;
            _resumeAudioAfterGainChange = resumePlaying;
            _restoreAudioAfterGainChange = true;
            _audioPlayer.Open(new Uri(path));
            StatusText.Text = $"{AudioTitle.Text} · applying AdLib level";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AudioDescription.Text = $"Could not apply AdLib level: {ex.Message}";
        }
    }

    private void AudioPlayer_MediaOpened(object? sender, EventArgs e)
    {
        if (_restoreAudioAfterGainChange)
        {
            _restoreAudioAfterGainChange = false;
            _audioReady = true;
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            AdlibGainSlider.IsEnabled = true;
            _audioPlayer.Position = _audioPositionAfterGainChange;
            if (_resumeAudioAfterGainChange)
            {
                _audioPlayer.Play();
                _audioTimer.Start();
                StatusText.Text = $"{AudioTitle.Text} · playing";
            }
            else
            {
                _audioPlayer.Pause();
                StatusText.Text = $"{AudioTitle.Text} · paused";
            }
        }
        else if (_playWhenReady)
        {
            _playWhenReady = false;
            _audioPlayer.Play();
            _audioTimer.Start();
            if (_audioSurface == AudioSurface.Room) RoomCueStatus.Text = "Playing";
            else StatusText.Text = $"{AudioTitle.Text} · playing";
        }
        UpdateAudioTime();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_audioReady) return;
        _audioPlayer.Play();
        _audioTimer.Start();
        StatusText.Text = $"{AudioTitle.Text} · playing";
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_audioReady) return;
        _audioPlayer.Pause();
        _audioTimer.Stop();
        UpdateAudioTime();
        StatusText.Text = $"{AudioTitle.Text} · paused";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopPlayback();

    private void StopPlayback()
    {
        _audioTimer.Stop();
        _audioPlayer.Stop();
        if (_audioReady)
        {
            if (_audioSurface == AudioSurface.Room) RoomCueStatus.Text = "Stopped";
            else StatusText.Text = $"{AudioTitle.Text} · stopped";
        }
        UpdateAudioTime();
    }

    private void UpdateAudioTime()
    {
        var current = _audioPlayer.Position;
        var total = _audioPlayer.NaturalDuration.HasTimeSpan ? _audioPlayer.NaturalDuration.TimeSpan : TimeSpan.Zero;
        AudioTime.Text = $"{current:m\\:ss} / {total:m\\:ss}";
        RoomCueTime.Text = AudioTime.Text;
    }

    private void StopAudio()
    {
        _playWhenReady = false;
        _adlibGainDebounceTimer.Stop();
        _adlibGainRequest++;
        _adlibBaseWave = null;
        _restoreAudioAfterGainChange = false;
        _resumeAudioAfterGainChange = false;
        _audioCancellation?.Cancel();
        _audioTimer.Stop();
        _audioPlayer.Close();
        _audioReady = false;
        _audioSurface = AudioSurface.None;
        _roomRenderedCue = null;
        RoomCuePauseButton.IsEnabled = false;
        RoomCueStopButton.IsEnabled = false;
        RoomCuePlayButton.IsEnabled = _roomSelectedCue is not null;
        if (_audioFile is null) return;
        try { File.Delete(_audioFile); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        _audioFile = null;
    }

    private void RefreshRoomPreview()
    {
        if (_currentRoom is null) return;
        var room = _currentRoom;
        // SCUMM repaints active objects in reverse directory order, then
        // paints queued drawObject calls on top in call order.
        var pixels = _objectSelection.Compose(room);
        var depthMask = room.DepthMask;
        RoomMaskToggle.IsEnabled = depthMask is not null;
        RoomMaskToggle.ToolTip = depthMask is null
            ? "No room depth mask was stored."
            : $"Highlight {depthMask.Pixels.ToArray().Count(value => value != 0):N0} marked pixels in the room mask.";
        RoomPreview.Source = RoomMaskToggle.IsChecked == true && depthMask is not null
            ? CreateMaskOverlayBitmap(room.Width, room.Height, pixels, room.Palette, depthMask.Pixels.ToArray())
            : CreateBitmap(room.Width, room.Height, pixels, room.Palette);
        RefreshWalkBoxOverlay();
        UpdateVerbArea();
        UpdateActorLayer();
        UpdateMessageLayer();

        var selected = _objects.FirstOrDefault(obj => obj.Id == _selectedObject);
        var selectedPosition = selected is null ? default : _objectSelection.GetPosition(selected);
        ObjectAssetPreview.Source = selected is null ? null
            : CreateBitmap(selected.Width, selected.Height, selected.Pixels, room.Palette);
        ObjectAssetPreview.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        ObjectAssetDescription.Text = selected is null ? ""
            : $"Scene detail · {selected.Width} × {selected.Height}";
        ObjectAssetDescription.ToolTip = selected is null ? null
            : $"Object {selected.Id} · position {selectedPosition.X}, {selectedPosition.Y}";

        ObjectList.Children.Clear();
        _hoveredHotspotRowId = null;
        ShowHotspotHighlight(null);
        var interactive = SessionModeToggle.IsChecked == true && _session is not null;
        if (interactive)
        {
            var hotspots = _currentRoomObjectRecords.OrderBy(item => item.LocalIndex)
                .Where(record => IsVerbHotspotAvailable(record, _session!.Snapshot) &&
                                 _roomVerbs.TryGetValue(record.Id, out var verbs) && verbs.Count > 0)
                .Select(record =>
                {
                    // The stored name, else the rooms a doorway can load, else its index.
                    var name = _roomObjectNames.TryGetValue(record.Id, out var stored) ? stored : null;
                    var doorway = DoorwayTargets(record).Length > 0 ? "→ " + DoorwayLabel(record) : null;
                    var label = name is null ? doorway ?? $"Hotspot {record.LocalIndex}"
                        : doorway is null ? name : $"{name} {doorway}";
                    return (Record: record, Name: name, Label: label);
                }).ToArray();
            var repeated = hotspots.GroupBy(item => item.Label).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToHashSet();
            foreach (var (record, name, label) in hotspots)
            {
                var button = new Button
                {
                    Content = repeated.Contains(label) ? $"{label} · {record.LocalIndex}" : label,
                    ToolTip = $"Hotspot {record.LocalIndex} · object {record.Id}" +
                              (name is null ? "" : $" · stored name \"{name}\"") +
                              $" · {record.X}, {record.Y} · {record.Width} × {record.Height}. " +
                              "Choose a decoded interaction. Use Enter or Space to activate this hotspot.",
                    Tag = record.Id,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 3),
                    Padding = new Thickness(8, 3, 8, 3),
                    Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                    Foreground = Brushes.Beige,
                    BorderThickness = new Thickness(0)
                };
                button.Click += ObjectButton_Click;
                // Hovering or focusing a row outlines its box on the room.
                button.MouseEnter += (_, _) => ShowHotspotHighlight(record);
                button.GotKeyboardFocus += (_, _) => ShowHotspotHighlight(record);
                button.MouseLeave += (_, _) => { if (!button.IsKeyboardFocused) ShowHotspotHighlight(null); };
                button.LostKeyboardFocus += (_, _) => { if (!button.IsMouseOver) ShowHotspotHighlight(null); };
                ObjectList.Children.Add(button);
            }
            ShowObjectListSection("HOTSPOTS");
            return;
        }
        for (var index = 0; index < _objects.Count; index++)
        {
            var obj = _objects[index];
            var visible = _objectSelection.IsActive(obj);
            var button = new Button
            {
                Content = interactive
                    ? $"Hotspot {index + 1} · interact{(_estimatedObjectIds.Contains(obj.Id) ? " · estimated image" : "")}" 
                    : $"Scene detail {index + 1} · {(visible ? _objectSelection.HasAlternatives(obj) ? "next" : "selected" : "show")}{(_objectSelection.CanShowBackground(obj) ? " / background" : "")}",
                ToolTip = $"Object {obj.Id} · {_objectSelection.GetPosition(obj).X}, {_objectSelection.GetPosition(obj).Y} · {obj.Width} × {obj.Height}" +
                          (_estimatedObjectIds.Contains(obj.Id) ? " · artwork estimate; not a script-proven state" : ""),
                Tag = obj.Id,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(8, 3, 8, 3),
                Background = obj.Id == _selectedObject ? new SolidColorBrush(Color.FromRgb(183, 132, 66)) : new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = obj.Id == _selectedObject ? Brushes.Black : Brushes.Beige,
                BorderThickness = new Thickness(0)
            };
            button.Click += ObjectButton_Click;
            ObjectList.Children.Add(button);
        }
        ShowObjectListSection("SCENE DETAILS");
    }

    private void ShowObjectListSection(string heading)
    {
        ObjectListHeader.Text = $"{heading} · {ObjectList.Children.Count}";
        ObjectListSection.Visibility = IsAdvancedMode && ObjectList.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearObjectList()
    {
        ObjectList.Children.Clear();
        _hoveredHotspotRowId = null;
        ObjectListSection.Visibility = Visibility.Collapsed;
    }

    private void ObjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id }) return;
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            var record = _currentRoomObjectRecords.FirstOrDefault(item => item.Id == id);
            ShowObjectVerbChoices(record is null || !_roomVerbs.TryGetValue(id, out var verbs)
                ? [] : verbs.Select(verb => new RoomVerbChoice(record, verb)).ToArray());
        }
        else SelectObject(id);
    }

    private void RoomMaskToggle_Changed(object sender, RoutedEventArgs e) => RefreshRoomPreview();

    private void WalkBoxToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (WalkBoxOverlay is null) return;
        RefreshWalkBoxOverlay();
        WalkBoxOverlay.Dispatcher.BeginInvoke(RefreshWalkBoxOverlay, DispatcherPriority.Loaded);
    }

    private void WalkBoxOverlay_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshWalkBoxOverlay();

    private static LoomWalkBoxSet? TryReadWalkBoxes(byte[] roomBytes)
    {
        try { return LoomWalkBoxReader.Read(roomBytes); }
        catch (InvalidDataException) { return null; }
    }

    private void ShowWalkBoxToggle()
    {
        WalkBoxToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        WalkBoxToggle.IsEnabled = _currentWalkBoxes is { Count: > 0 };
        RefreshWalkBoxOverlay();
    }

    /// <summary>
    /// The walk boxes of the displayed room (ScummVM <c>boxes.cpp</c>): the
    /// session's copy while it is in this room, since <c>setBoxFlags</c> can
    /// change their flags, otherwise the stored ones.
    /// </summary>
    private LoomWalkBoxSet? DisplayedWalkBoxes() =>
        SessionModeToggle.IsChecked == true && _session?.CurrentWalkBoxes is { } live &&
        _session.Snapshot.RoomId == _currentRoomResource?.ResourceId
            ? live : _currentWalkBoxes;

    /// <summary>
    /// Draws each walk box as its quadrangle with its number. Most LOOM boxes
    /// are line segments, the paths actors walk along. Invisible boxes are
    /// dashed; boxes outside the room image are listed in the tooltip.
    /// </summary>
    private void RefreshWalkBoxOverlay()
    {
        WalkBoxOverlay.Children.Clear();
        var boxes = DisplayedWalkBoxes();
        WalkBoxToggle.ToolTip = boxes is null ? "No walk boxes were read for this room." : DescribeWalkBoxes(boxes);
        var show = WalkBoxToggle.IsChecked == true && boxes is not null && _currentRoom is not null &&
                   WalkBoxToggle.Visibility == Visibility.Visible;
        WalkBoxOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show || WalkBoxOverlay.ActualWidth <= 0 || WalkBoxOverlay.ActualHeight <= 0) return;

        var room = _currentRoom!;
        var scale = Math.Min(WalkBoxOverlay.ActualWidth / room.Width, WalkBoxOverlay.ActualHeight / room.Height);
        if (scale <= 0) return;
        var offsetX = (WalkBoxOverlay.ActualWidth - room.Width * scale) / 2;
        var offsetY = (WalkBoxOverlay.ActualHeight - room.Height * scale) / 2;
        WalkBoxOverlay.Clip = new RectangleGeometry(new Rect(offsetX, offsetY, room.Width * scale, room.Height * scale));
        Point Map(double x, double y) => new(offsetX + (x + 0.5) * scale, offsetY + (y + 0.5) * scale);

        foreach (var box in boxes!.Boxes)
        {
            var invisible = (boxes.GetFlags(box.Index) & LoomWalkBox.InvisibleFlag) != 0;
            var accent = invisible ? Color.FromRgb(170, 170, 170) : Color.FromRgb(110, 235, 140);
            var shape = new System.Windows.Shapes.Polygon
            {
                Points = new PointCollection(box.Corners.Select(point => Map(point.X, point.Y))),
                Stroke = new SolidColorBrush(accent),
                StrokeThickness = Math.Clamp(scale * 0.75, 1.5, 3),
                StrokeLineJoin = PenLineJoin.Round,
                Fill = box.IsLine ? null : new SolidColorBrush(Color.FromArgb(48, accent.R, accent.G, accent.B))
            };
            if (invisible) shape.StrokeDashArray = [3, 2];
            WalkBoxOverlay.Children.Add(shape);

            var label = new Border
            {
                Child = new TextBlock
                {
                    Text = box.Index.ToString(),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = Math.Clamp(scale * 4.5, 8, 11),
                    Margin = new Thickness(3, 0, 3, 0)
                },
                Background = new SolidColorBrush(Color.FromArgb(208, 17, 24, 32)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var centre = Map(box.Corners.Average(point => point.X), box.Corners.Average(point => point.Y));
            Canvas.SetLeft(label, centre.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centre.Y - label.DesiredSize.Height / 2);
            WalkBoxOverlay.Children.Add(label);
        }

        // Actors the session places in this room: a marker at the feet, dashed when the position is an estimate.
        if (!ReferenceEquals(boxes, _session?.CurrentWalkBoxes)) return;
        foreach (var actor in _session!.Actors.Where(item => item.Room == _session.Snapshot.RoomId && item.X is not null && item.Y is not null))
        {
            var feet = Map(actor.X!.Value, actor.Y!.Value);
            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = 10, Height = 10,
                Stroke = new SolidColorBrush(Color.FromRgb(255, 190, 70)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(120, 255, 190, 70))
            };
            // The overlay is not hit-testable, so the details go on the checkbox's tooltip.
            WalkBoxToggle.ToolTip += $"\nActor {actor.Number} (A{actor.Number}) at ({actor.X}, {actor.Y}), walk box {actor.WalkBox?.ToString() ?? "unknown"}" +
                                     (actor.Visible == false ? ", hidden" : "") + (actor.Estimate is null ? "" : $"; estimate: {actor.Estimate}");
            if (actor.Estimate is not null) marker.StrokeDashArray = [2, 1.5];
            Canvas.SetLeft(marker, feet.X - 5);
            Canvas.SetTop(marker, feet.Y - 5);
            WalkBoxOverlay.Children.Add(marker);
            var name = new TextBlock
            {
                Text = $"A{actor.Number}",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 70)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 10
            };
            Canvas.SetLeft(name, feet.X + 6);
            Canvas.SetTop(name, feet.Y - 14);
            WalkBoxOverlay.Children.Add(name);
        }
    }

    private string DescribeWalkBoxes(LoomWalkBoxSet boxes)
    {
        var lines = new List<string>
        {
            $"{boxes.Count} walk boxes ({boxes.Boxes.Count(box => box.IsLine)} line segments) from the room's box table at 0x{boxes.TableOffset:X4}."
        };
        if (_currentRoom is { } room)
        {
            var outside = boxes.Boxes.Where(box =>
                    box.Corners.Max(point => point.X) < 0 || box.Corners.Min(point => point.X) >= room.Width ||
                    box.Corners.Max(point => point.Y) < 0 || box.Corners.Min(point => point.Y) >= room.Height)
                .Select(box => box.Index).ToArray();
            if (outside.Length > 0) lines.Add($"Outside the room image: box {string.Join(", ", outside)}.");
        }
        var flagged = boxes.Boxes.Where(box => boxes.GetFlags(box.Index) != 0)
            .Select(box => $"{box.Index} (0x{boxes.GetFlags(box.Index):X2})").ToArray();
        if (flagged.Length > 0) lines.Add($"Flags: box {string.Join(", ", flagged)}; 0x80 means invisible (dashed).");
        if (boxes.MatrixTruncated) lines.Add("The box matrix is truncated in the game data.");
        return string.Join("\n", lines);
    }

    private void RoomNavigationToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (RoomNavigationOverlay is null) return;
        RoomNavigationOverlay.Visibility = _roomNavigationCandidates.Count > 0 &&
            RoomNavigationToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (RoomNavigationOverlay.Visibility == Visibility.Visible)
            RoomNavigationOverlay.Dispatcher.BeginInvoke(RefreshRoomNavigationOverlay,
                DispatcherPriority.Loaded);
    }

    private void RoomPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        RoomViewport.Focus();
        if (_gameClickPairSent)
        {
            // The second press of a double click was sent on its way down.
            _gameClickPairSent = false;
            return;
        }
        if (!TryGetRoomImagePoint(e.GetPosition(RoomPreview), out var x, out var y)) return;
        if (SkipTalkAt(x, y)) return;
        if (GameClickActive)
        {
            QueueGameClick(x, y);
            return;
        }
        HandleRoomPoint(x, y);
    }

    private void RoomPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A second press while a single click waits for the double-click time makes a double click.
        if (!GameClickActive || _pendingGameClick is not { } first || _gameClickTimer?.IsEnabled != true ||
            _session!.Snapshot.RoomId != first.Room) return;
        _gameClickTimer.Stop();
        _pendingGameClick = null;
        _gameClickPairSent = true;
        HandleRoomPoint(first.X, first.Y, doubleClick: true);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>
    /// In the explore mode every room click is the game's click (decided 28 September):
    /// it goes through the input scripts (005–007). Inspection mode cycles artwork instead.
    /// </summary>
    private bool GameClickActive => SessionModeToggle.IsChecked == true && _session is not null;

    // The game's click: a single click waits for the system double-click time before it is sent.
    private DispatcherTimer? _gameClickTimer;
    private (int X, int Y, int? Room)? _pendingGameClick;
    private bool _gameClickPairSent;

    private void QueueGameClick(int x, int y)
    {
        _pendingGameClick = (x, y, _session?.Snapshot.RoomId);
        if (_gameClickTimer is null)
        {
            _gameClickTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime())
            };
            _gameClickTimer.Tick += (_, _) =>
            {
                _gameClickTimer.Stop();
                if (_pendingGameClick is { } pending && GameClickActive && _session!.Snapshot.RoomId == pending.Room)
                    HandleRoomPoint(pending.X, pending.Y);
                _pendingGameClick = null;
            };
        }
        _gameClickTimer.Stop();
        _gameClickTimer.Start();
    }

    /// <summary>
    /// The game's click: sends the click to the session as the game's own
    /// input path would receive it (<see cref="LoomExplorationSession.Click"/>), then
    /// continues frames as a doorway click does, so the ego's walk and script 007's
    /// sentence run and a room load they reach opens the destination. Returns
    /// whether another room was entered.
    /// </summary>
    private bool SendClickToGame(int x, int y, bool doubleClick) =>
        SendInputToGame(() => _session!.Click(x, y, doubleClick), doubleClick ? "Double click" : "Click");

    /// <summary>
    /// Sends input to the session, shows its first result, and plays the frames
    /// that follow on the playback clock (<see cref="StartPlayback"/>). A click on
    /// <paramref name="doorways"/> that reaches no room load and changes no scene (a cutscene that puts the ego) falls back to the
    /// doorway's direct verb when playback ends. Returns whether the session is
    /// in another room so far (with the clock running, a later frame may still get there).
    /// </summary>
    private bool SendInputToGame(Func<LoomObjectActionResult> send, string what,
        IReadOnlyList<LoomRoomObjectRecord>? doorways = null)
    {
        if (_session is null || _currentRoomResource is not { } sourceRoom || _game is null) return false;
        if (_playbackPaused)
        {
            StatusText.Text = $"{what} not sent: the scene is paused. Play (or space) goes on.";
            StatusText.ToolTip = null;
            return false;
        }
        var playing = PlaybackRunning;
        var sceneBefore = (_session.CutscenesBegun, _session.EgoPuts);
        var action = send();
        var refused = action.Uncertainties.FirstOrDefault(line => line.Contains("not sent, because", StringComparison.Ordinal));
        if (playing && refused is not null)
        {
            // Input is off while a scene plays (a cutscene): the game ignores the click and the scene goes on.
            StatusText.Text = $"{what} not sent to the game while the scene plays";
            StatusText.ToolTip = refused;
            return false;
        }
        if (action.DestinationRoomId is int destination && destination != sourceRoom.ResourceId)
        {
            StartPlayback(what, action, sourceRoom, doorways: null, leftRoom: true);
            return true;
        }
        StatusText.Text = refused is null ? $"{what} sent to the game (assumed input)" : $"{what} not sent to the game";
        StatusText.ToolTip = (refused ?? action.Evidence) + (action.Uncertainties.Count == 0 ? "" :
            Environment.NewLine + string.Join(Environment.NewLine, action.Uncertainties));
        ApplyRoomState(sourceRoom, _currentRoomObjectRecords, _objects, visit: false);
        UpdateRoomNavigationActions(sourceRoom);
        RefreshRoomPreview();
        // Refused input changes nothing on screen, so the frames that follow run at once
        // and a doorway's fallback is not delayed.
        StartPlayback(what, action, sourceRoom, doorways, leftRoom: false, instant: refused is not null, sceneBefore);
        return _session.Snapshot.RoomId != sourceRoom.ResourceId;
    }

    /// <summary>
    /// A click on the room. In the explore mode it is the game's click
    /// (<see cref="SendClickToGame"/>): the ego walks to what is under the point
    /// and verb 0x38 runs (a double click adds 0x36), as scripts 005–007 do.
    /// Room hotspots navigate: when the game's click reaches no room load on a
    /// doorway (refused input, or a route the preview cannot follow) and changed no
    /// scene, the doorway's verb runs directly, labelled as an exploration action.
    /// A click whose cutscene put the ego somewhere else is the game's answer: it waits for the next click. In the
    /// inspection mode a click cycles the stored artwork.
    /// </summary>
    // Why the last room click fell back to a doorway's direct verb (for the harnesses and the tooltip).
    private string? _lastDoorwayFallback;
    // Why the last doorway click did not fall back: the game answered it with a scene.
    private string? _lastDoorwayHeld;

    private void HandleRoomPoint(int x, int y, bool doubleClick = false)
    {
        _lastDoorwayFallback = null;
        _lastDoorwayHeld = null;
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            SendInputToGame(() => _session.Click(x, y, doubleClick), doubleClick ? "Double click" : "Click", DoorwaysAt(x, y));
            return;
        }
        CycleArtworkAt(x, y);
    }

    /// <summary>
    /// The game's click on a doorway reached no room load: the doorway's verb runs
    /// directly, labelled as an exploration action, not the game's click.
    /// </summary>
    private void FollowDoorwayFallback(IReadOnlyList<LoomRoomObjectRecord> doorways)
    {
        if (_session is null) return;
        var why = StatusText.Text;
        var refused = _session.LastAction?.Uncertainties.FirstOrDefault(line => line.Contains("not sent, because", StringComparison.Ordinal));
        _lastDoorwayFallback = refused ?? $"{why}: {_session.LastAction?.Evidence}";
        // Among doorways at this point, the first touchable one in directory
        // order is the one ScummVM's findObject picks. Untouchable doorways
        // stay reachable as labelled exploration actions.
        var live = doorways.Where(record => !_session.Snapshot.IsUntouchable(record.Id)).ToArray();
        if (live.Length > 0) FollowDoorway(live[0]);
        else if (doorways.Count == 1) FollowDoorway(doorways[0]);
        else ShowDoorwayMenu(doorways.Select(record => (DoorwayLabel(record), DoorwayToolTip(record),
            (Action)(() => FollowDoorway(record)))).ToArray());
        StatusText.ToolTip = $"{why}. The doorway's verb then ran directly, an exploration action, not the game's click." +
                             (StatusText.ToolTip is string tip ? Environment.NewLine + tip : "");
    }

    // ---- Playback clock (M1d) -------------------------------------------------

    /// <summary>One run of the playback clock after an input.</summary>
    private sealed class Playback
    {
        public required string What { get; init; }
        public LoomObjectActionResult? Action { get; init; }
        public required GameResource SourceRoom { get; init; }
        public IReadOnlyList<LoomRoomObjectRecord>? Doorways { get; init; }
        public bool LeftRoom { get; set; }
        public int IdleFrames { get; set; }
        public int CutsceneFrames { get; set; }
        public int WaitTicks { get; set; }
        public string? Note { get; set; }
        public string? FrameSignature { get; set; }
        public int? CameraX { get; set; }
        // The session's cutscene and ego-put counts before the input: a cutscene that put the ego
        // somewhere since then is a change of scene, the game's answer to the input.
        public (int Cutscenes, int EgoPuts) SceneBefore { get; init; }
    }

    private Playback? _playback;
    private DispatcherTimer? _playbackTimer;
    // Harnesses set this to run a playback's frames at once, as the synchronous
    // continuation did before M1d, so their results stay comparable.
    private bool _playInstantly = false;
    // True while an instant playback runs all its frames at once: their sounds are sorted out when it finishes.
    private bool _playbackBatch;
    // True while the playback itself opens the room the session entered.
    private bool _playbackEnteringRoom;
    // The room the playback last asked the view to open.
    private int? _playbackRequestedRoom;

    private bool PlaybackRunning => _playbackTimer?.IsEnabled == true;

    /// <summary>ScummVM's frame: <c>VAR_TIMER_NEXT</c> jiffies of 1/60 s (6 in LOOM: 10 frames a second).</summary>
    private TimeSpan PlaybackFrameInterval() =>
        TimeSpan.FromSeconds(Math.Clamp(_session?.FrameJiffies ?? 6, 1, LoomMessageTable.MaxFrameDelta) / 60.0);

    /// <summary>
    /// Plays the frames after an input on a timer at the game's frame rate,
    /// redrawing each frame (object states, camera, actors, messages), until no talk
    /// message is on screen, the ego does not walk or turn, the camera is still, and the scripts wait for input: no script is
    /// pending, or <see cref="ExitContinuationFrames"/> frames outside a cutscene
    /// passed (or the session left the room). Frames inside a cutscene do not
    /// count, up to <see cref="LoomExplorationSession.CutsceneFrameLimit"/>. A room
    /// load reached while playing opens that room, and playback goes on there.
    /// </summary>
    private void StartPlayback(string what, LoomObjectActionResult? action, GameResource sourceRoom,
        IReadOnlyList<LoomRoomObjectRecord>? doorways, bool leftRoom, bool instant = false, (int Cutscenes, int EgoPuts)? sceneBefore = null)
    {
        _playback = new Playback
        {
            What = what, Action = action, SourceRoom = sourceRoom, Doorways = doorways, LeftRoom = leftRoom,
            CameraX = _session?.CameraX,
            SceneBefore = sceneBefore ?? (_session?.CutscenesBegun ?? 0, _session?.EgoPuts ?? 0)
        };
        _playbackRequestedRoom = null;
        _playbackPaused = false;
        if (_game is not null && _session is not null && !_sceneSound.Follows(_session)) _sceneSound.Attach(_game, _session);
        if (_playInstantly || instant)
        {
            _playbackTimer?.Stop();
            _playbackBatch = true;
            try { while (PlaybackStep()) { } }
            finally { _playbackBatch = false; }
            FinishPlayback();
            return;
        }
        if (!PlaybackShouldContinue())
        {
            FinishPlayback();
            return;
        }
        _playbackTimer ??= CreatePlaybackTimer();
        _playbackTimer.Interval = PlaybackFrameInterval();
        if (!_playbackTimer.IsEnabled) _playbackTimer.Start();
        ShowPlaybackFrame();
        UpdatePlaybackControls();
    }

    /// <summary>Stops the clock where it is (the user interrupted); nothing more is played.</summary>
    private void StopScenePlayback()
    {
        _playbackTimer?.Stop();
        _playback = null;
        // The game's sounds play on after the clock stops, as its music does between scenes.
        if (_playbackPaused) _sceneSound.Resume();
        _playbackPaused = false;
        UpdatePlaybackControls();
    }

    // ---- Playback controls (M3c) ----------------------------------------------

    // True while the user paused a playback: its frames wait, and nothing else is sent to the game.
    private bool _playbackPaused;

    /// <summary>Pauses the playback clock where it is; <see cref="ResumeScenePlayback"/> goes on from the same frame.</summary>
    private void PauseScenePlayback()
    {
        if (_playback is null || _playbackPaused) return;
        _playbackTimer?.Stop();
        _playbackPaused = true;
        _sceneSound.Pause();
        StatusText.Text = "Paused. Play (or space) goes on; Continue ▸ plays one frame.";
        StatusText.ToolTip = "The scene's frames wait while paused. The game's own pause key (space, VAR_PAUSE_KEY) stops ScummVM's frame loop the same way.";
        UpdatePlaybackControls();
    }

    private void ResumeScenePlayback()
    {
        if (_playback is null || !_playbackPaused) return;
        _playbackPaused = false;
        _sceneSound.Resume();
        _playbackTimer ??= CreatePlaybackTimer();
        _playbackTimer.Interval = PlaybackFrameInterval();
        _playbackTimer.Start();
        StatusText.Text = $"Playing: {_playback.What}";
        UpdatePlaybackControls();
    }

    private DispatcherTimer CreatePlaybackTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
        timer.Tick += PlaybackTimer_Tick;
        return timer;
    }

    /// <summary>While paused, plays one frame (Continue ▸); the playback stays paused.</summary>
    private void StepPausedPlayback()
    {
        if (_playback is null || !ViewShowsSessionRoom()) return;
        if (!PlaybackStep())
        {
            _playbackPaused = false;
            FinishPlayback();
            return;
        }
        ShowPlaybackFrame();
        StatusText.Text = $"Paused after one frame (Room {_session?.Snapshot.RoomId:00}). Play goes on.";
    }

    private void PlaybackPause_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackPaused) ResumeScenePlayback();
        else PauseScenePlayback();
    }

    private void SkipCutscene_Click(object sender, RoutedEventArgs e) => SkipCutscene();

    /// <summary>
    /// Escape during a cutscene (ScummVM <c>processKeyboard</c>: <c>abortCutscene</c> while <c>VAR_CUTSCENEEXIT_KEY</c>
    /// is set): the script that set the cutscene's override goes on at its skip jump on the next frame, and the
    /// playback plays on from there. Assumed input, as every key the explorer sends.
    /// </summary>
    private void SkipCutscene()
    {
        if (!SessionInScene || _session is not { } session || _currentRoomResource is not { } room) return;
        var skippable = session.CutsceneSkippable;
        var action = session.PressKey(0x1B);
        StatusText.Text = skippable
            ? "Escape sent: the cutscene skips to its end (assumed input)"
            : "Escape sent, but this part of the cutscene cannot be skipped: no override is set yet";
        StatusText.ToolTip = string.Join(Environment.NewLine, action.Uncertainties);
        if (_playback is null) StartPlayback("Escape", action, room, doorways: null, leftRoom: false);
        else if (_playbackPaused) ResumeScenePlayback();
        UpdatePlaybackControls();
    }

    /// <summary>
    /// The player has no control: a cutscene is open, or the boot still plays its opening (script 044 opens no
    /// cutscene of its own; script 001 turns input off and sets <c>bit[0xD6E]</c> around it).
    /// </summary>
    private bool SessionInScene => _session is { } session && (session.InCutscene || session.BootRunning);

    /// <summary>Shows the playback bar in the explore mode, and enables pause/play and skip as the playback allows.</summary>
    private void UpdatePlaybackControls()
    {
        var active = GameClickActive && _game?.Id == GameId.Loom;
        PlaybackBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (!active) return;
        UpdateRoomScrollButtons();
        PlaybackPauseButton.IsEnabled = _playback is not null;
        PlaybackPauseButton.Content = _playbackPaused ? "▶ Play" : "⏸ Pause";
        var inCutscene = SessionInScene;
        var skippable = _session?.CutsceneSkippable == true;
        SkipCutsceneButton.IsEnabled = inCutscene && skippable;
        SkipCutsceneButton.ToolTip = !inCutscene
            ? "No cutscene is playing."
            : skippable
                ? "Skip the cutscene (Escape): ScummVM's abortCutscene sends the script that set the override to its skip jump. Assumed input."
                : "The cutscene has not set its skip point (beginOverride) yet, so Escape would do nothing now, as in the game.";
    }

    /// <summary>
    /// A known cutscene and the object verb that starts it (decoded; see the script map). With <see cref="Click"/>,
    /// the verb only leads into the room, and the game's click at that point starts the cutscene.
    /// </summary>
    private sealed record KnownCutscene(string Title, int RoomId, int ObjectId, int VerbId, int Script, string Source,
        (int X, int Y)? Click = null);

    private static readonly KnownCutscene[] KnownCutscenes =
    [
        new("Script 052: Room 10 → Room 11 → Room 13", 10, 295, 0x38, 52,
            "Room 10 object 295 verb 0x38 starts script 052 (+0x0003): a cutscene that waits 60 jiffies (+0x0041), loads Room 11 " +
            "(+0x0045), sets its skip point (+0x00C6) and ends with loadRoomWithEgo into Room 13 (+0x017D)."),
        new("Script 046: the Elders' trial", 8, 255, 0x38, 46,
            "Room 08 object 255 verb 0x38 (the sanctuary door) starts script 046 (+0x0000): a cutscene with its skip point at " +
            "+0x0020, through Rooms 09, 67, 09, 54, 09, 03, 08, 09, 11, 08 and 09, ending at +0x1421 in Room 09."),
        new("Script 086: the firewood scene (Room 37)", 36, 599, 0x38, 86,
            "Room 36 object 599 verb 0x38 leads into Room 37 (loadRoomWithEgo, walk to (24, 101)). On a first visit " +
            "(bit[0xD9A] is 0) Room 37's entry starts local 200 (+0x0097), which chains to script 086 once Bobbin's walk box " +
            "becomes 8 (+0x0016–0x001D). Script 086: Stoke mistakes Bobbin for Rusty; Rooms 37, 58, 37 and 38; skip point " +
            "+0x0023; endCutscene +0x055C in Room 38.",
            (109, 102))
    ];

    /// <summary>
    /// The known cutscenes by the indexed script that prints their texts, for Game Texts' "Play this cutscene".
    /// Script 052 prints no text (decoded), so none of its texts can offer it. Empty outside a LOOM session.
    /// </summary>
    private Dictionary<int, GalleryCutscene> GalleryCutscenes()
    {
        var cutscenes = new Dictionary<int, GalleryCutscene>();
        if (_game?.Id != GameId.Loom || _session is null || _indexedObjectSnapshot is null) return cutscenes;
        cutscenes[LoomExplorationSession.OpeningScript] = new(OpeningTitle, OpeningStartNote, () => PlayFromGameTexts(PlayOpening));
        foreach (var cutscene in KnownCutscenes)
            cutscenes[cutscene.Script] = new(cutscene.Title, CutsceneStartNote(cutscene),
                () => PlayFromGameTexts(() => PlayKnownCutscene(cutscene)));
        return cutscenes;
    }

    /// <summary>Plays a cutscene chosen in Game Texts, turning the interactive mode on first (playback runs only there).</summary>
    private void PlayFromGameTexts(Action play)
    {
        if (SessionModeToggle.IsChecked != true) SessionModeToggle.IsChecked = true;
        play();
    }

    private void CutsceneMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = CutsceneMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var opening = new MenuItem { Header = OpeningTitle, ToolTip = OpeningSource + " " + OpeningStartNote };
        opening.Click += (_, _) => PlayOpening();
        menu.Items.Add(opening);
        foreach (var cutscene in KnownCutscenes)
        {
            var item = new MenuItem { Header = cutscene.Title, ToolTip = cutscene.Source + " " + CutsceneStartNote(cutscene) };
            item.Click += (_, _) => PlayKnownCutscene(cutscene);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private const string OpeningTitle = "The opening (script 044)";

    private const string OpeningSource =
        "Script 001 +0x0222 starts script 044 and waits for it (+0x0225): Rooms 01, 02, 01, 02, 53 and 02, with the copyright " +
        "line, Room 01's points of light (locals 200–208), the fairy's message in Room 53 and Bobbin's lines on the cliff. " +
        "Two skip points (beginOverride +0x005A, +0x0124); after a skip Bobbin faces the screen (+0x063E).";

    private const string OpeningStartNote =
        "Stated start: a fresh session (seed 0) whose boot stops on the frame script 001 starts script 044, after the intro " +
        "screen's answer (Room 69: difficulty and draft assumed, not simulated). The rest of the boot plays on the playback " +
        "clock at the game's frame rate and ends as the app's own boot does, in Room 02. The current exploration state is replaced.";

    /// <summary>
    /// Plays the boot's opening cutscene (script 044): a fresh session stopped where script 001 starts it
    /// (<see cref="LoomExplorationSession.BootRunning"/>), then the rest of the boot on the playback clock.
    /// Escape skips it once script 044 has set its skip point.
    /// </summary>
    private void PlayOpening()
    {
        if (_game is null || _indexedObjectSnapshot is null || !GameClickActive) return;
        StopScenePlayback();
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, stopAtOpening: true);
        _sceneSound.Attach(_game, _session);
        if (_session.Snapshot.RoomId is not int roomId || _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is not { } room)
            return;
        StartPlayback("The opening (script 044)", null, room, doorways: null, leftRoom: false);
        StatusText.Text = $"Playing {OpeningTitle} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = OpeningStartNote + Environment.NewLine + OpeningSource + Environment.NewLine +
                             string.Join(Environment.NewLine, _session.Snapshot.Uncertainties);
    }

    private static string CutsceneStartNote(KnownCutscene cutscene) =>
        $"Stated start: a fresh session after the boot (seed 0), Room {cutscene.RoomId:00} opened from the catalog (the ego's arrival " +
        $"there is assumed), then object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} run directly: an exploration action, not " +
        "the game's click (Bobbin does not walk there, and the object may be untouchable in this state). " +
        (cutscene.Click is { } click
            ? $"The verb leads into the next room; once Bobbin's arrival walk ends (run before the view shows it), the game's click at " +
              $"({click.X}, {click.Y}) (assumed input) walks him on, and the room's own code starts the cutscene. "
            : "") +
        "The frames after it play on the playback clock at the game's frame rate. The current exploration state is replaced.";

    /// <summary>
    /// Plays a known cutscene from its stated start (<see cref="CutsceneStartNote"/>): a fresh session after the boot,
    /// a catalog visit to the room and the object's verb run directly, then the frames on the playback clock.
    /// </summary>
    private void PlayKnownCutscene(KnownCutscene cutscene)
    {
        if (_game is null || _indexedObjectSnapshot is null || !GameClickActive ||
            _game.Rooms.FirstOrDefault(item => item.ResourceId == cutscene.RoomId) is not { } room) return;
        StopScenePlayback();
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot);
        // Sounds from here on: the boot's are over; the visit's, the verb's and the click's play with the first frame.
        _sceneSound.Attach(_game, _session);
        _session.VisitRoom(room);
        var action = _session.ActivateObjectVerb(cutscene.ObjectId, cutscene.VerbId);
        var uncertainties = action.Uncertainties.ToList();
        if (cutscene.Click is { } click)
        {
            // The verb's loadRoomWithEgo walks Bobbin in; the click is sent once he stands, as a player's would be.
            for (var frame = 0; frame < LoomExplorationSession.CutsceneFrameLimit && _session.EgoMoving; frame++) _session.AdvanceFrame();
            action = _session.Click(click.X, click.Y);
            uncertainties.AddRange(action.Uncertainties);
        }
        if (_currentRoomResource?.ResourceId == cutscene.RoomId && _currentRoom is not null)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview();
        }
        StartPlayback(cutscene.Click is { } sent
                ? $"Object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} (run directly), then a click at ({sent.X}, {sent.Y})"
                : $"Object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} (run directly)",
            action, room, doorways: null, leftRoom: false);
        StatusText.Text = $"Playing {cutscene.Title} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = CutsceneStartNote(cutscene) + Environment.NewLine + cutscene.Source + Environment.NewLine +
                             string.Join(Environment.NewLine, uncertainties);
    }

    private bool PlaybackShouldContinue()
    {
        if (_session is not { } session || _playback is not { } playback) return false;
        var talking = session.Messages.HaveMsg != 0;
        // The ego's walk or turn, or a camera pan, still in progress plays on (M3a), even with no script left.
        // Other actors are not waited for: ambient walkers (script 042's swan) never stop.
        var moving = session.EgoMoving || session.CameraMoving;
        if (session.Snapshot.PendingScripts == 0 && !talking && !moving) return false;
        if (SessionInScene)
        {
            if (playback.CutsceneFrames < LoomExplorationSession.CutsceneFrameLimit) return true;
            playback.Note = $"A cutscene was still open after {LoomExplorationSession.CutsceneFrameLimit} frames; playback stopped following it.";
            return false;
        }
        return talking || moving || !playback.LeftRoom && playback.IdleFrames < ExitContinuationFrames;
    }

    /// <summary>Advances the session by one frame if playback goes on; returns whether it did.</summary>
    private bool PlaybackStep()
    {
        if (!PlaybackShouldContinue()) return false;
        var session = _session!;
        var playback = _playback!;
        if (SessionInScene) playback.CutsceneFrames++;
        // Frames of a walk do not count as idle: the sentence that waits for it runs after it.
        else if (!session.EgoMoving && !session.CameraMoving) playback.IdleFrames++;
        var room = session.Snapshot.RoomId;
        session.AdvanceFrame();
        if (session.Snapshot.RoomId != room) playback.LeftRoom = true;
        // The sounds the frame's scripts started or stopped (instant playback has no frames to wait for).
        if (!_playbackBatch) _sceneSound.Update();
        return true;
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (_session is null || _playback is not { } playback || _game is null)
        {
            StopScenePlayback();
            return;
        }
        // A room the playback entered is still loading: the next frame waits for it,
        // so the view's room load cannot take the session back to another room.
        if (!ViewShowsSessionRoom())
        {
            if (++playback.WaitTicks > 100) StopScenePlayback();
            return;
        }
        playback.WaitTicks = 0;
        if (!PlaybackStep())
        {
            FinishPlayback();
            return;
        }
        _playbackTimer!.Interval = PlaybackFrameInterval();
        ShowPlaybackFrame();
    }

    /// <summary>Whether the view shows the session's room; if not, asks the view to open it (once).</summary>
    private bool ViewShowsSessionRoom()
    {
        if (_session?.Snapshot.RoomId is not int roomId || _game is null) return false;
        if (_currentRoomResource?.ResourceId == roomId && _currentRoom is not null) return true;
        if (_playbackRequestedRoom != roomId && _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is { } target)
            EnterPlaybackRoom(target);
        return false;
    }

    private void EnterPlaybackRoom(GameResource target)
    {
        _playbackRequestedRoom = target.ResourceId;
        StatusText.Text = "You step into another scene.";
        StatusText.ToolTip = $"Room {target.ResourceId:00} · " + (_session?.LastAction?.Evidence ?? "");
        _playbackEnteringRoom = true;
        try { SelectResourceInTree(target); }
        finally { _playbackEnteringRoom = false; }
    }

    /// <summary>Redraws the frame: the room when object states changed, the camera when it moved, and the messages.</summary>
    private void ShowPlaybackFrame()
    {
        if (_session is not { } session || _playback is not { } playback || !ViewShowsSessionRoom() ||
            _currentRoomResource is not { } room) return;
        var snapshot = session.Snapshot;
        var signature = FrameSignature(snapshot);
        if (signature != playback.FrameSignature)
        {
            var first = playback.FrameSignature is null;
            playback.FrameSignature = signature;
            if (!first)
            {
                ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
                UpdateRoomNavigationActions(room);
                RefreshRoomPreview();
            }
        }
        if (session.CameraX != playback.CameraX)
        {
            playback.CameraX = session.CameraX;
            if (session.CameraX is int cameraX) ScrollToGameCamera(cameraX);
        }
        UpdateActorLayer();
        UpdateMessageLayer();
        UpdateVerbArea();
        UpdatePlaybackControls();
    }

    private static string FrameSignature(LoomSessionSnapshot snapshot) =>
        string.Join(",", snapshot.ObjectStates.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")) + "|" +
        string.Join(",", snapshot.ObjectPositions.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.X},{pair.Value.Y}")) + "|" +
        string.Join(",", snapshot.DrawOrder);

    /// <summary>Scrolls the 320-pixel view to the game's camera without the entry pan.</summary>
    private void ScrollToGameCamera(int cameraX)
    {
        if (_currentRoom is null || NormalRoomWidthToggle.IsChecked != true) return;
        StopCameraPan();
        var left = Math.Clamp(cameraX - Math.Min(320, _currentRoom.Width) / 2.0, 0,
            RoomViewport.ScrollableWidth / _roomDisplayScale);
        RoomViewport.ScrollToHorizontalOffset(left * _roomDisplayScale);
    }

    /// <summary>
    /// The end of a playback: says where the input led, shows the final state and,
    /// when a doorway click reached no room load, runs the doorway's fallback.
    /// </summary>
    private void FinishPlayback()
    {
        _playbackTimer?.Stop();
        _playbackPaused = false;
        if (_playback is not { } playback || _session is not { } session || _game is null)
        {
            _playback = null;
            UpdatePlaybackControls();
            return;
        }
        _playback = null;
        UpdatePlaybackControls();
        _sceneSound.Update();
        var snapshot = session.Snapshot;
        var note = playback.Note is null ? "" : Environment.NewLine + playback.Note;
        if (snapshot.RoomId is int reached && reached != playback.SourceRoom.ResourceId)
        {
            if (_currentRoomResource?.ResourceId != reached && _playbackRequestedRoom != reached &&
                _game.Rooms.FirstOrDefault(item => item.ResourceId == reached) is { } target)
                EnterPlaybackRoom(target);
            else if (_currentRoomResource is { } shown && shown.ResourceId == reached && _currentRoom is not null)
            {
                ApplyRoomState(shown, _currentRoomObjectRecords, _objects, visit: false);
                UpdateRoomNavigationActions(shown);
                RefreshRoomPreview();
            }
            StatusText.Text = $"{playback.What} sent to the game: entered Room {reached:00}";
            StatusText.ToolTip = (playback.Action?.Evidence ?? session.LastAction?.Evidence ?? "") + note;
            return;
        }
        if (playback.Action is { } action)
        {
            var refused = action.Uncertainties.FirstOrDefault(line => line.Contains("not sent, because", StringComparison.Ordinal));
            StatusText.Text = refused is null ? $"{playback.What} sent to the game (assumed input); no room load was reached" : $"{playback.What} not sent to the game";
            StatusText.ToolTip = (refused ?? action.Evidence) + (action.Uncertainties.Count == 0 ? "" :
                Environment.NewLine + string.Join(Environment.NewLine, action.Uncertainties)) + note;
        }
        if (_currentRoomResource is { } room && _currentRoom is not null && room.ResourceId == snapshot.RoomId)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview();
        }
        if (playback.Doorways is not { Count: > 0 } doorways || _currentRoomResource?.ResourceId != playback.SourceRoom.ResourceId)
            return;
        var cutscenes = session.CutscenesBegun - playback.SceneBefore.Cutscenes;
        var egoPuts = session.EgoPuts - playback.SceneBefore.EgoPuts;
        if (cutscenes > 0 && egoPuts > 0)
        {
            // The game answered the click with a change of scene: a cutscene put the ego somewhere else (Room 02's
            // script 045 puts the smaller Bobbin on the next stretch of the path, which ends the click's walk). The
            // game waits there for the player's next click, so the doorway's direct verb does not run. A cutscene
            // that leaves the ego where it is (a refusal, such as Room 37 object 601's verb 0x37) still falls back.
            _lastDoorwayHeld = $"{cutscenes} cutscene(s) begun, the ego put {egoPuts} time(s)";
            StatusText.Text = $"{playback.What} sent to the game: the scene changed; click again to go on";
            StatusText.ToolTip = "The click began a cutscene that put Bobbin somewhere else, and reached no room load. As in the game, " +
                                 "the next click goes on from there. The doorway's direct verb (an exploration action) runs only when " +
                                 "the game's click changes no scene." + (StatusText.ToolTip is string tip ? Environment.NewLine + tip : "");
            return;
        }
        FollowDoorwayFallback(doorways);
    }

    // ---- Actors on screen (M2c) -----------------------------------------------

    /// <summary>
    /// Draws the session's actors in the shown room over the room image (ScummVM <c>processActors</c>: sorted by y,
    /// each visible actor with a costume at (x, y − elevation) on its current limb frames), clipped to the room.
    /// An actor whose walk box has a mask is hidden where the room's depth mask (with the drawn objects' masks) is set,
    /// unless its classes say otherwise (class 20 never clips, class 21 always does).
    /// </summary>
    private void UpdateActorLayer()
    {
        var room = _currentRoom;
        if (!GameClickActive || room is null || _game is not { } game ||
            _session!.Snapshot.RoomId is not int roomId || roomId != _currentRoomResource?.ResourceId)
        {
            RoomActorOverlay.Source = null;
            RoomActorOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        var layer = LoomActorRenderer.Render(_session.Actors, roomId, room.Width, room.Height, id =>
        {
            if (_actorCostumes.TryGetValue(id, out var costume)) return costume;
            try { costume = LoomCostume.Load(game, id); }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or IOException) { costume = null; }
            return _actorCostumes[id] = costume;
        }, _objectSelection.ComposeDepthMask(room), _session.CurrentWalkBoxes, _session.ObjectClasses);
        if (layer.Actors.Count == 0)
        {
            RoomActorOverlay.Source = null;
            RoomActorOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        var pixels = new byte[room.Width * room.Height * 4];
        for (var i = 0; i < layer.Pixels.Length; i++)
        {
            var index = layer.Pixels[i];
            if (index == LoomActorRenderer.Transparent) continue;
            SetBgra(pixels, i * 4, index < room.Palette.Count ? room.Palette[index] : 0xFF000000u);
        }
        RoomActorOverlay.Source = FrozenBgra(room.Width, room.Height, pixels);
        RoomActorOverlay.Visibility = Visibility.Visible;
    }

    // ---- Messages on screen (M1c) ---------------------------------------------

    // The talk text's rectangle in room pixels (right and bottom exclusive), for a click that skips it.
    private (int Left, int Top, int Right, int Bottom)? _talkTextRect;
    // Verb-screen text (screen rows 144–199) as a 320 × 56 layer, or null.
    private BitmapSource? _verbMessageBitmap;

    /// <summary>The interactive LOOM mode, where the room and the verb screen are drawn as the game's one screen.</summary>
    private bool OneGameScreen() => _currentRoom is not null && GameClickActive && _game?.Id == GameId.Loom;

    /// <summary>The game's camera left edge in room pixels (ScummVM <c>virtscr[0].xstart</c>).</summary>
    private int GameCameraLeft(RoomImage room)
    {
        var visible = Math.Min(LoomMessageRenderer.ScreenWidth, room.Width);
        return Math.Clamp((_session?.CameraX ?? visible / 2) - visible / 2, 0, room.Width - visible);
    }

    /// <summary>
    /// Draws the session's messages over the room and in the verb screen, at their
    /// screen positions relative to the game's camera: screen row y is room row
    /// y (the boot's <c>initScreens(0, 144)</c>), rows 144–199 are the verb screen.
    /// Charset 0 draws them; if it cannot be read, a UI font does, labelled as not the game's.
    /// </summary>
    private void UpdateMessageLayer()
    {
        _talkTextRect = null;
        var room = _currentRoom;
        var session = _session;
        var messages = GameClickActive && room is not null && session!.Snapshot.RoomId == _currentRoomResource?.ResourceId
            ? session.Messages.OnScreen : [];
        var verbBefore = _verbMessageBitmap is not null;
        if (messages.Count == 0 || room is null)
        {
            RoomMessageOverlay.Source = null;
            RoomMessageOverlay.Visibility = Visibility.Collapsed;
            RoomMessageOverlay.ToolTip = null;
            _verbMessageBitmap = null;
        }
        else
        {
            var left = GameCameraLeft(room);
            if (_charset is { } charset)
            {
                var layer = LoomMessageRenderer.Render(messages, charset);
                RoomMessageOverlay.Source = MessageRoomBitmap(layer, room, left);
                _verbMessageBitmap = MessageVerbBitmap(layer, room.Palette);
                if (session!.Messages.Talk is { Y: < LoomMessageRenderer.VerbScreenTop } talk &&
                    LoomMessageRenderer.Render(talk, charset).Bounds is { } bounds)
                    _talkTextRect = (bounds.Left + left, bounds.Top - LoomMessageRenderer.RoomScreenTop,
                        bounds.Right + left, bounds.Bottom - LoomMessageRenderer.RoomScreenTop);
            }
            else
            {
                (RoomMessageOverlay.Source, _verbMessageBitmap, _talkTextRect) =
                    FallbackMessageBitmaps(messages, session!.Messages.Talk, room, left);
                ObjectsNote.Text = $"Messages use a UI font, not the game's ({_charsetError ?? "charset 0 is unavailable"}).";
            }
            RoomMessageOverlay.Visibility = Visibility.Visible;
        }
        VerbMessageOverlay.Source = _verbMessageBitmap;
        VerbMessageOverlay.Visibility = _verbMessageBitmap is null ? Visibility.Collapsed : Visibility.Visible;
        if (verbBefore != (_verbMessageBitmap is not null))
        {
            UpdateVerbStrip();
            UpdateRoomViewportLayout();
        }
    }

    private static BitmapSource MessageRoomBitmap(LoomMessageLayer layer, RoomImage room, int cameraLeft)
    {
        var pixels = new byte[room.Width * room.Height * 4];
        for (var screenY = LoomMessageRenderer.RoomScreenTop; screenY < LoomMessageRenderer.VerbScreenTop; screenY++)
        {
            var y = screenY - LoomMessageRenderer.RoomScreenTop;
            if (y >= room.Height) break;
            for (var x = 0; x < layer.Width; x++)
            {
                var index = layer.Pixels[screenY * layer.Width + x];
                var roomX = cameraLeft + x;
                if (index == LoomMessageRenderer.Transparent || roomX >= room.Width) continue;
                SetBgra(pixels, (y * room.Width + roomX) * 4, index < room.Palette.Count ? room.Palette[index] : 0xFF000000u);
            }
        }
        return FrozenBgra(room.Width, room.Height, pixels);
    }

    // The verb screen's text on a transparent 320 × 56 layer, or null when there is none.
    private static BitmapSource? MessageVerbBitmap(LoomMessageLayer layer, IReadOnlyList<uint> palette)
    {
        var pixels = new byte[LoomVerbArea.Width * LoomVerbArea.Height * 4];
        var any = false;
        for (var y = 0; y < LoomVerbArea.Height; y++)
        for (var x = 0; x < LoomVerbArea.Width; x++)
        {
            var index = layer.Pixels[(LoomVerbArea.Top + y) * layer.Width + x];
            if (index == LoomMessageRenderer.Transparent) continue;
            SetBgra(pixels, (y * LoomVerbArea.Width + x) * 4, index < palette.Count ? palette[index] : 0xFF000000u);
            any = true;
        }
        return any ? FrozenBgra(LoomVerbArea.Width, LoomVerbArea.Height, pixels) : null;
    }

    private static void SetBgra(byte[] pixels, int at, uint argb)
    {
        pixels[at] = (byte)argb;
        pixels[at + 1] = (byte)(argb >> 8);
        pixels[at + 2] = (byte)(argb >> 16);
        pixels[at + 3] = 0xFF;
    }

    private static BitmapSource FrozenBgra(int width, int height, byte[] pixels)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Presentation when charset 0 cannot be read: each message's lines in a UI font
    /// at the message's position and EGA colour. It is not the game's font.
    /// </summary>
    private (BitmapSource Room, BitmapSource? Verb, (int, int, int, int)? Talk) FallbackMessageBitmaps(
        IReadOnlyList<LoomMessage> messages, LoomMessage? talk, RoomImage room, int cameraLeft)
    {
        var typeface = new Typeface("Consolas");
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        (int, int, int, int)? talkRect = null;
        BitmapSource Draw(int width, int height, Func<LoomMessage, bool> include, int offsetX, int offsetY, bool keepTalk)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                foreach (var message in messages.Where(include))
                {
                    var argb = (message.Color & 0x0F) < room.Palette.Count ? room.Palette[message.Color & 0x0F] : 0xFFFFFFFFu;
                    var brush = new SolidColorBrush(Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
                    var top = message.Y + offsetY;
                    foreach (var line in message.Lines)
                    {
                        var text = new FormattedText(line, System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, typeface, 8, brush, dpi);
                        var x = message.X + offsetX - (message.Center ? text.Width / 2 : 0);
                        context.DrawText(text, new Point(Math.Max(offsetX, x), top));
                        if (keepTalk && ReferenceEquals(message, talk))
                        {
                            var rect = ((int)x, top, (int)Math.Ceiling(x + text.Width), top + (int)Math.Ceiling(text.Height));
                            talkRect = talkRect is { } r
                                ? (Math.Min(r.Item1, rect.Item1), Math.Min(r.Item2, rect.Item2), Math.Max(r.Item3, rect.Item3), Math.Max(r.Item4, rect.Item4))
                                : rect;
                        }
                        top += message.LineHeight != 0 ? message.LineHeight : 8;
                    }
                }
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        var roomBitmap = Draw(room.Width, room.Height, message => message.Y < LoomMessageRenderer.VerbScreenTop,
            cameraLeft, -LoomMessageRenderer.RoomScreenTop, keepTalk: true);
        BitmapSource? verb = messages.Any(message => message.Y >= LoomMessageRenderer.VerbScreenTop)
            ? Draw(LoomVerbArea.Width, LoomVerbArea.Height, message => message.Y >= LoomMessageRenderer.VerbScreenTop,
                0, -LoomVerbArea.Top, keepTalk: false)
            : null;
        return (roomBitmap, verb, talkRect);
    }

    /// <summary>A click on the talk text: ScummVM's talk-stop key ('.') ends its delay, as assumed input.</summary>
    private bool SkipTalkAt(int x, int y)
    {
        if (!GameClickActive || _talkTextRect is not { } rect || _session!.Messages.Talk is null ||
            x < rect.Left - 2 || x >= rect.Right + 2 || y < rect.Top - 2 || y >= rect.Bottom + 2) return false;
        _session.Messages.SkipTalkDelay();
        StatusText.Text = "Text skipped (the game's talk-stop key '.', assumed input)";
        StatusText.ToolTip = "ScummVM's '.' key sets the talk delay to 0, so the next frame ends the message or shows its next part.";
        if (!PlaybackRunning && _currentRoomResource is { } room)
            StartPlayback("Text skip", null, room, doorways: null, leftRoom: false);
        return true;
    }

    private bool CycleArtworkAt(int x, int y)
    {
        var obj = _objectSelection.HitTest(x, y);
        if (obj is null) return false;
        _estimatedObjectIds = new HashSet<int>();
        ObjectsNote.Text = "Asset inspection: click an image area to cycle its stored alternatives. The selection is local to this view.";
        var wasVisible = _objectSelection.IsActive(obj);
        var next = _objectSelection.Next(obj);
        _selectedObject = next?.Id ?? obj.Id;
        RefreshRoomPreview();
        StatusText.Text = next is null
            ? $"Object {obj.Id} · room background shown"
            : !wasVisible
            ? $"Object {next.Id} · first stored image shown at {PositionText(next)}"
            : next.Id == obj.Id
            ? $"Object {obj.Id} · no further visible alternative"
            : $"Object {next.Id} · stored image at {PositionText(next)}";
        return true;
    }

    private void RoomPreview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_currentRoom is null ||
            !TryGetRoomImagePoint(e.GetPosition(RoomPreview), out var x, out var y))
        {
            RoomPreview.Cursor = Cursors.Arrow;
            RoomPreview.ToolTip = null;
            ShowHoveredHotspotRow(null);
            SendPointerToGame(null, null);
            return;
        }
        var hoveredRow = SessionModeToggle.IsChecked == true ? HotspotRowRecordAt(x, y) : null;
        ShowHoveredHotspotRow(hoveredRow);
        SendPointerToGame((x, y), hoveredRow?.Id ?? 0);
        if (GameClickActive)
        {
            var doorways = DoorwaysAt(x, y);
            RoomPreview.Cursor = hoveredRow is null && doorways.Count == 0 ? Cursors.Arrow : Cursors.Hand;
            var what = hoveredRow is null ? "Nothing listed here: a click walks Bobbin to this point." :
                $"{(_roomObjectNames.TryGetValue(hoveredRow.Id, out var name) ? name : $"Object {hoveredRow.Id}")}: " +
                "a click walks Bobbin over and uses it (verb 0x38); a double click also looks at it (verb 0x36).";
            RoomPreview.ToolTip = what + " The click goes through the game's input scripts (005–007) as assumed input." +
                                  (doorways.Count > 0 ? $" This doorway leads to {string.Join(" or ", doorways.Select(DoorwayLabel))}." : "");
            return;
        }
        var artwork = _objectSelection.HitTest(x, y);
        if (artwork is not null)
        {
            var canChange = !_objectSelection.IsActive(artwork) ||
                            _objectSelection.HasAlternatives(artwork) ||
                            _objectSelection.CanShowBackground(artwork);
            RoomPreview.Cursor = canChange ? Cursors.Hand : Cursors.Arrow;
            RoomPreview.ToolTip = !canChange
                ? $"Object {artwork.Id} has no other visible image state. Use the hotspot list for script interactions."
                : _objectSelection.IsActive(artwork)
                ? _objectSelection.CanShowBackground(artwork)
                    ? $"Click to cycle Object {artwork.Id} through stored images and room background."
                    : $"Click to cycle Object {artwork.Id} through stored images."
                : $"Click to restore the first stored image at this position.";
            return;
        }
        if (SessionModeToggle.IsChecked != true)
        {
            RoomPreview.Cursor = Cursors.Arrow;
            RoomPreview.ToolTip = null;
            return;
        }
        var choices = FindVerbChoicesAt(x, y);
        RoomPreview.Cursor = choices.Count == 0 ? Cursors.Arrow : Cursors.Hand;
        RoomPreview.ToolTip = choices.Count switch
        {
            0 => null,
            1 => $"Click to interact. Object {choices[0].Object.Id} · {choices[0].Verb.Label}",
            _ => $"Click to choose an interaction. {choices.Count} object verb entries are available."
        };
    }

    private void RoomPreview_MouseLeave(object sender, MouseEventArgs e)
    {
        RoomPreview.Cursor = Cursors.Arrow;
        RoomPreview.ToolTip = null;
        ShowHoveredHotspotRow(null);
        SendPointerToGame(null, null);
    }

    // The room and the object under the pointer (0: none) when a frame last ran for the game pointer.
    private (int? Room, int? Target)? _gamePointerTarget;

    /// <summary>
    /// "Pointer as game input": gives the pointer's room position to the session
    /// as the virtual mouse (assumed input, no click). A frame runs only when the
    /// object under the pointer changes, so the input loop sees the new hover once.
    /// </summary>
    private void SendPointerToGame((int X, int Y)? point, int? target)
    {
        if (GamePointerToggle.IsChecked != true || SessionModeToggle.IsChecked != true || _session is null ||
            _currentRoomResource is not { } room || _session.Snapshot.RoomId != room.ResourceId) return;
        _session.SetPointer(point);
        // While the playback clock runs, its frames see the pointer.
        if (PlaybackRunning) return;
        if (_gamePointerTarget == (room.ResourceId, target)) return;
        _gamePointerTarget = (room.ResourceId, target);
        AdvanceOneFrame();
    }

    private void GamePointerToggle_Changed(object sender, RoutedEventArgs e)
    {
        _gamePointerTarget = null;
        if (GamePointerToggle.IsChecked != true) _session?.SetPointer(null);
        UpdateVerbArea();
    }

    // Height kept for the verb strip's note under the strip.
    private const double VerbStripNoteHeight = 36;

    private bool VerbStripShown() =>
        VerbAreaToggle.IsChecked == true && _session is not null && _game?.Id == GameId.Loom && _currentRoom is not null;

    private void VerbAreaToggle_Changed(object sender, RoutedEventArgs e)
    {
        UpdateVerbStrip();
        UpdateRoomViewportLayout();
    }

    /// <summary>
    /// The optional verb strip: a presentation of the session's verb slots
    /// (<see cref="LoomVerbArea.Compose"/>), drawn under the room at its scale.
    /// It shows game state; the drawing itself is not the game's.
    /// </summary>
    // The verb screen is shown for the strip, or for text a script printed there (rows 144–199).
    // In the interactive LOOM mode it is always shown, so the room keeps its size when a script prints there or clears it.
    private bool VerbScreenShown() => VerbStripShown() ||
        _currentRoom is not null && (_verbMessageBitmap is not null || GameClickActive && _game?.Id == GameId.Loom);

    private void UpdateVerbStrip()
    {
        if (!VerbStripShown())
        {
            if (VerbScreenShown())
            {
                // The verb screen holds the game's text: draw it with the verb slots it goes with,
                // so the close-up (verb 0x36, script 008) shows above the object's name (script 007).
                var slots = _session!.VerbArea();
                VerbStripImage.Source = CreateBitmap(slots.Width, slots.Height, slots.Pixels, _currentRoom!.Palette);
                VerbStripNote.Text = _verbMessageBitmap is not null
                    ? "Verb screen: the game's text there, with the verb slots it goes with (presentation of the slots)."
                    : "Verb screen (rows 144–199), kept under the room: the session's verb slots (presentation of the slots).";
                VerbStripNote.ToolTip = string.Join(Environment.NewLine, _session?.Messages.OnScreen
                    .Where(message => message.Y >= LoomVerbArea.Top)
                    .Select(message => $"\"{message.Text}\" at ({message.X}, {message.Y}) from {message.Source}") ?? []);
                VerbStripPanel.Visibility = Visibility.Visible;
                return;
            }
            VerbStripPanel.Visibility = Visibility.Collapsed;
            VerbStripImage.Source = null;
            return;
        }
        var picture = _session!.VerbArea();
        VerbStripImage.Source = CreateBitmap(picture.Width, picture.Height, picture.Pixels, _currentRoom!.Palette);
        var drawn = picture.Items.Count == 0
            ? "no image verb is on in the session's state"
            : "verbs " + VerbRanges(picture.Items.Where(item => item.Mode == 1).Select(item => item.VerbId)) + " on" +
              (picture.Items.Any(item => item.Mode == 2)
                  ? ", " + VerbRanges(picture.Items.Where(item => item.Mode == 2).Select(item => item.VerbId)) + " dim" : "");
        var estimate = picture.Items.Any(item => item.Estimate is not null) ? " Some follow an estimate." : "";
        var notes = picture.Notes.Count == 0 ? "" : $" {picture.Notes.Count} not drawn.";
        var clicks = GameClickActive ? " Clicks on verbs that are on go to the game as assumed input." : "";
        VerbStripNote.Text = $"Verb area: presentation of the session's verb slots, not the game's drawing; {drawn}.{estimate}{notes}{clicks}";
        VerbStripNote.ToolTip = string.Join(Environment.NewLine, picture.Items.Select(item =>
                $"verb 0x{item.VerbId:X2} (slot {item.Slot}): object {item.ImageObject}'s image from Room {item.ImageRoom:00} at ({item.X}, {item.Y})" +
                (item.Mode == 2 ? ", dim" : "") + (item.Estimate is null ? "" : $"; estimate: {item.Estimate}"))
            .Concat(picture.Notes.Select(line => $"not drawn: {line}"))
            .Append("Text verbs, the note letters scripts print, highlight colours and redraw timing are not drawn."));
        VerbStripPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Verb numbers as hex ranges: 0x01–0x08, 0x31, 0x32.</summary>
    private static string VerbRanges(IEnumerable<int> verbs)
    {
        var parts = new List<string>();
        var sorted = verbs.Distinct().Order().ToArray();
        for (var i = 0; i < sorted.Length;)
        {
            var end = i;
            while (end + 1 < sorted.Length && sorted[end + 1] == sorted[end] + 1) end++;
            parts.Add(end - i >= 2 ? $"0x{sorted[i]:X2}–0x{sorted[end]:X2}"
                : string.Join(", ", sorted[i..(end + 1)].Select(verb => $"0x{verb:X2}")));
            i = end + 1;
        }
        return string.Join(", ", parts);
    }

    // The verb strip's screen point (x 0–319, y 144–199) under the mouse, or null off the strip.
    private (int X, int Y)? VerbStripPoint(Point position)
    {
        if (VerbStripImage.ActualWidth <= 0 || VerbStripImage.ActualHeight <= 0) return null;
        var x =(int)(position.X / VerbStripImage.ActualWidth * LoomVerbArea.Width);
        var y = (int)(position.Y / VerbStripImage.ActualHeight * LoomVerbArea.Height);
        if (x is < 0 or >= LoomVerbArea.Width || y is < 0 or >= LoomVerbArea.Height) return null;
        return (x, LoomVerbArea.Top + y);
    }

    // With "Click as game input" on, only verbs whose slot is on (findVerbAtPos) react.
    private LoomVerbAreaItem? ClickableVerbAt(Point position) =>
        GameClickActive && VerbStripShown() && VerbStripPoint(position) is { } point
            ? LoomVerbArea.FindVerbAt(_session!.VerbArea().Items, point.X, point.Y) : null;

    private void VerbStripImage_MouseMove(object sender, MouseEventArgs e)
    {
        var verb = ClickableVerbAt(e.GetPosition(VerbStripImage));
        VerbStripImage.Cursor = verb is null ? null : Cursors.Hand;
        VerbStripImage.ToolTip = verb is null ? null
            : $"Verb 0x{verb.VerbId:X2} (slot {verb.Slot}): a click goes to the game's input script as assumed input (runInputScript(1, 0x{verb.VerbId:X2}, 0)).";
    }

    /// <summary>
    /// "Click as game input" on the verb strip: a click on a verb that is on goes
    /// to the session as ScummVM's <c>checkExecVerbs</c> would send it
    /// (<see cref="LoomExplorationSession.ClickVerbArea"/>); elsewhere nothing is sent.
    /// </summary>
    private void VerbStripImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        ClickVerbStrip(e.GetPosition(VerbStripImage));

    // Returns whether the click was sent (a point in the strip's own coordinates).
    private bool ClickVerbStrip(Point position)
    {
        if (ClickableVerbAt(position) is not { } verb || VerbStripPoint(position) is not { } point) return false;
        SendInputToGame(() => _session!.ClickVerbArea(point.X, point.Y), $"Click on verb 0x{verb.VerbId:X2}");
        return true;
    }

    /// <summary>
    /// Shows the verb-area close-up (verb 0x36) while the game pointer is on and
    /// the verb is on: script 008 sets it from the hovered object's verb 0x39
    /// (<c>var[0x068]</c>). It is game state reached from assumed hover input.
    /// </summary>
    private void UpdateVerbArea()
    {
        UpdateVerbStrip();
        var closeUp = GamePointerToggle.IsChecked == true || GameClickActive
            ? _session?.Verbs.FirstOrDefault(verb => verb.VerbId == 0x36) : null;
        if (closeUp is not { Mode: 1, ImageObject: int image } || _session is null)
        {
            VerbAreaPanel.Visibility = Visibility.Collapsed;
            return;
        }
        var snapshot = _session.Snapshot;
        var picture = closeUp.ImageRoom == snapshot.RoomId ? _objects.FirstOrDefault(item => item.Id == image) : null;
        VerbAreaImage.Source = picture is null || _currentRoom is null ? null
            : CreateBitmap(picture.Width, picture.Height, picture.Pixels, _currentRoom.Palette);
        var hovered = snapshot.KnownGlobalVariables.TryGetValue("var[0x066]", out var selected) ? $" for object {selected}" : "";
        VerbAreaText.Text = $"Verb area close-up: object {image}'s image (verb 0x36, script 008{hovered}). " +
                            (_session.LastAction is { IsGameInput: true } click && click.SourceRoomId == snapshot.RoomId
                                ? "Game state reached from an assumed click sent to the game's input script."
                                : "Game state reached from assumed hover input; no click was sent.") +
                            (closeUp.Estimate is null ? "" : $" Estimate: {closeUp.Estimate}.");
        VerbAreaPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The listed hotspot under a room point, as findObject would pick it: the
    /// first listed object in directory order whose box (at its current position)
    /// contains the point, touchable objects before untouchable ones.
    /// </summary>
    private LoomRoomObjectRecord? HotspotRowRecordAt(int x, int y)
    {
        if (_session is null) return null;
        var listed = ObjectList.Children.OfType<Button>().Select(button => button.Tag).OfType<int>().ToHashSet();
        if (listed.Count == 0) return null;
        var snapshot = _session.Snapshot;
        var hits = _currentRoomObjectRecords.OrderBy(item => item.LocalIndex).Where(record =>
        {
            if (!listed.Contains(record.Id)) return false;
            var at = snapshot.ObjectPositions.TryGetValue(record.Id, out var current)
                ? current : new RoomObjectPosition(record.X, record.Y);
            return at.X <= x && x < at.X + record.Width && at.Y <= y && y < at.Y + record.Height;
        }).ToArray();
        return hits.FirstOrDefault(record => !snapshot.IsUntouchable(record.Id)) ?? hits.FirstOrDefault();
    }

    private int? _hoveredHotspotRowId;
    private static readonly SolidColorBrush HotspotRowBrush = new(Color.FromRgb(39, 54, 66));
    private static readonly SolidColorBrush HoveredHotspotRowBrush = new(Color.FromRgb(46, 92, 116));

    /// <summary>
    /// Hovering the room highlights the hotspot row of the object under the
    /// pointer, scrolls it into view and outlines its box; null clears it.
    /// </summary>
    private void ShowHoveredHotspotRow(LoomRoomObjectRecord? record)
    {
        // Inspection mode lists scene details with their own colours.
        if (SessionModeToggle.IsChecked != true || _hoveredHotspotRowId == record?.Id) return;
        _hoveredHotspotRowId = record?.Id;
        foreach (var button in ObjectList.Children.OfType<Button>())
        {
            if (button.Tag is not int id || !_currentRoomObjectRecords.Any(item => item.Id == id)) continue;
            var hovered = id == record?.Id;
            button.Background = hovered ? HoveredHotspotRowBrush : HotspotRowBrush;
            if (hovered) button.BringIntoView();
        }
        // The row's own hover and focus outline take the box when the pointer leaves the room.
        if (record is not null || ObjectList.Children.OfType<Button>().All(button => !button.IsMouseOver && !button.IsKeyboardFocused))
            ShowHotspotHighlight(record);
    }

    private IReadOnlyList<RoomVerbChoice> FindVerbChoicesAt(int x, int y)
    {
        var snapshot = _session?.Snapshot;
        if (snapshot is null) return [];
        var choices = new List<RoomVerbChoice>();
        foreach (var record in _currentRoomObjectRecords.OrderBy(item => item.LocalIndex))
        {
            if (!IsVerbHotspotAvailable(record, snapshot) ||
                !_roomVerbs.TryGetValue(record.Id, out var verbs)) continue;
            var position = snapshot.ObjectPositions.TryGetValue(record.Id, out var current)
                ? current : new RoomObjectPosition(record.X, record.Y);
            if (x < position.X || x >= position.X + record.Width ||
                y < position.Y || y >= position.Y + record.Height) continue;
            choices.AddRange(verbs.Select(verb => new RoomVerbChoice(record, verb)));
        }
        return choices;
    }

    private bool IsVerbHotspotAvailable(LoomRoomObjectRecord record, LoomSessionSnapshot snapshot) =>
        record.Width > 0 && record.Height > 0 &&
        snapshot.ObjectOwners.GetValueOrDefault(record.Id) == 15 &&
        ParentStateMatches(record, snapshot);

    /// <summary>
    /// Doorway hotspots at a room point that are present in the room (owned by
    /// it, parent state matching), in directory order.
    /// </summary>
    private IReadOnlyList<LoomRoomObjectRecord> DoorwaysAt(int x, int y)
    {
        if (_session?.Snapshot is not { } snapshot) return [];
        return VisibleDoorways(snapshot).Where(record =>
        {
            var position = snapshot.ObjectPositions.TryGetValue(record.Id, out var current)
                ? current : new RoomObjectPosition(record.X, record.Y);
            return x >= position.X && x < position.X + record.Width && y >= position.Y && y < position.Y + record.Height;
        }).ToArray();
    }

    private IEnumerable<LoomRoomObjectRecord> VisibleDoorways(LoomSessionSnapshot snapshot) =>
        _roomNavigationCandidates.Select(candidate => candidate.Object).DistinctBy(record => record.Id)
            .Where(record => record.Width > 0 && record.Height > 0 &&
                             snapshot.ObjectOwners.GetValueOrDefault(record.Id) == 15 &&
                             ParentStateMatches(record, snapshot))
            .OrderBy(record => record.LocalIndex);

    private int[] DoorwayTargets(LoomRoomObjectRecord record) => _roomNavigationCandidates
        .Where(candidate => candidate.Object.Id == record.Id).Select(candidate => candidate.TargetRoomId)
        .Distinct().Order().ToArray();

    private string DoorwayLabel(LoomRoomObjectRecord record) =>
        "Room " + string.Join(" / ", DoorwayTargets(record).Select(id => id.ToString("00")));

    private string DoorwayToolTip(LoomRoomObjectRecord record)
    {
        var targets = DoorwayTargets(record);
        var text = $"Object {record.Id}: its script can load {string.Join(" or ", targets.Select(id => $"Room {id:00}"))}. " +
                   "Clicking runs its verb and follows the room load it reaches; if none is reached, the room opens for browsing.";
        return _session?.Snapshot.IsUntouchable(record.Id) == true
            ? text + " This hotspot is untouchable in the current preview (class 24), so the game would not select it here; " +
              "the click is an exploration action."
            : text;
    }

    /// <summary>
    /// Runs a doorway's verb and continues pending scripts. When no room load
    /// is reached, opens its only possible room, or offers its possible rooms.
    /// </summary>
    private void FollowDoorway(LoomRoomObjectRecord record)
    {
        var routes = _roomNavigationCandidates.Where(candidate => candidate.Object.Id == record.Id).ToArray();
        if (routes.Length == 0) return;
        if (ActivateObjectVerb(record, routes[0].Verb) || ContinueToReachedRoom()) return;
        var targets = routes.DistinctBy(candidate => candidate.TargetRoomId).OrderBy(candidate => candidate.TargetRoomId).ToArray();
        if (targets.Length == 1)
        {
            OpenRoomDestination(targets[0]);
            return;
        }
        ShowDoorwayMenu(targets.Select(candidate => ($"Room {candidate.TargetRoomId:00}",
            $"No room load was reached. Open Room {candidate.TargetRoomId:00} directly for exploration; " +
            $"object {candidate.Object.Id}'s script can load it ({candidate.Path}).",
            (Action)(() => OpenRoomDestination(candidate)))).ToArray());
    }

    private ContextMenu? _doorwayMenu;

    private void ShowDoorwayMenu(IReadOnlyList<(string Label, string ToolTip, Action Choose)> items)
    {
        var menu = new ContextMenu { PlacementTarget = RoomPreview, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        foreach (var (label, toolTip, choose) in items)
        {
            var item = new MenuItem { Header = label, ToolTip = toolTip };
            item.Click += (_, _) => choose();
            menu.Items.Add(item);
        }
        _doorwayMenu = menu;
        menu.IsOpen = true;
    }

    private void OpenRoomDestination(RoomVerbDestination candidate)
    {
        if (_game?.Rooms.FirstOrDefault(item => item.ResourceId == candidate.TargetRoomId) is not { } target)
            return;
        SelectResourceInTree(target);
    }

    private bool ParentStateMatches(LoomRoomObjectRecord record, LoomSessionSnapshot snapshot)
    {
        var checkedCount = 0;
        while (record.ParentLocalIndex != 0)
        {
            if (++checkedCount > _currentRoomObjectRecords.Count) return false;
            var parent = _currentRoomObjectRecords.FirstOrDefault(item => item.LocalIndex == record.ParentLocalIndex);
            if (parent is null || !snapshot.ObjectStates.TryGetValue(parent.Id, out var state) ||
                (state & 0x0F) != record.ParentRequiredState) return false;
            record = parent;
        }
        return true;
    }

    private bool TryGetRoomImagePoint(Point point, out int x, out int y)
    {
        x = y = 0;
        if (_currentRoom is null) return false;
        var room = _currentRoom;
        var scale = Math.Min(RoomPreview.ActualWidth / room.Width, RoomPreview.ActualHeight / room.Height);
        if (scale <= 0) return false;
        var imageX = (point.X - (RoomPreview.ActualWidth - room.Width * scale) / 2) / scale;
        var imageY = (point.Y - (RoomPreview.ActualHeight - room.Height * scale) / 2) / scale;
        if (imageX < 0 || imageY < 0 || imageX >= room.Width || imageY >= room.Height) return false;
        x = (int)imageX;
        y = (int)imageY;
        return true;
    }

    private void UpdateRoomNavigationActions(GameResource room)
    {
        RoomNavigationButtons.Children.Clear();
        _roomNavigationCandidates = [];
        if (SessionModeToggle.IsChecked != true || _session is null || _game is null)
        {
            RoomNavigationPanel.Visibility = Visibility.Collapsed;
            RoomNavigationOverlay.Visibility = Visibility.Collapsed;
            RoomNavigationToggle.Visibility = Visibility.Collapsed;
            return;
        }
        var candidates = new List<RoomVerbDestination>();
        _doorwayFinder ??= new LoomDoorwayFinder(_game);
        foreach (var record in _currentRoomObjectRecords)
        {
            if (!_roomVerbs.TryGetValue(record.Id, out var verbs)) continue;
            foreach (var verb in verbs)
            {
                LoomScriptDisassembly script;
                try { script = _verbReader.Decode(room, record, verb.VerbId); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
                // Follow started room-local and global scripts too: many exits
                // (for example Room 03's town and cemetery paths) load the room there.
                foreach (var target in _doorwayFinder.Find(room, script))
                    candidates.Add(new RoomVerbDestination(record, verb, target.RoomId, target.InstructionOffset, target.Path));
            }
        }
        _roomNavigationCandidates = candidates.DistinctBy(item =>
            (item.Object.Id, item.Verb.VerbId, item.TargetRoomId)).ToArray();
        // One button per target room; every hotspot that leads there stays in its tooltip.
        foreach (var target in _roomNavigationCandidates.GroupBy(item => item.TargetRoomId))
        {
            var candidate = target.First();
            // Verbs that share one code block are one source; name them all.
            var sources = target.GroupBy(item => (item.Object.Id, item.Verb.ScriptOffset, item.InstructionOffset, item.Path))
                .Select(source =>
                    $"{room.Name} object {source.Key.Id} ({string.Join(", ", source.Select(item => item.Verb.Label).Distinct())}) " +
                    $"code 0x{source.Key.ScriptOffset:X4}, room-load instruction +0x{source.Key.InstructionOffset:X4} ({source.Key.Path})")
                .ToArray();
            var button = new Button
            {
                Content = $"Room {target.Key:00}",
                ToolTip = (sources.Length == 1
                              ? $"Static candidate: {sources[0]}."
                              : $"Static candidates ({sources.Length} sources):\n• {string.Join("\n• ", sources)}") +
                          "\nOpen directly for exploration; script conditions and effects are not simulated.",
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 5, 5),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += (_, _) => OpenRoomDestination(candidate);
            RoomNavigationButtons.Children.Add(button);
        }
        RoomNavigationHeader.Text = $"DOORWAYS · {RoomNavigationButtons.Children.Count}";
        RoomNavigationPanel.ToolTip = "Possible doorways: rooms that decoded hotspot code can load. " +
                                      "A button opens the room directly for exploration; clicking a doorway box on the room runs its script.";
        RoomNavigationPanel.Visibility = IsAdvancedMode && _roomNavigationCandidates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RoomNavigationToggle.Visibility = _roomNavigationCandidates.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RoomNavigationOverlay.Visibility = _roomNavigationCandidates.Count > 0 && RoomNavigationToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        RefreshRoomNavigationOverlay();
    }

    private void RoomNavigationOverlay_SizeChanged(object sender, SizeChangedEventArgs e) =>
        RefreshRoomNavigationOverlay();

    private void RefreshRoomNavigationOverlay()
    {
        RoomNavigationOverlay.Children.Clear();
        if (_currentRoom is null || RoomNavigationOverlay.ActualWidth <= 0 || RoomNavigationOverlay.ActualHeight <= 0)
            return;

        var room = _currentRoom;
        var scale = Math.Min(RoomNavigationOverlay.ActualWidth / room.Width,
            RoomNavigationOverlay.ActualHeight / room.Height);
        if (scale <= 0) return;
        var offsetX = (RoomNavigationOverlay.ActualWidth - room.Width * scale) / 2;
        var offsetY = (RoomNavigationOverlay.ActualHeight - room.Height * scale) / 2;

        if (_session?.Snapshot is not { } snapshot) return;
        // Doorways that share one rectangle get one box and one label.
        foreach (var group in VisibleDoorways(snapshot).GroupBy(record =>
                 {
                     var at = snapshot.ObjectPositions.TryGetValue(record.Id, out var current)
                         ? current : new RoomObjectPosition(record.X, record.Y);
                     return (at.X, at.Y, record.Width, record.Height);
                 }))
        {
            var (x, y, width, height) = group.Key;
            var targetIds = group.SelectMany(DoorwayTargets).Distinct().Order().ToArray();
            var live = group.Any(record => !snapshot.IsUntouchable(record.Id));
            var toolTip = string.Join("\n", group.Select(DoorwayToolTip));
            var accent = live ? Color.FromRgb(255, 207, 105) : Color.FromRgb(190, 176, 150);

            var bounds = new Border
            {
                Width = width * scale,
                Height = height * scale,
                BorderThickness = new Thickness(Math.Clamp(scale, 1, 3)),
                BorderBrush = new SolidColorBrush(accent),
                Background = new SolidColorBrush(Color.FromArgb((byte)(live ? 48 : 24), 247, 187, 72)),
                CornerRadius = new CornerRadius(2),
                ToolTip = toolTip
            };
            Canvas.SetLeft(bounds, offsetX + x * scale);
            Canvas.SetTop(bounds, offsetY + y * scale);
            RoomNavigationOverlay.Children.Add(bounds);

            var label = new Border
            {
                Child = new TextBlock
                {
                    Text = "Room " + string.Join(" / ", targetIds.Select(id => id.ToString("00"))),
                    FontStyle = live ? FontStyles.Normal : FontStyles.Italic,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = Math.Clamp(scale * 6, 8, 13),
                    Margin = new Thickness(4, 2, 4, 2)
                },
                Background = new SolidColorBrush(Color.FromArgb(224, 17, 24, 32)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                ToolTip = toolTip
            };
            // Keep the label inside the room image, even for a narrow box at its edge.
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Max(offsetX, Math.Min(offsetX + x * scale + 2,
                offsetX + room.Width * scale - label.DesiredSize.Width - 2)));
            Canvas.SetTop(label, offsetY + y * scale + 2);
            RoomNavigationOverlay.Children.Add(label);
        }
    }

    private LoomRoomObjectRecord? _highlightedHotspot;

    private void HotspotHighlightOverlay_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ShowHotspotHighlight(_highlightedHotspot);

    /// <summary>
    /// Outlines a hotspot's box on the room image (at its current position in
    /// the session), or clears the outline when <paramref name="record"/> is null.
    /// </summary>
    private void ShowHotspotHighlight(LoomRoomObjectRecord? record)
    {
        _highlightedHotspot = record;
        HotspotHighlightOverlay.Children.Clear();
        HotspotHighlightOverlay.Visibility = record is null ? Visibility.Collapsed : Visibility.Visible;
        if (record is null || _currentRoom is null || HotspotHighlightOverlay.ActualWidth <= 0 ||
            HotspotHighlightOverlay.ActualHeight <= 0) return;

        var room = _currentRoom;
        var scale = Math.Min(HotspotHighlightOverlay.ActualWidth / room.Width,
            HotspotHighlightOverlay.ActualHeight / room.Height);
        if (scale <= 0) return;
        var offsetX = (HotspotHighlightOverlay.ActualWidth - room.Width * scale) / 2;
        var offsetY = (HotspotHighlightOverlay.ActualHeight - room.Height * scale) / 2;
        var at = _session?.Snapshot.ObjectPositions.TryGetValue(record.Id, out var current) == true
            ? current : new RoomObjectPosition(record.X, record.Y);
        var bounds = new Border
        {
            Width = Math.Max(1, record.Width * scale),
            Height = Math.Max(1, record.Height * scale),
            BorderThickness = new Thickness(Math.Clamp(scale, 2, 3)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(120, 220, 255)),
            Background = new SolidColorBrush(Color.FromArgb(56, 120, 220, 255)),
            CornerRadius = new CornerRadius(2)
        };
        Canvas.SetLeft(bounds, offsetX + at.X * scale);
        Canvas.SetTop(bounds, offsetY + at.Y * scale);
        HotspotHighlightOverlay.Children.Add(bounds);
    }

    private async void UpdateRoomMusicActions()
    {
        var generation = ++_roomMusicUpdateGeneration;
        RoomMusicButtons.Children.Clear();
        RoomMusicPanel.Visibility = Visibility.Collapsed;
        RoomMusicMoreButton.Visibility = Visibility.Collapsed;
        if (_game is null || _currentRoomResource is not { } room) return;
        var game = _game;
        var soundById = game.Resources.Where(item => item.Kind == ResourceKind.Sound && item.ResourceId.HasValue)
            .GroupBy(item => item.ResourceId!.Value).ToDictionary(group => group.Key, group => group.First());
        var possible = _roomSoundCandidates.Analyze(game, room, _currentRoomObjectRecords);
        var session = SessionModeToggle.IsChecked == true && _session?.Snapshot.RoomId == room.ResourceId
            ? _session : null;
        var events = session?.SoundEvents.ToArray() ?? [];
        var entrySequence = session?.RoomEntrySequence ?? 0;
        var idsToInspect = possible.Where(item => item.SoundId.HasValue).Select(item => item.SoundId!.Value)
            .Concat(events.Where(item => item.Event.TargetId.HasValue).Select(item => item.Event.TargetId!.Value))
            .Distinct().ToArray();
        var cachedTracks = idsToInspect.Where(_roomCueTrackCache.ContainsKey)
            .ToDictionary(id => id, id => _roomCueTrackCache[id]);
        Dictionary<int, (bool IsMusic, TimeSpan? Duration)> scannedTracks;
        try
        {
            scannedTracks = await Task.Run(() =>
            {
                var decoder = new LoomAdlibAudio();
                var results = new Dictionary<int, (bool IsMusic, TimeSpan? Duration)>(cachedTracks);
                foreach (var id in idsToInspect)
                {
                    if (results.ContainsKey(id) || !soundById.TryGetValue(id, out var resource)) continue;
                    try
                    {
                        if (!decoder.HasMusic(resource)) { results[id] = (false, null); continue; }
                        results[id] = (true, decoder.EstimateDuration(resource));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
                    {
                        results[id] = (false, null);
                    }
                }
                return results;
            });
        }
        catch (Exception)
        {
            if (generation == _roomMusicUpdateGeneration && ReferenceEquals(game, _game) && room == _currentRoomResource)
                RoomMusicPanel.Visibility = Visibility.Collapsed;
            return;
        }
        if (generation != _roomMusicUpdateGeneration || !ReferenceEquals(game, _game) || room != _currentRoomResource) return;
        foreach (var (id, result) in scannedTracks)
        {
            _roomCueTrackCache[id] = result;
            if (result.Duration is TimeSpan duration) _roomCueDurations[id] = duration;
        }
        bool IsMusic(int id) => scannedTracks.TryGetValue(id, out var result) && result.IsMusic;
        var reached = session is not null
            ? LoomSoundCueResolver.ResolveForRoom(events, room.ResourceId!.Value, entrySequence, IsMusic)
            : new LoomReachedSoundCue(null, false, 0);
        var reachedId = reached.Start?.TargetId;
        var evidenceById = possible.Where(item => item.SoundId.HasValue)
            .GroupBy(item => item.SoundId!.Value)
            .ToDictionary(group => group.Key, group => string.Join(Environment.NewLine, group.Select(item => item.Evidence)));
        var candidateIds = evidenceById.Keys.Concat(reachedId.HasValue ? [reachedId.Value] : []).Distinct();
        var candidates = new List<(GameResource Resource, TimeSpan Duration)>();
        foreach (var id in candidateIds)
        {
            if (!soundById.TryGetValue(id, out var resource) || !scannedTracks.TryGetValue(id, out var result) ||
                !result.IsMusic || result.Duration is not TimeSpan duration || duration.TotalSeconds < 15) continue;
            candidates.Add((resource, duration));
        }
        candidates = candidates.OrderByDescending(item => item.Duration).ThenBy(item => item.Resource.ResourceId).ToList();
        if (candidates.Count == 0)
        {
            ClearRoomCue();
            return;
        }

        var manuallySelected = _roomCueManuallySelected && _roomCueSourceRoomId == room.ResourceId
            ? candidates.FirstOrDefault(item => item.Resource.ResourceId == _roomSelectedCue?.ResourceId)
            : default;
        var selected = manuallySelected.Resource is not null ? manuallySelected : candidates[0];
        var selectedId = selected.Resource.ResourceId!.Value;
        var selectedReached = selectedId == reachedId;
        var selectedEvidence = selectedReached
            ? reached.Start!.Format()
            : evidenceById.GetValueOrDefault(selectedId, "Reached room cue.");
        SelectRoomCue(selected.Resource, selectedEvidence, selectedReached,
            manuallySelected.Resource is not null);

        var note = reached.HasUnknownSoundTarget
            ? "A reached sound command has an unresolved target; this selection uses the longest measured local music candidate."
            : selectedReached
                ? reached.StartedBeforeEntry
                    ? "This room's code started the cue on an earlier visit."
                    : "This cue began after you entered this room."
                : "Possible room music from static script paths; those paths have not been reached.";
        RoomMusicNote.Text = note;
        RoomMusicNote.ToolTip = "Room music candidates shorter than 15 seconds are omitted. The default is the longest first-pass estimate; looped tracks are measured through their first loop boundary, with estimates capped at 3:00.";
        RoomMusicMoreButton.ToolTip = RoomMusicNote.Text + " " + RoomMusicNote.ToolTip;
        RoomCueTitle.ToolTip = selectedEvidence + Environment.NewLine + note;

        foreach (var item in candidates.Where(item => item.Resource.ResourceId != selectedId))
        {
            var id = item.Resource.ResourceId!.Value;
            var evidence = id == reachedId ? reached.Start!.Format() : evidenceById.GetValueOrDefault(id, "");
            var isReached = id == reachedId;
            var button = new Button
            {
                Content = $"▶ {item.Resource.Name} · {FormatRoomCueDuration(item.Duration)}",
                ToolTip = (isReached ? "Reached during this session. " : "Static script possibility; execution has not reached this cue. ") + evidence,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 0, 3),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += async (_, _) =>
            {
                SelectRoomCue(item.Resource, evidence, isReached, manuallySelected: true);
                RoomMusicMoreButton.ContextMenu!.IsOpen = false;
                UpdateRoomMusicActions();
                await PlayRoomCueAsync();
            };
            RoomMusicButtons.Children.Add(button);
        }
        RoomMusicMoreButton.Content = $"Other possible music · {RoomMusicButtons.Children.Count} ▾";
        RoomMusicMoreButton.Visibility = RoomMusicButtons.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RoomMusicPanel.Visibility = Visibility.Visible;
    }

    private void RoomMusicMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private static string FormatRoomCueDuration(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";

    private void SelectRoomCue(GameResource sound, string evidence, bool reached, bool manuallySelected = false)
    {
        if (_roomSelectedCue != sound && _audioSurface == AudioSurface.Room) StopAudio();
        _roomSelectedCue = sound;
        _roomCueSourceRoomId = _currentRoomResource?.ResourceId;
        _roomCueReached = reached;
        _roomCueManuallySelected = manuallySelected;
        RoomCueTitle.Text = $"{(reached ? "Started" : "Possible")}: {sound.Name}";
        RoomCueEvidence.Text = reached ? "from this session's route" : "from room script data";
        RoomCueEvidence.ToolTip = evidence;
        RoomCueTitle.ToolTip = evidence;
        RoomCuePlayButton.ToolTip = "Play room cue";
        if (_roomCueDurations.TryGetValue(sound.ResourceId ?? -1, out var duration))
            RoomCueTime.Text = $"0:00 / {FormatRoomCueDuration(duration)}";
        RoomCuePlayButton.IsEnabled = true;
        RoomCuePauseButton.IsEnabled = _audioSurface == AudioSurface.Room && _audioReady;
        RoomCueStopButton.IsEnabled = _audioSurface == AudioSurface.Room;
        if (_audioSurface != AudioSurface.Room) RoomCueStatus.Text = "Select Play to listen.";
    }

    private void ClearRoomCue()
    {
        if (_audioSurface == AudioSurface.Room) StopAudio();
        _roomSelectedCue = null;
        _roomCueSourceRoomId = null;
        _roomCueReached = false;
        _roomCueManuallySelected = false;
        RoomCueTitle.Text = "No cue established";
        RoomCueTitle.ToolTip = null;
        RoomCueEvidence.Text = "";
        RoomCueEvidence.ToolTip = null;
        RoomCueStatus.Text = "";
        RoomCueTime.Text = "0:00 / 0:00";
        RoomCuePlayButton.IsEnabled = false;
        RoomCuePauseButton.IsEnabled = false;
        RoomCueStopButton.IsEnabled = false;
    }

    private void SelectObject(int id)
    {
        var obj = _objects.FirstOrDefault(item => item.Id == id);
        if (obj is null) return;
        _estimatedObjectIds = new HashSet<int>();
        ObjectsNote.Text = "Asset inspection: click an image area to cycle its stored alternatives. The selection is local to this view.";
        var hidden = false;
        RoomObjectImage? next = null;
        if (_objectSelection.IsActive(obj))
        {
            next = _objectSelection.Next(obj);
            hidden = next is null;
        }
        else
            _objectSelection.Select(obj);
        _selectedObject = next?.Id ?? id;
        RefreshRoomPreview();
        StatusText.Text = hidden ? $"Object {id} · background restored"
            : next is not null && next.Id != id ? $"Object {next.Id} · stored image at {PositionText(next)}"
            : $"Object {id} · selected at {PositionText(obj)} · {obj.Width} × {obj.Height}";
    }

    private string PositionText(RoomObjectImage obj)
    {
        var position = _objectSelection.GetPosition(obj);
        return $"{position.X}, {position.Y}";
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels, IReadOnlyList<uint> palette)
    {
        var colors = palette.Select(argb => Color.FromArgb((byte)(argb >> 24),
            (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)).ToList();
        var bitmap = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Indexed8, new BitmapPalette(colors), pixels, width);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateMaskOverlayBitmap(int width, int height, byte[] pixels,
        IReadOnlyList<uint> palette, byte[] mask)
    {
        var bgra = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = palette[pixels[i]];
            var blue = (byte)color;
            var green = (byte)(color >> 8);
            var red = (byte)(color >> 16);
            if (mask[i] != 0)
            {
                red = (byte)((red + 255) / 2);
                green = (byte)(green / 2);
                blue = (byte)((blue + 255) / 2);
            }
            bgra[i * 4] = blue;
            bgra[i * 4 + 1] = green;
            bgra[i * 4 + 2] = red;
            bgra[i * 4 + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateCostumeBitmap(int width, int height, byte[] pixels, IReadOnlyList<uint> palette)
    {
        var bgra = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = palette[pixels[i]];
            bgra[i * 4] = (byte)color;
            bgra[i * 4 + 1] = (byte)(color >> 8);
            bgra[i * 4 + 2] = (byte)(color >> 16);
            bgra[i * 4 + 3] = (byte)(color >> 24);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

}

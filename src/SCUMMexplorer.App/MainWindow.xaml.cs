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

namespace SCUMMexplorer.App;

public partial class MainWindow : Window
{
    private readonly GameFolderScanner _scanner = new();
    private readonly ScummV3EgaRoomDecoder _roomDecoder = new();
    private readonly ScummRoomObjects _objectDecoder = new();
    private readonly ScummRoomObjectDirectory _objectDirectory = new();
    private readonly LflObjectStateReader _stateReader = new();
    private readonly ScummV3CostumeReader _costumeReader = new();
    private readonly ScummCostumeRenderer _costumeRenderer = new();
    private ScummScriptReader _scriptReader = new();
    private ScummObjectVerbReader _verbReader = new();
    private readonly LoomRoomSoundCandidates _roomSoundCandidates = new();
    private readonly ScummBoundedScriptExecutor _scriptExecutor = new();
    private readonly LoomRelationshipIndexer _relationshipIndexer = new();
    private readonly ScummPcSpeakerAudio _audioDecoder = new();
    private readonly ScummAdlibAudio _adlibDecoder = new();

    private readonly MediaPlayer _audioPlayer = new();
    // The sounds the scripts start while the playback clock runs (cutscenes); separate from the room music panel.
    private readonly SceneSoundPlayer _sceneSound = new();
    private readonly CdVoicePlayer _cdVoice = new();
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _idleFrameTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly DispatcherTimer _roomPanTimer =new() { Interval = TimeSpan.FromMilliseconds(75) };
    private readonly DispatcherTimer _adlibGainDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private static readonly string LastFolderFile = Path.Combine(
        Environment.GetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer"),
        "last-folder.txt");
    private GameLibrary? _game;
    private LoomReplayCatalog? _replayCatalog;
    private LoomSceneSetupPlanner? _setupPlanner;
    private string? _pendingCatalogPreparation;
    private readonly Dictionary<string, LoomStartSetup> _startSetups = [];
    private LoomRelationshipIndex? _relationshipIndex;
    private GameResource? _selectedResource;
    private GameResource? _lastViewedRoomResource;
    private int _relationshipBuildGeneration;
    private int _roomCardGeneration;
    private bool _roomCardsMayUseCache;
    private CancellationTokenSource? _roomCardCancellation;
    private CancellationTokenSource? _previewCancellation;
    private RoomImage? _currentRoom;
    private GameResource? _currentRoomResource;
    private ScummWalkBoxSet? _currentWalkBoxes;
    /// <summary>The CD game's first-visit scene of the room shown (its actors are drawn from it); null for other games.</summary>
    private LoomCdScene? _cdScene;
    // The CD game's data source for the room view (null for the other games).
    private IScummDataSource? _cdData;
    private IReadOnlyList<ScummRoomObjectRecord> _currentRoomObjectRecords = [];
    private IReadOnlyDictionary<int, string> _roomObjectNames = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> _roomVerbs =
        new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
    private IReadOnlyList<RoomVerbDestination> _roomNavigationCandidates = [];
    private IReadOnlyList<RoomObjectImage> _objects = [];
    private RoomObjectSelection _objectSelection = new([]);
    private IReadOnlySet<int> _estimatedObjectIds = new HashSet<int>();
    private IReadOnlyDictionary<int, byte> _indexedStates = new Dictionary<int, byte>();
    private ScummIndexedObjectSnapshot? _indexedObjectSnapshot;
    private LoomExplorationSession? _session;
    // Charset 0 (99.LFL) for messages, or why it could not be read (then a labelled UI font is used).
    private ScummV3Charset? _charset;
    // Costumes read for the actor layer (M2c), by id; null when a costume cannot be read.
    private readonly Dictionary<int, ScummCostume?> _actorCostumes = [];
    private string? _charsetError;
    // The CD game's charset 2, the one script 001 sets when the intro ends (+0x015E); script 044 sets 4 for a while.
    private ScummV4Charset? _cdCharset;
    private readonly Dictionary<int, ScummV4Charset?> _cdCharsets = [];

    /// <summary>The CD charset a message was printed with (script 044 sets 4 for one title line); the default (2) when none was set or one cannot be read.</summary>
    private ScummV4Charset CdCharset(int? number, ScummV4Charset fallback)
    {
        if (number is not int n || n == 2 || _game?.V4Archive is not { } vga) return fallback;
        if (!_cdCharsets.TryGetValue(n, out var charset))
        {
            try { charset = vga.ReadCharset(n); }
            catch (InvalidDataException) { charset = null; }
            _cdCharsets[n] = charset;
        }
        return charset ?? fallback;
    }
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
    private sealed record RoomVerbDestination(ScummRoomObjectRecord Object, ScummObjectVerbEntry Verb,
        int TargetRoomId, int InstructionOffset, string Path = "direct");
    private ScummSceneDrawBatches? _sceneBatches;
    private LoomDoorwayFinder? _doorwayFinder;
    private IReadOnlyList<RoomArtworkEstimate> _artworkEstimates = [];
    private sealed record RoomVerbChoice(ScummRoomObjectRecord Object, ScummObjectVerbEntry Verb);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ReopenLastFolder();
        Closed += (_, _) => { _roomCardCancellation?.Cancel(); _costumeCardCancellation?.Cancel(); StopRoomPan(); StopAudio(); };
        Deactivated += (_, _) => StopRoomPan();
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        LoadViewOptions();
        PreviewTextInput += MainWindow_PreviewTextInput;
        _roomPanTimer.Tick += RoomPanTimer_Tick;
        _audioTimer.Tick += (_, _) => UpdateAudioTime();
        _idleFrameTimer.Tick += IdleFrameTimer_Tick;
        _sceneSound.PreparingChanged += SceneSound_PreparingChanged;
        _idleFrameTimer.Start();
        _adlibGainDebounceTimer.Tick += async (_, _) =>
        {
            _adlibGainDebounceTimer.Stop();
            await ApplyAdlibGainAsync();
        };
        _audioPlayer.MediaOpened += AudioPlayer_MediaOpened;
        _audioPlayer.MediaOpened += (_, _) => _sceneSound.StopAll("another sound was started in the app");
        // And the other way round: a sound the game starts stops the app's other player, so two copies never overlap.
        _sceneSound.Starting += () => { if (_audioSurface != AudioSurface.None) StopAudio(); };
        _sceneSound.PreviewStarting += source => StatusText.ToolTip = source + Environment.NewLine +
            "This audio follows the executed state preview; its estimated route is not confirmed gameplay.";
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
            MessageBox.Show(this, "Open a game folder first.", "Game texts", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var extraction = new LoomTextExtractor().Extract(_game);
            var gallery = new TextGalleryWindow(_game, extraction, roomId =>
            {
                if (_game?.Rooms.FirstOrDefault(room => room.ResourceId == roomId) is { } room)
                    SelectResourceInTree(room);
            }, GalleryCutscenes(), RenderFirstVisitRoomAsync) { Owner = this };
            gallery.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not read game texts", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanEditGameStatus => _session?.GameStatus.CanEdit == true && _replaySequence is null &&
        !_playbackPaused && (_playback is null || !PlaybackHasActivity);

    private GameStatusWindow BuildGameStatusWindow() => new(() => _session, () => CanEditGameStatus, () =>
    {
        if (_currentRoomResource is { } room && _currentRoom is not null && room.ResourceId == _session?.Snapshot.RoomId)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview(); UpdateActorLayer(); UpdateVerbArea(); UpdatePlaybackControls();
        }
    }, _game, () => IsAdvancedMode, _game?.Id == GameId.LoomVga ? null : LoomEgaStateFlags.Create(() => _replayCatalog?.Replays ?? [])) { Owner = this, Resources = Resources };


    private void GameStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_game?.Id is GameId.Loom or GameId.LoomVga && _session is not null) BuildGameStatusWindow().ShowDialog();
    }

    private void OpenGameFolder(string folder)
    {
        var game = _scanner.Scan(folder);
        var indexedSnapshot = game.Id == GameId.Loom && game.Version == ScummVersion.V3
            ? _stateReader.ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"))
            : null;
        _game = game;
        _glossary = game.Id is GameId.Loom or GameId.LoomVga ? LoomGlossary.Load(game) : LoomGlossary.Default;
        _usageIndex = LoomUsageIndex.Empty;
        if (game.Id is GameId.Loom or GameId.LoomVga) _ = BuildUsageIndexAsync(game);
        _replayCatalog = LoomReplayCatalog.Load(game);
        if (game.Id == GameId.Loom) _ = NameStoryBitsAsync(game, _glossary, _replayCatalog.Replays);
        _setupPlanner = null;
        _scriptReader = new ScummScriptReader(ScummProfile.For(game).Dialect);
        _verbReader = new ScummObjectVerbReader(ScummProfile.For(game).Dialect);
        _sceneBatches = null;
        _drawBatchesTask = null;
        if (game is { Id: GameId.Loom, Version: ScummVersion.V3 }) _ = DrawBatchesAsync(game); // the first room needs them
        _doorwayFinder = null;
        _indexedObjectSnapshot = indexedSnapshot;
        _indexedStates = indexedSnapshot?.States ?? new Dictionary<int, byte>();
        StopScenePlayback();
        _sceneSound.StopAll("another game was opened");
        _cdVoice.Stop();
        // The Roland device needs the upgrade's sounds in this folder; another folder falls back to AdLib.
        if (_sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland && !(SceneSoundPlayer.HasRolandSounds(game) && (_sceneSound.Roms ??= FindMt32Roms(game)) is not null))
        {
            // Not saved: the choice stays for a folder with the upgrade and the ROMs.
            _sceneSound.SetMode(SceneSoundPlayer.SoundMode.Adlib);
            _soundModeFallback = true;
            SoundModeCombo.SelectedIndex = (int)SceneSoundPlayer.SoundMode.Adlib;
            _soundModeFallback = false;
        }
        // The CD game gets a live session as well (clicks, walks, verbs); its entry points are the same scripts 005-007. It starts past
        // the intro (see LoomExplorationSession.ContinueCdBootAfterIntro) and has no sound or catalog yet.
        var cdIndexed = game is { Id: GameId.LoomVga, V4Archive: { } vga } ? vga.ReadIndexedObjects() : null;
        _cdData = cdIndexed is null ? null : ScummDataSources.For(game);
        _cdCutsceneGroups = null;
        StartCdSetupDerivation(game);
        if (cdIndexed is not null) _indexedStates = cdIndexed.States;
        _session = cdIndexed is not null ? new LoomExplorationSession(game, cdIndexed, completeBoot: false)
            : indexedSnapshot is null ? null : new LoomExplorationSession(game, indexedSnapshot, soundCard: SoundCard);
        if (_session is not null && cdIndexed is null) _sceneSound.Attach(game, _session);
        else if (_session is not null && game.V4Archive is { } cdGame) _cdVoice.Attach(cdGame.Disc, _session);
        _charsetError = null;
        _charset = game.Id == GameId.Loom ? ScummV3Charset.TryLoad(game.FolderPath, 0, out _charsetError) : null;
        _cdCharsets.Clear();
        try { _cdCharset = game.V4Archive?.ReadCharset(2); }
        catch (InvalidDataException error) { _cdCharset = null; _charsetError = $"charset 2 could not be read: {error.Message}"; }
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
            if (Directory.Exists(folder) || File.Exists(folder))
            {
                // Only reopening the last game may reuse the saved room thumbnails; the Open button rebuilds them.
                _roomCardsMayUseCache = true;
                try { OpenGameFolder(folder); }
                finally { _roomCardsMayUseCache = false; }
            }
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
        _roomVerbs = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
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
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        RoomEntryTraceOutput.Text = "";
        RoomEntryEventButtons.Children.Clear();
        RoomNavigationButtons.Children.Clear();
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomNavigationOverlay.Visibility = Visibility.Collapsed;
        DoorwayOptions.Visibility = ObjectAreaOptions.Visibility = Visibility.Collapsed;
        ObjectActionPanel.Visibility = Visibility.Collapsed;
        ObjectVerbButtons.Children.Clear();
        ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
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
        GameInfo.Text = game.Platform.StartsWith("DOS", StringComparison.Ordinal) ? "DOS edition" : game.Platform;
        GameInfo.ToolTip = $"{game.Version switch { ScummVersion.Unknown => "SCUMM version unconfirmed", _ => $"SCUMM {game.Version}" }} · {game.Platform}";
        ResourceTree.Items.Clear();
        RoomCardList.Children.Clear();
        RoomCardScroll.Visibility = Visibility.Visible;

        var rooms = new TreeViewItem { Header = $"Rooms  ({game.Scenes.Count()})", IsExpanded = false };
        foreach (var room in game.Scenes)
            rooms.Items.Add(new TreeViewItem { Header = LoomResourceNames.Label(_glossary, room), ToolTip = LoomResourceNames.Evidence(_glossary, room), Tag = room });
        ResourceTree.Items.Add(rooms);

        foreach (var (kind, label) in new[]
        {
            (ResourceKind.Costume, "Costumes"), (ResourceKind.Script, "Scripts"),
            (ResourceKind.Sound, "Sounds & music"), (ResourceKind.Executable, "Game files")
        })
        {
            if (game.Id != GameId.Loom && !(game.Id == GameId.LoomVga && kind is ResourceKind.Costume or ResourceKind.Sound or ResourceKind.Script) && kind != ResourceKind.Executable) continue;
            var group = game.Resources.Where(resource => resource.Kind == kind).ToArray();
            if (group.Length == 0) continue;
            var node = new TreeViewItem { Header = $"{label}  ({group.Length})", IsExpanded = false };
            foreach (var item in group)
                node.Items.Add(new TreeViewItem { Header = LoomResourceNames.Label(_glossary, item),
                    ToolTip = LoomResourceNames.Evidence(_glossary, item) ?? item.Description, Tag = item });
            ResourceTree.Items.Add(node);
        }

        if (game.Id == GameId.LoomVga && game.V4Archive is { Disc: not null } cdArchive)
        {
            // The disc's audio track: every stretch a script asks for with sayVoice. Long ones and those without a line are the music and narration.
            _cdCues = LoomCdCueIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value));
            var cueNode = new TreeViewItem { Header = $"CD audio  ({_cdCues.Count})", IsExpanded = false };
            for (var i = 0; i < _cdCues.Count; i++)
            {
                var cue = _cdCues[i];
                var kind = cue.IsMusicOrNarration ? "music / narration" : "voice";
                var resource = new GameResource($"CD audio {i + 1:000}", ResourceKind.Sound, game.FolderPath, CdCueIdBase + i,
                    $"{kind}, {cue.Cue.Seconds:0.#} s at disc second {cue.Cue.DiscSeconds:0.#}; first asked for by {cue.Source}.", 1, null);
                var snippet = cue.Text.Length > 40 ? cue.Text[..40] + "…" : cue.Text;
                cueNode.Items.Add(new TreeViewItem
                {
                    Header = $"{(cue.IsMusicOrNarration ? "♪ " : "")}{resource.Name} · {cue.Cue.Seconds:0.#} s{(snippet.Length > 0 ? " · " + snippet : "")}",
                    ToolTip = resource.Description, Tag = resource
                });
            }
            ResourceTree.Items.Add(cueNode);
        }

        PopulateRoomCards(game);
        PopulateCostumeCards(game);
        UpdateAdvancedMode();

        ShowPageHeader(true);
        PageTitle.Text = game.Title;
        var support = GameSupport.For(game);
        PageSubtitle.Text = support.Subtitle;
        PageSubtitle.ToolTip = game.DetectionNote;
        PreviewTitle.Text = "Choose a room";
        PreviewDescription.Text = support.EmptyPreview;
        StatusText.Text = "Ready";
        StatusText.ToolTip = $"{game.Scenes.Count()} indexed rooms found.";
        // Game Status needs the session, not a room: show it as soon as the game is loaded (the header then stays still).
        GameStatusButton.Visibility = game.Id is GameId.Loom or GameId.LoomVga && _session is not null ? Visibility.Visible : Visibility.Collapsed;
        // The CD game has no music device to choose (its music has not been read); its voices come from the disc's audio track (the CD Audio choice of its own sound box).
        SoundModeCombo.Visibility = game.Id == GameId.LoomVga ? Visibility.Collapsed : Visibility.Visible;
        CdSoundCombo.Visibility = game is { Id: GameId.LoomVga, V4Archive.Disc.AudioStartSector: not null } ? Visibility.Visible : Visibility.Collapsed;
        GameStatusButton.IsEnabled = _session is not null;
        FooterText.Text = $"{game.Title} · {game.Platform}";
        FooterText.ToolTip = game.FolderPath;
    }

    private void PopulateRoomCards(GameLibrary game)
    {
        _roomCardCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _roomCardCancellation = cancellation;
        var generation = ++_roomCardGeneration;
        var rooms = game.Scenes.ToArray();
        var indexedSnapshot = _indexedObjectSnapshot is null ? null : new ScummIndexedObjectSnapshot(
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

        // Reopening the last game reuses the saved thumbnails; opening with the button, or a missing or stale
        // cache, rebuilds them with the asynchronous loader and saves each one.
        var cached = _roomCardsMayUseCache
            ? RoomThumbnailCache.TryLoad(game.FolderPath, rooms.Select(room => room.ResourceId ?? -1)) : null;
        if (cached is not null)
        {
            foreach (var roomResource in rooms)
                if (cached.TryGetValue(roomResource.ResourceId ?? -1, out var thumbnail)) SetRoomCardThumbnail(roomResource, thumbnail);
            return;
        }
        RoomThumbnailCache.Begin(game.FolderPath);
        _ = LoadRoomCardThumbnailsAsync(game, rooms, indexedSnapshot, indexedStates,
            generation, cancellation.Token);
    }

    private async Task LoadRoomCardThumbnailsAsync(GameLibrary game, IReadOnlyList<GameResource> rooms,
        ScummIndexedObjectSnapshot? indexedSnapshot, IReadOnlyDictionary<int, byte> indexedStates,
        int generation, CancellationToken cancellationToken)
    {
        var sceneBatches = ScummSceneDrawBatches.Empty;
        if (game.Id == GameId.Loom && game.Version == ScummVersion.V3 && indexedSnapshot is not null)
        {
            try
            {
                sceneBatches = await DrawBatchesAsync(game).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception)
            {
                sceneBatches = ScummSceneDrawBatches.Empty;
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
            if (bitmap is not null && roomResource.ResourceId is int savedId && generation == _roomCardGeneration)
                RoomThumbnailCache.Save(game.FolderPath, savedId, bitmap);
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

    private Task<ScummSceneDrawBatches>? _drawBatchesTask;
    private GameLibrary? _drawBatchesGame;

    /// <summary>
    /// The game's script draw batches, built once on a worker thread and shared by the room cards, the first room and Game Texts
    /// (each used to build its own copy, the first room on the UI thread).
    /// </summary>
    private Task<ScummSceneDrawBatches> DrawBatchesAsync(GameLibrary game)
    {
        if (_drawBatchesTask is null || !ReferenceEquals(_drawBatchesGame, game))
        {
            _drawBatchesGame = game;
            _drawBatchesTask = Task.Run(() => ScummSceneDrawBatches.Build(game));
        }
        return _drawBatchesTask;
    }


    /// <summary>A room as the explorer first shows it (objects in their first-visit state, as on the room cards), at full size; null outside LOOM.</summary>
    private async Task<BitmapSource?> RenderFirstVisitRoomAsync(int roomId)
    {
        if (_game is not { Id: GameId.Loom, Version: ScummVersion.V3 } game || _indexedObjectSnapshot is null ||
            game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is not { } resource) return null;
        var snapshot = new ScummIndexedObjectSnapshot(
            _indexedObjectSnapshot.States.ToDictionary(pair => pair.Key, pair => pair.Value),
            _indexedObjectSnapshot.Owners.ToDictionary(pair => pair.Key, pair => pair.Value),
            _indexedObjectSnapshot.Classes?.ToDictionary(pair => pair.Key, pair => pair.Value));
        var states = new Dictionary<int, byte>(_indexedStates);
        var batchesTask = DrawBatchesAsync(game);
        try
        {
            var batches = await batchesTask;
            return await Task.Run(() =>
            {
                var bytes = File.ReadAllBytes(resource.FilePath);
                var room = new ScummV3EgaRoomDecoder().Decode(bytes);
                var records = new ScummRoomObjectDirectory().Decode(bytes);
                var objects = new ScummRoomObjects().Decode(bytes);
                var session = new LoomExplorationSession(game, snapshot);
                var visited = session.VisitRoom(resource);
                var initialized = CreateSessionRoomSelection(resource, room, records, objects, visited, states, batches);
                var picture = CreateBitmap(room.Width, room.Height, initialized.Selection.Compose(room), DisplayPalette(room, session.ShadowPalette));
                return (BitmapSource?)picture;
            });
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or InvalidOperationException) { return null; }
    }

    private static BitmapSource RenderRoomCardThumbnail(GameLibrary game, GameResource roomResource,
        ScummIndexedObjectSnapshot? indexedSnapshot, IReadOnlyDictionary<int, byte> indexedStates,
        ScummSceneDrawBatches sceneBatches, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (game is { Id: GameId.LoomVga, V4Archive: { } vga })
        {
            // First-visit state: the room's entry script run in a fresh session; the plain background where that cannot run.
            try
            {
                var cdScene = LoomCdRoomScene.FirstVisit(game, roomResource.ResourceId!.Value);
                return CreateRoomCardBitmap(cdScene.Room, cdScene.Compose(), cdScene.CameraX,
                    cdScene.ShadowPalette is { } shadow ? DisplayPalette(cdScene.Room, shadow) : cdScene.Room.Palette);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException)
            {
                var cdRoom = vga.DecodeRoom(roomResource.ResourceId!.Value);
                return CreateRoomCardBitmap(cdRoom, cdRoom.Pixels.ToArray(), 0);
            }
        }
        var bytes = File.ReadAllBytes(roomResource.FilePath);
        var decoder = new ScummV3EgaRoomDecoder();
        var room = decoder.Decode(bytes);
        var pixels = room.Pixels.ToArray();
        var cameraX = 0;
        IReadOnlyList<uint> palette = room.Palette;
        if (game.Id == GameId.Loom && game.Version == ScummVersion.V3 && indexedSnapshot is not null)
        {
            var records = new ScummRoomObjectDirectory().Decode(bytes);
            var objects = new ScummRoomObjects().Decode(bytes);
            var session = new LoomExplorationSession(game, indexedSnapshot);
            var snapshot = session.VisitRoom(roomResource);
            var initialized = CreateSessionRoomSelection(roomResource, room, records, objects,
                snapshot, indexedStates, sceneBatches);
            pixels = initialized.Selection.Compose(room);
            cameraX = session.CameraX ?? 0;
            palette = DisplayPalette(room, session.ShadowPalette);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return CreateRoomCardBitmap(room, pixels, cameraX, palette);
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

    private bool _restoringInteractiveView;

    private void AdvancedModeToggle_Click(object sender, RoutedEventArgs e)
    {
        // Normal browsing has no inspection-mode control: restore game input when
        // returning from Advanced, where the user can inspect stored artwork.
        if (!IsAdvancedMode && SessionModeToggle.IsChecked != true)
        {
            _restoringInteractiveView = true;
            try { SessionModeToggle.IsChecked = true; }
            finally { _restoringInteractiveView = false; }
        }
        if (!IsAdvancedMode && _selectedResource is { Kind: not (ResourceKind.Room or ResourceKind.Costume) })
        {
            var returnRoom = _lastViewedRoomResource ?? _game?.Rooms.FirstOrDefault();
            if (returnRoom is not null) SelectResourceInTree(returnRoom);
        }
        UpdateAdvancedMode();
    }

    private void UpdateAdvancedMode()
    {
        var advanced = IsAdvancedMode;
        // The hint line under the scene is for Advanced mode and the inspection view; the game's own screen needs none.
        ObjectsNote.Visibility = SessionModeToggle.IsChecked != true ? Visibility.Visible : Visibility.Collapsed;
        VerbStripBorder.ToolTip = advanced ? _verbScreenNote : null;
        CutsceneMenuButton.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        GameTextsButton.Visibility = advanced && _game is { } textGame && GameSupport.For(textGame).Texts ? Visibility.Visible : Visibility.Collapsed;
        var rootGrid = ContentPanel.Parent as Grid;
        var showRoomDetails = advanced && _selectedResource?.Kind == ResourceKind.Room;
        if (rootGrid is not null && rootGrid.ColumnDefinitions.Count > 2)
            rootGrid.ColumnDefinitions[2].Width = new GridLength(showRoomDetails ? 290 : 0);
        RoomObjectPanel.Visibility = showRoomDetails ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(ContentPanel, showRoomDetails ? 1 : 2);
        ResourceTree.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        UpdateNormalCatalog(advanced);
        RoomMaskToggle.Visibility = advanced && _currentRoom is not null ? Visibility.Visible : Visibility.Collapsed;
        WalkBoxToggle.Visibility = advanced && _currentRoom is not null && _currentWalkBoxes is not null
            ? Visibility.Visible : Visibility.Collapsed;
        SessionStatusText.Visibility = advanced && _session is not null && SessionModeToggle.IsChecked != true
            ? Visibility.Visible : Visibility.Collapsed;
        AdvanceFrameButton.Visibility = RestartButton.Visibility = advanced && _session is not null ? Visibility.Visible : Visibility.Collapsed;
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
        // Opening a scene can take seconds (a new game session runs the boot scripts, the first visit runs the room's): show it.
        if (e.NewValue is not TreeViewItem { Tag: GameResource { Kind: ResourceKind.Room } })
        {
            await ResourceTree_SelectedItemChangedCore(sender, e);
            return;
        }
        using var busy = BeginBusy(null);
        // The welcome text ("Choose a room") gives way to the scene's own at once; the blocking work comes after this paint.
        PreviewTitle.Text = "Scene";
        PreviewTitle.ToolTip = null;
        PreviewDescription.Text = "Loading the scene…";
        PreviewMessage.Visibility = Visibility.Visible;
        RoomDisplayLayout.Visibility = Visibility.Collapsed;
        // Paint the wait text now, before the blocking work below. A synchronous render pass keeps the body running as one call:
        // playback enters rooms through this handler and relies on that (it sets _playbackEnteringRoom around the call).
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        await ResourceTree_SelectedItemChangedCore(sender, e);
    }

    private int _busyCount;
    private IDisposable? _musicBusy;

    /// <summary>The wait cursor, and a short text over the room when <paramref name="text"/> is given, until the returned scope is disposed.</summary>
    private IDisposable BeginBusy(string? text)
    {
        _busyCount++;
        Mouse.OverrideCursor = Cursors.Wait;
        if (text is not null)
        {
            BusyBadgeText.Text = text;
            BusyBadge.Visibility = Visibility.Visible;
        }
        return new BusyScope(this);
    }

    private void EndBusy()
    {
        if (--_busyCount > 0) return;
        _busyCount = 0;
        Mouse.OverrideCursor = null;
        BusyBadge.Visibility = Visibility.Collapsed;
    }

    private sealed class BusyScope(MainWindow window) : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            window.EndBusy();
        }
    }

    /// <summary>A track the game started is still being rendered and the game waits for it (see <see cref="SceneSoundPlayer.IsPreparing"/>).</summary>
    private void SceneSound_PreparingChanged()
    {
        if (_sceneSound.IsPreparing) _scanGate.Reset(); else _scanGate.Set();
        if (_sceneSound.IsPreparing)
        {
            _musicBusy ??= BeginBusy(_sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland
                ? "Initialising the Roland MT-32 and preparing its music…" : "Preparing the music…");
        }
        else
        {
            _musicBusy?.Dispose();
            _musicBusy = null;
        }
    }

    private async Task ResourceTree_SelectedItemChangedCore(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Opening another view stops the playback clock, unless the playback itself entered the room.
        if (!_playbackEnteringRoom)
        {
            var leavingPreview = _playback is { Preview: true };
            if (SessionInScene) _sceneSound.StopAll("the cutscene was left from the catalog");
            if (leavingPreview) _sceneSound.StopAll("the demo's preview was left from the catalog");
            StopScenePlayback();
            // The opening is suspended boot code, rather than an open cutscene. A catalog
            // departure uses the ordinary post-boot exploration state so it cannot resume later.
            // The same for a demo's preview: its director script keeps running in that session, so leaving takes a fresh one.
            if (e.NewValue is TreeViewItem { Tag: GameResource { Kind: ResourceKind.Room } } &&
                (_session?.BootRunning == true || leavingPreview) && _game is not null && _indexedObjectSnapshot is not null)
            {
                _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, soundCard: SoundCard);
                _sceneSound.Attach(_game, _session);
                UpdatePlaybackControls();
            }
        }
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
        _roomVerbs = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
        _roomNavigationCandidates = [];
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        RoomEntryTraceOutput.Text = "";
        RoomEntryEventButtons.Children.Clear();
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomNavigationOverlay.Visibility = Visibility.Collapsed;
        DoorwayOptions.Visibility = ObjectAreaOptions.Visibility = Visibility.Collapsed;
        RoomMusicPanel.Visibility = Visibility.Collapsed;
        RoomPreview.ToolTip = null;
        RoomPreview.Cursor = Cursors.Arrow;
        ScriptEventButtons.Children.Clear();
        _selectedResource = resource;
        if (resource.Kind is ResourceKind.Room or ResourceKind.Costume)
            _showCostumeCards = resource.Kind == ResourceKind.Costume;
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
            DoorwayOptions.Visibility = ObjectAreaOptions.Visibility = Visibility.Collapsed;
            PreviewMessage.Visibility = Visibility.Collapsed;
            ScriptPanel.Visibility = Visibility.Visible;
            ShowPageHeader(true);
            PageTitle.Text = LoomResourceNames.Label(_glossary, resource);
            PageSubtitle.Text = _game?.Id == GameId.LoomVga ? "LOOM CD script · SCUMM v4" + (LoomResourceNames.Entry(_glossary, resource) is { } cdNamed ? $" · name: {cdNamed.EvidenceLabel}" : "") : LoomResourceNames.Entry(_glossary, resource) is { } named
                ? $"Indexed script resource · DOS LOOM old-v3 · name: {named.EvidenceLabel}" : "Indexed script resource · DOS LOOM old-v3";
            PageTitle.ToolTip = LoomResourceNames.Evidence(_glossary, resource) ?? resource.Description;
            UpdateScriptUsedBy(resource);
            ScriptOutput.Text = "";
            ScriptNote.Text = _game?.Id == GameId.LoomVga ? "Reading LOOM CD bytecode…" : "Reading DOS LOOM old-v3 bytecode…";
            var interior = _game is { Id: GameId.Loom } egaGame
                ? new LflResourceChain().FindLeftover(egaGame, resource) : null;
            var boundaryNote = interior is null ? "" : interior.Describe() +
                " This entry does not establish a standalone script.\n";
            try
            {
                var disassembly = _game?.Id == GameId.LoomVga && _cdData is not null
                    ? (resource.ScriptRoom is int scriptRoom ? _cdData.RoomLocalScript(scriptRoom, resource.ResourceId!.Value)
                        : _cdData.GlobalScript(resource.ResourceId!.Value)) ?? throw new InvalidDataException("This script is not stored in the CD data.")
                    : _scriptReader.Decode(resource);
                _scriptShown = disassembly;
                ScriptOutput.Text = FormatScript(disassembly);
                ScriptPlayButton.Visibility = ScriptCutscene(resource.ResourceId) is null ? Visibility.Collapsed : Visibility.Visible;
                var events = disassembly.Instructions
                    .Select(instruction => ScummScriptEvent.FromInstruction(disassembly, instruction,
                        ScummScriptEventCertainty.StaticPotential))
                    .Where(item => item is not null)
                    .Cast<ScummScriptEvent>()
                    .ToArray();
                ShowScriptEvents(ScriptEventButtons, events);
                ScriptNote.Text = boundaryNote + $"Control-flow decode: {disassembly.DecodedBytes}/{disassembly.CodeLength} resource bytes, " +
                    $"{disassembly.Issues?.Count ?? 0} unresolved paths. Event links are static candidates; branch choices and script order are not executed in this view.";
                StatusText.Text = $"{resource.Name} · {disassembly.Instructions.Count} instructions · " +
                    $"{disassembly.Issues?.Count ?? 0} decode issues";
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                _scriptShown = null;
                ScriptPlayButton.Visibility = Visibility.Collapsed;
                ScriptNote.Text = boundaryNote + "This indexed script resource could not be decoded.";
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
        ShowPageHeader(false); // the scene needs the height more than a title that only repeats the mode
        PageTitle.Text = "Explore the scene";
        PageTitle.ToolTip = resource.Name;
        PageSubtitle.Text = "Original artwork from the game";
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        PageSubtitle.ToolTip = resource.FilePath;
        PreviewTitle.Text = "Scene";
        PreviewTitle.ToolTip = resource.Name;
        PreviewDescription.Text = "Loading the scene…";
        PreviewMessage.Visibility = Visibility.Visible;
        StatusText.Visibility = Visibility.Visible;
        _pendingCameraX = null;
        RoomViewport.ScrollToHorizontalOffset(0);
        RoomPreview.Source = null;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.IsEnabled = false;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        WalkBoxToggle.Visibility = Visibility.Collapsed;
        WalkBoxOverlay.Visibility = Visibility.Collapsed;
        DoorwayOptions.Visibility = ObjectAreaOptions.Visibility = Visibility.Collapsed;
        ClearObjectList();
        ObjectsNote.Text = "Reading room objects…";
        _currentRoom = null;
        _objects = [];
        _objectSelection = new([]);
        _currentRoomResource = null;
        _currentWalkBoxes = null;
        _cdScene = null;
        _currentRoomObjectRecords = [];
        _roomObjectNames = new Dictionary<int, string>();
        _roomVerbs = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
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

        var isCd = _game is { Id: GameId.LoomVga, V4Archive: not null };
        if (!isCd && (_game is not { Version: ScummVersion.V3, Id: GameId.Loom or GameId.IndianaJones3 or GameId.Unknown } ||
            !_roomDecoder.CanDecode(resource.FilePath, 3)))
        {
            PreviewDescription.Text = "This room format is not supported yet.";
            return;
        }

        try
        {
            // The CD game's rooms come out of DISK01.LEC (no per-room file); the others read their LFL file.
            var bytes = isCd ? [] : await File.ReadAllBytesAsync(resource.FilePath, token);
            LoomCdScene? cdScene = null;
            if (isCd && _session is null)
            {
                var cdGame = _game!;
                try { cdScene = await Task.Run(() => LoomCdRoomScene.FirstVisit(cdGame, resource.ResourceId!.Value), token); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException) { }
            }
            var room = cdScene?.Room ?? (isCd ? await Task.Run(() => _game!.V4Archive!.DecodeRoom(resource.ResourceId!.Value), token) : _roomDecoder.Decode(bytes));
            ScummRoomData? cdRoom = isCd && _session is not null && _cdData is not null
                ? await Task.Run(() => _cdData.ReadRoom(resource.ResourceId!.Value), token) : null;
            _currentWalkBoxes = isCd ? cdScene?.WalkBoxes ?? cdRoom?.WalkBoxes : TryReadWalkBoxes(bytes);
            _cdScene = cdScene;
            if (_game?.Id is GameId.IndianaJones3 or GameId.Unknown || isCd && cdRoom is null)
            {
                if (token.IsCancellationRequested) return;
                _currentRoom = room;
                _currentRoomResource = resource;
                _lastViewedRoomResource = resource;
                _currentRoomObjectRecords = cdScene?.Records ?? [];
                _roomVerbs = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
                _roomNavigationCandidates = [];
                _objects = cdScene?.Images ?? [];
                _objectSelection = cdScene?.Selection ?? new RoomObjectSelection([], background: room);
                _estimatedObjectIds = new HashSet<int>();
                _selectedObject = null;
                ClearObjectList();
                ObjectsNote.Text = isCd ? CdSceneNote(cdScene) : "Background preview only. Object art, scripts, and room routes are not decoded for this game yet.";
                ObjectActionPanel.Visibility = Visibility.Collapsed;
                RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
                RoomMusicPanel.Visibility = Visibility.Collapsed;
                UpdateRoomNavigationActions(resource);
                RefreshRoomPreview();
                UpdateActorLayer();
                UpdateCurrentRoomCardThumbnail();
                RoomDisplayLayout.Visibility = Visibility.Visible;
                // The view starts where the entry script left the camera (room 4's Bobbin stands at x 580).
                if (cdScene is not null) _pendingCameraX = cdScene.CameraX;
                UpdateRoomViewportLayout();
                StatusText.HorizontalAlignment = HorizontalAlignment.Center;
                StatusText.Visibility = Visibility.Collapsed;
                RoomPreview.Visibility = Visibility.Visible;
                RoomMaskToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
                ShowWalkBoxToggle();
                PreviewMessage.Visibility = Visibility.Collapsed;
                UpdateAdvancedMode();
                PageSubtitle.Text = "Original artwork from the game";
                StatusText.Text = "Ready";
                StatusText.ToolTip = "Room background read from the game's own data.";
                return;
            }
            var objectRecords = cdRoom?.Objects ?? _objectDirectory.Decode(bytes);
            var objects = cdRoom is null ? _objectDecoder.Decode(bytes) : _cdData!.ReadObjectImages(resource.ResourceId!.Value);
            if (token.IsCancellationRequested) return;
            var catalogStart = PrepareCatalogStart(resource);
            if (catalogStart?.Failure is { } setupFailure)
            {
                _session!.RecordCatalogPreparation($"Preparation stopped: {setupFailure}. {catalogStart.Note}");
                PreviewTitle.Text = "Could not prepare this scene";
                PreviewDescription.Text = "The earlier story scene did not finish. Choose another room or restart the session.";
                PreviewMessage.Visibility = Visibility.Visible;
                StatusText.Text = "Story preparation stopped";
                StatusText.ToolTip = setupFailure;
                return; // A failed source stage must not be followed by the late-room visit.
            }
            _currentRoom = room;
            _currentRoomResource = resource;
            _lastViewedRoomResource = resource;
            _currentRoomObjectRecords = objectRecords;
            _roomVerbs = cdRoom is null ? _verbReader.ReadAll(bytes, objectRecords) : _cdData!.ReadObjectVerbs(resource.ResourceId!.Value, objectRecords);
            _roomObjectNames = cdRoom is null ? LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), objectRecords)
                : cdRoom.ObjectNames.ToDictionary(pair => pair.Key, pair => System.Text.Encoding.Latin1.GetString(pair.Value));
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
            // A catalog visit can start scripts which yield before their music/arrival work.
            // Use the ordinary bounded clock once the room is visible, without inventing input.
            if (GameClickActive && !_playbackEnteringRoom && _playback is null && _session?.Snapshot.PendingScripts > 0)
                StartPlayback("Catalog arrival frames (arrival assumed)", null, resource, doorways: null, leftRoom: false);
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

    private static string CdSceneNote(LoomCdScene? scene) =>
        scene is null
            ? "Background only: the room's entry script could not run. Bobbin, interaction and room routes are not read yet for the CD game."
            : "The room as its entry script leaves it (first visit)" +
              (scene.Snapshot.Picks is { Count: > 0 } ? $"; where the script branches on a random value, the explorer picked one ({scene.Snapshot.Picks.Count} seeded picks, listed in the session notes)" : "") +
              (scene.Actors.Any(actor => actor.Number == 1 && actor.Room == scene.Snapshot.RoomId)
                  ? "; Bobbin stands where an assumed arrival would bring him (an estimate: the route to the room was not played)" : "") +
              ". Interaction and room routes are not read yet for the CD game.";

    private LoomCatalogStartResult? PrepareCatalogStart(GameResource resource)
    {
        _pendingCatalogPreparation = null;
        if (SessionModeToggle.IsChecked != true || _session is null || _playbackEnteringRoom || _replaySequence is not null ||
            _replayCatalog?.CatalogStarts.FirstOrDefault(start => start.Room == resource.ResourceId) is not { } recipe)
            return null;
        // Human-approved source scenes; edition-specific operands are in fingerprinted recipe data.
        _sceneSound.StopAll("catalog story preparation runs unseen");
        var result = LoomCatalogStartPreparer.Prepare(_session, recipe);
        _sceneSound.Attach(_game!, _session);
        if (result.Prepared) _pendingCatalogPreparation = result.Note;
        return result;
    }

    private void ApplyRoomState(GameResource resource, IReadOnlyList<ScummRoomObjectRecord> records,
        IReadOnlyList<RoomObjectImage> images, bool visit)
    {
        _estimatedObjectIds = new HashSet<int>();
        RoomEntryEventButtons.Children.Clear();
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            if (visit) FollowSessionSound();
            if (visit) _issueBaseline = _session.Snapshot.Uncertainties.Count;
            var snapshot = visit ? _session.VisitRoom(resource, preserveStaffLevel: !_playbackEnteringRoom && _replaySequence is null) : _session.Snapshot;
            if (visit && _pendingCatalogPreparation is { } preparation)
            {
                _pendingCatalogPreparation = null;
                _session.RecordCatalogPreparation(preparation);
                snapshot = _session.Snapshot;
            }
            if (visit && _game?.Id == GameId.LoomVga && _cdVoice.Follows(_session)) _cdVoice.SkipPending();
            var sceneBatches = snapshot.IsFaithful ? _sceneBatches ?? ScummSceneDrawBatches.Empty : EnsureSceneBatches();
            var initialized = CreateSessionRoomSelection(resource, _currentRoom!, records, images, snapshot,
                _indexedStates, sceneBatches, estimateArtwork: _game?.Id != GameId.LoomVga);
            _objectSelection = initialized.Selection;
            _artworkEstimates = initialized.Estimates;
            _estimatedObjectIds = _artworkEstimates.Select(item => item.ObjectId).ToHashSet();
            var run = _session.LastEntryRun;
            var script = _session.LastEntryScript;
            RoomEntryTraceOutput.Text = (script is null ? "No embedded entry script." : FormatScript(script)) +
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
            RoomEntryTraceExpander.Header = "Room entry";
            RoomEntryTraceExpander.Visibility = Visibility.Visible;
            RoomEntrySummary.Text = $"{script?.Instructions.Count ?? 0} instructions · camera x {(_session.CameraX?.ToString() ?? "unknown")}{(_session.CameraEstimated ? " (estimate)" : "")} · " +
                                    (snapshot.ActiveSounds.Count == 0 ? "no script-started sounds" : "sounds " + string.Join(", ", snapshot.ActiveSounds));
            ShowScriptEvents(RoomEntryEventButtons, _session.LastVisitEvents);
            RefreshStateIssues();
            UpdateRoomMusicActions();
            SessionStatusText.Text = snapshot.IsFaithful
                ? "Exploration state ready."
                : "Exploration state is incomplete. Hover for details.";
            SessionStatusText.ToolTip = snapshot.Uncertainties.Count == 0
                ? "A script may be waiting for the next frame."
                : string.Join(Environment.NewLine, snapshot.Uncertainties.TakeLast(8));
            UpdatePlaybackControls();
            ObjectsNote.Text = _estimatedObjectIds.Count == 0 || !IsAdvancedMode
                ? "Click the scene to explore it. Hover to discover objects and exits."
                : $"Some scene details are previews; the game state remains unresolved ({_estimatedObjectIds.Count} details).";
            ShowLastObjectAction(resource.ResourceId);
            UpdateAdvancedMode();
            return;
        }

        ObjectActionPanel.Visibility = Visibility.Collapsed;
        SessionStatusText.Text = "Inspection mode: a click cycles an object's stored images; it is not the game's click and runs no script. Each room starts from the index; changes are local to this view.";
        SessionStatusText.ToolTip = null;
        RoomEntryTraceExpander.Header = "Room entry";
        RefreshStateIssues();
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
                RoomEntrySummary.Text = $"{entryScript.Instructions.Count} instructions" + (run.UnresolvedReason is null && !run.ResumeOffset.HasValue ? " · reached the end" : " · stopped early (see the raw disassembly)");
                _objectSelection = new RoomObjectSelection(images, run.ObjectStates,
                    run.ObjectPositions, background: _currentRoom, drawQueue: run.DrawOrder);
                if (run.UnresolvedReason is not null || run.ResumeOffset.HasValue)
                    CloseArtworkGaps(resource, images);
                RoomEntryTraceOutput.Text = FormatScript(entryScript) + Environment.NewLine +
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
            _sceneBatches ?? ScummSceneDrawBatches.Empty);
        _estimatedObjectIds = _artworkEstimates.Select(item => item.ObjectId).ToHashSet();
    }

    private ScummSceneDrawBatches EnsureSceneBatches()
    {
        if (_sceneBatches is not null) return _sceneBatches;
        try { _sceneBatches = _game is null ? ScummSceneDrawBatches.Empty : DrawBatchesAsync(_game).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _sceneBatches = ScummSceneDrawBatches.Empty;
        }
        return _sceneBatches;
    }

    private static (RoomObjectSelection Selection, IReadOnlyList<RoomArtworkEstimate> Estimates) CreateSessionRoomSelection(
        GameResource resource, RoomImage room, IReadOnlyList<ScummRoomObjectRecord> records,
        IReadOnlyList<RoomObjectImage> images, LoomSessionSnapshot snapshot,
        IReadOnlyDictionary<int, byte> indexedStates, ScummSceneDrawBatches sceneBatches, bool estimateArtwork = true)
    {
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates,
            snapshot.ObjectPositions, records, room, indexedStates, snapshot.DrawOrder);
        // The CD game's backgrounds carry their scenery and have no cut-out gaps; an estimate would only paint a
        // stored image the game did not draw (the 48 x 40 frames at the top left of room 4 after its cutscene).
        IReadOnlyList<RoomArtworkEstimate> estimates = snapshot.IsFaithful || !estimateArtwork
            ? Array.Empty<RoomArtworkEstimate>()
            : FillArtworkGaps(selection, resource, images, snapshot.ArtworkStateIds(), sceneBatches);
        return (selection, estimates);
    }

    private static IReadOnlyList<RoomArtworkEstimate> FillArtworkGaps(RoomObjectSelection selection,
        GameResource resource, IReadOnlyList<RoomObjectImage> images,
        IReadOnlyCollection<int>? statesSetByCode, ScummSceneDrawBatches sceneBatches)
    {
        var roomId = resource.ResourceId ?? 0;
        selection.ExcludeInterfaceImages(sceneBatches.InterfaceImages.Keys);
        var estimates = selection.FillGaps(sceneBatches.ForRoom(roomId), seed: roomId);
        if (!images.Any(selection.IsActive))
            estimates = [.. estimates, .. selection.ShowUnresolvedArtwork(sceneBatches.ForRoom(roomId), seed: roomId, statesSetByCode,
                sceneBatches.EntryStatesForRoom(roomId))];
        return estimates;
    }

    private static BitmapSource CreateRoomCardBitmap(RoomImage room, byte[] pixels, int cameraX, IReadOnlyList<uint>? palette = null)
    {
        var visibleWidth = Math.Min(320, room.Width);
        var left = Math.Clamp(cameraX - visibleWidth / 2, 0, room.Width - visibleWidth);
        var full = CreateBitmap(room.Width, room.Height, pixels, palette ?? room.Palette);
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
        SetRoomCardThumbnail(resource, CreateRoomCardBitmap(room, _objectSelection.Compose(room), cameraX, DisplayPalette(room)));
    }

    private void SetRoomCardThumbnail(GameResource resource, BitmapSource bitmap)
    {
        if (Equals(_currentRoomResource, resource) && _currentRoom is { } liveRoom)
        {
            var liveCameraX = SessionModeToggle.IsChecked == true && _session is not null
                ? _session.CameraX ?? 0
                : _pendingCameraX ?? 0;
            bitmap = CreateRoomCardBitmap(liveRoom, _objectSelection.Compose(liveRoom), liveCameraX, DisplayPalette(liveRoom));
        }
        var card = RoomCardList.Children.OfType<Button>().FirstOrDefault(item => Equals(item.Tag, resource));
        if (card is null) return;
        if (card.Content is Image preview) preview.Source = bitmap;
        else card.Content = CreateRoomCardImage(bitmap, LoomResourceNames.Label(_glossary, resource));
        card.ToolTip = $"Open {LoomResourceNames.Label(_glossary, resource)}";
    }

    private string EstimateSummary()
    {
        var sources = _artworkEstimates.Select(item => item.Reason).Distinct().ToArray();
        return sources.Length == 0 ? "" : $" (from {string.Join("; ", sources.Take(2))}{(sources.Length > 2 ? "; …" : "")})";
    }

    private void SessionMode_Changed(object sender, RoutedEventArgs e)
    {
        _gamePointerTarget = null;
        _session?.SetPointer(null);
        StopScenePlayback();
        if (SessionModeToggle.IsChecked != true) _sceneSound.StopAll("the interactive mode was turned off");
        if (SessionModeToggle.IsChecked != true) _cdVoice.Stop();
        if (_currentRoomResource is not { } room || _currentRoom is null) return;
        ApplyRoomState(room, _currentRoomObjectRecords, _objects,
            visit: SessionModeToggle.IsChecked == true && !_restoringInteractiveView);
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
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, soundCard: SoundCard);
        _sceneSound.Attach(_game, _session);
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
        FollowSessionSound();
        var snapshot = _session.AdvanceFrame();
        UpdateSessionSound();
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

    /// <summary>Frames an exit click may continue outside a cutscene before retrying an eligible doorway verb.</summary>
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
        // "Action tried; the result is unresolved" is for Advanced mode; the scene itself shows what happened.
        if (!IsAdvancedMode || action is null || roomId is null ||
            action.SourceRoomId != roomId && action.DestinationRoomId != roomId)
        {
            ObjectActionsSection.Visibility = Visibility.Collapsed;
            ObjectActionPanel.Visibility = Visibility.Collapsed;
            return;
        }
        ObjectActionPanel.Visibility = Visibility.Visible;
        ObjectVerbButtons.Children.Clear();
        ObjectActionsList.Children.Clear();
        ObjectActionsSection.Visibility = Visibility.Collapsed;
        var actionName = _currentRoomObjectRecords.FirstOrDefault(record => record.Id == action.ObjectId) is { } actionObject &&
                         _roomObjectNames.TryGetValue(actionObject.Id, out var storedName)
            ? storedName : "Something in the scene";
        ObjectActionNote.Text = action.DestinationRoomId is int target
            ? action.IsGameInput
                ? $"{actionName}: a new scene opened. The game walk remains a preview."
                : $"{actionName}: a new scene opened in this state preview."
            : action.VerbId == 0 ? $"{actionName}: ran; no scene change followed."
            : $"{actionName} · {LoomVerbNames.Label(action.VerbId)}: ran; no scene change followed.";
        ObjectActionNote.ToolTip = (action.DestinationRoomId is int destination ? $"Room {destination:00} · " : "") + action.Evidence;
        LastActionSummary.Text = ObjectActionNote.Text;
        LastActionIssues.Children.Clear();
        RefreshStateIssues();
        var timeline = LoomActionTimeline.Format(LoomActionTimeline.Build(action, new LoomScriptPresenter(_glossary, LoomVerbNames.LabelOrNull), actionName,
            action.VerbId == 0 ? null : LoomVerbNames.Label(action.VerbId)));
        LastActionTimeline.Text = timeline.TrimEnd();
        ObjectActionTraceOutput.Text = action.Evidence + Environment.NewLine +
            string.Join(Environment.NewLine, action.Uncertainties.Select(item => "Unresolved: " + item)) +
            Environment.NewLine + (action.Script is null ? "Object bytecode unavailable." : FormatScript(action.Script)) +
            Environment.NewLine + (action.Run?.FormatSummary() ?? "No object bytecode was run.");
        ObjectActionTraceExpander.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateAdvancedMode();
    }

    /// <summary>
    /// The verbs an object offers, as flat rows named by what they do. In Advanced mode they sit in the right panel under the hotspots
    /// (where the object is listed); without it the right panel is hidden, so they stay under the scene.
    /// </summary>
    private void ShowObjectVerbChoices(IReadOnlyList<RoomVerbChoice> choices)
    {
        ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
        ObjectVerbButtons.Children.Clear();
        ObjectActionsList.Children.Clear();
        var inPanel = IsAdvancedMode && choices.Count > 0;
        ObjectActionPanel.Visibility = inPanel ? Visibility.Collapsed : Visibility.Visible;
        ObjectActionsSection.Visibility = inPanel ? Visibility.Visible : Visibility.Collapsed;
        var objectName = choices.Count > 0 && _roomObjectNames.TryGetValue(choices[0].Object.Id, out var storedObjectName)
            ? storedObjectName : "This detail";
        ObjectActionsHeader.Text = $"ACTIONS · {objectName}";
        ObjectActionNote.Text = choices.Count == 0
            ? "No action is available here yet."
            : $"{objectName} is part of this scene. Choose an action to explore it.";
        var host = inPanel ? ObjectActionsList : ObjectVerbButtons;
        foreach (var choice in choices.OrderBy(item => LoomVerbNames.Order(item.Verb.VerbId)))
        {
            var button = new Button
            {
                Content = LoomVerbNames.Label(choice.Verb.VerbId),
                ToolTip = $"Object {choice.Object.Id} · verb 0x{choice.Verb.VerbId:X2} · code 0x{choice.Verb.ScriptOffset:X4}. " +
                          "This directly runs the object verb; input dispatch and walking are not simulated.",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 3),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) => ActivateObjectVerb(choice.Object, choice.Verb, play: true);
            host.Children.Add(button);
        }
        if (inPanel) AddObjectUsedBy(choices[0].Object.Id);
    }

    /// <summary>"Used by" for an object: the code that runs one of its verbs with a literal number.</summary>
    private void AddObjectUsedBy(int objectId)
    {
        var uses = _usageIndex.UsedBy(LoomUsageTarget.Object, objectId);
        var text = uses.Count == 0
            ? "Used by: no code names this object (clicks and drafts reach it through the input scripts)."
            : "Used by: " + string.Join("; ", uses.Take(6).Select(use => LoomUsageIndex.Describe(use, _glossary, VerbNameOrNull))) + (uses.Count > 6 ? $"; {uses.Count - 6} more" : "");
        ObjectActionsList.Children.Add(new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(Color.FromRgb(159, 171, 174)), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        });
    }

    /// <summary>
    /// The derived speaker placements (<see cref="LoomSceneSetupPlanner"/>, the "actor" steps only) for an object verb that prints lines for
    /// an actor who is not in the room. Returns the labelled notes; nothing happens outside LOOM or while the session cannot be edited.
    /// </summary>
    private List<string> PlaceSpeakersForDirectVerb(GameResource room, int objectId, int verbId)
    {
        var notes = new List<string>();
        if (_game is null || _session is null || _game.Id is not (GameId.Loom or GameId.LoomVga) || room.ResourceId is not int roomId || !_session.GameStatus.CanEdit) return notes;
        _setupPlanner ??= new LoomSceneSetupPlanner(_game);
        var setup = _setupPlanner.DeriveActors(roomId, objectId, verbId);
        foreach (var step in setup.Steps.Where(item => item.Kind == "actor"))
        {
            try { _session.PlaceActorInRoom(step.ObjectId, step.X, step.Y, step.Verb, step.EndBoundary ?? "the game's code"); }
            catch (InvalidOperationException) { continue; }
        }
        notes.AddRange(setup.Notes.Where(note => note.Contains("an actor outside the room is not shown", StringComparison.Ordinal)));
        return notes;
    }

    private bool ActivateObjectVerb(ScummRoomObjectRecord record, ScummObjectVerbEntry verb, bool play = false)
    {
        if (_session is null || _currentRoomResource is not { } sourceRoom || _game is null) return false;
        FollowSessionSound();
        // A verb run from the interaction menu has no scene start of its own, so the speakers its lines need (an actor who is not in the
        // room shows nothing) are placed as the catalog's start setup does: only the derived actor placements, never a pickup or a scene.
        var speakerNotes = play ? PlaceSpeakersForDirectVerb(sourceRoom, record.Id, verb.VerbId) : [];
        var action = _session.ActivateObjectVerb(record.Id, verb.VerbId);
        UpdateSessionSound();
        StatusText.Text = action.DestinationRoomId is int destination
            ? $"Entered Room {destination:00}"
            : _session.Snapshot.PendingScripts > 0
                ? "No room load reached; the session has pending scripts."
                : "Interaction finished";
        StatusText.ToolTip = action.Evidence + (action.Uncertainties.Count == 0 ? "" :
            Environment.NewLine + string.Join(Environment.NewLine, action.Uncertainties)) +
            (speakerNotes.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, speakerNotes));
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
        // The scene goes on by itself on the playback clock (otherwise its frames only moved when the pointer did).
        if (play) StartPlayback($"Object {record.Id}'s verb 0x{verb.VerbId:X2} (run directly)", action, sourceRoom, doorways: null, leftRoom: false);
        return false;
    }

    /// <summary>
    /// The events a script or room entry causes, digestibly: one flat row per thing it starts or opens (grouped, with a count,
    /// and a click opens it), then one line that counts the rest (character actions, dialogue...) with the full list in its tooltip.
    /// </summary>
    private void ShowScriptEvents(StackPanel container, IReadOnlyList<ScummScriptEvent> events)
    {
        container.Children.Clear();
        var rows = new List<(GameResource Target, string Verb, string Certainty, int Count)>();
        var others = new List<ScummScriptEvent>();
        foreach (var scriptEvent in events)
        {
            var target = FindEventTarget(scriptEvent);
            if (target is null) { others.Add(scriptEvent); continue; }
            var verb = scriptEvent.Kind switch
            {
                ScummScriptEventKind.ScriptStarted or ScummScriptEventKind.ObjectScriptStarted => "starts",
                ScummScriptEventKind.ScriptChained => "continues in",
                ScummScriptEventKind.MusicStarted or ScummScriptEventKind.SoundStarted => "plays",
                ScummScriptEventKind.RoomLoaded => "goes to",
                _ => "uses"
            };
            var certainty = scriptEvent.Certainty switch
            {
                ScummScriptEventCertainty.Reached => "",
                ScummScriptEventCertainty.Conditional => "maybe",
                _ => "possibly"
            };
            var index = rows.FindIndex(row => row.Target == target && row.Verb == verb && row.Certainty == certainty);
            if (index >= 0) rows[index] = rows[index] with { Count = rows[index].Count + 1 };
            else rows.Add((target, verb, certainty, 1));
        }
        foreach (var (target, verb, certainty, count) in rows)
        {
            var label = $"{(certainty.Length > 0 ? certainty + " " : "")}{verb} {LoomResourceNames.Label(_glossary, target)}{(count > 1 ? $" ×{count}" : "")}";
            var button = new Button
            {
                Content = label,
                ToolTip = $"Open {target.Name}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Background = new SolidColorBrush(Color.FromRgb(31, 44, 56)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) => SelectResourceInTree(target);
            container.Children.Add(button);
        }
        if (others.Count > 0)
        {
            var counts = others.GroupBy(item => item.Kind switch
            {
                ScummScriptEventKind.CharacterAction => "character action",
                ScummScriptEventKind.Dialogue when !item.Operands.Contains("text, ", StringComparison.Ordinal) =>
                    item.Operands.Contains("sayVoice, ", StringComparison.Ordinal) ? "CD audio cue" : "text layout",
                ScummScriptEventKind.Dialogue => "line of dialogue",
                ScummScriptEventKind.ObjectStateChanged => "object change",
                ScummScriptEventKind.CutsceneStarted or ScummScriptEventKind.CutsceneEnded => "cutscene mark",
                ScummScriptEventKind.ScriptsFrozen => "freeze",
                _ => "other event"
            }).Select(group => $"{group.Count()} {group.Key}{(group.Count() > 1 ? "s" : "")}");
            container.Children.Add(new TextBlock
            {
                Text = "Also: " + string.Join(", ", counts),
                ToolTip = string.Join(Environment.NewLine, others.Take(40).Select(item => item.Format())),
                Foreground = new SolidColorBrush(Color.FromRgb(159, 171, 174)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4, 2, 2)
            });
        }
    }

    /// <summary>The state the session could not settle, one short line each (the full text in the tooltip), in the inspector's own section.</summary>
    private int _issueBaseline;

    private void RefreshStateIssues()
    {
        StateIssueList.Children.Clear();
        if (_session is null || SessionModeToggle.IsChecked != true) { StateIssuesSection.Visibility = Visibility.Collapsed; return; }
        var issues = new List<string>();
        if (_estimatedObjectIds.Count > 0) issues.Add($"The artwork of {_estimatedObjectIds.Count} object(s) is estimated.");
        // The session logs everything it assumed since it began; this view lists what came after the last room entry.
        var log = _session.Snapshot.Uncertainties;
        issues.AddRange(log.Skip(Math.Min(_issueBaseline, log.Count)).Distinct().TakeLast(30));
        StateIssuesSection.Header = issues.Count == 0 ? "No assumptions" : $"Assumptions · {issues.Count}";
        if (issues.Count > 0)
            StateIssueList.Children.Add(new TextBlock
            {
                Text = "What the explorer had to assume or estimate to show this room (clicks sent as assumed input, camera or artwork estimates). Nothing is waiting on you.",
                Foreground = new SolidColorBrush(Color.FromRgb(159, 171, 174)), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
            });
        // Flat sections instead of nested expanders: a title with its count, one sentence of meaning, then the first few lines
        // (two lines each, full text in the tooltip) with a link to reveal the rest.
        const int visibleItems = 4;
        var dim = new SolidColorBrush(Color.FromRgb(159, 171, 174));
        foreach (var group in LoomAssumptionGroups.Group(issues))
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            section.Children.Add(new TextBlock
            {
                Text = $"{group.Title} ({group.Items.Count})", FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)), Margin = new Thickness(0, 0, 0, 2)
            });
            section.Children.Add(new TextBlock { Text = group.Meaning, Foreground = dim, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            var lines = new List<TextBlock>();
            foreach (var issue in group.Items)
            {
                var named = LoomResourceNames.NameBits(_glossary, LoomResourceNames.NameScripts(_glossary, issue));
                var line = new TextBlock
                {
                    Text = "• " + named, ToolTip = issue, Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)), FontSize = 11,
                    TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 30,
                    Margin = new Thickness(8, 0, 0, 3), Visibility = lines.Count < visibleItems ? Visibility.Visible : Visibility.Collapsed
                };
                lines.Add(line); section.Children.Add(line);
            }
            if (lines.Count > visibleItems)
            {
                var more = new TextBlock
                {
                    Text = $"Show {lines.Count - visibleItems} more", Foreground = new SolidColorBrush(Color.FromRgb(183, 132, 66)), FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), TextDecorations = TextDecorations.Underline
                };
                more.MouseLeftButtonUp += (_, _) =>
                {
                    bool expand = lines[visibleItems].Visibility != Visibility.Visible;
                    for (int i = visibleItems; i < lines.Count; i++) lines[i].Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                    more.Text = expand ? "Show fewer" : $"Show {lines.Count - visibleItems} more";
                };
                section.Children.Add(more);
            }
            StateIssueList.Children.Add(section);
        }
        StateIssuesSection.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private GameResource? FindEventTarget(ScummScriptEvent scriptEvent)
    {
        if (scriptEvent.TargetKind is not (ResourceKind.Sound or ResourceKind.Script or ResourceKind.Room) ||
            scriptEvent.TargetId is not int resourceId) return null;
        return _game?.Resources.FirstOrDefault(resource =>
            resource.Kind == scriptEvent.TargetKind && resource.ResourceId == resourceId &&
            (resource.ScriptRoom is null || resource.ScriptRoom == EventSourceRoom(scriptEvent)));
    }

    private int? EventSourceRoom(ScummScriptEvent ev) =>
        System.Text.RegularExpressions.Regex.Match(ev.SourceLabel, @"^Room (\d+)") is { Success: true } match
            ? int.Parse(match.Groups[1].Value) : _scriptShown?.Context is { } context &&
              System.Text.RegularExpressions.Regex.Match(context, @"^Room (\d+)") is { Success: true } scriptMatch
                ? int.Parse(scriptMatch.Groups[1].Value) : _session?.Snapshot.RoomId;

    private void StartRelationshipIndexBuild(GameLibrary game)
    {
        var generation = Interlocked.Increment(ref _relationshipBuildGeneration);
        if (game.Id != GameId.LoomVga && (game.Id != GameId.Loom || game.Version != ScummVersion.V3))
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

    // The bytecode column choice is kept from script to script (and while the app runs).
    private bool _showScriptBytes;
    private LoomGlossary _glossary = LoomGlossary.Default;
    private LoomUsageIndex _usageIndex = LoomUsageIndex.Empty;

    private async Task BuildUsageIndexAsync(GameLibrary game)
    {
        try
        {
            var index = await Task.Run(() => LoomUsageIndex.Build(game));
            if (!ReferenceEquals(_game, game)) return;
            _usageIndex = index;
            if (_selectedResource is { Kind: ResourceKind.Script } selected) UpdateScriptUsedBy(selected);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }

    /// <summary>Names the story bits after the catalog scene that sets them (about a second of scanning, so off the UI thread).</summary>
    private async Task NameStoryBitsAsync(GameLibrary game, LoomGlossary glossary, ReplayRecipe[] recipes)
    {
        try
        {
            var labels = await Task.Run(() => LoomEgaStateFlags.BitLabels(LoomStoryEvents.Read(game), recipes));
            if (!ReferenceEquals(_game, game)) return;
            glossary.StoryBitLabel = bit => labels.GetValueOrDefault(bit);
            if (_scriptShown is { } shown) ScriptOutput.Text = FormatScript(shown);
            RefreshStateIssues();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }

    private Func<int, string?> VerbNameOrNull => LoomVerbNames.LabelOrNull;

    private void UpdateScriptUsedBy(GameResource resource)
    {
        ScriptUsedByList.Children.Clear();
        if (resource.Kind != ResourceKind.Script || resource.ResourceId is not int id || _game?.Id is not (GameId.Loom or GameId.LoomVga) || !IsAdvancedMode)
        {
            ScriptUsedByExpander.Visibility = Visibility.Collapsed;
            return;
        }
        ScriptUsedByExpander.Visibility = Visibility.Visible;
        var uses = _usageIndex.UsedBy(LoomUsageTarget.Script, id, resource.ScriptRoom);
        ScriptUsedByExpander.Header = uses.Count == 0
            ? "Used by · no literal reference (a hook, a click or a variable starts it)"
            : $"Used by · {uses.Count}";
        ScriptUsedByExpander.ToolTip = "Static references in the decoded code; whether a branch runs depends on the game state.";
        foreach (var use in uses.Take(60))
            ScriptUsedByList.Children.Add(new TextBlock
            {
                Text = LoomUsageIndex.Describe(use, _glossary, VerbNameOrNull),
                Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2)
            });
        if (uses.Count > 60)
            ScriptUsedByList.Children.Add(new TextBlock { Text = $"… and {uses.Count - 60} more", Foreground = new SolidColorBrush(Color.FromRgb(159, 171, 174)), FontSize = 11 });
    }
    private ScummScriptDisassembly? _scriptShown;

    private void ScriptBytesToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showScriptBytes = ScriptBytesToggle.IsChecked == true;
        SaveViewOptions();
        if (_scriptShown is { } shown) ScriptOutput.Text = FormatScript(shown);
    }

    // Readable (names, if/else blocks) is the default; Raw is the decoded instructions and stays one click away. The choice is remembered.
    private bool _readableScripts = true;

    private void ScriptReadableToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (ScriptBytesToggle is null) return;
        _readableScripts = ScriptReadableToggle.IsChecked == true;
        ScriptBytesToggle.IsEnabled = !_readableScripts;
        SaveViewOptions();
        if (_scriptShown is { } shown) ScriptOutput.Text = FormatScript(shown);
    }

    private string FormatScript(ScummScriptDisassembly script) => _readableScripts && _game?.Id is GameId.Loom or GameId.LoomVga
        ? new LoomScriptPresenter(_glossary, LoomVerbNames.LabelOrNull).Present(script)
        : script.Format(_showScriptBytes);

    // A script the catalog knows as a scene can be played from its view: the cutscene machinery goes to the room and runs it.
    private KnownCutscene? ScriptCutscene(int? scriptId) =>
        scriptId is int id && _game?.Id == GameId.Loom
            ? AllCutscenes().FirstOrDefault(c => c.Steps is null && c.ObjectId != 0 && (c.TextScripts?.Contains(id) ?? c.Script == id))
              ?? AllCutscenes().FirstOrDefault(c => c.Steps is null && c.Script == id)
            : null;

    private void ScriptPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedResource is not { Kind: ResourceKind.Script } script || ScriptCutscene(script.ResourceId) is not { } cutscene || !CanStartCutscene) return;
        if (_lastViewedRoomResource is { } room) SelectResourceInTree(room);
        PlayFromGameTexts(() => PlayKnownCutscene(cutscene));
    }

    private bool _fullscreen;
    private WindowState _windowStateBefore;
    private WindowStyle _windowStyleBefore;
    private readonly List<(UIElement Element, Visibility Visibility)> _fullscreenHidden = [];
    private Thickness _contentMarginBefore;

    private void VerbStrip_MouseEnter(object sender, MouseEventArgs e) => FullscreenButton.Visibility = Visibility.Visible;
    private void VerbStrip_MouseLeave(object sender, MouseEventArgs e) => FullscreenButton.Visibility = Visibility.Collapsed;
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => SetFullscreen(!_fullscreen);

    /// <summary>The scene alone on the whole screen (F11 or the corner button); Escape leaves it. Everything else is hidden and comes back as it was.</summary>
    private void SetFullscreen(bool on)
    {
        if (on == _fullscreen) return;
        if (on)
        {
            _windowStateBefore = WindowState; _windowStyleBefore = WindowStyle; _contentMarginBefore = ContentPanel.Margin;
            _fullscreenHidden.Clear();
            foreach (UIElement element in new UIElement[] { AppHeader, SidePanel, AppStatusBar, RoomObjectPanel, PageHeader, RoomOptionsExpander, RoomControlsPanel, StatusText })
            {
                _fullscreenHidden.Add((element, element.Visibility));
                element.Visibility = Visibility.Collapsed;
            }
            ContentPanel.Margin = new Thickness(0);
            Grid.SetColumnSpan(ContentPanel, 3); Grid.SetColumn(ContentPanel, 0);
            WindowStyle = WindowStyle.None; WindowState = WindowState.Normal; WindowState = WindowState.Maximized;
        }
        else
        {
            foreach (var (element, visibility) in _fullscreenHidden) element.Visibility = visibility;
            _fullscreenHidden.Clear();
            ContentPanel.Margin = _contentMarginBefore;
            Grid.SetColumn(ContentPanel, 1);
            WindowStyle = _windowStyleBefore; WindowState = _windowStateBefore;
            UpdateAdvancedMode();
        }
        _fullscreen = on;
        UpdateRoomViewportLayout();
    }

    private void ShowPageHeader(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Visibility = PageSubtitle.Visibility = visibility;
        PageHeader.Margin = new Thickness(0, 0, 0, show ? 16 : 6);
    }

    /// <summary>A script, sound or costume view replaces the scene: offer the way back to the room the user came from.</summary>
    private void UpdateBackToSceneButton()
    {
        if (_selectedResource is { Kind: not ResourceKind.Room } && _lastViewedRoomResource is { } room)
        {
            BackToSceneButton.Content = $"← Back to Room {room.ResourceId:00}";
            BackToSceneButton.Visibility = Visibility.Visible;
        }
        else BackToSceneButton.Visibility = Visibility.Collapsed;
    }

    private void BackToScene_Click(object sender, RoutedEventArgs e)
    {
        if (_lastViewedRoomResource is { } room) SelectResourceInTree(room);
    }

    private void UpdateConnectionsButton()
    {
        UpdateBackToSceneButton();
        if (!IsAdvancedMode || _selectedResource is null ||
            (_game?.Id != GameId.LoomVga && _game is not { Id: GameId.Loom, Version: ScummVersion.V3 }))
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
            Title = $"Connections · {LoomResourceNames.Label(_glossary, selected)}",
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
            var heading = $"{LoomResourceNames.Label(_glossary, relationship.SourceResource)}  →  {relationship.Relationship}  →  {LoomResourceNames.NameScripts(_glossary, relationship.TargetLabel)}";
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
        foreach (var card in RoomCardList.Children.OfType<Button>().Concat(CostumeCardList.Children.OfType<Button>()))
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

        ShowPageHeader(true);
        PageTitle.Text = resource.Name;
        PageSubtitle.Text = "Every stored pose of this costume, at the same scale";
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        CostumeMessage.Text = "Reading costume…";
        CostumeMessage.Visibility = Visibility.Visible;
        StatusText.Text = $"{resource.Name} · reading costume";

        try
        {
            if (_game is { Id: GameId.LoomVga, V4Archive: { } vga })
            {
                var costume = vga.FindCostume(resource.ResourceId ?? -1) ?? throw new InvalidDataException("This costume has no data.");
                var poses = vga.DecodeCostumePoses(costume);
                foreach (var (index, pose) in poses) CostumePoseGallery.Children.Add(CreateCostumePoseCard(index, pose));
                if (poses.Count > 0)
                {
                    CostumeMessage.Visibility = Visibility.Collapsed;
                    CostumePoseScrollViewer.Dispatcher.BeginInvoke(ApplyCostumePoseScale, DispatcherPriority.Loaded);
                    StatusText.Text = $"{resource.Name} · {poses.Count} stored poses";
                }
                else
                {
                    CostumeMessage.Text = "No stored pose data is available for this costume.";
                    StatusText.Text = $"{resource.Name} · no picture available";
                }
                return;
            }
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
                    CostumePoseGallery.Children.Add(CreateCostumePoseCard(slot.Index, pose));
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

    private StackPanel CreateCostumePoseCard(int index, RoomImage pose)
    {
    var poseCard = new StackPanel
    {
        Margin = new Thickness(16, 14, 16, 22),
        HorizontalAlignment = HorizontalAlignment.Center
    };
    poseCard.Children.Add(new TextBlock
    {
        Text = $"POSE {index:00}",
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
        ToolTip = $"Pose {index:00} · {pose.Width} × {pose.Height} pixels"
    };
    RenderOptions.SetBitmapScalingMode(poseImage, BitmapScalingMode.NearestNeighbor);
    poseCard.Children.Add(poseImage);
        return poseCard;
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
        SaveViewOptions();
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
        // The scroll bar and the wheel stay inside the game's camera limits too.
        else if (_pendingCameraX is null && RoomViewport.ScrollableWidth > 0.5)
        {
            var (low, high) = RoomViewScrollRange();
            var inside = Math.Clamp(RoomViewport.HorizontalOffset, low, high);
            if (Math.Abs(inside - RoomViewport.HorizontalOffset) > 0.01) RoomViewport.ScrollToHorizontalOffset(inside);
        }
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
    private int RoomScreenRows => _game?.Id is GameId.Loom or GameId.LoomVga ? ScummVerbArea.Top - ScummV3MessageRenderer.RoomScreenTop : 128;

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
        // The optional verb strip shares the height: 56 screen rows at the room's scale (its explanation is a tooltip).
        var verbStrip = VerbScreenShown();
        var shownRows = ShownRoomRows(room);
        var oneScreen = OneGameScreen();
        _roomDisplayScale = Math.Min(availableWidth / visibleSourceWidth,
            usableHeight / (shownRows + (verbStrip ? ScummVerbArea.Height : 0)));
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
        VerbStripImage.Width = ScummVerbArea.Width * _roomDisplayScale;
        VerbStripImage.Height = ScummVerbArea.Height * _roomDisplayScale;
        VerbMessageOverlay.Width = VerbStripImage.Width;
        VerbMessageOverlay.Height = VerbStripImage.Height;
        // In the interactive LOOM mode the room and verb screens read as the game's one 200-row screen: no gap or rule.
        VerbStripPanel.Margin = new Thickness(0, oneScreen ? 0 : 2, 0, 0);
        VerbStripBorder.BorderThickness = new Thickness(0, oneScreen ? 0 : 1, 0, 0);
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
            return;
        }
        RoomViewport.UpdateLayout();
        // The first room opened is not always laid out yet: keep the camera until
        // the view can scroll (RoomViewport_ScrollChanged applies it then).
        if (_currentRoom.Width > 320 && RoomViewport.ScrollableWidth < 0.5) return;
        _pendingCameraX = null;
        var left = Math.Clamp(cameraX - Math.Min(320, _currentRoom.Width) / 2.0, 0,
            RoomViewport.ScrollableWidth / _roomDisplayScale);
        RoomViewport.ScrollToHorizontalOffset(left * _roomDisplayScale);
    }

    /// <summary>
    /// The view's horizontal offsets the manual pan may reach. In the interactive mode, with the session in the shown
    /// room, the game's camera limits (<c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c>, the camera being the screen's centre):
    /// Room 41's entry narrows them to 176, so its first 16 columns, which the game never shows, stay out of view.
    /// Otherwise the whole room.
    /// </summary>
    private (double Min, double Max) RoomViewScrollRange()
    {
        var max = RoomViewport.ScrollableWidth;
        if (!GameClickActive || _currentRoom is null || _session!.CameraRange is not { } range ||
            _session.Snapshot.RoomId is not int roomId || roomId != _currentRoomResource?.ResourceId)
            return (0, max);
        var half = Math.Min(320, _currentRoom.Width) / 2.0;
        var low = Math.Clamp((range.Min - half) * _roomDisplayScale, 0, max);
        return (low, Math.Clamp((range.Max - half) * _roomDisplayScale, low, max));
    }

    private void UpdateRoomScrollButtons()
    {
        var normalSize = NormalRoomWidthToggle.IsChecked == true;
        var (low, high) = RoomViewScrollRange();
        var canScroll = normalSize && high - low > 0.5;
        // No arrows while the player has no control (a cutscene or the boot): the game offers no scrolling then.
        RoomScrollButtonsPanel.Visibility = canScroll && !SessionInScene ? Visibility.Visible
            : normalSize ? Visibility.Hidden : Visibility.Collapsed;
        ScrollRoomLeftButton.IsEnabled = canScroll && RoomViewport.HorizontalOffset > low + 0.5;
        ScrollRoomRightButton.IsEnabled = canScroll && RoomViewport.HorizontalOffset < high - 0.5;
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
        var (low, high) = RoomViewScrollRange();
        if (NormalRoomWidthToggle.IsChecked != true || high - low <= 0.5 ||
            (direction < 0 && RoomViewport.HorizontalOffset <= low + 0.5) ||
            (direction > 0 && RoomViewport.HorizontalOffset >= high - 0.5)) return;

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
        var (low, high) = RoomViewScrollRange();
        var target = Math.Clamp(requested, low, high);
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

    // Keys go to the game in the interactive LOOM mode (the user's choice, 30 Sep: all the game's keys, as in
    // ScummVM), never while typing in a text box. Script 005 reads the note rows q-i and c-C, < and > (text speed),
    // F5 (save screen), F6 (music and effects), F7 (animation), Ctrl+V (version) and Escape.
    private bool GameKeysActive => SessionModeToggle.IsChecked == true &&
                                   _session is not null && _currentRoomResource is not null && IsLoomGame &&
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
        // Full screen: F11 toggles; Escape leaves it unless it is needed to skip a cutscene.
        if (e.Key == Key.F11 && _game is not null && RoomDisplayLayout.Visibility == Visibility.Visible) { e.Handled = true; SetFullscreen(!_fullscreen); return; }
        if (e.Key == Key.Escape && _fullscreen && !(GameClickActive && SessionInScene)) { e.Handled = true; SetFullscreen(false); return; }
        // Playback controls in the explore mode: Escape skips a cutscene
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
        if (e.Key is Key.Left or Key.Right && RoomPanKeysAvailable) { RoomViewport_PreviewKeyDown(sender, e); if (e.Handled) return; }
        if (!GameKeysActive) return;
        // F10 arrives as a system key.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (GameKeyCode(key, Keyboard.Modifiers) is not int code) return;
        e.Handled = true;
        SendKeyToGame(code);
    }

    private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // '.' is the engine's talk-stop key (processKeyboard; v3 has no VAR_TALKSTOP_KEY, so it is always on):
        // it ends the current text's delay and never reaches the scripts. It works whenever the interactive
        // session runs, also while a cutscene changes rooms.
        if (e.Text == "." && GameClickActive && IsLoomGame && Keyboard.FocusedElement is not (TextBox or ComboBox))
        {
            e.Handled = true;
            if (!SkipTalk("the game's talk-stop key '.'"))
                StatusText.Text = "'.' (the game's talk-stop key): no text is shown.";
            return;
        }
        // Printable characters go as their ASCII code, as ScummVM passes lastKeyHit.ascii.
        if (!GameKeysActive || e.Text is not { Length: 1 } text || text[0] is < ' ' or > '~') return;
        e.Handled = true;
        SendKeyToGame(text[0]);
    }

    /// <summary>
    /// Keys as game input: sends the key to the session as the game's own input
    /// path would receive it (<see cref="LoomExplorationSession.PressKey"/>).
    /// </summary>
    private void SendKeyToGame(int key) =>
        SendInputToGame(() => _session!.PressKey(key), $"Key {LoomExplorationSession.DescribeKey(key)}");

    /// <summary>A button caption with a small turning arc in front of it while the scenes of the rooms are still being worked out; the caption alone otherwise.</summary>
    private static object ButtonWithSpinner(string text, bool spinning)
    {
        if (!spinning) return text;
        var arc = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 6,1 A 5,5 0 1 1 1,6"),
            Stroke = Brushes.Goldenrod, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Width = 12, Height = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform()
        };
        arc.RenderTransform.BeginAnimation(RotateTransform.AngleProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(arc);
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private void RoomViewport_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_roomPanKey is not null) StopRoomPan();
    }

    // Left/Right pan the room wherever the focus is, so a room picked in the tree pans without first clicking the
    // arrows. Controls that use those keys themselves (text entry, drop-downs, sliders, tree nodes that
    // expand or collapse) keep them.
    private bool RoomPanKeysAvailable =>
        _game is not null && RoomDisplayLayout.Visibility == Visibility.Visible && Keyboard.Modifiers == ModifierKeys.None &&
        Keyboard.FocusedElement is not (TextBox or ComboBox or Slider or PasswordBox) and not TreeViewItem { HasItems: true };

    private void RoomViewport_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (NormalRoomWidthToggle.IsChecked != true) return;
        var direction = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (direction == 0) return;
        if (!_roomPanActive || _roomPanKey != e.Key)
            BeginRoomPan(direction, maximumStep: 64, key: e.Key);
        e.Handled = true;
    }

    private IReadOnlyList<LoomCdCue> _cdCues = [];
    private const int CdCueIdBase = 1000;
    private bool IsCdCue(GameResource resource) => _game?.V4Archive is not null && resource.ResourceId >= CdCueIdBase;

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
        var cdCue = IsCdCue(resource);
        var adlib = !cdCue && _sceneSound.Mode == SceneSoundPlayer.SoundMode.Adlib;
        var roland = !cdCue && _sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland;
        var format = cdCue ? "CD audio" : adlib ? "AdLib FM" : roland ? "Roland MT-32" : "PC speaker";
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
        ShowPageHeader(true);
        PageTitle.Text = resource.Name;
        StatusText.HorizontalAlignment = HorizontalAlignment.Left;
        PageSubtitle.Text = "Play this sound on demand.";
        PageSubtitle.ToolTip = resource.FilePath;
        AudioTitle.Text = resource.Name;
        AudioDescription.Text = $"Preparing {format} audio…";
        // No length until there is one: 0:00 / 0:00 would read as a sound of no length.
        AudioTime.Text = "Preparing…";
        PlayButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        AdlibGainSlider.IsEnabled = false;
        if (!cdCue && _sceneSound.Mode == SceneSoundPlayer.SoundMode.None)
        {
            _audioTimeNote = AudioTime.Text = "Muted";
            AudioDescription.Text = "No Sound selected";
            StatusText.Text = $"{resource.Name} · muted";
            return;
        }
        StatusText.Text = $"{resource.Name} · reading sound";
        _audioCancellation = new CancellationTokenSource();
        var token = _audioCancellation.Token;
        try
        {
            var wave = cdCue ? ReadCdCueWave(resource) : await RenderAudioWaveAsync(resource, adlib, token);
            if (token.IsCancellationRequested) return;
            _adlibBaseWave = adlib ? wave : null;
            if (adlib) wave = ScummAdlibAudio.ApplyOutputGain(wave, AdlibGainSlider.Value);
            var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(path, wave, token);
            if (token.IsCancellationRequested)
            {
                File.Delete(path);
                return;
            }
            _audioFile = path;
            _playWhenReady = autoPlay;
            _audioKnownDuration = cdCue ? TimeSpan.FromSeconds((wave.Length - 44) / (double)(BitConverter.ToInt32(wave, 24) * 4)) : WaveDuration(wave, adlib);
            _audioPlayer.Open(new Uri(path));
            _audioReady = true;
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            AdlibGainSlider.IsEnabled = adlib;
            AudioDescription.Text = !roland && wave.Length >= 44 + (adlib ? 49716 * 4 : 22050 * 2) * 179
                ? $"DOS {format} rendition · preview limited to three minutes"
                : cdCue ? $"{resource.Description} Straight from the disc's audio track · select Play to listen"
                : $"DOS {format} rendition · select Play to listen";
            StatusText.Text = $"{resource.Name} · {format} track ready";
            UpdateAudioTime();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            if (token.IsCancellationRequested) return;
            AudioDescription.Text = ex.Message;
            _audioTimeNote = AudioTime.Text = "No playable audio";
            StatusText.Text = $"{resource.Name} · playback unavailable";
        }
    }

    /// <summary>The cue's stretch of the disc's audio track, as the voice player would play it (the real recording, not a rendition).</summary>
    private byte[] ReadCdCueWave(GameResource resource)
    {
        var cue = _cdCues[resource.ResourceId!.Value - CdCueIdBase].Cue;
        var wave = _game!.V4Archive!.Disc!.ReadAudioWave(cue.TrackStartFrame, cue.TrackFrameCount);
        if (wave.Length <= 44) throw new InvalidDataException("The disc holds no audio at this position.");
        return wave;
    }

    private Task<byte[]> RenderAudioWaveAsync(GameResource resource, bool adlib,
        CancellationToken token) => Task.Run(() =>
        {
            if (_game?.V4Archive is { } vga)
            {
                if (_sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland) throw new NotSupportedException("The CD game has no MT-32 music. Choose AdLib or the PC speaker.");
                var cdSound = vga.ReadSounds().FirstOrDefault(item => item.Id == resource.ResourceId) ?? throw new InvalidDataException("This sound is not in DISK01.LEC.");
                return adlib
                    ? _adlibDecoder.RenderWave(cdSound.Adlib ?? throw new InvalidDataException("This sound has no AdLib part."), token)
                    : _audioDecoder.RenderWave(cdSound.Speaker ?? throw new InvalidDataException("This sound has no PC speaker part."), 6, token);
            }
            if (_sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland)
            {
                if (_sceneSound.Roms is not { } roms) throw new NotSupportedException("The MT-32 ROM images are not set.");
                if (!ScummRolandSong.IsRoland(resource))
                {
                    // The scripts of a Roland card choose the sound 32 higher for the same piece (scripts 001, 039, 040, 046, 095, 104).
                    var twin = _game?.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Sound && item.ResourceId == resource.ResourceId + 32);
                    if (twin is null || !ScummRolandSong.IsRoland(twin)) throw new NotSupportedException("This sound has no Roland version.");
                    resource = twin;
                }
                return new ScummMt32Audio(roms).RenderWave(resource, token);
            }
            if (ScummRolandSong.IsRoland(resource)) throw new NotSupportedException("This sound exists only for the Roland MT-32. Choose that device to hear it.");
            return adlib ? _adlibDecoder.RenderWave(resource, token) : _audioDecoder.RenderWave(resource, token);
        }, token);

    private async void RoomCuePlay_Click(object sender, RoutedEventArgs e) => await PlayRoomCueAsync();

    private async Task PlayRoomCueAsync()
    {
        if (_sceneSound.Mode == SceneSoundPlayer.SoundMode.None) return;
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
        RoomCueTime.Text = "Preparing…";
        _audioCancellation = new CancellationTokenSource();
        var token = _audioCancellation.Token;
        try
        {
            var adlib = _sceneSound.Mode == SceneSoundPlayer.SoundMode.Adlib;
            var wave = await RenderAudioWaveAsync(sound, adlib, token);
            if (token.IsCancellationRequested) return;
            if (adlib) wave = ScummAdlibAudio.ApplyOutputGain(wave, SceneSoundPlayer.AdlibGainDb);
            var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(path, wave, token);
            if (token.IsCancellationRequested)
            {
                File.Delete(path);
                return;
            }
            _audioFile = path;
            _playWhenReady = true;
            _audioKnownDuration = WaveDuration(wave, adlib);
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
            _audioTimeNote = RoomCueTime.Text = "No playable audio";
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

    /// <summary>The sound card number the scripts see for the selected device (see <see cref="SceneSoundPlayer.SoundCardFor"/>).</summary>
    private int SoundCard => SceneSoundPlayer.SoundCardFor(_sceneSound.Mode);

    // True while opening a folder switches the device back to AdLib on its own (the user's choice is not saved then).
    private bool _soundModeFallback;

    /// <summary>The MT-32 ROM images without asking: an environment override, the saved folder, a <c>mt32</c> folder beside the game folder.</summary>
    private static Mt32RomSet? FindMt32Roms(GameLibrary game)
    {
        string? saved = null;
        try { if (File.Exists(Mt32FolderFile)) saved = File.ReadAllText(Mt32FolderFile).Trim(); }
        catch (IOException) { }
        var beside = Path.GetDirectoryName(game.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Mt32RomSet.Find(Environment.GetEnvironmentVariable("SCUMM_MT32_ROM_ROOT")) ?? Mt32RomSet.Find(saved) ??
            (beside is null ? null : Mt32RomSet.Find(Path.Combine(beside, "mt32")));
    }

    private static readonly string Mt32FolderFile = Path.Combine(Path.GetDirectoryName(LastFolderFile)!, "mt32-rom-folder.txt");

    /// <summary>
    /// Makes the Roland device usable: this game folder must have the upgrade's sounds, and the MT-32 ROM images must be found
    /// (an environment override, the saved folder, an <c>mt32</c> folder beside the game folder, else the user is asked). Says why when not.
    /// </summary>
    private bool PrepareRoland()
    {
        if (_game is null || !SceneSoundPlayer.HasRolandSounds(_game))
        {
            MessageBox.Show(this, "This game folder does not have the LOOM Roland upgrade installed (its sounds 33 to 64 are missing), so there is no Roland music to play.",
                "Roland MT-32", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        if (_sceneSound.Roms is not null) return true;
        var roms = FindMt32Roms(_game);
        if (roms is null)
        {
            var picker = new OpenFolderDialog
            {
                Title = "Choose the folder with your MT-32 ROM images (MT32_CONTROL.ROM and MT32_PCM.ROM, or the CM32L pair)",
                Multiselect = false
            };
            if (picker.ShowDialog(this) != true) return false;
            roms = Mt32RomSet.Find(picker.FolderName);
            if (roms is null)
            {
                MessageBox.Show(this, "That folder has no MT32_CONTROL.ROM and MT32_PCM.ROM (or CM32L_CONTROL.ROM and CM32L_PCM.ROM). The ROM images are not part of this program; use your own copies.",
                    "Roland MT-32", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Mt32FolderFile)!);
                File.WriteAllText(Mt32FolderFile, picker.FolderName);
            }
            catch (IOException) { }
        }
        _sceneSound.Roms = roms;
        return true;
    }

    private const string SoundModeToolTip = "Music and effects. Changing device stops current audio; the next sound command uses the selected device.";

    private async void SoundModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || SoundModeCombo.SelectedIndex < 0) return;
        if (SessionInScene)
        {
            SoundModeCombo.SelectedIndex = (int)_sceneSound.Mode;
            return;
        }
        var requested = (SceneSoundPlayer.SoundMode)SoundModeCombo.SelectedIndex;
        if (requested == SceneSoundPlayer.SoundMode.Roland && !PrepareRoland())
        {
            SoundModeCombo.SelectedIndex = (int)_sceneSound.Mode;
            return;
        }
        var surface = _audioSurface;
        var selected = _selectedSound;
        StopAudio();
        _sceneSound.SetMode(requested);
        // The game's scripts read the sound card when they choose a sound, so the new device applies from the next one on.
        if (_session is not null) _session.SoundCard = SoundCard;
        if (!_soundModeFallback) SaveViewOptions();
        if (surface == AudioSurface.Resource && selected is not null)
            await ShowAudio(selected, preserveFormat: true);
        else if (surface == AudioSurface.Room)
            RoomCueStatus.Text = _sceneSound.Mode == SceneSoundPlayer.SoundMode.None ? "No Sound selected" : "Select Play to listen";
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
            var wave = await Task.Run(() => ScummAdlibAudio.ApplyOutputGain(sourceWave, gainDb));
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
        // The player reports the length only once the file is open; the rendered wave's own length is known at once.
        TimeSpan? total = _audioPlayer.NaturalDuration.HasTimeSpan ? _audioPlayer.NaturalDuration.TimeSpan : _audioKnownDuration;
        AudioTime.Text = total is TimeSpan length ? $"{current:m\\:ss} / {length:m\\:ss}" : _audioTimeNote ?? "Preparing…";
        RoomCueTime.Text = AudioTime.Text;
    }

    // The length of the rendered wave (null until it exists) and, when no wave can exist, what to show instead of a length.
    private TimeSpan? _audioKnownDuration;
    private string? _audioTimeNote;

    private TimeSpan WaveDuration(byte[] wave, bool adlib) => _sceneSound.Mode == SceneSoundPlayer.SoundMode.Roland && wave.Length >= 44
        ? TimeSpan.FromSeconds((wave.Length - 44) / (double)(BitConverter.ToInt32(wave, 24) * 4))
        : TimeSpan.FromSeconds(Math.Max(0, wave.Length - 44) / (double)(adlib ? 49716 * 4 : 22050 * 2));

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
        _audioKnownDuration = null;
        _audioTimeNote = null;
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
            ? CreateMaskOverlayBitmap(room.Width, room.Height, pixels, DisplayPalette(room), depthMask.Pixels.ToArray())
            : CreateBitmap(room.Width, room.Height, pixels, DisplayPalette(room));
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

    private static ScummWalkBoxSet? TryReadWalkBoxes(byte[] roomBytes)
    {
        try { return ScummWalkBoxReader.Read(roomBytes); }
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
    private ScummWalkBoxSet? DisplayedWalkBoxes() =>
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
            var invisible = (boxes.GetFlags(box.Index) & ScummWalkBox.InvisibleFlag) != 0;
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

    private string DescribeWalkBoxes(ScummWalkBoxSet boxes)
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

    private enum DisplayMode { Never, Hover, Always }
    private DisplayMode DoorwayMode => DoorwaysAlways.IsChecked == true ? DisplayMode.Always : DoorwaysHover.IsChecked == true ? DisplayMode.Hover : DisplayMode.Never;
    private DisplayMode ObjectAreaMode => ObjectsAlways.IsChecked == true ? DisplayMode.Always : ObjectsHover.IsChecked == true ? DisplayMode.Hover : DisplayMode.Never;
    private bool _viewOptionsReady;
    private HashSet<int> _hoveredDoorwayIds = [];

    private void DoorwayMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_viewOptionsReady || RoomNavigationOverlay is null) return;
        SaveViewOptions();
        _hoveredDoorwayIds = [];
        RoomNavigationOverlay.Visibility = _roomNavigationCandidates.Count > 0 && DoorwayMode != DisplayMode.Never ? Visibility.Visible : Visibility.Collapsed;
        RoomNavigationOverlay.Dispatcher.BeginInvoke(RefreshRoomNavigationOverlay, DispatcherPriority.Loaded);
    }

    private void ObjectAreaMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_viewOptionsReady || HotspotHighlightOverlay is null) return;
        SaveViewOptions();
        ShowHotspotHighlight(ObjectAreaMode == DisplayMode.Never ? null : _highlightedHotspot);
    }

    /// <summary>On-hover doorways: the doorway boxes under the pointer are the only ones drawn.</summary>
    private void UpdateDoorwayHover(int? x, int? y)
    {
        if (DoorwayMode != DisplayMode.Hover) return;
        var ids = x is int px && y is int py ? DoorwaysAt(px, py).Select(record => record.Id).ToHashSet() : [];
        if (ids.SetEquals(_hoveredDoorwayIds)) return;
        _hoveredDoorwayIds = ids;
        RefreshRoomNavigationOverlay();
    }

    // The viewing options (doorways, bytecode column; object hotspots always start at Never) are remembered between runs.
    private static readonly string ViewOptionsFile = Path.Combine(Path.GetDirectoryName(LastFolderFile)!, "view-options.txt");

    private void LoadViewOptions()
    {
        try
        {
            if (File.Exists(ViewOptionsFile))
                foreach (var line in File.ReadAllLines(ViewOptionsFile))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;
                    switch (parts[0])
                    {
                        case "doorways": (parts[1] == "Always" ? DoorwaysAlways : parts[1] == "Hover" ? DoorwaysHover : DoorwaysNever).IsChecked = true; break;
                        case "framing": NormalRoomWidthToggle.IsChecked = parts[1] != "0"; break;
                        case "sound" when Enum.TryParse<SceneSoundPlayer.SoundMode>(parts[1], out var mode):
                            // Restored as it was left; the ROM images of a Roland choice are looked for when a game opens.
                            _sceneSound.SetMode(mode);
                            SoundModeCombo.SelectedIndex = (int)_sceneSound.Mode;
                            break;
                        case "voices": CdSoundCombo.SelectedIndex = parts[1] != "0" ? 1 : 0; _cdVoice.Enabled = parts[1] != "0"; break;
                        case "bytecode": _showScriptBytes = parts[1] != "0"; ScriptBytesToggle.IsChecked = _showScriptBytes; break;
                        case "readable": _readableScripts = parts[1] != "0"; ScriptReadableToggle.IsChecked = _readableScripts; ScriptBytesToggle.IsEnabled = !_readableScripts; break;
                    }
                }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        _viewOptionsReady = true;
    }

    private void RoomOptions_Toggled(object sender, RoutedEventArgs e) { if (ReferenceEquals(sender, e.OriginalSource)) SaveViewOptions(); }

    private void SaveViewOptions()
    {
        if (!_viewOptionsReady) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ViewOptionsFile)!);
            File.WriteAllLines(ViewOptionsFile, [$"doorways={DoorwayMode}", $"bytecode={(_showScriptBytes ? 1 : 0)}", $"readable={(_readableScripts ? 1 : 0)}", $"framing={(NormalRoomWidthToggle.IsChecked == true ? 1 : 0)}", $"optionsOpen={(RoomOptionsExpander.IsExpanded ? 1 : 0)}", $"sound={_sceneSound.Mode}", $"voices={(CdSoundCombo.SelectedIndex == 1 ? 1 : 0)}"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
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
            SelectInteractTarget(HotspotRowRecordAt(x, y));
            QueueGameClick(x, y);
            return;
        }
        HandleRoomPoint(x, y);
    }

    private ScummRoomObjectRecord? _interactTarget;

    /// <summary>Selects the object the Interact menu acts on (null clears it).</summary>
    private void SelectInteractTarget(ScummRoomObjectRecord? record)
    {
        _interactTarget = record;
        UpdateInteractButton();
    }

    private void UpdateInteractButton()
    {
        if (_interactTarget is not null && !_currentRoomObjectRecords.Any(item => item.Id == _interactTarget.Id)) _interactTarget = null;
        var name = _interactTarget is not null && _roomObjectNames.TryGetValue(_interactTarget.Id, out var stored) ? stored : null;
        InteractButton.Content = name is null ? "Interact ▴" : $"Interact · {name} ▴";
        InteractButton.IsEnabled = _interactTarget is not null && InteractVerbs(_interactTarget).Count > 0 && !SessionInScene && _session is not null;
    }

    /// <summary>The verbs offered in the menu: look, use or go, and the drafts the object answers (the plumbing verbs stay in Advanced).</summary>
    private IReadOnlyList<ScummObjectVerbEntry> InteractVerbs(ScummRoomObjectRecord record) =>
        _roomVerbs.TryGetValue(record.Id, out var verbs)
            ? verbs.Where(verb => verb.VerbId is 0x36 or 0x38 || LoomDraftNames.ForVerb(verb.VerbId) is not null)
                .DistinctBy(verb => verb.VerbId).OrderBy(verb => LoomVerbNames.Order(verb.VerbId)).ToArray()
            : [];

    private void OpenInteractMenu(ScummRoomObjectRecord record, FrameworkElement target, System.Windows.Controls.Primitives.PlacementMode placement)
    {
        var heading = _roomObjectNames.TryGetValue(record.Id, out var name) ? name.ToUpperInvariant() : $"OBJECT {record.Id}";
        var entries = InteractVerbs(record).Select(verb => (LoomVerbNames.Label(verb.VerbId), (string?)null, (Action)(() =>
        {
            if (!SessionInScene) ActivateObjectVerb(record, verb, play: true);
        })));
        OpenPopupMenu(target, placement, heading, entries);
    }

    /// <summary>The dark pop-up menu used by Interact and the room's Play Cutscene: a small heading and one row per choice.</summary>
    private static void OpenPopupMenu(FrameworkElement target, System.Windows.Controls.Primitives.PlacementMode placement, string heading,
        IEnumerable<(string Label, string? Tip, Action Run)> entries)
    {
        var host = new StackPanel { MinWidth = 170 };
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = target, Placement = placement, StaysOpen = false, AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None
        };
        host.Children.Add(new TextBlock
        {
            Text = heading,
            Foreground = new SolidColorBrush(Color.FromRgb(183, 132, 66)), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 7, 10, 3)
        });
        var rows = new StackPanel();
        foreach (var (label, tip, run) in entries)
        {
            var item = new Button
            {
                Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 },
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 5, 10, 5),
                Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)), BorderThickness = new Thickness(0),
                ToolTip = tip
            };
            item.MouseEnter += (_, _) => item.Background = new SolidColorBrush(Color.FromRgb(46, 92, 116));
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.Click += (_, _) =>
            {
                popup.IsOpen = false;
                run();
            };
            rows.Children.Add(item);
        }
        host.Children.Add(new ScrollViewer { Content = rows, MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        popup.Child = new Border
        {
            Child = host, Background = new SolidColorBrush(Color.FromRgb(16, 24, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(0, 0, 0, 4)
        };
        popup.IsOpen = true;
    }

    private void Interact_Click(object sender, RoutedEventArgs e)
    {
        if (_interactTarget is { } record) OpenInteractMenu(record, InteractButton, System.Windows.Controls.Primitives.PlacementMode.Top);
    }

    private void RoomPreview_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!GameClickActive || SessionInScene || !TryGetRoomImagePoint(e.GetPosition(RoomPreview), out var x, out var y)) return;
        var record = HotspotRowRecordAt(x, y);
        SelectInteractTarget(record);
        if (record is not null && InteractVerbs(record).Count > 0)
        {
            e.Handled = true;
            OpenInteractMenu(record, RoomPreview, System.Windows.Controls.Primitives.PlacementMode.MousePoint);
        }
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
    /// <summary>LOOM on EGA or on CD: the click path (scripts 005-007) is the same; what reads EGA-only files stays on <c>GameId.Loom</c>.</summary>
    private bool IsLoomGame => _game?.Id is GameId.Loom or GameId.LoomVga;

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
        IReadOnlyList<ScummRoomObjectRecord>? doorways = null)
    {
        if (_session is null || _currentRoomResource is not { } sourceRoom || _game is null) return false;
        if (_playbackPaused)
        {
            StatusText.Text = $"{what} not sent: the scene is paused. Play (or space) goes on.";
            StatusText.ToolTip = null;
            return false;
        }
        var playing = PlaybackRunning;
        FollowSessionSound();
        var sceneBefore = (_session.CutscenesBegun, _session.EgoPuts);
        var sentencesBefore = _session.ObjectSentencesBegun;
        var action = send();
        if (!_playInstantly) UpdateSessionSound();
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
        StartPlayback(what, action, sourceRoom, doorways, leftRoom: false, instant: refused is not null, sceneBefore,
            sentencesBefore: sentencesBefore);
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
        if (SessionModeToggle.IsChecked == true && _session is not null && _session.WaitingForSkillClick)
        {
            // The CD intro's skill screen: a press on a button is the game's click; the intro's own playback goes on from there.
            var action = _session.ClickSkillScreen(x, y);
            StatusText.Text = _session.WaitingForSkillClick ? "Press one of the three skill buttons." : "Skill chosen (assumed input; the draft question that follows in the game is not simulated).";
            StatusText.ToolTip = action.Evidence + Environment.NewLine + string.Join(Environment.NewLine, action.Uncertainties);
            return;
        }
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            if (NearestEntranceAt(x, y) is { } entrance)
            {
                // Overlapping doors of one doorway (Room 16's two doors to Room 17): the game keeps one of them clickable
                // depending on where Bobbin stands, so the click would always use that one. The explorer lets the entrance
                // nearest to the click win and says so.
                StatusText.Text = $"Door object {entrance.Id} nearest the click taken (the game would not select it from where Bobbin stands: an exploration action)";
                FollowDoorway(entrance);
                return;
            }
            SendInputToGame(() => _session.Click(x, y, doubleClick), doubleClick ? "Double click" : "Click", DoorwaysAt(x, y));
            return;
        }
        CycleArtworkAt(x, y);
    }

    /// <summary>
    /// When several doorway objects overlap at a room point (every entrance of one door), the one whose walk point is nearest
    /// to the point, if the game would not select it now (untouchable, class 24). Null when the game's own click already picks
    /// the nearest entrance, or the point holds a single door.
    /// </summary>
    private ScummRoomObjectRecord? NearestEntranceAt(int x, int y)
    {
        if (_session?.Snapshot is not { } snapshot) return null;
        var doors = _roomNavigationCandidates.Select(candidate => candidate.Object).DistinctBy(record => record.Id)
            .Where(record => record.Width > 0 && record.Height > 0 && snapshot.ObjectOwners.GetValueOrDefault(record.Id) == 15 &&
                             ParentStateMatches(record, snapshot))
            .Where(record =>
            {
                var at = snapshot.ObjectPositions.TryGetValue(record.Id, out var current) ? current : new RoomObjectPosition(record.X, record.Y);
                return at.X <= x && x < at.X + record.Width && at.Y <= y && y < at.Y + record.Height;
            }).ToArray();
        if (doors.Length < 2) return null;
        var nearest = doors.OrderBy(record => (record.WalkX - x) * (record.WalkX - x) + (record.WalkY - y) * (record.WalkY - y)).First();
        return snapshot.IsUntouchable(nearest.Id) ? nearest : null;
    }

    /// <summary>
    /// The game's click on a doorway reached no room load: the doorway's verb runs
    /// directly, labelled as an exploration action, not the game's click.
    /// </summary>
    private void FollowDoorwayFallback(IReadOnlyList<ScummRoomObjectRecord> doorways)
    {
        if (_session is null) return;
        var why = StatusText.Text;
        var refused = _session.LastAction?.Uncertainties.FirstOrDefault(line => line.Contains("not sent, because", StringComparison.Ordinal));
        // Only a doorway eligible for native hit selection may be retried.
        // Closed/untouchable exits remain browsable through the Advanced room list.
        var live = doorways.Where(record => !_session.Snapshot.IsUntouchable(record.Id)).ToArray();
        if (live.Length == 0) return;
        _lastDoorwayFallback = refused ?? $"{why}: {_session.LastAction?.Evidence}";
        FollowDoorway(live[0]);
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
        public IReadOnlyList<ScummRoomObjectRecord>? Doorways { get; init; }
        public bool LeftRoom { get; set; }
        public int IdleFrames { get; set; }
        public int CutsceneFrames { get; set; }
        public int WaitTicks { get; set; }
        // The CD game's scenes keep time with its disc audio, which plays in real time: frames the timer was late for are made up (see PlaybackTimer_Tick).
        public long LastTickAt { get; set; }
        public TimeSpan Credit { get; set; }
        // A LOOM demo's self-running preview: it runs until the user opens another room.
        public bool Preview { get; init; }
        // A cutscene chosen from the CD game's derived list: the code may hold a branch that needs another state, so say so when none began.
        public string? ExpectedCutscene { get; init; }
        public string? Note { get; set; }
        public string? FrameSignature { get; set; }
        public int? CameraX { get; set; }
        // The session's cutscene and ego-put counts before the input: a cutscene that put the ego
        // somewhere since then is a change of scene, the game's answer to the input.
        public (int Cutscenes, int EgoPuts) SceneBefore { get; init; }
        public int SentencesBefore { get; init; }
    }

    private Playback? _playback;
    private LoomReplaySequence? _replaySequence;
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
        TimeSpan.FromSeconds(Math.Clamp(_session?.FrameJiffies ?? 6, 1, ScummMessageTable.MaxFrameDelta) / 60.0);

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
        IReadOnlyList<ScummRoomObjectRecord>? doorways, bool leftRoom, bool instant = false, (int Cutscenes, int EgoPuts)? sceneBefore = null, bool preview = false, string? expectedCutscene = null, int? sentencesBefore = null)
    {
        _playback = new Playback
        {
            What = what, Action = action, SourceRoom = sourceRoom, Doorways = doorways, LeftRoom = leftRoom, Preview = preview, ExpectedCutscene = expectedCutscene,
            CameraX = _session?.CameraX,
            SceneBefore = sceneBefore ?? (_session?.CutscenesBegun ?? 0, _session?.EgoPuts ?? 0),
            SentencesBefore = sentencesBefore ?? _session?.ObjectSentencesBegun ?? 0
        };
        _playbackRequestedRoom = null;
        _playbackPaused = false;
        // The scene opens where the game's camera is, whatever the view showed before (a replay can start in a wide room).
        if (_session?.CameraX is int startCamera && ViewShowsSessionRoom()) ScrollToGameCamera(startCamera);
        if (_game is { Id: not GameId.LoomVga } && _session is not null && !_sceneSound.Follows(_session)) _sceneSound.Attach(_game, _session);
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
        _replaySequence = null;
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
        if (_playback is null || _playbackPaused || !PlaybackHasActivity) return;
        _playbackTimer?.Stop();
        _playbackPaused = true;
        _sceneSound.Pause();
        _cdVoice.Pause();
        StatusText.Text = "Paused. Play (or space) goes on.";
        StatusText.ToolTip = "The scene's frames wait while paused. The game's own pause key (space, VAR_PAUSE_KEY) stops ScummVM's frame loop the same way.";
        UpdatePlaybackControls();
    }

    private void ResumeScenePlayback()
    {
        if (_playback is null || !_playbackPaused) return;
        _playbackPaused = false;
        _sceneSound.Resume();
        _cdVoice.Resume();
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

    /// <summary>While paused, plays one frame (Step ▸); the playback stays paused.</summary>
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

    private void SkipLine_Click(object sender, RoutedEventArgs e) => SkipTalk("the Skip line button, same as '.'");

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
        if (skippable) _replaySequence = null; // A skipped prelude no longer establishes the audited next stage.
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

    private bool PlaybackHasActivity => _replaySequence is { Complete: false, Failure: null } || SessionInScene || _playback is { Preview: true } || _session is { } session &&
        (session.EgoMoving || session.CameraMoving || !string.IsNullOrWhiteSpace(session.Messages.Talk?.Text) ||
         session.DraftPlaying);

    private bool CanStartCutscene => GameClickActive && !SessionInScene &&
        (_playback is null || !PlaybackHasActivity && !_playbackPaused);

    /// <summary>Shows the playback bar in the explore mode, and enables pause/play and skip as the playback allows.</summary>
    private void UpdatePlaybackControls()
    {
        GameStatusButton.Visibility = _game?.Id is GameId.Loom or GameId.LoomVga && _session is not null ? Visibility.Visible : Visibility.Collapsed;
        GameStatusButton.IsEnabled = _session is not null; // Inspection is available during playback too; editing stays guarded.
        CutsceneMenuButton.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        var active = GameClickActive && IsLoomGame;
        UpdateInteractButton();
        // Step plays one frame of a paused scene; with nothing paused there is nothing to step.
        AdvanceFrameButton.IsEnabled = _playback is not null && _playbackPaused;
        PlaybackBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        SoundModeCombo.IsEnabled = !SessionInScene;
        CdSoundCombo.IsEnabled = !SessionInScene;
        RoomSoundsButton.IsEnabled = !SessionInScene;
        SoundModeCombo.ToolTip = SessionInScene ? "The sound device can be changed when the scene is over." : SoundModeToolTip;
        AdlibGainPanel.IsEnabled = !SessionInScene;
        PlaybackPauseButton.IsEnabled = active && _playback is not null && (_playbackPaused || PlaybackHasActivity);
        PlaybackPauseButton.Content = _playbackPaused ? "▶ Play" : "⏸ Pause";
        SkipLineButton.IsEnabled = active && !string.IsNullOrWhiteSpace(_session?.Messages.Talk?.Text);
        SkipCutsceneButton.IsEnabled = active && SessionInScene && _session?.CutsceneSkippable == true;
        CutsceneMenuButton.IsEnabled = active && CanStartCutscene;
        var roomChoices = _currentRoomResource?.ResourceId is int shownRoom ? RoomCutsceneItems(shownRoom) : [];
        // Until the background runs have listed the scenes that play out in rooms, a first launch shows the button disabled (nothing is known yet).
        var listing = ScannedCutscenes && !_cdRoomListsDone && _cdSavedRoomLists is null && roomChoices.Count == 0;
        RoomCutsceneButton.Visibility = active && (roomChoices.Count > 0 || listing) ? Visibility.Visible : Visibility.Collapsed;
        RoomCutsceneButton.IsEnabled = active && CanStartCutscene && !listing;
        var computing = ScannedCutscenes && !_cdRoomListsDone && _cdSavedRoomLists is null;
        CutsceneScanNote.Visibility = active && (computing || ScanRunning) ? Visibility.Visible : Visibility.Collapsed;
        CutsceneScanProgress.Visibility = active && ScanRunning ? Visibility.Visible : Visibility.Collapsed;
        RoomCutsceneButton.Content = ButtonWithSpinner(roomChoices.Count == 1 || listing ? "Play Cutscene" : "Play Cutscene ▴", computing);
        RoomCutsceneButton.ToolTip = listing ? "Looking for the scenes that happen in this room… (the first launch takes a while; the result is kept for the next one)." : !CanStartCutscene ? "Playback is active. Finish or skip it before starting another scene." :
            roomChoices.Count == 1 ? $"Play {roomChoices[0].Title}" : "Choose a cutscene that happens in this room.";
        if (!active) return;
        CutsceneMenuButton.Content = "Play Cutscene";
        CutsceneMenuButton.IsEnabled = CanStartCutscene;
        CutsceneMenuButton.ToolTip = CanStartCutscene ? "Choose a cutscene from anywhere in the game." : "Playback is active. Finish or skip it before starting another scene.";
        UpdateRoomScrollButtons();
        var inCutscene = SessionInScene;
        var skippable = _session?.CutsceneSkippable == true;
        SkipCutsceneButton.IsEnabled = inCutscene && skippable;
        SkipCutsceneButton.ToolTip = !inCutscene
            ? "No cutscene is playing."
            : skippable
                ? "Skip the cutscene (Escape): ScummVM's abortCutscene sends the script that set the override to its skip jump. Assumed input."
                : "The cutscene has not set its skip point (beginOverride) yet, so Escape would do nothing now, as in the game.";
    }

    private bool PlaybackShouldContinue()
    {
        if (_session is not { } session || _playback is not { } playback) return false;
        if (_replaySequence is { } sequence)
        {
            var continuing = sequence.Continue();
            playback.Note = sequence.Failure;
            return continuing;
        }
        // The demo's director script is always pending and starts over; nothing but the user ends the preview.
        if (playback.Preview) return true;
        var talking = session.Messages.HaveMsg != 0;
        // The ego's walk or turn, or a camera pan, still in progress plays on (M3a), even with no script left.
        // Other actors are not waited for: ambient walkers (script 042's swan) never stop.
        var moving = session.EgoMoving || session.CameraMoving;
        if (session.Snapshot.PendingScripts == 0 && !talking && !moving) return false;
        // Script 020 keeps bit[0xD66] set until script 021 finishes the draft. Its notes and spell result
        // can take longer than the ordinary 32-frame idle window; keep their clock running as for a scene.
        var draftPlaying = session.DraftPlaying;
        if (SessionInScene || draftPlaying)
        {
            if (playback.CutsceneFrames < LoomExplorationSession.CutsceneFrameLimit) return true;
            playback.Note = $"A scene or draft was still running after {LoomExplorationSession.CutsceneFrameLimit} frames; playback stopped following it.";
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
        if (SessionInScene || session.DraftPlaying) playback.CutsceneFrames++;
        // Frames of a walk do not count as idle: the sentence that waits for it runs after it.
        else if (!session.EgoMoving && !session.CameraMoving) playback.IdleFrames++;
        var room = session.Snapshot.RoomId;
        session.AdvanceFrame();
        _replaySequence?.FrameAdvanced();
        if (session.Snapshot.RoomId != room) playback.LeftRoom = true;
        // The sounds the frame's scripts started or stopped (instant playback has no frames to wait for).
        if (!_playbackBatch) { _sceneSound.Update(); if (_game?.Id == GameId.LoomVga) _cdVoice.Update(); }
        return true;
    }

    private string? _idleSignature;
    private LoomExplorationSession? _idleSession;
    private DateTime _idleNextFrame;

    /// <summary>
    /// The game's frame loop keeps running between inputs (ambient scripts such as the loom's weaving, the swan or the
    /// waves animate without the pointer). With no playback clock running, one frame runs per game frame interval and
    /// the view is redrawn when something visible changed. A scene or a message that starts on its own gets a playback.
    /// </summary>
    private void IdleFrameTimer_Tick(object? sender, EventArgs e)
    {
        if (!GameClickActive || _session is not { } session || _playInstantly || _playbackBatch || PlaybackRunning || _playbackPaused ||
            _currentRoomResource is not { } room || _currentRoom is null || session.Snapshot.RoomId != room.ResourceId ||
            OwnedWindows.Count > 0 || !IsActive)
            return;
        var now = DateTime.UtcNow;
        if (!ReferenceEquals(_idleSession, session)) { _idleSession = session; _idleSignature = null; _idleNextFrame = now; }
        if (now < _idleNextFrame || _sceneSound.IsPreparing) return;
        // Count on from the due time, not from now: the timer ticks every 15 ms, so the frames keep the game's rate (a tick
        // arriving just before the due time used to cost a whole extra tick, which made the idle frames about half as fast
        // as the playback clock). After a long stall, start over rather than catching up in a burst.
        var interval = PlaybackFrameInterval();
        _idleNextFrame = now - _idleNextFrame > interval ? now + interval : _idleNextFrame + interval;
        FollowSessionSound();
        var snapshot = session.AdvanceFrame();
        UpdateSessionSound();
        if (snapshot.RoomId is int reached && reached != room.ResourceId)
        {
            AdvanceIdleReachedRoom(reached);
            return;
        }
        var signature = FrameSignature(snapshot, session);
        if (signature != _idleSignature)
        {
            var first = _idleSignature is null;
            _idleSignature = signature;
            if (!first)
            {
                ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
                UpdateRoomNavigationActions(room);
                RefreshRoomPreview();
            }
        }
        if (session.CameraX is int cameraX && session.CameraMoving) ScrollToGameCamera(cameraX);
        UpdateActorLayer();
        UpdateMessageLayer();
        if (SessionInScene || session.EgoMoving || session.CameraMoving || session.Messages.HaveMsg != 0)
            StartPlayback("A scene that started on its own", null, room, doorways: null, leftRoom: false);
    }

    private void AdvanceIdleReachedRoom(int reached)
    {
        if (_game?.Rooms.FirstOrDefault(item => item.ResourceId == reached) is not { } targetRoom) return;
        StatusText.Text = "You step into another scene.";
        StatusText.ToolTip = $"Room {reached:00} · " + (_session?.LastAction?.Evidence ?? "");
        SelectResourceInTree(targetRoom);
    }

    private void FollowSessionSound()
    {
        if (GameClickActive && _game is { Id: not GameId.LoomVga } && _session is not null && !_sceneSound.Follows(_session))
            _sceneSound.Attach(_game, _session);
        else if (GameClickActive && _game is { Id: GameId.LoomVga, V4Archive: { } vga } && _session is not null && !_cdVoice.Follows(_session))
            _cdVoice.Attach(vga.Disc, _session);
    }

    private void UpdateSessionSound()
    {
        if (GameClickActive && !_playbackBatch && _game?.Id != GameId.LoomVga) _sceneSound.Update();
        else if (GameClickActive && !_playbackBatch && _game?.Id == GameId.LoomVga) _cdVoice.Update();
    }

    private void CdSoundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CdSoundCombo.SelectedIndex < 0) return;
        _cdVoice.Enabled = CdSoundCombo.SelectedIndex == 1;
        RoomSoundsButton.Visibility = _game?.Id == GameId.LoomVga && _cdRoomCues.Count > 0 && _cdVoice.Enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!_cdVoice.Enabled) _cdVoice.Stop();
        SaveViewOptions();
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
        // The music the last frame started is still being prepared: the scene waits so the music starts with it.
        if (_sceneSound.IsPreparing) return;
        // The CD game's audio (voice, overture) runs on the real clock; a frame that took longer than its jiffies (drawing, a room's load) is
        // made up by running the frames it was late for, up to four a tick, so the scene stays with the music.
        var catchUp = 0;
        if (_game.Id == GameId.LoomVga)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (playback.LastTickAt != 0)
                playback.Credit += System.Diagnostics.Stopwatch.GetElapsedTime(playback.LastTickAt, now) - PlaybackFrameInterval();
            playback.LastTickAt = now;
            // Time the app could not follow (a pause, a long load) is not made up.
            playback.Credit = TimeSpan.FromTicks(Math.Clamp(playback.Credit.Ticks, 0, TimeSpan.FromMilliseconds(400).Ticks));
            while (catchUp < 4 && playback.Credit >= PlaybackFrameInterval()) { playback.Credit -= PlaybackFrameInterval(); catchUp++; }
        }
        for (var frame = 0; frame <= catchUp; frame++)
        {
            if (!PlaybackStep())
            {
                FinishPlayback();
                return;
            }
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
        var signature = FrameSignature(snapshot, session);
        if (signature != playback.FrameSignature)
        {
            var first = playback.FrameSignature is null;
            playback.FrameSignature = signature;
            // The first frame of a room entered during a playback may already differ from the state the room's view was built with (the CD skill
            // screen's buttons are drawn a few frames in, before the first frame shows), so the CD game refreshes on it too.
            if (!first || _game?.Id == GameId.LoomVga)
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

    /// <summary>The room's palette as the game shows it now: the colour slots a script faded (the opening's title) show other colours.</summary>
    private IReadOnlyList<uint> DisplayPalette(RoomImage room)
    {
        if (_session is not { } session || room.Palette.Count > 256) return room.Palette;
        return DisplayPalette(room, session.ShadowPalette);
    }

    private static IReadOnlyList<uint> DisplayPalette(RoomImage room, IReadOnlyList<byte> shadow)
    {
        var identity = true;
        for (var i = 0; i < room.Palette.Count && identity; i++) identity = shadow[i] == i;
        if (identity) return room.Palette;
        var shown = new uint[room.Palette.Count];
        for (var i = 0; i < shown.Length; i++) shown[i] = shadow[i] < room.Palette.Count ? room.Palette[shadow[i]] : room.Palette[i];
        return shown;
    }

    private static string FrameSignature(LoomSessionSnapshot snapshot, LoomExplorationSession session) =>
        string.Join("", session.ShadowPalette.Take(16)) + "|" +
        string.Join(",", snapshot.ObjectStates.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")) + "|" +
        string.Join(",", snapshot.ObjectPositions.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.X},{pair.Value.Y}")) + "|" +
        string.Join(",", snapshot.DrawOrder);

    /// <summary>Scrolls the 320-pixel view to the game's camera without the entry pan.</summary>
    private void ScrollToGameCamera(int cameraX)
    {
        if (_currentRoom is null || NormalRoomWidthToggle.IsChecked != true) return;
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
        var sequenceFailure = _replaySequence?.Failure;
        _replaySequence = null;
        UpdatePlaybackControls();
        _sceneSound.Update();
        if (_game?.Id == GameId.LoomVga) _cdVoice.Update();
        var snapshot = session.Snapshot;
        var note = playback.Note is null ? "" : Environment.NewLine + playback.Note;
        if (sequenceFailure is not null)
        {
            StatusText.Text = "Replay sequence stopped; later inputs were cancelled.";
            StatusText.ToolTip = sequenceFailure;
            if (_currentRoomResource is { } current && current.ResourceId == snapshot.RoomId)
            {
                ApplyRoomState(current, _currentRoomObjectRecords, _objects, visit: false);
                RefreshRoomPreview();
            }
            return;
        }
        if (snapshot.RoomId is int reached && reached != playback.SourceRoom.ResourceId)
        {
            if (_currentRoomResource?.ResourceId != reached && _playbackRequestedRoom != reached &&
                _game?.Rooms.FirstOrDefault(item => item.ResourceId == reached) is { } target)
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
        if (playback.ExpectedCutscene is { } expected && session.CutscenesBegun == playback.SceneBefore.Cutscenes)
        {
            StatusText.Text = $"{expected}: no cutscene began from this start";
            StatusText.ToolTip = "The code has a branch for this cutscene that this state does not take (a story state or held item the explorer did not set; Game Status can change it). " +
                                 "What the script did run is shown." + (StatusText.ToolTip is string started ? Environment.NewLine + started : "") + note;
        }
        if (playback.Doorways is not { Count: > 0 } doorways || _currentRoomResource?.ResourceId != playback.SourceRoom.ResourceId)
            return;
        // A look/use sentence can legitimately choose not to load a room (the grave's look needs Reading).
        // Once native input reached the sentence, replaying another verb or opening a merely possible target would override that answer.
        if (session.ObjectSentencesBegun > playback.SentencesBefore) return;
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
        if (_cdScene is { } cdScene && room is not null && cdScene.Snapshot.RoomId == _currentRoomResource?.ResourceId)
        {
            UpdateCdActorLayer(cdScene, room);
            return;
        }
        if (!GameClickActive || room is null || _game is not { } game ||
            _session!.Snapshot.RoomId is not int roomId || roomId != _currentRoomResource?.ResourceId)
        {
            RoomActorOverlay.Source = null;
            RoomActorOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        var layer = ScummActorRenderer.Render(_session.Actors, roomId, room.Width, room.Height, id =>
        {
            if (_actorCostumes.TryGetValue(id, out var costume)) return costume;
            try { costume = _session.ReadCostume(id); }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or IOException) { costume = null; }
            return _actorCostumes[id] = costume;
        }, _objectSelection.ComposeDepthMask(room), _session.CurrentWalkBoxes, _session.ObjectClasses, room.ExtraDepthMasks.Select(plane => plane.Pixels.ToArray()).ToArray());
        if (layer.Actors.Count == 0)
        {
            RoomActorOverlay.Source = null;
            RoomActorOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        var pixels = new byte[room.Width * room.Height * 4];
        var shown = DisplayPalette(room);
        for (var i = 0; i < layer.Pixels.Length; i++)
        {
            var index = layer.Pixels[i];
            if (!layer.IsDrawn(i)) continue;
            SetBgra(pixels, i * 4, index < shown.Count ? shown[index] : 0xFF000000u);
        }
        RoomActorOverlay.Source = FrozenBgra(room.Width, room.Height, pixels);
        RoomActorOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The CD game's actors over its first-visit room: the actors the entry script put in the room, Bobbin where an assumed arrival
    /// (a catalog visit) stands him. Costume colours go through each costume's own table into the room palette. An actor in a walk box with
    /// a mask is hidden where the room's depth mask (with the drawn objects' masks) is set, as on EGA.
    /// </summary>
    private void UpdateCdActorLayer(LoomCdScene scene, RoomImage room)
    {
        var layer = ScummActorRenderer.Render(scene.Actors, scene.Snapshot.RoomId!.Value, room.Width, room.Height, id =>
        {
            if (_actorCostumes.TryGetValue(id, out var costume)) return costume;
            try { costume = scene.Costumes(id); }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or IOException) { costume = null; }
            return _actorCostumes[id] = costume;
        }, _objectSelection.ComposeDepthMask(room), scene.WalkBoxes, scene.Snapshot.ObjectClasses);
        if (layer.Actors.Count == 0)
        {
            RoomActorOverlay.Source = null;
            RoomActorOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        var pixels = new byte[room.Width * room.Height * 4];
        for (var i = 0; i < layer.Pixels.Length; i++)
        {
            if (!layer.IsDrawn(i)) continue;
            var index = layer.Pixels[i];
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
    private bool OneGameScreen() => _currentRoom is not null && GameClickActive && IsLoomGame;

    /// <summary>The game's camera left edge in room pixels (ScummVM <c>virtscr[0].xstart</c>).</summary>
    private int GameCameraLeft(RoomImage room)
    {
        var visible = Math.Min(ScummV3MessageRenderer.ScreenWidth, room.Width);
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
                var layer = ScummV3MessageRenderer.Render(messages, charset);
                RoomMessageOverlay.Source = MessageRoomBitmap(layer, room, left);
                _verbMessageBitmap = MessageVerbBitmap(layer, room.Palette);
                if (session!.Messages.Talk is { Y: < ScummV3MessageRenderer.VerbScreenTop } talk &&
                    ScummV3MessageRenderer.Render(talk, charset).Bounds is { } bounds)
                    _talkTextRect = (bounds.Left + left, bounds.Top - ScummV3MessageRenderer.RoomScreenTop,
                        bounds.Right + left, bounds.Bottom - ScummV3MessageRenderer.RoomScreenTop);
            }
            else if (_cdCharset is { } cdCharset)
            {
                var layer = ScummV4MessageRenderer.Render(messages, number => CdCharset(number, cdCharset));
                RoomMessageOverlay.Source = MessageRoomBitmap(layer, room, left);
                _verbMessageBitmap = MessageVerbBitmap(layer, room.Palette);
                if (session!.Messages.Talk is { Y: < ScummV3MessageRenderer.VerbScreenTop } talk &&
                    ScummV4MessageRenderer.Render(talk, number => CdCharset(number, cdCharset)).Bounds is { } bounds)
                    _talkTextRect = (bounds.Left + left, bounds.Top - ScummV3MessageRenderer.RoomScreenTop,
                        bounds.Right + left, bounds.Bottom - ScummV3MessageRenderer.RoomScreenTop);
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

    private static BitmapSource MessageRoomBitmap(ScummMessageLayer layer, RoomImage room, int cameraLeft)
    {
        var pixels = new byte[room.Width * room.Height * 4];
        for (var screenY = ScummV3MessageRenderer.RoomScreenTop; screenY < ScummV3MessageRenderer.VerbScreenTop; screenY++)
        {
            var y = screenY - ScummV3MessageRenderer.RoomScreenTop;
            if (y >= room.Height) break;
            for (var x = 0; x < layer.Width; x++)
            {
                var index = layer.Pixels[screenY * layer.Width + x];
                var roomX = cameraLeft + x;
                if (index == ScummV3MessageRenderer.Transparent || roomX >= room.Width) continue;
                SetBgra(pixels, (y * room.Width + roomX) * 4, index < room.Palette.Count ? room.Palette[index] : 0xFF000000u);
            }
        }
        return FrozenBgra(room.Width, room.Height, pixels);
    }

    // The verb screen's text on a transparent 320 × 56 layer, or null when there is none.
    private static BitmapSource? MessageVerbBitmap(ScummMessageLayer layer, IReadOnlyList<uint> palette)
    {
        var pixels = new byte[ScummVerbArea.Width * ScummVerbArea.Height * 4];
        var any = false;
        for (var y = 0; y < ScummVerbArea.Height; y++)
        for (var x = 0; x < ScummVerbArea.Width; x++)
        {
            var index = layer.Pixels[(ScummVerbArea.Top + y) * layer.Width + x];
            if (index == ScummV3MessageRenderer.Transparent) continue;
            SetBgra(pixels, (y * ScummVerbArea.Width + x) * 4, index < palette.Count ? palette[index] : 0xFF000000u);
            any = true;
        }
        return any ? FrozenBgra(ScummVerbArea.Width, ScummVerbArea.Height, pixels) : null;
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
        IReadOnlyList<ScummMessage> messages, ScummMessage? talk, RoomImage room, int cameraLeft)
    {
        var typeface = new Typeface("Consolas");
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        (int, int, int, int)? talkRect = null;
        BitmapSource Draw(int width, int height, Func<ScummMessage, bool> include, int offsetX, int offsetY, bool keepTalk)
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
        var roomBitmap = Draw(room.Width, room.Height, message => message.Y < ScummV3MessageRenderer.VerbScreenTop,
            cameraLeft, -ScummV3MessageRenderer.RoomScreenTop, keepTalk: true);
        BitmapSource? verb = messages.Any(message => message.Y >= ScummV3MessageRenderer.VerbScreenTop)
            ? Draw(ScummVerbArea.Width, ScummVerbArea.Height, message => message.Y >= ScummV3MessageRenderer.VerbScreenTop,
                0, -ScummVerbArea.Top, keepTalk: false)
            : null;
        return (roomBitmap, verb, talkRect);
    }

    /// <summary>A click on the talk text: ScummVM's talk-stop key ('.') ends its delay, as assumed input.</summary>
    private bool SkipTalkAt(int x, int y)
    {
        if (!GameClickActive || _talkTextRect is not { } rect || _session!.Messages.Talk is null ||
            x < rect.Left - 2 || x >= rect.Right + 2 || y < rect.Top - 2 || y >= rect.Bottom + 2) return false;
        return SkipTalk("a click on the text: the game's talk-stop key '.'");
    }

    /// <summary>
    /// ScummVM's talk-stop key ('.'): the talk delay goes to 0, so the next frame ends the message or shows
    /// its next part. Returns false when no talk text is shown.
    /// </summary>
    private bool SkipTalk(string how)
    {
        if (!GameClickActive || _session!.Messages.Talk is null) return false;
        _session.Messages.SkipTalkDelay();
        StatusText.Text = $"Text skipped ({how}, assumed input)";
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
            UpdateDoorwayHover(null, null);
            SendPointerToGame(null, null);
            return;
        }
        var hoveredRow = SessionModeToggle.IsChecked == true ? HotspotRowRecordAt(x, y) : null;
        ShowHoveredHotspotRow(hoveredRow);
        UpdateDoorwayHover(x, y);
        SendPointerToGame((x, y), hoveredRow?.Id ?? 0);
        if (GameClickActive)
        {
            var doorways = DoorwaysAt(x, y);
            RoomPreview.Cursor = hoveredRow is null && doorways.Count == 0 ? Cursors.Arrow : Cursors.Hand;
            if (!IsAdvancedMode)
            {
                RoomPreview.ToolTip = hoveredRow is not null && _roomObjectNames.TryGetValue(hoveredRow.Id, out var plainName) ? plainName : null;
                return;
            }
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
        UpdateDoorwayHover(null, null);
        RoomPreview.Cursor = Cursors.Arrow;
        RoomPreview.ToolTip = null;
        ShowHoveredHotspotRow(null);
        SendPointerToGame(null, null);
    }

    // The room and the object under the pointer (0: none) when a frame last ran for the game pointer.
    private (int? Room, int? Target)? _gamePointerTarget;
    private LoomExplorationSession? _pointerSession;

    /// <summary>
    /// Interactive hover gives the pointer's room position to the session
    /// as the virtual mouse (assumed input, no click). A frame runs only when the
    /// object under the pointer changes, so the input loop sees the new hover once.
    /// </summary>
    private void SendPointerToGame((int X, int Y)? point, int? target)
    {
        if (!GameClickActive || _session is null ||
            _currentRoomResource is not { } room || _session.Snapshot.RoomId != room.ResourceId) return;
        if (!ReferenceEquals(_pointerSession, _session)) { _gamePointerTarget = null; _pointerSession = _session; }
        _session.SetPointer(point);
        // While the playback clock runs, its frames see the pointer.
        if (PlaybackRunning || _playbackPaused) return;
        if (_gamePointerTarget == (room.ResourceId, target)) return;
        _gamePointerTarget = (room.ResourceId, target);
        AdvanceOneFrame();
    }

    private bool VerbStripShown() =>
        GameClickActive && IsLoomGame && _currentRoom is not null;

    /// <summary>
    /// The interactive verb strip: a presentation of the session's verb slots
    /// (<see cref="ScummVerbArea.Compose"/>), drawn under the room at its scale.
    /// It shows game state; the drawing itself is not the game's.
    /// </summary>
    // The verb screen is shown for the strip, or for text a script printed there (rows 144–199).
    // In the interactive LOOM mode it is always shown, so the room keeps its size when a script prints there or clears it.
    private bool VerbScreenShown() => VerbStripShown() ||
        _currentRoom is not null && (_verbMessageBitmap is not null || GameClickActive && IsLoomGame);

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
                SetVerbScreenNote(_verbMessageBitmap is not null
                        ? "Verb screen: the game's text there, with the verb slots it goes with (presentation of the slots)."
                        : "Verb screen (rows 144–199), kept under the room: the session's verb slots (presentation of the slots).",
                    _session?.Messages.OnScreen
                        .Where(message => message.Y >= ScummVerbArea.Top)
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
        SetVerbScreenNote($"Verb area: presentation of the session's verb slots, not the game's drawing; {drawn}.{estimate}{notes}{clicks}",
            picture.Items.Select(item =>
                $"verb 0x{item.VerbId:X2} (slot {item.Slot}): object {item.ImageObject}'s image from Room {item.ImageRoom:00} at ({item.X}, {item.Y})" +
                (item.Mode == 2 ? ", dim" : "") + (item.Estimate is null ? "" : $"; estimate: {item.Estimate}"))
            .Concat(picture.Notes.Select(line => $"not drawn: {line}"))
            .Append("Text verbs, the note letters scripts print, highlight colours and redraw timing are not drawn."));
        VerbStripPanel.Visibility = Visibility.Visible;
    }

    // The verb screen's explanation: a tooltip on the strip (its first line the summary), not a row under it, so the
    // screen shows only the game's 56 rows. A verb's own tooltip (VerbStripImage_MouseMove) wins over it.
    private void SetVerbScreenNote(string summary, IEnumerable<string> details)
    {
        var lines = details.ToArray();
        _verbScreenNote = lines.Length == 0 ? summary
            : summary + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines);
        VerbStripBorder.ToolTip = IsAdvancedMode ? _verbScreenNote : null;
    }

    private string? _verbScreenNote;

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
        var x =(int)(position.X / VerbStripImage.ActualWidth * ScummVerbArea.Width);
        var y = (int)(position.Y / VerbStripImage.ActualHeight * ScummVerbArea.Height);
        if (x is < 0 or >= ScummVerbArea.Width || y is < 0 or >= ScummVerbArea.Height) return null;
        return (x, ScummVerbArea.Top + y);
    }

    // With "Click as game input" on, only verbs whose slot is on (findVerbAtPos) react.
    private ScummVerbAreaItem? ClickableVerbAt(Point position) =>
        GameClickActive && VerbStripShown() && VerbStripPoint(position) is { } point
            ? ScummVerbArea.FindVerbAt(_session!.VerbArea().Items, point.X, point.Y) : null;

    private void VerbStripImage_MouseMove(object sender, MouseEventArgs e)
    {
        var verb = ClickableVerbAt(e.GetPosition(VerbStripImage));
        VerbStripImage.Cursor = verb is null ? null : Cursors.Hand;
        VerbStripImage.ToolTip = verb is null || !IsAdvancedMode ? null
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
        var closeUp = GameClickActive
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
    private ScummRoomObjectRecord? HotspotRowRecordAt(int x, int y)
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
    private void ShowHoveredHotspotRow(ScummRoomObjectRecord? record)
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
            ShowHotspotHighlight(ObjectAreaMode == DisplayMode.Never ? null : record);
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

    private bool IsVerbHotspotAvailable(ScummRoomObjectRecord record, LoomSessionSnapshot snapshot) =>
        record.Width > 0 && record.Height > 0 &&
        snapshot.ObjectOwners.GetValueOrDefault(record.Id) == 15 &&
        ParentStateMatches(record, snapshot);

    /// <summary>
    /// Doorway hotspots at a room point that are present in the room (owned by
    /// it, parent state matching), in directory order.
    /// </summary>
    private IReadOnlyList<ScummRoomObjectRecord> DoorwaysAt(int x, int y)
    {
        if (_session?.Snapshot is not { } snapshot) return [];
        var (selected, unknown) = ScummObjectHitTest.Find(_currentRoomObjectRecords, snapshot.ObjectStates,
            snapshot.ObjectPositions, snapshot.ObjectClasses, x, y);
        // Native hit selection already excludes untouchable exits and mismatched parents.
        // A ground click or a different overlapping object must never open a static candidate.
        if (unknown is not null || selected == 0) return [];
        return VisibleDoorways(snapshot).Where(record => record.Id == selected).ToArray();
    }

    private IEnumerable<ScummRoomObjectRecord> VisibleDoorways(LoomSessionSnapshot snapshot) =>
        _roomNavigationCandidates.Select(candidate => candidate.Object).DistinctBy(record => record.Id)
            .Where(record => record.Width > 0 && record.Height > 0 &&
                             snapshot.ObjectOwners.GetValueOrDefault(record.Id) == 15 &&
                             !snapshot.IsUntouchable(record.Id) &&
                             ParentStateMatches(record, snapshot))
            .OrderBy(record => record.LocalIndex);

    private int[] DoorwayTargets(ScummRoomObjectRecord record) => _roomNavigationCandidates
        .Where(candidate => candidate.Object.Id == record.Id).Select(candidate => candidate.TargetRoomId)
        .Distinct().Order().ToArray();

    private string DoorwayLabel(ScummRoomObjectRecord record) =>
        "Room " + string.Join(" / ", DoorwayTargets(record).Select(id => id.ToString("00")));

    private string DoorwayToolTip(ScummRoomObjectRecord record)
    {
        var targets = DoorwayTargets(record);
        var text = $"Object {record.Id}: its script can load {string.Join(" or ", targets.Select(id => $"Room {id:00}"))}. " +
                   "Clicking follows a room load reached by its script. If none is reached, the current room stays open. " +
                   "The Advanced doorway buttons open possible destinations directly for browsing.";
        return _session?.Snapshot.IsUntouchable(record.Id) == true
            ? text + " This hotspot is untouchable in the current preview (class 24), so the game would not select it here; " +
              "a room click cannot follow it."
            : text;
    }

    /// <summary>
    /// Runs an eligible doorway's verb and continues pending scripts. A static
    /// possible destination cannot override the script's conditional answer.
    /// </summary>
    private void FollowDoorway(ScummRoomObjectRecord record)
    {
        var routes = _roomNavigationCandidates.Where(candidate => candidate.Object.Id == record.Id).ToArray();
        if (routes.Length == 0) return;
        if (ActivateObjectVerb(record, routes[0].Verb) || ContinueToReachedRoom()) return;
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

    private bool ParentStateMatches(ScummRoomObjectRecord record, LoomSessionSnapshot snapshot)
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
            DoorwayOptions.Visibility = ObjectAreaOptions.Visibility = Visibility.Collapsed;
            return;
        }
        var candidates = new List<RoomVerbDestination>();
        _doorwayFinder ??= new LoomDoorwayFinder(_game);
        foreach (var record in _currentRoomObjectRecords)
        {
            if (!_roomVerbs.TryGetValue(record.Id, out var verbs)) continue;
            foreach (var verb in verbs)
            {
                ScummScriptDisassembly script;
                try { script = _game.Id == GameId.LoomVga ? _cdData!.ReadObjectVerb(room.ResourceId!.Value, record, verb.VerbId, out _) : _verbReader.Decode(room, record, verb.VerbId); }
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
        DoorwayOptions.Visibility = _roomNavigationCandidates.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ObjectAreaOptions.Visibility = Visibility.Visible;
        RoomNavigationOverlay.Visibility = _roomNavigationCandidates.Count > 0 && DoorwayMode != DisplayMode.Never
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
        foreach (var group in VisibleDoorways(snapshot).Where(record => DoorwayMode == DisplayMode.Always || DoorwayMode == DisplayMode.Hover && _hoveredDoorwayIds.Contains(record.Id)).GroupBy(record =>
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

    private ScummRoomObjectRecord? _highlightedHotspot;

    private void HotspotHighlightOverlay_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ShowHotspotHighlight(_highlightedHotspot);

    /// <summary>
    /// Outlines a hotspot's box on the room image (at its current position in
    /// the session), or clears the outline when <paramref name="record"/> is null.
    /// </summary>
    private void ShowHotspotHighlight(ScummRoomObjectRecord? record)
    {
        _highlightedHotspot = record;
        HotspotHighlightOverlay.Children.Clear();
        var all = ObjectAreaMode == DisplayMode.Always && SessionModeToggle.IsChecked == true && _session is not null;
        HotspotHighlightOverlay.Visibility = record is null && !all ? Visibility.Collapsed : Visibility.Visible;
        if (_currentRoom is null || HotspotHighlightOverlay.ActualWidth <= 0 ||
            HotspotHighlightOverlay.ActualHeight <= 0 || record is null && !all) return;

        var room = _currentRoom;
        var scale = Math.Min(HotspotHighlightOverlay.ActualWidth / room.Width,
            HotspotHighlightOverlay.ActualHeight / room.Height);
        if (scale <= 0) return;
        var offsetX = (HotspotHighlightOverlay.ActualWidth - room.Width * scale) / 2;
        var offsetY = (HotspotHighlightOverlay.ActualHeight - room.Height * scale) / 2;
        if (all)
            foreach (var other in _currentRoomObjectRecords)
            {
                if (other.Id == record?.Id || !IsVerbHotspotAvailable(other, _session!.Snapshot)) continue;
                var otherAt = _session.Snapshot.ObjectPositions.TryGetValue(other.Id, out var pos) ? pos : new RoomObjectPosition(other.X, other.Y);
                var faint = new Border
                {
                    Width = Math.Max(1, other.Width * scale), Height = Math.Max(1, other.Height * scale),
                    BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(150, 120, 220, 255)),
                    Background = new SolidColorBrush(Color.FromArgb(22, 120, 220, 255)), CornerRadius = new CornerRadius(2)
                };
                Canvas.SetLeft(faint, offsetX + otherAt.X * scale);
                Canvas.SetTop(faint, offsetY + otherAt.Y * scale);
                HotspotHighlightOverlay.Children.Add(faint);
            }
        if (record is null) return;
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

    private int? _roomMusicActionsRoom;

    private async void UpdateRoomMusicActions()
    {
        var generation = ++_roomMusicUpdateGeneration;
        // Called again while the room stays (a scene plays, the view refreshes): hiding the music panel and the Sounds button until the list is
        // rebuilt made them blink and the row of buttons shake, so they stay as they are and are replaced when the list is ready.
        var sameRoom = _roomMusicActionsRoom == _currentRoomResource?.ResourceId && _currentRoomResource is not null;
        _roomMusicActionsRoom = _currentRoomResource?.ResourceId;
        if (!sameRoom)
        {
            RoomMusicPanel.Visibility = Visibility.Collapsed;
            _roomSoundChoices = [];
            RoomSoundsButton.Visibility = Visibility.Collapsed;
        }
        if (_game is { Id: GameId.LoomVga, V4Archive.Disc: not null } && _currentRoomResource is { ResourceId: int cdRoomId })
        {
            // The CD game: the disc's audio the room's own scripts ask for (music or narration first, then the spoken lines), played on demand.
            try { _cdRoomCues = LoomCdCueIndex.ScanRoom(_cdData ?? ScummDataSources.For(_game), cdRoomId).OrderByDescending(cue => cue.IsMusicOrNarration).ThenBy(cue => cue.Cue.Offset).ToArray(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _cdRoomCues = []; }
            RoomSoundsButton.Visibility = _cdRoomCues.Count > 0 && _cdVoice.Enabled ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        if (_game is null || _game.Id == GameId.LoomVga || _currentRoomResource is not { } room) return;
        // The interactive LOOM mode plays the music the game's scripts start (SceneSoundPlayer), so the panel's own
        // player would only duplicate it (the user's choice, 30 Sep; .verification/room-music-analysis). Inspection mode keeps it.
        // The Sounds menu (every music and effect the room may start, played on demand) serves both modes.
        var interactive = _game.Id == GameId.Loom && SessionModeToggle.IsChecked == true;
        if (interactive && _audioSurface == AudioSurface.Room && _roomCueSourceRoomId != room.ResourceId) StopAudio();
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
                var decoder = new ScummAdlibAudio();
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
        var choiceEvidence = possible.Where(item => item.SoundId.HasValue).GroupBy(item => item.SoundId!.Value)
            .ToDictionary(group => group.Key, group => string.Join(Environment.NewLine, group.Select(item => item.Evidence)));
        _roomSoundChoices = choiceEvidence.Keys.Concat(events.Where(item => item.Event.TargetId.HasValue).Select(item => item.Event.TargetId!.Value))
            .Distinct().Where(soundById.ContainsKey)
            .Select(id =>
            {
                var track = scannedTracks.GetValueOrDefault(id);
                return (Resource: soundById[id], Music: track.IsMusic, Duration: track.Duration,
                    Evidence: choiceEvidence.GetValueOrDefault(id, "Started during this session."));
            })
            .OrderBy(item => item.Resource.ResourceId)
            .ToList();
        RoomSoundsButton.Visibility = _roomSoundChoices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (interactive) { RoomMusicPanel.Visibility = Visibility.Collapsed; return; }
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
        RoomCueTitle.ToolTip = selectedEvidence + Environment.NewLine + note;

        RoomMusicPanel.Visibility = Visibility.Visible;
    }

    private List<(GameResource Resource, bool Music, TimeSpan? Duration, string Evidence)> _roomSoundChoices = [];

    private IReadOnlyList<LoomCdCue> _cdRoomCues = [];

    private static string SoundSourceSummary(string evidence)
    {
        var references = evidence.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        // Both editions show the initiating source on the row; full call paths belong in its tooltip.
        var source = (references.FirstOrDefault() ?? "").Split('→')[0].Trim();
        source = System.Text.RegularExpressions.Regex.Replace(source, @"\broom (\d+)\b", m => $"Room {int.Parse(m.Groups[1].Value):00}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(source, @"\bverb\b", "Verb", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string RoomSoundLabel(string resource, string kind, string source, TimeSpan? duration) =>
        $"▶ {(IsAdvancedMode ? resource + " · " + kind + " · " + SoundSourceSummary(source) : kind)}" +
        (duration is TimeSpan length ? $" · {FormatRoomCueDuration(length)}" : "");

    /// <summary>The CD game's Sounds menu: the disc audio this room's scripts ask for, played on demand (an exploration action).</summary>
    private void CdRoomSounds_Click()
    {
        if (_cdRoomCues.Count == 0 || SessionInScene || _game?.V4Archive?.Disc is not { } disc) return;
        var entries = new List<(string Label, string? Tip, Action Run)> { ("■ Stop", null, _cdVoice.Stop) };
        int music = 0, voices = 0;
        foreach (var cue in _cdRoomCues)
        {
            var picked = cue;
            var snippet = cue.Text.Length > 36 ? cue.Text[..36] + "…" : cue.Text;
            var kind = cue.IsMusicOrNarration ? $"♪ Music / narration {++music}" : $"Voice {++voices}";
            var label = IsAdvancedMode
                ? RoomSoundLabel($"CD cue {cue.Cue.DiscSeconds:0.#}", kind, cue.Source, TimeSpan.FromSeconds(cue.Cue.Seconds))
                : $"▶ {(cue.IsMusicOrNarration ? "♪ " : "")}{cue.Cue.Seconds:0.#} s{(snippet.Length > 0 ? " · " + snippet : "")}";
            entries.Add((label, IsAdvancedMode ? $"{cue.Source}\nDisc second {cue.Cue.DiscSeconds:0.#} · {cue.Cue.Seconds:0.#} s" +
                (cue.Text.Length > 0 ? $"\n\"{cue.Text}\"" : "") : null, () => _cdVoice.PlayNow(disc, picked.Cue)));
        }
        OpenPopupMenu(RoomSoundsButton, System.Windows.Controls.Primitives.PlacementMode.Top, "SOUNDS IN THIS ROOM", entries);
    }

    private void RoomSounds_Click(object sender, RoutedEventArgs e)
    {
        if (_game?.Id == GameId.LoomVga) { CdRoomSounds_Click(); return; }
        if (_roomSoundChoices.Count == 0 || SessionInScene) return;
        var entries = new List<(string Label, string? Tip, Action Run)>();
        if (_audioSurface == AudioSurface.Room) entries.Add(("■ Stop", null, StopAudio));
        int music = 0, effects = 0;
        foreach (var choice in _roomSoundChoices)
        {
            var picked = choice;
            // Normal: the kind of sound, numbered within its kind (the game gives sounds no titles); Advanced: the resource's own name.
            var kind = picked.Music ? $"♪ Music {++music}" : $"Sound effect {++effects}";
            var label = RoomSoundLabel(picked.Resource.Name, kind, picked.Evidence, picked.Duration);
            entries.Add((label, IsAdvancedMode ? picked.Evidence : null, () => _ = AuditionRoomSoundAsync(picked)));
        }
        OpenPopupMenu(RoomSoundsButton, System.Windows.Controls.Primitives.PlacementMode.Top, "SOUNDS IN THIS ROOM", entries);
    }

    /// <summary>Plays a sound picked from the room's list on demand (an exploration action; the game's own sounds stop first).</summary>
    private async Task AuditionRoomSoundAsync((GameResource Resource, bool Music, TimeSpan? Duration, string Evidence) choice)
    {
        if (_sceneSound.Mode == SceneSoundPlayer.SoundMode.None)
        {
            StatusText.Text = "Sound is off. Choose PC Speaker or AdLib to listen.";
            return;
        }
        _sceneSound.StopAll("a sound was picked from the room's sound list");
        SelectRoomCue(choice.Resource, choice.Evidence, false, manuallySelected: true);
        await PlayRoomCueAsync();
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

using System.IO;
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
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _adlibGainDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private static readonly string LastFolderFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SCUMMexplorer", "last-folder.txt");
    private GameLibrary? _game;
    private LoomRelationshipIndex? _relationshipIndex;
    private GameResource? _selectedResource;
    private int _relationshipBuildGeneration;
    private CancellationTokenSource? _previewCancellation;
    private RoomImage? _currentRoom;
    private GameResource? _currentRoomResource;
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
    private bool _changingCostumePose;
    private AudioSurface _audioSurface;
    private GameResource? _roomSelectedCue;
    private GameResource? _roomRenderedCue;
    private int? _roomCueSourceRoomId;
    private bool _roomCueReached;
    private enum AudioSurface { None, Resource, Room }
    private IReadOnlyDictionary<int, RoomImage> _costumePoses = new Dictionary<int, RoomImage>();
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
        Closed += (_, _) => StopAudio();
        _audioTimer.Tick += (_, _) => UpdateAudioTime();
        _adlibGainDebounceTimer.Tick += async (_, _) =>
        {
            _adlibGainDebounceTimer.Stop();
            await ApplyAdlibGainAsync();
        };
        _audioPlayer.MediaOpened += AudioPlayer_MediaOpened;
        _audioPlayer.MediaEnded += (_, _) => StopPlayback();
        _audioPlayer.MediaFailed += (_, args) =>
        {
            var surface = _audioSurface;
            var message = args.ErrorException?.Message ?? "Audio playback failed.";
            StopAudio();
            if (surface == AudioSurface.Room) RoomCueStatus.Text = $"Playback unavailable: {message}";
            else
            {
                AudioDescription.Text = message;
                PlayButton.IsEnabled = false;
                PauseButton.IsEnabled = false;
                StopButton.IsEnabled = false;
            }
        };
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
            }) { Owner = this };
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
        _session = indexedSnapshot is null ? null : new LoomExplorationSession(game, indexedSnapshot);
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
        ConnectionsButton.Visibility = Visibility.Collapsed;
        StopAudio();
        AudioPanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        CostumePanel.Visibility = Visibility.Collapsed;
        CostumePreview.Source = null;
        CostumePoseCombo.Items.Clear();
        UpdateCostumePoseNavigation();
        _costumePoses = new Dictionary<int, RoomImage>();
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
        RoomPreview.ToolTip = null;
        RoomPreview.Cursor = Cursors.Arrow;
        ScriptEventButtons.Children.Clear();
        ObjectAssetPreview.Source = null;
        ObjectAssetPreview.Visibility = Visibility.Collapsed;
        ObjectAssetDescription.Text = "";
        RoomPreview.Source = null;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.IsChecked = false;
        RoomMaskToggle.IsEnabled = false;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        PreviewMessage.Visibility = Visibility.Visible;
        GameTitle.Text = game.Title;
        GameInfo.Text = game.Platform == "DOS" ? "DOS edition" : game.Platform;
        GameInfo.ToolTip = $"{game.Version switch { ScummVersion.Unknown => "SCUMM version unconfirmed", _ => $"SCUMM {game.Version}" }} · {game.Platform}";
        ResourceTree.Items.Clear();

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

    private async void ResourceTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
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
        if (e.NewValue is not TreeViewItem { Tag: GameResource resource })
        {
            _selectedResource = null;
            UpdateConnectionsButton();
            if (AudioPanel.Visibility == Visibility.Visible || CostumePanel.Visibility == Visibility.Visible || ScriptPanel.Visibility == Visibility.Visible)
            {
                StopAudio();
                AudioPanel.Visibility = Visibility.Collapsed;
                ScriptPanel.Visibility = Visibility.Collapsed;
                CostumePanel.Visibility = Visibility.Collapsed;
                RoomObjectPanel.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(ContentPanel, 1);
                PreviewMessage.Visibility = Visibility.Visible;
                PreviewTitle.Text = "Choose a room or sound";
                PreviewDescription.Text = "Select a room, costume, or sound to inspect.";
            }
            return;
        }
        _selectedResource = resource;
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
            CostumePanel.Visibility = Visibility.Collapsed;
            RoomObjectPanel.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(ContentPanel, 2);
            RoomPreview.Visibility = Visibility.Collapsed;
            RoomMaskToggle.Visibility = Visibility.Collapsed;
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
        PageTitle.Text = resource.Name;
        PageSubtitle.Text = "Explore the scene and its possible paths.";
        PageSubtitle.ToolTip = resource.FilePath;
        PreviewTitle.Text = resource.Name;
        PreviewDescription.Text = "Reading the room background…";
        PreviewMessage.Visibility = Visibility.Visible;
        RoomPreview.Source = null;
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.IsChecked = false;
        RoomMaskToggle.IsEnabled = false;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        RoomNavigationToggle.Visibility = Visibility.Collapsed;
        ClearObjectList();
        ObjectsNote.Text = "Reading room objects…";
        _currentRoom = null;
        _objects = [];
        _objectSelection = new([]);
        _currentRoomResource = null;
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
            if (_game?.Id == GameId.IndianaJones3)
            {
                if (token.IsCancellationRequested) return;
                _currentRoom = room;
                _currentRoomResource = resource;
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
                RoomPreview.Visibility = Visibility.Visible;
                RoomMaskToggle.Visibility = Visibility.Visible;
                PreviewMessage.Visibility = Visibility.Collapsed;
                PageSubtitle.Text = "EGA background preview · object and script decoding is not yet verified.";
                StatusText.Text = $"{resource.Name} ready · {room.Width} × {room.Height}";
                StatusText.ToolTip = "Room background decoded from its individual LFL resource.";
                return;
            }
            var objectRecords = _objectDirectory.Decode(bytes);
            var objects = _objectDecoder.Decode(bytes);
            if (token.IsCancellationRequested) return;
            _currentRoom = room;
            _currentRoomResource = resource;
            _currentRoomObjectRecords = objectRecords;
            _roomVerbs = _verbReader.ReadAll(bytes, objectRecords);
            _roomObjectNames = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), objectRecords);
            _objects = objects;
            ApplyRoomState(resource, objectRecords, objects, visit: true);
            UpdateRoomNavigationActions(resource);
            RefreshRoomPreview();
            RoomPreview.Visibility = Visibility.Visible;
            RoomMaskToggle.Visibility = Visibility.Visible;
            PreviewMessage.Visibility = Visibility.Collapsed;
            StatusText.Text = $"{resource.Name} ready";
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
            _objectSelection = new RoomObjectSelection(images, snapshot.ObjectStates,
                snapshot.ObjectPositions, records, _currentRoom, _indexedStates, snapshot.DrawOrder);
            if (!snapshot.IsFaithful) CloseArtworkGaps(resource, images, snapshot.StatesSetByCode);
            var run = _session.LastEntryRun;
            var script = _session.LastEntryScript;
            RoomEntryTraceOutput.Text = (script?.Format() ?? "No embedded entry script.") +
                                        Environment.NewLine + "SESSION INTERPRETATION" + Environment.NewLine +
                                        (run?.FormatSummary() ?? "No entry run recorded.") +
                                        Environment.NewLine + "Known global variables: " + snapshot.KnownGlobalVariables.Count +
                                        "; inventory: " + (snapshot.InventoryKnown ? snapshot.Inventory.Count.ToString() : "unknown") +
                                        "; script-started sounds: " +
                                        (snapshot.ActiveSounds.Count == 0 ? "none known" : string.Join(", ", snapshot.ActiveSounds));
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
            ObjectsNote.Text = _estimatedObjectIds.Count == 0
                ? "Click a doorway box to move, or a room image to cycle its artwork. Hotspots run script interactions."
                : $"{_estimatedObjectIds.Count} scene details use an artwork estimate because the entry state is unresolved{EstimateSummary()}.";
            ShowLastObjectAction(resource.ResourceId);
            return;
        }

        ObjectActionPanel.Visibility = Visibility.Collapsed;
        SessionStatusText.Text = "Asset inspection: each room starts from the index; changes are local to this view.";
        SessionStatusText.ToolTip = null;
        RoomEntryTraceExpander.Header = "Advanced room details";
        AdvanceFrameButton.IsEnabled = false;
        _objectSelection = new RoomObjectSelection(images, _indexedStates, background: _currentRoom);
        _artworkEstimates = [];
        ObjectsNote.Text = images.Count == 0
            ? $"{records.Count} local object records; no decoded images. The room opens at its pre-entry index state."
            : $"{records.Count} local object records, {images.Count} stored images. The display uses pre-entry index states, not the first-visit game view. Click an image area to inspect its stored images.";
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
        if (_game is not null && _sceneBatches is null)
        {
            try { _sceneBatches = LoomSceneDrawBatches.Build(_game); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _sceneBatches = LoomSceneDrawBatches.Empty;
            }
        }
        var roomId = resource.ResourceId ?? 0;
        _objectSelection.ExcludeInterfaceImages((_sceneBatches ?? LoomSceneDrawBatches.Empty).InterfaceImages.Keys);
        _artworkEstimates = _objectSelection.FillGaps(_sceneBatches?.ForRoom(roomId), seed: roomId);
        if (!images.Any(_objectSelection.IsActive))
            _artworkEstimates = [.. _artworkEstimates, .. _objectSelection.ShowUnresolvedArtwork(_sceneBatches?.ForRoom(roomId), seed: roomId, statesSetByCode,
                _sceneBatches?.EntryStatesForRoom(roomId))];
        _estimatedObjectIds = _artworkEstimates.Select(item => item.ObjectId).ToHashSet();
    }

    private string EstimateSummary()
    {
        var sources = _artworkEstimates.Select(item => item.Reason).Distinct().ToArray();
        return sources.Length == 0 ? "" : $" (from {string.Join("; ", sources.Take(2))}{(sources.Length > 2 ? "; …" : "")})";
    }

    private void SessionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_currentRoomResource is not { } room || _currentRoom is null) return;
        ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: SessionModeToggle.IsChecked == true);
        UpdateRoomNavigationActions(room);
        RefreshRoomPreview();
    }

    private void ResetSession_Click(object sender, RoutedEventArgs e)
    {
        if (_game is null || _indexedObjectSnapshot is null) return;
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
        if (_session is null || _currentRoomResource is not { } room || _currentRoom is null) return;
        var snapshot = _session.AdvanceFrame();
        // A continued script can reach a room load; the session has already
        // entered that room, so show it as ActivateObjectVerb does.
        if (snapshot.RoomId is int reached && reached != room.ResourceId &&
            _game?.Rooms.FirstOrDefault(item => item.ResourceId == reached) is { } targetRoom)
        {
            StatusText.Text = $"Entered Room {reached:00}";
            StatusText.ToolTip = _session.LastAction?.Evidence;
            SelectResourceInTree(targetRoom);
            return;
        }
        ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
        UpdateRoomNavigationActions(room);
        RefreshRoomPreview();
    }

    /// <summary>Frames an exit click may continue outside a cutscene before falling back to browsing.</summary>
    /// <remarks>
    /// Delays and waits each take one session frame. Frames inside a cutscene do
    /// not count: the session follows it until it ends (up to 1000 frames), so
    /// Room 10 object 295 (script 052) passes through Room 11 and ends in Room 13
    /// after 65 frames. A click that falls short leaves its script pending, and
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
        StatusText.Text = $"Entered Room {reached:00}";
        StatusText.ToolTip = _session.LastAction?.Evidence;
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
        ObjectActionNote.Text = action.DestinationRoomId is int target
            ? $"Entered Room {target:00} through the reached room load. This is an exploration action on a state preview; exploration state remains incomplete."
            : _session!.Snapshot.PendingScripts > 0
                ? "No room load reached; the session has pending scripts. Exploration state remains incomplete."
            : "Interaction completed without opening another room. Exploration state remains incomplete.";
        ObjectActionNote.ToolTip = action.Evidence;
        ObjectActionTraceOutput.Text = action.Evidence + Environment.NewLine +
            string.Join(Environment.NewLine, action.Uncertainties.Select(item => "Unresolved: " + item)) +
            Environment.NewLine + (action.Script?.Format() ?? "Object bytecode unavailable.") +
            Environment.NewLine + (action.Run?.FormatSummary() ?? "No object bytecode was run.");
        ObjectActionTraceExpander.Visibility = Visibility.Visible;
    }

    private void ShowObjectVerbChoices(IReadOnlyList<RoomVerbChoice> choices)
    {
        ObjectActionPanel.Visibility = Visibility.Visible;
        ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
        ObjectVerbButtons.Children.Clear();
        ObjectActionNote.Text = choices.Count == 0
            ? "No decoded verb entry at this hotspot. Switch to inspection mode to view stored images."
            : "Choose an interaction. Hover for technical details.";
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
        if (_selectedResource is null || _game is not { Id: GameId.Loom, Version: ScummVersion.V3 })
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

    private void ShowCostume(GameResource resource)
    {
        _previewCancellation?.Cancel();
        _currentRoom = null;
        _objects = [];
        _selectedObject = null;
        ClearObjectList();
        CostumePreview.Source = null;
        CostumePreview.Visibility = Visibility.Collapsed;
        CostumePoseCombo.Items.Clear();
        CostumePoseCombo.IsEnabled = false;
        UpdateCostumePoseNavigation();
        _costumePoses = new Dictionary<int, RoomImage>();
        RoomPreview.Visibility = Visibility.Collapsed;
        RoomMaskToggle.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Collapsed;
        ScriptPanel.Visibility = Visibility.Collapsed;
        PreviewMessage.Visibility = Visibility.Collapsed;
        CostumePanel.Visibility = Visibility.Visible;
        RoomObjectPanel.Visibility = Visibility.Collapsed;
        Grid.SetColumnSpan(ContentPanel, 2);

        PageTitle.Text = resource.Name;
        PageSubtitle.Text = "Static pose preview · choose a pose below";
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
            var poses = new Dictionary<int, RoomImage>();
            _changingCostumePose = true;
            foreach (var slot in summary.AnimationSlots.Where(slot => slot.HasData))
            {
                try
                {
                    var pose = _costumeRenderer.DecodePose(container, resourceOffset, slot.Index);
                    if (pose is null) continue;
                    poses.Add(slot.Index, pose);
                    CostumePoseCombo.Items.Add(new ComboBoxItem { Content = $"Pose {slot.Index:00}", Tag = slot.Index });
                    availableCount++;
                }
                catch (InvalidDataException)
                {
                    damagedPoseCount++;
                }
            }
            _changingCostumePose = false;
            _costumePoses = poses;

            if (availableCount > 0)
            {
                CostumePoseCombo.IsEnabled = true;
                CostumePoseCombo.SelectedIndex = 0;
                ShowSelectedCostumePose();
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
            _changingCostumePose = false;
            CostumeMessage.Text = CostumeErrorMessage(ex);
            CostumeMessage.Visibility = Visibility.Visible;
            CostumePreview.Visibility = Visibility.Collapsed;
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

    private void CostumePoseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_changingCostumePose) ShowSelectedCostumePose();
    }

    private void PreviousCostumePoseButton_Click(object sender, RoutedEventArgs e) => MoveCostumePose(-1);

    private void NextCostumePoseButton_Click(object sender, RoutedEventArgs e) => MoveCostumePose(1);

    private void MoveCostumePose(int direction)
    {
        var nextIndex = CostumePoseCombo.SelectedIndex + direction;
        if (nextIndex < 0 || nextIndex >= CostumePoseCombo.Items.Count) return;
        CostumePoseCombo.SelectedIndex = nextIndex;
    }

    private void UpdateCostumePoseNavigation()
    {
        var selectedIndex = CostumePoseCombo.SelectedIndex;
        PreviousCostumePoseButton.IsEnabled = CostumePoseCombo.IsEnabled && selectedIndex > 0;
        NextCostumePoseButton.IsEnabled = CostumePoseCombo.IsEnabled &&
                                          selectedIndex >= 0 && selectedIndex < CostumePoseCombo.Items.Count - 1;
    }

    private void ShowSelectedCostumePose()
    {
        UpdateCostumePoseNavigation();
        if (CostumePoseCombo.SelectedItem is not ComboBoxItem { Tag: int poseIndex } ||
            !_costumePoses.TryGetValue(poseIndex, out var pose)) return;

        CostumePreview.Source = CreateCostumeBitmap(pose.Width, pose.Height, pose.Pixels.ToArray(), pose.Palette);
        CostumePreview.Visibility = Visibility.Visible;
        CostumeMessage.Visibility = Visibility.Collapsed;
        CostumeMessage.Text = "";
    }

    private async Task ShowAudio(GameResource resource, bool preserveFormat = false, bool autoPlay = false)
    {
        StopAudio();
        _audioSurface = AudioSurface.Resource;
        CostumePanel.Visibility = Visibility.Collapsed;
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
        PreviewMessage.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Visible;
        ClearObjectList();
        ObjectsNote.Text = "Choose a room to inspect its objects.";
        ObjectAssetPreview.Source = null;
        ObjectAssetPreview.Visibility = Visibility.Collapsed;
        ObjectAssetDescription.Text = "";
        PageTitle.Text = resource.Name;
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
        ObjectListSection.Visibility = ObjectList.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearObjectList()
    {
        ObjectList.Children.Clear();
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
        if (!TryGetRoomImagePoint(e.GetPosition(RoomPreview), out var x, out var y)) return;
        HandleRoomPoint(x, y);
    }

    private void HandleRoomPoint(int x, int y)
    {
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            // Room hotspots navigate. Among doorways at this point, the first
            // touchable one in directory order takes the click, as ScummVM's
            // findObject picks it. Untouchable doorways stay reachable as
            // labelled exploration actions.
            var doorways = DoorwaysAt(x, y);
            if (doorways.Count > 0)
            {
                var live = doorways.Where(record => !_session.Snapshot.IsUntouchable(record.Id)).ToArray();
                if (live.Length > 0) FollowDoorway(live[0]);
                else if (doorways.Count == 1) FollowDoorway(doorways[0]);
                else ShowDoorwayMenu(doorways.Select(record => (DoorwayLabel(record), DoorwayToolTip(record),
                    (Action)(() => FollowDoorway(record)))).ToArray());
                return;
            }
            var codeOnlyChoices = FindVerbChoicesAt(x, y)
                .Where(choice => !_objects.Any(image => image.Id == choice.Object.Id)).ToArray();
            if (codeOnlyChoices.Length > 0)
            {
                if (codeOnlyChoices.Length == 1)
                    ActivateObjectVerb(codeOnlyChoices[0].Object, codeOnlyChoices[0].Verb);
                else ShowObjectVerbChoices(codeOnlyChoices);
                return;
            }
            if (CycleArtworkAt(x, y)) return;
            var choices = FindVerbChoicesAt(x, y);
            if (choices.Count == 1) ActivateObjectVerb(choices[0].Object, choices[0].Verb);
            else ShowObjectVerbChoices(choices);
            return;
        }
        CycleArtworkAt(x, y);
    }

    private bool CycleArtworkAt(int x, int y)
    {
        var obj = _objectSelection.HitTest(x, y);
        if (obj is null) return false;
        _estimatedObjectIds = new HashSet<int>();
        ObjectsNote.Text = SessionModeToggle.IsChecked == true
            ? "Room-image choices are local to this view. Hotspots run script interactions."
            : "Asset inspection: click an image area to cycle its stored alternatives. The selection is local to this view.";
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
            return;
        }
        if (SessionModeToggle.IsChecked == true && _session is not null)
        {
            var doorways = DoorwaysAt(x, y);
            if (doorways.Count > 0)
            {
                var live = doorways.Where(record => !_session.Snapshot.IsUntouchable(record.Id)).ToArray();
                RoomPreview.Cursor = Cursors.Hand;
                RoomPreview.ToolTip = live.Length > 0 || doorways.Count == 1
                    ? $"Click to go to {DoorwayLabel(live.Length > 0 ? live[0] : doorways[0])}. {DoorwayToolTip(live.Length > 0 ? live[0] : doorways[0])}"
                    : $"Click to choose: {string.Join(" or ", doorways.Select(DoorwayLabel))}. None of these hotspots is live in this preview.";
                return;
            }
            var codeOnlyChoices = FindVerbChoicesAt(x, y)
                .Where(choice => !_objects.Any(image => image.Id == choice.Object.Id)).ToArray();
            if (codeOnlyChoices.Length > 0)
            {
                RoomPreview.Cursor = Cursors.Hand;
                RoomPreview.ToolTip = $"Click to interact with Object {codeOnlyChoices[0].Object.Id}. " +
                                      "This code-only hotspot takes priority over overlapping artwork; switch to inspection mode to cycle the image.";
                return;
            }
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
        RoomNavigationPanel.Visibility = _roomNavigationCandidates.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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

    private void UpdateRoomMusicActions()
    {
        RoomMusicButtons.Children.Clear();
        if (_game is null || _currentRoomResource is not { } room) return;
        var soundById = _game.Resources.Where(item => item.Kind == ResourceKind.Sound &&
                item.ResourceId.HasValue).GroupBy(item => item.ResourceId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        bool IsMusic(int id) => soundById.TryGetValue(id, out var resource) &&
            _adlibDecoder.HasMusic(resource);
        var reached = SessionModeToggle.IsChecked == true && _session is not null
            ? LoomSoundCueResolver.Resolve(_session.ReachedSoundEvents, IsMusic)
            : new LoomReachedSoundCue(null, false, 0);
        var possible = _roomSoundCandidates.Analyze(_game, room, _currentRoomObjectRecords);
        if (reached.Start?.TargetId is int currentId && soundById.TryGetValue(currentId, out var current))
        {
            SelectRoomCue(current, reached.Start.Format(), reached: true);
            RoomMusicNote.Text = "The cue above began on your route." +
                (reached.OtherActiveMusicCount > 0 ? " Other tracks may also be active." : "");
            RoomMusicNote.ToolTip = "The session follows reached sound commands, but does not simulate when a track ends naturally.";
        }
        else
        {
            var stillPossible = !_roomCueReached && _roomCueSourceRoomId == room.ResourceId &&
                _roomSelectedCue?.ResourceId is int selectedId &&
                possible.Any(item => item.SoundId == selectedId);
            if (!stillPossible) ClearRoomCue();
            RoomMusicNote.Text = reached.HasUnknownSoundTarget
                ? "The music for this visit is uncertain."
                : "No music has been confirmed here. These tracks may play in this room.";
            RoomMusicNote.ToolTip = reached.HasUnknownSoundTarget
                ? "A reached sound command has a target that the bounded interpreter could not resolve."
                : "Possible tracks come from static room and object script paths; those paths have not been reached.";
        }

        foreach (var group in possible.Where(item => item.SoundId is int id && IsMusic(id) &&
                     item.SoundId != reached.Start?.TargetId)
                 .GroupBy(item => item.SoundId!.Value).OrderBy(item => item.Key))
        {
            var target = soundById[group.Key];
            var evidence = string.Join(Environment.NewLine, group.Select(item => item.Evidence));
            var button = new Button
            {
                Content = $"▶ {target.Name}",
                ToolTip = "Static script possibility; execution has not reached this cue." +
                          Environment.NewLine + evidence,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 0, 3),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += async (_, _) =>
            {
                SelectRoomCue(target, evidence, reached: false);
                await PlayRoomCueAsync();
            };
            RoomMusicButtons.Children.Add(button);
        }
        if (possible.Any(item => item.SoundId is null))
            RoomMusicButtons.Children.Add(new TextBlock
            {
                Text = "Another possible cue has an unknown track.",
                ToolTip = string.Join(Environment.NewLine,
                    possible.Where(item => item.SoundId is null).Select(item => item.Evidence)),
                Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)),
                TextWrapping = TextWrapping.Wrap
            });
        var possibleTracks = RoomMusicButtons.Children.OfType<Button>().Count();
        RoomMusicMoreHeader.Text = possibleTracks == 0 ? "Other possible music" : $"Other possible music · {possibleTracks}";
        if (RoomMusicButtons.Children.Count == 0)
            RoomMusicButtons.Children.Add(new TextBlock
            {
                Text = "No other local music candidates found.",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)),
                TextWrapping = TextWrapping.Wrap
            });
        RoomMusicPanel.Visibility = Visibility.Visible;
    }

    private void SelectRoomCue(GameResource sound, string evidence, bool reached)
    {
        if (_roomSelectedCue != sound && _audioSurface == AudioSurface.Room) StopAudio();
        _roomSelectedCue = sound;
        _roomCueSourceRoomId = _currentRoomResource?.ResourceId;
        _roomCueReached = reached;
        RoomCueTitle.Text = $"{(reached ? "Started" : "Possible")}: {sound.Name}";
        RoomCueEvidence.Text = reached ? "from this session's route" : "from room script data";
        RoomCueEvidence.ToolTip = evidence;
        RoomCueTitle.ToolTip = evidence;
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

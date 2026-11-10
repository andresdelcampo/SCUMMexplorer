using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    /// <summary>
    /// Everything that belongs to the open game (10 November 2026 audit, roadmap step 1). <see cref="OpenGameFolder"/> replaces the whole object, so
    /// nothing of the game before can stay behind and a new field needs no reset line. Put a field here when its value is about the game, its rooms
    /// or a session of it. What stays on the window: its widgets and view options, the sound devices, the counters that tell an old background task
    /// its result is no longer wanted (they must keep counting across games) and the counts of work in flight.
    /// </summary>
    private GameContext _ctx = new();

    private sealed class GameContext
    {
        /// <summary>Ends what the context left running: its timers and its cancellable background work. The window has stopped play, playback and
        /// sound before; this is for what no stop routine reaches.</summary>
        public void Retire()
        {
            PlayTimer?.Stop();
            PlaybackTimer?.Stop();
            GameClickTimer?.Stop();
            foreach (var cancellation in new[] { RoomCardCancellation, PreviewCancellation, CostumeCardCancellation, CostumeColorCancellation,
                         AudioCancellation, DurationCancellation })
                cancellation?.Cancel();
            RoomCardBackgroundsReady.TrySetResult(false);
        }

        // ---- Play: the generic session, its screen, scene lists, overlays, Interact and Game Status ----
        public GameLibrary? GenericActionsGame;
        public int? GenericActionsRoom;
        public bool GenericActionsAdvanced;
        public bool GenericActionStarting;
        public int GenericNamesFrame;
        public Func<string, string>? GenericReadable;
        public readonly List<Button> GenericActionButtons = [];
        public readonly List<(string Label, ScummScriptDisassembly Code)> GenericRoomScripts = [];
        public readonly List<GenericSourceAction> GenericSourceActions = [];
        public RoomPanelAction? GenericLastAction;
        public IReadOnlyList<string> GenericAssumptions = [];
        public string? GenericSourceFailure;
        public int? GenericActionsShownFor;
        // The data behind the shared Interact control (R-6.22, R-16.9): the selected object and its source actions.
        // The button, menu and right click are the shared ones in MainWindow.RoomControls.cs.
        public int? GenericInteractTarget;
        public ScummGameSession? InventoryUndoSession;
        public ScummInventoryUndoToken? InventoryLastUndo;
        /// <summary>The game's values beyond the inventory (Indy 3: health, outfit, IQ points, story flags), read once per opened game.</summary>
        public (string Key, ScummStatusSchema Schema)? StatusSchema;
        public ScummGameSession? Play;
        public ScummPlayScreen? PlayScreen;
        public IReadOnlyDictionary<int, byte> PlayIndexedStates = new Dictionary<int, byte>();
        /// <summary>The <see cref="GameLibrary.Key"/> (folder, and language when the game holds several) of the game the play session runs.</summary>
        public string? PlayFolder;
        public DispatcherTimer? PlayTimer;
        public bool PlayPaused;
        public int PlayFrame;
        public ScummPlayFrame? PlayShown;
        public bool FollowingPlayRoom;
        public WriteableBitmap? PlayBitmap;
        // The play game's sounds that play a track of its disc (null for a game without them).
        public ScummCdAudioTracks? PlayCdTracks;
        // The play game's iMUSE songs (v5: Monkey Island 2, Fate of Atlantis), for where a looping song goes on (null for a game without them).
        public ScummImuseMusic? PlayImuse;
        /// <summary>The session's speech events before the last room visit from the list (a cutscene played from the list speaks its entry from there).</summary>
        public int PlaySpeechBeforeVisit;
        /// <summary>
        /// R-8.5b (rule confirmed by the user 20 Oct): the play session is a static preview's, running only the room's ambient scripts
        /// (<see cref="ScummSession.PrepareAmbientRoom"/>); no click or key reaches the game. <see cref="PlayLiveSource"/> is the session it was made from.
        /// </summary>
        public bool PlayAmbient;
        /// <summary>The kids were chosen by the reviewed assumption, not by the player (R-6.52); Advanced says so.</summary>
        public bool PlayPartyAssumed;
        public ScummGameSession? PlayLiveSource;
        // The pause state the play sounds were last given (S6 sound).
        public bool PlaySoundPaused;
        /// <summary>Set while a room pick in a played game keeps the play screen showing (no loading panel, no empty room view; cleared once the new
        /// room's picture is on the screen).</summary>
        public bool KeepPlayScreen;
        public Lazy<ScummCutscenePlanner>? PlayPlanner;
        public Dictionary<string, int[]>? PlaySavedRoomLists;
        public bool PlayRoomListsDone;
        public int PlayControlsFrame;
        public bool PlayControlsInCutscene;
        public ScummCutscene? SceneOpening;
        /// <summary>The game as the boot left it, from which every picked scene begins (R-8.5; null in Maniac Mansion and Zak McKracken, whose party is the player's).</summary>
        public ScummBootSnapshot? PlayBoot;
        public GameLibrary? PlayOverlayGame;
        public int? PlayOverlayRoom;
        public Dictionary<int, int[]> PlayDoorwayTargets = [];
        public (int X, int Y)? PlayOverlayPointer;
        public int? PlayHighlightId;
        public ScummRoomScene? PlayOverlayScene;
        public Dictionary<int, string> PlayOverlayNames = [];
        public Dictionary<int, int> PlayOverlayLocalIndex = [];
        public string? PlayOverlayListKey;
        public UIElement[] PlayOverlayObjectRows = [];
        public UIElement[] PlayOverlayDoorwayRows = [];
        public string? PlayOverlayHoverKey;
        public IReadOnlyList<RoomHotspot> PlayOverlayHotspots = [];
        public GameLibrary? GenericSoundGame;
        public int? GenericSoundRoom;
        public int GenericSoundEventCount = -1;
        public Dictionary<int, string> GenericSoundCandidates = [];
        public Task GenericRoomSoundsTask = Task.CompletedTask;

        // ---- LOOM: its session, catalog, planners, playback and CD data ----
        // R-8.5b (rule confirmed by the user 20 Oct): a LOOM static preview explores an ambient-only copy (ScummSession.PrepareAmbientRoom); this is the
        // session it was made from, which carries the game on when another room or a scene is opened.
        public LoomExplorationSession? LoomAmbientSource;
        public LoomReplayCatalog? ReplayCatalog;
        public LoomSceneSetupPlanner? SetupPlanner;
        public LoomCutsceneReadiness? Readiness;
        public string? PendingCatalogPreparation;
        public readonly Dictionary<string, LoomStartSetup> StartSetups = [];
        public LoomRelationshipIndex? RelationshipIndex;
        /// <summary>The CD game's first-visit scene of the room shown (its actors are drawn from it); null for other games.</summary>
        public LoomCdScene? CdScene;
        // The CD game's data source for the room view (null for the other games).
        public IScummDataSource? CdData;
        public LoomExplorationSession? Session;
        // Charset 0 (99.LFL) for messages, or why it could not be read (then a labelled UI font is used).
        public ScummV3Charset? Charset;
        public string? CharsetError;
        // The CD game's charset 2, the one script 001 sets when the intro ends (+0x015E); script 044 sets 4 for a while.
        public ScummV4Charset? CdCharset;
        public readonly Dictionary<int, ScummV4Charset?> CdCharsets = [];
        public LoomDoorwayFinder? DoorwayFinder;
        /// <summary>How many uncertainties the session had logged at its last room visit; Advanced lists the ones after it.</summary>
        public int IssueBaseline;
        public IReadOnlyList<LoomCdCue> CdCues = [];
        public ScummRoomObjectRecord? InteractTarget;
        // The game's click: a single click waits for the system double-click time before it is sent.
        public DispatcherTimer? GameClickTimer;
        public (int X, int Y, int? Room)? PendingGameClick;
        public bool GameClickPairSent;
        // Why the last room click fell back to a doorway's direct verb (for the harnesses and the tooltip).
        public string? LastDoorwayFallback;
        // Why the last doorway click did not fall back: the game answered it with a scene.
        public string? LastDoorwayHeld;
        public Playback? Playback;
        public LoomReplaySequence? ReplaySequence;
        public DispatcherTimer? PlaybackTimer;
        // True while an instant playback runs all its frames at once: their sounds are sorted out when it finishes.
        public bool PlaybackBatch;
        // True while the playback itself opens the room the session entered.
        public bool PlaybackEnteringRoom;
        // The room the playback last asked the view to open.
        public int? PlaybackRequestedRoom;
        // True while the user paused a playback: its frames wait, and nothing else is sent to the game.
        public bool PlaybackPaused;
        public string? IdleSignature;
        public LoomExplorationSession? IdleSession;
        public DateTime IdleNextFrame;
        // The talk text's rectangle in room pixels (right and bottom exclusive), for a click that skips it.
        public (int Left, int Top, int Right, int Bottom)? TalkTextRect;
        // Verb-screen text (screen rows 144–199) as a 320 × 56 layer, or null.
        public BitmapSource? VerbMessageBitmap;
        // The room and the object under the pointer (0: none) when a frame last ran for the game pointer.
        public (int? Room, int? Target)? GamePointerTarget;
        public LoomExplorationSession? PointerSession;
        public string? VerbScreenNote;
        public IReadOnlyList<ScummCutsceneGroup>? CdCutsceneGroups;
        public Lazy<LoomCdSceneSetupPlanner>? CdSetupPlanner;
        /// <summary>
        /// The shared scene engine (<see cref="ScummCutscenePlanner"/>, the one Indy 3, Monkey Island and Indy 4 use; ROADMAP 2b, R-8.36) on LOOM's own session:
        /// how each scanned scene is set up and started. Measured against the English catalog's 238 reviewed replays: all begin and reach the catalog's rooms,
        /// 91% within 10% of its length (.verification/loom-editions-20261031). <see cref="CdSetupPlanner"/> keeps only the room lists.
        /// </summary>
        public Lazy<ScummCutscenePlanner>? LoomScenePlanner;
        /// <summary>Room lists saved by an earlier run (same build and game data), used until the background runs finish.</summary>
        public Dictionary<string, int[]>? CdSavedRoomLists;
        public bool CdRoomListsDone;
        /// <summary>Steps of the background scan (a run and a start setup per scene on EGA; the CD counts its setups and its runs): <c>ScanDone</c> of <c>ScanTotal</c>.</summary>
        public int ScanTotal, ScanDone;
        public LoomPanelEntry? LoomPanelEntry;
        public IReadOnlyList<LoomCdCue> CdRoomCues = [];

        // ---- The game and the room view: names, the room shown, its objects, cards and sounds ----
        public CancellationTokenSource? CostumeCardCancellation;
        public GameLibrary? CostumeColorGame;
        public GameResource? CostumeColorResource;
        public CancellationTokenSource? CostumeColorCancellation;
        public bool CostumeColorsLoading;
        public GameLibrary? InspectionFor;
        public Task<GameLibrary>? InspectionCopy;
        public RoomOpenKind RoomOpenKind;
        public bool CatalogPreviewOnly;
        public bool IntroPreparing;
        public string? PendingStaticPreviewNote;
        public readonly Dictionary<GameResource, RoomCardPreviewStage> RoomCardStages = [];
        public Task? RoomCardLoadTask;
        public RoomCardLoadPhase RoomCardLoadPhase;
        public bool RoomCardCacheCommitted;
        public TaskCompletionSource<bool> RoomCardBackgroundsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public GameResource? RoomNavigationRoom;
        public int? HoveredHotspotRowId;
        public readonly Dictionary<GameResource, string> SoundLines = new();
        public ScummScriptReader ScriptReader = new();
        public ScummObjectVerbReader VerbReader = new();
        public GameLibrary? Game;
        public GameResource? SelectedResource;
        public GameResource? LastViewedRoomResource;
        public CancellationTokenSource? RoomCardCancellation;
        public CancellationTokenSource? PreviewCancellation;
        public RoomImage? CurrentRoom;
        public GameResource? CurrentRoomResource;
        public ScummWalkBoxSet? CurrentWalkBoxes;
        // A browsed old-bundle game's room as the generic session leaves it on the first visit (Indiana Jones and the Last Crusade EGA), or null (plain background).
        public ScummRoomScene? BrowsedScene;
        public IReadOnlyList<ScummRoomObjectRecord> CurrentRoomObjectRecords = [];
        public IReadOnlyDictionary<int, string> RoomObjectNames = new Dictionary<int, string>();
        /// <summary>True when <see cref="RoomObjectNames"/> are the names a browsed room stores (shown on its stored-image rows), not a live session's.</summary>
        public bool RoomNamesAreStored;
        public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> RoomVerbs =
            new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
        public IReadOnlyList<RoomVerbDestination> RoomNavigationCandidates = [];
        public IReadOnlyList<RoomObjectImage> Objects = [];
        public RoomObjectSelection ObjectSelection = new([]);
        public IReadOnlySet<int> EstimatedObjectIds = new HashSet<int>();
        public IReadOnlyDictionary<int, byte> IndexedStates = new Dictionary<int, byte>();
        public ScummIndexedObjectSnapshot? IndexedObjectSnapshot;
        // Costumes read for the actor layer (M2c), by id; null when a costume cannot be read.
        public readonly Dictionary<int, ScummCostume?> ActorCostumes = [];
        public int? SelectedObject;
        public string? AudioFile;
        public byte[]? AdlibBaseWave;
        public CancellationTokenSource? AudioCancellation;
        public bool AudioReady;
        public bool RestoreAudioAfterGainChange;
        public bool ResumeAudioAfterGainChange;
        public TimeSpan AudioPositionAfterGainChange;
        public GameResource? SelectedSound;
        public bool PlayWhenReady;
        public AudioSurface AudioSurface;
        public GameResource? RoomSelectedCue;
        public GameResource? RoomRenderedCue;
        public int? RoomCueSourceRoomId;
        public bool RoomCueReached;
        public bool RoomCueManuallySelected;
        public readonly Dictionary<int, TimeSpan> RoomCueDurations = [];
        public readonly Dictionary<int, (bool IsMusic, TimeSpan? Duration)> RoomCueTrackCache = [];
        public ScummSceneDrawBatches? SceneBatches;
        public IReadOnlyList<RoomArtworkEstimate> ArtworkEstimates = [];
        public Task<ScummSceneDrawBatches>? DrawBatchesTask;
        public GameLibrary? DrawBatchesGame;
        /// <summary>The objects drawn in the shown room of a v1 or v2 game, for the names the pointer reveals (empty for other games).</summary>
        public IReadOnlyList<BrowsedObjectLabel> OlderObjects = [];
        public BrowsedObjectLabel? BrowsedHover;
        public bool RestoringInteractiveView;
        public string? RoomHint;
        public GameNames Names = new(LoomGlossary.Default, null, "the hero");
        public LoomUsageIndex UsageIndex = LoomUsageIndex.Empty;
        public ScummScriptDisassembly? ScriptShown;
        // The session's camera x for the room just entered, applied once the view is laid out.
        public int? PendingCameraX;
        // "Sound NN - mm:ss" in the Advanced tree: the length of the sound on the selected device, rendered in the background (the devices differ:
        // the PC speaker and PCjr parts of one sound can be seconds apart), so it follows the device and is filled in as each sound is done.
        public CancellationTokenSource? DurationCancellation;
        public (GameLibrary? Game, SceneSoundPlayer.SoundMode Mode) DurationKey;
        public readonly System.Collections.Concurrent.ConcurrentDictionary<(SceneSoundPlayer.SoundMode Mode, GameResource Resource), string?> DurationLabels = new();
        // The length of the rendered wave (null until it exists) and, when no wave can exist, what to show instead of a length.
        public TimeSpan? AudioKnownDuration;
        public string? AudioTimeNote;
        public HashSet<int> HoveredDoorwayIds = [];
        public ScummRoomObjectRecord? HighlightedHotspot;
        public int? RoomMusicActionsRoom;
        public List<(GameResource Resource, bool Music, TimeSpan? Duration, string Evidence)> RoomSoundChoices = [];

        // <fields>
    }
}

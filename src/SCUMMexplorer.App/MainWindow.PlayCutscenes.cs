using System.IO;
using System.Windows;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// Cutscenes of a game played through its verbs (S7, 17 October 2026; first games: Indiana Jones and the Last Crusade, EGA and 256 colours): the
/// list comes from the game's scripts (<see cref="ScummCutscenePlanner"/>: the engine's scan, titles with the game's own verb names, one headless run per
/// scene from a fresh start in the background, with the progress line and the room lists as for LOOM CD), and a scene plays in the play screen
/// (<see cref="ScummPlayScreen"/>) of the running play session, with its sounds; Space or Pause pauses, Esc or Skip skips from the override point.
/// Offered where the registry's Cutscenes cell is above None for the family.
/// </summary>
public partial class MainWindow
{
    private Lazy<ScummCutscenePlanner>? _playPlanner;
    private Dictionary<string, int[]>? _playSavedRoomLists;
    private bool _playRoomListsDone;
    private int _playControlsFrame;
    private bool _playControlsInCutscene;

    /// <summary>The open game lists its cutscenes from its scripts and plays them in the play screen.</summary>
    private bool PlayCutscenes => _game is { } game && !IsLoomGame && ScummProfile.For(game).PlaysThroughVerbs && GameSupport.For(game).Cutscenes;

    private bool PlayCanStartCutscene => PlayActive && !_play!.InCutscene;

    private static bool UsesManiacCutsceneSetup(GameLibrary game) =>
        GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;

    private static ScummCutscenePlanner NewPlayCutscenePlanner(GameLibrary game) => UsesManiacCutsceneSetup(game)
        ? SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(game) : new(game);

    private bool PreparePlayCutsceneStartup(GameLibrary game)
    {
        if (!UsesManiacCutsceneSetup(game) || _play is not { } session) return true;
        try
        {
            if (SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.PrepareIfNeeded(game, session))
                _sceneSound.StopAll("the cutscene's disclosed startup preview was prepared");
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            StatusText.Text = "The scene could not start here. Finish choosing your characters first.";
            StatusText.ToolTip = IsAdvancedMode ? ex.Message : null;
            return false;
        }
    }

    /// <summary>Derives the list and runs every scene once from a fresh start, off the UI thread, from the moment the game opens.</summary>
    private void StartPlayCutsceneDerivation(GameLibrary game)
    {
        _playPlanner = null;
        _playSavedRoomLists = null;
        _playRoomListsDone = false;
        if (!PlayCutscenes) return;
        _playSavedRoomLists = _roomCardsMayUseCache ? CdRoomScenesCache.Load(game.FolderPath, game.Language) : null;
        // A game read from one archive or disk set shares its reader between sessions, so the scan and each background worker read from their own
        // copy of the game (their own reader), never the play session's: Monkey Island floppy, 164 runs, 109 s on one thread, 36 s on eight
        // (17 October 2026; more workers were slower), and a scan through the play session's reader while it played lost 11 groups. A file-per-room
        // game shares the one copy, as before. Since the cutscene snapshot (18 October 2026) a disk or archive game's runs start from a pool of boots,
        // each of its own copy and used by one run at a time (ScummBootSnapshotPool: at most one boot per worker); a file-per-room game boots once.
        var ownReader = game.V4Archive is not null || game.V4Disks is not null;
        GameLibrary Copy() => SCUMMexplorer.Core.Discovery.GameFolderScanner.Reopen(game); // same folder and language (R-3.16)
        var planner = new Lazy<ScummCutscenePlanner>(() =>
        {
            if (!ownReader) return NewPlayCutscenePlanner(game);
            // The planner's own copy also serves the first boot: the planner reads it only while it is built (the scan, before any run), and the runs
            // ask it only for its room list, so the pool's first snapshot may own its reader (one scan of the game fewer).
            GameLibrary? first = Copy();
            var scanned = first;
            return new ScummCutscenePlanner(scanned, () => ScummGameSession.BootForPlay(Interlocked.Exchange(ref first, null) ?? Copy(), out _),
                start: ScummCutsceneStart.SnapshotPerWorker);
        });
        _playPlanner = planner;
        _scanDone = 0; _scanTotal = 0;
        if (!_sceneSound.IsPreparing) _scanGate.Set();
        _ = Task.Run(() =>
        {
            try
            {
                var groups = planner.Value.Groups;
                if (!ReferenceEquals(_playPlanner, planner)) return;
                _scanTotal = groups.Count;
                Dispatcher.BeginInvoke(() => { if (_playPlanner == planner) UpdatePlaybackControls(); });
                // Each run starts from a copy of a boot (ScummBootSnapshot, 18 October 2026), not from a boot of its own (Monkey Island 2 booted 260 times
                // before, Indy 4 426 times). The entries are handed out one at a time, so a worker that drew long scenes does not hold the others back (a list's
                // default split gives each worker a fixed range: Monkey Island 2, 85 s wall for 189 s of work on eight workers).
                // A disk or archive game runs on at most four workers (executor allocation speed-up, 19 October 2026): its runs got slower per worker as
                // workers were added, so that eight took longer than four (CutsceneList prun, 16-core machine: Monkey Island 2 1/2/4/8 workers 30/20/26/56 s,
                // Indy 4 2/4/8 workers 51/42/75 s; the same lists whatever the count). Record: .verification/executor-alloc-20261019/README.md.
                var parallel = Math.Max(1, ownReader ? Math.Min(4, Environment.ProcessorCount / 2) : Environment.ProcessorCount / 2);
                Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(groups, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), new ParallelOptions { MaxDegreeOfParallelism = parallel }, group =>
                {
                    _scanGate.Wait();
                    planner.Value.Run(group.First);
                    CountScanStep(planner);
                });
                CdRoomScenesCache.Save(game.FolderPath, planner.Value.ExportRoomLists(), game.Language);
                Dispatcher.BeginInvoke(() => { if (_playPlanner == planner) { _playRoomListsDone = true; UpdatePlaybackControls(); } });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
            {
                // A rejected/unreviewed start offers no scenes; its progress indicator must finish too.
                Dispatcher.BeginInvoke(() => { if (_playPlanner == planner) { _playRoomListsDone = true; UpdatePlaybackControls(); } });
            }
        });
    }

    private static string PlayCutsceneKey(ScummCutscene trigger) => $"{trigger.Room},{trigger.ObjectId},{trigger.VerbId}";

    private bool PlayCutscenePlaysIn(ScummCutscenePlanner planner, ScummCutsceneGroup group, int room) =>
        planner.PlaysIn(group, room) || _playSavedRoomLists is { } saved && saved.TryGetValue(PlayCutsceneKey(group.First), out var rooms) && rooms.Contains(room);

    /// <summary>The list's entries (all, or those that play out in <paramref name="room"/>). The room's list waits for the scan (no entries until then).</summary>
    private List<(string Id, CutscenePickerItem Item)> PlayCutsceneItems(int? room = null)
    {
        if (_playPlanner is not { } lazy || room is not null && !lazy.IsValueCreated) return [];
        ScummCutscenePlanner planner;
        try { planner = lazy.Value; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException) { return []; }
        // R-1.13: in Normal mode a scene whose fresh-start run began no cutscene is not offered (nothing would play); Advanced lists it with its note.
        return planner.Groups.Where(group => room is null || PlayCutscenePlaysIn(planner, group, room.Value))
            .Where(group => IsAdvancedMode || !planner.TryGetRun(group.First, out var run) || run.Begun)
            .Select(group => ($"scumm-{group.First.Room}-{group.First.ObjectId}-{group.First.VerbId}", new CutscenePickerItem(
                PickerVerbTitle(ScummCutsceneTitles.Title(group.First), group.First.IsRoomEntry ? 0 : group.First.VerbId) +
                (room is int shown && group.First.Room != shown && group.Others.All(o => o.Room != shown) ? $" (played from Room {group.First.Room:00})" : ""),
                group.IsScene ? "Scenes" : "Dialogue and reactions", PlayCutsceneNote(planner, group), () => PlayScummCutscene(group)))).ToList();
    }

    /// <summary>The note behind an entry (R-8.13): the source, what is possible rather than certain, the fresh-start run and the stated start.</summary>
    private static string PlayCutsceneNote(ScummCutscenePlanner planner, ScummCutsceneGroup group)
    {
        var first = group.First;
        var others = group.Others.Count == 0 ? "" : " The same scripts are reached from " + string.Join(", ",
            group.Others.Select(o => $"{(o.Room != first.Room ? $"Room {o.Room:00} " : "")}object {o.ObjectId}{(o.ObjectName.Length > 0 ? $" ({o.ObjectName})" : "")} {o.VerbLabel}")) + ".";
        return $"Found in the game's scripts: {first.Room:00} · {(first.IsRoomEntry ? "the room's entry script" : $"object {first.ObjectId}'s {first.VerbLabel}")} " +
               $"reaches {string.Join(", ", first.Scripts)} (a cutscene ... endCutscene span of {first.SpanInstructions} instructions, {first.Texts} printed line(s), {first.RoomLoads} room load(s)). " +
               "A branch can keep part of it from running, so the lines and loads are possible, not certain." + others + " " +
               planner.StartNote(first) + " " +
               "Stated start: the running play session (its state is kept), the room entered as a state preview when the session is elsewhere (the hero's arrival is assumed), " +
               (first.IsRoomEntry ? "then the room's entry scripts played again. " :
                   $"then the object's verb 0x{first.VerbId:X2} run directly: an exploration action, not the game's click (the hero does not walk there, and the object may be untouchable in this state). ") +
               "The frames play in the play screen on the game's clock, with its sounds.";
    }

    /// <summary>
    /// Plays a scene in the play screen: the room is opened from the list when another is shown (its play visit is a state preview), the running
    /// session is kept, and the room's entry plays again or the object's verb runs directly.
    /// </summary>
    private async void PlayScummCutscene(ScummCutsceneGroup group)
    {
        var trigger = group.First;
        if (_game is not { } game || game.Rooms.FirstOrDefault(r => r.ResourceId == trigger.Room) is not { } room) return;
        var launch = ++_cutsceneLaunchGeneration;
        _introPreparing = false;
        if (!PreparePlayCutsceneStartup(game)) return;
        var visitedForIt = false;
        if (_currentRoomResource?.ResourceId != trigger.Room)
        {
            visitedForIt = true;
            SelectRoomWithIntent(room, RoomOpenKind.Cutscene);
            // The room view boots or continues play in the opened room; wait for it (a first boot takes a few seconds).
            for (var wait = 0; wait < 600 && !(PlayActive && (_play!.CurrentRoomId == trigger.Room || UsesManiacCutsceneSetup(game) && _play.CurrentRoomId == 45)); wait++)
            {
                if (launch != _cutsceneLaunchGeneration || !ReferenceEquals(_game, game)) return;
                await Task.Delay(50);
            }
        }
        if (launch != _cutsceneLaunchGeneration || !ReferenceEquals(_game, game)) return;
        if (!PlayActive || _play is not { } session) return;
        if (!PreparePlayCutsceneStartup(game)) return;
        _playPaused = false;
        var title = PickerVerbTitle(ScummCutsceneTitles.Title(trigger), trigger.IsRoomEntry ? 0 : trigger.VerbId);
        IReadOnlyList<string> uncertainties;
        try
        {
            if (trigger.IsRoomEntry)
            {
                // A room opened for the scene has just run its entry (the play visit, as the fresh-start run does); entering it again would run the
                // entry a second time from the state the first one left, and an entry that marks its first time (Monkey Island CD's Room 19, the
                // ship) then plays nothing (S7 MonkeyCd, 18 October 2026). Only a room the player is already in is entered again.
                if (!visitedForIt || session.CurrentRoomId != trigger.Room)
                {
                    _sceneSound.StopAll("a room's entry cutscene was started from the list");
                    session.EnterRoomAgain(room);
                }
                // The visit that opened the room is the scene here, not a state preview: a talkie speaks its lines from the visit on (S7 Indy4).
                else if (game.Speech is { } speech) _playSpeech.Attach(speech, session.Messages, _playSpeechBeforeVisit);
                uncertainties = session.Snapshot.Uncertainties;
            }
            else
            {
                if (session.CurrentRoomId != trigger.Room)
                {
                    _sceneSound.StopAll("a cutscene in another room was started from the list");
                    session.VisitRoom(room);
                }
                uncertainties = session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId).Uncertainties;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            StatusText.Text = $"{title} could not start here.";
            StatusText.ToolTip = IsAdvancedMode ? ex.Message : null;
            return;
        }
        ShowPlayFrame();
        PlayScreenImage.Focus();
        StatusText.Text = $"Playing {title} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = IsAdvancedMode && _playPlanner is { IsValueCreated: true } lazy
            ? PlayCutsceneNote(lazy.Value, group) + Environment.NewLine + string.Join(Environment.NewLine, uncertainties)
            : null;
        UpdatePlaybackControls();
    }

    /// <summary>The playback bar during play: Pause, Skip, Skip line and the room's Play Cutscene button, with the scan's note and bar.</summary>
    private void UpdatePlayCutsceneControls()
    {
        var active = PlayActive;
        PlaybackBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        InteractButton.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        AdvanceFrameButton.IsEnabled = false;
        UpdatePlayTransportControls();
        var canStart = PlayCanStartCutscene;
        CutsceneMenuButton.IsEnabled = active && canStart && _playPlanner is not null;
        CutsceneMenuButton.Content = "Play Cutscene";
        CutsceneMenuButton.ToolTip = canStart ? "Choose a cutscene from anywhere in the game." : "A cutscene is playing. Let it finish or skip it before starting another.";
        var roomChoices = _currentRoomResource?.ResourceId is int shownRoom ? RoomCutsceneItems(shownRoom) : [];
        var computing = _playPlanner is not null && !_playRoomListsDone && _playSavedRoomLists is null;
        var listing = computing && roomChoices.Count == 0;
        RoomCutsceneButton.Visibility = active && (roomChoices.Count > 0 || listing) ? Visibility.Visible : Visibility.Collapsed;
        RoomCutsceneButton.IsEnabled = active && canStart && !listing;
        RoomCutsceneButton.Content = ButtonWithSpinner(roomChoices.Count == 1 || listing ? "Play Cutscene" : "Play Cutscene ▴", computing);
        RoomCutsceneButton.ToolTip = listing ? "Looking for the scenes that happen in this room… (the first launch takes a while; the result is kept for the next one)."
            : !canStart ? "A cutscene is playing. Let it finish or skip it before starting another."
            : roomChoices.Count == 1 ? $"Play {roomChoices[0].Title}" : "Choose a cutscene that happens in this room.";
        CutsceneScanNote.Visibility = active && (computing || ScanRunning) ? Visibility.Visible : Visibility.Collapsed;
        CutsceneScanProgress.Visibility = active && ScanRunning ? Visibility.Visible : Visibility.Collapsed;
    }

    // Native transport state is cheap and must follow every frame: a cutscene's
    // override can appear or disappear between room-list refreshes.
    private void UpdatePlayTransportControls()
    {
        var active = PlayActive;
        UpdateSoundDeviceControls();
        PlaybackPauseButton.IsEnabled = active;
        PlaybackPauseButton.Content = _playPaused ? "▶ Play" : "⏸ Pause";
        PlaybackPauseButton.ToolTip = "Pause or resume the game, including walking, dialogue and cutscenes (Space).";
        var inCutscene = active && _play!.InCutscene;
        var skippable = inCutscene && _play!.CutsceneSkippable;
        SkipCutsceneButton.IsEnabled = skippable;
        SkipCutsceneButton.ToolTip = !inCutscene ? "No cutscene is playing."
            : skippable ? "Skip the cutscene (Escape): the script that set the override goes on at its skip point."
            : "The cutscene has not set its skip point yet, so Escape would do nothing now, as in the game.";
        SkipLineButton.IsEnabled = active && _play!.Messages.HaveMsg != 0;
    }

    private void SkipPlayCutscene()
    {
        if (!PlayActive || !_play!.CutsceneSkippable) return;
        _play.PressKey(0x1B);
        _playPaused = false; // As in LOOM, Skip continues from the native skip point.
        UpdatePlaybackControls();
    }

    /// <summary>Transport follows every frame; the derived room list refreshes a few times a second.</summary>
    private void RefreshPlayControlsOnFrame()
    {
        if (!PlayCutscenes) return;
        UpdatePlayTransportControls();
        var inCutscene = _play?.InCutscene == true;
        var changed = inCutscene != _playControlsInCutscene;
        _playControlsInCutscene = inCutscene;
        if (changed || ++_playControlsFrame % 12 == 0) UpdatePlayCutsceneControls();
    }
}

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
    private bool PlayCutscenes => _game is { } game && !HasDedicatedSession && ScummProfile.For(game).PlaysThroughVerbs && GameSupport.For(game).Cutscenes;

    // §8.6 (user, 31 Oct 2026: explore everything): a pick while a scene plays ends that scene, as a manual room change does (R-8.5a); only a boot
    // still running (the opening, a startup choice) keeps the pick waiting.
    private bool PlayCanStartCutscene => PlayCutscenes && !_introPreparing && !_genericActionStarting &&
        (UnansweredManiacCutsceneStartup || _playFolder != _game?.Key || StaticPreviewMayStartScene ||
            _play is not { BootRunning: true } && (_play is not { InCutscene: true } || !UsesManiacCutsceneSetup(_game!)));

    // R-8.5b1 (rule confirmed by the user 20 Oct): a static preview of the other families keeps Play Cutscene; the scene starts from the session the
    // preview was made from (its stated start), whatever that session was doing. Maniac Mansion and Zak keep their own startup rule below.
    private bool StaticPreviewMayStartScene => _catalogPreviewOnly && _game is { } preview && !UsesManiacCutsceneSetup(preview) &&
        !(_playAmbient && _play is { InCutscene: true });

    // R-8.34 already permits this fingerprinted, disclosed native menu preparation
    // for an explicit scene launch. Every other abandoned scope remains blocked.
    private bool UnansweredManiacCutsceneStartup => !PlayActive && _catalogPreviewOnly &&
        _game is { } game && UsesManiacCutsceneSetup(game) && _playFolder == game.Key &&
        _play is { CurrentRoomId: 45, InventoryCharactersAvailable: false, HasPendingPlayInput: false } session &&
        session.DeliveredInput.Count == 0;

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
        // Read now: the flag holds only while the game is being opened, and the derivation below runs later.
        var keptScenes = _roomCardsMayUseCache ? ScenePlanCache.Load(game.FolderPath, game.Language) : null;
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
        UpdateScanGate();
        _ = Task.Run(() =>
        {
            try
            {
                var groups = planner.Value.Groups;
                if (!ReferenceEquals(_playPlanner, planner)) return;
                // A later launch of the same build and data starts from the kept derivation (ScenePlanCache): only what is missing is derived.
                if (keptScenes is not null) planner.Value.ImportScenes(keptScenes);
                _scanTotal = groups.Count;
                Dispatcher.BeginInvoke(() => { if (_playPlanner == planner) UpdatePlaybackControls(); });
                // Each run starts from a copy of a boot (ScummBootSnapshot, 18 October 2026), not from a boot of its own (Monkey Island 2 booted 260 times
                // before, Indy 4 426 times). The entries are handed out one at a time, so a worker that drew long scenes does not hold the others back (a list's
                // default split gives each worker a fixed range: Monkey Island 2, 85 s wall for 189 s of work on eight workers).
                // A disk or archive game runs on at most four workers (executor allocation speed-up, 19 October 2026): its runs got slower per worker as
                // workers were added, so that eight took longer than four (CutsceneList prun, 16-core machine: Monkey Island 2 1/2/4/8 workers 30/20/26/56 s,
                // Indy 4 2/4/8 workers 51/42/75 s; the same lists whatever the count). Record: .verification/executor-alloc-20261019/README.md.
                var parallel = Math.Max(1, ownReader ? Math.Min(4, Environment.ProcessorCount / 2) : Environment.ProcessorCount / 2);
                var options = new ParallelOptions { MaxDegreeOfParallelism = parallel };
                Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(groups, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), options, group =>
                {
                    _scanGate.Wait();
                    planner.Value.Run(group.First);
                    CountScanStep(planner);
                });
                // The room lists are known once every scene has run: the Play Cutscene buttons are ready now, before the setup plans below.
                CdRoomScenesCache.Save(game.FolderPath, planner.Value.ExportRoomLists(), game.Language);
                Dispatcher.BeginInvoke(() => { if (_playPlanner == planner) { _playRoomListsDone = true; UpdatePlaybackControls(); } });
                // §8.6, second phase: the setup of each scene a fresh start does not begin, so a later pick plays it at once (a pick before that works
                // its plan out on the spot); kept for later launches (ScenePlanCache).
                var unplanned = groups.Where(group => !planner.Value.IsDerived(group.First)).ToList();
                Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(unplanned, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), options, group =>
                {
                    _scanGate.Wait();
                    if (ReferenceEquals(_playPlanner, planner)) planner.Value.PlanSetup(group.First);
                });
                if (ReferenceEquals(_playPlanner, planner)) ScenePlanCache.Save(game.FolderPath, game.Language, planner.Value.ExportScenes());
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
        planner.PlaysIn(group, room) || _playSavedRoomLists is { } saved && saved.TryGetValue(PlayCutsceneKey(group.First), out var rooms) && rooms.Contains(room) ||
        new[] { group.First }.Concat(group.Others).Any(t => t.LeadsTo.Contains(room) || (t.LoadTargets?.Contains(room) ?? false)); // R-6.36: Normal lists what Advanced lists

    // Titles stay readable in both modes; raw verb/object provenance belongs in the
    // entry's Advanced note. An unknown native word never becomes a made-up action.
    private static string GenericCutsceneTitle(ScummCutscene trigger) => ScummCutsceneTitles.Plain(trigger);

    /// <summary>The list's entries (all, or those that play out in <paramref name="room"/>). The room's list waits for the scan (no entries until then).</summary>
    private List<(string Id, CutscenePickerItem Item)> PlayCutsceneItems(int? room = null)
    {
        if (_playPlanner is not { } lazy || room is not null && !lazy.IsValueCreated) return [];
        ScummCutscenePlanner planner;
        try { planner = lazy.Value; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException) { return []; }
        // R-6.36 (user, 24 Oct): Normal lists every entry Advanced lists for the room; no fresh-start-run filter, no mode filter.
        return planner.Groups.Where(group => room is null || PlayCutscenePlaysIn(planner, group, room.Value) &&
                !OfferedByInteract($"scumm-{group.First.Room}-{group.First.ObjectId}-{group.First.VerbId}", room.Value,
                    (objectId, verbId) => GenericObjectActions(objectId).Any(action => action.Verb == verbId)))
            .Select(group => ($"scumm-{group.First.Room}-{group.First.ObjectId}-{group.First.VerbId}", new CutscenePickerItem(
                GenericCutsceneTitle(group.First) +
                (room is int shown && group.First.Room != shown && group.Others.All(o => o.Room != shown) ? $" (played from Room {group.First.Room:00})" : ""),
                planner.IsScene(group) ? "Scenes" : "Dialogue and reactions", PlayCutsceneNote(planner, group), () => PlayScummCutscene(group)))).ToList();
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
                   $"then the object's verb 0x{first.VerbId:X2} given to the game's sentence script with the hero put at the object (or run directly where the game refuses it): an exploration action, not the game's click. ") +
               "The frames play in the play screen on the game's clock, with its sounds.";
    }

    /// <summary>
    /// Plays a scene in the play screen: the room is opened from the list when another is shown (its play visit is a state preview), the running
    /// session is kept, and the room's entry plays again or the object's verb runs directly.
    /// </summary>
    private async void PlayScummCutscene(ScummCutsceneGroup group) => await PlayScummCutsceneAsync(group, null);

    /// <param name="interaction">A pick from Interact (R-8.38): an object that is not in the room yet is brought into it first.</param>
    private ScummCutscene? _sceneOpening;

    /// <summary>The game as the boot left it, from which every picked scene begins (R-8.5; null in Maniac Mansion and Zak McKracken, whose party is the player's).</summary>
    private ScummBootSnapshot? _playBoot;

    /// <summary>
    /// R-8.5 (6 November 2026, user report: "strange behavior around cutscene states"): a scene picked from a list begins from the state its plan was measured on,
    /// the boot with the plan's setup, not from wherever the session stands. A scene that already played would otherwise find its flags set and play nothing or
    /// another branch, and the scripts a finished scene left running would go on inside the next. The picture stays until the scene's room replaces it.
    /// </summary>
    private async Task<bool> StartSceneFromBootAsync(GameLibrary game, ScummCutscene trigger, ScummScenePlan? plan, Func<bool> current)
    {
        if (_playBoot is not { } boot) return false;
        var fresh = await Task.Run(() =>
        {
            var session = (ScummGameSession)boot.Start();
            ScummCutscenePlanner.IntoControl(session);
            return session;
        });
        if (!current()) return false;
        _sceneSound.StopAll("a picked scene begins from the boot");
        fresh.DressHeroForScene(trigger);
        if (plan is { HasEdits: true }) fresh.ApplySceneSetup(plan.Edits);
        fresh.ArrivalSetup = plan?.Edits;
        _playAmbient = false;
        _playLiveSource = null;
        _catalogPreviewOnly = false;
        _play = fresh;
        _playScreen = new ScummPlayScreen(game, fresh, _playIndexedStates!);
        return true;
    }

    private async Task PlayScummCutsceneAsync(ScummCutsceneGroup group, string? shownTitle, bool interaction = false)
    {
        var trigger = group.First;
        if (!PlayCanStartCutscene || _game is not { } game || game.Rooms.FirstOrDefault(r => r.ResourceId == trigger.Room) is not { } room) return;
        var launch = ++_cutsceneLaunchGeneration;
        _introPreparing = false;
        // R-8.5b: a static preview's ambient session gives way to the session it was made from; the scene plays from there (stated start).
        LeaveAmbientPreview();
        if (!PreparePlayCutsceneStartup(game)) return;
        // A list's pick begins from the boot, as its plan's runs do; an Interact pick acts on the room as it stands (R-8.5).
        var fromBoot = false;
        ScummScenePlan? earlyPlan = null;
        if (!interaction && _playBoot is not null && _playPlanner is { } earlyPlanner)
        {
            try { earlyPlan = await Task.Run(() => earlyPlanner.Value.PlanSetup(trigger)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException) { earlyPlan = null; }
            if (launch != _cutsceneLaunchGeneration || !ReferenceEquals(_game, game)) return;
            fromBoot = await StartSceneFromBootAsync(game, trigger, earlyPlan, () => launch == _cutsceneLaunchGeneration && ReferenceEquals(_game, game));
            if (launch != _cutsceneLaunchGeneration || !ReferenceEquals(_game, game)) return;
        }
        var visitedForIt = false;
        if (!PlayActive || _currentRoomResource?.ResourceId != trigger.Room || _play is { InCutscene: true } || _play?.CurrentRoomId != trigger.Room)
        {
            // A scene still playing is ended by opening the room again (the manual preparation cancels the departed scene, R-8.5a).
            _sceneSound.StopAll("another scene was picked");
            // Restarting the clock in the same prepared room does not execute entry.
            visitedForIt = _play?.CurrentRoomId != trigger.Room;
            // The visit that opens the room is already the scene's: it does not replay the room's arrival, and brings the hero a scene speaking as
            // him needs (StartPlayCoreAsync applies these to the visit).
            _sceneOpening = trigger;
            try
            {
                SelectRoomWithIntent(room, RoomOpenKind.Cutscene, reloadSelected: true);
                if (_roomOpenTask is { } opening) await opening;
            }
            finally { _sceneOpening = null; }
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
        var title = shownTitle ?? GenericCutsceneTitle(trigger);
        // §8.6 (user, 31 Oct 2026): the state the scene needs is set, then it starts the way the plan found works; nothing is refused by the game's rules.
        ScummScenePlan? plan = earlyPlan;
        if (plan is null && _playPlanner is { } plannerLazy)
        {
            try { plan = await Task.Run(() => plannerLazy.Value.PlanSetup(trigger)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException) { plan = null; }
            if (launch != _cutsceneLaunchGeneration || !ReferenceEquals(_game, game) || !PlayActive || !ReferenceEquals(_play, session)) return;
        }
        IReadOnlyList<string> uncertainties;
        // The scene's own scripts do not wait out their timers until it begins (as the plan's runs: Maniac Mansion's Script 139, seven minutes).
        if (plan?.Result.Way != ScummPickedStart.AtCutscene) session.HurryUntilCutscene(ScummCutsceneRun.HurriedScripts(game, trigger), trigger.ObjectId);
        // A scene that is not the room's arrival does not replay the arrival on the way in (ScummSession.ArrivalsSkippedFor), as the plan's runs.
        session.ArrivalsSkippedFor = trigger.IsRoomEntry ? null : trigger;
        session.ArrivalSetup = plan?.Edits;
        session.WantHeroForScene(trigger);
        try
        {
            // From the boot the setup was set before the visit, as in the plan's runs; again now for what the entry reset (ScummCutsceneRun.Run), the room not re-entered.
            if (fromBoot)
            {
                if (plan is { HasEdits: true })
                {
                    if (session.CurrentRoomId != trigger.Room && ScummSetupEdit.PlayRoom(plan.Edits) is null) session.VisitRoom(room);
                    session.ApplySceneSetup(plan.Edits);
                }
            }
            else if (plan is { HasEdits: true } && trigger.IsRoomEntry)
            {
                // A room entry reads its setup as the room is entered, and the room was opened before the plan was applied: as in the plan's runs,
                // the hero first goes to the room it comes from (a "came from" value) and the setup is set, then the room is entered again.
                if (plan.Edits.FirstOrDefault(edit => edit.Kind == ScummSetupKind.CameFrom) is { } came &&
                    game.Rooms.FirstOrDefault(r => r.ResourceId == came.Value) is { } from) session.VisitRoom(from);
                if (session.ApplySceneSetup(plan.Edits).Count > 0 || session.CurrentRoomId != trigger.Room)
                {
                    _sceneSound.StopAll("a picked scene's setup changed the room's state");
                    if (session.CurrentRoomId == trigger.Room) session.EnterRoomAgain(room);
                    else session.VisitRoom(room);
                    session.ApplySceneSetup(plan.Edits);
                    visitedForIt = true;
                }
            }
            else if (plan is { HasEdits: true } && !trigger.IsRoomEntry &&
                     (session.ApplySceneSetup(plan.Edits).Count > 0 | (session.CurrentRoomId == trigger.Room && session.PrepareEntry(plan.Edits))))
            {
                // What the room's entry derives from the new state (objects, who stands where) follows it, as in the plan's runs; a setup about how
                // the room is entered (the door, a pseudo room number) enters it again when the session is in it already.
                _sceneSound.StopAll("a picked scene's setup changed the room's state");
                if (session.CurrentRoomId == trigger.Room) session.EnterRoomAgain(room);
                else session.VisitRoom(room);
                // As the plan's runs do: again after the visit, for what the room's entry reset.
                session.ApplySceneSetup(plan.Edits);
            }
            if (interaction && !trigger.IsRoomEntry)
            {
                if (session.CurrentRoomId != trigger.Room) session.VisitRoom(room);
                session.BringObjectIntoRoom(trigger.ObjectId);
                session.PutHeroAtObject(trigger.ObjectId);
            }
            if (plan?.Result.Way == ScummPickedStart.AtCutscene && ScummSceneSetupSearch.FirstCutscene(game, trigger) is { } at)
            {
                if (session.CurrentRoomId != trigger.Room) session.VisitRoom(room);
                session.StartSceneAt(at.Label, at.Offset, at.Arguments);
                uncertainties = session.Snapshot.Uncertainties;
            }
            else if (trigger.IsRoomEntry)
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
                // A verb that plays only in another room is used there, the object held (ScummSetupKind.PlayedIn).
                var playRoom = ScummSetupEdit.PlayRoom(plan?.Edits) is int other && game.Rooms.FirstOrDefault(r => r.ResourceId == other) is { } otherRoom ? otherRoom : room;
                if (session.CurrentRoomId != playRoom.ResourceId)
                {
                    _sceneSound.StopAll("a cutscene in another room was started from the list");
                    session.VisitRoom(playRoom);
                }
                var second = plan?.Edits.FirstOrDefault(edit => edit.Kind == ScummSetupKind.SecondObject) is { } with ? int.Parse(with.Target, System.Globalization.CultureInfo.InvariantCulture) : 0;
                if (plan?.Result.Way == ScummPickedStart.Direct) session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId, second);
                else session.PickInteraction(trigger.ObjectId, trigger.VerbId, second, heldFirst: plan?.Result.Way == ScummPickedStart.GameHeldFirst);
                // As the plan's runs: the values that stand actors in the room again, once the pick has put the hero there.
                if (plan is { HasEdits: true }) session.ApplySceneSetup(ScummSetupEdit.InRoomActors(plan.Edits));
                uncertainties = session.Snapshot.Uncertainties;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            StatusText.Text = $"{title} could not start here.";
            StatusText.ToolTip = IsAdvancedMode ? ex.Message : null;
            return;
        }
        finally { session.ArrivalsSkippedFor = null; session.ArrivalSetup = null; }
        ShowPlayFrame();
        PlayScreenImage.Focus();
        StatusText.Text = $"Playing {title} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = IsAdvancedMode && _playPlanner is { IsValueCreated: true } lazy
            ? PlayCutsceneNote(lazy.Value, group) + Environment.NewLine + string.Join(Environment.NewLine, uncertainties)
            : null;
        UpdatePlaybackControls();
    }

    /// <summary>The playback bar during play: Pause, Skip, Skip line and the room's Play Cutscene button, with the scan's note and bar.</summary>
    private void UpdatePlayCutsceneControls() => RefreshRoomControls();

    // Native transport state is cheap and must follow every frame: a cutscene's
    // override can appear or disappear between room-list refreshes.
    private void UpdatePlayTransportControls()
    {
        UpdateSoundDeviceControls();
        RefreshRoomTransport();
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
        // Play Game Intro waits for the game's cutscene to end (2 November 2026, user report: it stayed disabled after the intro).
        if (changed) UpdateGameIntroButton();
    }
}

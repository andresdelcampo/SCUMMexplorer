using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

// The cutscene part of the main window: the known-cutscene catalog, the pickers (global and per room), the CD scan lists and the
// stated starts that play them. Moved out of MainWindow.xaml.cs unchanged (K-04).
public partial class MainWindow
{
    /// <summary>
    /// A known cutscene and the object verb that starts it (decoded; see the script map). With <see cref="Click"/>,
    /// the verb only leads into the room, and the game's click at that point starts the cutscene.
    /// </summary>
    private sealed record KnownCutscene(string Description, int RoomId, int ObjectId, int VerbId, int Script, string Source,
        (int X, int Y)? Click = null, string Category = "Scenes", int[]? TextScripts = null,
        LoomReplayStep[]? Steps = null, string? SequenceId = null, int SilentSteps = 0)
    {
        public string Title => LoomCutsceneTitles.Format(RoomId, Description);
        public string Id => SequenceId ?? (ObjectId == 0 ? $"Room {RoomId:00} entry" : $"Room {RoomId:00} object {ObjectId} Verb 0x{VerbId:X2}");
    }

    // Established start actions. Room linking uses only generated validation metadata.
    private static readonly KnownCutscene[] KnownCutscenes =
    [
        new("Walk to Room 13", 10, 295, 0x38, 52,
            "Room 10 object 295 verb 0x38 starts script 052 (+0x0003): a cutscene that waits 60 jiffies (+0x0041), loads Room 11 " +
            "(+0x0045), sets its skip point (+0x00C1) and ends with loadRoomWithEgo into Room 13 (+0x0168)."),
        new("The Elders' trial", 8, 255, 0x38, 46,
            "Room 08 object 255 verb 0x38 (the sanctuary door) starts script 046 (+0x0000): a cutscene with its skip point at " +
            "+0x0020, through Rooms 09, 67, 09, 54, 09, 03, 08, 09, 11, 08 and 09, ending at +0x13AA in Room 09."),
        new("The firewood scene", 36, 600, 0x38, 86,
            "Room 36 object 600 verb 0x38 leads into Room 37 (loadRoomWithEgo, walk to (24, 101)). On a first visit " +
            "(bit[0xD99] is 0) Room 37's entry starts local 200 (+0x008C), which chains to script 086 once Bobbin's walk box " +
            "becomes 8 (+0x0016–0x001D). Script 086: Stoke mistakes Bobbin for Rusty; Rooms 37, 58, 37 and 38; skip point " +
            "+0x0023; endCutscene +0x0536 in Room 38. Bobbin wears Rusty's look here: the unseen first steps of this " +
            "sequence cast the Awakening and the costume change on Rusty in Room 34 (the game's own scenes, run without showing them) and apply " +
            "the supported Game Status edit \"staff held\", since Stoke takes the distaff in this scene.",
            Steps: FirewoodSteps, SilentSteps: FirewoodSilentSteps)
    ];

    // Room 34's Awakening (script 085) and costume change (local 202) give Bobbin Rusty's look (costume 0x67), which
    // scenes 086 and 087 show. These are direct verbs from an assumed catalog arrival; the story is not reconstructed.
    internal static LoomReplayStep[] FirewoodSteps =>
    [
        new("visit", 34),
        new("verb", 562, 0x44), new("scene", EndBoundary: "endCutscene at Script 85 +0x05A8", ExpectedRoom: 34),
        new("verb", 562, 0x4C), new("scene", EndBoundary: "endCutscene at Room 34 local script 202 +0x015F", ExpectedRoom: 34),
        new("visit", 36), new("staff", 1), new("frames", Frames: 8),
        new("verb", 600, 0x38), new("frames", Frames: 32), new("click", X: 109, Y: 102),
        new("scene", EndBoundary: "endCutscene at Script 86 +0x0536", ExpectedRoom: 38)
    ];
    internal const int FirewoodSilentSteps = 8;

    private IEnumerable<KnownCutscene> AllCutscenes()
    {
        foreach (var scene in KnownCutscenes) yield return scene;
        foreach (var recipe in _replayCatalog?.Replays ?? [])
        {
            if (recipe.Steps is null && KnownCutscenes.Any(k => k.RoomId == recipe.Room && k.ObjectId == recipe.ObjectId && k.VerbId == recipe.Verb)) continue;
            yield return new(recipe.Title, recipe.Room, recipe.ObjectId, recipe.Verb, recipe.ScriptIds.FirstOrDefault(),
                recipe.Source, Category: recipe.Category, TextScripts: recipe.ScriptIds, Steps: recipe.Steps, SequenceId: recipe.Id,
                SilentSteps: recipe.SilentSteps);
        }
    }

    /// <summary>
    /// The known cutscenes by the indexed script that prints their texts, for Game Texts' "Play this cutscene".
    /// Script 052 prints no text (decoded), so none of its texts can offer it. Empty outside a LOOM session.
    /// </summary>
    private Dictionary<int, GalleryCutscene> GalleryCutscenes()
    {
        var cutscenes = new Dictionary<int, GalleryCutscene>();
        if (_game?.Id != GameId.Loom || _game.IsDemo || _session is null || _indexedObjectSnapshot is null) return cutscenes;
        cutscenes[LoomExplorationSession.OpeningScript] = new(OpeningTitle, OpeningStartNote, () => PlayFromGameTexts(PlayOpening));
        foreach (var cutscene in KnownCutscenes)
            cutscenes[cutscene.Script] = new(cutscene.Title, CutsceneStartNote(cutscene),
                () => PlayFromGameTexts(() => PlayKnownCutscene(cutscene)));
        // A unique reached story script has an unambiguous replay descriptor. Shared
        // helpers and scripts with multiple starts do not inherit an arbitrary replay.
        foreach (var group in AllCutscenes().Where(c => c.TextScripts is not null)
                     .SelectMany(c => c.TextScripts!.Where(id => id >= 44).Select(id => (id, scene: c))).GroupBy(x => x.id))
            if (group.Count() == 1 && !cutscenes.ContainsKey(group.Key))
            {
                var cutscene = group.Single().scene;
                cutscenes[group.Key] = new(cutscene.Title, CutsceneStartNote(cutscene),
                    () => PlayFromGameTexts(() => PlayKnownCutscene(cutscene)));
            }
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
        if (!CanStartCutscene) return;
        var picker = BuildCutscenePicker();
        if (picker.ShowDialog() == true) picker.SelectedPlay?.Invoke();
    }

    private CutscenePickerWindow BuildCutscenePicker()
    {
        var items = PlayableCutsceneItems().Select(choice => choice.Item).ToList();
        foreach (var gap in _replayCatalog?.Coverage ?? [])
            items.Add(new(gap.Title, "Needs validation", gap.Source, null, gap.Status));
        return new(items, _replayCatalog?.Summary ?? "", IsAdvancedMode, roomScope: false) { Owner = this };
    }

    private const string DemoPreviewTitle = "The preview";

    private const string DemoPreviewNote =
        "LOOM's self-running preview (LOOMDEMO.EXE): script 001 loads Room 01, whose entry starts script 045, a director that " +
        "visits the demo's rooms in turn and starts over. It is a different build from the full game (its own scripts and rooms; " +
        "script and story-bit names of the full game are not applied). It plays on the clock at the game's frame rate until you open another room.";

    private CutscenePickerItem DemoPreviewItem() => new(DemoPreviewTitle, "Scenes", DemoPreviewNote, PlayDemoPreview);

    /// <summary>Plays a LOOM demo's preview: a fresh session whose director script runs, on the playback clock, until another room is opened.</summary>
    private void PlayDemoPreview()
    {
        if (_game is not { IsDemo: true } || _indexedObjectSnapshot is null || !CanStartCutscene) return;
        StopScenePlayback();
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, demoPreview: true);
        _sceneSound.Attach(_game, _session);
        if (_session.Snapshot.RoomId is not int roomId || _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is not { } room)
            return;
        StartPlayback(DemoPreviewTitle, null, room, doorways: null, leftRoom: false, preview: true);
        StatusText.Text = "Playing the demo's preview. Open any room to stop it.";
        StatusText.ToolTip = DemoPreviewNote + Environment.NewLine + string.Join(Environment.NewLine, _session.Snapshot.Uncertainties);
    }

    private const string IntroTitle = "The Intro";
    private const string CdIntroTitle = IntroTitle;

    private const string CdIntroNote =
        "LOOM CD's start (script 001 from the beginning): Room 69's skill screen with the spoken line, then script 044 over the 84-second " +
        "overture cue on the disc's audio track (the logo, \"LucasArts Entertainment Company\", Room 53's fairy and Bobbin on the cliff), ending in Room 02. " +
        "Stated start: a fresh session (seed 0). The skill screen waits for the player in the game; here the answer is assumed (an estimate) " +
        "3 seconds in, and the draft question is not simulated. The overture's timer (var[0x0E]) counts one a jiffy from the cue's start, as ScummVM's CD timer does, " +
        "so the cutscene's fades follow it on the playback clock; whether they match the original's moment by moment has not been compared. The current exploration state is replaced.";

    private CutscenePickerItem CdIntroItem() => new(CdIntroTitle, "Scenes", CdIntroNote, PlayCdIntro);

    /// <summary>Plays the CD game's intro: a fresh session whose script 001 starts from the beginning, the frames on the playback clock, the voice and overture from the disc.</summary>
    private void PlayCdIntro()
    {
        if (_game is not { Id: GameId.LoomVga, V4Archive: { } vga } || !CanStartCutscene) return;
        StopScenePlayback();
        _cdVoice.Stop();
        _session = new LoomExplorationSession(_game, vga.ReadIndexedObjects(), completeBoot: false, playCdIntro: true);
        // The skill screen waits for a press on one of its buttons (SCUMM_CD_INTRO_AUTOANSWER=1 lets the harnesses run it unattended).
        _session.WaitForSkillClick = Environment.GetEnvironmentVariable("SCUMM_CD_INTRO_AUTOANSWER") != "1";
        _cdVoice.Attach(vga.Disc, _session);
        if (_session.Snapshot.RoomId is not int roomId || _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is not { } room)
            return;
        StartPlayback(CdIntroTitle, null, room, doorways: null, leftRoom: false);
        StatusText.Text = $"Playing {CdIntroTitle} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = CdIntroNote + Environment.NewLine + string.Join(Environment.NewLine, _session.Snapshot.Uncertainties);
    }

    private IReadOnlyList<LoomCutsceneGroup>? _cdCutsceneGroups;
    private Lazy<LoomCdSceneSetupPlanner>? _cdSetupPlanner;
    /// <summary>Room lists saved by an earlier run (same build and game data), used until the background runs finish.</summary>
    private Dictionary<string, int[]>? _cdSavedRoomLists;
    private bool _cdRoomListsDone;

    /// <summary>
    /// The CD scenes that a fresh start does not begin get a start setup derived from the scripts (<see cref="LoomCdSceneSetupPlanner"/>). It works on its own data
    /// source and sessions, off the UI thread, from the moment the game opens, so the picker's notes have it by the time the list is opened.
    /// </summary>
    private void StartCdSetupDerivation(GameLibrary game)
    {
        if (game is not { Id: GameId.LoomVga }) { _cdSetupPlanner = null; _cdSavedRoomLists = null; return; }
        // As with the thumbnails, only reopening the last game reuses the saved lists; the Open button derives them again.
        _cdSavedRoomLists = _roomCardsMayUseCache ? CdRoomScenesCache.Load(game.FolderPath) : null;
        var planner = new Lazy<LoomCdSceneSetupPlanner>(() =>
        {
            var data = ScummDataSources.For(game);
            return new LoomCdSceneSetupPlanner(game, LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value).ToList(), LoomCutsceneTitles.Doorways(game), game), data);
        });
        _cdSetupPlanner = planner;
        _cdRoomListsDone = false;
        _ = Task.Run(() =>
        {
            try
            {
                var groups = LoomCutsceneIndex.Group(planner.Value.Triggers);
                foreach (var group in groups) planner.Value.Derive(group.First);
                // Then the rooms each scene plays out in, so the room's own "Play Cutscene" list has them (the EGA list does, through the catalog's membership).
                planner.Value.IntroRooms();
                foreach (var group in groups) planner.Value.VisitedRooms(group.First);
                CdRoomScenesCache.Save(game.FolderPath, planner.Value.ExportRoomLists());
                Dispatcher.BeginInvoke(() => { if (_cdSetupPlanner == planner) { _cdRoomListsDone = true; UpdatePlaybackControls(); } });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException) { }
        });
    }

    private string CdSetupNote(LoomCutscene trigger) =>
        _cdSetupPlanner is not { IsValueCreated: true } lazy || !lazy.Value.TryGet(trigger, out var setup)
            ? "Whether a fresh start begins it is still being checked (open the list again in a few seconds). "
            : setup == LoomCdStartSetup.None ? "A fresh start begins its cutscene (checked by a headless run). " : setup.Describe() + " ";

    /// <summary>The CD game's cutscene triggers, derived from its scripts once per opened game (<see cref="LoomCutsceneIndex"/>).</summary>
    private IReadOnlyList<LoomCutsceneGroup> CdCutsceneGroups() =>
        _cdCutsceneGroups ??= _game is { Id: GameId.LoomVga } && _cdData is not null
            ? LoomCutsceneIndex.Group(LoomCutsceneIndex.Scan(_cdData, _game.Rooms.Select(r => r.ResourceId!.Value), LoomCutsceneTitles.Doorways(_game), _game))
            : [];

    private string CdCutsceneTitle(LoomCutscene trigger)
    {
        return PickerVerbTitle(LoomCutsceneTitles.Title(trigger), trigger.IsRoomEntry ? 0 : trigger.VerbId);
    }

    private string CdCutsceneNote(LoomCutsceneGroup group)
    {
        var first = group.First;
        var others = group.Others.Count == 0 ? "" : " The same scripts are reached from " + string.Join(", ",
            group.Others.Select(o => $"object {o.ObjectId}{(o.ObjectName.Length > 0 ? $" ({o.ObjectName})" : "")} {o.VerbLabel}")) + ".";
        return $"Found in the CD scripts: {first.Room.ToString("00")} · {(first.IsRoomEntry ? "the room's entry script" : $"object {first.ObjectId}'s {first.VerbLabel}")} " +
               $"reaches {string.Join(", ", first.Scripts)} (a cutscene ... endCutscene span of {first.SpanInstructions} instructions, {first.Texts} printed line(s), {first.RoomLoads} room load(s)). " +
               "A branch can keep part of it from running, so the lines and loads are possible, not certain." + others + " " +
               CdSetupNote(first) +
               "Stated start: the running exploration session (state kept, a fresh post-boot session if none), the room opened from the catalog (Bobbin's arrival is assumed), " +
               (first.IsRoomEntry ? "then the room's entry script plays on the clock. " :
                $"then the object's verb 0x{first.VerbId:X2} run directly: an exploration action, not the game's click (Bobbin does not walk there, the object may be untouchable in this state, and a draft response does not simulate casting the draft). ") +
               "The frames play on the playback clock at the game's frame rate; voices play from the disc as a click's would.";
    }

    private List<(string Id, CutscenePickerItem Item)> CdCutsceneItems(int? room = null) =>
        CdCutsceneGroups().Where(group => room is null || group.First.Room == room || group.Others.Any(o => o.Room == room) || CdPlaysIn(group, room.Value))
            .Select(group => ($"cd-{group.First.Room}-{group.First.ObjectId}-{group.First.VerbId}",
                new CutscenePickerItem(CdCutsceneTitle(group.First) + (room is int shown && group.First.Room != shown && group.Others.All(o => o.Room != shown) ? $" (played from Room {group.First.Room:00})" : ""),
                    group.IsScene ? "Scenes" : "Dialogue and reactions", CdCutsceneNote(group), () => PlayCdCutscene(group)))).ToList();

    /// <summary>Whether a headless run of the scene from its start (already derived in the background) was in this room at some point.</summary>
    private bool CdPlaysIn(LoomCutsceneGroup group, int room) =>
        _cdSavedRoomLists is { } saved && saved.TryGetValue($"{group.First.Room},{group.First.ObjectId},{group.First.VerbId}", out var savedRooms) && savedRooms.Contains(room) ||
        _cdSetupPlanner is { IsValueCreated: true } lazy && lazy.Value.TryGetVisitedRooms(group.First, out var rooms) && rooms.Contains(room);

    /// <summary>The intro's own list entry for a room it is in while it plays (the skill screen, Rooms 01 and 02, the fairy, the cliff); false until the background run is done.</summary>
    private bool CdIntroPlaysIn(int room) =>
        _cdSavedRoomLists is { } saved && saved.TryGetValue("intro", out var savedRooms) && savedRooms.Contains(room) ||
        _cdSetupPlanner is { IsValueCreated: true } lazy && lazy.Value.IntroRoomsIfKnown?.Contains(room) == true;

    /// <summary>Plays a CD cutscene from its stated start: the session's room visit, then the room's entry script or the object's verb run directly, on the playback clock.</summary>
    private void PlayCdCutscene(LoomCutsceneGroup group)
    {
        var trigger = group.First;
        if (_game is not { Id: GameId.LoomVga, V4Archive: { } vga } || !CanStartCutscene ||
            _game.Rooms.FirstOrDefault(item => item.ResourceId == trigger.Room) is not { } room) return;
        StopScenePlayback();
        _cdVoice.Stop();
        // Single scenes keep the running story/status, including later actor and staff state.
        // Only missing source prerequisites run unseen; explicit sequences/restart own their fresh starts.
        var setup = _cdSetupPlanner?.Value.Derive(trigger) ?? LoomCdStartSetup.None;
        var session = _session is { BootRunning: false } ? _session : _session = new LoomExplorationSession(_game, vga.ReadIndexedObjects(), completeBoot: false);
        setup = LoomCdSceneSetupPlanner.Adapt(setup, session);
        var useSetup = setup is { Plays: true, HasSteps: true };
        if (useSetup) LoomCdSceneSetupPlanner.ApplyBefore(session, _game, setup.Before);
        _cdVoice.Attach(vga.Disc, session);
        var before = (session.CutscenesBegun, session.EgoPuts);
        session.VisitRoom(room);
        if (useSetup) LoomCdSceneSetupPlanner.ApplyAfter(session, setup.After);
        var speakerNotes = trigger.IsRoomEntry ? [] : PlaceSpeakersForDirectVerb(room, trigger.ObjectId, trigger.VerbId);
        var action = trigger.IsRoomEntry ? null : session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
        var uncertainties = (action?.Uncertainties ?? session.Snapshot.Uncertainties).ToList();
        if (_currentRoomResource?.ResourceId == room.ResourceId && _currentRoom is not null)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview();
        }
        var title = CdCutsceneTitle(trigger);
        StartPlayback(trigger.IsRoomEntry ? $"Room {trigger.Room:00}'s entry scripts" : $"Object {trigger.ObjectId}'s verb 0x{trigger.VerbId:X2} (run directly)", action, room, doorways: null, leftRoom: false,
            sceneBefore: before, expectedCutscene: title);
        StatusText.Text = $"Playing {title} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = CdCutsceneNote(group) + Environment.NewLine + string.Join(Environment.NewLine, speakerNotes.Concat(uncertainties));
    }

    private string PickerVerbTitle(string title, int verb) =>
        IsAdvancedMode && verb != 0 ? $"{title} (verb 0x{verb:X2})" : title;

    /// <summary>
    /// The picker entries. <paramref name="onlyIds"/> limits the work to those entries: every note derives a start setup
    /// (real scenes run unseen), so building all of them just to show one room's buttons took seconds on the first room.
    /// </summary>
    private List<(string Id, CutscenePickerItem Item)> PlayableCutsceneItems(ISet<string>? onlyIds = null)
    {
        if (_game is { Id: GameId.LoomVga }) return [("cd-intro", CdIntroItem()), .. CdCutsceneItems()];
        // A demo has no catalog of scenes: the full game's replays name its rooms, objects and scripts.
        if (_game is { IsDemo: true }) return [("demo-preview", DemoPreviewItem())];
        var items = new List<(string, CutscenePickerItem)>();
        if (onlyIds is null || onlyIds.Contains("opening"))
            items.Add(("opening", new(OpeningTitle, "Scenes", OpeningSource + " " + OpeningStartNote, PlayOpening)));
        foreach (var scene in AllCutscenes().Where(scene => onlyIds is null || onlyIds.Contains(scene.Id)))
            items.Add((scene.Id, new(PickerVerbTitle(scene.Title, scene.ObjectId == 0 ? 0 : scene.VerbId), scene.Category, scene.Source + " " + CutsceneStartNote(scene), () => PlayKnownCutscene(scene))));
        return items;
    }

    private List<CutscenePickerItem> RoomCutsceneItems(int roomId)
    {
        if (_game is { IsDemo: true }) return roomId == 1 ? [DemoPreviewItem()] : [];
        if (_game is { Id: GameId.LoomVga })
        {
            var items = CdCutsceneItems(roomId).Select(choice => choice.Item).ToList();
            if (CdIntroPlaysIn(roomId)) items.Insert(0, CdIntroItem());
            return items;
        }
        var ids = (_replayCatalog?.Membership ?? []).Where(m => m.StartRoom == roomId || m.Rooms.Contains(roomId)).Select(m => m.Id).ToHashSet();
        var starts = (_replayCatalog?.Membership ?? []).ToDictionary(m => m.Id, m => m.StartRoom);
        return PlayableCutsceneItems(ids).Select(choice =>
            starts.TryGetValue(choice.Id, out var start) && start != roomId
                ? choice.Item with { Title = choice.Item.Title + $" (played from Room {start:00})" }
                : choice.Item).ToList();
    }

    private void RoomCutscene_Click(object sender, RoutedEventArgs e)
    {
        if (!CanStartCutscene || _currentRoomResource?.ResourceId is not int roomId) return;
        var items = RoomCutsceneItems(roomId);
        if (items.Count == 1) items[0].Play?.Invoke();
        else if (items.Count > 1)
            OpenPopupMenu(RoomCutsceneButton, System.Windows.Controls.Primitives.PlacementMode.Top, "CUTSCENES IN THIS ROOM",
                items.Where(item => item.Play is not null)
                    .Select(item => (item.Title, IsAdvancedMode ? item.Note : null, item.Play!)));
    }

    private List<(string Title, string Source, Action Play)> CutsceneChoices()
    {
        var choices = new List<(string Title, string Source, Action Play)>();
        if (_game?.Id != GameId.Loom || _session is null) return choices;
        if (_game.IsDemo) return [(DemoPreviewTitle, DemoPreviewNote, PlayDemoPreview)];
        choices.Add((OpeningTitle, OpeningSource + " " + OpeningStartNote, PlayOpening));
        foreach (var cutscene in AllCutscenes())
            choices.Add((cutscene.Title, cutscene.Source + " " + CutsceneStartNote(cutscene), () => PlayKnownCutscene(cutscene)));
        return choices;
    }

    private ContextMenu BuildCutsceneMenu()
    {
        var menu = new ContextMenu { PlacementTarget = CutsceneMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var choice in CutsceneChoices())
        {
            var item = new MenuItem { Header = choice.Title, ToolTip = choice.Source, IsEnabled = CanStartCutscene };
            item.Click += (_, _) => choice.Play();
            menu.Items.Add(item);
        }
        return menu;
    }

    private static string RoomList(IEnumerable<int> rooms)
    {
        var list = rooms.Distinct().Order().Select(room => room.ToString("00")).ToList();
        return (list.Count == 1 ? "Room " : "Rooms ") + string.Join(", ", list);
    }

    private const string OpeningTitle = IntroTitle;

    private const string OpeningSource =
        "Script 001 +0x021A starts script 044 and waits for it (+0x021D): Rooms 01, 02, 01, 02, 53 and 02, with the copyright " +
        "line, Room 01's points of light (locals 200–208), the fairy's message in Room 53 and Bobbin's lines on the cliff. " +
        "Two skip points (beginOverride +0x005A, +0x0124); after a skip Bobbin faces the screen (+0x0622).";

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
        if (_game is null || _indexedObjectSnapshot is null || !CanStartCutscene) return;
        StopScenePlayback();
        _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, stopAtOpening: true, soundCard: SoundCard);
        _sceneSound.Attach(_game, _session);
        if (_session.Snapshot.RoomId is not int roomId || _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) is not { } room)
            return;
        StartPlayback("The opening (script 044)", null, room, doorways: null, leftRoom: false);
        StatusText.Text = $"Playing {OpeningTitle} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = OpeningStartNote + Environment.NewLine + OpeningSource + Environment.NewLine +
                             string.Join(Environment.NewLine, _session.Snapshot.Uncertainties);
    }

    /// <summary>
    /// The start setup derived from the scene's own code (<see cref="LoomSceneSetupPlanner"/>): real earlier scenes run
    /// unseen before the scene's start. Scenes with hand-written sequences keep their explicit steps.
    /// </summary>
    private LoomStartSetup StartSetup(KnownCutscene cutscene)
    {
        if (_game is null || _game.Id != GameId.Loom || cutscene.Steps is not null || cutscene.ObjectId == 0) return LoomStartSetup.None;
        if (_startSetups.TryGetValue(cutscene.Id, out var cached)) return cached;
        _setupPlanner ??= new LoomSceneSetupPlanner(_game);
        return _startSetups[cutscene.Id] = _setupPlanner.Derive(cutscene.RoomId, cutscene.TextScripts ?? [cutscene.Script], cutscene.ObjectId, cutscene.VerbId);
    }

    private string CutsceneStartNote(KnownCutscene cutscene) => CutsceneBaseNote(cutscene) + (StartSetup(cutscene).Describe() is { Length: > 0 } setup ? " " + setup : "");

    private static string CutsceneBaseNote(KnownCutscene cutscene) =>
        cutscene.Steps is not null ? "Stated sequence: a fresh post-boot seed-0 session and assumed catalog arrival. " +
        "Explicit direct verbs, clicks, catalog hops (a visit step keeps the story state so far), supported Game Status edits (staff held) and, where the explorer cannot reach a scene by the game's own trigger, direct script starts run in the same session, waiting for each audited scene boundary, balanced scopes and settled state before the next input. " +
        "These exploration choices do not establish original-game prerequisites. Failed or skipped stages cancel later inputs. " + cutscene.Source :
        $"Stated start: the running session, or a fresh one after the boot (seed 0), with Room {cutscene.RoomId:00} opened from the catalog (the ego's arrival " +
        "there is assumed), " + (cutscene.ObjectId == 0 ? "then the room's entry scripts play on the clock. " :
        $"then object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} run directly: an exploration action, not " +
        "the game's click (Bobbin does not walk there, and the object may be untouchable in this state). Draft response verbs do not simulate casting a draft. ") +
        (cutscene.Click is { } click
            ? $"The verb leads into the next room; once Bobbin's arrival walk ends (run before the view shows it), the game's click at " +
              $"({click.X}, {click.Y}) (assumed input) walks him on, and the room's own code starts the cutscene. "
            : "") +
        "The frames after it play on the playback clock at the game's frame rate. The running exploration session is kept (Restart gives the fresh state); only the minimum the scene needs and the session lacks is added.";

    /// <summary>
    /// Plays a known cutscene from its stated start (<see cref="CutsceneStartNote"/>): a fresh session after the boot,
    /// a catalog visit to the room and the object's verb run directly, then the frames on the playback clock.
    /// </summary>
    private void PlayKnownCutscene(KnownCutscene cutscene)
    {
        if (_game is null || _indexedObjectSnapshot is null || !CanStartCutscene ||
            _game.Rooms.FirstOrDefault(item => item.ResourceId == cutscene.RoomId) is not { } room) return;
        StopScenePlayback();
        // A single scene keeps the running session (the user's state stays, a later one too; "Restart" goes back to the boot) and gets only
        // the minimum it needs. A stated sequence carries its own steps from the post-boot state, so it starts a fresh session.
        var keepSession = cutscene.Steps is null && _session is { BootRunning: false };
        var session = keepSession ? _session! : _session = new LoomExplorationSession(_game, _indexedObjectSnapshot, soundCard: SoundCard);
        // Sounds from here on: the boot's are over; the visit's, the verb's and the click's play with the first frame.
        // A sequence with unseen leading steps runs them first (the story state they reach is the scene's start), then attaches.
        var setup = StartSetup(cutscene);
        if (keepSession && _setupPlanner is not null) setup = _setupPlanner.Adapt(setup, _session!);
        var unseenSetup = cutscene.Steps is null && setup.HasSteps;
        if (!unseenSetup && (cutscene.Steps is null || cutscene.SilentSteps == 0)) _sceneSound.Attach(_game, session);
        session.VisitRoom(room);
        string? setupFailure = null;
        if (unseenSetup)
        {
            // The derived setup (real earlier scenes, then a visit of the scene's room) runs unseen; the sounds start with the scene.
            var prelude = new LoomReplaySequence(session, setup.Steps);
            if (!prelude.RunSilently(setup.Steps.Count)) setupFailure = $"The derived start setup stopped ({prelude.Failure ?? "no result"}); the scene starts from the catalog visit.";
            _sceneSound.Attach(_game, session);
        }
        LoomObjectActionResult? action;
        if (cutscene.Steps is { } steps)
        {
            _replaySequence = new LoomReplaySequence(session, steps);
            if (cutscene.SilentSteps > 0)
            {
                _replaySequence.RunSilently(cutscene.SilentSteps);
                _sceneSound.Attach(_game, session);
                // The unseen steps may have moved on to another room: the scene plays where the session now is.
                if (session.Snapshot.RoomId is int nowRoom && _game.Rooms.FirstOrDefault(item => item.ResourceId == nowRoom) is { } nowResource)
                    room = nowResource;
            }
            if (_replaySequence.Failure is null) _replaySequence.Continue();
            action = _replaySequence.LastAction;
        }
        else action = cutscene.ObjectId == 0 ? null : session.ActivateObjectVerb(cutscene.ObjectId, cutscene.VerbId);
        var uncertainties = (action?.Uncertainties ?? session.Snapshot.Uncertainties).ToList();
        if (setupFailure is not null) uncertainties.Insert(0, setupFailure);
        if (cutscene.Click is { } click)
        {
            // The verb's loadRoomWithEgo walks Bobbin in; the click is sent once he stands, as a player's would be.
            for (var frame = 0; frame < LoomExplorationSession.CutsceneFrameLimit && session.EgoMoving; frame++) session.AdvanceFrame();
            action = session.Click(click.X, click.Y);
            uncertainties.AddRange(action.Uncertainties);
        }
        if (_currentRoomResource?.ResourceId == room.ResourceId && _currentRoom is not null)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview();
        }
        StartPlayback(cutscene.Click is { } sent
                ? $"Object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} (run directly), then a click at ({sent.X}, {sent.Y})"
                : cutscene.ObjectId == 0 ? $"Room {cutscene.RoomId:00}'s entry scripts" : $"Object {cutscene.ObjectId}'s verb 0x{cutscene.VerbId:X2} (run directly)",
            action, room, doorways: null, leftRoom: false);
        StatusText.Text = $"Playing {cutscene.Title} from a stated start (see the tooltip). Escape skips once the cutscene allows it.";
        StatusText.ToolTip = CutsceneStartNote(cutscene) + Environment.NewLine + cutscene.Source + Environment.NewLine +
                             string.Join(Environment.NewLine, uncertainties);
    }
}

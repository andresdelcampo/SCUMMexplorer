using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>Own-source room actions and inspection for games with the native sentence interface.</summary>
public partial class MainWindow
{
    private Panel? _genericActionsHost, _genericSourceHost;
    private GameLibrary? _genericActionsGame;
    private int? _genericActionsRoom;
    private bool _genericActionsAdvanced;
    private bool _genericActionStarting;
    private int _genericNamesFrame;
    private Func<string, string>? _genericReadable;
    private readonly List<Button> _genericActionButtons = [];
    private readonly List<(string Label, ScummScriptDisassembly Code)> _genericRoomScripts = [];
    private readonly List<GenericSourceAction> _genericSourceActions = [];
    private RoomPanelAction? _genericLastAction;
    private IReadOnlyList<string> _genericAssumptions = [];
    private string? _genericSourceFailure;
    private int? _genericActionsShownFor;
    private sealed record GenericSourceAction(int Object, int Verb, string Title, string Target, ScummScriptDisassembly? Code, string Source, bool Executable);

    private bool GenericRoomActions => _game is { } game && !HasDedicatedSession && ScummProfile.For(game).PlaysThroughVerbs;

    private void InitializeGenericRoomActions(Panel actionsHost, Panel sourceHost)
    {
        _genericActionsHost = actionsHost;
        _genericSourceHost = sourceHost;
        RefreshPlayRoomActions();
    }

    // Source data is read only when the game or shown room changes. Frame refreshes only
    // update availability; the room's current native identity is followed by ShowPlayFrame.
    private void RefreshPlayRoomActions()
    {
        if (_genericActionsHost is null || _genericSourceHost is null) return;
        var room = _currentRoomResource?.ResourceId;
        var changed = !ReferenceEquals(_genericActionsGame, _game) || _genericActionsRoom != room;
        _genericActionsHost.Visibility = GenericRoomActions && room is not null ? Visibility.Visible : Visibility.Collapsed;
        _genericSourceHost.Visibility = GenericRoomActions && room is not null && IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        if (!GenericRoomActions || _game is not { } game || room is not int roomId)
        {
            _genericActionsGame = null;
            _genericActionsRoom = null;
            _genericActionsHost.Children.Clear();
            _genericSourceHost.Children.Clear();
            _genericActionButtons.Clear();
            return;
        }
        // The old browse-first object/navigation lists were built for the original
        // selected room. The native session can now have loaded another room.
        // Only while the native screen is live: RefreshPlayOverlayLists then fills its own rows. A static preview (R-6.23, 24 Oct 2026) keeps the
        // browsed hotspot and doorway rows, which are the only ones it has.
        if (PlayActive)
        {
            ObjectListSection.Visibility = Visibility.Collapsed;
            RoomNavigationPanel.Visibility = Visibility.Collapsed;
        }
        if (changed)
        {
            _genericLastAction = null;
            _genericSourceFailure = null;
            _genericActionsShownFor = null;
            _genericActionsGame = game;
            _genericActionsRoom = roomId;
            _genericRoomScripts.Clear();
            _genericSourceActions.Clear();
            _ = LoadGenericRoomActionsAsync(game, roomId);
        }
        var namesChanged = RefreshGenericRoomActionWords();
        if (changed || namesChanged || _genericActionsAdvanced != IsAdvancedMode || _genericActionsHost.Children.Count == 0)
        {
            _genericActionsAdvanced = IsAdvancedMode;
            BuildGenericRoomActions();
        }
        UpdateGenericRoomActionAvailability();
    }

    private sealed record GenericRoomSource(Func<string, string>? Readable, List<(string Label, ScummScriptDisassembly Code)> Scripts,
        List<GenericSourceAction> Actions, string? Failure);

    // The room's source is read on a worker and published when it is done (30 October 2026: read on the UI thread, Indy 4's first room change held
    // the window while the inspection copy was made; .verification/indy4-rooms-20261030). The lists stay empty until then.
    private async Task LoadGenericRoomActionsAsync(GameLibrary game, int room)
    {
        var source = await Task.Run(() => ReadGenericRoomActions(game, room));
        if (!ReferenceEquals(_genericActionsGame, game) || _genericActionsRoom != room || !ReferenceEquals(_game, game)) return;
        _genericReadable = source.Readable;
        _genericRoomScripts.Clear();
        _genericRoomScripts.AddRange(source.Scripts);
        _genericSourceActions.Clear();
        _genericSourceActions.AddRange(source.Actions);
        _genericSourceFailure = source.Failure;
        RefreshGenericRoomActionWords();
        _genericActionsAdvanced = IsAdvancedMode;
        BuildGenericRoomActions();
        UpdateGenericRoomActionAvailability();
    }

    private GenericRoomSource ReadGenericRoomActions(GameLibrary game, int room)
    {
        var scripts = new List<(string Label, ScummScriptDisassembly Code)>();
        var actions = new List<GenericSourceAction>();
        var readable = ManiacPreviewText.For(game);
        try
        {
            // Archive/disk readers own seek positions; background scene derivation and
            // live playback must not share this inspection reader (MainWindow.Inspection.cs).
            using var turn = UseInspectionCopy(game, out var sourceGame);
            var data = ScummDataSources.For(sourceGame);
            var stored = data.ReadRoom(room);
            var verbs = data.ReadObjectVerbs(room, stored.Objects);
            string Text(string text) => (readable?.Invoke(text) ?? text).Replace("@", "", StringComparison.Ordinal).Trim();
            void Add(string label, Func<ScummScriptDisassembly?> read)
            {
                try { if (read() is { } code) scripts.Add((label, code)); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
            }
            Add("Room entry", () => data.RoomEntryScript(room));
            Add("Room exit", () => data.RoomExitScript(room));
            foreach (var id in data.RoomLocalScriptIds(room)) Add($"Local script {id}", () => data.RoomLocalScript(room, id));
            foreach (var record in stored.Objects.DistinctBy(o => o.Id))
            {
                var name = stored.ObjectNames.TryGetValue(record.Id, out var bytes) ? Text(ScummTextDecoder.DecodeGlyphs(bytes)) : "";
                var target = name.Length > 0 ? name : "Unnamed object";
                foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
                {
                    ScummScriptDisassembly? code = null;
                    string? failure = null;
                    try { code = data.ReadObjectVerb(room, record, verb.VerbId, out _); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { failure = ex.Message; }
                    var title = $"Action · {target}";
                    // Initialization/default table entries are source plumbing, not user verbs.
                    // Their complete code remains inspectable in Advanced.
                    var executable = verb.VerbId < 250 && code is { UndecodedOffset: null } &&
                        code.Instructions.Any(i => i.Offset == code.EntryOffset);
                    var source = $"Room {room:00}, object {record.Id}, verb 0x{verb.VerbId:X2}; " +
                        (code is not null ? $"entry +0x{code.EntryOffset:X4}. Direct exploration action; no sentence is composed and the actor does not walk to the object."
                            : "Source unavailable: " + failure);
                    actions.Add(new(record.Id, verb.VerbId, title, target, code, source, executable));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            return new(readable, [], [], "Room source unavailable: " + ex.Message);
        }
        return new(readable, scripts, actions, null);
    }

    // Native interface verbs can arrive after the room's source has been cached.
    // Resolve only these small display words again, never the room/scripts/geometry.
    private bool RefreshGenericRoomActionWords()
    {
        var words = _playFolder == _game?.Key && _play is { PlayerHasControl: true } session && session.CurrentRoomId == _genericActionsRoom
            ? ScummCutscenePlanner.VerbsOf(session, _genericReadable)
            : _playPlanner is { IsValueCreated: true } lazy ? lazy.Value.Verbs : ScummCutsceneVerbs.None;
        var changed = false;
        for (var index = 0; index < _genericSourceActions.Count; index++)
        {
            var action = _genericSourceActions[index];
            var word = words.Word?.Invoke(action.Verb);
            var title = $"{(string.IsNullOrWhiteSpace(word) ? "Action" : word.Trim())} · {action.Target}";
            if (title == action.Title) continue;
            _genericSourceActions[index] = action with { Title = title };
            changed = true;
        }
        return changed;
    }

    private void BuildGenericRoomActions()
    {
        _genericActionsHost!.Children.Clear();
        _genericSourceHost!.Children.Clear();
        _genericActionButtons.Clear();
        // Parity with LOOM (R-6.9, 24 Oct): the actions are offered through the bar's Interact button and the
        // object's menu, not through a list of every action of the room under the bar.
        _genericActionsHost.Visibility = Visibility.Collapsed;
        _genericSourceHost.Visibility = Visibility.Collapsed;
        UpdateGenericAssumptions();
        FillRoomPanel(CurrentRoomControls.Panel());
        UpdateGenericActionsSection();
    }

    private void UpdateGenericAssumptions()
    {
        var list = new List<string>();
        if (_genericSourceFailure is not null) list.Add(_genericSourceFailure);
        if (_playPartyAssumed) list.Add(SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.PreparationNote);
        if (_browsedScene is { } shown && _currentRoom is not null && ReferenceEquals(shown.Room, _currentRoom)) list.Add(BrowsedSceneNote(shown));
        if (_genericLastAction is { } action) list.AddRange(action.Uncertainties);
        _genericAssumptions = list.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToArray();
    }

    /// <summary>
    /// R-6.43: the Advanced panel's `Room entry`, `Last action` and `Assumptions` sections, drawn once for every game from the provider's data in
    /// LOOM's widgets: the scripts are buttons to their tree entries and the readable text sits in the one nested expander.
    /// </summary>
    private void FillRoomPanel(RoomPanelData? data)
    {
        var advanced = IsAdvancedMode;
        RoomEntryEventButtons.Children.Clear();
        if (data is null || data.EntryText is null && data.Entry is null && data.Exit is null && data.LocalScripts.Count == 0)
        {
            RoomEntryTraceOutput.Text = "";
            RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        }
        else
        {
            RoomEntryTraceExpander.Header = "Room entry";
            RoomEntrySummary.Text = data.EntrySummary ?? (data.Entry is null ? "No embedded entry script." : $"{data.Entry.Instructions.Count} instructions" +
                (data.Exit is { } exit ? $" · exit script {exit.Instructions.Count} instructions" : ""));
            var events = data.EntryEvents ?? new[] { data.Entry, data.Exit }.OfType<ScummScriptDisassembly>()
                .SelectMany(code => code.Instructions.Select(i => ScummScriptEvent.FromInstruction(code, i, ScummScriptEventCertainty.StaticPotential)))
                .OfType<ScummScriptEvent>().Where(e => e.TargetKind is not null).DistinctBy(e => (e.SourceLabel, e.Offset, e.TargetKind, e.TargetId)).ToArray();
            ShowScriptEvents(RoomEntryEventButtons, events);
            foreach (var id in data.LocalScripts.Where(id => !events.Any(e => e.TargetKind == ResourceKind.Script && e.TargetId == id)))
                if (_game?.AllScripts.FirstOrDefault(r => r.ScriptRoom == data.Room && r.ResourceId == id) is { } local)
                    AddResourceButton(RoomEntryEventButtons, $"has {local.Name}", local);
            RoomEntryTraceOutput.Text = data.EntryText ?? string.Join(Environment.NewLine + Environment.NewLine,
                new[] { data.Entry, data.Exit }.OfType<ScummScriptDisassembly>().Select(FormatScript));
            RoomEntryTraceExpander.Visibility = advanced && !string.IsNullOrWhiteSpace(RoomEntryTraceOutput.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        LastActionIssues.Children.Clear();
        if (data?.LastAction is { } action)
        {
            LastActionSummary.Text = action.Summary;
            LastActionTimeline.Text = action.Timeline ?? "";
            LastActionTimeline.Visibility = string.IsNullOrEmpty(action.Timeline) ? Visibility.Collapsed : Visibility.Visible;
            ObjectActionTraceOutput.Text = action.RawTrace ?? action.Source + Environment.NewLine +
                string.Join(Environment.NewLine, action.Uncertainties.Select(item => "Unresolved: " + item)) +
                Environment.NewLine + (action.Code is null ? "Object bytecode unavailable." : FormatScript(action.Code));
            ObjectActionTraceExpander.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ObjectActionTraceOutput.Text = "";
            ObjectActionTraceExpander.Visibility = Visibility.Collapsed;
        }

        StateIssueList.Children.Clear();
        if (data is null || !data.HasAssumptions) { StateIssuesSection.Visibility = Visibility.Collapsed; return; }
        var groups = data.AssumptionGroups ?? (data.Assumptions.Count == 0 ? [] : [new RoomAssumptionGroup("", "", [.. data.Assumptions.Select(item => (item, item))])]);
        var count = data.AssumptionGroups?.Sum(group => group.Items.Count) ?? data.Assumptions.Count;
        StateIssuesSection.Header = count == 0 ? "No assumptions" : $"Assumptions · {count}";
        var dim = new SolidColorBrush(Color.FromRgb(159, 171, 174));
        if (count > 0)
            StateIssueList.Children.Add(new TextBlock
            {
                Text = "What the explorer had to assume or estimate to show this room (clicks sent as assumed input, camera or artwork estimates). Nothing is waiting on you.",
                Foreground = dim, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
            });
        // Flat sections: a title with its count, one sentence of meaning, the first few lines (two lines each, the full text in the tooltip) and a link to reveal the rest.
        const int visibleItems = 4;
        foreach (var group in groups)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            if (group.Title.Length > 0)
            {
                section.Children.Add(new TextBlock
                {
                    Text = $"{group.Title} ({group.Items.Count})", FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)), Margin = new Thickness(0, 0, 0, 2)
                });
                section.Children.Add(new TextBlock { Text = group.Meaning, Foreground = dim, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            }
            var lines = new List<TextBlock>();
            foreach (var (text, tip) in group.Items)
            {
                var line = new TextBlock
                {
                    Text = "• " + text, ToolTip = tip, Foreground = new SolidColorBrush(Color.FromRgb(184, 170, 140)), FontSize = 11,
                    TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = group.Title.Length > 0 ? 30 : double.PositiveInfinity,
                    Margin = new Thickness(8, 0, 0, 3), Visibility = lines.Count < visibleItems || group.Title.Length == 0 ? Visibility.Visible : Visibility.Collapsed
                };
                lines.Add(line); section.Children.Add(line);
            }
            if (group.Title.Length > 0 && lines.Count > visibleItems)
            {
                var more = new TextBlock
                {
                    Text = $"Show {lines.Count - visibleItems} more", Foreground = new SolidColorBrush(Color.FromRgb(183, 132, 66)), FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), TextDecorations = TextDecorations.Underline
                };
                more.MouseLeftButtonUp += (_, _) =>
                {
                    var expand = lines[visibleItems].Visibility != Visibility.Visible;
                    for (var i = visibleItems; i < lines.Count; i++) lines[i].Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                    more.Text = expand ? "Show fewer" : $"Show {lines.Count - visibleItems} more";
                };
                section.Children.Add(more);
            }
            StateIssueList.Children.Add(section);
        }
        StateIssuesSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>`ACTIONS · object` (LOOM's section): a flat button per verb of the selected object.</summary>
    private void UpdateGenericActionsSection()
    {
        if (!GenericRoomActions || !IsAdvancedMode || _genericInteractTarget is not int id || GenericObjectActions(id) is not { Count: > 0 } actions)
        {
            if (_genericActionsShownFor is null) return;
            _genericActionsShownFor = null;
            ObjectActionsList.Children.Clear();
            ObjectActionsSection.Visibility = Visibility.Collapsed;
            return;
        }
        if (_genericActionsShownFor == id && ObjectActionsList.Children.Count > 0) return;
        _genericActionsShownFor = id;
        ObjectActionsList.Children.Clear();
        ObjectActionsHeader.Text = $"ACTIONS · {(actions[0].Target is { Length: > 0 } name ? name : $"Object {id}")}";
        foreach (var action in actions)
        {
            var button = ActionPanelButton(GenericActionWord(action), action.Source);
            button.Click += (_, _) => RunGenericRoomAction(action);
            ObjectActionsList.Children.Add(button);
        }
        ObjectActionsSection.Visibility = Visibility.Visible;
    }

    // A static preview's ambient session (R-8.5b) takes the explicit exploration action too: the object's verb code runs from the preview's state.
    // A still static preview (no ambient script, no clock) prepares its preview session when the user picks an action (R-6.17, R-8.5b1).
    private bool GenericRoomActionCanStart => GenericRoomActions && !_genericActionStarting && !_introPreparing &&
        _game is { } game && _playFolder == game.Key && _play is not null &&
        (StillPreviewCanAct || PlayActive &&
            (_play is { PlayerHasControl: true, BootRunning: false } || _playAmbient && _play is { InCutscene: false }) &&
            _play.CurrentRoomId == _genericActionsRoom) &&
        (ScummProfile.For(game).EngineVersion > ScummVersion.V2 || _play.InventoryCharactersAvailable);

    // §8.6: where the game's scenes can be picked, an interaction is picked the same way (any time, the state set up, a running scene ended).
    private bool GenericInteractionCanStart => GenericRoomActions && !_genericActionStarting &&
        (PlayCutscenes && _playPlanner is not null && PlayCanStartCutscene || GenericRoomActionCanStart);

    private bool StillPreviewCanAct => _catalogPreviewOnly && _game is { } still && !UsesManiacCutsceneSetup(still) && !PlayActive && _play is not null && _genericActionsRoom is not null &&
        _currentRoomResource?.ResourceId == _genericActionsRoom;

    private void UpdateGenericRoomActionAvailability()
    {
        if (++_genericNamesFrame % 12 == 0 && _genericActionsHost is not null && GenericRoomActions &&
            _genericActionsRoom is not null && RefreshGenericRoomActionWords()) BuildGenericRoomActions();
        UpdateInteractButton();
    }

    // The data behind the shared Interact control (R-6.22, R-16.9): the selected object and its source actions.
    // The button, menu and right click are the shared ones in MainWindow.RoomControls.cs.
    private int? _genericInteractTarget;

    private IReadOnlyList<GenericSourceAction> GenericObjectActions(int objectId) =>
        _genericSourceActions.Where(a => a.Executable && a.Object == objectId).DistinctBy(a => a.Verb).ToArray();

    private static string GenericActionWord(GenericSourceAction action) =>
        action.Title.Split(" · ", 2)[0];

    /// <summary>The object (with actions) under a native screen point, from the same scene the overlays read.</summary>
    private int? GenericObjectAt((int X, int Y)? point)
    {
        if (point is not { } p || _playOverlayScene is not { } scene || _playShown is not { } frame) return null;
        var viewport = frame.Viewport;
        if (viewport.ToRoom(p.X, p.Y) is not { } at) return null;
        return ScummRoomOverlays.Objects(scene, _playDoorwayTargets)
            .FirstOrDefault(o => o.Contains(at.X, at.Y) && GenericObjectActions(o.ObjectId).Count > 0)?.ObjectId;
    }

    // A click selects the object under it, as in LOOM; the click itself still goes to the game.
    private void SelectGenericInteractAt((int X, int Y)? point)
    {
        if (!GenericRoomActions) return;
        _genericInteractTarget = GenericObjectAt(point);
        UpdateInteractButton();
    }

    // Right button where it stays the app's (R-7.16: up to engine version 3): LOOM's Interact menu at the pointer.
    private bool TryOpenGenericInteractAt((int X, int Y)? point)
    {
        if (!GenericRoomActions || GenericObjectAt(point) is not int objectId) return false;
        _genericInteractTarget = objectId;
        UpdateInteractButton();
        ShowInteractMenu(PlayScreenImage, System.Windows.Controls.Primitives.PlacementMode.MousePoint);
        return true;
    }

    // A native hotspot row selects its object and opens its menu; choosing a verb remains an explicit action.
    private void SelectGenericInteractTarget(int objectId)
    {
        if (GenericObjectActions(objectId).Count == 0) return;
        _genericInteractTarget = objectId;
        UpdateInteractButton();
        if (InteractButton.IsEnabled) ShowInteractMenu(InteractButton, System.Windows.Controls.Primitives.PlacementMode.Top);
    }

    private async void RunGenericRoomAction(GenericSourceAction action)
    {
        // R-8.38 (user, 31 Oct 2026): a picked interaction goes the way of a picked scene: the room opened (ending a scene still playing), the state
        // its code tests set (§8.6), the verb given to the game's sentence script with the hero put at the object, or run directly where it refuses.
        if (PlayCutscenes && _playPlanner is not null && PlayCanStartCutscene && _genericActionsRoom is int pickedRoom)
        {
            var trigger = new ScummCutscene(pickedRoom, action.Object, action.Target, action.Verb,
                [$"Room {pickedRoom} object {action.Object} verb 0x{action.Verb:X2}"], 0, 0, 0, "");
            await PlayScummCutsceneAsync(new ScummCutsceneGroup(trigger, []), action.Title);
            _genericLastAction = new RoomPanelAction(action.Title, $"{action.Target} · {GenericActionWord(action)}: picked.", action.Source,
                _play?.Snapshot.Uncertainties.TakeLast(6).ToArray() ?? [], action.Code);
            UpdateGenericAssumptions();
            FillRoomPanel(CurrentRoomControls.Panel());
            return;
        }
        if (!GenericRoomActionCanStart || _game is not { } game || _genericActionsRoom is not int roomId ||
            game.Rooms.FirstOrDefault(r => r.ResourceId == roomId) is not { } room) return;
        var launch = ++_cutsceneLaunchGeneration;
        _genericActionStarting = true;
        UpdateGenericRoomActionAvailability();
        try
        {
            if (StillPreviewCanAct && _play is { } source)
            {
                // R-8.5b1: the still preview becomes the room entered without its entry code (nothing else runs), and the action runs there.
                var prepared = source.PrepareAmbientRoom(room, requireAmbient: false);
                if (prepared.Session is not ScummGameSession still) { if (IsAdvancedMode) StatusText.ToolTip = prepared.Reason; return; }
                _playLiveSource = source;
                _play = still;
                _playAmbient = true;
                _playScreen = new ScummPlayScreen(game, still, _playIndexedStates);
                _sceneSound.Attach(game, still, RenderPlaySound, PlaySoundLoop);
                if (game.Speech is { } speech) _playSpeech.Attach(speech, still.Messages);
                StartPlayClock();
            }
            // This explicit exploration action never visits a room merely to enable
            // itself. A static/manual fallback remains quiet until native control is
            // available in this very room; entry scenes are reserved for their button.
            if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration || _currentRoomResource?.ResourceId != roomId ||
                !PlayActive || _play is not { } session || !session.PlayerHasControl && !_playAmbient || session.CurrentRoomId != roomId) return;
            var result = session.ActivateObjectVerb(action.Object, action.Verb);
            _playPaused = false;
            ShowPlayFrame();
            PlayScreenImage.Focus();
            StatusText.Text = action.Title;
            _genericLastAction = new RoomPanelAction(action.Title, $"{action.Target} · {GenericActionWord(action)}: ran.", action.Source, result.Uncertainties.ToArray(), action.Code);
            UpdateGenericAssumptions();
            FillRoomPanel(CurrentRoomControls.Panel());
            StatusText.ToolTip = IsAdvancedMode ? action.Source + Environment.NewLine + string.Join(Environment.NewLine, result.Uncertainties) : null;
            UpdatePlaybackControls();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            if (ReferenceEquals(_game, game) && launch == _cutsceneLaunchGeneration && IsAdvancedMode)
                StatusText.ToolTip = $"The exploration action could not start: {ex.Message}";
        }
        finally { _genericActionStarting = false; UpdateGenericRoomActionAvailability(); }
    }
}

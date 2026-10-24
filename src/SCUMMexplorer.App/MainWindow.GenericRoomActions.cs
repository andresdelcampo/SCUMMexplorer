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
    private sealed record GenericSourceAction(int Object, int Verb, string Title, string Target, ScummScriptDisassembly? Code, string Source, bool Executable);

    private bool GenericRoomActions => _game is { } game && !IsLoomGame && ScummProfile.For(game).PlaysThroughVerbs;

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
        ObjectListSection.Visibility = Visibility.Collapsed;
        RoomNavigationPanel.Visibility = Visibility.Collapsed;
        RoomEntryTraceExpander.Visibility = Visibility.Collapsed;
        if (changed)
        {
            _genericActionsGame = game;
            _genericActionsRoom = roomId;
            _genericRoomScripts.Clear();
            _genericSourceActions.Clear();
            ReadGenericRoomActions(game, roomId);
        }
        var namesChanged = RefreshGenericRoomActionWords();
        if (changed || namesChanged || _genericActionsAdvanced != IsAdvancedMode || _genericActionsHost.Children.Count == 0)
        {
            _genericActionsAdvanced = IsAdvancedMode;
            BuildGenericRoomActions();
        }
        UpdateGenericRoomActionAvailability();
    }

    private void ReadGenericRoomActions(GameLibrary game, int room)
    {
        try
        {
            // Archive/disk readers own seek positions; background scene derivation and
            // live playback must not share this inspection reader.
            var sourceGame = game.V4Archive is not null || game.V4Disks is not null
                ? SCUMMexplorer.Core.Discovery.GameFolderScanner.Reopen(game) : game;
            var data = ScummDataSources.For(sourceGame);
            var stored = data.ReadRoom(room);
            var verbs = data.ReadObjectVerbs(room, stored.Objects);
            _genericReadable = ManiacPreviewText.For(game);
            string Text(string text) => (_genericReadable?.Invoke(text) ?? text).Replace("@", "", StringComparison.Ordinal).Trim();
            void Add(string label, Func<ScummScriptDisassembly?> read)
            {
                try { if (read() is { } code) _genericRoomScripts.Add((label, code)); }
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
                    _genericSourceActions.Add(new(record.Id, verb.VerbId, title, target, code, source, executable));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            _genericRoomScripts.Clear();
            _genericSourceActions.Clear();
            if (IsAdvancedMode) _genericSourceHost!.Children.Add(new TextBlock { Text = "Room source unavailable: " + ex.Message, TextWrapping = TextWrapping.Wrap });
        }
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
        if (!IsAdvancedMode) return;
        FillRoomInspection(_genericSourceHost, CurrentRoomControls.Inspection());
    }

    /// <summary>The Advanced inspection (R-16.9): headed sections of expanders with the raw code, then the relationships; the content is the provider's.</summary>
    private void FillRoomInspection(Panel host, RoomInspection? inspection)
    {
        if (inspection is null) return;
        var heading = (string text, double top) => new TextBlock { Text = text, Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, top, 0, 4), TextWrapping = TextWrapping.Wrap };
        var first = true;
        foreach (var section in inspection.Sections)
        {
            host.Children.Add(heading(section.Heading, first ? 8 : 12));
            first = false;
            foreach (var item in section.Items)
            {
                if (item.Detail is null)
                {
                    if (item.Code is { } script) host.Children.Add(GenericSourceCode(item.Header, script));
                    continue;
                }
                var content = new StackPanel();
                content.Children.Add(new TextBlock { Text = item.Detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) });
                if (item.Code is { } code) content.Children.Add(GenericSourceCode("Raw disassembly", code));
                host.Children.Add(new Expander { Header = item.Header, Content = content, Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)) });
            }
        }
        host.Children.Add(heading("RELATIONSHIPS · POSSIBLE SOURCE OUTCOMES", 12));
        var links = new StackPanel();
        ShowScriptEvents(links, inspection.Relationships);
        host.Children.Add(links);
        if (inspection.Relationships.Count == 0) host.Children.Add(new TextBlock { Text = "No literal resource links were decoded. Computed destinations remain unresolved.", TextWrapping = TextWrapping.Wrap });
    }

    private static Expander GenericSourceCode(string label, ScummScriptDisassembly code)
    {
        var content = new StackPanel();
        // Delay formatting/widget attachment until requested; room changes never open code.
        var expander = new Expander
        {
            Header = label, Content = content, Margin = new Thickness(0, 2, 0, 2),
            Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210))
        };
        expander.Expanded += (_, _) =>
        {
            if (content.Children.Count == 0) content.Children.Add(new TextBox
            {
                IsReadOnly = true, Text = code.Format(true), TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"), FontSize = 11, MaxHeight = 320,
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                Background = new SolidColorBrush(Color.FromRgb(16, 24, 33)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 68, 82)), Padding = new Thickness(6),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
        };
        return expander;
    }

    // A static preview's ambient session (R-8.5b) takes the explicit exploration action too: the object's verb code runs from the preview's state.
    // A still static preview (no ambient script, no clock) prepares its preview session when the user picks an action (R-6.17, R-8.5b1).
    private bool GenericRoomActionCanStart => GenericRoomActions && !_genericActionStarting && !_introPreparing &&
        _game is { } game && _playFolder == game.Key && _play is not null &&
        (StillPreviewCanAct || PlayActive &&
            (_play is { PlayerHasControl: true, BootRunning: false } || _playAmbient && _play is { InCutscene: false }) &&
            _play.CurrentRoomId == _genericActionsRoom) &&
        (ScummProfile.For(game).EngineVersion > ScummVersion.V2 || _play.InventoryCharactersAvailable);

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
        var viewport = new ScummRoomViewport(frame.ViewLeft, frame.RoomTop, frame.RoomBottom);
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

    private void RunGenericRoomAction(GenericSourceAction action)
    {
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

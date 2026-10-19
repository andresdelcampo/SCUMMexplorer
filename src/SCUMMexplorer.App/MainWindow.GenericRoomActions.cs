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
    private readonly List<Button> _genericActionButtons = [];
    private readonly List<(string Label, ScummScriptDisassembly Code)> _genericRoomScripts = [];
    private readonly List<GenericSourceAction> _genericSourceActions = [];
    private sealed record GenericSourceAction(int Object, int Verb, string Title, ScummScriptDisassembly? Code, string Source, bool Executable);

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
            if (!GenericRoomActions) InteractButton.Visibility = Visibility.Visible;
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
        if (changed || _genericActionsAdvanced != IsAdvancedMode || _genericActionsHost.Children.Count == 0)
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
            var readable = ManiacPreviewText.For(game);
            string Text(string text) => (readable?.Invoke(text) ?? text).Replace("@", "", StringComparison.Ordinal).Trim();
            var words = _playFolder == game.Key && _play is not null ? ScummCutscenePlanner.VerbsOf(_play, Text)
                : _playPlanner is { IsValueCreated: true } lazy ? lazy.Value.Verbs : ScummCutsceneVerbs.None;
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
                    var word = words.Word?.Invoke(verb.VerbId);
                    var title = $"{(string.IsNullOrWhiteSpace(word) ? "Action" : Text(word))} · {target}";
                    // Initialization/default table entries are source plumbing, not user verbs.
                    // Their complete code remains inspectable in Advanced.
                    var executable = verb.VerbId < 250 && code is { UndecodedOffset: null } &&
                        code.Instructions.Any(i => i.Offset == code.EntryOffset);
                    var source = $"Room {room:00}, object {record.Id}, verb 0x{verb.VerbId:X2}; " +
                        (code is not null ? $"entry +0x{code.EntryOffset:X4}. Direct exploration action; no sentence is composed and the actor does not walk to the object."
                            : "Source unavailable: " + failure);
                    _genericSourceActions.Add(new(record.Id, verb.VerbId, title, code, source, executable));
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

    private void BuildGenericRoomActions()
    {
        _genericActionsHost!.Children.Clear();
        _genericSourceHost!.Children.Clear();
        _genericActionButtons.Clear();
        var choices = new StackPanel();
        var totals = _genericSourceActions.Where(a => a.Executable).GroupBy(a => a.Title).ToDictionary(g => g.Key, g => g.Count());
        var ordinal = new Dictionary<string, int>();
        foreach (var action in _genericSourceActions.Where(a => a.Executable))
        {
            var index = ordinal[action.Title] = ordinal.GetValueOrDefault(action.Title) + 1;
            var title = totals[action.Title] > 1 ? $"{action.Title} ({index})" : action.Title;
            var button = new Button
            {
                Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap },
                HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8, 5, 8, 5), ToolTip = IsAdvancedMode ? action.Source : null,
                Tag = action
            };
            button.Click += (_, _) => RunGenericRoomAction(action);
            _genericActionButtons.Add(button);
            choices.Children.Add(button);
        }
        if (choices.Children.Count > 0)
            _genericActionsHost.Children.Add(new Expander
            {
                Header = "Interact", Content = new ScrollViewer { Content = choices, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                Margin = new Thickness(0, 6, 0, 0)
            });
        _genericActionsHost.Visibility = choices.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!IsAdvancedMode) return;
        _genericSourceHost.Children.Add(new TextBlock
        {
            Text = "ROOM SCRIPTS", Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, 8, 0, 4)
        });
        foreach (var (label, code) in _genericRoomScripts) _genericSourceHost.Children.Add(GenericSourceCode(label, code));
        _genericSourceHost.Children.Add(new TextBlock { Text = "INTERACTIONS", Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, 12, 0, 4) });
        foreach (var action in _genericSourceActions)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = action.Source, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) });
            if (action.Code is { } code) content.Children.Add(GenericSourceCode("Raw disassembly", code));
            _genericSourceHost.Children.Add(new Expander { Header = $"{action.Title} · object {action.Object} / verb 0x{action.Verb:X2}", Content = content });
        }
        _genericSourceHost.Children.Add(new TextBlock { Text = "RELATIONSHIPS · POSSIBLE SOURCE OUTCOMES", Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, 12, 0, 4), TextWrapping = TextWrapping.Wrap });
        var scripts = _genericRoomScripts.Select(s => s.Code).Concat(_genericSourceActions.Select(a => a.Code).OfType<ScummScriptDisassembly>()).Distinct();
        var relationships = scripts.SelectMany(code => code.Instructions.Select(i => ScummScriptEvent.FromInstruction(code, i, ScummScriptEventCertainty.StaticPotential)))
            .OfType<ScummScriptEvent>().Where(e => e.TargetKind is not null).DistinctBy(e => (e.SourceLabel, e.Offset, e.TargetKind, e.TargetId)).ToArray();
        var links = new StackPanel();
        ShowScriptEvents(links, relationships);
        _genericSourceHost.Children.Add(links);
        if (relationships.Length == 0) _genericSourceHost.Children.Add(new TextBlock { Text = "No literal resource links were decoded. Computed destinations remain unresolved.", TextWrapping = TextWrapping.Wrap });
    }

    private static Expander GenericSourceCode(string label, ScummScriptDisassembly code)
    {
        var content = new StackPanel();
        // Delay formatting/widget attachment until requested; room changes never open code.
        var expander = new Expander { Header = label, Content = content, Margin = new Thickness(0, 2, 0, 2) };
        expander.Expanded += (_, _) =>
        {
            if (content.Children.Count == 0) content.Children.Add(new TextBox
            {
                IsReadOnly = true, Text = code.Format(true), TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"), FontSize = 11, MaxHeight = 320,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
        };
        return expander;
    }

    private bool GenericRoomActionCanStart => GenericRoomActions && !_genericActionStarting && !_introPreparing &&
        _game is { } game && _playFolder == game.Key && _play is { PlayerHasControl: true, BootRunning: false } &&
        (ScummProfile.For(game).EngineVersion > ScummVersion.V2 || _play.InventoryCharactersAvailable);

    private void UpdateGenericRoomActionAvailability()
    {
        foreach (var button in _genericActionButtons) button.IsEnabled = GenericRoomActionCanStart;
    }

    private async void RunGenericRoomAction(GenericSourceAction action)
    {
        if (!GenericRoomActionCanStart || _game is not { } game || _genericActionsRoom is not int roomId ||
            game.Rooms.FirstOrDefault(r => r.ResourceId == roomId) is not { } room) return;
        var launch = ++_cutsceneLaunchGeneration;
        _genericActionStarting = true;
        UpdateGenericRoomActionAvailability();
        try
        {
            // This is the explicit exploration action, not a catalog visit or a native click.
            // A failed quiet preview may be static; open the existing source-action path only
            // when the retained session has finished its native startup and departed scene.
            if (!PlayActive || _play!.CurrentRoomId != roomId)
            {
                SelectRoomWithIntent(room, RoomOpenKind.Cutscene, reloadSelected: true);
                if (_roomOpenTask is { } opening) await opening;
            }
            if (!ReferenceEquals(_game, game) || launch != _cutsceneLaunchGeneration || _currentRoomResource?.ResourceId != roomId ||
                !PlayActive || _play is not { PlayerHasControl: true } session || session.CurrentRoomId != roomId) return;
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// R-16.9: the one implementation of the room controls (playback bar, Play Cutscene, Interact, Sounds). It reads
/// an <see cref="IRoomControlProvider"/> and has no game, family or session-type test of its own; the providers
/// (<see cref="LoomRoomControls"/>, <see cref="PlayRoomControls"/>) only fill it with their data.
/// </summary>
public partial class MainWindow
{
    private IRoomControlProvider? _loomRoomControlsInstance, _playRoomControlsInstance;

    // The one place that picks the provider for the open game (composition, not UI behaviour).
    private IRoomControlProvider CurrentRoomControls =>
        PlayCutscenes ? _playRoomControlsInstance ??= new PlayRoomControls(this)
        : IsLoomGame ? _loomRoomControlsInstance ??= new LoomRoomControls(this)
        : NoRoomControls.Instance;

    private void RefreshRoomControls()
    {
        var provider = CurrentRoomControls;
        var state = provider.ReadState(_currentRoomResource?.ResourceId, IsAdvancedMode);
        ApplyTransport(state.Transport);
        // In a played game's room the bar keeps its row while it is not shown (a picked room being prepared), so the screen above does not shrink when
        // it comes back (user report, 2 November 2026: rooms changed size a second after they showed).
        PlaybackBar.Visibility = state.BarVisible ? Visibility.Visible : PlayCutscenes ? Visibility.Hidden : Visibility.Collapsed;
        CutsceneMenuButton.IsEnabled = state.GlobalCutsceneEnabled;
        CutsceneMenuButton.Content = "Play Cutscene";
        CutsceneMenuButton.ToolTip = state.CanStartCutscene ? RoomControlText.GlobalCutsceneTip : RoomControlText.GlobalCutsceneBusyTip;
        var entries = state.RoomCutscenes;
        RoomCutsceneButton.Visibility = state.BarVisible && (entries.Count > 0 || state.CutsceneListing) ? Visibility.Visible : Visibility.Collapsed;
        RoomCutsceneButton.IsEnabled = state.RoomCutsceneEnabled;
        RoomCutsceneButton.Content = ButtonWithSpinner(RoomControlText.RoomCutsceneCaption(entries.Count, state.CutsceneListing), state.ScanComputing);
        RoomCutsceneButton.ToolTip = RoomControlText.RoomCutsceneTip(state.CutsceneListing, state.CanStartCutscene, entries);
        CutsceneScanNote.Visibility = state.ScanNoteVisible ? Visibility.Visible : Visibility.Collapsed;
        CutsceneScanProgress.Visibility = state.ScanProgressVisible ? Visibility.Visible : Visibility.Collapsed;
        ApplyInteract(provider.ReadInteract());
    }

    // Cheap, on every frame of a live game.
    private void RefreshRoomTransport() { ApplyTransport(CurrentRoomControls.ReadTransport()); ApplyInteract(CurrentRoomControls.ReadInteract()); }

    private void ApplyTransport(RoomTransportState transport)
    {
        PlaybackPauseButton.IsEnabled = transport.PauseEnabled;
        PlaybackPauseButton.Content = RoomControlText.PauseCaption(transport.Paused);
        PlaybackPauseButton.ToolTip = RoomControlText.PauseTip;
        AdvanceFrameButton.IsEnabled = transport.StepEnabled;
        SkipCutsceneButton.IsEnabled = transport.InCutscene && transport.CutsceneSkippable;
        SkipCutsceneButton.ToolTip = RoomControlText.SkipTip(transport.InCutscene, transport.CutsceneSkippable);
        SkipLineButton.IsEnabled = transport.SkipLineEnabled;
    }

    private void ApplyInteract(RoomInteractState interact)
    {
        InteractButton.Visibility = interact.Visible ? Visibility.Visible : Visibility.Collapsed;
        InteractButton.Content = RoomControlText.InteractCaption(interact.TargetName);
        InteractButton.IsEnabled = interact.Enabled;
        InteractButton.ToolTip = RoomControlText.InteractTooltip(interact.DoubleClickLooks);
    }

    private void ShowInteractMenu(FrameworkElement anchor, PlacementMode placement)
    {
        if (CurrentRoomControls.InteractionMenu(IsAdvancedMode) is not { Choices.Count: > 0 } menu) return;
        OpenPopupMenu(anchor, placement, menu.Heading, menu.Choices.Select(choice => (choice.Label, choice.Source, choice.Run)));
    }

    /// <summary>The Interact button (R-6.39): the room's interactions grouped by object, upfront; groups collapse when there are many.</summary>
    private void ShowInteractOverview(FrameworkElement anchor, PlacementMode placement)
    {
        if (CurrentRoomControls.InteractionOverview(IsAdvancedMode) is not { Groups.Count: > 0 } overview) return;
        OpenGroupedPopupMenu(anchor, placement, "INTERACTIONS IN THIS ROOM",
            overview.Groups.OrderByDescending(group => group.Selected).Select(group =>
                (group.Heading, group.Selected, group.Choices.Select(choice => (choice.Label, choice.Source, choice.Run)).ToList())).ToList(),
            RoomInteractionRules.GroupsStartOpen(overview.Actions));
    }

    /// <summary>Like <see cref="OpenPopupMenu"/>, with one collapsible group per object (a header with the action count, then its rows).</summary>
    private static void OpenGroupedPopupMenu(FrameworkElement target, PlacementMode placement, string heading,
        IReadOnlyList<(string Heading, bool Selected, List<(string Label, string? Tip, Action Run)> Rows)> groups, bool allOpen)
    {
        var gold = new SolidColorBrush(Color.FromRgb(183, 132, 66));
        var text = new SolidColorBrush(Color.FromRgb(231, 224, 210));
        var hover = new SolidColorBrush(Color.FromRgb(46, 92, 116));
        var popup = new Popup { PlacementTarget = target, Placement = placement, StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.None };
        var host = new StackPanel { MinWidth = 190 };
        host.Children.Add(new TextBlock { Text = heading, Foreground = gold, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 7, 10, 3) });
        var list = new StackPanel();
        foreach (var (name, selected, rows) in groups)
        {
            var open = allOpen || selected;
            var body = new StackPanel { Visibility = open ? Visibility.Visible : Visibility.Collapsed };
            var header = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4, 10, 4), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Foreground = gold
            };
            void ShowHeader() => header.Content = new TextBlock
            {
                Text = $"{(body.Visibility == Visibility.Visible ? "▾" : "▸")} {name.ToUpperInvariant()}" + (body.Visibility == Visibility.Visible ? "" : $" · {rows.Count}"),
                FontSize = 11, FontWeight = FontWeights.Bold
            };
            ShowHeader();
            header.MouseEnter += (_, _) => header.Background = hover;
            header.MouseLeave += (_, _) => header.Background = Brushes.Transparent;
            header.Click += (_, _) => { body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; ShowHeader(); };
            list.Children.Add(header);
            foreach (var (label, tip, run) in rows)
            {
                var item = new Button
                {
                    Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 },
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(22, 4, 10, 4),
                    Background = Brushes.Transparent, Foreground = text, BorderThickness = new Thickness(0), ToolTip = tip
                };
                item.MouseEnter += (_, _) => item.Background = hover;
                item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
                item.Click += (_, _) => { popup.IsOpen = false; run(); };
                body.Children.Add(item);
            }
            list.Children.Add(body);
        }
        host.Children.Add(new ScrollViewer { Content = list, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        popup.Child = new Border
        {
            Child = host, Background = new SolidColorBrush(Color.FromRgb(16, 24, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(0, 0, 0, 4)
        };
        popup.IsOpen = true;
    }

    private void ShowRoomSoundsMenu()
    {
        if (CurrentRoomControls.SoundMenu(IsAdvancedMode) is not { } menu) return;
        var entries = new List<(string Label, string? Tip, Action Run)>();
        if (menu.Playing) entries.Add(("■ Stop", null, menu.Stop));
        entries.AddRange(menu.Choices.Select(choice => (choice.Label, choice.Tip, choice.Play)));
        OpenPopupMenu(RoomSoundsButton, PlacementMode.Top, "SOUNDS IN THIS ROOM", entries);
    }

    private void ShowRoomCutsceneMenu()
    {
        var state = CurrentRoomControls.ReadState(_currentRoomResource?.ResourceId, IsAdvancedMode);
        if (!state.RoomCutsceneEnabled) return;
        var items = state.RoomCutscenes;
        if (items.Count == 1) items[0].Start?.Invoke();
        else if (items.Count > 1)
            OpenPopupMenu(RoomCutsceneButton, PlacementMode.Top, "CUTSCENES IN THIS ROOM",
                items.Where(item => item.Start is not null).Select(item => (item.Title, IsAdvancedMode ? item.Note : null, item.Start!)));
    }

    /// <summary>
    /// The room, object and verb a cutscene entry's id names (<c>scumm-r-o-v</c>, <c>cd-r-o-v</c>, LOOM's catalog <c>Room NN object O Verb 0xVV</c>);
    /// null for a room entry, the intro or a named sequence.
    /// </summary>
    internal static (int Room, int Object, int Verb)? CutsceneTrigger(string id)
    {
        var m = System.Text.RegularExpressions.Regex.Match(id, @"^(?:scumm|cd)-(\d+)-(\d+)-(\d+)$");
        if (m.Success && int.Parse(m.Groups[2].Value) != 0) return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        m = System.Text.RegularExpressions.Regex.Match(id, @"^Room (\d+) object (\d+) Verb 0x([0-9A-F]+)$");
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), Convert.ToInt32(m.Groups[3].Value, 16)) : null;
    }

    /// <summary>
    /// R-8.2b (user, 31 October 2026): a room's Play Cutscene list leaves out the scenes that are an action of this room's Interact (the same object and
    /// verb), which offers them already; room entries, the intro, named sequences and scenes started in another room stay.
    /// </summary>
    private static bool OfferedByInteract(string id, int room, Func<int, int, bool> interactHas) =>
        CutsceneTrigger(id) is { } t && t.Room == room && interactHas(t.Object, t.Verb);

    private static RoomCutsceneEntry ToEntry(CutscenePickerItem item) => new(item.Title, item.Category, item.Note, item.Play, item.Status);

    private sealed class NoRoomControls : IRoomControlProvider
    {
        public static NoRoomControls Instance { get; } = new();
        public RoomTransportState ReadTransport() => RoomTransportState.Idle;
        public RoomControlState ReadState(int? room, bool advanced) => RoomControlState.Hidden;
        public RoomInteractState ReadInteract() => new(false, false, null);
        public RoomCutscenePicker GlobalCutscenes(bool advanced) => new([], "");
        public bool RoomListsDone => true;
        public RoomGameStatusEntry GameStatus() => RoomGameStatusEntry.None;
        public RoomIntroEntry GameIntro() => RoomIntroEntry.None;
        public RoomPanelData? Panel() => null;
        public void TogglePause() { }
        public void Step() { }
        public void Skip() { }
        public void SkipLine() { }
        public RoomInteractionMenu? InteractionMenu(bool advanced) => null;
        public RoomInteractionOverview? InteractionOverview(bool advanced) => null;
        public RoomSoundMenu? SoundMenu(bool advanced) => null;
    }

}

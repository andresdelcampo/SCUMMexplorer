using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>Explicit inventory choices on one paused session; closing discards only un-applied choices.</summary>
public partial class InventoryStatusWindow : Window
{
    private readonly ScummGameSession _session;
    private readonly Func<bool> _isCurrent;
    private readonly Action _refresh;
    private readonly bool _advanced;
    private readonly Func<string, string> _readable;
    private ScummInventoryStatusSnapshot _snapshot = null!;
    private ScummInventoryUndoToken? _undo;
    private readonly Dictionary<int, int> _targets = [];

    public InventoryStatusWindow(ScummGameSession session, Func<bool> isCurrent, Action refresh,
        bool advanced = false, Func<string, string>? readable = null)
    {
        InitializeComponent();
        _session = session; _isCurrent = isCurrent; _refresh = refresh; _advanced = advanced;
        _readable = readable ?? (text => text);
        LoadSnapshot();
    }

    private void LoadSnapshot()
    {
        _snapshot = _session.CaptureInventoryStatus();
        _targets.Clear(); InventoryRows.Children.Clear();
        string DisplayName(string name) => _readable(name).Replace("@", "", StringComparison.Ordinal).Trim();
        var characters = _snapshot.Characters.Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new CharacterChoice(c.ActorId, DisplayName(c.Name!))).ToArray();
        string? CharacterName(int id) => characters.FirstOrDefault(c => c.ActorId == id)?.Name;
        SessionSummary.Text = CharacterName(_snapshot.CurrentActorId) is { } current
            ? current + (_advanced ? $" · Room {_snapshot.RoomId:00}" : "") : "";
        foreach (var item in _snapshot.Items)
        {
            if (!_advanced && (string.IsNullOrWhiteSpace(item.Name) || CharacterName(item.OwnerId) is null)) continue;
            _targets[item.ObjectId] = item.OwnerId;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 1), Background = new SolidColorBrush(Color.FromRgb(25, 38, 50)) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = DisplayName(item.Name ?? $"Object {item.ObjectId}"), Margin = new Thickness(10, 12, 8, 12), TextWrapping = TextWrapping.Wrap });
            var owner = new TextBlock { Text = CharacterName(item.OwnerId) ?? $"Actor {item.OwnerId}", Margin = new Thickness(10, 12, 8, 12) };
            Grid.SetColumn(owner, 1); row.Children.Add(owner);
            var choice = new ComboBox { ItemsSource = characters, DisplayMemberPath = nameof(CharacterChoice.Name), SelectedValuePath = nameof(CharacterChoice.ActorId),
                SelectedValue = item.OwnerId, Tag = item.ObjectId, Margin = new Thickness(8),
                IsEnabled = _snapshot.CanEdit && _isCurrent() && CharacterName(item.OwnerId) is not null && !string.IsNullOrWhiteSpace(item.Name), MinWidth = 95 };
            choice.SelectionChanged += (_, _) =>
            {
                if (choice.SelectedValue is int target) _targets[item.ObjectId] = target;
                UpdateButtons();
            };
            if (_advanced) choice.ToolTip = $"Object {item.ObjectId} · slot {item.Slot} · retained room {item.RetainedRoomId}";
            Grid.SetColumn(choice, 2); row.Children.Add(choice); InventoryRows.Children.Add(row);
        }
        if (InventoryRows.Children.Count == 0 && !_snapshot.Items.Any(item => _snapshot.Characters.Any(c => c.ActorId == item.OwnerId)))
            InventoryRows.Children.Add(new TextBlock { Text = "No items held by your characters.", Margin = new Thickness(12), Foreground = new SolidColorBrush(Color.FromRgb(198, 185, 159)) });
        AdvancedEvidence.Visibility = _advanced ? Visibility.Visible : Visibility.Collapsed;
        AdvancedEvidence.Text = string.Join("\n", _snapshot.Characters.Select(c => c.Evidence).Distinct()) +
            (_snapshot.Refusal is { } refusal ? "\n" + refusal : "") + "\n" +
            string.Join("\n", _session.SessionTrace.Where(line => line.StartsWith("Explicit inventory ", StringComparison.Ordinal)).TakeLast(5));
        UpdateButtons();
    }

    private bool HasChanges => _snapshot.Items.Any(item => _targets.TryGetValue(item.ObjectId, out var target) && target != item.OwnerId);
    private void UpdateButtons()
    {
        ApplyButton.IsEnabled = _isCurrent() && _snapshot.CanEdit && HasChanges;
        UndoButton.IsEnabled = _isCurrent() && _snapshot.CanEdit && _undo is not null;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrent()) { Rejected("The live session changed."); return; }
        var changes = _snapshot.Items.Where(item => _targets.TryGetValue(item.ObjectId, out var target) && target != item.OwnerId)
            .Select(item => new ScummInventoryTransfer(item.ObjectId, _targets[item.ObjectId])).ToArray();
        var result = _session.ApplyInventoryTransfers(_snapshot, changes);
        if (!result.Applied) { Rejected(result.Refusal); return; }
        _undo = result.Undo;
        _refresh(); LoadSnapshot();
        ChoiceSummary.Text = $"Your inventory choice was applied ({changes.Length} {(changes.Length == 1 ? "item" : "items")}).";
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrent() || _undo is null) { Rejected("The live session changed."); return; }
        var result = _session.UndoInventoryTransfers(_undo);
        if (!result.Applied) { _undo = null; Rejected(result.Refusal); return; }
        _undo = null;
        _refresh(); LoadSnapshot(); ChoiceSummary.Text = "Your last inventory choice was undone.";
    }

    private void Rejected(string? detail)
    {
        LoadSnapshot(); ChoiceSummary.Text = "No changes were applied.";
        if (_advanced && detail is not null) AdvancedEvidence.Text += "\n" + detail;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private sealed record CharacterChoice(int ActorId, string Name)
    {
        // The shared dark ComboBox template presents Content directly.
        public override string ToString() => Name;
    }
}

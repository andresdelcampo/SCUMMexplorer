using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>Explicit admin inventory choices on one paused session. Closing discards only pending choices.</summary>
public partial class InventoryStatusWindow : Window
{
    private readonly ScummGameSession _session;
    private readonly Func<bool> _isCurrent;
    private readonly Action _refresh;
    private readonly bool _advanced;
    private readonly int _engineVersion;
    private readonly Func<string, string> _readable;
    private readonly Func<ScummInventoryCharacter, BitmapSource?>? _characterPicture;
    private ScummInventoryStatusSnapshot _snapshot = null!;
    private ScummInventoryUndoToken? _undo;
    private readonly Dictionary<int, int> _targets = [];
    private readonly Dictionary<int, BitmapSource?> _portraits = [];
    private readonly Dictionary<int, string> _portraitEvidence = [];
    private CharacterChoice[] _characters = [];

    public InventoryStatusWindow(ScummGameSession session, Func<bool> isCurrent, Action refresh,
        bool advanced = false, Func<string, string>? readable = null, int engineVersion = 2,
        Func<ScummInventoryCharacter, BitmapSource?>? characterPicture = null)
    {
        InitializeComponent();
        _session = session; _isCurrent = isCurrent; _refresh = refresh; _advanced = advanced;
        _engineVersion = engineVersion;
        _characterPicture = characterPicture;
        _readable = readable ?? (text => text);
        LoadSnapshot();
    }

    private string DisplayName(string name) => _readable(name).Replace("@", "", StringComparison.Ordinal).Trim();
    private string? CharacterName(int id) => _characters.FirstOrDefault(c => c.ActorId == id)?.Name;
    private DataTemplate CharacterTemplate => (DataTemplate)FindResource("CharacterChoiceTemplate");

    private void LoadSnapshot()
    {
        _snapshot = _session.CaptureInventoryStatus();
        _targets.Clear();
        _characters = _snapshot.Characters.Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new CharacterChoice(c.ActorId, DisplayName(c.Name!), Portrait(c))).ToArray();
        SessionSummary.Text = CharacterName(_snapshot.CurrentActorId) is { } current
            ? current + (_advanced ? $" · Room {_snapshot.RoomId:00}" : "") : "";
        GrantRecipient.ItemsSource = _characters;
        GrantRecipient.ItemTemplate = CharacterTemplate;
        GrantRecipient.SelectedValuePath = nameof(CharacterChoice.ActorId);
        GrantRecipient.SelectedValue = _snapshot.CurrentActorId;
        GrantRecipient.IsEnabled = _snapshot.CanEdit && _isCurrent();
        ItemSearch.IsEnabled = _snapshot.CanEdit && _isCurrent();
        RefreshCatalog(); RefreshRows();
        AdvancedEvidence.Visibility = _advanced ? Visibility.Visible : Visibility.Collapsed;
        AdvancedEvidence.Text = string.Join("\n", _snapshot.Characters.Select(c => c.Evidence).Distinct()) +
            "\n" + string.Join("\n", _portraitEvidence.Values) +
            "\n" + string.Join("\n", _session.InventoryCatalogNotes) +
            (_snapshot.Refusal is { } refusal ? "\n" + refusal : "") + "\n" +
            string.Join("\n", _session.SessionTrace.Where(line => line.StartsWith("Explicit inventory ", StringComparison.Ordinal)).TakeLast(5));
        UpdateButtons();
    }

    private void RefreshCatalog(bool advance = false)
    {
        var selected = (CatalogItems.SelectedItem as ItemChoice)?.ObjectId;
        var search = ItemSearch.Text.Trim();
        var items = _snapshot.Catalog.Where(item => _advanced || !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new ItemChoice(item.ObjectId, DisplayName(item.Name ?? $"Object {item.ObjectId}")))
            .Where(item => search.Length == 0 || item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        CatalogItems.ItemsSource = items;
        CatalogItems.SelectedItem = advance ? items.FirstOrDefault(item => !_targets.ContainsKey(item.ObjectId)) ?? items.FirstOrDefault()
            : items.FirstOrDefault(item => item.ObjectId == selected) ?? items.FirstOrDefault();
        CatalogItems.IsEnabled = _snapshot.CanEdit && _isCurrent();
        UpdateButtons();
    }

    private void RefreshRows()
    {
        InventoryRows.Children.Clear();
        var rows = _snapshot.Items.Select(item => (item.ObjectId, item.OwnerId, item.Name, Pending: false))
            .Concat(_snapshot.Catalog.Where(item => _targets.ContainsKey(item.ObjectId) && !_snapshot.Items.Any(held => held.ObjectId == item.ObjectId))
                .Select(item => (item.ObjectId, OwnerId: _targets[item.ObjectId], item.Name, Pending: true)));
        foreach (var item in rows)
        {
            if (!_advanced && string.IsNullOrWhiteSpace(item.Name)) continue;
            _targets.TryAdd(item.ObjectId, item.OwnerId);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 1), Background = new SolidColorBrush(Color.FromRgb(25, 38, 50)) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = DisplayName(item.Name ?? $"Object {item.ObjectId}"), Margin = new Thickness(10, 16, 8, 12), TextWrapping = TextWrapping.Wrap });
            var owner = new TextBlock { Text = item.Pending ? "Pending addition" : CharacterName(item.OwnerId) ?? (_advanced ? $"Actor {item.OwnerId}" : "Elsewhere"), Margin = new Thickness(10, 16, 8, 12) };
            Grid.SetColumn(owner, 1); row.Children.Add(owner);
            var choice = new ComboBox { ItemsSource = _characters, ItemTemplate = CharacterTemplate, SelectedValuePath = nameof(CharacterChoice.ActorId),
                SelectedValue = _targets[item.ObjectId], Tag = item.ObjectId, Margin = new Thickness(8),
                IsEnabled = _snapshot.CanEdit && _isCurrent() && !string.IsNullOrWhiteSpace(item.Name), MinWidth = 120, MaxDropDownHeight = 520 };
            choice.SelectionChanged += (_, _) =>
            {
                if (choice.SelectedValue is int target) _targets[item.ObjectId] = target;
                UpdateButtons();
            };
            if (_advanced) choice.ToolTip = $"Object {item.ObjectId}";
            Grid.SetColumn(choice, 2); row.Children.Add(choice); InventoryRows.Children.Add(row);
        }
        if (InventoryRows.Children.Count == 0)
            InventoryRows.Children.Add(new TextBlock { Text = "No items held by your characters. Choose an item above to add it.", Margin = new Thickness(12), Foreground = new SolidColorBrush(Color.FromRgb(198, 185, 159)) });
        UpdateButtons();
    }

    private bool HasChanges => _targets.Any(target => !_snapshot.Items.Any(item => item.ObjectId == target.Key && item.OwnerId == target.Value));
    private int PendingAdditions => _targets.Keys.Count(id => !_snapshot.Items.Any(item => item.ObjectId == id));
    private void UpdateButtons()
    {
        if (_snapshot is null) return;
        var editable = _isCurrent() && _snapshot.CanEdit;
        ApplyButton.IsEnabled = editable && HasChanges;
        UndoButton.IsEnabled = editable && _undo is not null;
        AddItemButton.IsEnabled = editable && CatalogItems.SelectedItem is ItemChoice selected && GrantRecipient.SelectedValue is int recipient &&
            (!_targets.TryGetValue(selected.ObjectId, out var current) ? PendingAdditions < _snapshot.FreeSlots : current != recipient);
        AddItemButton.Content = CatalogItems.SelectedItem is ItemChoice selectedItem && _snapshot.Items.Any(item => item.ObjectId == selectedItem.ObjectId)
            ? "Move item" : "Add item";
        CapacitySummary.Text = $"{_snapshot.Items.Count + PendingAdditions} of {_snapshot.Capacity} inventory slots used" +
            (PendingAdditions > 0 ? $" ({PendingAdditions} pending)" : "") +
            (PendingAdditions == _snapshot.FreeSlots ? ". Inventory full." : "");
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (!AddItemButton.IsEnabled || CatalogItems.SelectedItem is not ItemChoice item || GrantRecipient.SelectedValue is not int actor) return;
        _targets[item.ObjectId] = actor;
        RefreshRows(); RefreshCatalog(advance: true);
        ChoiceSummary.Text = "Item added to your pending choices. Apply Changes to keep it; Close discards pending choices.";
    }
    private void ItemSearch_TextChanged(object sender, TextChangedEventArgs e) { if (_snapshot is not null) RefreshCatalog(); }
    private void CatalogItems_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrent()) { Rejected("The live session changed."); return; }
        var changes = _targets.Where(target => !_snapshot.Items.Any(item => item.ObjectId == target.Key && item.OwnerId == target.Value))
            .Select(target => new ScummInventoryTransfer(target.Key, target.Value)).ToArray();
        var result = _session.ApplyInventoryEdits(_snapshot, changes);
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

    // Display-only actor copy: use this session's costume, animation and palette, never alter the live actor.
    private BitmapSource? Portrait(ScummInventoryCharacter character)
    {
        var actorId = character.ActorId;
        if (_portraits.TryGetValue(actorId, out var cached)) return cached;
        BitmapSource? picture = null;
        try
        {
            picture = _characterPicture?.Invoke(character);
            if (picture is not null)
            {
                _portraitEvidence[actorId] = $"Actor {actorId}: native selection picture; {character.Evidence}";
                _portraits[actorId] = picture;
                return picture;
            }
            if (_session.Actors.FirstOrDefault(actor => actor.Number == actorId) is { } actor)
            {
                _portraitEvidence[actorId] = $"Actor {actorId}: current native costume {actor.Look.Costume}, animation and palette; display crop only" +
                    (actor.Look.PaletteEstimate is { } paletteEstimate ? "; estimate: " + paletteEstimate : "") +
                    (actor.Look.Estimate is { } estimate ? "; " + estimate : "");
                var display = actor with { Room = 1, X = 96, Y = 224, Visible = true, WalkBox = null, Look = actor.Look with { Elevation = 0, Scale = 255 } };
                var layer = ScummActorRenderer.Render([display], 1, 256, 256, _session.ReadCostume, engineVersion: _engineVersion);
                var points = Enumerable.Range(0, layer.Pixels.Length).Where(layer.IsDrawn).ToArray();
                if (points.Length > 0)
                {
                    var left = points.Min(i => i % 256); var top = points.Min(i => i / 256);
                    var width = points.Max(i => i % 256) - left + 1; var height = points.Max(i => i / 256) - top + 1;
                    var pixels = new uint[width * height];
                    for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                    {
                        var source = (top + y) * 256 + left + x;
                        if (layer.IsDrawn(source)) pixels[y * width + x] = ScummPlayScreen.EgaPalette[layer.Pixels[source] & 15];
                    }
                    picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                    picture.Freeze();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        _portraits[actorId] = picture;
        return picture;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private sealed record CharacterChoice(int ActorId, string Name, BitmapSource? Portrait)
    { public override string ToString() => Name; }
    private sealed record ItemChoice(int ObjectId, string Name)
    { public override string ToString() => Name; }
}

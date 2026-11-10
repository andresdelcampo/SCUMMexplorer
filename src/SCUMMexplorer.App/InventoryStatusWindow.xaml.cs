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
    private ScummInventoryStatusSnapshot _snapshot = null!;
    private ScummInventoryUndoToken? _undo;
    private readonly Dictionary<int, int> _targets = [];
    private readonly Dictionary<int, BitmapSource?> _portraits = [];
    private readonly Dictionary<int, string> _portraitEvidence = [];
    private CharacterChoice[] _characters = [];
    internal ScummInventoryUndoToken? LastUndo => _undo;
    /// <summary>The game's values beyond the inventory (Indy 3: health, outfit, IQ points, story flags), or null.</summary>
    private readonly ScummStatusSchema? _schema;
    private int? _health;
    private int? _outfit;
    private readonly Dictionary<int, bool> _awards = [];
    private readonly Dictionary<string, bool> _flags = [];
    /// <summary>One playable character (Indy 3): no recipient choices.</summary>
    private bool SingleCharacter => _characters.Length == 1;

    public InventoryStatusWindow(ScummGameSession session, Func<bool> isCurrent, Action refresh,
        bool advanced = false, Func<string, string>? readable = null, int engineVersion = 2,
        ScummInventoryUndoToken? undo = null, ScummStatusSchema? schema = null)
    {
        InitializeComponent();
        _session = session; _isCurrent = isCurrent; _refresh = refresh; _advanced = advanced;
        _engineVersion = engineVersion;
        _undo = undo;
        _schema = schema is { IsEmpty: false } ? schema : null;
        _readable = readable ?? (text => text);
        LoadSnapshot();
        BuildStatusTabs();
    }

    // Glyphs the charset draws (a trademark sign, "IndyWear(tm) by Lucasfilm") are left out of the window's text.
    private string DisplayName(string name) => System.Text.RegularExpressions.Regex.Replace(_readable(name).Replace("@", "", StringComparison.Ordinal), @"\\x[0-9A-Fa-f]{2}|\p{Cc}", "").Trim();
    private string? CharacterName(int id) => _characters.FirstOrDefault(c => c.ActorId == id)?.Name;
    private Dictionary<int, string> ItemNames()
    {
        var names = new Dictionary<int, string>();
        foreach (var group in _snapshot.Catalog.GroupBy(item => DisplayName(item.Name ?? $"Object {item.ObjectId}"), StringComparer.CurrentCultureIgnoreCase))
        {
            var variants = group.OrderBy(item => item.ObjectId).ToArray();
            var qualified = variants.Select(item => variants.Length > 1 && item.Context?.Text is { } context &&
                !string.IsNullOrWhiteSpace(DisplayName(context)) ? $"{group.Key} ({DisplayName(context)})" : group.Key).ToArray();
            for (var index = 0; index < variants.Length; index++)
            {
                var label = qualified[index];
                var collisions = qualified.Count(name => string.Equals(name, label, StringComparison.CurrentCultureIgnoreCase));
                names[variants[index].ObjectId] = variants.Length == 1 || label != group.Key && collisions == 1
                    ? label : $"{label} ({index + 1})";
            }
        }
        return names;
    }
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
        if (SingleCharacter)
        {
            GiveLabel.Visibility = GrantRecipient.Visibility = MoveHeader.Visibility = Visibility.Collapsed;
            GiveColumn.Width = MoveColumn.Width = new GridLength(0);
            InventoryHeading.Text = $"Edit {_characters[0].Name}’s inventory";
            InventoryIntro.Text = "Admin choices: add items. These are not earned pickups or played scenes.";
            InventoryTab.Header = "Inventory";
        }
        ItemSearch.IsEnabled = _snapshot.CanEdit && _isCurrent();
        RefreshCatalog(); RefreshRows();
        AdvancedEvidence.Visibility = _advanced ? Visibility.Visible : Visibility.Collapsed;
        AdvancedEvidence.Text = string.Join("\n", _snapshot.Characters.Select(c => c.Evidence).Distinct()) +
            "\n" + string.Join("\n", _portraitEvidence.Values) +
            "\n" + string.Join("\n", _session.InventoryCatalogNotes) +
            "\n" + string.Join("\n", _snapshot.Catalog.Where(item => item.Context is not null).Select(item =>
                $"Object {item.ObjectId}: source association actor {item.Context!.ActorId}: {item.Context!.Evidence}")) +
            (_snapshot.Refusal is { } refusal ? "\n" + refusal : "") + "\n" +
            string.Join("\n", _session.SessionTrace.Where(line => line.StartsWith("Explicit inventory ", StringComparison.Ordinal)).TakeLast(5));
        UpdateButtons();
    }

    private void RefreshCatalog(bool advance = false)
    {
        var selected = (CatalogItems.SelectedItem as ItemChoice)?.ObjectId;
        var search = ItemSearch.Text.Trim();
        var names = ItemNames();
        var items = _snapshot.Catalog.Where(item => _advanced || !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new ItemChoice(item.ObjectId, names[item.ObjectId]))
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
        var names = ItemNames();
        var rows = _snapshot.Items.Select(item => (item.ObjectId, item.OwnerId, item.Name, Pending: false))
            .Concat(_snapshot.Catalog.Where(item => _targets.ContainsKey(item.ObjectId) && !_snapshot.Items.Any(held => held.ObjectId == item.ObjectId))
                .Select(item => (item.ObjectId, OwnerId: _targets[item.ObjectId], item.Name, Pending: true)));
        foreach (var item in rows)
        {
            if (!_advanced && string.IsNullOrWhiteSpace(item.Name)) continue;
            // One hero (Indy 3): what others hold (Henry's coins, the money set aside as owner 14) is not his inventory.
            if (SingleCharacter && !item.Pending && item.OwnerId != _characters[0].ActorId) continue;
            _targets.TryAdd(item.ObjectId, item.OwnerId);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 1), Background = new SolidColorBrush(Color.FromRgb(25, 38, 50)) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = names.GetValueOrDefault(item.ObjectId, DisplayName(item.Name ?? $"Object {item.ObjectId}")), Margin = new Thickness(10, 16, 8, 12), TextWrapping = TextWrapping.Wrap });
            var owner = new TextBlock { Text = item.Pending ? "Pending addition" : CharacterName(item.OwnerId) ?? (_advanced ? $"Actor {item.OwnerId}" : "Elsewhere"), Margin = new Thickness(10, 16, 8, 12) };
            Grid.SetColumn(owner, 1); row.Children.Add(owner);
            if (SingleCharacter) { InventoryRows.Children.Add(row); continue; }
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
            InventoryRows.Children.Add(new TextBlock { Text = SingleCharacter ? "No items held. Choose an item above to add it." : "No items held by your characters. Choose an item above to add it.", Margin = new Thickness(12), Foreground = new SolidColorBrush(Color.FromRgb(198, 185, 159)) });
        UpdateButtons();
    }

    private bool HasInventoryChanges => _targets.Any(target => !_snapshot.Items.Any(item => item.ObjectId == target.Key && item.OwnerId == target.Value));
    private bool HasChanges => HasInventoryChanges || StatusChoices() is not null;
    private int PendingAdditions => _targets.Keys.Count(id => !_snapshot.Items.Any(item => item.ObjectId == id));
    private void UpdateButtons()
    {
        if (_snapshot is null) return;
        var editable = _isCurrent() && _snapshot.CanEdit;
        ApplyButton.IsEnabled = editable || _schema is not null && _isCurrent() && _session.StatusEditRefusal() is null;
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
        ChoiceSummary.Text = "Item added to your pending choices. Apply and close keeps it; Cancel discards pending choices.";
    }
    private void ItemSearch_TextChanged(object sender, TextChangedEventArgs e) { if (_snapshot is not null) RefreshCatalog(); }
    private void CatalogItems_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrent()) { Rejected("The live session changed."); return; }
        if (!HasChanges) { Close(); return; }
        if (HasInventoryChanges)
        {
            var changes = _targets.Where(target => !_snapshot.Items.Any(item => item.ObjectId == target.Key && item.OwnerId == target.Value))
                .Select(target => new ScummInventoryTransfer(target.Key, target.Value)).ToArray();
            var result = _session.ApplyInventoryEdits(_snapshot, changes);
            if (!result.Applied) { Rejected(result.Refusal); return; }
            _undo = result.Undo;
        }
        // The values beyond the inventory (health, outfit, IQ points, story flags), after it: a granted outfit can then be worn.
        if (_schema is not null && StatusChoices() is { } choices && _session.ApplyStatusChoices(_schema, choices) is { } refusal)
        { _refresh(); Rejected(refusal); return; }
        _refresh();
        Close();
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
            if (_session.ReadInventoryCharacterAppearance(actorId, out var evidence) is { } actor)
            {
                _portraitEvidence[actorId] = $"Actor {actorId}: {evidence}; native full-body display copy" +
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

    /// <summary>The pending choices beyond the inventory, or null when none differs from the session.</summary>
    private ScummStatusChoices? StatusChoices()
    {
        if (_schema is null) return null;
        int? health = _health is int h && _schema.HealthVariable is { } hv && _session.StatusVariable(hv) != h ? h : null;
        int? outfit = _outfit is int o && _schema.OutfitVariable is { } ov && _session.StatusVariable(ov) != o ? o : null;
        var awards = _awards.Where(a => a.Value != _session.StatusAward(_schema, a.Key) > 0).ToDictionary(a => a.Key, a => a.Value);
        var flags = _flags.Where(f => f.Value != _session.StatusBit(f.Key)).ToDictionary(f => f.Key, f => f.Value);
        return health is null && outfit is null && awards.Count == 0 && flags.Count == 0 ? null : new(health, outfit, awards, flags);
    }

    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(198, 185, 159));

    /// <summary>
    /// Tabs for the game's values beyond the inventory (user, 9 November 2026: Indy 3's health, outfit, IQ points and story flags, "like we added for
    /// LOOM"), from <see cref="ScummStatusSchema"/>. Every choice waits for Apply and close.
    /// </summary>
    private void BuildStatusTabs()
    {
        if (_schema is not { } schema) return;
        var hero = _characters.FirstOrDefault(c => c.ActorId == _snapshot.CurrentActorId)?.Name ?? "Hero";
        var editable = _isCurrent() && _session.StatusEditRefusal() is null;
        if (schema.HealthVariable is not null || schema.OutfitVariable is not null)
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = $"{hero}’s state", FontSize = 17 });
            panel.Children.Add(new TextBlock { Text = "Admin choices, as the game keeps them. Nothing is played.", Foreground = Muted, Margin = new Thickness(0, 6, 0, 16), TextWrapping = TextWrapping.Wrap });
            if (schema.HealthVariable is { } healthVariable)
            {
                var now = _session.StatusVariable(healthVariable) ?? schema.HealthMax;
                var value = new TextBlock { Text = $"{now} of {schema.HealthMax}", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 90 };
                var slider = new Slider { Minimum = 0, Maximum = schema.HealthMax, Value = now, Width = 360, IsEnabled = editable, TickFrequency = schema.HealthMax / 10.0,
                    IsSnapToTickEnabled = false, SmallChange = 10, LargeChange = schema.HealthMax / 10.0 };
                System.Windows.Automation.AutomationProperties.SetName(slider, "Health");
                slider.ValueChanged += (_, _) => { _health = (int)Math.Round(slider.Value); value.Text = $"{_health} of {schema.HealthMax}"; UpdateButtons(); };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row.Children.Add(new TextBlock { Text = "Health", Width = 90, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(slider); row.Children.Add(value);
                panel.Children.Add(row);
                panel.Children.Add(new TextBlock { Text = "Fist fights take it down; at 0 the fight is lost.", Foreground = Muted, Margin = new Thickness(90, 0, 0, 16) });
                if (_advanced) panel.Children.Add(new TextBlock { Text = $"{healthVariable}: {schema.HealthWhere}", Foreground = Muted, FontSize = 11, Margin = new Thickness(90, -12, 0, 16) });
            }
            if (schema.OutfitVariable is { } outfitVariable)
            {
                var worn = _session.StatusVariable(outfitVariable);
                // The outfits the hero has: the one worn and those he holds (a held outfit can be put on; one not held is granted in Inventory first).
                var held = _snapshot.Items.Select(i => i.ObjectId).Concat(_targets.Keys).ToHashSet();
                var choices = schema.Outfits.Where(o => o.ObjectId == worn || held.Contains(o.ObjectId))
                    .Select(o => new ItemChoice(o.ObjectId, DisplayName(o.Name ?? $"Object {o.ObjectId}"))).ToArray();
                var box = new ComboBox { ItemsSource = choices, SelectedValuePath = nameof(ItemChoice.ObjectId), SelectedValue = worn, Width = 360, IsEnabled = editable && choices.Length > 1 };
                System.Windows.Automation.AutomationProperties.SetName(box, "Outfit");
                box.SelectionChanged += (_, _) => { _outfit = box.SelectedValue as int?; UpdateButtons(); };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row.Children.Add(new TextBlock { Text = "Wearing", Width = 90, VerticalAlignment = VerticalAlignment.Center });
                // No outfit worn or held yet (the game's start: his own clothes): nothing to choose until one is added in Inventory.
                if (choices.Length == 0)
                    row.Children.Add(new TextBlock { Text = $"{hero}’s own clothes: no outfit held yet.", VerticalAlignment = VerticalAlignment.Center });
                else row.Children.Add(box);
                panel.Children.Add(row);
                panel.Children.Add(new TextBlock { Text = "Guards remember the outfit they saw. To wear an outfit, it must be held (add it in Inventory).", Foreground = Muted, Margin = new Thickness(90, 0, 0, 16), TextWrapping = TextWrapping.Wrap });
                if (_advanced) panel.Children.Add(new TextBlock { Text = $"{outfitVariable}: {schema.OutfitWhere}", Foreground = Muted, FontSize = 11, Margin = new Thickness(90, -12, 0, 16) });
            }
            StatusTabs.Items.Add(new TabItem { Header = hero, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        }
        if (schema.Awards.Count > 0)
        {
            var total = new TextBlock { FontSize = 17 };
            void Total() => total.Text = $"IQ points: {schema.Awards.Sum(a => (_awards.TryGetValue(a.Slot, out var given) ? given : _session.StatusAward(schema, a.Slot) > 0) ? a.Points : 0)} of {schema.Awards.Sum(a => a.Points)}";
            Total();
            StatusTabs.Items.Add(new TabItem { Header = "IQ points", Content = CheckList(total,
                "The game awards IQ points for clever solutions. Checking one counts it as earned; nothing is played.",
                schema.Awards.Select(a => (Key: a.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture), Label: $"{a.Title} ({a.Points} points)",
                    Detail: $"slot {a.Slot}: {a.Where}", On: _session.StatusAward(schema, a.Slot) > 0)),
                (key, on) => { _awards[int.Parse(key, System.Globalization.CultureInfo.InvariantCulture)] = on; Total(); }, editable) });
        }
        if (schema.Flags.Count > 0)
            StatusTabs.Items.Add(new TabItem { Header = "Story", Content = CheckList(new TextBlock { Text = "Story: what the game remembers", FontSize = 17 },
                "Each mark is something that happened, named after the scene that sets it. Checking marks it as done, as the game would; nothing is played.",
                // A flag no scene names and the game's start has set (Indy 3's Script 118) is the game's setup, not story: left out.
                schema.Flags.Where(f => !(f.Unnamed && _session.StatusBit(f.Bit))).Select(f => (Key: f.Bit, Label: string.Join(" / ", f.Titles.Distinct()), Detail: $"{f.Bit}: {string.Join("; ", f.Where)}", On: _session.StatusBit(f.Bit)))
                    .OrderBy(f => f.Label.Contains("An unnamed scene", StringComparison.Ordinal) ? 1 : 0).ThenBy(f => f.Label, StringComparer.CurrentCultureIgnoreCase),
                (key, on) => _flags[key] = on, editable) });
    }

    private UIElement CheckList(TextBlock heading, string intro, IEnumerable<(string Key, string Label, string Detail, bool On)> rows, Action<string, bool> changed, bool editable)
    {
        var list = new StackPanel();
        foreach (var row in rows)
        {
            var box = new CheckBox { Content = new TextBlock { Text = DisplayName(row.Label), TextWrapping = TextWrapping.Wrap }, IsChecked = row.On, IsEnabled = editable,
                Margin = new Thickness(10, 5, 10, 5), Foreground = Foreground };
            if (_advanced) box.ToolTip = row.Detail;
            box.Checked += (_, _) => { changed(row.Key, true); UpdateButtons(); };
            box.Unchecked += (_, _) => { changed(row.Key, false); UpdateButtons(); };
            list.Children.Add(box);
        }
        var panel = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(heading);
        top.Children.Add(new TextBlock { Text = intro, Foreground = Muted, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);
        panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(21, 31, 41)) });
        return panel;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private sealed record CharacterChoice(int ActorId, string Name, BitmapSource? Portrait)
    { public override string ToString() => Name; }
    private sealed record ItemChoice(int ObjectId, string Name)
    { public override string ToString() => Name; }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private sealed record CostumeColorChoice(string Label, ScummV1CostumeColor Color, string Evidence)
    {
        public override string ToString() => Label;
    }
    private GameLibrary? _costumeColorGame;
    private GameResource? _costumeColorResource;
    private CancellationTokenSource? _costumeColorCancellation;
    private StackPanel? _costumeColorHeader;
    private ComboBox? _costumeColorPicker;
    private TextBlock? _costumeColorEvidence, _costumeColorProgress;
    private bool _updatingCostumeColors, _costumeColorsLoading;
    private int _costumeColorRequest;

    private ScummV1CostumeColor? PrepareCostumeColors(GameResource resource)
    {
        var game = _game;
        var colors = game is null ? null : ScummV1ActorColors.For(game);
        if (game is null || colors is not { SupportsContexts: true })
        {
            _costumeColorCancellation?.Cancel();
            _costumeColorRequest++;
            _costumeColorsLoading = false;
            _costumeColorGame = null;
            _costumeColorResource = null;
            if (_costumeColorHeader is not null) _costumeColorHeader.Visibility = Visibility.Collapsed;
            return colors?.ColorOf(resource.ResourceId ?? 0);
        }
        if (ReferenceEquals(_costumeColorGame, game) && ReferenceEquals(_costumeColorResource, resource) &&
            _costumeColorCancellation?.IsCancellationRequested == false)
        {
            UpdateCostumeColorVisibility();
            return (_costumeColorPicker?.SelectedItem as CostumeColorChoice)?.Color ?? colors.ColorOf(resource.ResourceId ?? 0);
        }

        _costumeColorCancellation?.Cancel();
        _costumeColorCancellation?.Dispose();
        var cancellation = _costumeColorCancellation = new CancellationTokenSource();
        _costumeColorGame = game;
        _costumeColorResource = resource;
        EnsureCostumeColorControls();
        var color = colors.ColorOf(resource.ResourceId ?? 0);
        _costumeColorsLoading = true;
        SetCostumeColorChoices([new("Default colours", color, $"{color.Basis}: {color.Evidence}")]);
        _ = LoadCostumeColorContextsAsync(game, resource, colors, color, ++_costumeColorRequest, cancellation.Token);
        return color;
    }

    private void EnsureCostumeColorControls()
    {
        if (_costumeColorHeader is not null) return;
        CostumePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        CostumePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        foreach (UIElement child in CostumePanel.Children) Grid.SetRow(child, 1);

        _costumeColorHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = "Colours", Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
        });
        _costumeColorPicker = new ComboBox
        {
            MinWidth = 210, MaxWidth = 420, MinHeight = 32,
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Automation.AutomationProperties.SetName(_costumeColorPicker, "Costume colours");
        _costumeColorPicker.SelectionChanged += (_, _) =>
        {
            if (_updatingCostumeColors || _costumeColorResource is not { } resource ||
                !ReferenceEquals(_game, _costumeColorGame) || !ReferenceEquals(_selectedResource, resource)) return;
            ShowCostume(resource);
        };
        row.Children.Add(_costumeColorPicker);
        _costumeColorProgress = new TextBlock
        {
            Text = "Reading colours…", Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
        };
        row.Children.Add(_costumeColorProgress);
        _costumeColorHeader.Children.Add(row);
        _costumeColorEvidence = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)), FontSize = 11,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 86, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed
        };
        _costumeColorHeader.Children.Add(_costumeColorEvidence);
        CostumePanel.Children.Add(_costumeColorHeader);
    }

    private async Task LoadCostumeColorContextsAsync(GameLibrary game, GameResource resource,
        ScummV1ActorColors colors, ScummV1CostumeColor defaultColor, int request, CancellationToken cancellation)
    {
        try
        {
            var contexts = await Task.Run(() => colors.ContextsOf(resource.ResourceId ?? 0, cancellation), cancellation);
            if (cancellation.IsCancellationRequested || request != _costumeColorRequest || !ReferenceEquals(_game, game) ||
                !ReferenceEquals(_selectedResource, resource) || !ReferenceEquals(_costumeColorResource, resource)) return;
            var choices = new List<CostumeColorChoice>();
            var same = contexts.Where(c => c.Skin == defaultColor.Skin && c.Suit == defaultColor.Suit).ToArray();
            static string Evidence(IEnumerable<ScummV1CostumeColorContext> source) => string.Join("\n", source.Select(c =>
                $"{c.Basis}{(c.IsEstimate ? " (estimate)" : "")}: {c.Evidence}").Distinct());
            choices.Add(new("Default colours", defaultColor,
                $"{defaultColor.Basis}: {defaultColor.Evidence}" + (same.Length > 0 ? "\n" + Evidence(same) : "")));
            foreach (var group in contexts.Where(c => c.Skin != defaultColor.Skin || c.Suit != defaultColor.Suit)
                         .GroupBy(c => (c.Skin, c.Suit)).OrderBy(g => g.Key.Skin).ThenBy(g => g.Key.Suit))
            {
                var evidence = Evidence(group);
                var names = group.Select(c => c.ActorName?.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
                var label = $"Colours {choices.Count + 1}";
                if (names.Length > 0) label = string.Join(" / ", names) + " · " + label;
                choices.Add(new(label, new(defaultColor.Costume, group.Key.Skin, group.Key.Suit,
                    group.All(c => c.IsEstimate) ? ScummV1ColorBasis.Estimate : ScummV1ColorBasis.Script, evidence, 0), evidence));
            }
            SetCostumeColorChoices(choices);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            if (!cancellation.IsCancellationRequested && request == _costumeColorRequest &&
                ReferenceEquals(_costumeColorResource, resource) && ReferenceEquals(_game, game))
                SetCostumeColorChoices([new("Default colours", defaultColor, $"{defaultColor.Basis}: {defaultColor.Evidence}\nColour contexts unavailable: {ex.Message}")]);
        }
        finally
        {
            if (request == _costumeColorRequest && ReferenceEquals(_costumeColorResource, resource) && ReferenceEquals(_game, game))
            {
                _costumeColorsLoading = false;
                UpdateCostumeColorVisibility();
            }
        }
    }

    private void SetCostumeColorChoices(IReadOnlyList<CostumeColorChoice> choices)
    {
        _updatingCostumeColors = true;
        try
        {
            _costumeColorPicker!.ItemsSource = choices;
            _costumeColorPicker.SelectedIndex = 0;
            _costumeColorPicker.IsEnabled = choices.Count > 1;
        }
        finally { _updatingCostumeColors = false; }
        UpdateCostumeColorVisibility();
    }

    private void UpdateCostumeColorVisibility()
    {
        if (_costumeColorHeader is null) return;
        var current = ReferenceEquals(_game, _costumeColorGame) && ReferenceEquals(_selectedResource, _costumeColorResource);
        if (!current) _costumeColorCancellation?.Cancel();
        var choice = _costumeColorPicker?.SelectedItem as CostumeColorChoice;
        _costumeColorHeader.Visibility = current && (_costumeColorsLoading || _costumeColorPicker!.Items.Count > 1 || IsAdvancedMode)
            ? Visibility.Visible : Visibility.Collapsed;
        _costumeColorEvidence!.Text = choice?.Evidence ?? "";
        _costumeColorEvidence.ToolTip = choice?.Evidence;
        _costumeColorEvidence.Visibility = current && IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        _costumeColorProgress!.Visibility = current && _costumeColorsLoading ? Visibility.Visible : Visibility.Collapsed;
        _costumeColorPicker!.IsEnabled = !_costumeColorsLoading && _costumeColorPicker.Items.Count > 1;
        _costumeColorPicker!.ToolTip = IsAdvancedMode ? choice?.Evidence : null;
    }
}

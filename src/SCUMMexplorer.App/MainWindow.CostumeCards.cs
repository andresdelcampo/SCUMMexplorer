using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private CancellationTokenSource? _costumeCardCancellation;
    private bool _showCostumeCards;

    /// <summary>The Costumes tab of the normal view: both LOOM games, and the v4 games that are only browsed.</summary>
    private static bool HasCostumeCards(GameLibrary? game) => game is { Id: GameId.Loom or GameId.LoomVga } || game?.V4Disks is not null || game?.Version == ScummVersion.V2;

    private void NormalCatalogTab_Click(object sender, RoutedEventArgs e)
    {
        _showCostumeCards = ReferenceEquals(sender, CostumesTab);
        UpdateNormalCatalog(AdvancedModeToggle.IsChecked == true);
    }

    private void UpdateNormalCatalog(bool advanced)
    {
        bool visible = _game is not null && !advanced;
        bool costumes = HasCostumeCards(_game);
        NormalCatalogTabs.Visibility = visible && costumes ? Visibility.Visible : Visibility.Collapsed;
        if (!costumes) _showCostumeCards = false;
        RoomsTab.IsChecked = !_showCostumeCards;
        CostumesTab.IsChecked = _showCostumeCards;
        RoomCardScroll.Visibility = visible && !_showCostumeCards ? Visibility.Visible : Visibility.Collapsed;
        CostumeCardScroll.Visibility = visible && _showCostumeCards ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PopulateCostumeCards(GameLibrary game)
    {
        _costumeCardCancellation?.Cancel();
        var cancellation = _costumeCardCancellation = new CancellationTokenSource();
        _showCostumeCards = false;
        CostumeCardList.Children.Clear();
        if (!HasCostumeCards(game)) return;
        var resources = game.Resources.Where(r => r.Kind == ResourceKind.Costume).ToArray();
        foreach (var resource in resources)
        {
            var card = new Button
            {
                Width = 96, Height = 94, Padding = new Thickness(4), Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(31, 45, 61)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 82, 96)), BorderThickness = new Thickness(2),
                Content = new TextBlock { Text = resource.Name, TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)), FontSize = 11 },
                Tag = resource, ToolTip = resource.Name + " · loading preview"
            };
            System.Windows.Automation.AutomationProperties.SetName(card, "Open " + resource.Name);
            card.Click += (_, _) => SelectResourceInTree(resource);
            CostumeCardList.Children.Add(card);
        }
        _ = LoadCostumeCardThumbnailsAsync(game, resources, cancellation.Token);
    }

    private async Task LoadCostumeCardThumbnailsAsync(GameLibrary game, GameResource[] resources, CancellationToken token)
    {
        // A source pose with the most visible pixels gives small thumbnails a complete silhouette.
        // This is a static gallery choice, never an animation or actor-state assumption.
        var containers = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in resources)
        {
            if (token.IsCancellationRequested) return;
            BitmapSource? bitmap = null;
            string detail;
            bool noData = false;
            try
            {
                (bitmap, detail) = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (game.V4ArchiveFor(resource) is { } vga)
                    {
                        // The CD game (or a browsed v4 game's disk): the stored pose with the most visible pixels, in the palette of the room that holds the costume.
                        var costume = vga.FindCostume(resource.ResourceId!.Value) ?? throw new InvalidDataException("No costume block.");
                        var poses = vga.DecodeCostumePoses(costume);
                        if (poses.Count == 0) throw new InvalidDataException("No decoded pose.");
                        var cdBest = poses.MaxBy(p => p.Image.Pixels.Span.ToArray().Count(b => (p.Image.Palette[b] >> 24) != 0));
                        return (CreateCostumeBitmap(cdBest.Image.Width, cdBest.Image.Height, cdBest.Image.Pixels.ToArray(), cdBest.Image.Palette),
                            $"{resource.Name} · stored pose {cdBest.Pose:00}");
                    }
                    if (resource.Offset is not int offset) throw new NotSupportedException();
                    if (!containers.TryGetValue(resource.FilePath, out var container))
                        containers[resource.FilePath] = container = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
                    var reader = new ScummV3CostumeReader(); var renderer = new ScummCostumeRenderer();
                    var slots = reader.Decode(container, offset).AnimationSlots.Where(s => s.HasData);
                    RoomImage? best = null; int bestPixels = 0, bestSlot = 0;
                    foreach (var slot in slots)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            var pose = renderer.DecodePose(container, offset, slot.Index);
                            if (pose is null) continue;
                            int pixels = 0;
                            foreach (byte p in pose.Pixels.Span)
                                if ((pose.Palette[p] >> 24) != 0) pixels++;
                            if (pixels <= bestPixels) continue;
                            best = pose; bestPixels = pixels; bestSlot = slot.Index;
                        }
                        catch (InvalidDataException) { }
                    }
                    if (best is null) throw new InvalidDataException("No decoded pose.");
                    return (CreateCostumeBitmap(best.Width, best.Height, best.Pixels.ToArray(), best.Palette),
                        $"{resource.Name} · stored pose {bestSlot:00}");
                }, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                detail = resource.Name + " · picture unavailable";
                // The index entry points at no costume (missing file, offset past the end, not a header): see
                // .verification/archive/costume-leftovers-20261003. Normal mode hides such cards; Advanced lists them in the tree.
                noData = ex is InvalidDataException or FileNotFoundException;
            }
            if (token.IsCancellationRequested || !ReferenceEquals(_game, game)) return;
            var card = CostumeCardList.Children.OfType<Button>().Single(c => Equals(c.Tag, resource));
            if (noData) { CostumeCardList.Children.Remove(card); continue; }
            if (bitmap is not null)
            {
                var content = new StackPanel();
                var image = new Image { Source = bitmap, Width = 80, Height = 62, Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                content.Children.Add(image);
                content.Children.Add(new TextBlock { Text = resource.ResourceId?.ToString("000"), FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 186)) });
                card.Content = content;
            }
            card.ToolTip = detail;
        }
    }
}

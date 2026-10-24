using DisplayMode = SCUMMexplorer.Core.Scumm.RoomOverlayMode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// R-16.9 (user, 24 Oct 2026: "hotspots and doorways visibility should not be game specific -including implementation as much as possible"):
/// the one drawing of doorway and hotspot boxes and the one construction of the Advanced hotspot and doorway rows. LOOM's room picture, the native
/// game screen and the browsed rooms are only <see cref="RoomSurface"/>s (where a room point lands); a game or room is only an
/// <see cref="IRoomHotspotProvider"/>. LOOM's look is the reference.
/// </summary>
public partial class MainWindow
{
    /// <summary>Where room points land on a canvas, and the horizontal span a label must stay inside.</summary>
    private sealed record RoomSurface(Canvas Canvas, double Scale, Func<double, double, Point> Map, double Left, double Right);

    private static readonly Color DoorwayLive = Color.FromRgb(255, 207, 105);
    private static readonly Color DoorwayDim = Color.FromRgb(190, 176, 150);

    private static void DrawDoorwayBoxes(RoomSurface surface, IEnumerable<RoomDoorwayBox> boxes)
    {
        var scale = surface.Scale;
        foreach (var box in boxes)
        {
            var accent = box.Live ? DoorwayLive : DoorwayDim;
            var at = surface.Map(box.X, box.Y);
            var bounds = new Border
            {
                Width = box.Width * scale,
                Height = box.Height * scale,
                BorderThickness = new Thickness(Math.Clamp(scale, 1, 3)),
                BorderBrush = new SolidColorBrush(accent),
                Background = new SolidColorBrush(Color.FromArgb((byte)(box.Live ? 48 : 24), 247, 187, 72)),
                CornerRadius = new CornerRadius(2),
                ToolTip = box.Tip
            };
            Canvas.SetLeft(bounds, at.X);
            Canvas.SetTop(bounds, at.Y);
            surface.Canvas.Children.Add(bounds);

            var label = new Border
            {
                Child = new TextBlock
                {
                    Text = box.Prefix + "Room " + string.Join(" / ", box.Targets.Select(id => id.ToString("00"))),
                    FontStyle = box.Live ? FontStyles.Normal : FontStyles.Italic,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = Math.Clamp(scale * 6, 8, 13),
                    Margin = new Thickness(4, 2, 4, 2)
                },
                Background = new SolidColorBrush(Color.FromArgb(224, 17, 24, 32)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                ToolTip = box.Tip
            };
            // Keep the label inside the room image, even for a narrow box at its edge.
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Max(surface.Left, Math.Min(at.X + 2, surface.Right - label.DesiredSize.Width - 2)));
            Canvas.SetTop(label, at.Y + 2);
            surface.Canvas.Children.Add(label);
        }
    }

    /// <summary>A hotspot's box: strong for the one under the pointer or chosen in the list, faint for the others under `Always`.</summary>
    private static void DrawHotspotBox(RoomSurface surface, RoomHotspot hotspot, bool strong)
    {
        var scale = surface.Scale;
        var at = surface.Map(hotspot.X, hotspot.Y);
        var box = strong
            ? new Border
            {
                Width = Math.Max(1, hotspot.Width * scale), Height = Math.Max(1, hotspot.Height * scale),
                BorderThickness = new Thickness(Math.Clamp(scale, 2, 3)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 220, 255)),
                Background = new SolidColorBrush(Color.FromArgb(56, 120, 220, 255)),
                CornerRadius = new CornerRadius(2)
            }
            : new Border
            {
                Width = Math.Max(1, hotspot.Width * scale), Height = Math.Max(1, hotspot.Height * scale),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(150, 120, 220, 255)),
                Background = new SolidColorBrush(Color.FromArgb(22, 120, 220, 255)), CornerRadius = new CornerRadius(2)
            };
        Canvas.SetLeft(box, at.X);
        Canvas.SetTop(box, at.Y);
        surface.Canvas.Children.Add(box);
    }

    /// <summary>The Advanced `HOTSPOTS` rows (LOOM's): the stored name, else `Hotspot N`, a name used twice carrying its index; hovering a row outlines its box.</summary>
    private void FillHotspotRows(IRoomHotspotProvider provider)
    {
        var hotspots = provider.Hotspots().Where(item => !item.IsDoorway && provider.HasActions(item)).ToArray();
        var repeated = RoomHotspotRules.RepeatedLabels(hotspots);
        foreach (var hotspot in hotspots)
        {
            var button = new Button
            {
                Content = RoomControlText.HotspotLabel(hotspot.Name, hotspot.LocalIndex, repeated.Contains(hotspot.Name ?? $"Hotspot {hotspot.LocalIndex}")),
                ToolTip = RoomControlText.HotspotTip(hotspot.LocalIndex, hotspot.ObjectId, hotspot.Name, hotspot.X, hotspot.Y, hotspot.Width, hotspot.Height),
                Tag = hotspot.ObjectId,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(8, 3, 8, 3),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = Brushes.Beige,
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) => provider.Choose(hotspot);
            // Hovering or focusing a row outlines its box on the room.
            button.MouseEnter += (_, _) => provider.Highlight(hotspot);
            button.GotKeyboardFocus += (_, _) => provider.Highlight(hotspot);
            button.MouseLeave += (_, _) => { if (!button.IsKeyboardFocused) provider.Highlight(null); };
            button.LostKeyboardFocus += (_, _) => { if (!button.IsMouseOver) provider.Highlight(null); };
            ObjectList.Children.Add(button);
        }
    }

    /// <summary>The Advanced `DOORWAYS` rows: one button per possible destination room.</summary>
    private void FillDoorwayRows(IEnumerable<RoomDoorwayRow> rows)
    {
        foreach (var row in rows)
        {
            var button = new Button
            {
                Content = row.Label,
                ToolTip = row.Tip,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 5, 5),
                Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 224, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
            };
            button.Click += (_, _) => row.Open();
            RoomNavigationButtons.Children.Add(button);
        }
        RoomNavigationHeader.Text = $"DOORWAYS · {RoomNavigationButtons.Children.Count}";
    }

    private static readonly SolidColorBrush HotspotRowBrush = new(Color.FromRgb(39, 54, 66));
    private static readonly SolidColorBrush HoveredHotspotRowBrush = new(Color.FromRgb(46, 92, 116));
    private int? _hoveredHotspotRowId;

    /// <summary>The row of the hotspot under the room pointer is lit and scrolled into view (every game).</summary>
    private void LightHotspotRow(int? objectId)
    {
        if (_hoveredHotspotRowId == objectId) return;
        _hoveredHotspotRowId = objectId;
        foreach (var button in ObjectList.Children.OfType<Button>())
        {
            if (button.Tag is not int id) continue;
            var hovered = id == objectId;
            button.Background = hovered ? HoveredHotspotRowBrush : HotspotRowBrush;
            if (hovered) button.BringIntoView();
        }
    }
}

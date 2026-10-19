using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>Viewing aids for the native game screen; none intercept input or change game state.</summary>
public partial class MainWindow
{
    private readonly Canvas _playOverlayCanvas = new() { IsHitTestVisible = false, ClipToBounds = true, Visibility = Visibility.Collapsed };
    private GameLibrary? _playOverlayGame;
    private int? _playOverlayRoom;
    private Dictionary<int, int[]> _playDoorwayTargets = [];
    private (int X, int Y)? _playOverlayPointer;
    private ScummRoomScene? _playOverlayScene;
    private Dictionary<int, string> _playOverlayNames = [];
    private string? _playOverlayListKey;

    private void ClearPlayOverlays()
    {
        _playOverlayCanvas.Children.Clear();
        _playOverlayCanvas.Visibility = Visibility.Collapsed;
        _playOverlayPointer = null;
        _playOverlayScene = null;
        _playOverlayListKey = null;
    }

    private void UpdatePlayOverlayHover((int X, int Y)? point)
    {
        if (_playOverlayPointer == point) return;
        _playOverlayPointer = point;
        if (ObjectAreaMode == DisplayMode.Hover || DoorwayMode == DisplayMode.Hover) RefreshPlayOverlays();
    }

    private void RefreshPlayOverlays() => RefreshPlayOverlays(_playShown);

    private void RefreshPlayOverlays(ScummPlayFrame? frame)
    {
        if (!PlayActive || _playScreen is null || _game is null || frame?.RoomId is not int roomId)
        {
            ClearPlayOverlays();
            return;
        }
        var changed = !ReferenceEquals(_playOverlayGame, _game) || _playOverlayRoom != roomId;
        if (changed)
        {
            _playOverlayGame = _game;
            _playOverlayRoom = roomId;
            _playOverlayPointer = null;
            _playDoorwayTargets = ReadPlayDoorwayTargets(_game, roomId);
        }
        if (_playOverlayCanvas.Parent is null)
        {
            RoomViewportHost.Children.Add(_playOverlayCanvas);
            Panel.SetZIndex(_playOverlayCanvas, 1);
            _playOverlayCanvas.SizeChanged += (_, _) => RefreshPlayOverlays();
        }
        var wanted = ObjectAreaMode != DisplayMode.Never || DoorwayMode != DisplayMode.Never ||
            IsAdvancedMode && (RoomMaskToggle.IsChecked == true || WalkBoxToggle.IsChecked == true);
        // Decoded backgrounds/objects are cached by ScummPlayScreen. Capture mutable source state only when an aid is visible.
        if (changed || wanted || IsAdvancedMode || _playOverlayScene is null) _playOverlayScene = _playScreen.Scene();
        if (_playOverlayScene is not { } scene || scene.RoomId != roomId) { ClearPlayOverlays(); return; }

        ObjectAreaOptions.Visibility = Visibility.Visible;
        DoorwayOptions.Visibility = _playDoorwayTargets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RoomMaskToggle.Visibility = WalkBoxToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        RoomMaskToggle.IsEnabled = scene.Room.DepthMask is not null || scene.Room.ExtraDepthMasks.Count > 0;
        RoomMaskToggle.ToolTip = RoomMaskToggle.IsEnabled ? "Highlight the displayed room and active objects' native depth areas." : "No depth areas are stored in this room.";
        WalkBoxToggle.IsEnabled = scene.State.WalkBoxes is { Count: > 0 };
        WalkBoxToggle.ToolTip = WalkBoxToggle.IsEnabled ? "The current room's native walking areas; disabled boxes are dashed." : "No walkboxes are stored in this room.";

        _playOverlayCanvas.Children.Clear();
        _playOverlayCanvas.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;
        if (!wanted || _playOverlayCanvas.ActualWidth <= 0 || _playOverlayCanvas.ActualHeight <= 0) return;
        var scale = Math.Min(_playOverlayCanvas.ActualWidth / ScummPlayFrame.Width, _playOverlayCanvas.ActualHeight / ScummPlayFrame.Height);
        var offsetX = (_playOverlayCanvas.ActualWidth - ScummPlayFrame.Width * scale) / 2;
        var offsetY = (_playOverlayCanvas.ActualHeight - ScummPlayFrame.Height * scale) / 2;
        var viewport = new ScummRoomViewport(frame.ViewLeft, frame.RoomTop, frame.RoomBottom);
        _playOverlayCanvas.Clip = new RectangleGeometry(new Rect(offsetX, offsetY + frame.RoomTop * scale,
            ScummPlayFrame.Width * scale, Math.Max(0, frame.RoomBottom - frame.RoomTop) * scale));
        Point Map(double x, double y) => new(offsetX + (x - frame.ViewLeft) * scale, offsetY + (y + frame.RoomTop) * scale);

        if (IsAdvancedMode && RoomMaskToggle.IsChecked == true && RoomMaskToggle.IsEnabled)
        {
            var tint = BitmapSource.Create(ScummPlayFrame.Width, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null,
                ScummRoomOverlays.DepthPixels(scene, viewport), ScummPlayFrame.Width * 4);
            var image = new Image { Source = tint, Width = ScummPlayFrame.Width * scale, Height = ScummPlayFrame.Height * scale };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(image, offsetX); Canvas.SetTop(image, offsetY);
            _playOverlayCanvas.Children.Add(image);
        }
        if (IsAdvancedMode && WalkBoxToggle.IsChecked == true && scene.State.WalkBoxes is { } boxes)
            foreach (var box in boxes.Boxes)
            {
                if (box.Corners.Any(point => point.X < -1000 || point.Y < -1000)) continue;
                var disabled = (boxes.GetFlags(box.Index) & ScummWalkBox.InvisibleFlag) != 0;
                var color = disabled ? Colors.Gray : Color.FromRgb(110, 235, 140);
                var polygon = new System.Windows.Shapes.Polygon
                {
                    Points = new PointCollection(box.Corners.Select(point => Map(point.X + 0.5, point.Y + 0.5))),
                    Stroke = new SolidColorBrush(color), StrokeThickness = Math.Clamp(scale * 0.75, 1.5, 3),
                    Fill = box.IsLine ? null : new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B))
                };
                if (disabled) polygon.StrokeDashArray = [3, 2];
                _playOverlayCanvas.Children.Add(polygon);
                Label(box.Index.ToString(), Map(box.Corners.Average(point => point.X), box.Corners.Average(point => point.Y)), color);
            }
        var pointInRoom = _playOverlayPointer is { } pointer ? viewport.ToRoom(pointer.X, pointer.Y) : null;
        foreach (var obj in ScummRoomOverlays.Objects(scene, _playDoorwayTargets))
        {
            var mode = obj.IsDoorway ? DoorwayMode : ObjectAreaMode;
            if (mode == DisplayMode.Never || mode == DisplayMode.Hover &&
                (pointInRoom is not { } hover || !obj.Contains(hover.X, hover.Y))) continue;
            var at = Map(obj.X, obj.Y);
            var color = obj.IsDoorway ? Color.FromRgb(255, 207, 105) : Color.FromRgb(120, 220, 255);
            var bounds = new Border
            {
                Width = Math.Max(1, obj.Width * scale), Height = Math.Max(1, obj.Height * scale),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(Math.Clamp(scale, 1, 3)),
                Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)), CornerRadius = new CornerRadius(2)
            };
            Canvas.SetLeft(bounds, at.X); Canvas.SetTop(bounds, at.Y);
            _playOverlayCanvas.Children.Add(bounds);
            if (obj.IsDoorway) Label("Room " + string.Join(" / ", obj.Destinations.Select(id => id.ToString("00"))), at, color);
        }

        void Label(string text, Point at, Color color)
        {
            var label = new Border
            {
                Child = new TextBlock { Text = text, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
                    FontSize = Math.Clamp(scale * 5, 8, 13), Margin = new Thickness(3, 1, 3, 1) },
                Background = new SolidColorBrush(Color.FromArgb(224, 17, 24, 32)), BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1)
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Clamp(at.X + 2, offsetX, Math.Max(offsetX, offsetX + 320 * scale - label.DesiredSize.Width)));
            Canvas.SetTop(label, at.Y + 2);
            _playOverlayCanvas.Children.Add(label);
        }
    }

    private void RefreshPlayOverlayLists(Action<int> interact)
    {
        if (!PlayActive || !IsAdvancedMode || _playOverlayScene is not { } scene)
        {
            _playOverlayListKey = null;
            return;
        }
        var objects = ScummRoomOverlays.Objects(scene, _playDoorwayTargets);
        var key = _game!.Key + ":" + scene.RoomId + ":" + string.Join(',', objects.Select(obj => $"{obj.ObjectId}@{obj.X},{obj.Y}"));
        if (_playOverlayListKey == key) { DisplayLists(); return; }
        _playOverlayListKey = key;
        RoomNavigationButtons.Children.Clear(); ObjectList.Children.Clear();
        foreach (var obj in objects)
        {
            var name = _playOverlayNames.GetValueOrDefault(obj.ObjectId, $"Object {obj.ObjectId}");
            if (obj.IsDoorway)
                foreach (var destination in obj.Destinations)
                {
                    var room = _game.Rooms.FirstOrDefault(item => item.ResourceId == destination);
                    if (room is null) continue;
                    var button = Item($"{name} → Room {destination:00}");
                    button.ToolTip = $"Possible destination decoded from Room {scene.RoomId:00} object {obj.ObjectId}. Opens directly for exploration; source conditions are not promised.";
                    button.Click += (_, _) => SelectResourceInTree(room);
                    RoomNavigationButtons.Children.Add(button);
                }
            else
            {
                var button = Item(name);
                button.Tag = obj.ObjectId;
                button.ToolTip = $"Room {scene.RoomId:00} object {obj.ObjectId}: choose an exploration interaction.";
                button.Click += (_, _) => interact(obj.ObjectId);
                button.MouseEnter += (_, _) => { _playOverlayPointer = (obj.X + obj.Width / 2 - _playShown!.ViewLeft, obj.Y + obj.Height / 2 + _playShown.RoomTop); RefreshPlayOverlays(); };
                button.MouseLeave += (_, _) => UpdatePlayOverlayHover(null);
                ObjectList.Children.Add(button);
            }
        }
        DisplayLists();

        void DisplayLists()
        {
            RoomNavigationHeader.Text = $"DOORWAYS · {RoomNavigationButtons.Children.Count}";
            RoomNavigationPanel.Visibility = RoomNavigationButtons.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowObjectListSection("HOTSPOTS");
        }

        static Button Item(string text) => new()
        {
            Content = text, HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 5, 4),
            Background = new SolidColorBrush(Color.FromRgb(39, 54, 66)), Foreground = Brushes.Beige,
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98))
        };
    }

    private Dictionary<int, int[]> ReadPlayDoorwayTargets(GameLibrary game, int roomId)
    {
        var resource = game.Rooms.FirstOrDefault(room => room.ResourceId == roomId);
        if (resource is null) return [];
        var found = new Dictionary<int, int[]>();
        _playOverlayNames = [];
        try
        {
            // Background preparation has its own seekable readers. Never scan an active archive handle concurrently.
            if (game.V4Archive is not null || game.V4Disks is not null) game = GameFolderScanner.Reopen(game);
            var data = ScummDataSources.For(game);
            var room = data.ReadRoom(roomId);
            _playOverlayNames = room.ObjectNames.ToDictionary(pair => pair.Key, pair => ScummTextDecoder.DecodeGlyphs(pair.Value).Trim().TrimEnd('@').Trim())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToDictionary();
            var records = room.Objects;
            var verbs = data.ReadObjectVerbs(roomId, records);
            var finder = new ScummDoorwayFinder(game, data);
            foreach (var record in records)
            {
                if (!verbs.TryGetValue(record.Id, out var entries)) continue;
                var targets = new HashSet<int>();
                foreach (var verb in entries)
                    try
                    {
                        var script = data.ReadObjectVerb(roomId, record, verb.VerbId, out _);
                        targets.UnionWith(finder.Find(resource, script).Select(target => target.RoomId));
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { }
                if (targets.Count > 0) found[record.Id] = targets.Order().ToArray();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { }
        return found;
    }
}

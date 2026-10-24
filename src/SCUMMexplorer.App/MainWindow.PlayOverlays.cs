using DisplayMode = SCUMMexplorer.Core.Scumm.RoomOverlayMode;
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
    private int? _playHighlightId;
    private ScummRoomScene? _playOverlayScene;
    private Dictionary<int, string> _playOverlayNames = [];
    private Dictionary<int, int> _playOverlayLocalIndex = [];
    private string? _playOverlayListKey;
    private UIElement[] _playOverlayObjectRows = [];
    private UIElement[] _playOverlayDoorwayRows = [];

    private void ClearPlayOverlays()
    {
        _playOverlayCanvas.Children.Clear();
        _playOverlayCanvas.Visibility = Visibility.Collapsed;
        _playOverlayPointer = null;
        _playOverlayHoverKey = null;
        _playOverlayHotspots = [];
        _playHighlightId = null;
        _playOverlayScene = null;
        _playOverlayListKey = null;
        _playOverlayObjectRows = [];
        _playOverlayDoorwayRows = [];
    }

    private void UpdatePlayOverlayHover((int X, int Y)? point)
    {
        if (_playOverlayPointer == point) return;
        _playOverlayPointer = point;
        if (IsAdvancedMode) LightHotspotRow(PlayHotspotAt(point)?.ObjectId);
        // R-6.48: a pointer move redraws only when what it points at changed (the hotspot under it, the doorways under it); the table is the one the last
        // redraw (every game frame) built, so a move does no scene capture, no hotspot derivation and no control writes.
        if (ObjectAreaMode != DisplayMode.Hover && DoorwayMode != DisplayMode.Hover) return;
        var key = PlayHoverKey(point);
        if (key == _playOverlayHoverKey) return;
        _playOverlayHoverKey = key;
        RefreshPlayOverlays();
    }

    private string? _playOverlayHoverKey;
    private IReadOnlyList<RoomHotspot> _playOverlayHotspots = [];

    /// <summary>What the pointer points at in the cached hotspot table: the strong box's object and the doorway objects under it ("" for neither).</summary>
    private string PlayHoverKey((int X, int Y)? point)
    {
        if (point is not { } p || _playShown is not { } frame ||
            new ScummRoomViewport(frame.ViewLeft, frame.RoomTop, frame.RoomBottom).ToRoom(p.X, p.Y) is not { } at) return "";
        return RoomHotspotRules.HoverKey(_playOverlayHotspots, at.X, at.Y, _playHighlightId is null && ObjectAreaMode != DisplayMode.Never, DoorwayMode == DisplayMode.Hover);
    }

    /// <summary>Whether a present object of the room (a hotspot or a doorway) is under a native screen point.</summary>
    private bool PlayObjectAt((int X, int Y)? point)
    {
        if (point is not { } p || _playShown is not { } frame) return false;
        if (new ScummRoomViewport(frame.ViewLeft, frame.RoomTop, frame.RoomBottom).ToRoom(p.X, p.Y) is not { } at) return false;
        // The scene as the game leaves it now (the overlays' copy is refreshed only while an aid is visible).
        return _playScreen?.Scene() is { } scene && ScummRoomOverlays.Objects(scene, _playDoorwayTargets).Any(item => item.Contains(at.X, at.Y));
    }

    /// <summary>The listed hotspot under a native screen point, as the game's search would pick it (the same pick as LOOM's room picture).</summary>
    private RoomHotspot? PlayHotspotAt((int X, int Y)? point)
    {
        if (point is not { } p || _playShown is not { } frame) return null;
        if (new ScummRoomViewport(frame.ViewLeft, frame.RoomTop, frame.RoomBottom).ToRoom(p.X, p.Y) is not { } at) return null;
        var provider = PlayHotspots;
        return RoomHotspotRules.PointedAt(provider.Hotspots().Where(item => !item.IsDoorway && provider.HasActions(item)), at.X, at.Y);
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
            _playOverlayHoverKey = null;
            _playDoorwayTargets = ReadPlayDoorwayTargets(_game, roomId);
        }
        if (_playOverlayCanvas.Parent is null)
        {
            RoomViewportHost.Children.Add(_playOverlayCanvas);
            Panel.SetZIndex(_playOverlayCanvas, 1);
            _playOverlayCanvas.SizeChanged += (_, _) => RefreshPlayOverlays();
        }
        var wanted = ObjectAreaMode != DisplayMode.Never || DoorwayMode != DisplayMode.Never || _playHighlightId is not null ||
            IsAdvancedMode && (RoomMaskToggle.IsChecked == true || WalkBoxToggle.IsChecked == true);
        // Decoded backgrounds/objects are cached by ScummPlayScreen. Capture mutable source state only when an aid is visible.
        if (changed || wanted || IsAdvancedMode || _playOverlayScene is null) _playOverlayScene = _playScreen.Scene();
        if (_playOverlayScene is not { } scene || scene.RoomId != roomId) { ClearPlayOverlays(); return; }

        ObjectAreaOptions.Visibility = RoomOverlayRules.HotspotOptionOffered() ? Visibility.Visible : Visibility.Collapsed;
        DoorwayOptions.Visibility = RoomOverlayRules.DoorwayOptionOffered(_playDoorwayTargets.Count) ? Visibility.Visible : Visibility.Collapsed;
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
        var surface = new RoomSurface(_playOverlayCanvas, scale, Map, offsetX, offsetX + ScummPlayFrame.Width * scale);
        var pointInRoom = _playOverlayPointer is { } pointer ? viewport.ToRoom(pointer.X, pointer.Y) : null;
        var hotspots = PlayHotspots.Hotspots();
        _playOverlayHotspots = hotspots;
        // The same drawing as LOOM's room picture: doorway boxes by their option; hotspot boxes faint under `Always`, strong for the one pointed at or chosen in the list.
        DrawDoorwayBoxes(surface, RoomHotspotRules.DoorwayBoxes(hotspots,
            group => RoomOverlayRules.Shows(DoorwayMode, pointInRoom is { } hover && group.Any(item => item.Contains(hover.X, hover.Y))),
            item => PlayHotspots.DoorwayTip(item), _ => true));
        var objects = hotspots.Where(item => !item.IsDoorway).ToArray();
        var pointed = _playHighlightId is int chosen ? objects.FirstOrDefault(item => item.ObjectId == chosen)
            : ObjectAreaMode != DisplayMode.Never && pointInRoom is { } at ? RoomHotspotRules.PointedAt(objects, at.X, at.Y) : null;
        if (ObjectAreaMode == DisplayMode.Always)
            foreach (var item in objects)
                if (item.ObjectId != pointed?.ObjectId) DrawHotspotBox(surface, item, false);
        if (pointed is not null) DrawHotspotBox(surface, pointed, true);

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

    private void RefreshPlayOverlayLists()
    {
        if (!PlayActive || !IsAdvancedMode || _playOverlayScene is not { } scene)
        {
            _playOverlayListKey = null;
            return;
        }
        var provider = PlayHotspots;
        var hotspots = provider.Hotspots();
        var key = _game!.Key + ":" + scene.RoomId + ":" + string.Join(',', hotspots.Select(item => $"{item.ObjectId}@{item.X},{item.Y}:{(provider.HasActions(item) ? 1 : 0)}"));
        // Preview controls share these panels and can replace their children without changing
        // native room state. Only reuse rows that are still the ones this view published.
        if (_playOverlayListKey == key &&
            ObjectList.Children.Cast<UIElement>().SequenceEqual(_playOverlayObjectRows) &&
            RoomNavigationButtons.Children.Cast<UIElement>().SequenceEqual(_playOverlayDoorwayRows))
        { DisplayLists(); return; }
        _playOverlayListKey = key;
        RoomNavigationButtons.Children.Clear(); ObjectList.Children.Clear();
        _hoveredHotspotRowId = null;
        FillHotspotRows(provider);
        FillDoorwayRows(provider.DoorwayRows());
        _playOverlayObjectRows = ObjectList.Children.Cast<UIElement>().ToArray();
        _playOverlayDoorwayRows = RoomNavigationButtons.Children.Cast<UIElement>().ToArray();
        DisplayLists();

        void DisplayLists()
        {
            RoomNavigationHeader.Text = $"DOORWAYS · {RoomNavigationButtons.Children.Count}";
            RoomNavigationPanel.Visibility = RoomNavigationButtons.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowObjectListSection("HOTSPOTS");
        }
    }

    private Dictionary<int, int[]> ReadPlayDoorwayTargets(GameLibrary game, int roomId)
    {
        var resource = game.Rooms.FirstOrDefault(room => room.ResourceId == roomId);
        if (resource is null) return [];
        var found = new Dictionary<int, int[]>();
        _playOverlayNames = [];
        _playOverlayLocalIndex = [];
        try
        {
            // Background preparation has its own seekable readers. Never scan an active archive handle concurrently.
            if (game.V4Archive is not null || game.V4Disks is not null) game = GameFolderScanner.Reopen(game);
            var data = ScummDataSources.For(game);
            var room = data.ReadRoom(roomId);
            _playOverlayNames = room.ObjectNames.ToDictionary(pair => pair.Key, pair => ScummTextDecoder.DecodeGlyphs(pair.Value).Trim().TrimEnd('@').Trim())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToDictionary();
            var records = room.Objects;
            _playOverlayLocalIndex = records.DistinctBy(record => record.Id).ToDictionary(record => record.Id, record => record.LocalIndex);
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

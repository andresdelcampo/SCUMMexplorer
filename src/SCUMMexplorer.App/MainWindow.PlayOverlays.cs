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

    private void ClearPlayOverlays()
    {
        _playOverlayCanvas.Children.Clear();
        _playOverlayCanvas.Visibility = Visibility.Collapsed;
        _ctx.PlayOverlayPointer = null;
        _ctx.PlayOverlayHoverKey = null;
        _ctx.PlayOverlayHotspots = [];
        _ctx.PlayHighlightId = null;
        _ctx.PlayOverlayScene = null;
        _ctx.PlayOverlayListKey = null;
        _ctx.PlayOverlayObjectRows = [];
        _ctx.PlayOverlayDoorwayRows = [];
    }

    private void UpdatePlayOverlayHover((int X, int Y)? point)
    {
        if (_ctx.PlayOverlayPointer == point) return;
        _ctx.PlayOverlayPointer = point;
        if (IsAdvancedMode) LightHotspotRow(PlayHotspotAt(point)?.ObjectId);
        // R-6.48: a pointer move redraws only when what it points at changed (the hotspot under it, the doorways under it); the table is the one the last
        // redraw (every game frame) built, so a move does no scene capture, no hotspot derivation and no control writes.
        if (ObjectAreaMode != DisplayMode.Hover && DoorwayMode != DisplayMode.Hover) return;
        var key = PlayHoverKey(point);
        if (key == _ctx.PlayOverlayHoverKey) return;
        _ctx.PlayOverlayHoverKey = key;
        RefreshPlayOverlays();
    }


    /// <summary>What the pointer points at in the cached hotspot table: the strong box's object and the doorway objects under it ("" for neither).</summary>
    private string PlayHoverKey((int X, int Y)? point)
    {
        if (point is not { } p || _ctx.PlayShown is not { } frame ||
            frame.Viewport.ToRoom(p.X, p.Y) is not { } at) return "";
        return RoomHotspotRules.HoverKey(_ctx.PlayOverlayHotspots, at.X, at.Y, _ctx.PlayHighlightId is null && ObjectAreaMode != DisplayMode.Never, DoorwayMode == DisplayMode.Hover);
    }

    /// <summary>Whether a present object of the room (a hotspot or a doorway) is under a native screen point.</summary>
    private bool PlayObjectAt((int X, int Y)? point)
    {
        if (point is not { } p || _ctx.PlayShown is not { } frame) return false;
        if (frame.Viewport.ToRoom(p.X, p.Y) is not { } at) return false;
        // The scene as the game leaves it now (the overlays' copy is refreshed only while an aid is visible).
        return _ctx.PlayScreen?.Scene() is { } scene && ScummRoomOverlays.Objects(scene, _ctx.PlayDoorwayTargets).Any(item => item.Contains(at.X, at.Y));
    }

    /// <summary>The listed hotspot under a native screen point, as the game's search would pick it (the same pick as LOOM's room picture).</summary>
    private RoomHotspot? PlayHotspotAt((int X, int Y)? point)
    {
        if (point is not { } p || _ctx.PlayShown is not { } frame) return null;
        if (frame.Viewport.ToRoom(p.X, p.Y) is not { } at) return null;
        var provider = PlayHotspots;
        return RoomHotspotRules.PointedAt(provider.Hotspots().Where(item => !item.IsDoorway && provider.HasActions(item)), at.X, at.Y);
    }

    private void RefreshPlayOverlays() => RefreshPlayOverlays(_ctx.PlayShown);

    private void RefreshPlayOverlays(ScummPlayFrame? frame)
    {
        if (!PlayActive || _ctx.PlayScreen is null || _ctx.Game is null || frame?.RoomId is not int roomId)
        {
            ClearPlayOverlays();
            return;
        }
        var changed = !ReferenceEquals(_ctx.PlayOverlayGame, _ctx.Game) || _ctx.PlayOverlayRoom != roomId;
        if (changed)
        {
            _ctx.PlayOverlayGame = _ctx.Game;
            _ctx.PlayOverlayRoom = roomId;
            _ctx.PlayOverlayPointer = null;
            _ctx.PlayOverlayHoverKey = null;
            // Read on a worker (30 October 2026: on the UI thread it waited for the inspection copy, 6.8 s on Indy 4's first room); the overlay and the
            // doorway rows fill in when the room's code has been read.
            _ctx.PlayDoorwayTargets = [];
            _ctx.PlayOverlayNames = [];
            _ctx.PlayOverlayLocalIndex = [];
            _ = LoadPlayDoorwayTargetsAsync(_ctx.Game, roomId);
        }
        if (_playOverlayCanvas.Parent is null)
        {
            RoomViewportHost.Children.Add(_playOverlayCanvas);
            Panel.SetZIndex(_playOverlayCanvas, 1);
            _playOverlayCanvas.SizeChanged += (_, _) => RefreshPlayOverlays();
        }
        var wanted = ObjectAreaMode != DisplayMode.Never || DoorwayMode != DisplayMode.Never || _ctx.PlayHighlightId is not null ||
            IsAdvancedMode && (RoomMaskToggle.IsChecked == true || WalkBoxToggle.IsChecked == true);
        // Decoded backgrounds/objects are cached by ScummPlayScreen. Capture mutable source state only when an aid is visible.
        if (changed || wanted || IsAdvancedMode || _ctx.PlayOverlayScene is null) _ctx.PlayOverlayScene = _ctx.PlayScreen.Scene();
        if (_ctx.PlayOverlayScene is not { } scene || scene.RoomId != roomId) { ClearPlayOverlays(); return; }

        ObjectAreaOptions.Visibility = RoomOverlayRules.HotspotOptionOffered() ? Visibility.Visible : Visibility.Collapsed;
        DoorwayOptions.Visibility = RoomOverlayRules.DoorwayOptionOffered(_ctx.PlayDoorwayTargets.Count) ? Visibility.Visible : Visibility.Collapsed;
        RoomMaskToggle.Visibility = WalkBoxToggle.Visibility = IsAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        RoomMaskToggle.IsEnabled = scene.Room.DepthMask is not null || scene.Room.ExtraDepthMasks.Count > 0;
        RoomMaskToggle.ToolTip = RoomMaskToggle.IsEnabled ? "Highlight the displayed room and active objects' native depth areas." : "No depth areas are stored in this room.";
        WalkBoxToggle.IsEnabled = scene.State.WalkBoxes is { Count: > 0 };
        WalkBoxToggle.ToolTip = WalkBoxToggle.IsEnabled ? "The current room's native walking areas; disabled boxes are dashed." : "No walkboxes are stored in this room.";

        _playOverlayCanvas.Children.Clear();
        _playOverlayCanvas.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;
        if (!wanted || _playOverlayCanvas.ActualWidth <= 0 || _playOverlayCanvas.ActualHeight <= 0) return;
        var scale = Math.Min(_playOverlayCanvas.ActualWidth / frame.PixelWidth, _playOverlayCanvas.ActualHeight / ScummPlayFrame.Height);
        var offsetX = (_playOverlayCanvas.ActualWidth - frame.PixelWidth * scale) / 2;
        var offsetY = (_playOverlayCanvas.ActualHeight - ScummPlayFrame.Height * scale) / 2;
        var viewport = frame.Viewport;
        _playOverlayCanvas.Clip = new RectangleGeometry(new Rect(offsetX, offsetY + frame.RoomTop * scale,
            frame.PixelWidth * scale, Math.Max(0, frame.RoomBottom - frame.RoomTop) * scale));
        Point Map(double x, double y) => new(offsetX + (x - frame.ViewLeft) * scale, offsetY + (y + frame.RoomTop) * scale);

        if (IsAdvancedMode && RoomMaskToggle.IsChecked == true && RoomMaskToggle.IsEnabled)
        {
            var tint = BitmapSource.Create(frame.PixelWidth, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null,
                ScummRoomOverlays.DepthPixels(scene, viewport), frame.PixelWidth * 4);
            var image = new Image { Source = tint, Width = frame.PixelWidth * scale, Height = ScummPlayFrame.Height * scale };
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
        var surface = new RoomSurface(_playOverlayCanvas, scale, Map, offsetX, offsetX + frame.PixelWidth * scale);
        var pointInRoom = _ctx.PlayOverlayPointer is { } pointer ? viewport.ToRoom(pointer.X, pointer.Y) : null;
        var hotspots = PlayHotspots.Hotspots();
        _ctx.PlayOverlayHotspots = hotspots;
        // The same drawing as LOOM's room picture: doorway boxes by their option; hotspot boxes faint under `Always`, strong for the one pointed at or chosen in the list.
        DrawDoorwayBoxes(surface, RoomHotspotRules.DoorwayBoxes(hotspots,
            group => RoomOverlayRules.Shows(DoorwayMode, pointInRoom is { } hover && group.Any(item => item.Contains(hover.X, hover.Y))),
            item => PlayHotspots.DoorwayTip(item), _ => true));
        var objects = hotspots.Where(item => !item.IsDoorway).ToArray();
        var pointed = _ctx.PlayHighlightId is int chosen ? objects.FirstOrDefault(item => item.ObjectId == chosen)
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
        if (!PlayActive || !IsAdvancedMode || _ctx.PlayOverlayScene is not { } scene)
        {
            _ctx.PlayOverlayListKey = null;
            return;
        }
        var provider = PlayHotspots;
        var hotspots = provider.Hotspots();
        var key = _ctx.Game!.Key + ":" + scene.RoomId + ":" + string.Join(',', hotspots.Select(item => $"{item.ObjectId}@{item.X},{item.Y}:{(provider.HasActions(item) ? 1 : 0)}"));
        // Preview controls share these panels and can replace their children without changing
        // native room state. Only reuse rows that are still the ones this view published.
        if (_ctx.PlayOverlayListKey == key &&
            ObjectList.Children.Cast<UIElement>().SequenceEqual(_ctx.PlayOverlayObjectRows) &&
            RoomNavigationButtons.Children.Cast<UIElement>().SequenceEqual(_ctx.PlayOverlayDoorwayRows))
        { DisplayLists(); return; }
        _ctx.PlayOverlayListKey = key;
        RoomNavigationButtons.Children.Clear(); ObjectList.Children.Clear();
        _ctx.HoveredHotspotRowId = null;
        FillHotspotRows(provider);
        FillDoorwayRows(provider.DoorwayRows());
        _ctx.PlayOverlayObjectRows = ObjectList.Children.Cast<UIElement>().ToArray();
        _ctx.PlayOverlayDoorwayRows = RoomNavigationButtons.Children.Cast<UIElement>().ToArray();
        DisplayLists();

        void DisplayLists()
        {
            RoomNavigationHeader.Text = $"DOORWAYS · {RoomNavigationButtons.Children.Count}";
            RoomNavigationPanel.Visibility = RoomNavigationButtons.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowObjectListSection("HOTSPOTS");
        }
    }

    private async Task LoadPlayDoorwayTargetsAsync(GameLibrary game, int roomId)
    {
        var read = await Task.Run(() => ReadPlayDoorwayTargets(game, roomId));
        if (!ReferenceEquals(_ctx.PlayOverlayGame, game) || _ctx.PlayOverlayRoom != roomId) return;
        (_ctx.PlayDoorwayTargets, _ctx.PlayOverlayNames, _ctx.PlayOverlayLocalIndex) = read;
        RefreshPlayOverlays();
        RefreshPlayOverlayLists();
    }

    private (Dictionary<int, int[]> Targets, Dictionary<int, string> Names, Dictionary<int, int> LocalIndex) ReadPlayDoorwayTargets(GameLibrary game, int roomId)
    {
        var resource = game.Rooms.FirstOrDefault(room => room.ResourceId == roomId);
        var found = new Dictionary<int, int[]>();
        var names = new Dictionary<int, string>();
        var localIndex = new Dictionary<int, int>();
        if (resource is null) return (found, names, localIndex);
        try
        {
            // Background preparation has its own seekable readers. Never scan an active archive handle concurrently: read through the inspection copy.
            using var turn = UseInspectionCopy(game, out game);
            var data = ScummDataSources.For(game);
            var room = data.ReadRoom(roomId);
            names = room.ObjectNames.ToDictionary(pair => pair.Key, pair => ScummTextDecoder.DecodeGlyphs(pair.Value).Trim().TrimEnd('@').Trim())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToDictionary();
            var records = room.Objects;
            localIndex = records.DistinctBy(record => record.Id).ToDictionary(record => record.Id, record => record.LocalIndex);
            var verbs = data.ReadObjectVerbs(roomId, records);
            var finder = new ScummDoorwayFinder(game, data);
            foreach (var record in records)
            {
                if (!verbs.TryGetValue(record.Id, out var entries)) continue;
                var targets = new HashSet<int>();
                foreach (var verb in finder.DoorVerbs(entries))
                    try
                    {
                        var script = data.ReadObjectVerb(roomId, record, verb.VerbId, out _);
                        targets.UnionWith(finder.FindDoorway(resource, script, verb).Select(target => target.RoomId));
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { }
                if (targets.Count > 0) found[record.Id] = targets.Order().ToArray();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException) { }
        return (found, names, localIndex);
    }
}

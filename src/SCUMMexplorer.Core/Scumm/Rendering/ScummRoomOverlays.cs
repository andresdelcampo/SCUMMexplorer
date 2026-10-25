namespace SCUMMexplorer.Core.Scumm;

/// <summary>Room pixels projected into the native 320 x 200 screen, including its camera and main-screen band.</summary>
public readonly record struct ScummRoomViewport(int ViewLeft, int Top, int Bottom, int Width = ScummPlayFrame.Width)
{
    public (int X, int Y)? ToRoom(int screenX, int screenY) =>
        screenX >= 0 && screenX < Width && screenY >= Top && screenY < Bottom
            ? (screenX + ViewLeft, screenY - Top) : null;

    public (int X, int Y) ToScreen(int roomX, int roomY) => (roomX - ViewLeft, roomY + Top);
}

/// <summary>A selectable native object rectangle; possible doorway destinations stay distinct from object hotspots.</summary>
public sealed record ScummRoomOverlayObject(int ObjectId, int X, int Y, int Width, int Height, IReadOnlyList<int> Destinations)
{
    public bool IsDoorway => Destinations.Count != 0;
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>Display-only overlays use the same live object selection and composed depth mask as the actor renderer.</summary>
public static class ScummRoomOverlays
{
    public static IReadOnlyList<ScummRoomOverlayObject> Objects(ScummRoomScene scene,
        IReadOnlyDictionary<int, int[]> doorwayTargets) => scene.Records.Where(scene.IsHotspot).Select(record =>
    {
        var at = scene.Snapshot.ObjectPositions.GetValueOrDefault(record.Id, new RoomObjectPosition(record.X, record.Y));
        return new ScummRoomOverlayObject(record.Id, at.X, at.Y, record.Width, record.Height,
            doorwayTargets.TryGetValue(record.Id, out var targets) ? targets : []);
    }).ToArray();

    /// <summary>ARGB tint at native screen resolution; no room pixels, palette or runtime state are changed.</summary>
    public static uint[] DepthPixels(ScummRoomScene scene, ScummRoomViewport viewport)
    {
        var pixels = new uint[viewport.Width * ScummPlayFrame.Height];
        var mask = scene.Selection.ComposeDepthMask(scene.Room);
        var extra = scene.Room.ExtraDepthMasks;
        for (var y = Math.Max(0, viewport.Top); y < Math.Min(ScummPlayFrame.Height, viewport.Bottom); y++)
            for (var x = 0; x < viewport.Width; x++)
            {
                var roomX = x + viewport.ViewLeft;
                var roomY = y - viewport.Top;
                if (roomX < 0 || roomX >= scene.Room.Width || roomY < 0 || roomY >= scene.Room.Height) continue;
                var index = roomY * scene.Room.Width + roomX;
                if (mask is not null && mask[index] != 0 || extra.Any(plane => plane.Pixels.Span[index] != 0))
                    pixels[y * viewport.Width + x] = 0x806EDCFF;
            }
        return pixels;
    }
}

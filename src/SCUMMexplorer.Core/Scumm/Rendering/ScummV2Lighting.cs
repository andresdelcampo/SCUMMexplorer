namespace SCUMMexplorer.Core.Scumm;

/// <summary>DOS v1/v2 front-buffer lighting, from ScummVM gfx.cpp drawFlashlight (18 October 2026).
/// Room darkness affects the background/objects; actors are subsequently drawn using their light-mode palette.</summary>
public static class ScummV2Lighting
{
    // Native nibble masks. The forward/backward curves deliberately differ, including in v1.
    private static readonly byte[] V1Forward = [0,0,0,0, 0,0,0,255, 0,0,255,255, 0,0,255,255, 0,255,255,255, 0,255,255,255, 0,255,255,255, 255,255,255,255];
    private static readonly byte[] V1Backward = [255,0,0,0, 255,255,0,0, 255,255,0,0, 255,255,255,0, 255,255,255,0, 255,255,255,0, 255,255,255,255, 255,255,255,255];
    private static readonly byte[] V2Forward = [0,0,0,0, 0,0,15,255, 0,0,255,255, 0,15,255,255, 0,255,255,255, 15,255,255,255, 15,255,255,255, 255,255,255,255];
    private static readonly byte[] V2Backward = [0,0,0,0, 255,0,0,0, 255,255,0,0, 255,255,0,0, 255,255,255,0, 255,255,255,0, 255,255,255,0, 255,255,255,255];

    /// <summary>Whether one background pixel is restored from the back buffer. Coordinates are in the room;
    /// pointer coordinates are in the main viewport (its top row is zero). No actor/game/story state is changed.</summary>
    public static bool IsRoomPixelLit(int x, int y, int lights, (int X, int Y) strips, int engineVersion,
        (int X, int Y) pointer, int viewLeft, int viewWidth, int viewHeight)
    {
        if (engineVersion is not (1 or 2) || (lights & 2) != 0) return true;
        if ((lights & 4) == 0 || strips.X <= 0 || strips.Y <= 0) return false;
        var width = Math.Min(strips.X * 8, viewWidth);
        var height = Math.Min(strips.Y * 8, viewHeight);
        if (width <= 0 || height <= 0) return false;
        // Position snaps before centering and clips by moving the whole rectangle, not by shrinking it.
        var left = Math.Clamp(((pointer.X + viewLeft) & ~7) - width / 2 - viewLeft, 0, viewWidth - width);
        var top = Math.Clamp((pointer.Y & ~1) - height / 2, 0, viewHeight - height);
        var localX = x - viewLeft - left;
        var localY = y - top;
        if (localX < 0 || localX >= width || localY < 0 || localY >= height) return false;
        var cornerRow = Math.Min(localY, height - 1 - localY);
        if (cornerRow >= 8) return true;
        var forward = engineVersion == 1 ? V1Forward : V2Forward;
        var backward = engineVersion == 1 ? V1Backward : V2Backward;
        var visible = true;
        if (localX < 8)
        {
            var mask = forward[cornerRow * 4 + localX / 2];
            visible &= (mask & (localX % 2 == 0 ? 0xF0 : 0x0F)) != 0;
        }
        if (localX >= width - 8)
        {
            var rightX = localX - (width - 8);
            var mask = backward[cornerRow * 4 + rightX / 2];
            visible &= (mask & (rightX % 2 == 0 ? 0x0F : 0xF0)) != 0;
        }
        return visible;
    }

    public static byte[] Compose(byte[] background, int width, int height, int lights, (int X, int Y) strips,
        int engineVersion, (int X, int Y) pointer, int viewLeft = 0, int viewWidth = 320, int? viewHeight = null)
    {
        if (background.Length != checked(width * height)) throw new ArgumentException("Background size does not match its dimensions.", nameof(background));
        if (engineVersion is not (1 or 2) || (lights & 2) != 0) return background;
        var shown = new byte[background.Length];
        if ((lights & 4) == 0 || strips.X <= 0 || strips.Y <= 0) return shown;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            if (IsRoomPixelLit(x, y, lights, strips, engineVersion, pointer, viewLeft, Math.Min(width, viewWidth), Math.Min(height, viewHeight ?? height)))
                shown[y * width + x] = background[y * width + x];
        return shown;
    }
}

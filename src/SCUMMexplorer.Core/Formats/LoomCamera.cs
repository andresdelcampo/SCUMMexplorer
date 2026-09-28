namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// ScummVM's <c>camera</c> for v3 apart from its x position, which the session keeps in <c>VAR_CAMERA_POS_X</c>
/// (<c>var[0x002]</c>): the mode, the followed actor, the destination and <c>_movingToActor</c>.
/// <see cref="Move"/> is <c>moveCamera</c>, run once a frame after the walks.
/// </summary>
public sealed class LoomCamera
{
    /// <summary>ScummVM <c>kNormalCameraMode</c>, <c>kFollowActorCameraMode</c>, <c>kPanningCameraMode</c>.</summary>
    public const int Normal = 1, FollowActor = 2, Panning = 3;
    /// <summary>ScummVM <c>camera._leftTrigger</c>/<c>_rightTrigger</c> (strips 10 and 30 of the 40 on screen).</summary>
    public const int LeftTrigger = 10, RightTrigger = 30;

    public int Mode { get; set; } = Normal;
    /// <summary>The actor a follow-mode camera follows (<c>camera._follows</c>).</summary>
    public int Follows { get; set; }
    public int DestX { get; set; } = 160;
    public bool MovingToActor { get; set; }

    /// <summary><c>startScene</c>: normal mode, centred on the first screen.</summary>
    public void Reset(int x)
    {
        Mode = Normal;
        DestX = x;
        MovingToActor = false;
    }

    /// <summary>ScummVM <c>_screenStartStrip</c> after <c>cameraMoved</c> for a camera at <paramref name="x"/>.</summary>
    public static int ScreenStartStrip(int x) => x / 8 - 20;

    /// <summary>
    /// ScummVM <c>moveCamera</c> (v3, no snap scroll): the camera is rounded down to a strip, moves 8 pixels a frame
    /// back inside <c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c> first; a follow-mode camera starts moving to its actor once
    /// the actor is outside the trigger strips, and keeps moving 8 pixels a frame towards it (or towards the pan's
    /// destination) until it is on the actor's strip. <c>cameraMoved</c> then keeps it half a screen from the room's
    /// edges. Returns the new x.
    /// </summary>
    public int Move(int current, int min, int max, (int Min, int Max) roomBounds, int? followedX)
    {
        var x = current & 0xFFF8;
        if (x < min) return Clamp(x + 8, roomBounds);
        if (x > max) return Clamp(x - 8, roomBounds);
        if (Mode == FollowActor && followedX is int actorX)
        {
            var strip = actorX / 8 - ScreenStartStrip(current);
            if (strip < LeftTrigger || strip > RightTrigger) MovingToActor = true;
        }
        if (MovingToActor && followedX is int target) DestX = target;
        if (DestX < min) DestX = min;
        if (DestX > max) DestX = max;
        if (x < DestX) x += 8;
        if (x > DestX) x -= 8;
        if (MovingToActor && followedX is int arrived && x / 8 == arrived / 8) MovingToActor = false;
        return Clamp(x, roomBounds);
    }

    private static int Clamp(int x, (int Min, int Max) bounds) => Math.Clamp(x, bounds.Min, bounds.Max);
}

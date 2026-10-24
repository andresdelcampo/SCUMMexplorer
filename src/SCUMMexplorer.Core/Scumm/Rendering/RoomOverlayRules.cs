namespace SCUMMexplorer.Core.Scumm;

/// <summary>The `Never / On hover / Always` choice of the Hotspots and Doorways options (R-6.5), the same for every game.</summary>
public enum RoomOverlayMode { Never, Hover, Always }

/// <summary>
/// R-16.9 (user, 24 Oct 2026: "hotspots and doorways visibility should not be game specific"): when an object's box is
/// drawn and when an option row is offered is decided here once; each surface (LOOM's room picture, the native game
/// screen) only draws what these rules allow.
/// </summary>
public static class RoomOverlayRules
{
    /// <summary>Whether a box is drawn: always in `Always`, only under the pointer in `On hover`, never in `Never`.</summary>
    public static bool Shows(RoomOverlayMode mode, bool underPointer) =>
        mode == RoomOverlayMode.Always || mode == RoomOverlayMode.Hover && underPointer;

    /// <summary>The `Doorways` option row is offered only while the room has a doorway (R-6.19: no inert controls).</summary>
    public static bool DoorwayOptionOffered(int doorwayCount) => doorwayCount > 0;

    /// <summary>The `Hotspots` option row is offered in both modes (Normal and Advanced) in every game.</summary>
    public static bool HotspotOptionOffered() => true;

    /// <summary>Whether a mode is the one that needs a pointer position.</summary>
    public static bool NeedsPointer(RoomOverlayMode mode) => mode == RoomOverlayMode.Hover;
}

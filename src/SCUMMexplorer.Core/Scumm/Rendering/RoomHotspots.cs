namespace SCUMMexplorer.Core.Scumm;

// R-16.9 (user, 24 Oct 2026: "hotspots and doorways visibility should not be game specific -including implementation
// as much as possible"): which objects are hotspots or doorways, how they are grouped, labelled and picked is decided
// here once. A game (LOOM's live session, the generic play sessions, a browsed room) only fills these types; the App
// draws and lists them with one implementation (MainWindow.RoomHotspots.cs).

/// <summary>An object of the room that the game's own object search can select, with its current box.</summary>
public sealed record RoomHotspot(int ObjectId, int LocalIndex, string? Name, int X, int Y, int Width, int Height, IReadOnlyList<int> Destinations)
{
    /// <summary>A doorway: its scripts can load a room (R-6.13). Doorways have their own overlay, list and option.</summary>
    public bool IsDoorway => Destinations.Count != 0;

    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>One doorway box to draw: doorways in one rectangle share it and its label (<paramref name="Live"/> is false for an untouchable one, drawn dimmer).</summary>
public sealed record RoomDoorwayBox(int X, int Y, int Width, int Height, IReadOnlyList<int> Targets, bool Live, string Tip, string Prefix = "");

/// <summary>One row of the Advanced doorway list: a possible destination room, its tooltip and the action that opens it for exploration.</summary>
public sealed record RoomDoorwayRow(int TargetRoom, string Label, string Tip, Action Open);

/// <summary>The one interface a game (or a browsed room) implements to fill the shared hotspot and doorway views.</summary>
public interface IRoomHotspotProvider
{
    /// <summary>The room shown, if any.</summary>
    int? RoomId { get; }

    /// <summary>Every present object with a box, hotspots and doorways, in directory order.</summary>
    IReadOnlyList<RoomHotspot> Hotspots();

    /// <summary>The tooltip of a doorway's box (the game's own wording of what its script can load).</summary>
    string DoorwayTip(RoomHotspot doorway);

    /// <summary>Whether the object is touchable now (an untouchable doorway is drawn dimmer and is not picked first).</summary>
    bool IsLive(RoomHotspot hotspot);

    /// <summary>Whether the game offers any action on the object now (only those are listed as hotspot rows, as in LOOM).</summary>
    bool HasActions(RoomHotspot hotspot);

    /// <summary>The row of a hotspot is hovered or focused (null: left): the game surface outlines that box.</summary>
    void Highlight(RoomHotspot? hotspot);

    /// <summary>The rows of the Advanced doorway list.</summary>
    IReadOnlyList<RoomDoorwayRow> DoorwayRows();

    /// <summary>A hotspot row was chosen: the game's own way of offering that object's actions (the shared Interact menu).</summary>
    void Choose(RoomHotspot hotspot);
}

public static class RoomHotspotRules
{
    /// <summary>
    /// Whether the game's own object search can select the object here (<c>findObject</c>): it has a box, it is not at 0, 0 (off the scene), the room owns
    /// it, it is not untouchable (class 24, R-6.15) and its parent chain is in the required states. The same for every game; a version only supplies
    /// the state bits (<paramref name="stateMask"/>) and what an unlisted owner means.
    /// </summary>
    public static bool IsHotspot(ScummRoomObjectRecord record, IReadOnlyList<ScummRoomObjectRecord> records, ScummSessionSnapshot snapshot,
        byte stateMask = 0x0F, byte ownerWhenUnlisted = 0x0F)
    {
        if (record.Width <= 0 || record.Height <= 0) return false;
        var at = snapshot.ObjectPositions.TryGetValue(record.Id, out var moved) ? moved : new RoomObjectPosition(record.X, record.Y);
        if (at == new RoomObjectPosition(0, 0)) return false;
        if ((snapshot.ObjectOwners.TryGetValue(record.Id, out var owner) ? owner : ownerWhenUnlisted) != 0x0F || snapshot.IsUntouchable(record.Id)) return false;
        if (stateMask == 8 && (snapshot.ObjectStates.GetValueOrDefault(record.Id) & 2) != 0) return false;
        var current = record;
        for (var steps = 0; current.ParentLocalIndex != 0; steps++)
        {
            var parent = records.FirstOrDefault(item => item.LocalIndex == current.ParentLocalIndex);
            if (steps > records.Count || parent is null || (snapshot.ObjectStates.GetValueOrDefault(parent.Id) & stateMask) != current.ParentRequiredState) return false;
            current = parent;
        }
        return true;
    }

    /// <summary>The doorway boxes to draw: doorways in one rectangle share one box and one label; <paramref name="shown"/> decides per box (R-6.5).</summary>
    public static IReadOnlyList<RoomDoorwayBox> DoorwayBoxes(IEnumerable<RoomHotspot> hotspots, Func<IReadOnlyList<RoomHotspot>, bool> shown,
        Func<RoomHotspot, string> tip, Func<RoomHotspot, bool> live) =>
        hotspots.Where(item => item.IsDoorway).GroupBy(item => (item.X, item.Y, item.Width, item.Height))
            .Select(group => group.ToArray()).Where(group => shown(group))
            .Select(group => new RoomDoorwayBox(group[0].X, group[0].Y, group[0].Width, group[0].Height,
                group.SelectMany(item => item.Destinations).Distinct().Order().ToArray(), group.Any(live), string.Join("\n", group.Select(tip))))
            .ToArray();

    /// <summary>The names used by more than one hotspot row; those rows carry their index (LOOM's pattern).</summary>
    public static HashSet<string> RepeatedLabels(IEnumerable<RoomHotspot> hotspots) =>
        hotspots.Where(item => !item.IsDoorway).GroupBy(item => item.Name ?? $"Hotspot {item.LocalIndex}")
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();

    /// <summary>The hotspot under a room point as the game's search would pick it: the first listed in directory order, touchable ones before untouchable.</summary>
    public static RoomHotspot? PointedAt(IEnumerable<RoomHotspot> hotspots, int x, int y, Func<RoomHotspot, bool>? untouchable = null)
    {
        var hits = hotspots.Where(item => item.Contains(x, y)).ToArray();
        return hits.FirstOrDefault(item => untouchable?.Invoke(item) != true) ?? hits.FirstOrDefault();
    }
}

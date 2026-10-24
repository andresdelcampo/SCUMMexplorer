using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>The two providers that fill the shared hotspot and doorway views (R-16.9); they only read the game's data.</summary>
public partial class MainWindow
{
    private IRoomHotspotProvider? _loomHotspotsInstance, _playHotspotsInstance;
    private GameResource? _roomNavigationRoom;
    private LoomRoomHotspots LoomHotspots => (LoomRoomHotspots)(_loomHotspotsInstance ??= new LoomRoomHotspots(this));
    private PlayRoomHotspots PlayHotspots => (PlayRoomHotspots)(_playHotspotsInstance ??= new PlayRoomHotspots(this));

    /// <summary>LOOM's live session (EGA, CD, demos) and the browsed rooms that share its doorway candidates.</summary>
    private sealed class LoomRoomHotspots(MainWindow w) : IRoomHotspotProvider
    {
        public int? RoomId => w._currentRoomResource?.ResourceId;

        public RoomHotspot Describe(ScummRoomObjectRecord record)
        {
            var at = w._session?.Snapshot.ObjectPositions.TryGetValue(record.Id, out var current) == true ? current : new RoomObjectPosition(record.X, record.Y);
            return new RoomHotspot(record.Id, record.LocalIndex, w._roomObjectNames.TryGetValue(record.Id, out var name) ? name : null,
                at.X, at.Y, record.Width, record.Height, w.DoorwayTargets(record));
        }

        public IReadOnlyList<RoomHotspot> Hotspots() =>
            w._session?.Snapshot is not { } snapshot ? [] :
            w._currentRoomObjectRecords.OrderBy(item => item.LocalIndex).Where(item => w.IsVerbHotspotAvailable(item, snapshot)).Select(Describe).ToArray();

        public string DoorwayTip(RoomHotspot doorway) =>
            w._currentRoomObjectRecords.FirstOrDefault(item => item.Id == doorway.ObjectId) is { } record ? w.DoorwayToolTip(record) : "";

        public bool IsLive(RoomHotspot hotspot) => w._session?.Snapshot.IsUntouchable(hotspot.ObjectId) != true;

        public bool HasActions(RoomHotspot hotspot) => w._roomVerbs.TryGetValue(hotspot.ObjectId, out var verbs) && verbs.Count > 0;

        public void Highlight(RoomHotspot? hotspot) =>
            w.ShowHotspotHighlight(hotspot is null ? null : w._currentRoomObjectRecords.FirstOrDefault(item => item.Id == hotspot.ObjectId));

        public void Choose(RoomHotspot hotspot) => w.ChooseLoomObject(hotspot.ObjectId);

        // One button per target room; every hotspot that leads there stays in its tooltip.
        public IReadOnlyList<RoomDoorwayRow> DoorwayRows()
        {
            var rows = new List<RoomDoorwayRow>();
            var room = w._roomNavigationRoom;
            foreach (var target in w._roomNavigationCandidates.GroupBy(item => item.TargetRoomId))
            {
                var candidate = target.First();
                // Verbs that share one code block are one source; name them all.
                var sources = target.GroupBy(item => (item.Object.Id, item.Verb.ScriptOffset, item.InstructionOffset, item.Path))
                    .Select(source =>
                        $"{room?.Name} object {source.Key.Id} ({string.Join(", ", source.Select(item => item.Verb.Label).Distinct())}) " +
                        $"code 0x{source.Key.ScriptOffset:X4}, room-load instruction +0x{source.Key.InstructionOffset:X4} ({source.Key.Path})")
                    .ToArray();
                var tip = (sources.Length == 1
                              ? $"Static candidate: {sources[0]}."
                              : $"Static candidates ({sources.Length} sources):\n• {string.Join("\n• ", sources)}") +
                          "\nOpen directly for exploration; script conditions and effects are not simulated.";
                rows.Add(new RoomDoorwayRow(target.Key, $"Room {target.Key:00}", tip, () => w.OpenRoomDestination(candidate)));
            }
            return rows;
        }
    }

    /// <summary>The native game screen of every generic session (v1/v2 Maniac/Zak, verb-based v3/v4/v5 and their static previews).</summary>
    private sealed class PlayRoomHotspots(MainWindow w) : IRoomHotspotProvider
    {
        public int? RoomId => w._playOverlayScene?.RoomId;

        public IReadOnlyList<RoomHotspot> Hotspots() =>
            w._playOverlayScene is not { } scene ? [] :
            ScummRoomOverlays.Objects(scene, w._playDoorwayTargets).Select(item => new RoomHotspot(item.ObjectId,
                w._playOverlayLocalIndex.GetValueOrDefault(item.ObjectId, item.ObjectId), w._playOverlayNames.GetValueOrDefault(item.ObjectId),
                item.X, item.Y, item.Width, item.Height, item.Destinations)).ToArray();

        public string DoorwayTip(RoomHotspot doorway) =>
            $"Object {doorway.ObjectId}: its script can load {string.Join(" or ", doorway.Destinations.Select(id => $"Room {id:00}"))}. " +
            "The Advanced doorway buttons open possible destinations directly for browsing.";

        public bool IsLive(RoomHotspot hotspot) => true;

        public bool HasActions(RoomHotspot hotspot) => w.GenericObjectActions(hotspot.ObjectId).Count > 0;

        public void Highlight(RoomHotspot? hotspot)
        {
            w._playHighlightId = hotspot?.ObjectId;
            w.RefreshPlayOverlays();
        }

        public void Choose(RoomHotspot hotspot) => w.SelectGenericInteractTarget(hotspot.ObjectId);

        public IReadOnlyList<RoomDoorwayRow> DoorwayRows()
        {
            if (w._playOverlayScene is not { } scene || w._game is not { } game) return [];
            var rows = new List<RoomDoorwayRow>();
            foreach (var target in Hotspots().Where(item => item.IsDoorway).SelectMany(item => item.Destinations.Select(id => (Room: id, Door: item)))
                         .GroupBy(item => item.Room).OrderBy(group => group.Key))
            {
                var room = game.Rooms.FirstOrDefault(item => item.ResourceId == target.Key);
                if (room is null) continue;
                var tip = $"Static candidate: Room {scene.RoomId:00} object {string.Join(", ", target.Select(item => item.Door.ObjectId).Distinct())}." +
                          "\nOpen directly for exploration; script conditions and effects are not simulated.";
                rows.Add(new RoomDoorwayRow(target.Key, $"Room {target.Key:00}", tip, () => w.SelectResourceInTree(room)));
            }
            return rows;
        }
    }
}

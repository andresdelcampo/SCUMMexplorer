using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    // A newly scanned library must not reuse a previous opening's native state after its
    // source files change. Within an opening, card and room share the same detached scene.
    private static readonly ConditionalWeakTable<GameLibrary, ConcurrentDictionary<int, Lazy<ScummRoomScene?>>> RoomPreviewScenes = new();

    private static ScummRoomScene? ReadFirstVisit(GameLibrary game, int roomId) =>
        RoomPreviewScenes.GetValue(game, _ => new()).GetOrAdd(roomId, _ => new(() =>
        {
            try
            {
                return GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced
                    ? Core.Maniac.ManiacFirstVisit.Run(game, roomId)
                    : ScummRoomFirstVisit.Run(game, roomId);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException)
            { return null; }
        })).Value;

    // The old object directory supplies names; reached session state supplies eligibility and position.
    // Index visibility is not a substitute for the v1/v2 engine's object search after room entry.
    private static IReadOnlyList<BrowsedObjectLabel> ReadOlderFirstVisitObjects(byte[] bytes, ScummRoomScene scene)
    {
        var names = ScummOldRoomObjects.Read(LflEncoding.DecodeXorFF(bytes), null)
            .GroupBy(entry => entry.Id).ToDictionary(group => group.Key, group => group.Last().Name);
        return scene.Records.Where(scene.IsHotspot).Select(record =>
        {
            var position = scene.Snapshot.ObjectPositions.GetValueOrDefault(record.Id,
                new RoomObjectPosition(record.X, record.Y));
            return new BrowsedObjectLabel(record.Id, names.GetValueOrDefault(record.Id, ""),
                position.X, position.Y, record.Width, record.Height);
        }).ToArray();
    }
}

namespace SCUMMexplorer.App;

public partial class MainWindow
{
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

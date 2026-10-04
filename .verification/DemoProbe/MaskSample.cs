using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// DemoProbe <folder> walk <room> <x> <y> mask: per frame, the ego's visible pixels with the room's depth mask against the same pose unmasked.
static class MaskSample
{
    private static byte[]? _bytes;
    private static RoomImage? _room;
    private static IReadOnlyList<LoomRoomObjectRecord>? _records;
    private static IReadOnlyList<RoomObjectImage>? _images;
    private static LoomWalkBoxSet? _boxes;
    private static readonly Dictionary<int, LoomCostume?> Costumes = [];

    public static void Print(GameLibrary game, LoomExplorationSession session, int roomId, int click, int frame)
    {
        if (session.Snapshot.RoomId != roomId) return;
        var resource = game.Rooms.Single(r => r.ResourceId == roomId);
        _bytes ??= File.ReadAllBytes(resource.FilePath);
        _room ??= new OldV3EgaRoomDecoder().Decode(_bytes);
        _records ??= new LoomRoomObjectDirectory().Decode(_bytes);
        _images ??= new LoomRoomObjects().Decode(_bytes);
        _boxes ??= LoomWalkBoxReader.Read(_bytes);
        var snapshot = session.Snapshot;
        var selection = new RoomObjectSelection(_images, snapshot.ObjectStates, snapshot.ObjectPositions, _records, _room, drawQueue: snapshot.DrawOrder);
        var mask = selection.ComposeDepthMask(_room);
        var ego = session.Actors.Single(a => a.Number == 1);
        if (click == 2 && frame == 29)
        {
            // The mask around the ego at the start of the hidden stretch: the room's own mask, then with the objects' masks painted in.
            var roomMask = _room.DepthMask!.Pixels.ToArray();
            for (var y = 92; y <= 116; y++)
            {
                var own = new System.Text.StringBuilder(); var composed = new System.Text.StringBuilder();
                for (var x = 520; x <= 580; x++)
                {
                    own.Append(roomMask[y * _room.Width + x] == 0 ? '.' : '#');
                    composed.Append(x == ego.X && y == ego.Y ? '@' : mask![y * _room.Width + x] == 0 ? '.' : '#');
                }
                Console.WriteLine($"   y{y,3} room {own}   composed {composed}");
            }
            Console.WriteLine($"   objects drawing masks: {string.Join(", ", _images!.Where(o => o.Mask is not null && snapshot.ObjectStates.GetValueOrDefault(o.Id) != 0).Select(o => o.Id))}");
        }
        LoomCostume? Costume(int id) => Costumes.TryGetValue(id, out var c) ? c : Costumes[id] = LoomCostume.Load(game, id);
        var clipped = LoomActorRenderer.Render([ego], roomId, _room.Width, _room.Height, Costume, mask, _boxes, session.ObjectClasses).Actors.SingleOrDefault();
        var plain = LoomActorRenderer.Render([ego], roomId, _room.Width, _room.Height, Costume).Actors.SingleOrDefault();
        if (clipped is not null && plain is not null && plain.Pixels != clipped.Pixels)
            Console.WriteLine($"   mask: click {click} frame {frame} ego ({ego.X},{ego.Y}) box {ego.WalkBox} costume {ego.Look.Costume} z-plane {clipped.ZPlane}: {clipped.Pixels} of {plain.Pixels} pixels visible");
    }
}

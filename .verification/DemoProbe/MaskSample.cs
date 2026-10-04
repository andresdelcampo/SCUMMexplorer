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
        var states = snapshot.ObjectStates.ToDictionary(p => p.Key, p => p.Value);
        // DEMO_ACTIVATE=157 draws that object as an experiment (its image and mask), to see what its mask does for the walker.
        foreach (var id in (Environment.GetEnvironmentVariable("DEMO_ACTIVATE") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) states[int.Parse(id)] = 1;
        var selection = new RoomObjectSelection(_images, states, snapshot.ObjectPositions, _records, _room, drawQueue: snapshot.DrawOrder);
        var mask = selection.ComposeDepthMask(_room);
        var ego = session.Actors.Single(a => a.Number == 1);
        if (click == 0 && frame == 23)
            foreach (var box in _boxes!.Boxes)
                Console.WriteLine($"   Box {box.Index}: mask {box.Mask}, flags 0x{box.Flags:X2}; {string.Join(';', box.Corners)}");
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
            foreach (var o in _images!)
            {
                var p = selection.GetPosition(o);
                var near = p.X < 600 && p.X + o.Width > 480 && p.Y < 125 && p.Y + o.Height > 85;
                if (near || o.Mask is not null)
                    Console.WriteLine($"   object {o.Id} at ({p.X},{p.Y}) {o.Width}x{o.Height}, mask {(o.Mask is null ? "none" : "yes")}, active {selection.IsActive(o)}, state {snapshot.ObjectStates.GetValueOrDefault(o.Id)}{(near ? " NEAR the walk" : "")}");
            }
        }
        LoomCostume? Costume(int id) => Costumes.TryGetValue(id, out var c) ? c : Costumes[id] = LoomCostume.Load(game, id);
        var clipped = LoomActorRenderer.Render([ego], roomId, _room.Width, _room.Height, Costume, mask, _boxes, session.ObjectClasses).Actors.SingleOrDefault();
        var plain = LoomActorRenderer.Render([ego], roomId, _room.Width, _room.Height, Costume).Actors.SingleOrDefault();
        if (clipped is not null && plain is not null && plain.Pixels != clipped.Pixels)
            Console.WriteLine($"   mask: click {click} frame {frame} ego ({ego.X},{ego.Y}) box {ego.WalkBox} costume {ego.Look.Costume} z-plane {clipped.ZPlane}: {clipped.Pixels} of {plain.Pixels} pixels visible");
    }
}

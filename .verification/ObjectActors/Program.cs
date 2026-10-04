using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// Usage: ObjectActors <game folder> <room>
// For every room object: its rectangle and walk point, and the actors the game's code puts into the room at a point inside
// (or within 16 pixels of) that rectangle, or at the object itself (putActorAtObject).
var game = new GameFolderScanner().Scan(args[0]);
var room = int.Parse(args[1]);
var resource = game.Rooms.Single(r => r.ResourceId == room);
var bytes = File.ReadAllBytes(resource.FilePath);
var records = new LoomRoomObjectDirectory().Decode(bytes);
var names = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), records);
var scanner = new LoomSceneStateScanner(game);
for (var actor = 2; actor < 13; actor++)
    foreach (var p in scanner.FindActorPlacers(actor, room))
        Console.WriteLine($"placer actor {actor}: {p.Where} at ({p.X},{p.Y}) object {p.AtObject} dress {p.Dress}");
foreach (var r in records)
    Console.WriteLine($"object {r.Id} \"{names.GetValueOrDefault(r.Id, "")}\" rect ({r.X},{r.Y}) {r.Width}x{r.Height} walk ({r.WalkX},{r.WalkY})");

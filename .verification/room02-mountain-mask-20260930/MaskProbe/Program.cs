using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

var game = new GameFolderScanner().Scan(args[0]);
var roomResource = game.Rooms.Single(r => r.ResourceId == 2);
var bytes = File.ReadAllBytes(roomResource.FilePath);
var room = new LoomEgaRoomDecoder().Decode(bytes);
var records = new LoomRoomObjectDirectory().Decode(bytes);
var images = new LoomRoomObjects().Decode(bytes);
var boxes = LoomWalkBoxReader.Read(bytes);
var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL")));
var costumes = new Dictionary<int,LoomCostume?>();
LoomCostume? Costume(int id)
{
    if (!costumes.TryGetValue(id,out var costume)) costumes[id] = costume = LoomCostume.Load(game,id);
    return costume;
}
foreach (var box in boxes.Boxes) Console.WriteLine($"Box {box.Index}: mask {box.Mask}, flags 0x{box.Flags:X2}; {string.Join(';',box.Corners)}");
Console.WriteLine("frame,x,y,box,costume,zPlane,visiblePixels,unmaskedPixels,removedByMask");
int frame = 0;
void Sample()
{
    if (session.Snapshot.RoomId != 2) return;
    var snapshot = session.Snapshot;
    var selection = new RoomObjectSelection(images,snapshot.ObjectStates,snapshot.ObjectPositions,records,room,drawQueue:snapshot.DrawOrder);
    var mask = selection.ComposeDepthMask(room);
    var ego = session.Actors.Single(a => a.Number == 1);
    var clipped = LoomActorRenderer.Render([ego],2,room.Width,room.Height,Costume,mask,boxes,session.ObjectClasses).Actors.SingleOrDefault();
    var plain = LoomActorRenderer.Render([ego],2,room.Width,room.Height,Costume).Actors.SingleOrDefault();
    if (clipped is not null && plain is not null)
        Console.WriteLine($"{frame},{ego.X},{ego.Y},{ego.WalkBox},{ego.Look.Costume},{clipped.ZPlane},{clipped.Pixels},{plain.Pixels},{plain.Pixels-clipped.Pixels}");
}
Sample();
for (int click = 0; click < 3 && session.Snapshot.RoomId == 2; click++)
{
    session.Click(377,9);
    for (int i = 0; i < 400 && session.Snapshot.RoomId == 2; i++)
    {
        session.AdvanceFrame(); frame++; Sample();
        if (!session.EgoMoving && !session.InCutscene && i > 32) break;
    }
}
Console.WriteLine($"Final room {session.Snapshot.RoomId}; assumed input only, no actor position or mask override.");

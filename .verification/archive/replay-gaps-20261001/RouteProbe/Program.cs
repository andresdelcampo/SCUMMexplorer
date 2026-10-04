using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var game=new GameFolderScanner().Scan(args[0]);var objects=new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0],"00.LFL"));
Directory.CreateDirectory(args[1]);
foreach(var room in new[]{13,36})
{
    var s=new LoomExplorationSession(game,objects);s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==room));
    for(int f=0;f<32;f++)s.AdvanceFrame();
    var records=new List<object>();
    if(room==13 && args.Length>2 && args[2]=="jet-response")
    {
        // Source object 325 verb 0x42 starts local 202, which sets its class24.
        // A direct response is an explorer prelude, not a simulated draft cast.
        var prelude=s.ActivateObjectVerb(325,0x42);records.Add(new{prelude});
        int quiet=0;
        for(int f=0;f<3000;f++)
        {
            if(!s.InCutscene&&!s.EgoMoving&&s.Messages.HaveMsg==0){if(++quiet>=32)break;}else quiet=0;
            s.AdvanceFrame();
        }
    }
    var boxes=s.CurrentWalkBoxes!;var target=room==13?2:9;
    var start=new{room,ego=s.Actors[0],classes=s.Snapshot.ObjectClasses,boxes=boxes.Boxes};
    // Only input: use the center of the decoded watched box. No position,
    // variable, class, inventory or direct global-script writes.
    var box=boxes.Boxes.Single(b=>b.Index==target);
    int x=box.Corners.Sum(p=>p.X)/4,y=box.Corners.Sum(p=>p.Y)/4;
    if(args.Length>2 && args[2]=="floor-offset")y+=24; // probe below the selectable target; actual input, no position write
    var action=s.Click(x,y);records.Add(new{input=new{x,y},action});
    var seen=new HashSet<int>();var reached=new HashSet<int>();int frames=0,idle=0,baseline=s.CutscenesBegun;bool ever=s.InCutscene;
    for(;frames<3000;frames++)
    {
        var ego=s.Actors[0];if(ego.WalkBox is int b)seen.Add(b);if(s.Snapshot.RoomId is int r)reached.Add(r);
        ever|=s.CutscenesBegun>baseline;
        if(ever && !s.InCutscene&&!s.EgoMoving&&s.Messages.HaveMsg==0&&!s.CameraMoving){if(++idle>=32)break;}else idle=0;
        s.AdvanceFrame();
    }
    var output=new{start,records,frames,seenBoxes=seen.Order(),rooms=reached.Order(),s.InCutscene,s.EgoMoving,ego=s.Actors[0],classes=s.Snapshot.ObjectClasses,trace=s.SessionTrace,uncertainties=s.Snapshot.Uncertainties};
    File.WriteAllText(Path.Combine(args[1],$"room-{room}.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"Room {room}: click ({x},{y}), seen boxes {string.Join(',',seen)}, scene {ever}, frame {frames}, room {s.Snapshot.RoomId}");
}

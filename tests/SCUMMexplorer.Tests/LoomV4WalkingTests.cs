using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomV4WalkingTests
{
    [Theory]
    [InlineData(120,255,107,115)]
    [InlineData(80,255,92,84)]
    [InlineData(120,128,104,108)]
    public void V4MovesOnItsFirstFrameAndKeepsTheNativeSignedFractions(int destination,int scale,int first,int second)
    {
        var actors=new LoomActorTable(useV4Walking:true);
        var place=new LoomActorTable.Placement(1,null,null,null);
        actors.SetIgnoreBoxes(1,true);
        actors.SetScale(1,scale);
        actors.Put(1,100,100,1,place,1);
        actors.Walk(1,destination,100,place,"walk");
        actors.StepWalks(place);
        Assert.Equal((first,100),(actors[1].X,actors[1].Y));
        actors.StepWalks(place);
        Assert.Equal((second,100),(actors[1].X,actors[1].Y));
        for(var n=0;n<20 && actors[1].Moving!=0;n++) actors.StepWalks(place);
        Assert.Equal((destination,100,0),(actors[1].X,actors[1].Y,actors[1].Moving));
    }

    [Fact]
    public void V4TurnsAroundTheNativeFourDirectionRingWhileMoving()
    {
        var actors = new LoomActorTable(useV4Walking: true);
        var place = new LoomActorTable.Placement(1, null, null, null);
        actors.SetIgnoreBoxes(1, true);
        actors.Put(1, 100, 100, 1, place, 1); // Initial facing is down (180).
        actors.Walk(1, 100, 80, place, "walk");
        actors.StepWalks(place);
        Assert.Equal(90, actors[1].Look.Facing); // Native negative tie turns through right.
        Assert.Equal(98, actors[1].Y);
        actors.StepWalks(place);
        Assert.Equal(0, actors[1].Look.Facing);
        Assert.Equal(96, actors[1].Y);
    }

    [Fact]
    public void V3RetainsItsTurnOnlyFirstFrame()
    {
        var actors=new LoomActorTable();
        var place=new LoomActorTable.Placement(1,null,null,null);
        actors.SetIgnoreBoxes(1,true);
        actors.Put(1,100,100,1,place,1);
        actors.Walk(1,120,100,place,"walk");
        actors.StepWalks(place);
        Assert.Equal((100,100),(actors[1].X,actors[1].Y));
    }

    [Fact]
    public void V4IntermediaryBoxIsSampledBeforeTheDestinationBox()
    {
        var boxes=new LoomWalkBoxSet([
            Box(0,100,80,100,100), Box(1,100,100,150,100), Box(2,150,100,180,100)],
            [0,0,0,1,2,1,255,0,0,0,1,1,1,2,2,2,255,0,1,1,2,2,2,255],0,false);
        var place=new LoomActorTable.Placement(1,boxes,null,null);
        var actors=new LoomActorTable(useV4Walking:true);
        actors.Put(1,100,80,1,place,1);
        actors.Walk(1,175,100,place,"walk");
        var seen=new List<int>();
        for(var n=0;n<100 && actors[1].Moving!=0;n++) {
            seen.Add(actors[1].WalkBox!.Value);
            actors.StepWalks(place);
        }
        Assert.Equal(new[]{0,1,2},seen.Distinct());
        Assert.Equal((175,100,2,0),(actors[1].X,actors[1].Y,actors[1].WalkBox,actors[1].Moving));
    }

    [Fact]
    public void V4NoRouteStopsInTheCurrentBox()
    {
        var boxes=new LoomWalkBoxSet([Box(0,100,100,120,100),Box(1,200,100,220,100)],
            [0,0,0,255,1,1,1,255],0,false);
        var place=new LoomActorTable.Placement(1,boxes,null,null);
        var actors=new LoomActorTable(useV4Walking:true);
        actors.Put(1,100,100,1,place,1);
        actors.Walk(1,220,100,place,"walk");
        for(var n=0;n<10 && actors[1].Moving!=0;n++) actors.StepWalks(place);
        Assert.Equal((100,100,0,0),(actors[1].X,actors[1].Y,actors[1].WalkBox,actors[1].Moving));
    }

    [Fact]
    public void TheCdSessionUsesNativeV4WalkingOnTheRoom34Return()
    {
        var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Games/loom vga en"));
        if(!Directory.Exists(folder)) return;
        var game=new GameFolderScanner().Scan(folder);
        var session=new LoomExplorationSession(game,game.Vga!.ReadIndexedObjects(),completeBoot:false);
        session.VisitRoomById(46);
        for(var n=0;n<3000 && !session.GameStatus.CanEdit;n++) session.AdvanceFrame();
        Assert.True(session.GameStatus.CanEdit);
        session.ActivateObjectVerb(610,0x36);
        Assert.Equal(34,session.CurrentRoomId);
        Assert.Equal((246,64,6),(session.Actors[0].X,session.Actors[0].Y,session.Actors[0].WalkBox));
        session.AdvanceFrame();
        Assert.Equal((246,65),(session.Actors[0].X,session.Actors[0].Y));
        for(var n=0;n<1000 && session.InCutscene;n++) session.AdvanceFrame();
        Assert.False(session.InCutscene);
        Assert.Equal((246,82,5),(session.Actors[0].X,session.Actors[0].Y,session.Actors[0].WalkBox));
    }

    private static LoomWalkBox Box(int index,int x1,int y1,int x2,int y2) =>
        new(index,new(x1,y1),new(x1,y1),new(x2,y2),new(x2,y2),0,0,255);
}

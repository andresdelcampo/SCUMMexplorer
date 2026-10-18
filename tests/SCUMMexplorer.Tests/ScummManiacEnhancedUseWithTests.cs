using SCUMMexplorer.Core.Maniac;
using System.Reflection;
using System.Text.Json;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedUseWithTests
{
    private static (GameLibrary Game, ScummGameSession Session)? Prepare(bool radio, ScummScriptObserver? observer = null)
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        ManiacFirstVisit.Prepare(game, session);
        if (radio)
        {
            session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2)); session.Settle();
            session.ActivateObjectVerb(142, 14); session.ActivateObjectVerb(142, 1); Frames(session, 120);
        }
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 7)); Frames(session, 10);
        session.ActivateObjectVerb(84, 14); Frames(session, 10);
        return (game, session);
    }
    private static void Frames(ScummGameSession s, int n) { for (var f=0;f<n;f++) s.AdvanceFrame(); }
    private static void Click(ScummGameSession s, int x, int y)
    { Assert.NotNull(s.ClickScreen(x,y,out var refusal)); Assert.Null(refusal); Frames(s,5); }
    private static void Verb(ScummGameSession s, int verb)
    { var item=s.PlayVerbArea().Items.Single(v=>v.VerbId==verb && v.Mode==1); Click(s,item.X+4,item.Y+3); }
    private static void Item(ScummGameSession s, int id)
    {
        var slots=s.CaptureInventoryStatus().Items.Where(i=>i.OwnerId==3).OrderBy(i=>i.Slot).ToArray();
        var index=Array.FindIndex(slots,i=>i.ObjectId==id);
        Assert.InRange(index,0,3);
        Click(s,index%2==0?8:184,s.MainScreen.Bottom+36+index/2*8);
    }

    [Fact]
    public void NativeBatteryUseConsumesItsHeldCodeAndPowersTheMovingTorch()
    {
        var source=new List<ScummScriptObservation>();
        var observer=new ScummScriptObserver(source.Add, includeSite:s=>s.Context?.Contains("Object 167 / Verb 0x0B",StringComparison.Ordinal)==true);
        if(Prepare(true,observer) is not var (game,s)) return;
        var before=s.Snapshot;
        var slots=s.CaptureInventoryStatus().Items.ToArray();
        Assert.Equal(2,slots.Single(i=>i.ObjectId==167).RetainedRoomId);
        Assert.Equal(3,before.ObjectOwners[167]);
        Verb(s,11); Item(s,167);
        Assert.Equal(1,s.Snapshot.KnownGlobalVariables[ScummVars.V2.SentencePreposition]);
        Assert.Equal("Usar pilas en",ScummTextDecoder.DecodeGlyphs(s.PlaySentenceGlyphs()));
        Item(s,84);
        Assert.Equal("Usar pilas en linterna",ScummTextDecoder.DecodeGlyphs(s.PlaySentenceGlyphs()));
        Assert.Equal(0,s.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Item(s,84);
        Assert.Equal(2,s.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Assert.Equal(0,s.Snapshot.ObjectOwners[167]);
        Assert.DoesNotContain(s.CaptureInventoryStatus().Items,i=>i.ObjectId==167);
        Assert.Equal(3,s.Snapshot.ObjectOwners[84]);
        foreach(var id in new[]{142,167,84}) Assert.Equal(before.ObjectStates[id],s.Snapshot.ObjectStates[id]);
        Assert.Equal(JsonSerializer.Serialize(before.ObjectClasses),JsonSerializer.Serialize(s.Snapshot.ObjectClasses));
        Assert.Equal(before.ObjectOwners.Where(p=>p.Key!=167),s.Snapshot.ObjectOwners.Where(p=>p.Key!=167));
        Assert.Equal(slots.Single(i=>i.ObjectId==84),s.CaptureInventoryStatus().Items.Single(i=>i.ObjectId==84));
        var start=source.First(o=>o.Site is { Stage:"instruction", Offset:0x0018 });
        var end=source.Last(o=>o.Site is { Stage:"instruction", Offset:0x0026 });
        Assert.Equal(new[]{"var[0x032]"},start.Values.Keys.Where(k=>start.Values[k]!=end.Values.GetValueOrDefault(k)).ToArray());
        Assert.Equal((ushort)84,start.Values[ScummVars.V2.ActiveObject2]);
        Verb(s,4); Item(s,84); Item(s,84); Frames(s,20);
        Assert.Equal(1,s.Snapshot.KnownGlobalVariables["var[0x09C]"]);
        s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==5)); Frames(s,20);
        s.PointAtScreen((179,79));
        var screen=new ScummPlayScreen(game,s,((ScummV2DataSource)ScummDataSources.For(game)).ReadIndexedObjects().States);
        Assert.Equal(12,s.CaptureRoom().Lights);
        Assert.Equal((6,4),s.CaptureRoom().Flashlight);
        var first=screen.Scene()!.Compose();
        Assert.Contains(first,p=>p!=0);
        s.PointAtScreen((250,100));
        Assert.False(first.SequenceEqual(screen.Scene()!.Compose()));
        Assert.DoesNotContain(s.Snapshot.Uncertainties,n=>n.Contains("getObjPreposition") || n.Contains("unsupported v2 drawSentence"));
    }

    [Fact]
    public void NativeSwitchOnWithoutBatteriesRefusesAndPreservesInventory()
    {
        if(Prepare(false) is not var (_,s)) return;
        var before=s.CaptureInventoryStatus().Items.ToArray();
        var torchState=s.Snapshot.ObjectStates[84];
        Verb(s,4); Item(s,84); Item(s,84);
        Assert.Contains(s.Messages.OnScreen,m=>m.Offset==0x0047 && m.Text.Contains("Necesita"));
        Assert.Equal(0,s.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Assert.Equal(0,s.Snapshot.KnownGlobalVariables["var[0x09C]"]);
        Assert.Equal(before,s.CaptureInventoryStatus().Items);
        Assert.Equal(torchState,s.Snapshot.ObjectStates[84]);
    }

    [Fact]
    public void SessionHeaderCopyFollowsNativeRoomInventoryAndConsumedLocations()
    {
        if(Prepare(true) is not var (game,s)) return;
        var runtime=(ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s)!;
        // The actual preparation picked radio/battery in Room2, then left for Room7.
        Assert.Equal(1,runtime.ObjectPreposition!(167));
        Assert.Equal(255,runtime.ObjectPreposition(141)); // An indexed but unloaded/unheld object is nowhere.
        Assert.True(runtime.SetObjectPreposition!(141,7)); // Missing write does not invent code.
        Assert.Equal(255,runtime.ObjectPreposition(141));
        Assert.True(runtime.SetObjectPreposition(167,6)); // Native header opcode only, isolated lifecycle unit.
        Assert.Equal(6,runtime.ObjectPreposition(167));
        Assert.Equal(1,ScummDataSources.For(game).ReadRoom(2).Objects.Single(o=>o.Id==167).Preposition);
        s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==2)); Frames(s,1);
        Assert.Equal(6,runtime.ObjectPreposition(167));
        var key=ScummDataSources.For(game).ReadRoom(2).Objects.Single(o=>o.Id==141);
        var walkY=key.WalkY;
        Assert.True(runtime.SetObjectPreposition(141,3)); // Room header before pickup.
        Assert.Equal(3,runtime.ObjectPreposition(141));
        s.ActivateObjectVerb(141,14);
        Assert.Equal(3,runtime.ObjectPreposition(141)); // Pickup copies the changed room header.
        Assert.True(runtime.SetObjectPreposition(141,4));
        Assert.Equal(4,runtime.ObjectPreposition(141));
        Assert.Equal(walkY,ScummDataSources.For(game).ReadRoom(2).Objects.Single(o=>o.Id==141).WalkY);
        Assert.Equal(key.Preposition,ScummDataSources.For(game).ReadRoom(2).Objects.Single(o=>o.Id==141).Preposition);
        // Consume the actual held batteries through their native Use response, then the header must disappear.
        Verb(s,11); Item(s,167); Item(s,84); Item(s,84);
        Assert.Equal(0,s.Snapshot.ObjectOwners[167]);
        Assert.Equal(255,runtime.ObjectPreposition(167));
    }

    [Fact]
    public void NativeUseOnWrongHeldTargetDoesNotConsumeBatteries()
    {
        var source=new List<ScummScriptObservation>();
        var observer=new ScummScriptObserver(source.Add, includeSite:site=>site.Context?.Contains("Object 167 / Verb 0x0B",StringComparison.Ordinal)==true);
        if(Prepare(true,observer) is not var (_,s)) return;
        var held=s.CaptureInventoryStatus().Items.ToArray();
        Verb(s,11); Item(s,167); Item(s,142); Item(s,142);
        Assert.Contains(source,o=>o.Site is { Stage:"instruction", Offset:0x0018 } &&
            o.Values.GetValueOrDefault(ScummVars.V2.ActiveObject2)==142);
        Assert.Equal(0,s.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Assert.Equal(3,s.Snapshot.ObjectOwners[167]);
        Assert.Equal(held,s.CaptureInventoryStatus().Items);
    }
}

using SCUMMexplorer.Core.Scumm;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Tests;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\mmansion ega en");
var data = (ScummV2DataSource)ScummDataSources.For(game);
if(args.Contains("--source"))
{
    foreach(var (room,id,verb) in new[]{(2,142,14),(2,142,1),(2,167,11),(7,84,4)})
    {
        var obj=data.ReadRoom(room).Objects.Single(obj=>obj.Id==id);
        var bytes=LflEncoding.DecodeXorFF(File.ReadAllBytes(game.Rooms.Single(resource=>resource.ResourceId==room).FilePath));
        Console.WriteLine($"Room{room} object{id}: preposition{bytes[obj.CodeOffset+12]>>5}");
        Console.WriteLine(data.ReadObjectVerb(room,obj,verb,out _).Format());
    }
    return;
}
if(args.Contains("--verify"))
{
    Console.WriteLine("Classic native Use-with acceptance; room state previews/direct pickups are disclosed exploration actions, not an earned story route.");
    var actual = new List<ScummScriptObservation>();
    var run = ManiacClassicUseWithRoute.Prepare(game.FolderPath, new(actual.Add,
        site=>site.Script is 2 or 4 || site.Context?.Contains("167")==true,
        key=>key is "var[0x008]" or "var[0x009]" or "var[0x00A]" or "var[0x032]"));
    void Require(bool ok,string why) { if(!ok) throw new InvalidOperationException(why); }
    void Show(string step) => Console.WriteLine($"{step}: {ScummTextDecoder.DecodeGlyphs(run.PlaySentenceGlyphs())}; B={run.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject1]}, C={run.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject2]}, prep={run.Snapshot.KnownGlobalVariables[ScummVars.V2.SentencePreposition]}, battery={run.Snapshot.KnownGlobalVariables["var[0x032]"]}, owner167={run.Snapshot.ObjectOwners[167]}");
    ManiacClassicUseWithRoute.Verb(run,11);
    ManiacClassicUseWithRoute.Inventory(run,167); Show("Battery selected");
    ManiacClassicUseWithRoute.Inventory(run,84); Show("Two objects selected");
    Require(run.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject1]==167 && run.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject2]==84,"Native two-object sentence not composed");
    ManiacClassicUseWithRoute.Inventory(run,84);
    ManiacClassicUseWithRoute.Frames(run,40); Show("After native execution");
    Require(run.Snapshot.KnownGlobalVariables["var[0x032]"]==2 && run.Snapshot.ObjectOwners[167]==0,"Native battery code did not install/consume");
    Require(actual.Any(observation=>observation.Site is {Script:2,Offset:0x19F,Operation:"doSentence"} && observation.Values.GetValueOrDefault("var[0x009]")==167 && observation.Values.GetValueOrDefault("var[0x00A]")==84),"Native sentence dispatch not observed");
    foreach(var observation in actual.Where(observation=>observation.Site?.Context?.Contains("167")==true))
      Console.WriteLine($"Battery source {observation.Site!.Context} +{observation.Site.Offset:X4} {observation.Site.Operation} {observation.Site.Operands}");
    ManiacClassicUseWithRoute.Verb(run,4); ManiacClassicUseWithRoute.Inventory(run,84); ManiacClassicUseWithRoute.Inventory(run,84);
    ManiacClassicUseWithRoute.Frames(run,20);
    run.VisitRoom(game.Rooms.Single(room=>room.ResourceId==5)); ManiacClassicUseWithRoute.Frames(run,20);
    run.PointAtScreen((179,79)); var lit=run.CaptureRoom();
    Console.WriteLine($"Native Switch on, dark Room5: lights={lit.Lights}, strips={lit.Flashlight}, pointer={lit.RoomPointer}");
    Require((lit.Lights&4)!=0&&(lit.Lights&2)==0,"Source Switch on did not enable dark-room beam");
    Require(!run.Snapshot.Uncertainties.Any(line=>line.Contains("unsupported v2")),"Unsupported v2 behavior remains");
    Console.WriteLine("PASS native held-inventory Use-with dispatch, battery installation/consumption and Switch-on flashlight mode.");
    return;
}
var observations = new List<ScummScriptObservation>();
var keys = new HashSet<string>(new[] {0,1,8,9,10,14,15,16,26,27,28,29,32,33,35,50,34,38,39}.Select(id => $"var[0x{id:X3}]"));
var observer = new ScummScriptObserver(observations.Add, site => site.Script is 4 or 2 || site.Context?.Contains("167") == true, keys.Contains);
var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
ManiacFirstVisit.Prepare(game, session);
void Frames(int count) { for(var frame=0;frame<count;frame++) session.AdvanceFrame(); }
void Dump(string label)
{
    Console.WriteLine(label+": room="+session.CurrentRoomId+" control="+session.PlayerHasControl+" sentence="+ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
    Console.WriteLine(string.Join(' ', session.Snapshot.KnownGlobalVariables.Where(pair=>keys.Contains(pair.Key)).Select(pair=>$"{pair.Key}={pair.Value}")));
}
session.VisitRoom(game.Rooms.Single(room=>room.ResourceId==2)); session.Settle();
session.ActivateObjectVerb(142,14); session.ActivateObjectVerb(142,1); Frames(120);
session.VisitRoom(game.Rooms.Single(room=>room.ResourceId==7)); Frames(10);
session.ActivateObjectVerb(84,14); Frames(10);
Dump("Acquired");
var ego=session.Snapshot.KnownGlobalVariables[ScummVars.V2.Ego];
var inventory=session.CaptureInventoryStatus().Items.Where(item=>item.OwnerId==ego).OrderBy(item=>item.Slot).ToArray();
Console.WriteLine("Inventory: "+string.Join(';',inventory.Select(item=>$"{item.ObjectId} native-slot{item.Slot} {item.Name}")));
void Click(int x,int y)
{
    var mark=observations.Count;
    var input=session.ClickScreen(x,y,out var refused); Console.WriteLine($"CLICK{x},{y}: {input}; {refused}");
    Frames(5); Dump("After");
    foreach(var observation in observations.Skip(mark).Where(item=>item.Site?.Stage=="instruction").Take(100))
      Console.WriteLine($"OBS {observation.Site!.Script}/{observation.Site.Context}+{observation.Site.Offset:X4} {observation.Site.Operation} {observation.Site.Operands} :: "+string.Join(' ',observation.Values.Select(pair=>$"{pair.Key}={pair.Value}")));
}
var use=session.PlayVerbArea().Items.Single(verb=>verb.VerbId==11&&verb.Mode==1);
Click(use.X+4,use.Y+3);
foreach(var id in new[]{167,84})
{
    var index=Array.FindIndex(inventory,item=>item.ObjectId==id);
    Click(index%2==0?8:184,session.MainScreen.Bottom+36+index/2*8);
}
Click(8,session.MainScreen.Bottom+4); Frames(120); Dump("Final");
Console.WriteLine("Pending: "+string.Join(';',session.PendingScriptSummary));
Console.WriteLine("Battery owner "+session.Snapshot.ObjectOwners[167]);
foreach(var script in new[]{4,2})
  Console.WriteLine(data.GlobalScript(script)!.Format());
foreach(var (room,id,verb) in new[]{(2,167,11),(7,84,4)})
  Console.WriteLine(data.ReadObjectVerb(room,data.ReadRoom(room).Objects.Single(obj=>obj.Id==id),verb,out _).Format());

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
if(args[1]=="actions")
{
 var s=ScummGameSession.BootForPlay(game,out _);var messages=new HashSet<string>();
 void Run(int n){for(var f=0;f<n;f++){foreach(var m in s.Messages.OnScreen)messages.Add(m.Text);s.AdvanceFrame();}}
 void Click(int x,int y){if(s.ClickScreen(x,y,out var why)is null)throw new Exception(why);Run(5);}
 foreach(var line in File.ReadLines(args[2]).Where(l=>!l.StartsWith('#')&&!string.IsNullOrWhiteSpace(l)))
 {
 var a=line.Split(',');Console.WriteLine($"STEP {line}");
 switch(a[0]){
 case "wait":Run(int.Parse(a[1]));break;
 case "visit":s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==int.Parse(a[1])));break;
 case "activate":s.ActivateObjectVerb(int.Parse(a[1]),int.Parse(a[2]));break;
 case "key":s.PressPlayKey(int.Parse(a[1]),out _);Run(5);break;
 case "click":Click(int.Parse(a[1]),int.Parse(a[2]));break;
 case "obj":var o=data.ReadRoom(s.CurrentRoomId!.Value).Objects.Single(o=>o.Id==int.Parse(a[1]));Click(o.X+Math.Min(4,o.Width/2)-s.ViewLeft,o.Y+Math.Min(4,o.Height/2)+s.MainScreen.Top);break;
 case "center":var c=data.ReadRoom(s.CurrentRoomId!.Value).Objects.Single(o=>o.Id==int.Parse(a[1]));Console.WriteLine($"center {c}");Click(c.X+c.Width/2-s.ViewLeft,c.Y+c.Height/2+s.MainScreen.Top);break;
 case "inv":var id=int.Parse(a[1]);var inventory=s.CaptureInventoryStatus();var ego=inventory.CurrentActorId;var items=inventory.Items.Where(i=>i.OwnerId==ego).OrderBy(i=>i.Slot).ToArray();var ix=Array.FindIndex(items,i=>i.ObjectId==id);if(ix<0||ix>3)throw new Exception($"Object{id} not among 4 slots: {string.Join(',',items.Select(i=>i.ObjectId))}");Click(ix%2==0?8:184,s.MainScreen.Bottom+36+8*(ix/2));break;
 case "verb":var v=s.PlayVerbArea().Items.Single(v=>v.VerbId==int.Parse(a[1])&&v.Mode==1);Click(v.X+Math.Min(4,v.Width/2),v.Y+3);break;
 case "sentence":Click(8,s.MainScreen.Bottom+4);break;
 case "skip":if(s.CutsceneSkippable)s.PressKey(27);else throw new Exception("Not skippable");break;
 case "untilskip":for(int f=0;f<int.Parse(a[1])&&!s.CutsceneSkippable;f++)Run(1);if(!s.CutsceneSkippable)throw new Exception("No skip reached");break;
 case "control":for(int f=0;f<int.Parse(a[1])&&!s.PlayerHasControl;f++)Run(1);if(!s.PlayerHasControl)throw new Exception("No control");break;
 case "expect-owner":if(s.Snapshot.ObjectOwners[int.Parse(a[1])]!=int.Parse(a[2]))throw new Exception(line);break;
 case "expect-var":if(s.Snapshot.KnownGlobalVariables[a[1]]!=int.Parse(a[2]))throw new Exception(line);break;
 case "expect-room":if(s.CurrentRoomId!=int.Parse(a[1]))throw new Exception(line);break;
 case "expect-control":if(!s.PlayerHasControl)throw new Exception(line);break;
 case "expect-no-unsupported":if(s.Snapshot.Uncertainties.Any(u=>u.Contains("unsupported v2")))throw new Exception(line);break;
 default:throw new ArgumentException($"Unknown action {a[0]}");
 }
 Console.WriteLine($"STATE left={s.ViewLeft} room={s.CurrentRoomId} virtual={s.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room)} cut={s.InCutscene} owner18={s.Snapshot.ObjectOwners[18]} ticket={s.Snapshot.ObjectOwners[82]} peanut={s.Snapshot.ObjectOwners[51]} vars={string.Join(';',s.Snapshot.KnownGlobalVariables.Where(v=>v.Key is "var[0x04F]" or "var[0x09B]" or "var[0x052]" or "var[0x03E]"))} sentence={ScummTextDecoder.DecodeGlyphs(s.PlaySentenceGlyphs())}");
 }
 Console.WriteLine("MESSAGES "+string.Join('|',messages));Console.WriteLine("PENDING "+string.Join(';',s.PendingScriptSummary));Console.WriteLine(string.Join('\n',s.Snapshot.Uncertainties));return;
}
if(args[1]=="run")
{
 var obs=new List<ScummScriptObservation>();
 var s=new ScummGameSession(game,((ScummV2DataSource)data).ReadIndexedObjects(),data:data,observer:new ScummScriptObserver(obs.Add,site=>site.Script is 104 or 115 or 116 or 30));
 void Run(int n){for(int f=0;f<n;f++){s.AdvanceFrame();if(f%300==0)Console.WriteLine($"frame{f}: room{s.CurrentRoomId} cut{s.InCutscene} owner51={s.Snapshot.ObjectOwners[51]} actor9={s.Actors.Single(a=>a.Number==9)} pending={string.Join(';',s.PendingScriptSummary)}");}}
 Run(150);Console.WriteLine($"bootroom={s.CurrentRoomId}");
 s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==8));Console.WriteLine($"aftervisit room={s.CurrentRoomId} varsroom={s.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room)}");
 Console.WriteLine($"initial ticket82={s.Snapshot.ObjectOwners[82]} ticket83={s.Snapshot.ObjectOwners[83]} vars={string.Join(';',s.Snapshot.KnownGlobalVariables.Where(v=>v.Key is "var[0x04F]" or "var[0x09B]" or "var[0x052]"))}");
 s.ActivateObjectVerb(190,13);Run(3000);
 foreach(var o in obs.Where(o=>o.Site?.Operation is "pickupObject" or "getActorX" or "getObjectOwner"))Console.WriteLine($"OBS {o.Site} {string.Join(';',o.Values)}");
 Console.WriteLine(string.Join('\n',s.Snapshot.Uncertainties));return;
}
if(args[1]=="room")
{
 int r=int.Parse(args[2]);var room=data.ReadRoom(r);var tables=data.ReadObjectVerbs(r,room.Objects);
 foreach(var obj in room.Objects){Console.WriteLine($"OBJECT {r}/{obj.Id} '{(room.ObjectNames.TryGetValue(obj.Id,out var n)?ScummTextDecoder.DecodeGlyphs(n):"")}' rect {obj.X},{obj.Y},{obj.Width},{obj.Height}");
 foreach(var v in tables.GetValueOrDefault(obj.Id,[])){Console.WriteLine($"VERB {v.VerbId}");foreach(var i in data.ReadObjectVerb(r,obj,v.VerbId,out _).Instructions)Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");}}
 return;
}
if(args[1]=="find")
{foreach(var id in data.GlobalScriptIds()){var s=data.GlobalScript(id);if(s is null||!s.Instructions.Any(i=>i.Operands.Contains("0x0052")||i.Operands.Contains("ticket",StringComparison.OrdinalIgnoreCase)))continue;
 Console.WriteLine($"SCRIPT {id}");foreach(var i in s.Instructions)Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");}return;}
if (args[1] == "source")
{
    foreach(var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        var records=data.ReadRoom(room);
        var verbs=data.ReadObjectVerbs(room,records.Objects);
        foreach(var obj in records.Objects)
        {
            var name=records.ObjectNames.TryGetValue(obj.Id,out var bytes)?ScummTextDecoder.DecodeGlyphs(bytes):"";
            if(name.Contains("ticket",StringComparison.OrdinalIgnoreCase)||room is 10 or 11)
                Console.WriteLine($"Room {room}, object {obj.Id} '{name}' rect {obj.X},{obj.Y},{obj.Width},{obj.Height} verbs {string.Join(',',verbs.GetValueOrDefault(obj.Id,[]).Select(v=>v.VerbId))}");
            foreach(var verb in verbs.GetValueOrDefault(obj.Id,[]))
            {
                var script=data.ReadObjectVerb(room,obj,verb.VerbId,out _);
                if(!script.Instructions.Any(i=>i.Operands.Contains("0x0033")||i.Operands.Contains("0x003F")||i.Operands.Contains("ticket",StringComparison.OrdinalIgnoreCase)))continue;
                Console.WriteLine($"OBJECT {room}/{obj.Id}/{verb.VerbId} '{name}'");
                foreach(var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
            }
        }
    }
    return;
}

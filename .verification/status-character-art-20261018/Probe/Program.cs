using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using SCUMMexplorer.Core.Maniac;
using System.Reflection;
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var session = ScummGameSession.BootForPlay(game, out _);
Console.WriteLine("BOOT "+session.CurrentRoomId+" "+session.PlayerHasControl);
if (args.Length > 1 && args[1]=="prepare") ManiacCutsceneSetup.PrepareIfNeeded(game,session);
var runtime = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
foreach(var actor in session.Actors.Take(7)) Console.WriteLine($"A {actor.Number} name={runtime.ActorNames.GetValueOrDefault(actor.Number)} costume={actor.Look.Costume} room={actor.Room} facing={actor.Look.Facing} palette={string.Join(",",actor.Look.Palette)}");
foreach(var c in session.CaptureInventoryStatus().Characters) {
 var a=session.ReadInventoryCharacterAppearance(c.ActorId,out var evidence); if(a is null){Console.WriteLine($"NO PIC {c.ActorId}");continue;}
 var display=a with {Room=1,X=96,Y=224,Visible=true,WalkBox=null,Look=a.Look with {Elevation=0,Scale=255}};
 var layer=ScummActorRenderer.Render([display],1,256,256,session.ReadCostume,engineVersion:(int)game.Version);
 var points=Enumerable.Range(0,layer.Pixels.Length).Where(layer.IsDrawn).ToArray();
 Console.WriteLine($"PIC {c.ActorId} {points.Max(i=>i%256)-points.Min(i=>i%256)+1}x{points.Max(i=>i/256)-points.Min(i=>i/256)+1} {evidence}");
}
foreach(var id in data.GlobalScriptIds()){
 var script = data.GlobalScript(id); if(script is null)continue;
 foreach(var op in script.Instructions.Where(i=>i.Operation=="actorOps" && (i.Operands.Contains("sub=0x04") ||i.Operands.Contains("sub=0x02")))) Console.WriteLine($"S{id} +{op.Offset:X4} {op.Operands}");
}
foreach(var room in game.Rooms.Where(r=>r.ResourceId.HasValue)) {
 var script=data.RoomEntryScript(room.ResourceId!.Value); if(script is null)continue;
 foreach(var op in script.Instructions.Where(i=>i.Operation=="actorOps" || (room.ResourceId==26 && i.Operation=="move")))Console.WriteLine($"R{room.ResourceId} +{op.Offset:X4} {op.Operation} {op.Operands}");
}
if(args.Length>2) { var id=int.Parse(args[2]); foreach(var op in data.GlobalScript(id)!.Instructions)Console.WriteLine($"S{id}+{op.Offset:X4} {op.Operation} {op.Operands}"); }


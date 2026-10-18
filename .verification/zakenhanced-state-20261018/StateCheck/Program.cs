using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using System.Text.Json;

var game = new GameFolderScanner().Scan(args[0]);
var session = ScummGameSession.BootForPlay(game, out _);
void Frames(int count) { for (int i=0;i<count;i++) session.AdvanceFrame(); }
void Key(char value) { if(session.PressPlayKey(value,out var error) is null) throw new Exception(error); Frames(2); }
void Click(int x,int y) { if(session.ClickScreen(x,y,out var error) is null) throw new Exception(error); Frames(2); }
Frames(150);
Key('w'); Click(50,81); Click(10,145); Frames(180);
Key('d'); Click(55,78); Click(10,145); Frames(180);
if(session.Snapshot.ObjectOwners.GetValueOrDefault(15)!=1) throw new Exception("Phone bill was not genuinely acquired");
var source=ScummDataSources.For(game).GlobalScript(108)!;
var before=session.CaptureInventoryStatus();
var moved=session.ApplyInventoryTransfers(before,[new(15,2)]);
if(!moved.Applied) throw new Exception(moved.Refusal);
var after=session.CaptureInventoryStatus();
var undone=session.UndoInventoryTransfers(moved.Undo!);
if(!undone.Applied || session.Snapshot.ObjectOwners[15]!=1) throw new Exception(undone.Refusal??"Undo did not restore Zak's bill");
Console.WriteLine(JsonSerializer.Serialize(new {
    session.CurrentRoomId, session.UserInputOn, session.InCutscene,
    Owner=session.Snapshot.ObjectOwners[15],
    FirstPlayer=session.Snapshot.KnownGlobalVariables["var[0x02A]"],
    LastPlayer=session.Snapshot.KnownGlobalVariables["var[0x02B]"],
    Before=before.Items, After=after.Items, Characters=before.Characters,
    Applied=moved.Applied, Undone=undone.Applied,
    ActorSource=source.Instructions.Where(i=>i.Operation=="actorOps" && i.Operands.Contains("name=")).Select(i=>new{i.Offset,i.Operands}),
    ReachedPickup=session.SessionTrace.Where(t=>t.Contains("objects 15 and 0") || t.Contains("objects 92 and 0"))
},new JsonSerializerOptions{WriteIndented=true}));

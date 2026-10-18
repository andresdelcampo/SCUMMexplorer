using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args[0]);
var session = ScummGameSession.BootForPlay(game, out _);
var frames = 0;
void Frames(int n) { for (var i = 0; i < n; i++) { session.AdvanceFrame(); frames++; } }
void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
void Until(Func<bool> done)
{ for (var i = 0; i < 2500 && !done(); i++) Frames(1); Require(done(), "bounded route stopped"); }
void Click(int x, int y)
{ Require(session.ClickScreen(x, y + session.MainScreen.Top, out _) is not null, "click refused"); Frames(1); }
void Key(int key) { Require(session.PressPlayKey(key, out _) is not null, "key refused"); Frames(1); }
Require(!session.CaptureInventoryStatus().CanEdit,"menu must not be editable");
foreach (var (x,y) in new[] {(76,104),(196,104),(292,60)}) { Click(x,y); Frames(100); }
Until(() => session.CurrentRoomId == 44 && session.CutsceneSkippable);
Require(!session.CaptureInventoryStatus().CanEdit,"cutscene must not be editable");
session.PressKey(27); Until(() => session.PlayerHasControl);
Click(8,120); Until(() => session.CurrentRoomId == 1); Frames(120);
for (var i = 0; i < 2; i++) { Click(8,120); Frames(200); }
Key('d'); Click(376-session.ViewLeft,84); Click(376-session.ViewLeft,84);
Until(() => (session.Snapshot.ObjectStates[54] & 8) != 0);
Key('d'); Click(416-session.ViewLeft,84); Click(416-session.ViewLeft,84);
Until(() => session.Snapshot.ObjectOwners[55] == 3);
Until(() => session.CaptureInventoryStatus().CanEdit);
var view = session.CaptureInventoryStatus();
Console.WriteLine($"Native route: {frames} frames, room {view.RoomId}, actor {view.CurrentActorId}, inputs {session.DeliveredInput.Count}");
foreach (var actor in view.Characters) Console.WriteLine($"CHARACTER {actor.ActorId} {actor.Name}: {actor.Evidence}");
foreach (var item in view.Items) Console.WriteLine($"ITEM {item}");
var before = session.Snapshot;
var actors = JsonSerializer.Serialize(session.Actors);
var apply = session.ApplyInventoryTransfers(view,[new(55,1)]);
Require(apply.Applied,apply.Refusal ?? "apply failed");
Require(session.Snapshot.ObjectOwners[55] == 1,"owner not changed");
Require(session.UndoInventoryTransfers(apply.Undo!).Applied,"undo failed");
Require(view.Items.SequenceEqual(session.CaptureInventoryStatus().Items),"held metadata changed");
Require(before.KnownGlobalVariables.OrderBy(x=>x.Key).SequenceEqual(session.Snapshot.KnownGlobalVariables.OrderBy(x=>x.Key)),"words changed");
Require(before.ObjectStates.OrderBy(x=>x.Key).SequenceEqual(session.Snapshot.ObjectStates.OrderBy(x=>x.Key)),"states changed");
Require(before.ObjectClasses.OrderBy(x=>x.Key).SequenceEqual(session.Snapshot.ObjectClasses.OrderBy(x=>x.Key)),"classes changed");
Require(actors == JsonSerializer.Serialize(session.Actors),"actors changed");
foreach (var line in session.SessionTrace.Where(x=>x.StartsWith("Explicit inventory "))) Console.WriteLine(line);
Console.WriteLine("PASS: key55 owner3->1->3; slot/retainedroom/name unchanged; all words/states/classes/actors unchanged.");
var data = ScummDataSources.For(game);
foreach (var id in new[] { 1, 4 })
    foreach (var op in data.GlobalScript(id)!.Instructions.Where(x=>x.Operation=="actorOps" && x.Operands.Contains("name=") || x.Operation=="move" && x.Operands.StartsWith("var[0x000],")))
        Console.WriteLine($"SOURCE Script{id} +{op.Offset:X4}: {op.Operation} {op.Operands}");

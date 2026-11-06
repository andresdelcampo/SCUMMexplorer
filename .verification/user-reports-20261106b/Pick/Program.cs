using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: Pick <game folder> <room> <object> <verb hex> [frames, default 200] [trace filter]
// 6 November 2026 (user: a door opened from Interact does not show open): the room picked as the app does, the verb picked as Interact does, then the object's state
// and the trace lines of the pick.
var game = new GameFolderScanner().Scan(args[0]);
var boot = ScummGameSession.BootForPlay(game, out _);
ScummCutscenePlanner.IntoControl(boot);
var session = (ScummGameSession)ScummBootSnapshot.Of(boot).Start();
var room = game.Rooms.First(r => r.ResourceId == int.Parse(args[1]));
session.VisitRoomAsPicked(room);
var (objectId, verb) = (int.Parse(args[2]), Convert.ToInt32(args[3], 16));
var frames = args.Length > 4 ? int.Parse(args[4]) : 200;
for (var i = 0; i < 30; i++) session.StepFrame();
Console.WriteLine($"before: room {session.CurrentRoomId} state {session.Snapshot.ObjectStates.GetValueOrDefault(objectId)} control {session.PlayerHasControl}");
var mark = session.SessionTrace.Count;
session.BringObjectIntoRoom(objectId);
session.PutHeroAtObject(objectId);
session.PickInteraction(objectId, verb);
for (var i = 0; i < frames; i++)
{
    session.StepFrame();
    if (i < 5 || i % 40 == 0) Console.WriteLine($"  frame {i}: state {session.Snapshot.ObjectStates.GetValueOrDefault(objectId)} cutscene {session.InCutscene}");
}
Console.WriteLine($"after: room {session.CurrentRoomId} state {session.Snapshot.ObjectStates.GetValueOrDefault(objectId)}");
var filter = args.Length > 5 ? args[5] : null;
foreach (var line in session.SessionTrace.Skip(mark).Where(l => !l.Contains("delay") && (filter is null || l.Contains(filter))).Take(60)) Console.WriteLine("  " + (line.Length > 180 ? line[..180] : line));
foreach (var line in session.Snapshot.Uncertainties.TakeLast(4)) Console.WriteLine("  ? " + (line.Length > 200 ? line[..200] : line));

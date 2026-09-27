using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Usage: VisitProbe <game folder> <step>...
// A step is "R" (visit Room R from the catalog) or "R/O/V" (visit Room R, then
// activate object O's verb V, in hex). After each action, pending scripts are
// continued for up to 8 frames, as a doorway click does in the app. Prints the
// session trace, reached events and uncertainties added by each step.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var session = new LoomExplorationSession(game, index);
var uncertainties = 0;
var trace = 0;

void Show(string label)
{
    var snapshot = session.Snapshot;
    Console.WriteLine($"== {label}: room {snapshot.RoomId:00}, pending {snapshot.PendingScripts}");
    foreach (var line in session.SessionTrace.Skip(trace)) Console.WriteLine("   trace: " + line);
    foreach (var line in snapshot.Uncertainties.Skip(uncertainties)) Console.WriteLine("   uncertain: " + line);
    trace = session.SessionTrace.Count;
    uncertainties = snapshot.Uncertainties.Count;
}

foreach (var step in args.Skip(1))
{
    var parts = step.Split('/');
    var roomId = int.Parse(parts[0]);
    if (session.Snapshot.RoomId != roomId)
    {
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == roomId));
        Show($"visit Room {roomId:00}");
    }
    if (parts.Length < 3) continue;
    var objectId = int.Parse(parts[1]);
    var verb = Convert.ToInt32(parts[2], 16);
    var action = session.ActivateObjectVerb(objectId, verb);
    Console.WriteLine($"   action: {action.Evidence}");
    Show($"object {objectId} verb 0x{verb:X2}");
    for (var frame = 1; frame <= 8 && session.Snapshot.PendingScripts > 0 && session.Snapshot.RoomId == roomId; frame++)
    {
        session.AdvanceFrame();
        Show($"frame {frame}");
        if (session.Snapshot.RoomId != roomId)
            Console.WriteLine($"   entered Room {session.Snapshot.RoomId:00}: {session.LastAction?.Evidence}");
    }
}

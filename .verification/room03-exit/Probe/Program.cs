using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Runs the bounded session through Room 03: catalog visit, one hotspot verb,
// frame advances until a room load is dispatched, then the reverse verb in the
// destination. Prints what the session reached and every uncertainty added.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var room03 = game.Rooms.Single(r => r.ResourceId == 3);

void Show(LoomExplorationSession session, string label, int uncertaintiesBefore, int traceBefore)
{
    var snapshot = session.Snapshot;
    var bits = snapshot.KnownGlobalVariables;
    Console.WriteLine($"== {label}: room {snapshot.RoomId:00}, pending {snapshot.PendingScripts}, " +
                      $"bit[0xD62]={(bits.GetValueOrDefault("var[0x0D6]") >> 2) & 1}, " +
                      $"bit[0xD6E]={(bits.GetValueOrDefault("var[0x0D6]") >> 14) & 1}, " +
                      $"bit[0xD7A]={(bits.GetValueOrDefault("var[0x0D7]") >> 10) & 1}, var[0x003]={bits.GetValueOrDefault("var[0x003]")}");
    foreach (var line in session.SessionTrace.Skip(traceBefore)) Console.WriteLine("   session: " + line);
    foreach (var line in snapshot.Uncertainties.Skip(uncertaintiesBefore)) Console.WriteLine("   uncertainty: " + line);
}

void Route(int objectId, int verbId, int? reverseObject, int? reverseVerb)
{
    Console.WriteLine();
    Console.WriteLine($"######## Room 03 object {objectId} verb 0x{verbId:X2}");
    var session = new LoomExplorationSession(game, index);
    session.VisitRoom(room03);
    Show(session, "Room 03 catalog visit", 0, 0);
    Console.WriteLine("   entry script 42 started: " + session.LastVisitEvents.Any(e =>
        e.Kind == LoomScriptEventKind.ScriptStarted && e.TargetId == 0x2A));
    int u = session.Snapshot.Uncertainties.Count, t = session.SessionTrace.Count;
    var action = session.ActivateObjectVerb(objectId, verbId);
    Console.WriteLine($"   action: {action.Evidence}");
    Show(session, "after action", u, t);
    for (var frame = 1; frame <= 3 && session.Snapshot.RoomId == 3; frame++)
    {
        u = session.Snapshot.Uncertainties.Count; t = session.SessionTrace.Count;
        session.AdvanceFrame();
        Show(session, $"frame {frame}", u, t);
    }
    Console.WriteLine($"   last action: {session.LastAction?.Evidence}");
    if (reverseObject is not int back || session.Snapshot.RoomId == 3) return;
    u = session.Snapshot.Uncertainties.Count; t = session.SessionTrace.Count;
    var reverse = session.ActivateObjectVerb(back, reverseVerb!.Value);
    Console.WriteLine($"   reverse action: {reverse.Evidence}");
    Show(session, "after reverse action", u, t);
    var from = session.Snapshot.RoomId;
    for (var frame = 1; frame <= 3 && session.Snapshot.RoomId == from; frame++)
    {
        u = session.Snapshot.Uncertainties.Count; t = session.SessionTrace.Count;
        session.AdvanceFrame();
        Show(session, $"reverse frame {frame}", u, t);
    }
    Console.WriteLine($"   last action: {session.LastAction?.Evidence}");
}

Route(162, 0x36, 167, 0x38);
Route(161, 0x36, null, null);
Route(163, 0x36, null, null);
Route(164, 0x38, null, null);

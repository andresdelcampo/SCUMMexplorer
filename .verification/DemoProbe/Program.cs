using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// Usage: DemoProbe <game folder> [index | play <frames>]
if (args.Length > 1 && args[1] == "index") { IndexDump.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "play") { PlayDump.Run(args[0], args.Length > 2 ? int.Parse(args[2]) : 3000); return; }
// What the existing code makes of a LOOM folder: identity, resources, every script decoded, every room opened in a session, the boot.
var game = new GameFolderScanner().Scan(args[0]);
Console.WriteLine($"{game.Title}: id {game.Id}, version {game.Version}, rooms {game.Rooms.Count()}, resources {game.Resources.Count} ({string.Join(", ", game.Resources.GroupBy(r => r.Kind).Select(g => $"{g.Count()} {g.Key}"))})");
var scripts = 0; var scriptFail = new List<string>();
var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script))
{
    try { reader.Decode(r); scripts++; } catch (Exception ex) { scriptFail.Add($"script {r.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
}
Console.WriteLine($"scripts decoded {scripts}, failed {scriptFail.Count}");
foreach (var f in scriptFail.Take(8)) Console.WriteLine("  " + f);
var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
LoomExplorationSession session;
try { session = new LoomExplorationSession(game, snapshot); }
catch (Exception ex) { Console.WriteLine("session: " + ex); return; }
Console.WriteLine($"session created; boot running {session.BootRunning}, room {session.Snapshot.RoomId}");
var frames = 0;
try
{
    while (session.BootRunning && frames < 3000) { session.AdvanceFrame(); frames++; }
    Console.WriteLine($"boot ended after {frames} frames in room {session.Snapshot.RoomId}; uncertainties {session.Snapshot.Uncertainties.Count}");
}
catch (Exception ex) { Console.WriteLine($"boot frames {frames}: {ex.GetType().Name}: {ex.Message}"); }
var ok = 0; var failed = new List<string>();
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
{
    try { session.VisitRoomById(room.ResourceId!.Value); ok++; }
    catch (Exception ex) { failed.Add($"room {room.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
}
Console.WriteLine($"rooms visited {ok}, failed {failed.Count}");
foreach (var f in failed.Take(10)) Console.WriteLine("  " + f);
Console.WriteLine("room ids: " + string.Join(" ", game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId)));

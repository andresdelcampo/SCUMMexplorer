using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

var game = new GameFolderScanner().Scan(args[0]);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var room = game.Rooms.Single(r => r.ResourceId == 36);
var records = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(room.FilePath));
var session = new LoomExplorationSession(game, index);
session.VisitRoom(room);
for (int i = 0; i < 32; i++) session.AdvanceFrame();
foreach (var record in records.Where(r => r.X <= 465 && r.X + r.Width > 465 && r.Y <= 1 && r.Y + r.Height > 1))
    Console.WriteLine($"Hit box {record.Id}: {record}; class mask 0x{session.ObjectClasses.GetValueOrDefault(record.Id):X8}");
var exit = records.Single(r => r.Id == 599);
Console.WriteLine($"Exit 599: {exit}; class mask 0x{session.ObjectClasses.GetValueOrDefault(599):X8}");
Console.WriteLine($"Moved exit position: {session.Snapshot.ObjectPositions.GetValueOrDefault(599)}");
var action = session.Click(465,1);
Console.WriteLine(action.Evidence);
Console.WriteLine($"Selected object var071: {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x071]")}");
for (int i = 0; i < 250 && session.Snapshot.RoomId == 36; i++) session.AdvanceFrame();
Console.WriteLine($"After walk: room {session.Snapshot.RoomId}, ego {session.Actors.First(a => a.Number == 1)}");
foreach (var line in session.SessionTrace.TakeLast(15)) Console.WriteLine(line);

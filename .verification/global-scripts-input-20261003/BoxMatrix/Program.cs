using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
var b = s.CurrentWalkBoxes!;
for (int f = 4; f <= 10; f++) { Console.Write($"from {f}:"); for (int t = 0; t <= 11; t++) Console.Write($" {t}->{b.GetNextBox(f, t)}"); Console.WriteLine(); }
for (int i = 0; i < b.Count; i++) Console.WriteLine($"box {i} flags {b.GetFlags(i):X2}");

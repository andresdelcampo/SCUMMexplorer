using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
var steps = new[] { new LoomReplayStep("script", ObjectId: 200, X: 0x35),
    new LoomReplayStep("scene", EndBoundary: "endCutscene at Room 34 local script 200 +0x0056", ExpectedRoom: 34) };
var seq = new LoomReplaySequence(s, steps);
int frames = 0; var cam = new List<int>();
while (seq.Continue() && frames < 3000) { s.AdvanceFrame(); seq.FrameAdvanced(); frames++; if (frames % 100 == 0) cam.Add(s.CameraX ?? -1); }
Console.WriteLine($"complete={seq.Complete} failure={seq.Failure} frames={frames} room={s.Snapshot.RoomId} camera samples={string.Join(",", cam)}");
foreach (var t in s.SessionTrace.Where(t => t.Contains("script 200") || t.Contains("utscene"))) Console.WriteLine(t);
foreach (var u in s.Snapshot.Uncertainties.Take(6)) Console.WriteLine("U: " + u);

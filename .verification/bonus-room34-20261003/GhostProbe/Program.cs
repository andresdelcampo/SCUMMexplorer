using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 36));
var steps = new LoomReplayStep[] {
    new("verb", 599, 56), new("frames", Frames: 32), new("click", X: 109, Y: 102),
    new("scene", EndBoundary: "endCutscene at Script 86 +0x055C", ExpectedRoom: 38),
    new("verb", 610, 54), new("scene", EndBoundary: "endCutscene at Script 87 +0x09B9", ExpectedRoom: 38),
    new("visit", 34), new("frames", Frames: 40),
    new("verb", 559, 55), new("scene", EndBoundary: "endCutscene at Script 98 +0x0618", ExpectedRoom: 34) };
var seq = new LoomReplaySequence(s, steps);
int frames = 0;
void Show(string label)
{
    var snap = s.Snapshot;
    Console.WriteLine($"[{label}] frame {frames} room {snap.RoomId} stage {seq.Stage}");
    foreach (var a in s.Actors.Where(a => a.Room == 34)) Console.WriteLine($"  actor {a.Number} at ({a.X},{a.Y}) visible {a.Visible} costume {a.Look?.Costume} frame {a.Look?.Frame}");
    foreach (var id in new[] { 561, 586 }) Console.WriteLine($"  obj {id} untouchable {snap.IsUntouchable(id)}");
}
int lastStage = -1;
while (seq.Continue() && frames < 6000)
{
    if (seq.Stage != lastStage) { lastStage = seq.Stage; if (seq.Stage == 8) Show("before 098"); }
    s.AdvanceFrame(); seq.FrameAdvanced(); frames++;
}
Console.WriteLine($"complete={seq.Complete} failure={seq.Failure} frames={frames} room={s.Snapshot.RoomId}");
Show("end");

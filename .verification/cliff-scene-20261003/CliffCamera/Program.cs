using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var cat = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var e = cat.GetProperty("replays").EnumerateArray().Single(r => r.GetProperty("id").GetString() == args[2]);
var steps = e.GetProperty("steps").Deserialize<LoomReplayStep[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == e.GetProperty("room").GetInt32()));
var seq = new LoomReplaySequence(s, steps);
var last = -1; var f = 0;
string Ego() { var a = s.Actors.Single(x => x.Number == 1); return $"ego ({a.X},{a.Y}) box {a.WalkBox} costume {a.Look?.Costume} facing {a.Look?.Facing}"; }
for (; f < 6000 && seq.Continue(); f++)
{
    s.AdvanceFrame(); seq.FrameAdvanced();
    if (s.CameraX != last) { if (f < 6 || (s.CameraX is int c && c % 32 == 0) || f > 100) Console.WriteLine($"f{f}: stage {seq.Stage} camera {s.CameraX} {Ego()}"); last = s.CameraX ?? -1; }
}
Console.WriteLine($"frames={f} complete={seq.Complete} failure={seq.Failure}; final {Ego()} camera {s.CameraX}");
Console.WriteLine(string.Join(Environment.NewLine, s.Snapshot.Uncertainties.Where(u => u.Contains("Explorer setup")).Take(2)));

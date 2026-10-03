using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var cat = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
foreach (var id in new[] { "sequence-087-098", "sequence-086-087" })
{
    var e = cat.GetProperty("replays").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
    var steps = e.GetProperty("steps").Deserialize<LoomReplayStep[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    int silent = e.GetProperty("silentSteps").GetInt32();
    var s = new LoomExplorationSession(game, objects);
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == e.GetProperty("room").GetInt32()));
    var seq = new LoomReplaySequence(s, steps);
    int silentFrames = 0;
    bool ok = seq.RunSilently(silent);
    string Ego() => $"room {s.Snapshot.RoomId} ego costume {s.Actors.Single(a => a.Number == 1).Look?.Costume}, actor3 {s.Actors.Single(a => a.Number == 3).Look?.Costume}";
    Console.WriteLine($"{id}: silent ok={ok} stage={seq.Stage}; {Ego()}");
    int frames = 0; var costumes = new SortedSet<int>();
    while (seq.Continue() && frames < 6000) { s.AdvanceFrame(); seq.FrameAdvanced(); frames++; if (s.Actors.Single(a => a.Number == 1).Look?.Costume is int c) costumes.Add(c); }
    Console.WriteLine($"  visible: complete={seq.Complete} failure={seq.Failure} frames={frames} ego costumes seen {string.Join(",", costumes)}; {Ego()}");
}

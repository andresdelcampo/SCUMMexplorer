using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var recipes = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var results = new List<object>();
foreach (var recipe in recipes.EnumerateArray())
{
    var session = new LoomExplorationSession(game, objects);
    int room = recipe.GetProperty("room").GetInt32();
    var rooms = new HashSet<int> { room };
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
    int trace = session.SessionTrace.Count;
    var steps = JsonSerializer.Deserialize<LoomReplayStep[]>(recipe.GetProperty("steps"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    var sequence = new LoomReplaySequence(session, steps);
    int frames = 0;
    var stages = new List<object>(); int previous = -1;
    while (sequence.Continue())
    {
        if (sequence.Stage != previous) { stages.Add(new { stage = sequence.Stage, frames, room = session.Snapshot.RoomId }); previous = sequence.Stage; }
        if (++frames > steps.Sum(s => s.Frames)) throw new InvalidOperationException("Total frame bound exceeded");
        if (session.Snapshot.RoomId is int observed) rooms.Add(observed);
        session.AdvanceFrame(); sequence.FrameAdvanced();
    }
    if (session.Snapshot.RoomId is int final) rooms.Add(final);
    if (!sequence.Complete) throw new InvalidOperationException(sequence.Failure);
    var boundaries = session.SessionTrace.Skip(trace).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    results.Add(new { id = recipe.GetProperty("id").GetString(), startRoom = room, rooms = rooms.Order().ToArray(), frames, stages, boundaries,
        uncertainties = session.Snapshot.Uncertainties, trace = session.SessionTrace.Skip(trace).ToArray() });
    Console.WriteLine($"{recipe.GetProperty("id")}: {frames} frames, rooms {string.Join(',', rooms.Order())}, {boundaries.Length} boundaries");
}
var files = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    .Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], JsonSerializer.Serialize(new { sourceFiles = files, results }, new JsonSerializerOptions { WriteIndented = true }));

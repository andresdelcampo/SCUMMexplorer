using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// Audit <game folder> <out folder> <loom-replays.json> [scripts|scenes|interactions|all]
// Missing data is a hard error. A started scene is not a completed scene. All starts are isolated seed-0 previews.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var glossary = LoomGlossary.Load(game);
var presenter = new LoomScriptPresenter(glossary);
var output = args[1]; Directory.CreateDirectory(output);
var mode = args.Length > 3 ? args[3] : "all";
var json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, json));
var clock = Stopwatch.StartNew();
var indexed = game.V4Archive?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
LoomExplorationSession Fresh() => new(game, indexed, completeBoot: game.Id == GameId.Loom);
var rooms = game.Rooms.Select(r => r.ResourceId!.Value).ToArray();
var selectedRooms = mode is not ("dump" or "anomalies") && args.Length > 4 ? args.Skip(4).Select(int.Parse).ToHashSet() : rooms.ToHashSet();
var errors = new List<string>();

if (mode == "dump")
{
    foreach (var room in args.Skip(4).Select(int.Parse))
    {
        var rd = data.ReadRoom(room); var lines = new List<string>();
        foreach (var id in data.RoomLocalScriptIds(room)) lines.Add(data.RoomLocalScript(room, id)!.Format(false));
        foreach (var (obj, entries) in data.ReadObjectVerbs(room, rd.Objects))
        foreach (var entry in entries.Where(e => !e.IsFallback))
                lines.Add(data.ReadObjectVerb(room, rd.Objects.Single(o => o.Id == obj), entry.VerbId, out _).Format(false));
        File.WriteAllLines(Path.Combine(output, $"room{room}.txt"), lines);
    }
}

if (mode is "scripts" or "all")
{
    var blocks = new List<object>(); int complete = 0, decoded = 0;
    void Read(string place, Func<ScummScriptDisassembly?> read)
    {
        try
        {
            if (read() is not { } script) return;
            var readable = presenter.Present(script); var raw = script.Format();
            if (string.IsNullOrWhiteSpace(readable) || string.IsNullOrWhiteSpace(raw)) throw new InvalidDataException("Empty presentation");
            decoded++; if (script.HasCompleteControlFlow) complete++;
            blocks.Add(new { place, script.CodeLength, script.DecodedBytes, script.DecodeNote, script.Issues });
        }
        catch (Exception ex) { errors.Add(place + ": " + ex.Message); }
    }
    foreach (var id in data.GlobalScriptIds()) Read($"global {id}", () => data.GlobalScript(id));
    foreach (var room in rooms)
    {
        Read($"room {room} entry", () => data.RoomEntryScript(room));
        Read($"room {room} exit", () => data.RoomExitScript(room));
        foreach (var id in data.RoomLocalScriptIds(room)) Read($"room {room} local {id}", () => data.RoomLocalScript(room, id));
        var rd = data.ReadRoom(room);
        foreach (var (id, entries) in data.ReadObjectVerbs(room, rd.Objects))
            foreach (var entry in entries)
                Read($"room {room} object {id} verb {entry.VerbId:X2}", () => data.ReadObjectVerb(room, rd.Objects.Single(o => o.Id == id), entry.VerbId, out _));
    }
    Save("scripts", new { edition = game.Id.ToString(), decoded, complete, errors, blocks });
    Console.WriteLine($"Scripts: {decoded} blocks, {complete} complete control-flow decodes, {errors.Count} read/format errors.");
}

Probe Play(string id, int room, int obj, int verb, Func<LoomExplorationSession, (int Begun, int Trace)>? setup, bool escape,
    LoomReplayStep[]? steps = null, int[]? expectedRooms = null, int maxFrames = 6000)
{
    try
    {
        var s = Fresh(); var baseline = s.CutscenesBegun; var trace = s.SessionTrace.Count;
        s.VisitRoomById(room);
        if (setup is not null) (baseline, trace) = setup(s);
        // An entry can leave a cutscene open when a direct verb is sent. Count its start as well as its later end.
        // Keep the object's starts separately so an ambient entry cannot masquerade as the target scene.
        var targetBaseline = s.CutscenesBegun;
        LoomReplaySequence? sequence = steps is null ? null : new(s, steps);
        if (sequence is null && obj != 0) s.ActivateObjectVerb(obj, verb);
        var visited = new HashSet<int>(); var uncertainty = new HashSet<string>(); int frames = 0, quiet = 0, skips = 0;
        for (; frames < maxFrames; frames++)
        {
            if (s.Snapshot.RoomId is int now) visited.Add(now);
            foreach (var note in s.Snapshot.Uncertainties) uncertainty.Add(note);
            var busy = s.InCutscene || s.Messages.HaveMsg != 0 || s.EgoMoving || s.CameraMoving;
            quiet = busy ? 0 : quiet + 1;
            if (sequence is null && quiet >= 90 && (s.CutscenesBegun > baseline || frames >= 300)) break;
            if (sequence is not null && !sequence.Continue()) break;
            if (escape && s.CutsceneSkippable) { s.PressKey(0x1B); skips++; }
            s.AdvanceFrame(); sequence?.FrameAdvanced();
        }
        var boundaries = s.SessionTrace.Skip(trace).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
        var begun = s.CutscenesBegun - baseline; var ended = boundaries.Count(t => t.StartsWith("endCutscene at "));
        var targetBegun = obj == 0 ? begun : s.CutscenesBegun - targetBaseline;
        var unsupported = uncertainty.Where(t => t.Contains("unsupported", StringComparison.OrdinalIgnoreCase) || t.Contains("unmodelled", StringComparison.OrdinalIgnoreCase)).ToArray();
        var missingRooms = (expectedRooms ?? []).Except(visited).ToArray();
        var status = sequence?.Failure is not null ? "sequence-failed" : sequence?.Complete == true ? "sequence-completed" :
            frames >= maxFrames ? "timeout" : begun == 0 ? "no-scene" : begun != ended || s.InCutscene ? "unbalanced" : targetBegun == 0 ? "entry-only" :
            unsupported.Length > 0 ? "unsupported" : missingRooms.Length > 0 ? "rooms-changed" : "completed";
        return new(id, room, obj, verb, escape, status, frames, begun, ended, skips, s.Snapshot.RoomId, visited.Order().ToArray(),
            missingRooms, sequence?.Failure, unsupported, boundaries, uncertainty.ToArray(), null, targetBegun);
    }
    catch (Exception ex) { return new(id, room, obj, verb, escape, "error", 0, 0, 0, 0, null, [], [], null, [], [], [], ex.ToString()); }
}

if (mode is "scenes" or "all")
{
    var probes = new List<Probe>();
    void Run(string id, int room, int obj, int verb, Func<LoomExplorationSession, (int Begun, int Trace)>? setup, LoomReplayStep[]? steps = null, int[]? expected = null)
    {
        foreach (var escape in new[] { false, true })
        {
            var result = Play(id, room, obj, verb, setup, escape, steps, escape ? null : expected, steps is null ? 6000 : 40000);
            probes.Add(result);
            Save("scenes", probes);
        }
        if (probes.Count % 20 == 0) Console.WriteLine($"Scenes: {probes.Count / 2} starts ({clock.Elapsed.TotalSeconds:F1}s), last {id}: {probes[^2].Status}/{probes[^1].Status}");
    }
    if (game.Id == GameId.LoomVga)
    {
        var triggers = LoomCdCutsceneIndex.Scan(data, rooms);
        var groups = LoomCdCutsceneIndex.Group(triggers);
        var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
        var setups = new List<object>();
        foreach (var group in groups.Where(g => selectedRooms.Contains(g.First.Room)))
        {
            var t = group.First; var setup = planner.Derive(t);
            var id = $"room {t.Room} object {t.ObjectId} verb {t.VerbId:X2}";
            setups.Add(new { id, group.IsScene, setup.Plays, setup.Before, setup.After, setup.Notes });
            // Use precisely the planner's supported start. An unplayable trigger is still probed and reported.
            Run(id, t.Room, t.ObjectId, t.VerbId, setup is { Plays: true, HasSteps: true } ? s => {
                LoomCdSceneSetupPlanner.ApplyBefore(s, game, setup.Before);
                var marker = (s.CutscenesBegun, s.SessionTrace.Count);
                s.VisitRoomById(t.Room);
                LoomCdSceneSetupPlanner.ApplyAfter(s, setup.After);
                return marker;
            } : null);
        }
        Save("setups", new { triggers = triggers.Count, groups = groups.Count, setups });
    }
    else
    {
        var catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(args[2]), json)!;
        var mismatches = catalog.SourceFiles.Where(f => !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(game.FolderPath, f.Name)))).Equals(f.Sha256, StringComparison.OrdinalIgnoreCase)).Select(f => f.Name).ToArray();
        Save("catalog", new { matches = mismatches.Length == 0, mismatches, catalog.Summary, catalog.Coverage });
        if (mismatches.Length != 0) throw new InvalidDataException("Replay catalog fingerprint does not match this edition.");
        var planner = new LoomSceneSetupPlanner(game);
        foreach (var r in catalog.Replays.Where(r => selectedRooms.Contains(r.Room)))
        {
            var setup = r.Steps is null && r.ObjectId != 0 ? planner.Derive(r.Room, r.ScriptIds, r.ObjectId, r.Verb) : LoomStartSetup.None;
            Run(r.Id, r.Room, r.ObjectId, r.Verb, setup.HasSteps ? s => {
                var prelude = new LoomReplaySequence(s, setup.Steps);
                if (!prelude.RunSilently(setup.Steps.Count)) throw new InvalidDataException("Start setup failed: " + prelude.Failure);
                return (s.CutscenesBegun, s.SessionTrace.Count);
            } : null, r.Steps, r.Rooms);
        }
    }
    Console.WriteLine("Scenes: " + string.Join(", ", probes.GroupBy(p => (p.Escape, p.Status)).Select(g => $"{g.Key}: {g.Count()}")));
}

if (mode is "interactions" or "all")
{
    var probes = new List<Probe>();
    foreach (var room in rooms.Where(selectedRooms.Contains))
    {
        var rd = data.ReadRoom(room);
        foreach (var (obj, entries) in data.ReadObjectVerbs(room, rd.Objects))
            foreach (var entry in entries.Where(e => !e.IsFallback))
                probes.Add(Play($"room {room} object {obj} verb {entry.VerbId:X2}", room, obj, entry.VerbId, null, false, maxFrames: 1200));
        Save("interactions", probes);
        Console.WriteLine($"Interactions: through room {room}: {probes.Count} direct verbs ({clock.Elapsed.TotalSeconds:F1}s)");
    }
    Console.WriteLine("Interactions: " + string.Join(", ", probes.GroupBy(p => p.Status).Select(g => $"{g.Key}: {g.Count()}")));
}

if (mode == "anomalies")
{
    var earlier = JsonSerializer.Deserialize<Probe[]>(File.ReadAllText(args[4]), json)!;
    var probes = new List<Probe>();
    foreach (var r in earlier.Where(p => p.Status is "timeout" or "unbalanced" or "entry-only" or "error"))
    {
        var result = Play(r.Id, r.Room, r.Object, r.Verb, null, false, maxFrames: 6000);
        probes.Add(result);
        Console.WriteLine($"{result.Id}: {result.Status}, {result.Frames} frames");
    }
    Save("anomalies", probes);
}

record SourceFile(string Name, string Sha256);
record Recipe(string Id, int Room, int ObjectId, int Verb, int[] ScriptIds, int[] Rooms, LoomReplayStep[]? Steps);
record Catalog(SourceFile[] SourceFiles, Recipe[] Replays, JsonElement[] Coverage, string Summary);
record Probe(string Id, int Room, int Object, int Verb, bool Escape, string Status, int Frames, int Begun, int Ended, int Skips,
    int? EndRoom, int[] Visited, int[] MissingRooms, string? Failure, string[] Unsupported, string[] Boundaries, string[] Uncertainties, string? Error, int TargetBegun = 0);

using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

// Usage: CdSeed <LOOM CD folder or .cue> <output loom-cd-scenes.json>
// Makes the shipped seed of the CD's room lists: for each scanned group the rooms its headless run entered (from its derived start setup; the intro's rooms under
// "intro"), tied to the fingerprint of the disc's data. The app uses it only when the fingerprint of the opened game matches, and derives them live otherwise.
// Categories are not part of the seed: the run says 19 of the static rule's 98 scenes stay in their room, some of which leave it in the validated EGA game (Room 34's
// sleep reactions go to Room 52 there), so the CD keeps its static rule until the run is checked against the real game. The differences are printed.
var game = new GameFolderScanner().Scan(args[0]);
var vga = game.V4Archive ?? throw new InvalidOperationException("Not a LOOM CD game.");
var data = ScummDataSources.For(game);
var triggers = LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value).ToList(), LoomCutsceneTitles.Doorways(game), game);
var groups = LoomCutsceneIndex.Group(triggers);
var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
var rooms = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
var scenes = new SortedSet<string>(StringComparer.Ordinal); // by the run, for the printed comparison only
var started = System.Diagnostics.Stopwatch.StartNew();
foreach (var group in groups)
{
    var t = group.First;
    var setup = planner.Derive(t);
    var visited = planner.VisitedRooms(t);
    var key = $"{t.Room},{t.ObjectId},{t.VerbId}";
    rooms[key] = visited.ToArray();
    // A run that never began the cutscene (no start found) says nothing about where it goes: those keep the static rule.
    if (LoomCutsceneRun.IsScene(group, setup.Plays ? visited.Where(r => r != t.Room).ToArray() : null)) scenes.Add(key);
    if (!setup.Plays) Console.WriteLine($"  no start found, static rule kept: {LoomCutsceneTitles.Title(t)}");
}
rooms["intro"] = planner.IntroRooms().ToArray();
File.WriteAllText(args[1], JsonSerializer.Serialize(new { fingerprint = vga.Fingerprint, rooms }, new JsonSerializerOptions { WriteIndented = true }));
var staticScenes = groups.Count(g => g.IsScene);
Console.WriteLine($"{groups.Count} groups in {started.Elapsed.TotalSeconds:F0} s; scenes by run {scenes.Count}, by the static rule {staticScenes}; wrote {args[1]}");
foreach (var g in groups)
{
    var key = $"{g.First.Room},{g.First.ObjectId},{g.First.VerbId}";
    if (scenes.Contains(key) != g.IsScene) Console.WriteLine($"  differs: {LoomCutsceneTitles.Title(g.First)} static {(g.IsScene ? "Scene" : "Dialogue")} -> run {(scenes.Contains(key) ? "Scene" : "Dialogue")} (texts {g.First.Texts}, loads {g.First.RoomLoads}, rooms [{string.Join(",", rooms[key])}])");
}

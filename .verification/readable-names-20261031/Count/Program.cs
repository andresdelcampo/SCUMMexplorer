using System.Reflection;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// Usage: Count <game folder>...
// 31 October 2026 (ROADMAP item 3): every script of each game in the readable view, before (the engine's variable names only) and after the game's own
// names (objects from each room's data, actors from actorOps actorName). Counts the object and actor references that stay unnamed ("object 123",
// "actor 5") and the named object references ("object 123 “jar”").
foreach (var folder in args)
{
    var game = new GameFolderScanner().Scan(folder);
    var data = ScummDataSources.For(game);
    var scripts = new List<ScummScriptDisassembly>();
    void Add(Func<ScummScriptDisassembly?> read) { try { if (read() is { } s) scripts.Add(s); } catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException) { } }
    foreach (var id in data.GlobalScriptIds()) Add(() => data.GlobalScript(id));
    foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
    {
        Add(() => data.RoomEntryScript(room));
        Add(() => data.RoomExitScript(room));
        foreach (var local in data.RoomLocalScriptIds(room)) Add(() => data.RoomLocalScript(room, local));
        try
        {
            var objects = data.ReadRoom(room).Objects;
            foreach (var (objectId, verbs) in data.ReadObjectVerbs(room, objects))
                foreach (var verb in verbs) Add(() => data.ReadObjectVerb(room, objects.Single(o => o.Id == objectId), verb.VerbId, out _));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException) { }
    }
    (int ObjNamed, int ObjBare, int ActorBare, int VerbNamed, int VerbBare) Measure(LoomGlossary glossary, Func<int, string?>? verbs)
    {
        int named = 0, bare = 0, actors = 0, verbNamed = 0, verbBare = 0;
        foreach (var script in scripts)
        {
            string text;
            try { text = new LoomScriptPresenter(glossary, verbs, "the hero").Present(script); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException) { continue; }
            var body = text.Split("\nNames used", 2)[0];
            named += Regex.Matches(body, @"\bobject \d+ “").Count;
            bare += Regex.Matches(body, @"\bobject \d+(?! “)\b").Count;
            actors += Regex.Matches(body, @"\bactor \d+\b").Count;
            verbNamed += Regex.Matches(body, "verb \"").Count;
            verbBare += Regex.Matches(body, @"\bverb 0x[0-9A-F]{2}\b(?!:)").Count;
        }
        return (named, bare, actors, verbNamed, verbBare);
    }
    var before = LoomGlossary.Load(game);
    typeof(LoomGlossary).GetField("_pendingGame", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(before, null);
    var b = Measure(before, null);
    var after = LoomGlossary.Load(game);
    var a = Measure(after, after.VerbName);
    string P(int n, int total) => total == 0 ? "-" : $"{100.0 * n / total:F1}%";
    var actorRefsBefore = b.ActorBare;
    Console.WriteLine($"{Path.GetFileName(folder.TrimEnd('/', '\\'))}: {scripts.Count} scripts; object references named {P(b.ObjNamed, b.ObjNamed + b.ObjBare)} -> {P(a.ObjNamed, a.ObjNamed + a.ObjBare)} " +
                      $"({a.ObjNamed} of {a.ObjNamed + a.ObjBare}); actor references named {P(actorRefsBefore - b.ActorBare, actorRefsBefore)} -> {P(actorRefsBefore - a.ActorBare, actorRefsBefore)} ({actorRefsBefore - a.ActorBare} of {actorRefsBefore}); " +
                      $"verb references named {P(b.VerbNamed, b.VerbNamed + b.VerbBare)} -> {P(a.VerbNamed, a.VerbNamed + a.VerbBare)} ({a.VerbNamed} of {a.VerbNamed + a.VerbBare})");
}

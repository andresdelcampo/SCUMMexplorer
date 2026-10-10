using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var evidence = new List<object>();
foreach (var (edition, relative) in new[] { ("ega-en", "loom ega mt32 en"),
    ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, relative)); var data = ScummDataSources.For(game);
    var calls = new List<object>(); var actors = new List<object>(); var errors = new List<string>();
    void Inspect(string where, ScummScriptDisassembly? script)
    {
        if (script is null) return;
        var code = script.Instructions.ToArray();
        for (int n = 0; n < code.Length; n++)
        {
            var i = code[n];
            if (i.Operation is "startScript" or "chainScript" && i.Operands.StartsWith("0x1D,"))
                calls.Add(new { where, i.Offset, i.Operation, i.Operands,
                    context = code.Skip(Math.Max(0, n - 10)).Take(12).Select(c => $"+{c.Offset:X4} {c.Operation} {c.Operands}").ToArray() });
            if (i.Operation is "putActorInRoom" or "putActor" or "actorOps" && i.Operands.StartsWith("0x01,") &&
                (i.Operation != "actorOps" || i.Operands.Contains("costume")))
                actors.Add(new { where, i.Offset, i.Operation, i.Operands });
        }
    }
    foreach (var id in data.GlobalScriptIds())
    {
        try { Inspect($"Global {id}", data.GlobalScript(id)); }
        catch (InvalidDataException e) { errors.Add($"Global {id}: {e.Message}"); }
    }
    foreach (int number in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Inspect($"Room {number} entry", data.RoomEntryScript(number)); Inspect($"Room {number} exit", data.RoomExitScript(number));
        foreach (int id in data.RoomLocalScriptIds(number)) Inspect($"Room {number} local {id}", data.RoomLocalScript(number, id));
        var room = data.ReadRoom(number);
        foreach (var (id, verbs) in data.ReadObjectVerbs(number, room.Objects))
        foreach (var verb in verbs.Where(v => !v.IsFallback))
            Inspect($"Room {number} object {id} verb {verb.VerbId:X2}", data.ReadObjectVerb(number,
                room.Objects.Single(o => o.Id == id), verb.VerbId, out _));
    }
    foreach (int id in new[] { 17, 25, 29, 34, 52, 54, 60, 65, 101, 102 })
        File.WriteAllText(Path.Combine(output, $"{edition}-global-{id}.txt"), data.GlobalScript(id)?.Format());
    File.WriteAllText(Path.Combine(output, edition + "-levels.json"), JsonSerializer.Serialize(calls, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(output, edition + "-ego.json"), JsonSerializer.Serialize(actors, new JsonSerializerOptions { WriteIndented = true }));
    evidence.Add(new { edition, levelSites = calls.Count, egoSites = actors.Count, errors });
    Console.WriteLine($"{edition}: {calls.Count} source level sites, {actors.Count} ego placement/costume sites, {errors.Count} decode limits");
}
File.WriteAllText(Path.Combine(output, "source-summary.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));

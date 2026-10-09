using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
foreach (var (edition, folder) in new[] { ("ega-en", "loom ega mt32 en"), ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, folder)); var data = ScummDataSources.For(game);
    var lines = new List<string>();
    void Inspect(string where, LoomScriptDisassembly? script)
    {
        if (script is null) return;
        var code = script.Instructions.ToArray();
        for (int n = 0; n < code.Length; n++)
            if (code[n].Operation is "startScript" or "chainScript" && code[n].Operands.StartsWith("0x19,"))
            {
                lines.Add(where);
                lines.AddRange(code.Skip(Math.Max(0, n - 2)).Take(5).Select(i => $"+{i.Offset:X4} {i.Operation} {i.Operands}"));
            }
    }
    foreach (int id in data.GlobalScriptIds())
        try { Inspect($"Global {id}", data.GlobalScript(id)); } catch (InvalidDataException) { }
    foreach (int room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Inspect($"Room {room} entry", data.RoomEntryScript(room));
        foreach (int id in data.RoomLocalScriptIds(room)) Inspect($"Room {room} local {id}", data.RoomLocalScript(room, id));
        var roomData = data.ReadRoom(room);
        foreach (var (id, verbs) in data.ReadObjectVerbs(room, roomData.Objects))
            foreach (var v in verbs.Where(v => !v.IsFallback))
                Inspect($"Room {room} object {id} ({roomData.ObjectNames.GetValueOrDefault(id)}) verb {v.VerbId:X2}", data.ReadObjectVerb(room, roomData.Objects.Single(o => o.Id == id), v.VerbId, out _));
    }
    File.WriteAllLines(Path.Combine(output, edition + "-learning.txt"), lines);
    foreach (int room in new[] { 9, 10, 17, 18, 28, 34, 38, 46, 51 })
    {
        File.WriteAllText(Path.Combine(output, $"{edition}-room-{room}-entry.txt"), data.RoomEntryScript(room)?.Format());
        var roomData = data.ReadRoom(room);
        File.WriteAllLines(Path.Combine(output, $"{edition}-room-{room}-objects.txt"), roomData.Objects.Select(o => $"{o.Id} {roomData.ObjectNames.GetValueOrDefault(o.Id)} [{o.X},{o.Y},{o.Width},{o.Height}] walk {o.WalkX},{o.WalkY}"));
    }
    foreach (int id in new[] { 56, 58, 59, 85, 86, 87, 104, 105 })
        File.WriteAllText(Path.Combine(output, $"{edition}-global-{id}.txt"), data.GlobalScript(id)?.Format());
    var index = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    var samples = new List<object>();
    foreach (int room in new[] { 9, 10, 17, 18, 34, 38, 51 })
    {
        var session = new LoomExplorationSession(game, index);
        session.VisitRoomById(room);
        for (int f = 0; f < 200; f++) session.AdvanceFrame();
        samples.Add(new { room, session.InCutscene, session.GameStatus.StaffHeld, session.GameStatus.AvailableNoteCount,
            actors = session.Actors.Where(a => a.Number == 1 || a.Room == room).Select(a => new { a.Number, a.Room, a.Visible, a.X, a.Y, a.Look.Costume, a.Look.Frame, a.Look.StandFrame }),
            session.GameStatus.CanEdit });
    }
    File.WriteAllText(Path.Combine(output, edition + "-arrivals.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{edition}: reviewed source copied to isolated packet, {samples.Count} catalog samples (estimates)");
}

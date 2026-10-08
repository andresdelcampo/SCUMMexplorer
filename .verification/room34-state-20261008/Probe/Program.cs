using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, json));
var editions = new[] {
    ("ega-en", "Games/loom ega mt32 en"),
    ("ega-es", "Games/ALTERNATIVE LOOMS/loom ega mt32 es"),
    ("vga-en", "Games/loom vga en")
};

if (args.Skip(1).Contains("transitions"))
{
    StateTransitions.Run(output);
    return;
}

foreach (var (name, folder) in editions)
{
    var game = new GameFolderScanner().Scan(folder);
    var data = ScummDataSources.For(game);
    var blocks = new List<(string Place, LoomScriptDisassembly Script)>();
    var errors = new List<string>();
    void Read(string place, Func<LoomScriptDisassembly?> read)
    {
        try { if (read() is { } script) blocks.Add((place, script)); }
        catch (Exception ex) { errors.Add(place + ": " + ex.Message); }
    }
    foreach (var id in data.GlobalScriptIds()) Read($"global {id}", () => data.GlobalScript(id));
    foreach (var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Read($"room {room} entry", () => data.RoomEntryScript(room));
        Read($"room {room} exit", () => data.RoomExitScript(room));
        foreach (var id in data.RoomLocalScriptIds(room)) Read($"room {room} local {id}", () => data.RoomLocalScript(room, id));
        var rd = data.ReadRoom(room);
        foreach (var (obj, entries) in data.ReadObjectVerbs(room, rd.Objects))
            foreach (var entry in entries)
                Read($"room {room} object {obj} verb {entry.VerbId:X2}", () => data.ReadObjectVerb(room, rd.Objects.Single(o => o.Id == obj), entry.VerbId, out _));
    }
    // These are literal decoded references, including destinations. Dynamic aliases and
    // undecoded bytes remain outside the search; the packet records those limits.
    var terms = game.Id == GameId.LoomVga ? new[] { "bit[0x01B]", "bit[0x070]", "bit[0x00E]", "bit[0x067]", "bit[0x068]" }
        : name == "ega-en" ? new[] { "bit[0xD7A]", "bit[0xDCF]", "bit[0xD6D]", "bit[0xDC6]", "bit[0xDC7]" }
        : new[] { "bit[0xD7B]", "bit[0xDD0]", "bit[0xD6E]", "bit[0xDC7]", "bit[0xDC8]" };
    Save(name + "-references", new { errors, terms, blockCount = blocks.Count,
        limits = "Literal formatted operands in decoded reachable instructions only; dynamic numeric targets, backing-word aliases and undecoded bytes are not exhaustively classified.",
        refs = blocks.SelectMany(b => b.Script.Instructions.Where(i => terms.Any(t => i.Operands.Contains(t)))
            .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray(),
        helperCalls = blocks.SelectMany(b => b.Script.Instructions.Where(i =>
            (i.Operation is "startScript" or "chainScript") &&
            new[] { "0x55,", "0x62,", "0xC8,", "0xD1," }.Any(t => i.Operands.StartsWith(t)))
            .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray(),
        box6Writes = blocks.Where(b => b.Place.StartsWith("room 34 ") || b.Place.StartsWith("global "))
            .SelectMany(b => b.Script.Instructions.Where(i =>
                (i.Operation == "setBoxFlags" && i.Operands.StartsWith("0x06,")) ||
                (i.Operation == "matrixOps" && i.Operands.StartsWith("setBoxFlags, 0x06,")))
                .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray() });
    var selected = blocks.Where(b => b.Place.StartsWith("room 34 ") || b.Place.StartsWith("room 46 ") ||
        b.Place is "global 85" or "global 97" or "global 98" ||
        b.Script.Instructions.Any(i => terms.Any(t => i.Operands.StartsWith(t + " ="))));
    File.WriteAllText(Path.Combine(output, name + "-scripts.txt"), string.Join("\n", selected.Select(b => b.Script.Format())));
    Save(name + "-resources", new { game.Id, game.FolderPath, scripts = selected.Select(b => new { b.Place,
        b.Script.Context, b.Script.ContainerFileNumber, b.Script.ResourceOffset, b.Script.CodeLength, b.Script.Issues }).ToArray(),
        files = game.Id == GameId.LoomVga
            ? new[] { new { path = "DISK01.LEC (decoded from the local disc)", sha256 = Convert.ToHexString(SHA256.HashData(
                (byte[])typeof(LoomVgaArchive).GetField("_lec", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game.Vga!)!)) } }
            : new[] { Path.Combine(folder, "00.LFL"), Path.Combine(folder, "34.LFL"), Path.Combine(folder, "46.LFL") }
                .Select(p => new { path = p, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() });
    Console.WriteLine($"{name}: {blocks.Count} decoded blocks; {errors.Count} read errors; state references exported.");
}

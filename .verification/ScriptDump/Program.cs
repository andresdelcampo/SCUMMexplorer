using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// Usage: ScriptDump <game folder> <global script id>...
//        ScriptDump <game folder> room <room> <local id>
//        ScriptDump <game folder> entry <room>
//        ScriptDump <game folder> objmap <other game folder>   (room objects of both editions side by side, matched by directory position)
var game = new GameFolderScanner().Scan(args[0]);
var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
void Dump(string title, ScummScriptDisassembly? d)
{
    Console.WriteLine($"== {title}");
    if (d is null) { Console.WriteLine("(none)"); return; }
    foreach (var i in d.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
}
Dictionary<int, List<(int Id, string Name)>> Objects(GameLibrary g)
{
    var result = new Dictionary<int, List<(int, string)>>();
    foreach (var room in g.Rooms.Where(r => r.ResourceId.HasValue))
    {
        var bytes = File.ReadAllBytes(room.FilePath);
        var records = new ScummRoomObjectDirectory().Decode(bytes);
        var names = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), records);
        result[room.ResourceId!.Value] = records.Select(r => (r.Id, names.GetValueOrDefault(r.Id, ""))).ToList();
    }
    return result;
}
if (args[1] == "objmap")
{
    var a = Objects(game); var b = Objects(new GameFolderScanner().Scan(args[2]));
    foreach (var room in a.Keys.OrderBy(k => k))
    {
        if (!b.TryGetValue(room, out var other)) { Console.WriteLine($"room {room}: missing in other"); continue; }
        Console.WriteLine($"room {room}: {a[room].Count} vs {other.Count}");
        for (int i = 0; i < Math.Max(a[room].Count, other.Count); i++)
            Console.WriteLine($"  {(i < a[room].Count ? a[room][i].Id.ToString() : "-"),5} {(i < a[room].Count ? a[room][i].Name : ""),-14} | {(i < other.Count ? other[i].Id.ToString() : "-"),5} {(i < other.Count ? other[i].Name : "")}");
    }
}
else if (args[1] == "entry")
    Dump($"room {args[2]} entry", ScummDataSources.For(game).RoomEntryScript(int.Parse(args[2])));
else if (args[1] == "exit")
    Dump($"room {args[2]} exit", ScummDataSources.For(game).RoomExitScript(int.Parse(args[2])));
else if (args[1] == "room")
    Dump($"room {args[2]} local {args[3]}", ScummDataSources.For(game).RoomLocalScript(int.Parse(args[2]), int.Parse(args[3])));
else foreach (var a in args.Skip(1))
    Dump($"script {a}", ScummDataSources.For(game).GlobalScript(int.Parse(a)));

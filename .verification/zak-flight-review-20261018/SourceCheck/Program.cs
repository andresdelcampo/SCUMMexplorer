using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Read-only source inspection; no session or game state is created or changed.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
if (args[1] == "names")
{
    foreach (var resource in game.Rooms)
    {
        var room = resource.ResourceId!.Value;
        foreach (var name in data.ReadRoom(room).ObjectNames)
        {
            var text = Encoding.Latin1.GetString(name.Value);
            if (args.Skip(2).Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"Room {room} object {name.Key}: {text}");
        }
    }
    return;
}
foreach (var room in args.Skip(1).Select(int.Parse))
{
    var read = data.ReadRoom(room);
    Console.WriteLine($"ROOM {room}");
    foreach (var record in read.Objects.DistinctBy(o => o.Id))
    {
        Console.WriteLine($"OBJECT {record.Id}: {Encoding.Latin1.GetString(read.ObjectNames.GetValueOrDefault(record.Id) ?? [])}");
        foreach (var verb in data.ReadObjectVerbs(room,[record]).GetValueOrDefault(record.Id) ?? [])
        {
            Console.WriteLine($"VERB {verb.VerbId}");
            foreach (var instruction in data.ReadObjectVerb(room, record, verb.VerbId, out _).Instructions)
                Console.WriteLine($"+{instruction.Offset:X4} {instruction.Operation} {instruction.Operands}");
        }
    }
}

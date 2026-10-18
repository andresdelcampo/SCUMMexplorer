using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Source inspection and explicitly selected native-session diagnostics; game files are never changed.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
if (args[1] == "boot")
{
    var session = ScummGameSession.BootForPlay(game, out _);
    var ego = session.CaptureInventoryStatus().CurrentActorId;
    void Show(string stage) => Console.WriteLine($"{stage}: boot={session.BootReport}, room={session.CurrentRoomId}, control={session.PlayerHasControl}, cutscene={session.InCutscene}, ego={ego}, egoRoom={session.Actors.Single(a=>a.Number==ego).Room}");
    Show("boot");
    session.EnterRoomAgain(game.Rooms.Single(r=>r.ResourceId==11));
    Show("preview");
    for (var f=0; f<1000; f++) session.AdvanceFrame();
    Show("preview+1000");
    foreach (var line in session.SessionTrace.Where(t=>t.Contains("arrival",StringComparison.OrdinalIgnoreCase)||t.Contains("follow",StringComparison.OrdinalIgnoreCase)).TakeLast(12)) Console.WriteLine(line);
    return;
}
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

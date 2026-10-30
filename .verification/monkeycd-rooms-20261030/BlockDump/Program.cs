using System.Reflection;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: BlockDump <game folder> <room>
// 30 October 2026: lists a v5 room's code blocks (ENCD, EXCD, LSCR) with their positions and sizes, and the 24 bytes after an empty ENCD
// (ScummVM setupRoomSubBlocks keeps the data pointer of an empty block, so runEntryScript would run what follows). Reads only.
var game = new GameFolderScanner().Scan(args[0]);
var room = int.Parse(args[1]);
if (args.Length > 2 && args[2] == "index")
{
    var states = ((ScummV5DataSource)ScummDataSources.For(game)).ReadIndexedObjects().States;
    Console.WriteLine($"index objects: {states.Count}, highest id {states.Keys.Max()}");
    var data = ScummDataSources.For(game).ReadRoom(room);
    Console.WriteLine($"room {room} objects: {string.Join(", ", data.Objects.Select(o => o.Id).Distinct().Order())}");
    return;
}
foreach (var archive in game.V4Disks!.Disks.OfType<ScummV5Archive>())
{
    var resources = (byte[])typeof(ScummV5Archive).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(archive)!;
    foreach (var script in archive.ReadRoomScripts(room))
    {
        Console.WriteLine($"{script.Kind} {script.Number} at {script.Position} ({script.Code.Length} bytes of code)");
        if (script.Code.Length == 0)
        {
            var after = resources.AsSpan(script.Position + 8, 24).ToArray();
            Console.WriteLine("  next bytes: " + Convert.ToHexString(after) + "  \"" + new string(after.Select(b => b is >= 32 and < 127 ? (char)b : '.').ToArray()) + "\"");
        }
    }
}

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args[0] == "window") { WindowProof.Run(args); return; }
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        var watch = Stopwatch.StartNew();
        var report = new List<string>();
        var scan = typeof(MainWindow).GetMethod("ScanGenericRoomSounds", BindingFlags.Static | BindingFlags.NonPublic)!;
        var bounded = args.Contains("bounded");
        var fixtures = bounded ? new[] { "mi2 talkie en", "indy4 vga en" } : new[] { "mmansion ega en", "mmansionE ega es", "zak ega en", "zakE ega en", "indy3 ega en", "indy3 vga en", "monkey1 ega en", "monkey1 vga en", "mi2 talkie en", "indy4 vga en" };
        foreach (var fixture in fixtures)
        {
            var game = new GameFolderScanner().Scan(Path.Combine(args[1], fixture));
            var data = ScummDataSources.For(game);
            var rooms = 0; var roomsWithSounds = 0; var links = 0; var direct = 0;
            foreach (var room in game.Rooms)
            {
                var id = room.ResourceId!.Value;
                if (bounded && id != (fixture == "mi2 talkie en" ? 7 : 4)) continue;
                var found = (Dictionary<int, string>)scan.Invoke(null, [game, id])!;
                rooms++; links += found.Count; if (found.Count > 0) roomsWithSounds++;
                foreach (var instruction in (data.RoomEntryScript(id)?.Instructions ?? []).Concat(data.RoomExitScript(id)?.Instructions ?? []))
                {
                    if (instruction.Operation is not ("startSound" or "startMusic")) continue;
                    var first = instruction.Operands.Split(',')[0].Trim();
                    if (!first.StartsWith("0x") || !int.TryParse(first.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var sound)) continue;
                    if (!found.ContainsKey(sound)) throw new InvalidOperationException($"Lost direct sound: {fixture} room {id}, sound {sound}");
                    direct++;
                }
            }
            if (roomsWithSounds == 0) throw new InvalidOperationException("No source sound links: " + fixture);
            report.Add($"PASS {fixture}: {rooms} rooms; {roomsWithSounds} source-linked menus; {links} room/sound links; {direct} independent direct entry/exit starts retained");
            File.WriteAllLines(Path.Combine(output, "source-census.txt"), report);
        }
        report.Add($"PASS {fixtures.Length} native data-source families; {watch.Elapsed.TotalSeconds:F2}s; no App/MainWindow/window/session created or scripts executed");
        File.WriteAllLines(Path.Combine(output, "source-census.txt"), report);
        Console.WriteLine(string.Join(Environment.NewLine, report));
    }
}

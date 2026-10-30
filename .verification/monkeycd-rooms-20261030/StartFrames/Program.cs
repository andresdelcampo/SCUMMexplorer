using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: StartFrames <game folder> <room> <output folder>
// 30 October 2026 (MonkeyCd: "the lookout shows for one frame at start"): the real MainWindow opens the game and picks the room as a catalog click does;
// every rendered frame, the room of the play frame on screen (_playShown) is logged when it changes, with the play image's visibility. Private profile.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        string? last = null;
        var shownRooms = new List<int?>();
        CompositionTarget.Rendering += (_, _) =>
        {
            var shown = Field("_playShown");
            var room = (int?)shown?.GetType().GetProperty("RoomId")?.GetValue(shown);
            var image = (Image)window.FindName("PlayScreenImage");
            var visible = image.IsVisible && image.Source is not null;
            var state = $"shown room {room?.ToString() ?? "-"} visible {visible}";
            if (visible) shownRooms.Add(room);
            if (state != last) Log(state);
            last = state;
        };
        window.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var room = game.Rooms.First(r => r.ResourceId == int.Parse(args[1]));
                var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", BindingFlags.NonPublic)!;
                Call("SelectRoomWithIntent", room, Enum.Parse(kind, "Catalog"), false);
                Log($"picked Room {args[1]}");
                var until = clock.Elapsed.TotalSeconds + 25; while (clock.Elapsed.TotalSeconds < until) await Task.Delay(20);
                var other = shownRooms.Where(r => r != int.Parse(args[1])).Distinct().ToArray();
                Log(other.Length == 0 ? "PASS: only the picked room was shown" : $"FAIL: other rooms shown: {string.Join(", ", other)}");
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
    }
}

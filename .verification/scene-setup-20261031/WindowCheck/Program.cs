using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: WindowCheck <game folder> <output folder> <room,object,verb>...
// 31 October 2026 (REQUIREMENTS §8.6): the real MainWindow opens the game, waits for the background scene derivation, then plays each named scene
// from the whole-game list (its Play action, as a pick in the picker does) and logs the room, the texts on screen and the setup, with a picture.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        async Task Pump(double seconds) { var until = clock.Elapsed.TotalSeconds + seconds; while (clock.Elapsed.TotalSeconds < until) await Task.Delay(20); }
        void State(string stage)
        {
            var play = (ScummGameSession?)Field("_play");
            Log($"{stage}: room={play?.CurrentRoomId} cutscene={play?.InCutscene} texts=[{string.Join(" / ", play?.Messages.OnScreen.SelectMany(m => m.Lines).Where(l => l.Length > 0).Take(3) ?? [])}]");
        }
        window.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                for (var i = 0; i < 300 && !(bool)Field("_playRoomListsDone")!; i++) await Pump(1);
                Log($"derivation done={Field("_playRoomListsDone")}");
                foreach (var spec in args.Skip(2))
                {
                    var items = (System.Collections.IList)Call("PlayCutsceneItems", new object?[] { null })!;
                    object? picked = null;
                    foreach (var item in items)
                        if ((string)item!.GetType().GetField("Item1")!.GetValue(item)! == "scumm-" + spec.Replace(',', '-')) picked = item.GetType().GetField("Item2")!.GetValue(item);
                    if (picked is null) { Log($"{spec}: not in the list"); continue; }
                    Log($"{spec}: playing \"{picked.GetType().GetProperty("Title")!.GetValue(picked)}\"");
                    ((Action)picked.GetType().GetProperty("Play")!.GetValue(picked)!).Invoke();
                    for (var i = 1; i <= 8; i++) { await Pump(1.5); State($"{spec} +{i * 1.5:F1}s"); if (i == 3) Capture($"{spec.Replace(',', '-')}"); }
                    var play = (ScummGameSession?)Field("_play");
                    Log($"{spec} setup: {string.Join(" | ", play?.Snapshot.Uncertainties.Where(u => u.Contains("setup") || u.Contains("picked") || u.Contains("started directly")).TakeLast(3) ?? [])}");
                }
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
    }
}

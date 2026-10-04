using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// A LOOM demo in the real MainWindow: the room picker as for the full game, the preview playing from Room 01 on the playback clock
// (rooms it enters, screenshots), and the preview stopping when another room is opened.
// Arguments: <demo folder> <output folder> [seconds to play, default 40]
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        var seconds = args.Length > 2 ? int.Parse(args[2]) : 40;
        Directory.CreateDirectory(output);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Width = 1200, Height = 760 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        void Save(string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render((Visual)window.Content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file)); encoder.Save(stream);
        }
        int? SessionRoom() => (Field("_session") as SCUMMexplorer.Core.Formats.LoomExplorationSession)?.Snapshot.RoomId;
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                report.Add($"{game.Title}: IsDemo {game.IsDemo}, {game.Scenes.Count()} scenes");
                await Task.Delay(1500);
                // Idle: the director must not run on its own.
                await Task.Delay(3000);
                report.Add($"idle for 4.5 s: session room {SessionRoom()} (expected 1), playback running {Field("_playback") is not null}");
                var roomButton = Find<Button>("RoomCutsceneButton");
                report.Add($"Room 01 cutscene button visible: {roomButton.Visibility}, content \"{roomButton.Content}\", tooltip \"{roomButton.ToolTip}\"");
                // Open Room 01 so the view shows the session room, then play the preview.
                var room1 = game.Rooms.First(r => r.ResourceId == 1);
                Call("SelectResourceInTree", room1);
                await Task.Delay(1500);
                report.Add($"in Room 01: Play Cutscene button {roomButton.Visibility}, content \"{roomButton.Content}\", tooltip \"{roomButton.ToolTip}\"");
                Call("PlayDemoPreview");
                var seen = new List<int>();
                var shots = 0;
                for (var t = 0; t < seconds * 2; t++)
                {
                    await Task.Delay(500);
                    if (SessionRoom() is int room && (seen.Count == 0 || seen[^1] != room))
                    {
                        seen.Add(room);
                        if (shots++ < 12) Save($"preview-{seen.Count:00}-room-{room:00}.png");
                    }
                }
                report.Add($"after {seconds} s of the preview: rooms entered {string.Join(" > ", seen)}; playback running {Field("_playback") is not null}; status \"{Find<TextBlock>("StatusText").Text}\"");
                // Open another room: the preview must stop and the session be a fresh one.
                var other = game.Rooms.First(r => r.ResourceId == 9);
                Call("SelectResourceInTree", other);
                await Task.Delay(2500);
                var room9 = SessionRoom();
                await Task.Delay(4000);
                var leftover = Field("_playback");
                report.Add($"opened Room 09: playback null {leftover is null}, still the preview {leftover?.GetType().GetProperty("Preview")?.GetValue(leftover)}, what \"{leftover?.GetType().GetProperty("What")?.GetValue(leftover)}\"; session room {room9} then {SessionRoom()} (expected 9 both: the director no longer runs)");
                Save("after-leaving.png");
            }
            catch (Exception ex) { report.Add("FAILED: " + ex); }
            File.WriteAllLines(Path.Combine(output, "demo-preview-check.txt"), report);
            app.Shutdown();
        };
        app.Run(window);
    }
}

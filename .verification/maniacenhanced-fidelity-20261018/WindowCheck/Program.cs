using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG", Path.Combine(output, "harness-failure.log"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(stream);
        }
        uint[] Pixels(BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var pixels = new uint[converted.PixelWidth * converted.PixelHeight];
            converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
            return pixels;
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var roomId = int.Parse(args[2]);
                var resource = game.Rooms.Single(r => r.ResourceId == roomId);
                Call("SelectResourceInTree", resource);
                for (var wait = 0; wait < 1200; wait++)
                {
                    if (Equals(Field("_currentRoomResource"), resource) && Field("_browsedScene") is ScummRoomScene { RoomId: var id } && id == roomId && Field("_playTimer") is not null) break;
                    await Task.Delay(100);
                }
                Check(Equals(Field("_currentRoomResource"), resource), "Requested room did not load.");
                Check(Field("_browsedScene") is ScummRoomScene { RoomId: var loaded } && loaded == roomId, "Requested source scene did not load.");
                Call("StopPlay");
                Call("RefreshRoomPreview");
                var scene = (ScummRoomScene)Field("_browsedScene")!;
                var selection = (RoomObjectSelection)Field("_objectSelection")!;
                var preview = (Image)window.FindName("RoomPreview");
                Check(preview.Source is BitmapSource, "RoomPreview has no real bitmap.");
                var actual = Pixels((BitmapSource)preview.Source);
                var indexed = scene.Compose(selection);
                var palette = scene.DisplayPalette();
                var expected = indexed.Select(i => palette[i]).ToArray();
                Check(actual.SequenceEqual(expected), "Actual RoomPreview bitmap does not match native scene lighting/palette.");
                Check(!scene.RoomLit && indexed.All(p => p == 0), "Dark source preview was shown lit.");
                var note = (TextBlock)window.FindName("ObjectsNote");
                Check(!note.IsVisible || note.Text.Length == 0, "Technical notes leaked into Normal mode.");
                Capture("dark-room");
                var report = new List<string> { $"PASS Room {roomId}: real RoomPreview bitmap {actual.Length} pixels exactly matches source Compose/DisplayPalette; source lights {scene.State.Lights}; Normal notes clean." };
                if (args.Contains("torch"))
                {
                    await (Task)Call("StartPlayAsync", game.Rooms.Single(r => r.ResourceId == 28), CancellationToken.None)!;
                    ((DispatcherTimer)Field("_playTimer")!).Stop();
                    var session = (ScummGameSession)Field("_play")!;
                    session.Settle();
                    session.ActivateObjectVerb(69, 14);
                    session.ActivateObjectVerb(69, 4);
                    for (var f = 0; f < 120; f++) session.AdvanceFrame();
                    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 36));
                    for (var f = 0; f < 120; f++) session.AdvanceFrame();
                    Check(session.CaptureRoom() is { Lights: 12, Flashlight: (9, 7) }, "Native flashlight source prerequisites did not reach lights12/9x7.");
                    session.PointAtScreen((179, 79));
                    Call("ShowPlayFrame");
                    var image = (Image)window.FindName("PlayScreenImage");
                    var first = Pixels((BitmapSource)image.Source);
                    var shown = (ScummPlayFrame)Field("_playShown")!;
                    Check(first.SequenceEqual(shown.Pixels), "Actual Play bitmap differs from composed frame.");
                    Check(image.ToolTip is null, "Technical Play tooltip leaked into Normal mode.");
                    Capture("torch-first");
                    session.PointAtScreen((250, 100));
                    Call("ShowPlayFrame");
                    var second = Pixels((BitmapSource)image.Source);
                    Check(!first.SequenceEqual(second), "Moving the pointer did not move the actual flashlight bitmap.");
                    Capture("torch-moved");
                    report.Add("PASS actual PlayScreen bitmap: native direct pickup/Switch on (exploration actions), lights12/9x7; beam follows pointer, technical tooltip absent. No battery/story override.");
                }
                File.WriteAllLines(Path.Combine(output, "result.txt"), report);
                Call("StopPlay");
                app.Shutdown(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(output, "failure.txt"), exception.ToString());
                app.Shutdown(1);
            }
        };
        app.Run(window);
    }
}

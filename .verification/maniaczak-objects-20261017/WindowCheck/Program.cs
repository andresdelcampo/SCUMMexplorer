using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                if (!ScummProfile.For(game).HasFirstVisitScenes) throw new Exception("First visits are not enabled.");
                var report = new List<string>();
                foreach (var roomId in args.Skip(2).Select(int.Parse))
                {
                    var resource = game.Rooms.Single(room => room.ResourceId == roomId);
                    Call("SelectResourceInTree", resource);
                    for (var wait = 0; wait < 1200 && !Equals(Field("_currentRoomResource"), resource); wait++) await Task.Delay(100);
                    if (!Equals(Field("_currentRoomResource"), resource)) throw new Exception($"Room {roomId} did not load.");
                    if (Field("_browsedScene") is not ScummRoomScene scene) throw new Exception($"Room {roomId} has no session scene.");
                    var selection = (RoomObjectSelection)Field("_objectSelection")!;
                    var images = (IReadOnlyList<RoomObjectImage>)Field("_objects")!;
                    if (!images.Where(selection.IsActive).Select(image => image.Id).SequenceEqual(scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id)))
                        throw new Exception($"Room {roomId} does not show the session's object selection.");
                    var labels = (IReadOnlyList<BrowsedObjectLabel>)Field("_olderObjects")!;
                    if (!labels.Select(label => label.Id).SequenceEqual(scene.Records.Where(scene.IsHotspot).Select(record => record.Id)))
                        throw new Exception($"Room {roomId} labels disagree with source hotspot eligibility.");
                    foreach (var label in labels)
                    {
                        var record = scene.Records.Last(item => item.Id == label.Id);
                        var position = scene.Snapshot.ObjectPositions.GetValueOrDefault(label.Id, new RoomObjectPosition(record.X, record.Y));
                        if (label.X != position.X || label.Y != position.Y) throw new Exception("Hotspot position is stale.");
                    }
                    window.UpdateLayout();
                    var note = (TextBlock)window.FindName("ObjectsNote");
                    if (note.IsVisible && note.Text.Length > 0) throw new Exception("Technical object notes leaked into Normal mode.");
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(output, $"room-{roomId:00}.png"))) encoder.Save(stream);
                    report.Add($"Room {roomId:00}: scene {scene.Outcome}; active [{string.Join(',', images.Where(selection.IsActive).Select(image => image.Id))}]; hotspots [{string.Join(',', labels.Select(label => label.Id))}]; gaps {scene.Gaps().Count}");
                }
                File.WriteAllLines(Path.Combine(output, "result.txt"), report);
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

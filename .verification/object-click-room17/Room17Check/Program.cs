using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Real MainWindow in its default mode. Synthetic mouse-down/up pairing uses
// the same private handler path as the app; the point is in source room pixels.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) =>
            typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight,
                96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file));
            encoder.Save(stream);
        }

        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var room = game.Rooms.Single(item => item.ResourceId == 17);
                Call("SelectResourceInTree", room);
                for (var n = 0; n < 100 &&
                     (Field("_currentRoomResource") as GameResource)?.ResourceId != 17; n++)
                    await Task.Delay(100);
                await Task.Delay(300);
                window.UpdateLayout();
                var session = (LoomExplorationSession)Field("_session")!;
                report.Add($"Before: room={session.Snapshot.RoomId}; Advanced={((CheckBox)window.FindName("AdvancedModeToggle")).IsChecked}; input? status={((TextBlock)window.FindName("StatusText")).Text}");
                report.Add($"Object 357 owner={session.Snapshot.ObjectOwners.GetValueOrDefault(357)} state={session.Snapshot.ObjectStates.GetValueOrDefault(357)}");
                Call("QueueGameClick", 44, 110);
                Call("RoomPreview_MouseLeftButtonDown", null, null);
                Call("RoomPreview_MouseLeftButtonUp", null, null);
                var talkSeen = false;
                var closeupSeen = false;
                for (var n = 0; n < 300; n++)
                {
                    await Task.Delay(50);
                    var talk = session.Messages.Talk;
                    var roomMessage = (Image)window.FindName("RoomMessageOverlay");
                    var verbPanel = (FrameworkElement)window.FindName("VerbStripPanel");
                    var verbImage = (Image)window.FindName("VerbStripImage");
                    if (verbPanel.Visibility == Visibility.Visible && verbImage.Source is not null)
                        closeupSeen = true;
                    if (n % 20 == 0)
                        report.Add($"t={n * 50}ms ego={session.Actors.First(item => item.Number == 1).X},{session.Actors.First(item => item.Number == 1).Y} moving={session.EgoMoving} pending={session.Snapshot.PendingScripts} talk='{talk?.Text}' playback={typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)}");
                    if (!talkSeen && talk?.Source.Contains("Room 17 Object 357 Verb 0x36", StringComparison.Ordinal) == true)
                    {
                        talkSeen = true;
                        report.Add($"Talk at {n * 50}ms: '{talk.Text}' source={talk.Source}; overlay={roomMessage.Visibility}; bitmap={roomMessage.Source is not null}; verbPanel={verbPanel.Visibility}; verbImage={verbImage.Source is not null}");
                        Save("room17-look-live.png");
                    }
                    if (talkSeen && talk is null) break;
                }
                report.Add($"After: room={session.Snapshot.RoomId}; talkSeen={talkSeen}; closeupSeen={closeupSeen}; playback={typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)}");
                report.Add($"Look 0x36 reached={session.SessionTrace.Any(line => line.Contains("startObject runs object 357 verb 0x36", StringComparison.Ordinal))}; text sources={string.Join(" | ", session.Messages.OnScreen.Select(item => item.Source + "='" + item.Text + "'"))}");
                report.Add($"Relevant trace: {string.Join(" | ", session.SessionTrace.Where(line => line.Contains("object 357", StringComparison.OrdinalIgnoreCase)).TakeLast(8))}");
                report.Add($"Status: {((TextBlock)window.FindName("StatusText")).Text}");
                if (!talkSeen || !closeupSeen) Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                report.Add(ex.ToString());
                Environment.ExitCode = 2;
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

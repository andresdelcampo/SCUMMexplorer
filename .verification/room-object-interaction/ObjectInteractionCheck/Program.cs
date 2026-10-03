using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Real MainWindow interaction check in normal mode; points use the same room-pixel handler as clicks.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        void Save(string file)
        {
            window.UpdateLayout();
            var group = Find<FrameworkElement>("RoomDisplayGroup");
            var width = Math.Max(1, (int)group.ActualWidth);
            var height = Math.Max(1, (int)group.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(group), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow, normal explore mode, game double-click path. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Find<TreeView>("ResourceTree");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(group => group.Items.OfType<TreeViewItem>())
                        .First(node => (int?)node.Tag?.GetType().GetProperty("ResourceId")?.GetValue(node.Tag) == room &&
                            node.Tag?.GetType().GetProperty("Kind")?.GetValue(node.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 120 && Shown() != room; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                }
                async Task Act(int room, int objectId, int x, int y, string expected)
                {
                    await Open(room);
                    var session = Session();
                    var roomOverlay = Find<Image>("RoomMessageOverlay");
                    var verbPanel = Find<FrameworkElement>("VerbStripPanel");
                    var verbImage = Find<Image>("VerbStripImage");
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    Call("QueueGameClick", x, y);
                    Call("RoomPreview_MouseLeftButtonDown", null, null);
                    Call("RoomPreview_MouseLeftButtonUp", null, null);
                    var found = new List<string>();
                    var saved = false;
                    while (clock.ElapsedMilliseconds < 25000 && (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!)
                    {
                        await Task.Delay(20);
                        var talk = session.Messages.Talk;
                        if (talk is { } message && !found.Contains(message.Text)) found.Add(message.Text);
                        if (!saved && talk?.Text.Contains(expected, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            window.UpdateLayout(); Save($"room{room:00}-object{objectId}-talk.png"); saved = true;
                        }
                    }
                    window.UpdateLayout();
                    var traces = string.Join(" | ", session.SessionTrace.Where(line => line.Contains($"object {objectId}", StringComparison.OrdinalIgnoreCase) || line.Contains("startScript", StringComparison.Ordinal)).TakeLast(8));
                    Check(session.Snapshot.RoomId == room, $"Room {room:00} remained open after object {objectId} click");
                    Check(found.Any(line => line.Contains(expected, StringComparison.OrdinalIgnoreCase)), $"object {objectId} showed source look text containing [{expected}]; talk seen=[{string.Join(" / ", found)}]");
                    Check(roomOverlay.Visibility == Visibility.Visible, $"object {objectId} message layer visible when sampled");
                    Check(verbPanel.Visibility == Visibility.Visible ? verbImage.Source is BitmapSource : true,
                        $"object {objectId} close-up screen {(verbPanel.Visibility == Visibility.Visible ? "shown" : "not established")}");
                    report.Add($"TRACE Room {room:00} object {objectId}: {traces}");
                    Save($"room{room:00}-object{objectId}-final.png");
                }

                Check(Find<CheckBox>("AdvancedModeToggle").IsChecked != true, "Advanced mode remains off");
                Check(Find<CheckBox>("VerbAreaToggle").IsChecked != true, "verb strip remains off");
                Check(Find<CheckBox>("SessionModeToggle").IsChecked == true, "game interaction is on");
                await Act(10, 296, 64, 100, "cerrada a cal y canto");
                await Act(22, 404, 196, 52, "campanita de cristal");
            }
            catch (Exception exception) { report.Add(exception.ToString()); Environment.ExitCode = 1; }
            finally
            {
                report.Add($"Failures: {failures}");
                File.WriteAllLines(Path.Combine(output, "object-interaction-check.txt"), report, System.Text.Encoding.UTF8);
                if (failures > 0) Environment.ExitCode = 1;
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// Checks the room view's camera start and the optional entry pan in the real MainWindow.
// For each room given (entered from the tree; the first after the boot is Room 02):
// with "Pan to the camera on entry" off, the view opens at the camera at once; with it on,
// it opens at the left edge, moves in steps of 8 source pixels and stops at the camera;
// a manual pan stops it. The manual pan, the scroll bar and the arrows stay inside the game's
// camera limits (Room 41: roomScroll 176, so the view starts at x 16 and cannot move).
// Arguments: <Games\loom> <output folder> <rooms>. No physical input.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; synthetic tree selection and toggle changes. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var viewport = (ScrollViewer)window.FindName("RoomViewport");
                var toggle = (ToggleButton)window.FindName("CameraPanToggle");
                var session = Field("_session")!;
                int? Camera() => (int?)session.GetType().GetProperty("CameraX")!.GetValue(session);
                double Scale() => (double)Field("_roomDisplayScale")!;
                async Task Enter(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(200);
                }
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    toggle.IsChecked = false;
                    await Enter(room);
                    var camera = Camera();
                    var width = (int)Field("_currentRoom")!.GetType().GetProperty("Width")!.GetValue(Field("_currentRoom"))!;
                    var expected = camera is int c ? Math.Clamp(c - Math.Min(320, width) / 2.0, 0, viewport.ScrollableWidth / Scale()) : 0;
                    var left = viewport.HorizontalOffset / Scale();
                    Check(Math.Abs(left - expected) < 0.6, $"Room {room:00} pan off: camera x {camera?.ToString() ?? "unknown"}, view starts at source x {left:0.#} (expected {expected:0.#})");
                    Save($"room-{room:00}-pan-off.png");

                    // The manual pan and the scroll bar stay inside the camera limits (VAR_CAMERA_MIN_X/MAX_X).
                    var range = ((int Min, int Max)?)session.GetType().GetProperty("CameraRange")!.GetValue(session);
                    var half = Math.Min(320, width) / 2.0;
                    var leftmost = range is { } r1 ? Math.Clamp(r1.Min - half, 0, viewport.ScrollableWidth / Scale()) : 0;
                    var rightmost = range is { } r2 ? Math.Clamp(r2.Max - half, leftmost, viewport.ScrollableWidth / Scale()) : viewport.ScrollableWidth / Scale();
                    Call("PanRoomBySourcePixels", -2000.0);
                    window.UpdateLayout();
                    var panLeft = viewport.HorizontalOffset / Scale();
                    Call("PanRoomBySourcePixels", 2000.0);
                    window.UpdateLayout();
                    var panRight = viewport.HorizontalOffset / Scale();
                    viewport.ScrollToHorizontalOffset(0);
                    window.UpdateLayout();
                    await Task.Delay(100);
                    var bar = viewport.HorizontalOffset / Scale();
                    var leftButton = ((Button)window.FindName("ScrollRoomLeftButton")).IsEnabled;
                    var arrows = ((FrameworkElement)window.FindName("RoomScrollButtonsPanel")).Visibility;
                    Check(Math.Abs(panLeft - leftmost) < 0.6 && Math.Abs(panRight - rightmost) < 0.6 && Math.Abs(bar - leftmost) < 0.6 && !leftButton,
                        $"Room {room:00} manual pan: limits {range} give source x {leftmost:0.#}..{rightmost:0.#}; pan left {panLeft:0.#}, right {panRight:0.#}, scroll bar to 0 → {bar:0.#}; left arrow enabled {leftButton}; arrows {arrows} (room {width} px)");
                    Save($"room-{room:00}-pan-limits.png");
                    viewport.ScrollToHorizontalOffset(expected * Scale());
                    window.UpdateLayout();
                    if (expected <= leftmost + 0.5) continue;

                    // Pan on: leave and enter again through the tree. A catalog visit resets the
                    // camera to 160, so this only holds for the boot room, which the session is in.
                    toggle.IsChecked = true;
                    Call("ApplyRoomState", Field("_currentRoomResource"), Field("_currentRoomObjectRecords"), Field("_objects"), true);
                    typeof(MainWindow).GetField("_pendingCameraX", Private)!.SetValue(window, camera);
                    Call("UpdateRoomViewportLayout");
                    await Task.Delay(50);
                    var start = viewport.HorizontalOffset / Scale();
                    var samples = new List<double> { start };
                    // Sample after each of the app's own timer ticks (its handler runs first).
                    var timer = (System.Windows.Threading.DispatcherTimer)Field("_cameraPanTimer")!;
                    EventHandler sample = (_, _) =>
                    {
                        samples.Add(viewport.HorizontalOffset / Scale());
                        if (samples.Count == 5) Save($"room-{room:00}-pan-midway.png");
                    };
                    timer.Tick += sample;
                    for (var n = 0; n < 400 && timer.IsEnabled; n++) await Task.Delay(50);
                    timer.Tick -= sample;
                    var steps = samples.Zip(samples.Skip(1), (a, b) => b - a).Where(d => d > 0.01).ToArray();
                    Check(start < leftmost + 8.5, $"Room {room:00} pan on: starts at source x {start:0.#} (leftmost {leftmost:0.#})");
                    Check(steps.Length > 3 && steps.All(d => d <= 8.01), $"Room {room:00} pan on: {steps.Length} steps, largest {(steps.Length == 0 ? 0 : steps.Max()):0.##} source px");
                    Check(Math.Abs(samples[^1] - expected) < 0.6, $"Room {room:00} pan on: ends at source x {samples[^1]:0.#} (expected {expected:0.#}; scrollable {viewport.ScrollableWidth / Scale():0.##}, target {(double)Field("_cameraPanTargetSourceX")!:0.##}; last samples {string.Join(", ", samples.TakeLast(4).Select(v => v.ToString("0.##")))})");
                    Save($"room-{room:00}-pan-end.png");

                    // A manual pan stops the entry pan.
                    typeof(MainWindow).GetField("_pendingCameraX", Private)!.SetValue(window, camera);
                    Call("UpdateRoomViewportLayout");
                    await Task.Delay(300);
                    Call("PanRoomBySourcePixels", 2.0);
                    Call("BeginRoomPan", 1, 2.0, null, null);
                    Call("StopRoomPan");
                    var stopped = ((System.Windows.Threading.DispatcherTimer)Field("_cameraPanTimer")!).IsEnabled;
                    Check(!stopped, $"Room {room:00}: a manual pan stops the entry pan");
                }
                toggle.IsChecked = false;
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "camera-pan-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "camera-pan-check.txt")));
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fallback)!);
                        System.IO.File.WriteAllLines(fallback, report, System.Text.Encoding.UTF8);
                        Console.Error.WriteLine($"Report not written ({writeError.Message}); wrote {fallback} instead.");
                    }
                    catch (Exception fallbackError) when (fallbackError is System.IO.IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Report not written ({writeError.Message}; {fallbackError.Message}):");
                        foreach (var line in report) Console.Error.WriteLine(line);
                    }
                    Environment.ExitCode = 2;
                }
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

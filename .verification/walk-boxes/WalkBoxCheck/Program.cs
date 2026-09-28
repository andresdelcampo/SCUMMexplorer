using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SCUMMexplorer.App;

// For each room given: browse to it, show the full panorama with doorways off,
// tick "Show walk boxes", and capture the room surface. Checks that the overlay
// draws one outline and one label per box and that each outline's points map
// back to the box corners in room pixels. The by-eye comparison with each room's
// floor is recorded separately. Arguments: <Games\loom> <output folder> <rooms>.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow { Width = 1600, Height = 1000 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(FrameworkElement element, string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(System.IO.Path.Combine(output, file)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; WalkBoxToggle ticked through IsChecked, panorama view, doorways hidden. No physical input." };
            var failures = 0;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var overlay = (Canvas)window.FindName("WalkBoxOverlay");
                var surface = (FrameworkElement)window.FindName("RoomSceneSurface");
                var toggle = (CheckBox)window.FindName("WalkBoxToggle");
                ((CheckBox)window.FindName("NormalRoomWidthToggle")).IsChecked = false;
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    ((CheckBox)window.FindName("RoomNavigationToggle")).IsChecked = false;
                    toggle.IsChecked = false;
                    toggle.IsChecked = true;
                    await Task.Delay(300);
                    window.UpdateLayout();

                    var boxes = Call("DisplayedWalkBoxes")!;
                    var list = ((System.Collections.IEnumerable)boxes.GetType().GetProperty("Boxes")!.GetValue(boxes)!).Cast<object>().ToArray();
                    var roomImage = Field("_currentRoom")!;
                    var width = (int)roomImage.GetType().GetProperty("Width")!.GetValue(roomImage)!;
                    var height = (int)roomImage.GetType().GetProperty("Height")!.GetValue(roomImage)!;
                    var shapes = overlay.Children.OfType<Polygon>().ToArray();
                    var labels = overlay.Children.OfType<Border>().ToArray();
                    var scale = Math.Min(overlay.ActualWidth / width, overlay.ActualHeight / height);
                    var offsetX = (overlay.ActualWidth - width * scale) / 2;
                    var offsetY = (overlay.ActualHeight - height * scale) / 2;
                    var ok = overlay.Visibility == Visibility.Visible && toggle.IsEnabled &&
                             shapes.Length == list.Length && labels.Length == list.Length;
                    report.Add($"Room {room:00}: {width}x{height}, {list.Length} boxes, {shapes.Length} outlines, {labels.Length} labels, scale {scale:0.###}");
                    var markers = overlay.Children.OfType<Ellipse>().ToArray();
                    report.Add($"  {markers.Length} actor markers: " + string.Join("; ", markers.Select(m =>
                        $"({(Canvas.GetLeft(m) + 5 - offsetX) / scale - 0.5:0},{(Canvas.GetTop(m) + 5 - offsetY) / scale - 0.5:0}){(m.StrokeDashArray.Count > 0 ? " estimate" : "")}")));
                    for (var index = 0; index < list.Length && index < shapes.Length; index++)
                    {
                        var box = list[index];
                        var corners = ((System.Collections.IEnumerable)box.GetType().GetProperty("Corners")!.GetValue(box)!).Cast<object>()
                            .Select(p => ((int)p.GetType().GetProperty("X")!.GetValue(p)!, (int)p.GetType().GetProperty("Y")!.GetValue(p)!)).ToArray();
                        var flags = (int)Convert.ToInt32(boxes.GetType().GetMethod("GetFlags")!.Invoke(boxes, [index])!);
                        var mapped = shapes[index].Points.Select(p => ((p.X - offsetX) / scale - 0.5, (p.Y - offsetY) / scale - 0.5)).ToArray();
                        var match = mapped.Length == corners.Length &&
                                    mapped.Zip(corners).All(pair => Math.Abs(pair.First.Item1 - pair.Second.Item1) < 0.01 && Math.Abs(pair.First.Item2 - pair.Second.Item2) < 0.01);
                        var inside = corners.All(c => c.Item1 >= 0 && c.Item1 < width && c.Item2 >= 0 && c.Item2 < height);
                        ok &= match;
                        report.Add($"  box {index,2} flags 0x{flags:X2} {(inside ? "inside " : "OUTSIDE")} corners {string.Join(" ", corners.Select(c => $"({c.Item1},{c.Item2})"))}: outline maps back {(match ? "exactly" : "WRONG")}");
                    }
                    if (!ok) failures++;
                    report.Add($"{(ok ? "PASS" : "FAIL")} Room {room:00}");
                    Save(surface, $"room-{room:00}-walk-boxes.png");
                    toggle.IsChecked = false;
                    window.UpdateLayout();
                    var cleared = overlay.Visibility == Visibility.Collapsed;
                    if (!cleared) failures++;
                    report.Add($"{(cleared ? "PASS" : "FAIL")} Room {room:00} unticking hides the overlay");
                }
                report.Add($"Failures: {failures}");
                if (failures > 0) Environment.ExitCode = 1;
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(System.IO.Path.Combine(output, "walk-box-check.txt"), report); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(System.IO.Path.Combine(output, "walk-box-check.txt")));
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fallback)!);
                        System.IO.File.WriteAllLines(fallback, report);
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

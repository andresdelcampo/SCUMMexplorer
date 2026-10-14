using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// Reverse hover link: for each room given, and for each hotspot row, puts the room
// pointer at the centre of the row's box through the handler's own path
// (HotspotRowRecordAt, then ShowHoveredHotspotRow, as RoomPreview_MouseMove does),
// and checks that exactly the row findObject would pick is highlighted and its box
// outlined; then RoomPreview_MouseLeave clears both. WPF synthetic mouse events carry
// no position, so the pointer position is passed in room pixels. No physical input.
// Arguments: <Games\loom> <output folder> <room numbers>.
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
            var report = new List<string> { "Real MainWindow; the room pointer is given in room pixels to the handler's own methods. No physical input." };
            var failures = 0;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var overlay = (Canvas)window.FindName("HotspotHighlightOverlay");
                var list = (Panel)window.FindName("ObjectList");
                var hoverBrush = ((SolidColorBrush)typeof(MainWindow).GetField("HoveredHotspotRowBrush", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Color;
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                    var records = ((System.Collections.IEnumerable)Field("_currentRoomObjectRecords")!).Cast<object>().ToArray();
                    int Prop(object record, string name) => (int)record.GetType().GetProperty(name)!.GetValue(record)!;
                    var buttons = list.Children.OfType<Button>().ToArray();
                    report.Add($"Room {room:00}: {buttons.Length} hotspot rows");
                    for (var index = 0; index < buttons.Length; index++)
                    {
                        var button = buttons[index];
                        var record = records.First(r => Prop(r, "Id") == (int)button.Tag);
                        var (x, y) = (Prop(record, "X") + Prop(record, "Width") / 2, Prop(record, "Y") + Prop(record, "Height") / 2);
                        var picked = Call("HotspotRowRecordAt", x, y);
                        Call("ShowHoveredHotspotRow", picked);
                        window.UpdateLayout();
                        var pickedId = picked is null ? (int?)null : Prop(picked, "Id");
                        var highlighted = buttons.Where(b => b.Background is SolidColorBrush brush && brush.Color == hoverBrush).ToArray();
                        var box = overlay.Children.OfType<Border>().SingleOrDefault();
                        var ok = pickedId is not null && highlighted.Length == 1 && (int)highlighted[0].Tag == pickedId && box is not null;
                        // An earlier listed object covering the centre wins, as in findObject.
                        var covered = pickedId != (int)button.Tag;
                        if (covered && pickedId is int other)
                            ok &= records.TakeWhile(r => Prop(r, "Id") != (int)button.Tag).Any(r => Prop(r, "Id") == other) ||
                                  ((SCUMMexplorer.Core.Loom.ScummSessionSnapshot)Field("_session")!.GetType().GetProperty("Snapshot")!.GetValue(Field("_session"))!).IsUntouchable((int)button.Tag);
                        if (index == 0) Save($"room-{room:00}-hover-row-{index}.png");
                        Call("RoomPreview_MouseLeave", null, null);
                        window.UpdateLayout();
                        var cleared = !buttons.Any(b => b.Background is SolidColorBrush brush && brush.Color == hoverBrush) &&
                                      overlay.Children.Count == 0;
                        ok &= cleared;
                        if (!ok) failures++;
                        report.Add($"{(ok ? "PASS" : "FAIL")} Room {room:00} pointer ({x},{y}), centre of row \"{button.Content}\" (object {button.Tag}): " +
                                   $"highlighted {string.Join(", ", highlighted.Select(b => $"\"{b.Content}\""))}{(covered ? $" (object {pickedId?.ToString() ?? "none"} comes first there)" : "")}; " +
                                   $"outline {(box is null ? "none" : $"{box.Width:0.#}x{box.Height:0.#}")}; cleared after leave: {cleared}");
                    }
                    // A point with no hotspot highlights nothing.
                    Call("ShowHoveredHotspotRow", Call("HotspotRowRecordAt", -5, -5));
                    var none = !buttons.Any(b => b.Background is SolidColorBrush brush && brush.Color == hoverBrush);
                    if (!none) failures++;
                    report.Add($"{(none ? "PASS" : "FAIL")} Room {room:00} pointer (-5,-5): no row highlighted");
                }
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "reverse-hover-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "reverse-hover-check.txt")));
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

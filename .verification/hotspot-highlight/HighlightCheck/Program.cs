using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// For each room given: browse to it, then for every hotspot row raise MouseEnter
// (and, for the first row, give it keyboard focus), and check that the highlight
// canvas shows one box at the object's rectangle; then MouseLeave clears it.
// Arguments: <Games\loom> <output folder> <room numbers>. No physical input.
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
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
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
            var report = new List<string> { "Real MainWindow; synthetic MouseEnter/MouseLeave and Keyboard.Focus on hotspot rows. No physical input." };
            var failures = 0;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var overlay = (Canvas)window.FindName("HotspotHighlightOverlay");
                var list = (Panel)window.FindName("ObjectList");
                // Hotspot rows are an Advanced-mode catalog; normal-mode pointer discovery
                // is covered separately by PointerInputCheck.
                var advanced = (CheckBox)window.FindName("AdvancedModeToggle");
                advanced.IsChecked = true;
                Call("AdvancedModeToggle_Click", advanced, new RoutedEventArgs());
                ((Expander)window.FindName("ObjectListSection")).IsExpanded = true;
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
                    var buttons = list.Children.OfType<Button>().ToArray();
                    report.Add($"Room {room:00}: {buttons.Length} hotspot rows");
                    for (var index = 0; index < buttons.Length; index++)
                    {
                        var button = buttons[index];
                        var record = records.First(r => (int)r.GetType().GetProperty("Id")!.GetValue(r)! == (int)button.Tag);
                        int Prop(string name) => (int)record.GetType().GetProperty(name)!.GetValue(record)!;
                        if (index == 0) Keyboard.Focus(button);
                        else button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                        window.UpdateLayout();
                        var box = overlay.Children.OfType<Border>().SingleOrDefault();
                        var ok = overlay.Visibility == Visibility.Visible && box is not null;
                        var detail = box is null ? "no box" :
                            $"box at ({Canvas.GetLeft(box):0.#},{Canvas.GetTop(box):0.#}) {box.Width:0.#}x{box.Height:0.#}";
                        // The box's aspect ratio must match the record's rectangle.
                        if (box is not null && Prop("Width") > 0 && Prop("Height") > 0)
                            ok &= Math.Abs(box.Width / box.Height - (double)Prop("Width") / Prop("Height")) < 0.05;
                        if (index == 0) Save($"room-{room:00}-row-{index}-focus.png");
                        if (index == 1) Save($"room-{room:00}-row-{index}-hover.png");
                        if (index == 0) Keyboard.ClearFocus();
                        else button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                        window.UpdateLayout();
                        var cleared = overlay.Visibility == Visibility.Collapsed && overlay.Children.Count == 0;
                        ok &= cleared;
                        if (!ok) failures++;
                        report.Add($"{(ok ? "PASS" : "FAIL")} Room {room:00} row \"{button.Content}\" object {button.Tag} rect ({Prop("X")},{Prop("Y")}) {Prop("Width")}x{Prop("Height")} " +
                                   $"via {(index == 0 ? "focus" : "hover")}: {detail}; cleared after leave: {cleared}");
                    }
                }
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "highlight-check.txt"), report); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "highlight-check.txt")));
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

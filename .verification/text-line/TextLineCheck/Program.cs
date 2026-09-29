using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// The game's text line (screen rows 0–15, above the room), in the real MainWindow. Room 41's entry sets text slot 0's
// y to 12 (script 016, `startScript 0x10, [0x000C]`), so Bobbin's look lines there (object 632 verb 0x36) print on the
// text line and spill into the room's first rows. Checks: the strip is above the room and touches it, it holds the
// line's first row of text, the room keeps its size and position with and without the text, and the verb screen
// is unchanged. The verb is run directly (an exploration action). Arguments: <Games\loom> <output folder>.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(FrameworkElement element, string file)
        {
            window.UpdateLayout();
            var width = Math.Max(1, (int)element.ActualWidth);
            var height = Math.Max(1, (int)element.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file));
            encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; object 632's verb 0x36 run directly through the session (exploration action). No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var viewport = (FrameworkElement)window.FindName("RoomViewport");
                var host = (FrameworkElement)window.FindName("RoomViewportHost");
                var strip = (FrameworkElement)window.FindName("TextLinePanel");
                var overlay = (Image)window.FindName("TextLineMessageOverlay");
                var verbStrip = (FrameworkElement)window.FindName("VerbStripPanel");
                ((CheckBox)window.FindName("SessionModeToggle")).IsChecked = true;
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                for (var n = 0; n < 100 && Field("_currentRoomResource") is null; n++) await Task.Delay(100);
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 41 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) != 41; n++)
                    await Task.Delay(100);
                await Task.Delay(500);
                window.UpdateLayout();
                string Geometry()
                {
                    var top = viewport.TranslatePoint(new Point(0, 0), host);
                    return $"view {viewport.ActualWidth:0.#}x{viewport.ActualHeight:0.#} at ({top.X:0.#}, {top.Y:0.#})";
                }
                var before = Geometry();
                var stripBottom = strip.TranslatePoint(new Point(0, strip.ActualHeight), host).Y;
                var viewTop = viewport.TranslatePoint(new Point(0, 0), host).Y;
                report.Add($"Room 41 before: {before}; text line {strip.ActualWidth:0.#}x{strip.ActualHeight:0.#}, visible {strip.IsVisible}, bottom at y {stripBottom:0.#}");
                Check(strip.IsVisible && Math.Abs(stripBottom - viewTop) < 0.6, "the text line is shown directly above the room");
                Check(Math.Abs(strip.ActualHeight - viewport.ActualHeight * 16 / 128) < 1.5,
                    $"the text line is 16 rows at the room's scale ({strip.ActualHeight:0.#} px vs room {viewport.ActualHeight:0.#} px for 128 rows)");
                var verbTop = verbStrip.TranslatePoint(new Point(0, 0), host).Y;
                Check(verbStrip.IsVisible && Math.Abs(verbTop - (viewTop + viewport.ActualHeight)) < 0.6,
                    $"the verb screen starts where the room ends (y {verbTop:0.#} vs {viewTop + viewport.ActualHeight:0.#}): one screen, no gap");
                Save((FrameworkElement)window.FindName("RoomDisplayGroup"), "room41-before.png");

                // The sword (object 634 verb 0x36): two lines at y 12 and 20. Line 1 starts on the text line and is
                // drawn whole (reading of the original; ScummVM clips it at row 15), so its lower rows reach room rows
                // 0–3, which line 2 (from room row 4) cannot.
                Session().ActivateObjectVerb(634, 0x36);
                Call("UpdateMessageLayer");
                await Task.Delay(300);
                window.UpdateLayout();
                foreach (var message in Session().Messages.OnScreen) report.Add($"   on screen: \"{message.Text.Replace("\n", " / ")}\" at ({message.X}, {message.Y}) from {message.Source}");
                var roomText = ((Image)window.FindName("RoomMessageOverlay")).Source as BitmapSource;
                var topRowsLit = 0;
                if (roomText is not null)
                {
                    var pixels = new byte[roomText.PixelWidth * 4 * 4];
                    roomText.CopyPixels(new Int32Rect(0, 0, roomText.PixelWidth, 4), pixels, roomText.PixelWidth * 4, 0);
                    for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) topRowsLit++;
                }
                Check(topRowsLit > 30, $"the sword's first line runs on into the room's rows 0–3 ({topRowsLit} lit pixels there)");
                Save((FrameworkElement)window.FindName("RoomDisplayGroup"), "room41-look-634-sword.png");

                Session().ActivateObjectVerb(632, 0x36);
                Call("UpdateMessageLayer");
                await Task.Delay(300);
                window.UpdateLayout();
                var messages = Session().Messages.OnScreen;
                foreach (var message in messages) report.Add($"   on screen: \"{message.Text.Replace("\n", " / ")}\" at ({message.X}, {message.Y}) from {message.Source}");
                var source = overlay.Source as BitmapSource;
                var lit = 0;
                if (source is not null)
                {
                    var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
                    source.CopyPixels(pixels, source.PixelWidth * 4, 0);
                    for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) lit++;
                }
                Check(messages.Any(message => message.Y < 16) && lit > 50,
                    $"the look line printed at y 12 shows on the text line ({lit} lit pixels of 320 x 16)");
                var after = Geometry();
                report.Add($"Room 41 with the text: {after}");
                Check(before == after, "the room keeps its size and position when the text appears");
                Check(verbStrip.IsVisible, "the verb screen is still under the room");
                Save((FrameworkElement)window.FindName("RoomDisplayGroup"), "room41-look-632-text-line.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "text-line-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    foreach (var line in report) Console.Error.WriteLine(line);
                    Environment.ExitCode = 2;
                }
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

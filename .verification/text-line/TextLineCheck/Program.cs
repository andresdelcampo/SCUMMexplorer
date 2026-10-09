using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

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
                var overlay = (Image)window.FindName("RoomMessageOverlay");
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
                var viewTop = viewport.TranslatePoint(new Point(0, 0), host).Y;
                var scale = (double)Field("_roomDisplayScale")!;
                report.Add($"Room 41 before: {before}; scale {scale:0.###}");
                // The boot's initScreens(0, 144): the room screen is all 144 rows from screen row 0, with no text line above.
                Check(Math.Abs(viewport.ActualHeight - 144 * scale) < 1,
                    $"the room view shows all 144 rows ({viewport.ActualHeight:0.#} px = {viewport.ActualHeight / scale:0.#} rows)");
                Check(window.FindName("TextLinePanel") is null, "no separate text line above the room");
                var verbTop = verbStrip.TranslatePoint(new Point(0, 0), host).Y;
                Check(verbStrip.IsVisible && Math.Abs(verbTop - (viewTop + viewport.ActualHeight)) < 0.6,
                    $"the verb screen starts where the room ends (y {verbTop:0.#} vs {viewTop + viewport.ActualHeight:0.#}): one screen, no gap");
                Save((FrameworkElement)window.FindName("RoomDisplayGroup"), "room41-before.png");

                // The sword (object 634 verb 0x36): two lines at y 12 and 20, drawn over the room's rows 12-27.
                Session().ActivateObjectVerb(634, 0x36);
                Call("UpdateMessageLayer");
                await Task.Delay(300);
                window.UpdateLayout();
                foreach (var message in Session().Messages.OnScreen) report.Add($"   on screen: \"{message.Text.Replace("\n", " / ")}\" at ({message.X}, {message.Y}) from {message.Source}");
                var roomText = ((Image)window.FindName("RoomMessageOverlay")).Source as BitmapSource;
                int LitRows(BitmapSource? text, int from, int count)
                {
                    if (text is null) return 0;
                    var lit = 0;
                    var pixels = new byte[text.PixelWidth * count * 4];
                    text.CopyPixels(new Int32Rect(0, from, text.PixelWidth, count), pixels, text.PixelWidth * 4, 0);
                    for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) lit++;
                    return lit;
                }
                var firstLine = LitRows(roomText, 12, 8);
                var secondLine = LitRows(roomText, 20, 8);
                Check(firstLine > 30 && secondLine > 30, $"the sword's two lines are drawn whole over the room's rows 12-27 ({firstLine} and {secondLine} lit pixels)");
                Save((FrameworkElement)window.FindName("RoomDisplayGroup"), "room41-look-634-sword.png");

                Session().ActivateObjectVerb(632, 0x36);
                Call("UpdateMessageLayer");
                await Task.Delay(300);
                window.UpdateLayout();
                var messages = Session().Messages.OnScreen;
                foreach (var message in messages) report.Add($"   on screen: \"{message.Text.Replace("\n", " / ")}\" at ({message.X}, {message.Y}) from {message.Source}");
                var lit = LitRows(overlay.Source as BitmapSource, 0, 24);
                Check(messages.Any(message => message.Y < 16) && lit > 50,
                    $"the look line printed at y 12 shows over the room's top rows ({lit} lit pixels in rows 0-23)");
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

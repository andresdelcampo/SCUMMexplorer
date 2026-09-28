using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// "Show verb area" (presentation): in the real MainWindow, checks that the strip
// is off by default, sits under the room at the room's scale without clipping,
// draws the verbs the session's slots have on (after script 018, and the close-up
// after a hover), and goes away again. Script 018 is started through the
// session's internal test seam, as the tests do. No physical input.
// Arguments: <Games\loom> <output folder>.
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
            var report = new List<string> { "Real MainWindow; verb state changed through the session's test seam and the pointer handler. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var toggle = (CheckBox)window.FindName("VerbAreaToggle");
                var pointer = (CheckBox)window.FindName("GamePointerToggle");
                var strip = (FrameworkElement)window.FindName("VerbStripPanel");
                var image = (Image)window.FindName("VerbStripImage");
                var note = (TextBlock)window.FindName("VerbStripNote");
                var viewport = (FrameworkElement)window.FindName("RoomViewport");
                var host = (FrameworkElement)window.FindName("RoomViewportHost");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) != room; n++)
                        await Task.Delay(100);
                    await Settle();
                }
                async Task Settle() { await Task.Delay(300); window.UpdateLayout(); }
                byte PixelAt(int x, int y)
                {
                    var source = (BitmapSource)image.Source;
                    var row = new byte[source.PixelWidth];
                    source.CopyPixels(new Int32Rect(0, y, source.PixelWidth, 1), row, source.PixelWidth, 0);
                    return row[x];
                }
                bool AnyPixel(int left, int top, int width, int height)
                {
                    for (var y = top; y < top + height; y++)
                        for (var x = left; x < left + width; x++)
                            if (PixelAt(x, y) != 0) return true;
                    return false;
                }

                await Open(2);
                var roomHeightOff = viewport.ActualHeight;
                Check(toggle.IsChecked == false && strip.Visibility != Visibility.Visible, "the verb area is off by default and not shown");

                toggle.IsChecked = true;
                await Settle();
                Check(strip.Visibility == Visibility.Visible, "on: the strip is shown under the room");
                Check(Math.Abs(image.ActualWidth - viewport.ActualWidth) < 1.5 &&
                      Math.Abs(image.ActualHeight - image.ActualWidth * 56 / 320) < 1.5,
                    $"on: the strip is 320 x 56 screen pixels at the room's scale ({image.ActualWidth:0.#} x {image.ActualHeight:0.#}; room view {viewport.ActualWidth:0.#} wide)");
                var bottom = strip.TranslatePoint(new Point(0, strip.ActualHeight), host).Y;
                Check(bottom <= host.ActualHeight + 0.5 && viewport.ActualHeight < roomHeightOff,
                    $"on: the room shrinks so the strip and its note fit (strip bottom {bottom:0.#} of {host.ActualHeight:0.#}; room {roomHeightOff:0.#} → {viewport.ActualHeight:0.#})");
                Check(note.Text.Contains("presentation", StringComparison.Ordinal) && note.Text.Contains("not the game's drawing", StringComparison.Ordinal) &&
                      note.Text.Contains("no image verb is on", StringComparison.Ordinal),
                    $"on, after the boot: nothing is on, and the note says it is a presentation: \"{note.Text}\"");
                Check(!AnyPixel(0, 0, 320, 56), "on, after the boot: the strip is empty");
                Save("room-02-verb-area-boot.png");

                // Script 018 [2] turns the distaff notes on (as script 003 does in play).
                var script = new LoomScriptDisassembly(250, 1, 0, 32, 16,
                    [new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"), new(5, 0, "stopObjectCode", "", "00")],
                    null, null, [], [], "Synthetic test script");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript", Private)!.Invoke(Session(), [script]);
                Call("UpdateVerbArea");
                await Settle();
                Check(note.Text.Contains("verbs 0x01–0x08, 0x31, 0x32 on", StringComparison.Ordinal) &&
                      (note.ToolTip as string ?? "").Contains("verb 0x01 (slot 1): object 100's image from Room 01 at (16, 144)", StringComparison.Ordinal),
                    $"after script 018: the note lists the verbs that are on, the tooltip their images: \"{note.Text}\"");
                Check(AnyPixel(16, 0, 24, 16) && AnyPixel(192, 0, 24, 16),
                    "after script 018: the first and last distaff notes (x 16 and 192, row 144) are drawn");
                Save("room-02-verb-area-distaff.png");

                // The pointer over object 156 (class 1): script 008 turns the close-up (verb 0x36) on at (248, 152).
                pointer.IsChecked = true;
                Call("SendPointerToGame", (ValueTuple<int, int>?)(700, 40), (int?)156);
                await Settle();
                var closeUp = Session().Verbs.FirstOrDefault(verb => verb.VerbId == 0x36);
                Check(closeUp is { Mode: 1, X: 248, Y: 152 } && note.Text.Contains("0x36", StringComparison.Ordinal) &&
                      AnyPixel(248, 8, 16, 8),
                    $"hover on object 156: the close-up (verb 0x36, {closeUp?.Describe()}) is drawn at (248, 152): \"{note.Text}\"");
                Save("room-02-verb-area-close-up.png");
                pointer.IsChecked = false;

                toggle.IsChecked = false;
                await Settle();
                Check(strip.Visibility != Visibility.Visible && Math.Abs(viewport.ActualHeight - roomHeightOff) < 0.5,
                    $"off again: the strip is gone and the room has its size back ({viewport.ActualHeight:0.#})");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written must not crash the harness: fall back to the temp folder.
                try { File.WriteAllLines(Path.Combine(output, "verb-area-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    var fallback = Path.Combine(Path.GetTempPath(), "SCUMMexplorer-harness", "verb-area-check.txt");
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                        File.WriteAllLines(fallback, report, System.Text.Encoding.UTF8);
                        Console.Error.WriteLine($"Report not written ({writeError.Message}); wrote {fallback} instead.");
                    }
                    catch (Exception fallbackError) when (fallbackError is IOException or UnauthorizedAccessException)
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

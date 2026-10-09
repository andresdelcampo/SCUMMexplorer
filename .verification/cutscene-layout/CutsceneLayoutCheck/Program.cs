using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// The room view's layout while cutscenes play, in the real MainWindow on the real playback clock.
// Plays the opening (script 044: Rooms 01, 02, 01, 02, 53, 02) and the first 40 s of script 046 (the Elders' trial),
// sampling every 20 ms: the room view's size and top edge, the verb strip, the scroll arrows and the controls below.
// Checks: the room view keeps one size and position while a cutscene plays; the verb strip is shown throughout; the
// scroll arrows are not shown while the player has no control; the room is not overlapped by what is under it; and
// disabled buttons (Skip, Pause) are drawn dark, not white. No physical input. Arguments: <Games\loom> <output folder>.
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
        object? Property(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
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
        // The mean brightness (0–255) of an element as drawn.
        double Brightness(FrameworkElement element)
        {
            window.UpdateLayout();
            var width = Math.Max(1, (int)element.ActualWidth);
            var height = Math.Max(1, (int)element.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
                context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            double sum = 0;
            for (var i = 0; i < pixels.Length; i += 4) sum += (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3.0;
            return sum / (width * height);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; PlayOpening and PlayKnownCutscene are called as the menu calls them, on the playback clock. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool HasPlayback() => Field("_playback") is not null;
                bool InScene() => (bool)Property("SessionInScene")!;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                for (var n = 0; n < 100 && Shown() is null; n++) await Task.Delay(100);
                var host = Find<FrameworkElement>("RoomViewportHost");
                var viewport = Find<FrameworkElement>("RoomViewport");
                var verbStrip = Find<FrameworkElement>("VerbStripPanel");
                var arrows = Find<FrameworkElement>("RoomScrollButtonsPanel");
                var controls = Find<FrameworkElement>("RoomControlsPanel");

                // Disabled buttons once a playback has ended: Pause and Skip are off.
                void CheckDisabledButtons()
                {
                    var pause = Find<Button>("PlaybackPauseButton");
                    var skip = Find<Button>("SkipCutsceneButton");
                    var pauseBright = Brightness(pause);
                    var skipBright = Brightness(skip);
                    Save(Find<FrameworkElement>("PlaybackBar"), "playback-bar-idle.png");
                    Check(pause.IsVisible && !pause.IsEnabled && !skip.IsEnabled && pauseBright < 110 && skipBright < 110,
                        $"disabled Pause and Skip are drawn dark: mean brightness {pauseBright:0} and {skipBright:0} " +
                        $"(visible {pause.IsVisible}, enabled {pause.IsEnabled}, {skip.IsEnabled}; limit 110)");
                }

                async Task Play(string title, Action start, int limitMs, string prefix)
                {
                    var clock = Stopwatch.StartNew();
                    start();
                    var sizes = new Dictionary<string, int>();
                    var log = new List<string>();
                    string? last = null;
                    var arrowsInScene = 0;
                    var stripOff = 0;
                    var overlap = 0;
                    var samples = 0;
                    var saved = new HashSet<int>();
                    while (clock.ElapsedMilliseconds < limitMs)
                    {
                        await Task.Delay(20);
                        if (!HasPlayback() && clock.ElapsedMilliseconds > 500) break;
                        if (!InScene() || Shown() is not int room || Session().Snapshot.RoomId != room) continue;
                        samples++;
                        var top = viewport.TranslatePoint(new Point(0, 0), host);
                        var stripTop = verbStrip.IsVisible ? verbStrip.TranslatePoint(new Point(0, 0), host).Y : double.NaN;
                        var arrowsTop = arrows.IsVisible ? arrows.TranslatePoint(new Point(0, 0), host).Y : double.NaN;
                        var bottom = top.Y + viewport.ActualHeight;
                        if (!double.IsNaN(stripTop) && stripTop < bottom - 0.5 || !double.IsNaN(arrowsTop) && arrowsTop < bottom - 0.5) overlap++;
                        if (arrows.IsVisible) arrowsInScene++;
                        if (!verbStrip.IsVisible) stripOff++;
                        var size = $"view {viewport.ActualWidth:0.#}x{viewport.ActualHeight:0.#} at y {top.Y:0.#}";
                        sizes[size] = sizes.GetValueOrDefault(size) + 1;
                        var now = $"Room {room:00}: {size}, verb strip {(verbStrip.IsVisible ? $"at y {stripTop:0.#}" : "off")}, " +
                                  $"arrows {(arrows.IsVisible ? "shown" : arrows.Visibility.ToString().ToLowerInvariant())}, controls {controls.ActualHeight:0.#} high";
                        if (now != last) log.Add($"{clock.ElapsedMilliseconds,6} ms: {now}");
                        last = now;
                        if (saved.Add(room)) Save(Find<FrameworkElement>("RoomDisplayLayout"), $"{prefix}-room{room:00}.png");
                    }
                    report.Add($"{title} ({clock.ElapsedMilliseconds} ms, {samples} samples while the player had no control):");
                    report.AddRange(log.Select(line => "      " + line));
                    report.Add("   Sizes: " + string.Join("; ", sizes.Select(pair => $"{pair.Key} ({pair.Value} samples)")));
                    Check(samples > 0 && sizes.Count == 1, $"{title}: the room view keeps one size and position ({sizes.Count} seen)");
                    Check(samples > 0 && stripOff == 0, $"{title}: the verb strip is shown throughout ({stripOff} samples without it)");
                    Check(samples > 0 && arrowsInScene == 0, $"{title}: no scroll arrows while the player has no control ({arrowsInScene} samples with them)");
                    Check(overlap == 0, $"{title}: nothing below the room overlaps its bottom rows ({overlap} samples)");
                    if (HasPlayback())
                    {
                        Call("StopScenePlayback");
                        await Task.Delay(200);
                    }
                }

                await Play("The opening (script 044)", () => Call("PlayOpening"), 240000, "opening");
                await Task.Delay(300);
                CheckDisabledButtons();
                var cutscenes = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var trial = cutscenes.Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 46);
                await Play("Script 046, the Elders' trial (first 40 s)", () => Call("PlayKnownCutscene", trial), 40000, "trial");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "cutscene-layout-check.txt"), report, System.Text.Encoding.UTF8); }
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

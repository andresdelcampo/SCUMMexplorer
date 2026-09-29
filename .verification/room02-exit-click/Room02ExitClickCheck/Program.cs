using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Room 02's exit click in the real MainWindow, on the playback clock. After the boot the
// ego stands on the cliff (828, 60). A single click on the exit (object 153 at (450, 60))
// walks it down the path until script 045's box-4 change of scene puts the smaller Bobbin at
// (798, 118) and walks it to (756, 107). The click began a cutscene and reached no room
// load, so the app runs no doorway fallback: the view stays in Room 02 and waits for another
// click, as the game does. Two more clicks on the same point (box 10's change of scene, then
// the walk point and verb 0x38) reach Room 03 through the game's click.
// No physical input. Arguments: <Games\loom> <output folder>.
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
        T Find<T>(string name) => (T)window.FindName(name);
        void Save(string file)
        {
            window.UpdateLayout();
            var group = Find<FrameworkElement>("RoomDisplayGroup");
            var width = Math.Max(1, (int)group.ActualWidth);
            var height = Math.Max(1, (int)group.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(group), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file));
            encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; room points are given to HandleRoomPoint on the playback clock. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Find<TreeView>("ResourceTree");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool Playing() => (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                string Status() => Find<TextBlock>("StatusText").Text;
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 2 &&
                        i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 100 && Shown() != 2; n++) await Task.Delay(100);
                await Task.Delay(300);
                window.UpdateLayout();
                var ego = Session().Actors[0];
                Check(Shown() == 2 && (ego.X, ego.Y) == (828, 60), $"Room 02 after the boot, the ego on the cliff: ({ego.X}, {ego.Y})");

                // One click on the exit, played on the clock (10 frames a second) until playback ends.
                async Task<(long Ms, List<string> Log)> Click(int x, int y, int room)
                {
                    var clock = Stopwatch.StartNew();
                    var log = new List<string>();
                    Call("HandleRoomPoint", x, y, false);
                    for (var n = 0; n < 100 && !Playing() && Shown() == room; n++) await Task.Delay(20);
                    (int?, int?, int?, int?) last = default;
                    while (clock.ElapsedMilliseconds < 40000)
                    {
                        await Task.Delay(20);
                        var e = Session().Actors[0];
                        var now = (Session().Snapshot.RoomId, e.X, e.Y, e.Look.Costume);
                        if (now != last && (now.Item4 != last.Item4 || now.Item1 != last.Item1 || log.Count == 0))
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: Room {now.Item1:00}, ego ({now.Item2}, {now.Item3}) costume {now.Item4}, view Room {Shown():00}, cutscene {Session().InCutscene}");
                        last = now;
                        if (!Playing() && clock.ElapsedMilliseconds > 500) break;
                    }
                    await Task.Delay(400);
                    return (clock.ElapsedMilliseconds, log);
                }

                var first = await Click(450, 60, 2);
                report.Add("First click on the exit (450, 60), costume and room changes:");
                report.AddRange(first.Log.Select(line => "      " + line));
                ego = Session().Actors[0];
                report.Add($"      after {first.Ms} ms: status \"{Status()}\"; held: {Field("_lastDoorwayHeld") ?? "no"}; fallback: {Field("_lastDoorwayFallback") ?? "no"}");
                Check(Shown() == 2 && Session().Snapshot.RoomId == 2, $"the view stays in Room 02: view Room {Shown():00}, session Room {Session().Snapshot.RoomId:00}");
                Check((ego.X, ego.Y) == (756, 107) && ego.Look.Costume == 2,
                    $"the smaller Bobbin (costume 2) waits on the path after script 045's box-4 scene: ({ego.X}, {ego.Y}) costume {ego.Look.Costume}");
                Check(Field("_lastDoorwayFallback") is null && Field("_lastDoorwayHeld") is string,
                    "no doorway fallback ran: the click began a cutscene, and the game waits for another click");
                Check(Status().Contains("click again", StringComparison.Ordinal), $"the status says to click again: \"{Status()}\"");
                Save("room02-exit-first-click-app.png");

                var second = await Click(450, 60, 2);
                report.Add("Second click on the same point:");
                report.AddRange(second.Log.Select(line => "      " + line));
                ego = Session().Actors[0];
                Check(Shown() == 2 && (ego.X, ego.Y) == (609, 110) && Field("_lastDoorwayHeld") is string,
                    $"the second click stops at box 10's change of scene and waits again: ({ego.X}, {ego.Y}), view Room {Shown():00}");
                Save("room02-exit-second-click-app.png");

                var third = await Click(450, 60, 2);
                for (var n = 0; n < 100 && (Shown() != 3 || Playing()); n++) await Task.Delay(100);
                report.Add("Third click on the same point:");
                report.AddRange(third.Log.Select(line => "      " + line));
                Check(Shown() == 3 && Session().LastAction is { IsGameInput: true } && Field("_lastDoorwayFallback") is null,
                    $"the third click reaches the walk point, runs verb 0x38 and enters Room 03 through the game's click: view Room {Shown():00}; status \"{Status()}\"");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "room02-exit-click-check.txt"), report, System.Text.Encoding.UTF8); }
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

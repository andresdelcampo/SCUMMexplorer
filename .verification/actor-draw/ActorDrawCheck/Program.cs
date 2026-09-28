using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// M2c in the real MainWindow: after the boot, Room 02 shows the session's actors on the
// actor layer (RoomActorOverlay) over the room image, sorted by y and drawn at
// (x, y − elevation). The check captures the room view, measures the ego's pixels,
// then clicks empty floor (the game's click through HandleRoomPoint, assumed input)
// and records the actor layer on every playback frame: the walk ends at once in the
// session (labelled estimate), so what the frames show is recorded, not assumed.
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
            var report = new List<string> { "Real MainWindow; room points are given to HandleRoomPoint. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Find<TreeView>("ResourceTree");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool Playing() => (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && Shown() != room; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                }
                var overlay = Find<Image>("RoomActorOverlay");
                // The layer's opaque pixels as (count, bounds, hash) for the frame log.
                (int Count, (int, int, int, int)? Bounds, int Hash) Layer()
                {
                    if (overlay.Visibility != Visibility.Visible || overlay.Source is not BitmapSource source) return (0, null, 0);
                    var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
                    source.CopyPixels(pixels, source.PixelWidth * 4, 0);
                    int count = 0, left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1, hash = 17;
                    for (var i = 0; i < source.PixelWidth * source.PixelHeight; i++)
                    {
                        if (pixels[i * 4 + 3] == 0) continue;
                        count++;
                        int x = i % source.PixelWidth, y = i / source.PixelWidth;
                        (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
                        hash = unchecked(hash * 31 + i * 7 + pixels[i * 4] + pixels[i * 4 + 1] * 3 + pixels[i * 4 + 2] * 5);
                    }
                    return (count, count == 0 ? null : (left, top, right, bottom), hash);
                }
                string Actors() => string.Join("; ", Session().Actors.Where(a => a.Room == Session().Snapshot.RoomId && a.Visible == true && a.Look.Costume is not 0)
                    .Select(a => $"A{a.Number} costume {a.Look.Costume} at ({a.X}, {a.Y}) frame {a.Look.Frame} facing {a.Look.Facing} limbs [{string.Join(",", a.Look.Animation.Positions.Take(3))}]"));

                await Open(2);
                Check(Session().Snapshot.RoomId == 2, "Room 02 is open in the explore mode after the boot");
                var ego = Session().Actors[0];
                report.Add($"      actors in the room: {Actors()}");
                var first = Layer();
                Check(overlay.Visibility == Visibility.Visible && first.Count > 0, $"the actor layer is shown: {first.Count} pixels, bounds {first.Bounds}");
                // The ego stands at (828, 60): its pixels lie around x 828 and end at row 60.
                var egoPixels = CountIn(overlay, 810, 15, 846, 62);
                Check(egoPixels > 100, $"the ego is drawn at its feet (828, 60): {egoPixels} pixels in (810, 15)–(846, 62)");
                Save("room02-after-boot-app.png");

                // A single click on empty floor (Room 02 box 4 at (860, 118)): scripts 005–007 walk the ego there.
                var clock = Stopwatch.StartNew();
                Call("HandleRoomPoint", 860, 118, false);
                for (var n = 0; n < 100 && !Playing(); n++) await Task.Delay(20);
                var frames = new List<string>();
                var hashes = new HashSet<int>();
                var lastHash = first.Hash;
                var saved = 0;
                while (clock.ElapsedMilliseconds < 20000)
                {
                    await Task.Delay(15);
                    var layer = Layer();
                    if (layer.Hash != lastHash)
                    {
                        lastHash = layer.Hash;
                        hashes.Add(layer.Hash);
                        frames.Add($"{clock.ElapsedMilliseconds,5} ms: {layer.Count} px, bounds {layer.Bounds}; {Actors()}");
                        if (saved < 6) Save($"room02-click-frame{++saved}-app.png");
                    }
                    if (!Playing() && clock.ElapsedMilliseconds > 1500) break;
                }
                report.Add("Actor layer changes while the click played (changes only):");
                report.AddRange(frames.Select(f => "      " + f));
                ego = Session().Actors[0];
                report.Add($"      after the click: {Actors()}; ego estimate: {ego.Estimate ?? "none"}; look estimate: {ego.Look.Estimate ?? "none"}");
                Check(hashes.Count > 0, $"the actor layer was redrawn during the click's playback ({hashes.Count} distinct pictures)");
                Check(ego.X != 828 || ego.Y != 60, $"the ego moved: now at ({ego.X}, {ego.Y}) (the walk ends at once: labelled estimate)");
                var after = Layer();
                Check(after.Bounds is { } b && ego.X is int ex && b.Item1 <= ex && b.Item3 >= ex,
                    $"the drawn ego follows the session's position: layer bounds {after.Bounds}, ego x {ego.X}");
                Save("room02-after-click-app.png");

                // Another room: the layer only shows actors in the shown room.
                await Open(3);
                var room3 = Layer();
                report.Add($"      Room 03 visit: session room {Session().Snapshot.RoomId}; actors {Actors()}; layer {room3.Count} px");
                Check(Session().Snapshot.RoomId != 3 || room3.Count == 0 || Session().Actors.Any(a => a.Room == 3 && a.Visible == true && a.Look.Costume is not 0),
                    "in Room 03 the layer shows only actors in that room");
                Save("room03-visit-app.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "actor-draw-check.txt"), report, System.Text.Encoding.UTF8); }
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

    static int CountIn(Image overlay, int left, int top, int right, int bottom)
    {
        if (overlay.Source is not BitmapSource source) return 0;
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        var count = 0;
        for (var y = Math.Max(0, top); y < Math.Min(bottom, source.PixelHeight); y++)
        for (var x = Math.Max(0, left); x < Math.Min(right, source.PixelWidth); x++)
            if (pixels[(y * source.PixelWidth + x) * 4 + 3] != 0) count++;
        return count;
    }
}

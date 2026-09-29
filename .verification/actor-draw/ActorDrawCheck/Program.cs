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
// and records the actor layer on every playback frame. Since M3a the walk is stepped
// (walkActor): the frames show the ego's walk frame and its steps, and a second click
// on the path shows the camera following the ego 8 pixels a frame (moveCamera). Since M3b
// a third click on the exit runs object 153's verb 0x38: its cutscene pans the camera and
// waits `delay 60` jiffies (11 frames of 6 jiffies) on the playback clock before Room 03.
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
                // M3a: Room 02 local 202 turns the ego towards the fairy on every frame while it flies off left.
                Check(ego.Look.Facing == 270, $"after the boot the ego faces 270 (left profile, as in ScummVM): facing {ego.Look.Facing}");
                Save("room02-after-boot-app.png");

                // A single click on empty floor (Room 02 box 4 at (860, 118)): scripts 005–007 walk the ego there.
                var clock = Stopwatch.StartNew();
                Call("HandleRoomPoint", 860, 118, false);
                for (var n = 0; n < 100 && !Playing(); n++) await Task.Delay(20);
                var frames = new List<string>();
                var hashes = new HashSet<int>();
                var lastHash = first.Hash;
                var saved = 0;
                var steps = new List<(int X, int Y, int? Frame, string Limbs)>();
                while (clock.ElapsedMilliseconds < 30000)
                {
                    await Task.Delay(15);
                    var layer = Layer();
                    if (layer.Hash != lastHash)
                    {
                        lastHash = layer.Hash;
                        hashes.Add(layer.Hash);
                        var walker = Session().Actors[0];
                        steps.Add((walker.X ?? -1, walker.Y ?? -1, walker.Look.Frame, string.Join(",", walker.Look.Animation.Positions.Take(3))));
                        frames.Add($"{clock.ElapsedMilliseconds,5} ms: {layer.Count} px, bounds {layer.Bounds}; moving {walker.Moving}; camera {Session().CameraX}; {Actors()}");
                        if (saved < 6) Save($"room02-click-frame{++saved}-app.png");
                    }
                    if (!Playing() && clock.ElapsedMilliseconds > 1500) break;
                }
                report.Add("Actor layer changes while the click played (changes only):");
                report.AddRange(frames.Select(f => "      " + f));
                ego = Session().Actors[0];
                report.Add($"      after the click: {Actors()}; ego estimate: {ego.Estimate ?? "none"}; look estimate: {ego.Look.Estimate ?? "none"}");
                Check(hashes.Count > 0, $"the actor layer was redrawn during the click's playback ({hashes.Count} distinct pictures)");
                Check(ego.X != 828 || ego.Y != 60, $"the ego moved: now at ({ego.X}, {ego.Y})");
                // walkActor: each frame moves the ego by at most its walk speed; script 045's putActor is the one jump.
                var moves = steps.Zip(steps.Skip(1)).Where(pair => (pair.First.X, pair.First.Y) != (pair.Second.X, pair.Second.Y)).ToArray();
                var small = moves.Count(pair => Math.Abs(pair.Second.X - pair.First.X) <= 8 && Math.Abs(pair.Second.Y - pair.First.Y) <= 2);
                Check(small >= 10 && moves.Length - small <= 2, $"the ego walks in steps: {small} frames move it by at most (8, 2), {moves.Length - small} jump (script 045's putActor)");
                var walkFrames = steps.Where(step => step.Frame == 9).ToArray();
                Check(walkFrames.Length >= 10 && walkFrames.Select(step => step.Limbs).Distinct().Count() >= 3,
                    $"the walk frame (9, Bobbin without the distaff) plays while it walks: {walkFrames.Length} frames, {walkFrames.Select(step => step.Limbs).Distinct().Count()} limb poses");
                var after = Layer();
                Check(after.Bounds is { } b && ego.X is int ex && b.Item1 <= ex && b.Item3 >= ex,
                    $"the drawn ego follows the session's position: layer bounds {after.Bounds}, ego x {ego.X}");
                Save("room02-after-click-app.png");

                // A second click on the path's floor (box 10 at (627, 124), no object): the smaller ego walks left down
                // the path and the camera follows it, until script 045's box-10 change of scene stops it at (609, 110).
                clock.Restart();
                var viewport = Find<ScrollViewer>("RoomViewport");
                Call("HandleRoomPoint", 627, 124, false);
                for (var n = 0; n < 100 && !Playing(); n++) await Task.Delay(20);
                var cameras = new List<int>();
                var offsets = new List<double>();
                var cameraLog = new List<string>();
                while (clock.ElapsedMilliseconds < 40000)
                {
                    await Task.Delay(15);
                    // Only Room 02: when the walk stops at a change of scene, the app's labelled doorway fallback may enter Room 03.
                    if (Session().Snapshot.RoomId == 2 && Session().CameraX is int camera && (cameras.Count == 0 || cameras[^1] != camera))
                    {
                        cameras.Add(camera);
                        offsets.Add(viewport.HorizontalOffset);
                        var walker = Session().Actors[0];
                        cameraLog.Add($"{clock.ElapsedMilliseconds,5} ms: camera {camera}, view offset {viewport.HorizontalOffset:0}, ego ({walker.X}, {walker.Y}) frame {walker.Look.Frame}");
                        if (cameras.Count == 6) Save("room02-camera-follows-app.png");
                    }
                    if (!Playing() && clock.ElapsedMilliseconds > 1500) break;
                }
                report.Add("Camera changes during the second click (changes only):");
                report.AddRange(cameraLog.Select(line => "      " + line));
                var pans = cameras.Zip(cameras.Skip(1)).ToArray();
                Check(pans.Length >= 5 && pans.All(pair => Math.Abs(pair.Second - pair.First) == 8),
                    $"the camera follows the walk 8 pixels a frame: {pans.Length} moves, from {cameras.FirstOrDefault()} to {cameras.LastOrDefault()}");
                Check(offsets.Distinct().Count() >= 5, $"the view scrolls with the camera: {offsets.Distinct().Count()} offsets");
                ego = Session().Actors[0];
                report.Add($"      after the second click: Room {Session().Snapshot.RoomId:00}; {Actors()}; status: {Find<TextBlock>("StatusText").Text}");

                // M3b: a third click on the exit (object 153). Script 007 runs its verb 0x38: a cutscene pans the camera
                // to 440, then `delay 0x3C` (+0x0026) holds the view for 60 jiffies before loadRoomWithEgo 3.
                // decreaseScriptDelay runs the script again once 60 − 6k < 0: 11 frames, 66 jiffies, 1.1 s at 10 frames a second.
                clock.Restart();
                var traceFrom = Session().SessionTrace.Count;
                Call("HandleRoomPoint", 450, 60, false);
                for (var n = 0; n < 100 && !Playing(); n++) await Task.Delay(20);
                (long Ms, int Jiffies, int? Camera)? delayStart = null, delayEnd = null;
                int? roomAfterDelay = null;
                while (clock.ElapsedMilliseconds < 40000 && delayEnd is null)
                {
                    await Task.Delay(5);
                    var trace = Session().SessionTrace;
                    var total = (int)Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x02F]");
                    for (; traceFrom < trace.Count; traceFrom++)
                    {
                        if (delayStart is null && trace[traceFrom].StartsWith("Room 02 Object 153 Verb 0x38 waits 60 jiffies", StringComparison.Ordinal))
                        {
                            delayStart = (clock.ElapsedMilliseconds, total, Session().CameraX);
                            Save("room02-exit-delay-app.png");
                        }
                        if (delayStart is not null && trace[traceFrom].StartsWith("Room 02 Object 153 Verb 0x38's delay ends", StringComparison.Ordinal))
                        {
                            delayEnd = (clock.ElapsedMilliseconds, total, Session().CameraX);
                            roomAfterDelay = Session().Snapshot.RoomId;
                        }
                    }
                    if (!Playing() && clock.ElapsedMilliseconds > 1500) break;
                }
                for (var n = 0; n < 100 && Shown() != Session().Snapshot.RoomId; n++) await Task.Delay(50);
                report.Add($"      exit click: delay starts at {delayStart?.Ms} ms (VAR_TIMER_TOTAL {delayStart?.Jiffies}, camera {delayStart?.Camera}), " +
                           $"ends at {delayEnd?.Ms} ms (VAR_TIMER_TOTAL {delayEnd?.Jiffies}); then Room {roomAfterDelay:00}; view shows Room {Shown():00}");
                Check(delayStart?.Camera == 440, $"the exit's cutscene pans the camera to 440 before its delay: camera {delayStart?.Camera}");
                Check(delayStart is { } ds && delayEnd is { } de && de.Jiffies - ds.Jiffies == 66,
                    $"the delay of 60 jiffies lasts 11 frames of 6 jiffies on the session clock: {delayEnd?.Jiffies - delayStart?.Jiffies} jiffies");
                Check(delayStart is { } ws && delayEnd is { } we && we.Ms - ws.Ms is >= 900 and <= 3000,
                    $"the view holds for about 1.1 s on the playback clock: {delayEnd?.Ms - delayStart?.Ms} ms");
                Check(roomAfterDelay == 3 && Shown() == 3, $"after the delay loadRoomWithEgo enters Room 03: session Room {roomAfterDelay:00}, view Room {Shown():00}");

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

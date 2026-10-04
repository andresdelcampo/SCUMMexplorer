using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Plays catalog scenes through the real MainWindow (PlayKnownCutscene, the picker's own entry) and saves the room view
// every <interval> ms with the on-screen texts and the picker note. No physical input.
// Arguments: <Games\loom> <output folder> <seconds> <interval ms> <catalog id or title>...   (id = loom-replays.json "id")
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var seconds = int.Parse(args[2]);
        var interval = int.Parse(args[3]);
        var ids = args.Skip(4).ToArray();
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
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1500);
                var all = ((IEnumerable)typeof(MainWindow).GetMethod("AllCutscenes", Private)!.Invoke(window, null)!).Cast<object>().ToList();
                string Prop(object o, string n) => o.GetType().GetProperty(n)!.GetValue(o)?.ToString() ?? "";
                for (var s = 0; s < ids.Length; s++)
                {
                    var wanted = ids[s];
                    var scene = all.FirstOrDefault(o => Prop(o, "SequenceId") == wanted || Prop(o, "Id") == wanted || Prop(o, "Title") == wanted);
                    if (scene is null) { report.Add($"NOT FOUND: {wanted}"); continue; }
                    var tag = $"s{s}";
                    report.Add($"== {wanted} ({Prop(scene, "Title")})");
                    report.Add("   start note: " + (string)typeof(MainWindow).GetMethod("CutsceneStartNote", Private)!.Invoke(window, [scene])!);
                    var clock = Stopwatch.StartNew();
                    var before = Field("_session");
                    { var bs = (LoomExplorationSession?)before; var b4 = bs?.Actors.FirstOrDefault(a => a.Number == 4); report.Add($"   before: session room {bs?.Snapshot.RoomId}; actor4 room {b4?.Room} at ({b4?.X},{b4?.Y}) costume {b4?.Look.Costume} estimate {b4?.Estimate}"); }
                    Call("PlayKnownCutscene", scene);
                    report.Add($"   session kept: {ReferenceEquals(before, Field("_session"))}");
                    var n = 0;
                    var lastText = ""; var lastActors = "";
                    var nextShot = 0L;
                    while (clock.ElapsedMilliseconds < seconds * 1000L)
                    {
                        await Task.Delay(40);
                        var session = (LoomExplorationSession)Field("_session")!;
                        var room = session.Snapshot.RoomId;
                        var text = string.Join(" | ", session.Messages.OnScreen.Where(m => m.Lines.Any(l => l.Trim().Length > 0)).Select(m => $"{m.SourceLabel}: {string.Join(" / ", m.Lines)}"));
                        if (text != lastText) { report.Add($"   {clock.ElapsedMilliseconds,6} ms room {room:00}: {text}"); lastText = text; report.Add("      actors: " + string.Join("; ", session.Actors.Where(a => a.Room == room).Select(a => $"#{a.Number} ({a.X},{a.Y}) vis={a.Visible} costume={a.Look.Costume} frame={a.Look.Frame} elev={a.Look.Elevation}"))); }
                        var actorSig = string.Join("; ", session.Actors.Where(a => a.Room == room).Select(a => $"#{a.Number} vis={a.Visible} costume={a.Look.Costume}"));
                        if (actorSig != lastActors && actorSig.Contains("#8")) {
                            var rimg = (SCUMMexplorer.Core.Rendering.RoomImage)Field("_currentRoom")!; var gm = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                            var sel = Field("_objectSelection")!; var mask = (byte[]?)sel.GetType().GetMethod("ComposeDepthMask")!.Invoke(sel, [rimg]);
                            foreach (var useMask in new[] { false, true }) {
                                var lay = SCUMMexplorer.Core.Rendering.LoomActorRenderer.Render(session.Actors, room!.Value, rimg.Width, rimg.Height, id => SCUMMexplorer.Core.Rendering.LoomCostume.Load(gm, id), useMask ? mask : null, session.CurrentWalkBoxes, session.ObjectClasses);
                                report.Add($"      render mask={useMask}: " + string.Join("; ", lay.Actors.Select(a => $"#{a.Number} px={a.Pixels} z={a.ZPlane} bounds={a.Bounds} dir={a.Direction} frame={a.Frame}")) + " skipped: " + string.Join("|", lay.Skipped));
                            }
                                                        { var only = SCUMMexplorer.Core.Rendering.LoomActorRenderer.Render(session.Actors.Where(a => a.Number == 8).ToArray(), room!.Value, rimg.Width, rimg.Height, id => SCUMMexplorer.Core.Rendering.LoomCostume.Load(gm, id));
                              var hist = only.Pixels.Where(b => b != 0xFF).GroupBy(b => b).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}=#{rimg.Palette[g.Key]:X6}"); report.Add("      actor8 colours: " + string.Join(" ", hist)); }
                            var a4 = session.Actors.First(a => a.Number == 8); report.Add($"      actor4 box={a4.WalkBox} look.anim={a4.Look.Animation} facing={a4.Look.Facing} classes={(session.ObjectClasses.TryGetValue(4, out var cb) ? cb : 0u):X}");
                        }
                        if (actorSig != lastActors) { report.Add($"   {clock.ElapsedMilliseconds,6} ms ACTORS: {actorSig}"); lastActors = actorSig; Save($"{tag}-actors-{clock.ElapsedMilliseconds}.png"); }
                        if (clock.ElapsedMilliseconds >= nextShot) { Save($"{tag}-{n++:00}.png"); nextShot += interval; }
                        if (Field("_playback") is null && clock.ElapsedMilliseconds > 800) break;
                    }
                    Save($"{tag}-end.png");
                    { window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render((Visual)window.Content);
                      var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var st = File.Create(Path.Combine(output, $"{tag}-window.png")); enc.Save(st); }
                    report.Add($"   ended after {clock.ElapsedMilliseconds} ms, {n} shots; status: {Find<TextBlock>("StatusText").Text}");
                    var sess = (LoomExplorationSession)Field("_session")!;
                    var ego = sess.Actors.FirstOrDefault(a => a.Number == 1);
                    report.Add($"   room view size: {Find<FrameworkElement>("RoomDisplayGroup").ActualWidth:0}x{Find<FrameworkElement>("RoomDisplayGroup").ActualHeight:0}; controls {Find<FrameworkElement>("RoomControlsPanel").ActualHeight:0}; window {window.ActualWidth:0}x{window.ActualHeight:0}");
                    report.Add($"   story bits set: {string.Join(" ", sess.StoryEvents.Where(e => sess.StoryBit(e.Key) == true).Select(e => e.Key))}");
                    report.Add($"   ego: room {ego?.Room} at ({ego?.X},{ego?.Y}) costume {ego?.Look.Costume:X2} staff {sess.GameStatus.StaffHeld}");
                }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

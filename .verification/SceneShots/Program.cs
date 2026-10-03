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
                    var scene = all.FirstOrDefault(o => Prop(o, "SequenceId") == wanted || Prop(o, "Title") == wanted);
                    if (scene is null) { report.Add($"NOT FOUND: {wanted}"); continue; }
                    var tag = $"s{s}";
                    report.Add($"== {wanted} ({Prop(scene, "Title")})");
                    report.Add("   start note: " + (string)typeof(MainWindow).GetMethod("CutsceneStartNote", Private)!.Invoke(window, [scene])!);
                    var clock = Stopwatch.StartNew();
                    Call("PlayKnownCutscene", scene);
                    var n = 0;
                    var lastText = "";
                    var nextShot = 0L;
                    while (clock.ElapsedMilliseconds < seconds * 1000L)
                    {
                        await Task.Delay(40);
                        var session = (LoomExplorationSession)Field("_session")!;
                        var room = session.Snapshot.RoomId;
                        var text = string.Join(" | ", session.Messages.OnScreen.Where(m => m.Lines.Any(l => l.Trim().Length > 0)).Select(m => $"{m.SourceLabel}: {string.Join(" / ", m.Lines)}"));
                        if (text != lastText) { report.Add($"   {clock.ElapsedMilliseconds,6} ms room {room:00}: {text}"); lastText = text; }
                        if (clock.ElapsedMilliseconds >= nextShot) { Save($"{tag}-{n++:00}.png"); nextShot += interval; }
                        if (Field("_playback") is null && clock.ElapsedMilliseconds > 800) break;
                    }
                    Save($"{tag}-end.png");
                    report.Add($"   ended after {clock.ElapsedMilliseconds} ms, {n} shots; status: {Find<TextBlock>("StatusText").Text}");
                    var sess = (LoomExplorationSession)Field("_session")!;
                    var ego = sess.Actors.FirstOrDefault(a => a.Number == 1);
                    report.Add($"   ego: room {ego?.Room} at ({ego?.X},{ego?.Y}) costume {ego?.Look.Costume:X2} staff {sess.GameStatus.StaffHeld}");
                }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

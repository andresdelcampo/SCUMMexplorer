using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: Monkey2CutsceneWindowCheck <Monkey Island 2 folder> <output folder> <scene title>...
// S7 Monkey2 (18 October 2026), after the MonkeyCd check: opens the real MainWindow on Monkey Island 2 with a private profile (SCUMM_EXPLORER_PROFILE_ROOT)
// and a private room-list cache (SCUMM_ROOM_CACHE_DIR), both in the output folder; opens Room 4 (where the play boot ends) so play starts; times the
// background list (each worker boots from its own copy of the game); lists Room 9's scenes; then plays the named scenes from the global picker, each
// once the window offers the picker (no cutscene playing), and writes the scene-sound log (the iMUSE song the scene starts). Saves captures and
// cutscene-check.txt.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        string Status() => ((TextBlock)window.FindName("StatusText")).Text;
        string Lines(ScummGameSession s) => string.Join(" / ", s.Messages.OnScreen.Select(m => m.Text));
        string Title(object item) => (string)item.GetType().GetProperty("Title")!.GetValue(item)!;
        void Save(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            var player = (SceneSoundPlayer)Field("_sceneSound")!;
            try
            {
                var open = System.Diagnostics.Stopwatch.StartNew();
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var room4 = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 4 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room4);
                for (var n = 0; n < 900 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 4");
                var note = (TextBlock)window.FindName("CutsceneScanNote");
                report.Add($"Play started in Room {session.CurrentRoomId} {open.Elapsed.TotalSeconds:0.0} s after opening; playback bar {((FrameworkElement)window.FindName("PlaybackBar")).Visibility}; " +
                           $"room button {((Button)window.FindName("RoomCutsceneButton")).Visibility}; scan note {note.Visibility}: \"{note.Text.Replace('\n', ' ')}\"");
                Save("cutscene-01-scan-running.png");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var n = 0; n < 6000 && !(bool)Field("_playRoomListsDone")!; n++)
                {
                    await Task.Delay(100);
                    if (n % 100 == 0) report.Add($"   t={n / 10} s: scan {Field("_scanDone")} of {Field("_scanTotal")}, play room {session.CurrentRoomId}, frame {Field("_playFrame")}");
                }
                report.Add($"background runs done after {clock.Elapsed.TotalSeconds:0} s (from play start; {open.Elapsed.TotalSeconds:0} s from opening): {(bool)Field("_playRoomListsDone")!}; " +
                           $"scan {Field("_scanDone")} of {Field("_scanTotal")}; play is in Room {session.CurrentRoomId}; note {note.Visibility}");
                await Task.Delay(500);
                var planner = ((Lazy<ScummCutscenePlanner>)Field("_playPlanner")!).Value;
                report.Add($"planner: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes, " +
                           $"{planner.Groups.Count(g => planner.TryGetRun(g.First, out var r) && r.Begun)} begun; runs cached {planner.ExportRoomLists().Count}");
                Save("cutscene-02-list-ready.png");

                var roomItems = ((System.Collections.IEnumerable)Call("RoomCutsceneItems", 9)!).Cast<object>().ToList();
                report.Add($"Room 9 list ({roomItems.Count}): {string.Join(" | ", roomItems.Select(Title))}");
                var picker = (Window)typeof(MainWindow).GetMethod("BuildCutscenePicker", Private)!.Invoke(window, null)!;
                picker.Close();
                var all = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("PlayableCutsceneItems", Private)!.Invoke(window, [null])!).Cast<object>()
                    .Select(t => t.GetType().GetField("Item2")!.GetValue(t)!).ToList();
                report.Add($"picker entries (Normal mode): {all.Count}; first five: {string.Join(" | ", all.Take(5).Select(Title))}");
                // The scenes named after the folders, in order; each starts only when the window would offer the picker (no cutscene playing), as a user can.
                var index = 3;
                foreach (var wanted in args.Skip(2))
                {
                    for (var n = 0; n < 600 && !(bool)typeof(MainWindow).GetProperty("PlayCanStartCutscene", Private)!.GetValue(window)!; n++) await Task.Delay(100);
                    if (!(bool)typeof(MainWindow).GetProperty("PlayCanStartCutscene", Private)!.GetValue(window)!) { report.Add($"\"{wanted}\" not started: a cutscene is still playing (in a dialogue?)"); continue; }
                    var room = int.Parse(wanted.AsSpan(5, 2), System.Globalization.CultureInfo.InvariantCulture);
                    var logFrom = player.Log.Count;
                    var item = all.First(i => Title(i) == wanted);
                    var begun = session.CutscenesBegun;
                    ((Action)item.GetType().GetProperty("Play")!.GetValue(item)!).Invoke();
                    for (var n = 0; n < 200 && session.CurrentRoomId != room; n++) await Task.Delay(100);
                    // A scene may begin after a wait of its own (Room 13 local script 211 waits 1800 jiffies before Largo comes in): up to 60 s.
                    for (var n = 0; n < 600 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                    var name = $"cutscene-{index++:00}-room-{room:00}.png";
                    report.Add($"played \"{wanted}\" from the picker: room {session.CurrentRoomId}, cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}; " +
                               $"on screen \"{Lines(session)}\"; Skip enabled {((Button)window.FindName("SkipCutsceneButton")).IsEnabled}; active sounds [{string.Join(",", session.Snapshot.ActiveSounds)}]; status \"{Status()}\"");
                    Save(name);
                    for (var n = 0; n < 150 && session.InCutscene; n++)
                    {
                        await Task.Delay(100);
                        if (n % 30 == 29) report.Add($"   +{(n + 1) / 10} s: room {session.CurrentRoomId}, in cutscene {session.InCutscene}, on screen \"{Lines(session)}\"");
                    }
                    report.Add($"   then: room {session.CurrentRoomId}, in cutscene {session.InCutscene}, on screen \"{Lines(session)}\"");
                    Save(name.Replace(".png", "-later.png", StringComparison.Ordinal));
                    report.Add($"-- scene sound log since \"{wanted}\" (device {SceneSoundPlayer.DeviceName(player.Mode)})");
                    foreach (var entry in player.Log.Skip(logFrom)) report.Add($"   frame {entry.Frame} {entry.Milliseconds} ms {entry.Action} {entry.SoundId} {entry.Kind} | {entry.Source}");
                }
                report.Add($"frames played by the window: {Field("_playFrame")}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "cutscene-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

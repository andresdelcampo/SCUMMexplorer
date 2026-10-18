using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: MonkeyCdCutsceneWindowCheck <Monkey Island CD folder> <output folder>
// S7 MonkeyCd (18 October 2026), after the floppy check: opens the real MainWindow on the CD (disc image) with a private profile
// (SCUMM_EXPLORER_PROFILE_ROOT) and a private room-list cache (SCUMM_ROOM_CACHE_DIR), both in the output folder; opens Room 38 (where the play boot ends)
// so play starts; times the background list (each worker boots from its own copy of the game, read from the disc image); then plays the poster from
// Room 33's Play Cutscene list and Room 19's entry (the ship, Part II) from the global picker's list, and writes the scene-sound log (the disc track the
// scene starts). Saves captures and cutscene-check.txt.
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
                var firstLazy = Field("_playPlanner");
                string? lastTotal = null;
                for (var n = 0; n < 25; n++)
                {
                    var total = $"{Field("_scanTotal")}";
                    if (total != lastTotal) report.Add($"   {n * 100} ms after opening: scan total {total}, planner created {(Field("_playPlanner") as Lazy<ScummCutscenePlanner>)?.IsValueCreated}, same planner {ReferenceEquals(firstLazy, Field("_playPlanner"))}");
                    lastTotal = total;
                    await Task.Delay(100);
                }
                var room38 = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 38 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room38);
                for (var n = 0; n < 900 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 38");
                var bar = (FrameworkElement)window.FindName("PlaybackBar");
                var roomButton = (Button)window.FindName("RoomCutsceneButton");
                var note = (TextBlock)window.FindName("CutsceneScanNote");
                report.Add($"Play started in Room {session.CurrentRoomId} {open.Elapsed.TotalSeconds:0.0} s after opening; playback bar {bar.Visibility}; room button {roomButton.Visibility}; scan note {note.Visibility}: \"{note.Text.Replace('\n', ' ')}\"");
                Save("cutscene-01-scan-running.png");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var gate = (ManualResetEventSlim)Field("_scanGate")!;
                int closed = 0, samples = 0;
                for (var n = 0; n < 3000 && !(bool)Field("_playRoomListsDone")!; n++)
                {
                    await Task.Delay(100);
                    samples++; if (!gate.IsSet) closed++;
                    if (n % 100 == 0) report.Add($"   t={n / 10} s: scan {Field("_scanDone")} of {Field("_scanTotal")}, gate {(gate.IsSet ? "open" : "closed (a track is being prepared)")}, play room {session.CurrentRoomId}, frame {Field("_playFrame")}");
                }
                report.Add($"   the gate was closed in {closed} of {samples} samples");
                report.Add($"background runs done after {clock.Elapsed.TotalSeconds:0} s (from play start; {open.Elapsed.TotalSeconds:0} s from opening): {(bool)Field("_playRoomListsDone")!}; scan {Field("_scanDone")} of {Field("_scanTotal")}; play is in Room {session.CurrentRoomId}");
                await Task.Delay(500);
                var planner = ((Lazy<ScummCutscenePlanner>)Field("_playPlanner")!).Value;
                report.Add($"planner: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes, " +
                           $"{planner.Groups.Count(g => planner.TryGetRun(g.First, out var r) && r.Begun)} begun; same planner as at opening {ReferenceEquals(firstLazy, Field("_playPlanner"))}; " +
                           $"scan total {Field("_scanTotal")}, runs cached {planner.ExportRoomLists().Count}");

                var roomItems = (System.Collections.IEnumerable)Call("RoomCutsceneItems", 33)!;
                var titles = roomItems.Cast<object>().Select(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)!).ToList();
                report.Add($"Room 33 list ({titles.Count}): {string.Join(" | ", titles)}");
                var poster = roomItems.Cast<object>().First(i => ((string)i.GetType().GetProperty("Title")!.GetValue(i)!).Contains("poster", StringComparison.Ordinal));
                var begun = session.CutscenesBegun;
                ((Action)poster.GetType().GetProperty("Play")!.GetValue(poster)!).Invoke();
                for (var n = 0; n < 50 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                report.Add($"played \"{poster.GetType().GetProperty("Title")!.GetValue(poster)}\": room {session.CurrentRoomId}, cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}, " +
                           $"on screen \"{Lines(session)}\", Skip enabled {((Button)window.FindName("SkipCutsceneButton")).IsEnabled}, status \"{Status()}\"");
                Save("cutscene-02-poster.png");
                for (var n = 0; n < 300 && session.InCutscene; n++) await Task.Delay(100);
                report.Add($"poster scene over: in cutscene {session.InCutscene}, room {session.CurrentRoomId}");

                var picker = (Window)typeof(MainWindow).GetMethod("BuildCutscenePicker", Private)!.Invoke(window, null)!;
                picker.Close();
                var all = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("PlayableCutsceneItems", Private)!.Invoke(window, [null])!).Cast<object>()
                    .Select(t => t.GetType().GetField("Item2")!.GetValue(t)!).ToList();
                report.Add($"picker entries (Normal mode): {all.Count}; first five: {string.Join(" | ", all.Take(5).Select(i => i.GetType().GetProperty("Title")!.GetValue(i)))}");
                var logFrom = player.Log.Count;
                var ship = all.First(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)! == "Room 19 · Entry");
                begun = session.CutscenesBegun;
                ((Action)ship.GetType().GetProperty("Play")!.GetValue(ship)!).Invoke();
                for (var n = 0; n < 200 && session.CurrentRoomId != 19; n++) await Task.Delay(100);
                for (var n = 0; n < 100 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                report.Add($"played \"Room 19 · Entry\" from the picker: room {session.CurrentRoomId}, cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}; " +
                           $"on screen \"{Lines(session)}\"; active sounds [{string.Join(",", session.Snapshot.ActiveSounds)}]; status \"{Status()}\"");
                Save("cutscene-03-room-19-entry.png");
                await Task.Delay(3000);
                Save("cutscene-04-room-19-later.png");
                report.Add($"-- scene sound log since the ship's entry (device {SceneSoundPlayer.DeviceName(player.Mode)})");
                foreach (var entry in player.Log.Skip(logFrom)) report.Add($"   frame {entry.Frame} {entry.Milliseconds} ms {entry.Action} {entry.SoundId} {entry.Kind} | {entry.Source}");
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

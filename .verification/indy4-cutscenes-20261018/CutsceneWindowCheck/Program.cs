using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: Indy4CutsceneWindowCheck <Indy 4 CD folder> <output folder>
// S7 Indy4 (18 October 2026), from the MonkeyCd check: opens the real MainWindow on the talkie CD (disc image) with a private profile
// (SCUMM_EXPLORER_PROFILE_ROOT) and a private room-list cache (SCUMM_ROOM_CACHE_DIR), both in the output folder; opens Room 4 (where the play boot ends,
// the playable credits) so play starts; times the background list; then plays Sophia's lecture (Room 11's entry) and the diving suit (Room 82's entry)
// from the global picker, and writes the speech player's log (the scene's spoken lines from MONSTER.SOU) and the scene-sound log (its iMUSE music).
// Saves captures and cutscene-check.txt.
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
        string Lines(ScummGameSession s) => string.Join(" / ", s.Messages.OnScreen.Select(m => m.Text.Replace("\\x10", " ").Replace("\x10", " ")));
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
            var speech = (TalkieSpeechPlayer)Field("_playSpeech")!;
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
                var bar = (FrameworkElement)window.FindName("PlaybackBar");
                var roomButton = (Button)window.FindName("RoomCutsceneButton");
                var note = (TextBlock)window.FindName("CutsceneScanNote");
                report.Add($"Play started in Room {session.CurrentRoomId} {open.Elapsed.TotalSeconds:0.0} s after opening; playback bar {bar.Visibility}; room button {roomButton.Visibility}; scan note {note.Visibility}: \"{note.Text.Replace('\n', ' ')}\"");
                Save("cutscene-01-scan-running.png");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var n = 0; n < 6000 && !(bool)Field("_playRoomListsDone")!; n++)
                {
                    await Task.Delay(100);
                    if (n % 100 == 0) report.Add($"   t={n / 10} s: scan {Field("_scanDone")} of {Field("_scanTotal")}, play room {session.CurrentRoomId}, frame {Field("_playFrame")}");
                }
                report.Add($"background runs done after {clock.Elapsed.TotalSeconds:0} s (from play start; {open.Elapsed.TotalSeconds:0} s from opening): {(bool)Field("_playRoomListsDone")!}; scan {Field("_scanDone")} of {Field("_scanTotal")}; play is in Room {session.CurrentRoomId}");
                await Task.Delay(500);
                var planner = ((Lazy<ScummCutscenePlanner>)Field("_playPlanner")!).Value;
                report.Add($"planner: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes, " +
                           $"{planner.Groups.Count(g => planner.TryGetRun(g.First, out var r) && r.Begun)} begun; verb source: {planner.VerbSource}");
                Save("cutscene-02-list-done.png");

                var picker = (Window)typeof(MainWindow).GetMethod("BuildCutscenePicker", Private)!.Invoke(window, null)!;
                picker.Close();
                var all = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("PlayableCutsceneItems", Private)!.Invoke(window, [null])!).Cast<object>()
                    .Select(t => t.GetType().GetField("Item2")!.GetValue(t)!).ToList();
                report.Add($"picker entries (Normal mode): {all.Count}; first five: {string.Join(" | ", all.Take(5).Select(i => i.GetType().GetProperty("Title")!.GetValue(i)))}");

                foreach (var (title, room, capture) in new[] { ("Room 11 · Entry", 11, "cutscene-03-room-11-lecture.png"), ("Room 82 · Entry", 82, "cutscene-04-room-82-diving.png") })
                {
                    var logFrom = player.Log.Count;
                    var speechFrom = speech.Log.Count;
                    var item = all.First(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)! == title);
                    var begun = session.CutscenesBegun;
                    ((Action)item.GetType().GetProperty("Play")!.GetValue(item)!).Invoke();
                    for (var n = 0; n < 200 && session.CurrentRoomId != room; n++) await Task.Delay(100);
                    for (var n = 0; n < 150 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                    report.Add($"played \"{title}\" from the picker: room {session.CurrentRoomId}, cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}; " +
                               $"on screen \"{Lines(session)}\"; active sounds [{string.Join(",", session.Snapshot.ActiveSounds)}]; Skip enabled {((Button)window.FindName("SkipCutsceneButton")).IsEnabled}; status \"{Status()}\"");
                    Save(capture);
                    await Task.Delay(6000);
                    report.Add($"   6 s later: room {session.CurrentRoomId}, in cutscene {session.InCutscene}, on screen \"{Lines(session)}\"");
                    report.Add($"-- speech player since \"{title}\"");
                    foreach (var line in speech.Log.Skip(speechFrom)) report.Add("   " + line);
                    report.Add($"-- scene sound log since \"{title}\" (device {SceneSoundPlayer.DeviceName(player.Mode)})");
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

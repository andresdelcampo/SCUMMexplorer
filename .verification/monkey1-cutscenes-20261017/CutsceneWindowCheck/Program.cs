using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: MonkeyCutsceneWindowCheck <Monkey Island floppy folder> <output folder>
// S7 MonkeyFloppy (17 October 2026), after the Indy3 check: opens the real MainWindow with a private profile (SCUMM_EXPLORER_PROFILE_ROOT) and a private
// room-list cache (SCUMM_ROOM_CACHE_DIR), both in the output folder; opens Room 33 so play starts; times the background list (a disk game: each worker
// boots from its own copy of the game); then plays two scenes through the window's own entries: the poster from the room's Play Cutscene list, and
// Room 37's entry from the global picker's list (the entry's own Play action, as the picker's Play button invokes it). Saves captures and cutscene-check.txt.
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
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                await Task.Delay(1500);
                var room33 = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 33 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room33);
                for (var n = 0; n < 300 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 33");
                var bar = (FrameworkElement)window.FindName("PlaybackBar");
                var roomButton = (Button)window.FindName("RoomCutsceneButton");
                var note = (TextBlock)window.FindName("CutsceneScanNote");
                report.Add($"Play started in Room {session.CurrentRoomId}; playback bar {bar.Visibility}; room button {roomButton.Visibility}, enabled {roomButton.IsEnabled}; scan note {note.Visibility}: \"{note.Text.Replace('\n', ' ')}\"");
                Save("cutscene-01-scan-running.png");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var n = 0; n < 3000 && !(bool)Field("_playRoomListsDone")!; n++) await Task.Delay(100);
                report.Add($"background runs done after {clock.Elapsed.TotalSeconds:0} s: {(bool)Field("_playRoomListsDone")!}; scan {Field("_scanDone")} of {Field("_scanTotal")}");
                await Task.Delay(500);
                var planner = ((Lazy<ScummCutscenePlanner>)Field("_playPlanner")!).Value;
                report.Add($"planner: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes, " +
                           $"{planner.Groups.Count(g => planner.TryGetRun(g.First, out var r) && r.Begun)} begun; rooms in the game {((SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!).Rooms.Count()}");

                // The room's list (R-8.2), as RoomCutscene_Click builds it.
                var roomItems = (System.Collections.IEnumerable)Call("RoomCutsceneItems", 33)!;
                var titles = roomItems.Cast<object>().Select(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)!).ToList();
                report.Add($"Room 33 list ({titles.Count}): {string.Join(" | ", titles)}; button {roomButton.Visibility}, enabled {roomButton.IsEnabled}, caption {roomButton.Content}");
                Save("cutscene-02-room-33-button.png");
                var poster = roomItems.Cast<object>().First(i => ((string)i.GetType().GetProperty("Title")!.GetValue(i)!).Contains("poster", StringComparison.Ordinal));
                var begun = session.CutscenesBegun;
                ((Action)poster.GetType().GetProperty("Play")!.GetValue(poster)!).Invoke();
                for (var n = 0; n < 50 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                report.Add($"played \"{poster.GetType().GetProperty("Title")!.GetValue(poster)}\": cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}, " +
                           $"on screen \"{Lines(session)}\", Skip enabled {((Button)window.FindName("SkipCutsceneButton")).IsEnabled}, status \"{Status()}\"");
                Save("cutscene-03-poster.png");
                for (var n = 0; n < 300 && session.InCutscene; n++) await Task.Delay(100);
                report.Add($"poster scene over: in cutscene {session.InCutscene}, room {session.CurrentRoomId}");

                // The global picker's list (R-8.1): its entries and their Play actions, as CutsceneMenu_Click shows them.
                var picker = (Window)typeof(MainWindow).GetMethod("BuildCutscenePicker", Private)!.Invoke(window, null)!;
                report.Add($"picker built: {picker.GetType().Name}");
                picker.Close();
                var all = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("PlayableCutsceneItems", Private)!.Invoke(window, [null])!).Cast<object>()
                    .Select(t => t.GetType().GetField("Item2")!.GetValue(t)!).ToList();
                report.Add($"picker entries: {all.Count}; first five: {string.Join(" | ", all.Take(5).Select(i => i.GetType().GetProperty("Title")!.GetValue(i)))}");
                var meathook = all.First(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)! == "Room 37 · Entry");
                begun = session.CutscenesBegun;
                ((Action)meathook.GetType().GetProperty("Play")!.GetValue(meathook)!).Invoke();
                for (var n = 0; n < 200 && session.CurrentRoomId != 37; n++) await Task.Delay(100);
                for (var n = 0; n < 50 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                report.Add($"played \"Room 37 · Entry\" from the picker: room {session.CurrentRoomId}, cutscenes begun {begun} -> {session.CutscenesBegun}, in cutscene {session.InCutscene}; " +
                           $"on screen \"{Lines(session)}\"; status \"{Status()}\"");
                Save("cutscene-04-room-37-entry.png");
                await Task.Delay(2500);
                Save("cutscene-05-room-37-later.png");
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

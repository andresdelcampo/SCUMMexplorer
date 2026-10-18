using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// <fixture> <output> <config.json>. Uses real room/picker actions and timer handler,
// with private profile/cache. This verifies integration, not physical mouse or timing.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(args[2]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethods(Private).Single(m => m.Name == name && m.GetParameters().Length == values.Length).Invoke(window, values);
        void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        void Save(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            void Log(string line) { report.Add(line); File.WriteAllLines(Path.Combine(output, "progress.txt"), report); }
            try
            {
                Call("OpenGameFolder", args[0]); await Task.Delay(1000);
                var tree = (TreeView)window.FindName("ResourceTree");
                var room = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).First(i =>
                    (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == config.BrowseRoom &&
                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", room.Tag);
                for (var n = 0; n < 1200 && Field("_playTimer") is null; n++) await Task.Delay(100);
                Require(Field("_playTimer") is DispatcherTimer, "Play timer missing");
                await Task.Delay(300);
                Require((int)Field("_playFrame")! > 0, "Real timer did not advance");
                ((DispatcherTimer)Field("_playTimer")!).Stop();
                Require(((Image)window.FindName("PlayScreenImage")).ToolTip is null && ((TextBlock)window.FindName("ObjectsNote")).Text == "", "Technical note in Normal mode");
                Save("01-scan-running");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var n = 0; n < 6000 && !(bool)Field("_playRoomListsDone")!; n++) await Task.Delay(100);
                Require((bool)Field("_playRoomListsDone")!, "Background derivation did not finish in ten minutes");
                var planner = ((Lazy<ScummCutscenePlanner>)Field("_playPlanner")!).Value;
                Log($"Census: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes, {planner.Groups.Count(g => planner.TryGetRun(g.First, out var r) && r.Begun)} begun; background {clock.Elapsed.TotalSeconds:0.0}s");
                Save("02-scan-done");
                async Task Frame()
                {
                    var before = (int)Field("_playFrame")!;
                    for (var n = 0; n < 2000 && (int)Field("_playFrame")! == before; n++)
                    { Call("PlayTimer_Tick", null, EventArgs.Empty); if ((int)Field("_playFrame")! == before) await Task.Delay(10); }
                    Require((int)Field("_playFrame")! > before, "Frame stalled");
                    await Task.Delay(1);
                }
                var index = 0;
                foreach (var scene in config.Scenes)
                {
                    var group = planner.Groups.Single(g => g.First.Room == scene.Room && g.First.ObjectId == scene.Object && g.First.VerbId == scene.Verb);
                    Require(planner.TryGetRun(group.First, out var run) && run.Begun, "Selected scene did not begin in fresh run");
                    var id = $"scumm-{scene.Room}-{scene.Object}-{scene.Verb}";
                    var tuples = ((IEnumerable)Call("PlayableCutsceneItems", (object?)null)!).Cast<object>();
                    var tuple = tuples.Single(t => (string)t.GetType().GetField("Item1")!.GetValue(t)! == id);
                    var item = tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                    var title = (string)item.GetType().GetProperty("Title")!.GetValue(item)!;
                    Require(!title.Contains("verb 0x", StringComparison.Ordinal), "Raw verb suffix in Normal title");
                    if (index == 0)
                    {
                        var roomItems = ((IEnumerable)Call("RoomCutsceneItems", scene.Room)!).Cast<object>().ToList();
                        item = roomItems.Single(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)! == title);
                        Log($"Room list: {roomItems.Count} entries; invoking {title}");
                    }
                    else
                    {
                        var picker = (Window)Call("BuildCutscenePicker")!; picker.Show(); await Task.Delay(50); Save("03-picker"); picker.Close();
                        Log($"Picker action: {title}");
                    }
                    var session = (ScummGameSession)Field("_play")!;
                    var traceStart = session.SessionTrace.Count; var begun = session.CutscenesBegun;
                    ((Action)item.GetType().GetProperty("Play")!.GetValue(item)!).Invoke();
                    for (var n = 0; n < 1200 && !session.SessionTrace.Skip(traceStart).Any(t => t.Contains(scene.Trace, StringComparison.Ordinal)); n++) await Frame();
                    Require(session.CutscenesBegun > begun && session.SessionTrace.Skip(traceStart).Any(t => t.Contains(scene.Trace, StringComparison.Ordinal)), "Selected scene's own cutscene did not begin");
                    for (var n = 0; n < 30 && !session.Messages.OnScreen.Any(); n++) await Frame();
                    Call("ShowPlayFrame"); Call("UpdatePlaybackControls"); await Task.Delay(30);
                    Save($"04-scene-{index + 1}");
                    Log($"Begun {title}: room={session.CurrentRoomId}, frame={Field("_playFrame")}, message={string.Join(" / ", session.Messages.OnScreen.Select(m => m.Text))}");
                    for (var n = 0; n < 6000 && session.InCutscene; n++)
                    {
                        if (session.CutsceneSkippable) Call("SkipCutscene_Click", window, new RoutedEventArgs(Button.ClickEvent));
                        await Frame();
                    }
                    Require(!session.InCutscene, "Scene did not finish or skip within bound");
                    File.WriteAllLines(Path.Combine(output, $"trace-{++index}.txt"), session.SessionTrace);
                }
                Log("PASS");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "cutscene-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
    private sealed record Config(int BrowseRoom, Scene[] Scenes);
    private sealed record Scene(int Room, int Object, int Verb, string Trace);
}

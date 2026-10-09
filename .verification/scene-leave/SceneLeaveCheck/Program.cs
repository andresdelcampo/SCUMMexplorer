using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;

// Leaving a conversation from the catalog, in the real MainWindow: the trial (script 046) is played from the cutscene
// menu's entry on the playback clock; after a few seconds, Room 02 is picked in the resource tree while the scene plays.
// Checks: the scene is over in Room 02 (no cutscene open, the playback controls say so), a click is sent to the game's
// input script instead of being refused, and Bobbin walks. The click is assumed input given to HandleRoomPoint (no
// physical input). Arguments: <Games\loom> <output folder>.
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
        object? Property(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        int? CurrentRoom() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; the trial played from its known-cutscene entry, Room 02 picked in the tree mid-scene, then a click given to HandleRoomPoint (assumed input). No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                ((CheckBox)window.FindName("SessionModeToggle")).IsChecked = true;
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                for (var n = 0; n < 100 && Field("_currentRoomResource") is null; n++) await Task.Delay(100);

                var known = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var trial = known.Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 46);
                Call("PlayKnownCutscene", trial);
                await Task.Delay(8000);
                report.Add($"After 8 s of the trial: room {CurrentRoom()}, in cutscene {Session().InCutscene}, playback {Property("PlaybackRunning")}, talk \"{Session().Messages.Talk?.Text}\"");
                Check(Session().InCutscene, "the trial's cutscene is playing when Room 02 is picked");

                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 2 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 100 && CurrentRoom() != 2; n++) await Task.Delay(100);
                await Task.Delay(1500);
                var session = Session();
                report.Add($"After the pick: room {CurrentRoom()} (session {session.Snapshot.RoomId}), in cutscene {session.InCutscene}, in scene {Property("SessionInScene")}, playback {Property("PlaybackRunning")}");
                foreach (var line in session.SessionTrace.Where(line => line.StartsWith("catalog visit", StringComparison.Ordinal))) report.Add("   trace: " + line);
                Check(CurrentRoom() == 2 && session.Snapshot.RoomId == 2, "Room 02 is open in the view and in the session");
                Check(!session.InCutscene && !(bool)Property("SessionInScene")!, "no cutscene is open in Room 02");

                var ego = session.Actors[0];
                report.Add($"Ego before the click: ({ego.X}, {ego.Y}) in Room {ego.Room}");
                var target = (X: Math.Max(8, (ego.X ?? 480) - 80), Y: ego.Y ?? 110);
                Call("HandleRoomPoint", target.X, target.Y, false);
                var action = session.LastAction!;
                foreach (var line in action.Uncertainties.Take(4)) report.Add("   click: " + line);
                Check(!action.Uncertainties.Any(line => line.Contains("not sent", StringComparison.Ordinal)),
                    $"the click at ({target.X}, {target.Y}) is sent to the game's input script");
                for (var n = 0; n < 80 && (bool)Property("PlaybackRunning")!; n++) await Task.Delay(100);
                var after = Session().Actors[0];
                report.Add($"Ego after the click: ({after.X}, {after.Y}); status \"{((TextBlock)window.FindName("StatusText")).Text}\"");
                Check(after.X != ego.X || after.Y != ego.Y, "Bobbin walks after the click");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "scene-leave-check.txt"), report, System.Text.Encoding.UTF8);
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

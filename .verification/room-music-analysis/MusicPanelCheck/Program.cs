using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;

// Measures (does not judge) the two music paths in the real MainWindow, for the room media player analysis (item I):
// 1. a catalog visit to Room 16 (its entry starts sound 30): what the MUSIC panel shows, and when the scene sound
//    player starts sound 30 (on the visit, or only with the next click's playback);
// 2. Room 08: the panel's cue played, then the trial (script 046) played from its menu entry: whether the panel's
//    player keeps playing while the cutscene's own music starts.
// Clicks are assumed input given to HandleRoomPoint; no physical input. Arguments: <Games\loom> <output folder>.
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
        string Text(string name) => ((TextBlock)window.FindName(name)).Text;
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow, interactive mode. Measurements for the room media player analysis; no physical input." };
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                ((CheckBox)window.FindName("SessionModeToggle")).IsChecked = true;
                for (var n = 0; n < 100 && Field("_currentRoomResource") is null; n++) await Task.Delay(100);
                var player = (SceneSoundPlayer)Field("_sceneSound")!;
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                async Task Pick(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && CurrentRoom() != room; n++) await Task.Delay(100);
                    await Task.Delay(2500);
                }
                void Log(string step, int from)
                {
                    report.Add($"{step}: scene sound player log since the step:");
                    foreach (var entry in player.Log.Skip(from)) report.Add($"      {entry}");
                    if (player.Log.Count == from) report.Add("      (nothing)");
                    report.Add($"   MUSIC panel: \"{Text("RoomCueTitle")}\" / \"{Text("RoomCueEvidence")}\" / status \"{Text("RoomCueStatus")}\" / note \"{Text("RoomMusicNote")}\"; panel visible {((FrameworkElement)window.FindName("RoomMusicPanel")).Visibility}");
                }

                var from = player.Log.Count;
                await Pick(16);
                var reached = Session().SoundEvents.Where(e => e.RoomId == 16).Select(e => $"{e.Event.Kind} {e.Event.TargetId} ({e.Event.Certainty})");
                report.Add($"Room 16 catalog visit; session sound events in Room 16: {string.Join(", ", reached)}");
                Log("After the catalog visit to Room 16 (2.5 s)", from);

                from = player.Log.Count;
                var ego = Session().Actors[0];
                Call("HandleRoomPoint", Math.Max(8, (ego.X ?? 160) - 40), ego.Y ?? 110, false);
                for (var n = 0; n < 80 && (bool)Property("PlaybackRunning")!; n++) await Task.Delay(100);
                await Task.Delay(1000);
                Log($"After a click 40 px left of Bobbin ({ego.X}, {ego.Y}) in Room 16", from);

                var failures = 0;
                void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
                var panel = (FrameworkElement)window.FindName("RoomMusicPanel");
                var toggle = (CheckBox)window.FindName("SessionModeToggle");
                var audio = (System.Windows.Media.MediaPlayer)Field("_audioPlayer")!;
                string Surface() => Field("_audioSurface")!.ToString()!;
                Check(panel.Visibility != Visibility.Visible, "after the change: the MUSIC panel is hidden in the interactive mode (Room 16)");

                from = player.Log.Count;
                await Pick(8);
                toggle.IsChecked = false;
                await Task.Delay(2500);
                Check(panel.Visibility == Visibility.Visible, "the MUSIC panel is shown in the inspection mode (Room 08)");
                await (Task)Call("PlayRoomCueAsync")!;
                await Task.Delay(3000);
                Log("Room 08, inspection mode, the MUSIC panel's Play (3 s)", from);
                report.Add($"   panel player: surface {Surface()}, source {(audio.Source is null ? "none" : "set")}, position {audio.Position:mm\\:ss}");
                toggle.IsChecked = true;
                await Task.Delay(2500);
                report.Add($"After turning the interactive mode on: surface {Surface()}, source {(audio.Source is null ? "none" : "set")}, panel {panel.Visibility}");
                Check(Surface() == "None" && audio.Source is null && panel.Visibility != Visibility.Visible,
                    "turning the interactive mode on hides the panel and stops its music");

                // Another player running when the game starts a sound (the panel's cue started through reflection).
                await (Task)Call("PlayRoomCueAsync")!;
                await Task.Delay(3000);
                report.Add($"The app's other player started in the interactive mode: surface {Surface()}, source {(audio.Source is null ? "none" : "set")}, position {audio.Position:mm\\:ss}");
                from = player.Log.Count;
                var known = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                Call("PlayKnownCutscene", known.Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 46));
                await Task.Delay(25000);
                report.Add($"The other player after 25 s of the trial: surface {Surface()}, source {(audio.Source is null ? "none" : "set")}");
                Log("The trial, 25 s", from);
                Check(player.Log.Skip(from).Any(entry => entry is { Action: "start", SoundId: 31 }) && audio.Source is null && Surface() == "None",
                    "the trial's own sound 31 plays and the other player was stopped (no double music)");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "music-panel-check.txt"), report, System.Text.Encoding.UTF8);
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

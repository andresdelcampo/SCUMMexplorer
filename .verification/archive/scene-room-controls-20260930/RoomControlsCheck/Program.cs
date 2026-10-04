using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app = new Application(); var window = new MainWindow();
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] values) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, values);
        T Find<T>(string n) => (T)window.FindName(n);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        var report = new List<string>(); int failures = 0;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        window.ContentRendered += async (_, _) =>
        {
            async Task Wait(Func<bool> condition, int limit = 200)
            {
                for (int n = 0; n < limit && !condition(); n++) await Task.Delay(50);
                if (!condition()) throw new TimeoutException("Expected state did not arrive");
            }
            async Task Pick(int id)
            {
                var item = Find<TreeView>("ResourceTree").Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).Single(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == id);
                Call("SelectResourceInTree", item.Tag);
                await Wait(() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) == id);
            }
            try
            {
                Call("OpenGameFolder", args[0]); await Wait(() => Find<TreeView>("ResourceTree").Items.Count > 0);
                await Pick(16); await Task.Delay(200);
                Check(Field("_playback") is not null && !Find<Button>("PlaybackPauseButton").IsEnabled, "arrival background clock does not enable Pause");
                Call("PauseScenePlayback");
                Check(!(bool)Field("_playbackPaused")!, "idle arrival cannot be paused by keyboard/handler either");
                await Pick(8); await Task.Delay(200);
                Check(Find<Button>("CutsceneMenuButton").IsEnabled, "idle arrival does not block starting a cutscene");
                var known = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var trial = known.Cast<object>().Single(s => (int)s.GetType().GetProperty("Script")!.GetValue(s)! == 46);
                var sound = Find<ComboBox>("SoundModeCombo"); var player = (SceneSoundPlayer)Field("_sceneSound")!;
                foreach (bool paused in new[] { false, true })
                {
                    // Allow the baseline's arrival polling to finish before starting its trial too.
                    await Task.Delay(3500);
                    Call("PlayKnownCutscene", trial); await Wait(() => Session().InCutscene); await Task.Delay(300);
                    if (paused) Call("PauseScenePlayback");
                    Check(Find<Button>("PlaybackPauseButton").IsEnabled && !sound.IsEnabled, $"trial {(paused ? "paused" : "playing")}: Pause/Play usable, sound configuration locked");
                    var mode = player.Mode; sound.SelectedIndex = 0;
                    Check(player.Mode == mode && sound.SelectedIndex == (int)mode, "disabled sound selection cannot bypass scene guard");
                    await Pick(2); await Task.Delay(200);
                    Check(!Session().InCutscene && !Session().BootRunning && !(bool)Field("_playbackPaused")!, "room change ends scene and clears paused state");
                    Check(!Find<Button>("PlaybackPauseButton").IsEnabled && !Find<Button>("SkipCutsceneButton").IsEnabled && Find<Button>("CutsceneMenuButton").IsEnabled && sound.IsEnabled, "new room restores idle buttons and sound configuration");
                    Check(player.Log.Any(e => e.Action == "stop all" && e.Source.Contains("left from the catalog")), "departed cutscene voices stopped");
                    await Task.Delay(3500);
                    Check(Session().Snapshot.RoomId == 2 && !Session().InCutscene, "old scene does not resume during arrival frames");
                }
                Call("PlayOpening"); await Wait(() => Session().BootRunning);
                Call("PauseScenePlayback");
                Check(!sound.IsEnabled, "opening locks sound configuration while paused");
                await Pick(16); await Task.Delay(200);
                Check(!Session().BootRunning && !Session().InCutscene && sound.IsEnabled && !Find<Button>("PlaybackPauseButton").IsEnabled, "leaving opening cancels boot playback and restores idle controls");
                await Task.Delay(3500);
                Check(Session().Snapshot.RoomId == 16 && !Session().BootRunning, "opening cannot restart or load another room after departure");
                sound.SelectedIndex = 1;
                Check(player.Mode == SceneSoundPlayer.SoundMode.PcSpeaker, "sound configuration works again after departure");
                report.Add("Real WPF controls, source sessions and real arrival clock; synthetic selection/calls, no physical input.");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1], "room-controls-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

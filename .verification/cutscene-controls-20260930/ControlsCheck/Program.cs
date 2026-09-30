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
        var app = new Application();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        T Find<T>(string name) => (T)window.FindName(name);
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; synthetic button events and catalog visits. No physical input." };
            int failures = 0;
            void Check(bool ok, string message) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {message}"); }
            async Task Wait(Func<bool> done, int limit = 100)
            {
                for (int n = 0; n < limit && !done(); n++) await Task.Delay(100);
                if (!done()) throw new TimeoutException("Window did not reach expected state");
            }
            try
            {
                Call("OpenGameFolder", args[0]);
                await Wait(() => Find<TreeView>("ResourceTree").Items.Count > 0);
                Find<CheckBox>("SessionModeToggle").IsChecked = true;
                async Task Pick(int room)
                {
                    var item = Find<TreeView>("ResourceTree").Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .Single(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room" &&
                            (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room);
                    Call("SelectResourceInTree", item.Tag);
                    await Wait(() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) == room);
                    Call("UpdatePlaybackControls");
                }
                var button = Find<Button>("CutsceneMenuButton");
                foreach (int room in new[] { 20, 41 })
                {
                    await Pick(room);
                    Check(button.Visibility == Visibility.Collapsed && ((ContextMenu)Call("BuildCutsceneMenu")!).Items.Count == 0,
                        $"Room {room}: no cutscene control or placeholder menu");
                }
                await Pick(11);
                var menu = (ContextMenu)Call("BuildCutsceneMenu")!;
                Check(button.Visibility == Visibility.Visible && button.Content.ToString()!.EndsWith("▾") && menu.Items.Count == 2,
                    "Room 11: dropdown for two scenes");
                Check(menu.Items.OfType<MenuItem>().All(i => !i.Header.ToString()!.Contains("Script", StringComparison.OrdinalIgnoreCase)),
                    "dropdown uses plain scene titles");
                await Pick(8);
                Check(button.Content.ToString() == "Play a cutscene", "Room 08: ordinary button without dropdown marker");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(() => Session().InCutscene && !string.IsNullOrWhiteSpace(Session().Messages.Talk?.Text), 350);
                Check(Session().InCutscene, "single-scene button directly starts the trial");
                var skip = Find<Button>("SkipLineButton");
                Call("UpdatePlaybackControls");
                var line = Session().Messages.Talk;
                Check(skip.IsEnabled && Session().Messages.TalkDelay > 0, $"Skip line enabled for live text: {line?.Text}");
                skip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Session().Messages.TalkDelay == 0, "Skip line sets the same zero talk delay as '.'");
                await Wait(() => !ReferenceEquals(line, Session().Messages.Talk), 80);
                Check(Session().InCutscene, "next line/frame follows without skipping the whole cutscene");
                Call("StopScenePlayback");
                Find<CheckBox>("SessionModeToggle").IsChecked = false;
                Check(Find<FrameworkElement>("PlaybackBar").Visibility == Visibility.Collapsed, "inspection mode hides cutscene controls");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally
            {
                report.Add($"Failures: {failures}");
                Directory.CreateDirectory(args[1]);
                File.WriteAllLines(Path.Combine(args[1], "controls-check.txt"), report);
                window.Close(); app.Shutdown(); Environment.ExitCode = failures == 0 ? 0 : 1;
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
            async Task Pick(int id)
            {
                var item = Find<TreeView>("ResourceTree").Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).Single(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == id);
                Call("SelectResourceInTree", item.Tag);
                for (int n = 0; n < 100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) != id; n++) await Task.Delay(100);
                // Catalog visits now play their arrival frames. Wait for that playback before testing an idle start.
                for (int n = 0; n < 150 && Field("_playback") is not null; n++) await Task.Delay(100);
                Call("UpdatePlaybackControls");
            }
            async Task Drain()
            {
                for (int n = 0; n < 4000 && Field("_playback") is not null; n++)
                {
                    ((DispatcherTimer?)Field("_playbackTimer"))?.Stop();
                    Call("PlaybackTimer_Tick", null, EventArgs.Empty);
                    if (n % 20 == 0 || (bool)Field("_playbackEnteringRoom")!) await Task.Delay(15);
                }
                Call("UpdatePlaybackControls");
            }
            try
            {
                Call("OpenGameFolder", args[0]); await Task.Delay(1000);
                await Pick(11);
                var oldMenu = (ContextMenu)Call("BuildCutsceneMenu")!;
                var oldTrial = oldMenu.Items.OfType<MenuItem>().Single(i => i.Header.ToString()!.Contains("trial"));
                await Pick(8);
                var button = Find<Button>("CutsceneMenuButton");
                Check(button.IsEnabled, "single-scene control enabled while idle");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int n = 0; n < 200 && !Session().InCutscene; n++) await Task.Delay(50);
                for (int n = 0; n < 200 && !Session().CutsceneSkippable; n++)
                {
                    ((DispatcherTimer?)Field("_playbackTimer"))?.Stop();
                    Call("PlaybackTimer_Tick", null, EventArgs.Empty); await Task.Delay(15);
                }
                Call("PauseScenePlayback"); var saved = Session();
                Check(!button.IsEnabled && Find<Button>("PlaybackPauseButton").IsEnabled, "playing/paused scene disables starts and keeps pause usable");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                oldTrial.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Call("PlayOpening");
                Check(ReferenceEquals(saved, Session()), "single, stale dropdown and opening starts cannot replace an active session");
                var blocked = (ContextMenu)Call("BuildCutsceneMenu")!;
                Check(blocked.Items.OfType<MenuItem>().All(i => !i.IsEnabled), "new dropdown items disabled during playback");
                Check(Find<Button>("SkipCutsceneButton").IsEnabled, "whole-scene skip remains enabled at a source skip point");
                Call("SkipCutscene"); await Drain();
                Check(Field("_playback") is null && !Session().InCutscene && button.IsEnabled, "start control restored after skipping and remaining frames");
                await Pick(10); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Drain();
                Check(Field("_playback") is null && !Session().InCutscene && button.IsEnabled, "start control restored after natural scene completion");
                await Pick(11); oldMenu = (ContextMenu)Call("BuildCutsceneMenu")!;
                oldTrial = oldMenu.Items.OfType<MenuItem>().Single(i => i.Header.ToString()!.Contains("trial"));
                oldTrial.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Call("PauseScenePlayback"); saved = Session();
                oldTrial.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(!button.IsEnabled && ReferenceEquals(saved, Session()), "multiple-scene dropdown prevents repeated starts");
                await Pick(10);
                Check(button.IsEnabled, "room change restores idle control");
                Find<CheckBox>("SessionModeToggle").IsChecked = false;
                Check(Find<FrameworkElement>("PlaybackBar").Visibility == Visibility.Collapsed, "inspection hides playback controls");
                Find<CheckBox>("SessionModeToggle").IsChecked = true;
                Check(button.IsEnabled, "interactive return restores idle scene control");
                report.Add("Real WPF window with synthetic handlers/menu actions and accelerated source frames; no physical input. No executor or scene state fabricated.");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1], "busy-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

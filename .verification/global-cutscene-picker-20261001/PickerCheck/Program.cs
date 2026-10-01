using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app = new Application(); var w = new MainWindow();
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(w);
        object? Call(string n, params object?[] v) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(w, v);
        T Find<T>(string n) => (T)w.FindName(n);
        var report = new List<string>(); int failures = 0;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        w.ContentRendered += async (_, _) =>
        {
            async Task Wait(Func<bool> condition) { for (int i=0; i<200 && !condition(); i++) await Task.Delay(50); if (!condition()) throw new TimeoutException(); }
            async Task Pick(int id) { var game = (GameLibrary)Field("_game")!; Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == id)); await Wait(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == id); await Task.Delay(200); }
            try
            {
                Call("OpenGameFolder", args[0]); await Pick(16); w.UpdateLayout();
                Check(Find<Button>("CutsceneMenuButton").IsVisible && Find<Button>("CutsceneMenuButton").IsEnabled, "global Play Cutscene visible and enabled in normal Room 16");
                var menu = (ContextMenu)Call("BuildCutsceneMenu")!;
                Check(menu.Items.Count >= 4, "opening, trial, transition and firewood offered outside their scene rooms");
                Check(!Find<Button>("PlaybackPauseButton").IsEnabled, "idle arrival keeps Pause disabled");
                var bitmap = new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(w);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var f=File.Create(Path.Combine(args[1],"normal.png"))) encoder.Save(f);
                var trial = menu.Items.OfType<MenuItem>().Single(i => i.Header.ToString() == "The Elders' trial");
                trial.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Wait(() => ((LoomExplorationSession)Field("_session")!).InCutscene);
                Check(!Find<Button>("CutsceneMenuButton").IsEnabled && !Find<ComboBox>("SoundModeCombo").IsEnabled, "global replay locks starts and sound during scene");
                Call("PauseScenePlayback"); Check(!Find<ComboBox>("SoundModeCombo").IsEnabled,"pause keeps sound locked");
                await Pick(16);
                Check(!((LoomExplorationSession)Field("_session")!).InCutscene && !Find<Button>("PlaybackPauseButton").IsEnabled && Find<Button>("CutsceneMenuButton").IsEnabled,"room departure restores idle controls");
                if (args.Length > 2)
                {
                    Check(!Find<Button>("GameTextsButton").IsVisible,"Game Texts hidden in normal");
                    var advanced=Find<CheckBox>("AdvancedModeToggle"); advanced.IsChecked=true; advanced.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Check(Find<Button>("GameTextsButton").IsVisible,"Game Texts visible in Advanced");
                    advanced.IsChecked=false; advanced.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Check(!Find<Button>("GameTextsButton").IsVisible && Find<CheckBox>("SessionModeToggle").IsChecked==true,"normal return hides Game Texts and preserves interactive input");
                }
                report.Add("Real WPF layout and routed handlers; synthetic input, no independent listening.");
            }
            catch(Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1],"picker-check.txt"),report); w.Close(); app.Shutdown(); Environment.ExitCode=failures==0?0:1; }
        };
        app.Run(w);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1180, Height = 760, Left = -3000, Top = -3000, ShowActivated = false, ShowInTaskbar = false };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        T Find<T>(string name) => (T)window.FindName(name);
        var report = new List<string>();
        void Check(bool condition, string text) { report.Add($"{(condition ? "PASS" : "FAIL")} {text}"); if (!condition) throw new Exception(text); }
        void Save(string name)
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var bmp = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                var advanced = Find<CheckBox>("AdvancedModeToggle"); advanced.IsChecked = true; Call("AdvancedModeToggle_Click", advanced, new RoutedEventArgs());
                await Task.Delay(1500);
                var tree = Find<TreeView>("ResourceTree");
                var soundGroup = tree.Items.OfType<TreeViewItem>().First(g => g.Items.OfType<TreeViewItem>().Any(i => i.Tag is GameResource { Kind: ResourceKind.Sound }));
                var previousCursor = Mouse.OverrideCursor;
                var previousBusyCount = (int)Field("_busyCount")!;
                soundGroup.IsExpanded = true;
                if (soundGroup.Items.Count >= 1000)
                    Check(Mouse.OverrideCursor == Cursors.Wait && (int)Field("_busyCount")! == previousBusyCount + 1, $"wait cursor active immediately for {soundGroup.Items.Count} sound rows");
                else
                    Check(Mouse.OverrideCursor == previousCursor && (int)Field("_busyCount")! == previousBusyCount, "small sound branch does not add a waiting cursor");
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                Check((int)Field("_busyCount")! == previousBusyCount, "expansion releases its busy scope after layout");
                Check(Mouse.OverrideCursor == previousCursor, "expansion restores cursor after layout");
                var items = soundGroup.Items.OfType<TreeViewItem>().ToArray();
                var selected = items.FirstOrDefault(i => i.Tag is GameResource { ResourceId: GameLibrary.SpeechIdBase + 45 }) ?? items.First(i => i.Tag is GameResource { ResourceId: 28 });
                selected.IsSelected = true; selected.BringIntoView();
                for (int n = 0; n < 100 && !Find<Button>("PlayButton").IsEnabled; n++) await Task.Delay(100);
                await Task.Delay(500);
                ((MediaPlayer)Field("_audioPlayer")!).Volume = 0;
                Check(Find<Button>("PlayButton").IsEnabled, "sound prepared");
                Check(Find<TextBlock>("AudioPlaybackState").Text == "Ready to play", "ready state");
                Check(Find<TextBlock>("PageTitle").Text == "Sounds & music", "page title avoids repeated resource name");
                Check(selected.Header is string, "existing treeview label retained");
                if (((GameResource)selected.Tag).ResourceId >= GameLibrary.SpeechIdBase)
                    Check(Find<TextBlock>("AudioLine").Text.Length > 0, "source speech text appears in player");
                Save("ready");
                Call("PlayButton_Click", null, new RoutedEventArgs()); await Task.Delay(700);
                Check(Find<ProgressBar>("AudioProgress").Value > 0, "real progress advances during playback");
                Check(Find<TextBlock>("AudioPlaybackState").Text == "Playing", "playing state");
                Call("PauseButton_Click", null, new RoutedEventArgs());
                Check(Find<TextBlock>("AudioPlaybackState").Text == "Paused", "paused state"); Save("paused");
                Call("StopButton_Click", null, new RoutedEventArgs());
                Check(Find<ProgressBar>("AudioProgress").Value == 0, "stop resets progress");
                window.Width = 900; window.Height = 600; await Task.Delay(200); Save("minimum");
                Check(Find<Border>("AudioCard").ActualWidth <= Find<ScrollViewer>("AudioPanel").ActualWidth, "card fits minimum width");
                Check(Find<Button>("StopButton").ActualWidth == 80, "minimum-width controls retain size");
                var stop = Find<Button>("StopButton");
                var host = Find<ScrollViewer>("AudioPanel");
                Check(stop.TranslatePoint(new Point(0, stop.ActualHeight), host).Y <= host.ActualHeight, "minimum-height controls remain visible without scrolling");
                Check(host.ScrollableHeight == 0, "ordinary sound card fits minimum window without scrollbar");
                // Selection on a second row retains keyboard-style selection and full-width layout.
                var other = items.First(i => i != selected); other.IsSelected = true;
                Check(other.IsSelected && !selected.IsSelected, "tree selection moves to another sound");
                await Task.Delay(500);
                var resource = (GameResource)selected.Tag;
                if (resource.ResourceId < GameLibrary.SpeechIdBase)
                {
                    var game = (GameLibrary)Field("_game")!;
                    var decoder = new ScummAdlibAudio();
                    var unsupported = game.Resources.First(r => r.Kind == ResourceKind.Sound && decoder.ReadLifetime(r).Kind == ScummSoundLifetimeKind.Unknown && !r.Name.Contains('.'));
                    await (Task)Call("ShowAudio", unsupported, false, false)!;
                    Check(Find<TextBlock>("AudioPlaybackState").Text == "Playback unavailable" && !Find<Button>("PlayButton").IsEnabled, "unsupported audio is clearly labelled and disabled");
                    Save("unavailable");
                    Find<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                    await (Task)Call("ShowAudio", resource, false, false)!;
                    Check(Find<TextBlock>("AudioPlaybackState").Text == "Muted" && Find<ProgressBar>("AudioProgress").Value == 0, "muted device resets player presentation");
                }
            }
            catch (Exception ex) { report.Add("FAILED " + ex); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "validation.txt"), report); app.Shutdown(); }
        };
        app.Run(window);
    }
}

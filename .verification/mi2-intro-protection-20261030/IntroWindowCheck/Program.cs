using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: IntroWindowCheck <game folder> <output folder> [hands-off]
// hands-off (31 October 2026): no key is pressed; Play Game Intro answers the copy protection itself, and only the full-game box is clicked.
// 30 October 2026 (MI2 opening blocked at the copy protection, user report of 28 October): the real MainWindow opens the game, presses the header's
// Play Game Intro, lets the real clock run, then presses Enter as a keyboard event through the window's own handlers. Logs the play state and the
// texts on screen and saves pictures. Private profile and room cache in the output folder.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        void State(string stage)
        {
            var play = (ScummGameSession?)Field("_play");
            Log($"{stage}: PlayActive={Prop("PlayActive")} ambient={Field("_playAmbient")} takesInput={Prop("PlayTakesInput")} " +
                $"room={play?.CurrentRoomId} userInput={play?.UserInputOn} boot={play?.BootRunning} pending=[{string.Join(", ", play?.PendingScriptSummary.Take(5) ?? [])}] " +
                $"texts=[{string.Join(" / ", play?.Messages.OnScreen.SelectMany(m => m.Lines).Where(l => l.Length > 0).Take(3) ?? [])}] " +
                $"preview={((FrameworkElement)window.FindName("PreviewMessage")).Visibility} display={((FrameworkElement)window.FindName("RoomDisplayLayout")).Visibility} " +
                $"focus={Keyboard.FocusedElement?.GetType().Name}");
        }
        async Task Pump(double seconds) { var until = clock.Elapsed.TotalSeconds + seconds; while (clock.Elapsed.TotalSeconds < until) await Task.Delay(20); }
        void Press(Key key)
        {
            var source = PresentationSource.FromVisual(window)!;
            var target = (IInputElement?)Keyboard.FocusedElement ?? window;
            var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            target.RaiseEvent(down);
            if (!down.Handled) target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
            Log($"pressed {key} on {target.GetType().Name}: handled by preview={down.Handled}");
        }
        window.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await Pump(3);
                var intro = (Button)window.FindName("GameIntroButton");
                Log($"opened; intro button visible={intro.Visibility} enabled={intro.IsEnabled} caption={intro.Content}");
                intro.Focus();
                intro.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, intro));
                if (args.Length > 2 && args[2] == "hands-off")
                {
                    for (var i = 1; i <= 12; i++) { await Pump(2); State($"hands off +{i * 2}s"); if (i == 2) Capture("h1-prompt"); if (i == 4) Capture("h2-recipe"); }
                    Capture("h3-choice");
                    var auto = (ScummGameSession)Field("_play")!;
                    Log("trace: " + string.Join(" | ", auto.SessionTrace.Where(line => line.StartsWith("intro:", StringComparison.Ordinal))));
                    var chosen = auto.ClickScreen(24, 64, out var why);
                    Log($"clicked the full-game box: {(chosen is null ? "refused: " + why : "delivered")}");
                    for (var i = 1; i <= 8; i++) { await Pump(3); State($"after the choice +{i * 3}s"); }
                    Capture("h4-after-choice");
                    return;
                }
                for (var i = 1; i <= 4; i++) { await Pump(2); State($"intro +{i * 2}s"); }
                Capture("1-intro-waiting");
                Press(Key.Enter);
                for (var i = 1; i <= 3; i++) { await Pump(2); State($"after Enter +{i * 2}s"); }
                Capture("2-after-enter");
                // The answer the game itself keeps (Script 130 +0x0134 writes var[0x206]); read only, typed as text input through the window's handlers.
                var play = (ScummGameSession)Field("_play")!;
                if (play.Snapshot.KnownGlobalVariables.TryGetValue("var[0x206]", out var answer) && answer != 0)
                {
                    var digits = answer.ToString("0000");
                    foreach (var digit in digits)
                    {
                        var target = (IInputElement?)Keyboard.FocusedElement ?? window;
                        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, target, digit.ToString()));
                        await Pump(0.3);
                    }
                    Log($"typed the game's own answer {digits}");
                    for (var i = 1; i <= 3; i++) { await Pump(2); State($"after the answer +{i * 2}s"); }
                    Capture("3-after-answer");
                    // The "Monkey Island 2" box (object 1156) at game screen (24, 64), delivered as the play image delivers a click.
                    var delivered = play.ClickScreen(24, 64, out var refused);
                    Log($"clicked the full-game box: {(delivered is null ? "refused: " + refused : "delivered")}");
                    for (var i = 1; i <= 10; i++) { await Pump(3); State($"after the choice +{i * 3}s"); }
                    Capture("4-after-choice");
                }
                else Log("no var[0x206] answer (a release without the protection)");
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
    }
}

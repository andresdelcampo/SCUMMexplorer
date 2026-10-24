using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SCUMMexplorer.App;

// Usage: LiveHover <game folder> <output folder> [moves=6000] [advanced]
// The user's 24 Oct steps, live in the real MainWindow placed far off screen (no input device is touched): open the game, Play Game Intro, choose the two
// kids with the game's own clicks, skip the cutscene with the Skip button, expand Room options, Hotspots = On hover, then move the game pointer over the sign
// ("cartel") through the window's own move body (PointAtScreen + UpdatePlayOverlayHover). Logs per 250 moves: mean/max cost of the move body, heartbeat gap,
// managed memory, overlay children. A watchdog ends only this process.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out Pt point);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct Pt { public int X, Y; }

    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var moves = args.Length > 2 ? int.Parse(args[2]) : 6000;
        var advancedRun = args.Length > 3 && args[3] == "advanced";
        var real = args.Length > 3 && args[3] == "real";
        var area = args.Length > 4 ? args[4] : "Hover"; // Never | Hover | Always
        var sweep = args.Length > 5 && args[5] == "stay" ? false : true;
        (int X, int Y) Pos(int i, int top) { if (!sweep) return (10 + (i * 7) % 28, top + 82 + (i * 5) % 20); var speed = 0.03 * (1 + (i / 700) % 6); var x = 4 + (int)(110 * (0.5 + 0.5 * Math.Sin(i * speed))); var y = top + 70 + (int)(40 * (0.5 + 0.5 * Math.Sin(i * speed * 0.37))); return (x, y); }
        var report = new List<string>();
        void Log(string text) { lock (report) { report.Add($"{DateTime.Now:HH:mm:ss.fff} {text}"); File.WriteAllLines(Path.Combine(output, "live.txt"), report); } }
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Left = real ? 0 : -12000, Top = 0, Width = 1500, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        T Control<T>(string name) where T : class => (T)window.FindName(name);
        object? Play() => Field("_play");
        T PlayGet<T>(string name) => (T)Play()!.GetType().GetProperty(name)!.GetValue(Play())!;

        var lastTick = Stopwatch.StartNew();
        long maxGap = 0; var phase = "setup";
        var heartbeat = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(10) };
        heartbeat.Tick += (_, _) => { var gap = lastTick.ElapsedMilliseconds; lastTick.Restart(); if (gap > maxGap) maxGap = gap; };
        heartbeat.Start();
        _ = new System.Threading.Timer(_ =>
        {
            var stalled = lastTick.ElapsedMilliseconds;
            if (stalled < 20_000) return;
            Log($"WATCHDOG: dispatcher blocked {stalled} ms in phase '{phase}'; ending this process");
            Environment.Exit(2);
        }, null, 1000, 1000);
        _ = new System.Threading.Timer(_ => { Log($"WATCHDOG: run exceeded 240 s in phase '{phase}'"); Environment.Exit(3); }, null, 240_000, Timeout.Infinite);

        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(4000);
                phase = "intro";
                Call("GameIntro_Click", Control<Button>("GameIntroButton"), new RoutedEventArgs());
                var wait = Stopwatch.StartNew();
                while (wait.Elapsed.TotalSeconds < 90 && !(Play() is not null && PlayGet<int?>("CurrentRoomId") == 45 && Control<UIElement>("PlayScreenImage").IsVisible)) await Task.Delay(250);
                Log($"intro: play={(Play() is not null)} room={(Play() is null ? null : PlayGet<int?>("CurrentRoomId"))} PlayActive={Prop("PlayActive")}");
                await Task.Delay(1500);
                phase = "kids";
                var top = PlayGet<(int Top, int Bottom)>("MainScreen").Top;
                foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) })
                {
                    var click = Play()!.GetType().GetMethods().First(m => m.Name == "ClickScreen" && m.GetParameters().Length == 3);
                    var parameters = new object?[] { x, y + top, null };
                    var result = click.Invoke(Play(), parameters);
                    Log($"click {x},{y}: {(result is null ? "refused: " + parameters[2] : "delivered")}");
                    await Task.Delay(2500);
                }
                phase = "wait for the cutscene";
                wait.Restart();
                var skip = Control<Button>("SkipCutsceneButton");
                while (wait.Elapsed.TotalSeconds < 90 && !(PlayGet<int?>("CurrentRoomId") == 44 && PlayGet<bool>("CutsceneSkippable"))) await Task.Delay(250);
                Log($"cutscene: room={PlayGet<int?>("CurrentRoomId")} skippable={PlayGet<bool>("CutsceneSkippable")} skipButtonEnabled={skip.IsEnabled}");
                if (skip.IsEnabled) skip.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); else Play()!.GetType().GetMethod("PressKey", new[] { typeof(int) })!.Invoke(Play(), new object[] { 27 });
                phase = "wait for control";
                wait.Restart();
                while (wait.Elapsed.TotalSeconds < 60 && !PlayGet<bool>("PlayerHasControl")) await Task.Delay(250);
                await Task.Delay(1500);
                Log($"control: {PlayGet<bool>("PlayerHasControl")} room={PlayGet<int?>("CurrentRoomId")} PlayTakesInput={Prop("PlayTakesInput")}");

                phase = "options";
                if (advancedRun) { var adv = Control<CheckBox>("AdvancedModeToggle"); adv.IsChecked = true; adv.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(800); }
                Control<Expander>("RoomOptionsExpander").IsExpanded = true;
                await Task.Delay(500);
                Log($"hotspot options visible: {Control<UIElement>("ObjectAreaOptions").IsVisible}");
                var hover = Control<RadioButton>("Objects" + area);
                hover.IsChecked = true;
                await Task.Delay(800);

                var canvas = (Canvas)Field("_playOverlayCanvas")!;
                var objectList = Control<Panel>("ObjectList");
                var movePointer = typeof(MainWindow).GetMethod("UpdatePlayOverlayHover", Private)!;
                var point = Play()!.GetType().GetMethod("PointAtScreen")!;
                if (real)
                {
                    // ONE short run with the real pointer (24 Oct brief: under 30 s), then the pointer goes back where it was.
                    phase = "real pointer";
                    GetCursorPos(out var home);
                    var image = Control<FrameworkElement>("PlayScreenImage");
                    var scale = Math.Min(image.ActualWidth / 320, image.ActualHeight / 200);
                    var ox = (image.ActualWidth - 320 * scale) / 2; var oy = (image.ActualHeight - 200 * scale) / 2;
                    var clock = Stopwatch.StartNew(); long lastSecond = 0; var steps = 0; double worstStep = 0; var stepClock = new Stopwatch();
                    maxGap = 0;
                    while (clock.Elapsed.TotalSeconds < 20)
                    {
                        var i = steps++;
                        var (gx, gy) = Pos(i, top);
                        var screen = image.PointToScreen(new Point(ox + (gx + 0.5) * scale, oy + (gy + 0.5) * scale));
                        SetCursorPos((int)screen.X, (int)screen.Y);
                        stepClock.Restart();
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        worstStep = Math.Max(worstStep, stepClock.Elapsed.TotalMilliseconds);
                        await Task.Delay(6);
                        if (clock.ElapsedMilliseconds / 1000 != lastSecond)
                        {
                            lastSecond = clock.ElapsedMilliseconds / 1000;
                            Log($"real t={lastSecond}s steps {steps}: heartbeat max gap {maxGap} ms, worst input turnaround {worstStep:F0} ms, overlay children {canvas.Children.Count}, mem {GC.GetTotalMemory(false) / 1048576} MB, highlight={(Field("_playOverlayPointer") is not null)}");
                            maxGap = 0; worstStep = 0;
                        }
                    }
                    SetCursorPos(home.X, home.Y);
                    Log("DONE (real)");
                    return;
                }
                phase = "hover storm";
                var rate = Stopwatch.StartNew();
                double sumMs = 0, worst = 0; long worstGap = 0; int n = 0;
                maxGap = 0;
                for (var i = 0; i < moves; i++)
                {
                    // The sign is the stored object at room 8,80 (32 x 24); the room screen starts at row 16. The pointer wiggles inside it.
                    (int, int)? p = Pos(i, top);
                    var t = Stopwatch.GetTimestamp();
                    point.Invoke(Play(), new object?[] { p });
                    movePointer.Invoke(window, new object?[] { p });
                    var ms = (Stopwatch.GetTimestamp() - t) * 1000.0 / Stopwatch.Frequency;
                    sumMs += ms; worst = Math.Max(worst, ms); n++;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                    await Task.Delay(8);
                    if ((i + 1) % 250 == 0)
                    {
                        Log($"moves {i + 1}: body mean {sumMs / n:F3} ms worst {worst:F2} ms; heartbeat max gap {maxGap} ms; mem {GC.GetTotalMemory(false) / 1048576} MB, ws {Process.GetCurrentProcess().WorkingSet64 / 1048576} MB; " +
                            $"overlay children {canvas.Children.Count}, ObjectList {objectList.Children.Count}, trace {PlayGet<IReadOnlyList<string>>("SessionTrace").Count}, elapsed {rate.Elapsed.TotalSeconds:F1} s");
                        sumMs = 0; worst = 0; n = 0; maxGap = 0;
                    }
                }
                Log("DONE");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { Call("StopPlay"); } catch { }
                Environment.Exit(Environment.ExitCode);
            }
        };
        app.Run(window);
    }
}

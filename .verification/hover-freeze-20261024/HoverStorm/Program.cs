using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;

// Usage: HoverStorm <game folder> <output folder> <room> [seconds-per-pass=6] [only: normal|advanced]
// Hover responsiveness (24 October 2026, R-16.10). The real MainWindow (private profile and room cache) opens a game, selects the room as a user does, waits
// for what the window shows (live play screen, static preview or browsed picture), keeps every clock RUNNING (as for a user), then moves the real cursor
// over the displayed room in sweeps (Win32 SetCursorPos, so WPF gets genuine WM_MOUSEMOVE) for each combination of Hotspots Never/On hover/Always,
// Doorways Never/On hover/Always and Normal/Advanced. A DispatcherTimer heartbeat (10 ms) records the largest gap between ticks and the turnaround of the
// input queue per step; the report line per pass is the evidence. A watchdog thread ends the run and writes what it has.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var roomId = int.Parse(args[2]);
        var passMs = (args.Length > 3 ? int.Parse(args[3]) : 6) * 1000;
        var only = args.Length > 4 ? args[4] : "";
        var report = new List<string>();
        void Flush() { lock (report) File.WriteAllLines(Path.Combine(output, "storm.txt"), report); }
        var game = new GameFolderScanner().Scan(args[0]);
        report.Add($"{game.Id} {args[0]} room {roomId}");
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Left = 0, Top = 0, Width = 1500, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        T Control<T>(string name) where T : class => (T)window.FindName(name);

        var lastTick = Stopwatch.StartNew();
        long maxGapMs = 0; long ticks = 0; var phase = "setup";
        var heartbeat = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(10) };
        heartbeat.Tick += (_, _) => { var gap = lastTick.ElapsedMilliseconds; lastTick.Restart(); ticks++; if (gap > maxGapMs) maxGapMs = gap; };
        heartbeat.Start();
        var watchdog = new System.Threading.Timer(_ =>
        {
            var stalled = lastTick.ElapsedMilliseconds;
            if (stalled < 20_000) return;
            lock (report) report.Add($"WATCHDOG: dispatcher blocked {stalled} ms in phase '{phase}'; stopped");
            Flush(); Environment.Exit(2);
        }, null, 1000, 1000);
        var total = new System.Threading.Timer(_ => { lock (report) report.Add($"WATCHDOG: whole run exceeded 400 s in phase '{phase}'"); Flush(); Environment.Exit(3); }, null, 400_000, Timeout.Infinite);

        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var watch = Stopwatch.StartNew();
                Call("OpenGameFolder", args[0]);
                var tree = Control<TreeView>("ResourceTree");
                var room = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == roomId &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                phase = "open room";
                Call("SelectResourceInTree", room);
                while (Field("_currentRoom") is null && watch.Elapsed.TotalSeconds < 90) await Task.Delay(100);
                await Task.Delay(5000);
                if (args.Length > 5 && args[5] == "intro")
                {
                    // As the user did: Play Game Intro, let it run, then open the room again.
                    phase = "game intro";
                    Call("GameIntro_Click", Control<Button>("GameIntroButton"), new RoutedEventArgs());
                    // The game itself walks on to the wanted room (Maniac Mansion's own opening ends at the entrance); the player then has control.
                    var wait = Stopwatch.StartNew();
                    while (wait.Elapsed.TotalSeconds < 60)
                    {
                        await Task.Delay(1000);
                        var now = (Field("_currentRoomResource") as GameResource)?.ResourceId;
                        var control = Field("_play") is { } p && (bool)(p.GetType().GetProperty("PlayerHasControl")?.GetValue(p) ?? false);
                        if (now == roomId && control) break;
                    }
                    await Task.Delay(1500);
                    report.Add($"after the intro ({wait.Elapsed.TotalSeconds:0} s): PlayActive={Prop("PlayActive")} room={(Field("_currentRoomResource") as GameResource)?.ResourceId}");
                }
                var playActive = (bool)(Prop("PlayActive") ?? false);
                var ambient = (bool)(Field("_playAmbient") ?? false);
                var older = (System.Collections.ICollection?)Field("_olderObjects");
                report.Add($"opened after {watch.Elapsed.TotalSeconds:0.0} s: PlayActive={playActive} ambient={ambient} session={(Field("_session") is not null)} olderObjects={older?.Count}");
                Control<Expander>("RoomOptionsExpander").IsExpanded = true;
                await Task.Delay(300);
                var surfaceName = Control<FrameworkElement>("PlayScreenImage").IsVisible ? "PlayScreenImage" : "RoomPreview";
                report.Add("pointer surface: " + surfaceName);
                report.Add($"options visible: hotspots={Control<UIElement>("ObjectAreaOptions").IsVisible} doorways={Control<UIElement>("DoorwayOptions").IsVisible}");
                {
                    var adv0 = Control<CheckBox>("AdvancedModeToggle"); adv0.IsChecked = true; adv0.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    await Task.Delay(1200);
                    report.Add($"DOORWAYS rows in Advanced: {Control<Panel>("RoomNavigationButtons").Children.Count}; panel visible={Control<UIElement>("RoomNavigationPanel").IsVisible}; header={Control<TextBlock>("RoomNavigationHeader").Text}");
                    adv0.IsChecked = false; adv0.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    await Task.Delay(800);
                }
                var advanced = Control<CheckBox>("AdvancedModeToggle");

                var targets = new List<(double X, double Y, double Width, double Height)>();
                (double W, double H) roomSize = (0, 0);
                if (Field("_currentRoom") is { } shown)
                {
                    roomSize = (Convert.ToDouble(shown.GetType().GetProperty("Width")!.GetValue(shown)), Convert.ToDouble(shown.GetType().GetProperty("Height")!.GetValue(shown)));
                    if (older is not null) foreach (var o in older) { var t = o.GetType(); targets.Add((Convert.ToDouble(t.GetProperty("X")!.GetValue(o)), Convert.ToDouble(t.GetProperty("Y")!.GetValue(o)), Convert.ToDouble(t.GetProperty("Width")!.GetValue(o)), Convert.ToDouble(t.GetProperty("Height")!.GetValue(o)))); }
                    report.Add($"room {roomSize.W}x{roomSize.H}; hover targets: {string.Join("; ", targets.Select(b => $"{b.X},{b.Y} {b.Width}x{b.Height}"))}");
                }

                async Task Pass(string label)
                {
                    phase = label;
                    await Task.Delay(500);
                    var surface = Control<FrameworkElement>(surfaceName);
                    var size = new Size(surface.ActualWidth, surface.ActualHeight);
                    if (size.Width < 50) { report.Add($"{label}: surface has no size"); return; }
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
                    // PointToScreen and SetCursorPos work in device pixels, sizes are in DIPs: the user's 24 Oct remark (hover points above the object) was this.
                    Point ToScreen(double x, double y) => surface.PointToScreen(new Point(x, y));
                    var origin = new Point(0, 0);
                    maxGapMs = 0; ticks = 0; lastTick.Restart();
                    var sw = Stopwatch.StartNew();
                    long steps = 0, slowSteps = 0, worstStep = 0, hits = 0;
                    while (sw.ElapsedMilliseconds < passMs)
                    {
                        var t = sw.ElapsedMilliseconds / 1000.0;
                        var fx = (Math.Sin(t * 1.7) + 1) / 2; var fy = (Math.Cos(t * 2.9) + 1) / 2;
                        if (targets.Count > 0)
                        {
                            // Jitter inside the stored objects' boxes (the sign, the doors): the pointer stays over a hotspot.
                            var box = targets[(int)(t * 0.8) % targets.Count];
                            var imageScale = Math.Min(size.Width / roomSize.W, size.Height / roomSize.H);
                            var ox = (size.Width - roomSize.W * imageScale) / 2; var oy = (size.Height - roomSize.H * imageScale) / 2;
                            var p = ToScreen(ox + (box.X + fx * box.Width) * imageScale, oy + (box.Y + fy * box.Height) * imageScale);
                            SetCursorPos((int)p.X, (int)p.Y);
                        }
                        else { var p = ToScreen(fx * size.Width * 0.98 + 2, fy * size.Height * 0.98 + 2); SetCursorPos((int)p.X, (int)p.Y); }
                        var step = Stopwatch.StartNew();
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        if (Field("_browsedHover") is not null || Field("_hoveredHotspotRowId") is not null || Field("_playHighlightId") is not null) hits++;
                        var ms = step.ElapsedMilliseconds; steps++; if (ms > 100) slowSteps++; worstStep = Math.Max(worstStep, ms);
                        await Task.Delay(6);
                    }
                    report.Add($"{label}: {steps} moves in {sw.ElapsedMilliseconds} ms ({steps * 1000.0 / sw.ElapsedMilliseconds:0} moves/s); heartbeat ticks {ticks}, max gap {maxGapMs} ms; input turnaround worst {worstStep} ms, >100 ms: {slowSteps}; steps with an object under the pointer: {hits}");
                    Flush();
                }

                Control<RadioButton>("ObjectsAlways").IsChecked = true; Control<RadioButton>("DoorwaysAlways").IsChecked = true;
                await Task.Delay(800);
                {
                    window.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, "always.png")); encoder.Save(stream);
                }
                foreach (var adv in new[] { false, true })
                {
                    if (only is "normal" or "advanced" && (only == "advanced") != adv) continue;
                    advanced.IsChecked = adv; advanced.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    await Task.Delay(1500);
                    foreach (var (objects, doors) in new[] { ("ObjectsNever", "DoorwaysNever"), ("ObjectsHover", "DoorwaysHover"), ("ObjectsAlways", "DoorwaysAlways") })
                    {
                        Control<RadioButton>(objects).IsChecked = true; Control<RadioButton>(doors).IsChecked = true;
                        await Pass($"{(adv ? "Advanced" : "Normal")} hotspots {objects[7..]} doorways {doors[8..]}");
                    }
                }
                report.Add("DONE");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                Flush();
                try { Call("StopPlay"); } catch { }
                Environment.Exit(Environment.ExitCode);
            }
        };
        app.Run(window);
    }
}

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: OverlaySurvey <game folder> <output folder> [room]
// Room overlays, Advanced panel and Game Status survey (20 October 2026), implementation-agnostic so it runs before and after the change.
// The real MainWindow (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR in the output folder) opens the game, selects a room
// as a user does (the given one, else the boot's room when its catalog preparation is live, else the first live room), waits for the live
// session, stops every clock, then: Hotspots and Doorways Never vs Always, Advanced Show depth areas and Show walk boxes, each measured as
// changed pixels of the displayed room (RoomViewportHost) against the plain view; the visible right-panel sections; Game Status opened from
// its header button, one pending change where the window offers one, then one invoke of its Apply button (automation peer, so a disabled
// button is not clicked) and whether the window closed. Writes survey.txt (one summary line last) and PNGs.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>();
        var summary = new Dictionary<string, string>();
        var game = new GameFolderScanner().Scan(args[0]);
        int roomId;
        if (args.Length > 2) roomId = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
        else
        {
            var boot = ScummGameSession.BootForPlay(game, out _);
            var prepare = typeof(ScummSession).GetMethod("PrepareManualRoom");
            bool Live(int id)
            {
                if (prepare is null) return true;
                dynamic result = prepare.Invoke(boot, [game.Rooms.First(r => r.ResourceId == id), 2400, 90, CancellationToken.None])!;
                return (bool)result.Succeeded;
            }
            roomId = boot.CurrentRoomId is int b && Live(b) ? b : game.Rooms.Select(r => r.ResourceId!.Value).First(Live);
        }
        report.Add($"{game.Id} {args[0]}: room {roomId}");

        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        T Control<T>(string name) where T : class => (T)window.FindName(name);
        void StopClocks()
        {
            foreach (var field in typeof(MainWindow).GetFields(Private).Where(f => f.FieldType == typeof(DispatcherTimer)))
                (field.GetValue(window) as DispatcherTimer)?.Stop();
        }
        async Task Settle(int ms = 350)
        {
            await Task.Delay(ms);
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            StopClocks();
        }
        uint[] Pixels(string name)
        {
            var host = Control<FrameworkElement>("RoomViewportHost");
            host.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)host.ActualWidth), Math.Max(1, (int)host.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, name + ".png"))) encoder.Save(stream);
            var pixels = new uint[bitmap.PixelWidth * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        static int Diff(uint[] a, uint[] b) => a.Length != b.Length ? -1 : a.Zip(b).Count(p => p.First != p.Second);
        string Visible(string name) => Control<UIElement>(name) is { } e ? (e.IsVisible ? (e is UIElement { IsEnabled: false } ? "disabled" : "shown") : "hidden") : "missing";

        void Flush() { lock (report) File.WriteAllLines(Path.Combine(output, "survey.txt"), report); }
        var watchdog = new System.Threading.Timer(_ => { lock (report) report.Add("WATCHDOG: no completion within 240 s; stopped"); Flush(); Environment.Exit(2); }, null, 240_000, Timeout.Infinite);
        window.ContentRendered += async (_, _) =>
        {
            var watch = Stopwatch.StartNew();
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Control<TreeView>("ResourceTree");
                var room = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == roomId &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room);
                bool Live() => Field("_session") is not null && Field("_currentRoom") is not null && Field("_play") is null ||
                               (bool)(Prop("PlayActive") ?? false) && Field("_playShown") is not null;
                while (!Live() && watch.Elapsed.TotalSeconds < 120) await Task.Delay(100);
                if (!Live()) throw new InvalidOperationException("No live room within 120 s");
                await Task.Delay(2500);
                var verbGame = Field("_play") is not null;
                var shownRoom = verbGame ? ((ScummGameSession)Field("_play")!).CurrentRoomId : roomId;
                report.Add($"live after {watch.Elapsed.TotalSeconds:0.0} s; {(verbGame ? "native play screen" : "LOOM session")}; room shown {shownRoom}");
                summary["room"] = shownRoom?.ToString() ?? "?";
                Control<Expander>("RoomOptionsExpander").IsExpanded = true;
                await Settle();

                // Normal mode: hotspots and doorways.
                summary["hotspots option"] = Visible("ObjectAreaOptions");
                summary["doorways option"] = Visible("DoorwayOptions");
                Control<RadioButton>("ObjectsNever").IsChecked = true; Control<RadioButton>("DoorwaysNever").IsChecked = true;
                await Settle();
                var plain = Pixels("00-plain");
                var noise = Diff(plain, Pixels("00-plain-again"));
                Control<RadioButton>("ObjectsAlways").IsChecked = true; await Settle();
                summary["hotspots Always px"] = Diff(plain, Pixels("01-hotspots-always")).ToString();
                Control<RadioButton>("ObjectsNever").IsChecked = true; Control<RadioButton>("DoorwaysAlways").IsChecked = true; await Settle();
                summary["doorways Always px"] = Diff(plain, Pixels("02-doorways-always")).ToString();
                Control<RadioButton>("DoorwaysNever").IsChecked = true; await Settle();
                report.Add($"noise between two plain captures: {noise} px");

                // Advanced mode: depth areas, walk boxes and the right panel.
                var advanced = Control<CheckBox>("AdvancedModeToggle");
                advanced.IsChecked = true; advanced.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Settle(800);
                var advancedPlain = Pixels("03-advanced-plain");
                summary["depth option"] = Visible("RoomMaskToggle");
                summary["walkbox option"] = Visible("WalkBoxToggle");
                var mask = Control<CheckBox>("RoomMaskToggle");
                if (mask.IsVisible && mask.IsEnabled) { mask.IsChecked = true; await Settle(); summary["depth px"] = Diff(advancedPlain, Pixels("04-depth")).ToString(); mask.IsChecked = false; await Settle(); }
                else summary["depth px"] = "n/a";
                var boxes = Control<CheckBox>("WalkBoxToggle");
                if (boxes.IsVisible && boxes.IsEnabled) { boxes.IsChecked = true; await Settle(); summary["walkbox px"] = Diff(advancedPlain, Pixels("05-walkboxes")).ToString(); boxes.IsChecked = false; await Settle(); }
                else summary["walkbox px"] = "n/a";

                var panel = Control<Border>("RoomObjectPanel");
                var sections = new List<string>();
                void Walk(DependencyObject node, int depth)
                {
                    foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<FrameworkElement>())
                    {
                        if (!child.IsVisible) continue;
                        if (child is Expander expander)
                        {
                            var header = expander.Header is TextBlock t ? t.Text : expander.Header?.ToString() ?? "";
                            var count = expander.Content is Panel p ? p.Children.Count : expander.Content is FrameworkElement { } c && LogicalTreeHelper.GetChildren(c).OfType<Panel>().FirstOrDefault() is { } inner ? inner.Children.Count : 0;
                            sections.Add(depth == 0 ? $"{header}({count})" : $"  {header}");
                            continue;
                        }
                        if (child is TextBlock { Text: var text } && text.Length > 0 && text == text.ToUpperInvariant() && text.Any(char.IsLetter)) sections.Add(text);
                        if (child is Panel or Decorator or ScrollViewer or ContentControl) Walk(child, child.Name is "GenericRoomSourceHost" ? 0 : depth);
                    }
                }
                Walk(panel, 0);
                var top = sections.Where(s => !s.StartsWith("  ", StringComparison.Ordinal)).ToArray();
                report.Add("Advanced right panel: " + string.Join(" | ", sections));
                summary["advanced panel"] = string.Join("; ", top.Take(12)) + (top.Length > 12 ? $"; +{top.Length - 12}" : "");
                Pixels("06-advanced-window");
                CaptureWindow(window, Path.Combine(output, "07-window.png"));

                report.Add("SUMMARY-PRE " + string.Join(" ¦ ", summary.Select(p => $"{p.Key}: {p.Value}"))); Flush();
                // Game Status: one click on Apply.
                var status = Control<Button>("GameStatusButton");
                if (!status.IsVisible || !status.IsEnabled) summary["game status"] = "not offered";
                else
                {
                    string outcome = "?";
                    _ = window.Dispatcher.InvokeAsync(async () =>
                    {
                        Window? dialog = null;
                        try
                        {
                        for (var i = 0; i < 100 && dialog is null; i++)
                        {
                            await Task.Delay(100);
                            dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w != window && w.IsVisible && w.GetType().Name.Contains("Status"));
                        }
                        if (dialog is null) { outcome = "no window" + CloseMessageBoxes(); return; }
                        await Task.Delay(800);
                        var change = "no change made";
                        if (dialog.FindName("CatalogItems") is Selector catalog && dialog.FindName("AddItemButton") is Button add)
                        {
                            if (dialog.FindName("GrantRecipient") is Selector who && who.SelectedIndex < 0 && who.Items.Count > 0) who.SelectedIndex = 0;
                            for (var i = 0; i < catalog.Items.Count && !add.IsEnabled; i++) { catalog.SelectedIndex = i; await Task.Delay(20); }
                            if (add.IsEnabled) { Invoke(add); change = "one item added"; }
                        }
                        await Task.Delay(200);
                        var apply = dialog.FindName("ApplyButton") as Button;
                        var label = apply?.Content?.ToString() ?? "no Apply button";
                        var enabled = apply?.IsEnabled == true;
                        if (enabled) Invoke(apply!);
                        await Task.Delay(500);
                        outcome = $"'{label}' {(enabled ? "clicked once" : "disabled")} after {change}: {(dialog.IsVisible ? "window stays open" : "closed")}";
                        }
                        catch (Exception ex) { outcome = "error: " + ex.GetType().Name + " " + ex.Message; }
                        finally { if (dialog?.IsVisible == true) dialog.Close(); }
                    }, DispatcherPriority.Background);
                    status.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    for (var i = 0; i < 50 && outcome == "?"; i++) await Task.Delay(100);
                    summary["game status"] = outcome;
                }
                report.Add("SUMMARY " + string.Join(" Â¦ ", summary.Select(p => $"{p.Key}: {p.Value}")));
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "survey.txt"), report);
                try { Call("StopPlay"); } catch { }
                Flush(); Environment.Exit(Environment.ExitCode);
            }
        };
        app.Run(window);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumProc proc, IntPtr param);
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern uint GetDlgItemText(IntPtr hwnd, int id, System.Text.StringBuilder text, int max);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    /// <summary>A message box the app showed instead of a window: its text, then it is closed so the run goes on.</summary>
    static string CloseMessageBoxes()
    {
        var texts = new List<string>();
        EnumThreadWindows(GetCurrentThreadId(), (hwnd, _) =>
        {
            var name = new System.Text.StringBuilder(64); GetClassName(hwnd, name, 64);
            if (name.ToString() != "#32770") return true;
            var text = new System.Text.StringBuilder(1024); GetDlgItemText(hwnd, 0xFFFF, text, 1024);
            texts.Add(text.ToString()); PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
        return texts.Count == 0 ? "" : "; message box: " + string.Join(" / ", texts);
    }

    static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();

    static void CaptureWindow(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}





using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Usage: PanelCheck <game folder> <output folder> <room> [select]
// Opens the game in the real MainWindow (off screen), switches to Advanced, shows the room, optionally selects the first object that has actions
// (as a click on its hotspot row would) and writes panel.txt (every visible text, button and expander of the right panel) and window.png.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Dump(DependencyObject o, StringBuilder sb, int depth)
    {
        if (o is UIElement { IsVisible: false }) return;
        var pad = new string(' ', depth * 2);
        switch (o)
        {
            case Expander e: sb.AppendLine($"{pad}[expander {(e.IsExpanded ? "open" : "closed")}] {(e.Header is TextBlock t ? t.Text : e.Header)}"); break;
            case Button b: sb.AppendLine($"{pad}[button] {(b.Content is TextBlock bt ? bt.Text : b.Content)}"); return;
            case TextBox x: sb.AppendLine($"{pad}[textbox {x.Text.Length} chars] {x.Text.Split('\n').FirstOrDefault()?.Trim()}"); return;
            case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text): sb.AppendLine($"{pad}{tb.Text}"); return;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++) Dump(VisualTreeHelper.GetChild(o, i), sb, o is Expander or StackPanel ? depth + 1 : depth);
    }

    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var room = int.Parse(args[2]);
        var select = args.Length > 3 && args[3] == "select";
        var log = new StringBuilder();
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Left = -12000, Top = 0, Width = 1500, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        T Control<T>(string name) where T : class => (T)window.FindName(name);
        _ = new System.Threading.Timer(_ => { File.WriteAllText(Path.Combine(output, "panel.txt"), "TIMEOUT\n" + log); Environment.Exit(3); }, null, 120_000, Timeout.Infinite);

        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(4000);
                var adv = Control<CheckBox>("AdvancedModeToggle");
                adv.IsChecked = true; adv.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(1500);
                var game = (GameLibrary)Field("_game")!;
                log.AppendLine($"game: {game.Title} {game.Version}; scripts {game.Resources.Count(r => r.Kind == ResourceKind.Script)} + local {game.RoomLocalScripts.Count}");
                var resource = game.Rooms.First(r => r.ResourceId == room);
                Call("SelectResourceInTree", resource);
                await Task.Delay(9000);
                if (args.Length > 3 && args[3].StartsWith("pan"))
                {
                    object? play = Field("_play");
                    string Cam() => play is null ? "no play" : $"camera {play.GetType().GetProperty("CameraX")!.GetValue(play)} range {play.GetType().GetProperty("CameraPanRange")!.GetValue(play)} view {play.GetType().GetProperty("ViewLeft")!.GetValue(play)} active {(bool)typeof(MainWindow).GetProperty("PlayActive", Private)!.GetValue(window)!}";
                    log.AppendLine("before: " + Cam());
                    log.AppendLine($"panel: {Control<UIElement>("PlayScrollButtonsPanel").Visibility}");
                    for (var i = 0; i < 6; i++) { Call("PlayPanBy", 60.0); await Task.Delay(500); }
                    log.AppendLine("after right: " + Cam());
                    for (var i = 0; i < 12; i++) { Call("PlayPanBy", -60.0); await Task.Delay(500); }
                    log.AppendLine("after left: " + Cam());
                    log.AppendLine($"left enabled {Control<Button>("PlayScrollLeftButton").IsEnabled}, right enabled {Control<Button>("PlayScrollRightButton").IsEnabled}");
                }
                if (select)
                {
                    var actions = (System.Collections.IEnumerable)Field("_genericSourceActions")!;
                    foreach (var a in actions)
                    {
                        var t = a.GetType();
                        if ((bool)t.GetProperty("Executable")!.GetValue(a)!)
                        {
                            Call("SelectGenericInteractTarget", (int)t.GetProperty("Object")!.GetValue(a)!);
                            log.AppendLine($"selected object {t.GetProperty("Object")!.GetValue(a)}");
                            break;
                        }
                    }
                    await Task.Delay(1500);
                }
                if (args.Length > 4 && args[4] == "script")
                {
                    var local = game.AllScripts.FirstOrDefault(r => r.ScriptRoom is not null) ?? game.AllScripts.First();
                    Call("SelectResourceInTree", local);
                    await Task.Delay(4000);
                    var ssb = new StringBuilder();
                    Dump(Control<Grid>("ScriptPanel"), ssb, 0);
                    File.WriteAllText(Path.Combine(output, "script.txt"), local.Name + Environment.NewLine + ssb + Environment.NewLine + Control<TextBox>("ScriptOutput").Text);
                }
                Control<Expander>("RoomEntryTraceExpander").IsExpanded = true; Control<Expander>("StateIssuesSection").IsExpanded = true; await Task.Delay(800);
                foreach (var name in new[] { "RestartButton", "ConnectionsButton", "SessionModeToggle", "PlayFullscreenButton", "AdvanceFrameButton" })
                    log.AppendLine($"{name}: visible={Control<UIElement>(name).IsVisible} enabled={Control<UIElement>(name).IsEnabled}");
                log.AppendLine("Connections: " + Control<Button>("ConnectionsButton").Content + " / " + Control<Button>("ConnectionsButton").ToolTip);
                var sb = new StringBuilder();
                Dump(Control<Border>("RoomObjectPanel"), sb, 0);
                File.WriteAllText(Path.Combine(output, "panel.txt"), log + sb.ToString());
                var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(window);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(Path.Combine(output, "window.png"))) enc.Save(fs);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "panel.txt"), ex.ToString()); Environment.ExitCode = 1; }
            finally { Environment.Exit(Environment.ExitCode); }
        };
        app.Run(window);
    }
}

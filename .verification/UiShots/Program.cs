using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Normal-mode layout, the corner full-screen button and the Advanced script view (bytecode toggle, play button) in the real MainWindow.
// Arguments: <Games\loom> <output folder>
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string n) => (T)window.FindName(n);
        void SaveWindow(string file)
        {
            window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render((Visual)window.Content);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
            using var s = File.Create(Path.Combine(output, file)); enc.Save(s);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1500);
                var tree = Find<TreeView>("ResourceTree");
                var items = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).ToList();
                string KindOf(TreeViewItem i) => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() ?? "";
                int? IdOf(TreeViewItem i) => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag);
                Call("SelectResourceInTree", items.First(i => KindOf(i) == "Room" && IdOf(i) == 17).Tag);
                await Task.Delay(2500);
                var group = Find<FrameworkElement>("RoomDisplayGroup");
                report.Add($"normal: room view {group.ActualWidth:0}x{group.ActualHeight:0}; note '{Find<TextBlock>("ObjectsNote").Text}'; verb tooltip {Find<Border>("VerbStripBorder").ToolTip ?? "none"}");
                var fs = Find<Button>("FullscreenButton");
                report.Add($"full-screen button before hover: {fs.Visibility}");
                Call("VerbStrip_MouseEnter", null, null);
                report.Add($"after hover: {fs.Visibility}");
                SaveWindow("normal-hover.png");
                Call("Fullscreen_Click", fs, new RoutedEventArgs());
                await Task.Delay(1000);
                window.UpdateLayout();
                report.Add($"full screen: state {window.WindowState}, style {window.WindowStyle}, header {Find<FrameworkElement>("AppHeader").Visibility}, side {Find<FrameworkElement>("SidePanel").Visibility}, room view {group.ActualWidth:0}x{group.ActualHeight:0}, window {window.ActualWidth:0}x{window.ActualHeight:0}");
                SaveWindow("fullscreen.png");
                Call("SetFullscreen", false);
                await Task.Delay(800);
                report.Add($"left full screen: state {window.WindowState}, style {window.WindowStyle}, header {Find<FrameworkElement>("AppHeader").Visibility}, side {Find<FrameworkElement>("SidePanel").Visibility}, room view {group.ActualWidth:0}x{group.ActualHeight:0}");
                // Advanced: scripts
                var adv = Find<CheckBox>("AdvancedModeToggle");
                adv.IsChecked = true; Call("AdvancedModeToggle_Click", adv, new RoutedEventArgs());
                var s91 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 91);
                Call("SelectResourceInTree", s91.Tag);
                await Task.Delay(1200);
                var toggle = Find<CheckBox>("ScriptBytesToggle");
                var firstLines = (string t) => string.Join(" / ", t.Split('\n').Skip(6).Take(2).Select(l => l.TrimEnd()));
                report.Add($"script 91: bytecode on -> {firstLines(Find<TextBox>("ScriptOutput").Text)}; play button {Find<Button>("ScriptPlayButton").Visibility}");
                SaveWindow("script-bytes.png");
                toggle.IsChecked = false;
                await Task.Delay(300);
                report.Add($"bytecode off -> {firstLines(Find<TextBox>("ScriptOutput").Text)}");
                var s92 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 92);
                Call("SelectResourceInTree", s92.Tag);
                await Task.Delay(800);
                report.Add($"next script keeps the choice: toggle {toggle.IsChecked}, output {firstLines(Find<TextBox>("ScriptOutput").Text)}");
                SaveWindow("script-nobytes.png");
                var s5 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 5);
                Call("SelectResourceInTree", s5.Tag);
                await Task.Delay(600);
                report.Add($"script 05 (not a scene): play button {Find<Button>("ScriptPlayButton").Visibility}");
                // Play as cutscene
                Call("SelectResourceInTree", s91.Tag);
                await Task.Delay(800);
                Call("ScriptPlay_Click", null, null);
                await Task.Delay(3000);
                var session = (LoomExplorationSession)Field("_session")!;
                report.Add($"play as cutscene: session room {session.Snapshot.RoomId}, in scene {session.InCutscene}, playback {(Field("_playback") is not null)}, selected view room {Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"))}");
                SaveWindow("script-played.png");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Opens the real Game Status window on a LOOM session (Room 43 visited from the catalog), ticks a story event, applies it, saves captures.
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
        void Save(FrameworkElement e, string file)
        {
            e.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)e.ActualWidth, (int)e.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(e);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bitmap));
            using var s = File.Create(Path.Combine(output, file)); enc.Save(s);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1500);
                var session = (LoomExplorationSession)Field("_session")!;
                session.VisitRoomById(43);
                for (var i = 0; i < 60; i++) session.AdvanceFrame();
                foreach (var (name, picker) in new[] { ("picker-all", (Window)Call("BuildCutscenePicker")!) })
                {
                    picker.Show();
                    await Task.Delay(800);
                    Save(picker, name + ".png");
                    picker.Close();
                }
                var status = (GameStatusWindow)Call("BuildGameStatusWindow")!;
                status.Show();
                await Task.Delay(1200);
                var list = (ListBox)status.FindName("FlagList");
                var expander = (Expander)status.FindName("FlagExpander");
                report.Add($"expander visible {expander.Visibility}, rows {list.Items.Count}");
                expander.IsExpanded = true;
                await Task.Delay(300);
                Save(status, "status-1-before.png");
                var row = list.Items.OfType<GameStatusWindow.FlagRow>().First(r => r.Key == "bit[0xDB6]");
                report.Add($"row: {row.Label} | {row.Detail} | checked {row.Checked}; session bit {session.StoryBit("bit[0xDB6]")}");
                row.Checked = true;
                await Task.Delay(300);
                var apply = (Button)status.FindName("ApplyButton");
                report.Add($"after ticking: apply enabled {apply.IsEnabled}, message '{((TextBlock)status.FindName("EditMessage")).Text}'");
                Save(status, "status-2-ticked.png");
                typeof(GameStatusWindow).GetMethod("Apply_Click", Private)!.Invoke(status, [apply, new RoutedEventArgs()]);
                await Task.Delay(500);
                report.Add($"after apply: session bit {session.StoryBit("bit[0xDB6]")}, message '{((TextBlock)status.FindName("EditMessage")).Text}'");
                Save(status, "status-3-applied.png");
                status.Close();
                // Advanced mode: a script, a sound and a costume view each offer the way back to the room.
                var adv = (CheckBox)window.FindName("AdvancedModeToggle");
                adv.IsChecked = true; Call("AdvancedModeToggle_Click", adv, new RoutedEventArgs());
                var tree = (TreeView)window.FindName("ResourceTree");
                var back = (Button)window.FindName("BackToSceneButton");
                var items = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).ToList();
                string KindOf(TreeViewItem i) => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() ?? "";
                int? IdOf(TreeViewItem i) => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag);
                var room17 = items.First(i => KindOf(i) == "Room" && IdOf(i) == 17);
                Call("SelectResourceInTree", room17.Tag);
                await Task.Delay(1500);
                report.Add($"room 17 shown: back button {back.Visibility}");
                foreach (var kind in new[] { "Script", "Sound", "Costume" })
                {
                    var item = items.FirstOrDefault(i => KindOf(i) == kind);
                    if (item is null) { report.Add($"{kind}: none in the tree"); continue; }
                    Call("SelectResourceInTree", item.Tag);
                    await Task.Delay(900);
                    report.Add($"{kind} view: back button {back.Visibility} '{back.Content}'");
                    Save(window, $"advanced-{kind}.png");
                    if (back.Visibility == Visibility.Visible)
                    {
                        Call("BackToScene_Click", back, new RoutedEventArgs());
                        await Task.Delay(1500);
                        var shown = Field("_currentRoomResource");
                        report.Add($"  after clicking: back {back.Visibility}, room shown {shown?.GetType().GetProperty("ResourceId")?.GetValue(shown)}");
                    }
                }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

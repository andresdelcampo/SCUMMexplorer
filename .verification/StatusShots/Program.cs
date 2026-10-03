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
                foreach (var (name, picker) in new[] { ("picker-room02", (Window)Call("BuildRoomCutscenePicker", 2)!), ("picker-all", (Window)Call("BuildCutscenePicker")!) })
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
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

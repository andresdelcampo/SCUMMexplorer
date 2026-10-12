using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Opens a browse-only game in the real MainWindow, turns Advanced mode and "Always" object boxes on, and saves each room with its hotspot list.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var advanced = (CheckBox)window.FindName("AdvancedModeToggle");
                advanced.IsChecked = true;
                Call("UpdateAdvancedMode");
                ((RadioButton)window.FindName("ObjectsAlways")).IsChecked = true;
                ((RadioButton)window.FindName("DoorwaysAlways")).IsChecked = true;
                var report = new List<string>();
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .FirstOrDefault(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    if (item is null) { report.Add($"Room {room:00}: not in this game"); continue; }
                    Call("SelectResourceInTree", item.Tag!);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    window.UpdateLayout();
                    var list = (Panel)window.FindName("ObjectList");
                    var overlay = (Panel)window.FindName("HotspotHighlightOverlay");
                    report.Add($"Room {room:00}: header=\"{((TextBlock)window.FindName("ObjectListHeader")).Text}\" rows={list.Children.Count} boxesDrawn={overlay.Children.Count} " +
                        $"overlay={overlay.Visibility} section={((UIElement)window.FindName("ObjectListSection")).Visibility} options={((UIElement)window.FindName("ObjectAreaOptions")).Visibility}");
                    report.Add($"  doorway buttons: {string.Join(" | ", ((Panel)window.FindName("RoomNavigationButtons")).Children.OfType<Button>().Select(b => b.Content))}; doorway boxes drawn={((Panel)window.FindName("RoomNavigationOverlay")).Children.Count}; doorway options={((UIElement)window.FindName("DoorwayOptions")).Visibility}");
                    report.Add("  first rows: " + string.Join(" | ", list.Children.OfType<Button>().Take(6).Select(b => b.Content)));
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"room-{room:00}.png")); encoder.Save(stream);
                }
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

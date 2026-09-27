using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Rendering;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var report = new List<string>();
                foreach (int room in args.Length > 2 ? args.Skip(2).Select(int.Parse) : new[] {3, 17})
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    if (Field("_currentRoomResource") != item.Tag) throw new Exception($"Room {room} did not load");
                    await Task.Delay(300);
                    window.UpdateLayout();
                    var selection = (RoomObjectSelection)Field("_objectSelection")!;
                    var images = (IReadOnlyList<RoomObjectImage>)Field("_objects")!;
                    report.Add($"Room {room:00}: active images {string.Join(',', images.Where(selection.IsActive).Select(i => i.Id))}");
                    foreach (string name in new[] {"SessionStatusText", "ObjectsNote", "StatusText"})
                        report.Add(name + ": " + ((TextBlock)window.FindName(name)).Text);
                    foreach (var button in ((Panel)window.FindName("RoomNavigationButtons")).Children.OfType<Button>())
                        report.Add($"Doorway button \"{button.Content}\": {button.ToolTip?.ToString()?.Replace("\n", " | ")}");
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"room-{room:00}.png")); encoder.Save(stream);
                }
                File.WriteAllLines(Path.Combine(output, "window-check.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

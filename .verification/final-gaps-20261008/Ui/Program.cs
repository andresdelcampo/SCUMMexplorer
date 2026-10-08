using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.GetFullPath(Path.Combine(args[1], "cache")));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, ShowActivated = false, Left = -2000 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        var report = new List<string>();
        void Check(bool ok, string message) => report.Add((ok ? "PASS " : "FAIL ") + message);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var (edition, id, owner) in new[] {
                    ("loom ega mt32 en", 116, "Script 114"),
                    ("ALTERNATIVE LOOMS/loom ega mt32 es", 51, "Script 49"),
                    ("loom vga en", 97, "") })
                {
                    Call("OpenGameFolder", Path.GetFullPath(Path.Combine(args[0], edition)));
                    Find<ToggleButton>("AdvancedModeToggle").IsChecked = true;
                    Call("AdvancedModeToggle_Click", Find<ToggleButton>("AdvancedModeToggle"), new RoutedEventArgs());
                    var game = (GameLibrary)Field("_game")!;
                    var resource = game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == id && r.ScriptRoom is null);
                    Call("SelectResourceInTree", resource);
                    await Task.Delay(150);
                    var title = Find<TextBlock>("PageTitle").Text;
                    var note = Find<TextBlock>("ScriptNote").Text;
                    var text = Find<TextBox>("ScriptOutput").Text;
                    if (owner.Length > 0)
                    {
                        Check(title == resource.Name, edition + " interior entry has numeric page label");
                        Check(note.Contains("leftover index entry") && note.Contains(owner), edition + " names containing resource");
                        Check(note.Contains("does not establish a standalone script"), edition + " retains explicit evidence limit");
                        Check(!title.Contains("animate actor"), edition + " no accidental reviewed title");
                        Check(id == 116 ? text.Length > 100 : text.Contains("past the end"), edition + " original raw decode/length diagnostic retained");
                    }
                    else
                    {
                        Check(title.Contains("Room 34 arrival"), "VGA genuine Script 97 reviewed title retained");
                        Check(!note.Contains("leftover"), "VGA not misclassified as old-v3 interior entry");
                        Check(text.Length > 100, "VGA script remains readable");
                    }
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(args[1], $"script-{(owner.Length == 0 ? "vga" : id == 116 ? "en" : "es")}-{id}.png"));
                    encoder.Save(file);
                }
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                app.Shutdown(report.Any(r => r.StartsWith("FAIL ")) ? 1 : 0);
            }
            catch (Exception exception)
            {
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report.Append(exception.ToString())); app.Shutdown(1);
            }
        };
        app.Run(window);
    }
}

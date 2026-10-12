using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Opens a browsed v4 game (Monkey Island VGA) in the real MainWindow, selects the given rooms and one costume, and saves what the window shows.
// args: game folder, output folder, room numbers…
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
        void Shot(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                report.Add($"game: {game.Title} / {game.Id} / {game.Version} / {game.Resources.Count} resources / {game.Rooms.Count()} rooms");
                var tree = (TreeView)window.FindName("ResourceTree");
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    var resource = game.Rooms.FirstOrDefault(r => r.ResourceId == room);
                    if (resource is null) { report.Add($"room {room}: not in this game"); continue; }
                    Call("SelectResourceInTree", resource);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != resource; n++) await Task.Delay(100);
                    if (Field("_currentRoomResource") != resource) throw new Exception($"Room {room} did not load: {((TextBlock)window.FindName("PreviewDescription")).Text}");
                    await Task.Delay(300);
                    report.Add($"room {room}: {((TextBlock)window.FindName("StatusText")).ToolTip} | {((TextBlock)window.FindName("ObjectsNote")).Text}");
                    Shot($"room-{room:00}.png");
                }
                // Normal mode: the Costumes tab of the catalog, as for LOOM.
                var tabs = (Panel)window.FindName("NormalCatalogTabs");
                report.Add($"costume tabs visible: {tabs.Visibility}");
                Call("NormalCatalogTab_Click", window.FindName("CostumesTab")!, new RoutedEventArgs());
                await Task.Delay(5000);
                var cards = (Panel)window.FindName("CostumeCardList");
                report.Add($"costume cards: {cards.Children.Count}");
                Shot("costume-cards.png");
                var wanted = int.TryParse(Environment.GetEnvironmentVariable("GV4_COSTUME"), out var wantedId) ? wantedId : (int?)null;
                var costume = game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Costume && (wanted is null || r.ResourceId == wanted));
                if (costume is null) { report.Add("no costumes offered"); }
                else {
                Call("SelectResourceInTree", costume);
                await Task.Delay(1500);
                report.Add($"costume {costume.Name}: {((TextBlock)window.FindName("StatusText")).Text} | {((TextBlock)window.FindName("CostumeMessage")).Text}");
                Shot("costume.png");
                }
                foreach (var script in game.Resources.Where(r => r.Kind == ResourceKind.Script).Skip(5).Take(2))
                {
                    Call("SelectResourceInTree", script);
                    await Task.Delay(1500);
                    var shown = ((TextBox)window.FindName("ScriptOutput")).Text;
                    var firstLines = string.Join(" / ", shown.Split((char)10).Take(4).Select(l => l.Trim()));
                    report.Add($"script {script.Name}: {((TextBlock)window.FindName("PageSubtitle")).Text} | {((TextBlock)window.FindName("ScriptNote")).Text} | {shown.Length} chars | {firstLines}");
                }
                foreach (var sound in game.Resources.Where(r => r.Kind == ResourceKind.Sound).Take(1).Concat(game.Resources.Where(r => r.Kind == ResourceKind.Sound).Skip(7).Take(1)))
                {
                    Call("SelectResourceInTree", sound);
                    for (var n = 0; n < 150 && ((TextBlock)window.FindName("AudioTime")).Text.StartsWith("Preparing"); n++) await Task.Delay(100);
                    report.Add($"sound {sound.Name}: {((TextBlock)window.FindName("AudioDescription")).Text} | {((TextBlock)window.FindName("AudioTime")).Text} | {((TextBlock)window.FindName("StatusText")).Text}");
                }
            }
            catch (Exception ex) { report.Add("ERROR " + ex); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

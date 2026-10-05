using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// LOOM CD in the real window: the room scene, the costume cards and one costume's poses. Arguments: <disc folder or .cue> <output folder>. No physical input.
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
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var report = new List<string> { $"{game.Title} | {game.Id} | rooms {game.Rooms.Count()} | costumes {game.Resources.Count(r => r.Kind == ResourceKind.Costume)}" };
                // First-visit scenes (the entry script's objects over the background), a few rooms; cards need a moment to render too.
                foreach (var number in new[] { 1, 4, 10, 13, 16, 31, 35, 40, 53, 86 })
                {
                    var room = game.Rooms.First(r => r.ResourceId == number);
                    Call("SelectResourceInTree", room);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != room; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    Shot($"room-{number:00}.png");
                    report.Add($"room {number}: {((IReadOnlyCollection<object>)((System.Collections.IEnumerable)Field("_objects")!).Cast<object>().ToList()).Count} object images");
                }
                var costumesTab = (RadioButton)window.FindName("CostumesTab");
                costumesTab.IsChecked = true;
                Call("NormalCatalogTab_Click", costumesTab, new RoutedEventArgs());
                await Task.Delay(4000);
                Shot("costume-cards.png");
                var cards = (WrapPanel)window.FindName("CostumeCardList");
                report.Add($"costume cards: {cards.Children.Count}");
                var costume = game.Resources.First(r => r.Kind == ResourceKind.Costume && r.ResourceId == (game.Id == GameId.LoomVga ? 5 : 3));
                Call("SelectResourceInTree", costume);
                await Task.Delay(1500);
                Shot("costume-5.png");
                var panel = (FrameworkElement)window.FindName("CostumePanel"); var poses = (Panel)window.FindName("CostumePoseGallery");
                report.Add($"costume panel {panel.Visibility}, poses {poses.Children.Count}, message {((TextBlock)window.FindName("CostumeMessage")).Text}");
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
            window.Close();
        };
        app.Run(window);
    }
}

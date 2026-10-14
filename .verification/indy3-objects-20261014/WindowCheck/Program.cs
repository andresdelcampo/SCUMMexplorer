using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Usage: WindowCheck <Indy3 EGA game folder> <output folder> [room ...]
// S5: opens the real MainWindow on Indiana Jones and the Last Crusade EGA with a private profile (SCUMM_EXPLORER_PROFILE_ROOT in the output folder),
// selects each room through the tree, waits for the room load, and saves a window capture, the room cards' state and the visible notes (Normal mode,
// then Advanced mode). No input is injected. Writes window-check.txt and room-NN.png / room-NN-advanced.png.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(string name)
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
                var tree = (TreeView)window.FindName("ResourceTree");
                var advancedToggle = (CheckBox)window.FindName("AdvancedModeToggle");
                var roomCards = (WrapPanel)window.FindName("RoomCardList");
                var report = new List<string>();
                await Task.Delay(1500);
                foreach (var room in args.Skip(2).Select(int.Parse))
                {
                    advancedToggle.IsChecked = false;
                    Call("UpdateAdvancedMode");
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 200 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    if (Field("_currentRoomResource") != item.Tag) throw new Exception($"Room {room} did not load");
                    await Task.Delay(500);
                    var scene = Field("_browsedScene");
                    report.Add($"Room {room:00}: first-visit scene {(scene is null ? "NONE (plain background)" : "present")}; Normal-mode note: \"{((TextBlock)window.FindName("ObjectsNote")).Text}\"");
                    Save($"room-{room:00}.png");
                    advancedToggle.IsChecked = true;
                    Call("UpdateAdvancedMode");
                    await Task.Delay(300);
                    report.Add($"   Advanced-mode note: \"{((TextBlock)window.FindName("ObjectsNote")).Text}\"");
                    Save($"room-{room:00}-advanced.png");
                }
                var cards = roomCards.Children.OfType<Button>().Count(b => b.Content is Image);
                report.Add($"Room cards with a picture: {cards} of {roomCards.Children.OfType<Button>().Count()}");
                File.WriteAllLines(Path.Combine(output, "window-check.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

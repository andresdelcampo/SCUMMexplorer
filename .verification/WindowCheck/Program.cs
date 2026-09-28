using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

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
                var advancedToggle = (CheckBox)window.FindName("AdvancedModeToggle");
                var roomCards = (WrapPanel)window.FindName("RoomCardList");
                var roomCardScroll = (ScrollViewer)window.FindName("RoomCardScroll");
                var rawTree = (TreeView)window.FindName("ResourceTree");
                var exitToggle = (CheckBox)window.FindName("RoomNavigationToggle");
                var exitOverlay = (Canvas)window.FindName("RoomNavigationOverlay");
                var expectedRoomCount = ((GameLibrary)Field("_game")!).Rooms.Count();
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
                    if (advancedToggle.IsChecked == true || rawTree.Visibility != Visibility.Collapsed ||
                        roomCards.Children.Count != expectedRoomCount || roomCardScroll.Visibility != Visibility.Visible ||
                        exitToggle.IsChecked == true || exitOverlay.Visibility != Visibility.Collapsed)
                        throw new Exception("Visual-first defaults failed: room cards, hidden resource tree, or hidden exits are incorrect.");
                    if (((TextBlock)window.FindName("PageTitle")).Text.Contains("Room", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("A raw room label is visible in the scene title.");
                    if (roomCards.Children.OfType<Button>().Any(button =>
                        string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button))))
                        throw new Exception("A visual room card has no accessible name.");
                    var selectedCard = roomCards.Children.OfType<Button>().Single(button =>
                        ((GameResource)button.Tag!).ResourceId == room);
                    if (selectedCard.BorderThickness.Left < 2 ||
                        selectedCard.BorderBrush.ToString() != new SolidColorBrush(Color.FromRgb(183, 132, 66)).ToString())
                        throw new Exception($"Selected Room {room:00} is not highlighted in the visual picker.");
                    var firstPair = roomCards.Children.OfType<Button>().Take(2).ToArray();
                    if (firstPair.Length == 2 &&
                        Math.Abs(firstPair[0].TranslatePoint(new Point(), roomCards).Y -
                                 firstPair[1].TranslatePoint(new Point(), roomCards).Y) > 1)
                        throw new Exception("Room thumbnails do not fit in the intended two-column picker.");
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
                    advancedToggle.IsChecked = true;
                    Call("UpdateAdvancedMode");
                    window.UpdateLayout();
                    if (rawTree.Visibility != Visibility.Visible ||
                        roomCardScroll.Visibility != Visibility.Collapsed ||
                        ((Border)window.FindName("RoomObjectPanel")).Visibility != Visibility.Visible ||
                        Field("_currentRoomResource") != item.Tag)
                        throw new Exception($"Advanced mode failed: tree={rawTree.Visibility}, cards={roomCards.Visibility}, panel={((Border)window.FindName("RoomObjectPanel")).Visibility}, room={Field("_currentRoomResource") == item.Tag}.");
                    advancedToggle.IsChecked = false;
                    Call("UpdateAdvancedMode");
                    window.UpdateLayout();
                }
                window.Width = 1050;
                window.Height = 700;
                await Task.Delay(200);
                window.UpdateLayout();
                if (window.FindName("RoomViewportHost") is not Grid compactViewport ||
                    compactViewport.ActualWidth < 500)
                    throw new Exception("Compact window layout leaves too little room for the scene.");
                var compactBitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                compactBitmap.Render(window);
                var compactEncoder = new PngBitmapEncoder(); compactEncoder.Frames.Add(BitmapFrame.Create(compactBitmap));
                using (var compactStream = File.Create(Path.Combine(output, "room-17-1050.png"))) compactEncoder.Save(compactStream);
                advancedToggle.IsChecked = true;
                Call("UpdateAdvancedMode");
                var scriptItem = rawTree.Items.OfType<TreeViewItem>()
                    .SelectMany(group => group.Items.OfType<TreeViewItem>())
                    .First(item => item.Tag?.GetType().GetProperty("Kind")?.GetValue(item.Tag)?.ToString() == "Script");
                Call("SelectResourceInTree", scriptItem.Tag!);
                for (int n = 0; n < 100 && ((Grid)window.FindName("ScriptPanel")).Visibility != Visibility.Visible; n++)
                    await Task.Delay(100);
                if (((Grid)window.FindName("ScriptPanel")).Visibility != Visibility.Visible ||
                    ((Border)window.FindName("RoomObjectPanel")).Visibility != Visibility.Collapsed)
                    throw new Exception("Advanced resource inspection opened without replacing the scene with its own view.");
                advancedToggle.IsChecked = false;
                Call("AdvancedModeToggle_Click", advancedToggle, new RoutedEventArgs());
                for (int n = 0; n < 100 && Field("_currentRoomResource") is null; n++) await Task.Delay(100);
                if (Field("_currentRoomResource") is null || ((Grid)window.FindName("ScriptPanel")).Visibility != Visibility.Collapsed)
                    throw new Exception("Leaving Advanced mode did not return from a technical resource to the scene.");
                File.WriteAllLines(Path.Combine(output, "window-check.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new Application();
        var window = new MainWindow { ShowInTaskbar = false, WindowState = WindowState.Minimized };
        var report = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        T Control<T>(string name) => (T)window.FindName(name);
        void Check(bool condition, string description)
        {
            report.Add((condition ? "PASS " : "FAIL ") + description);
            if (!condition) throw new Exception(description);
        }
        byte[] Pixels(BitmapSource source)
        {
            var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += (_, _) => ticks++;
        window.Loaded += async (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(args[2]);
                Call("OpenGameFolder", args[0]);
                var cards = Control<WrapPanel>("RoomCardList").Children.OfType<Button>().ToArray();
                Check(cards.Length > 50, $"all {cards.Length} LOOM cards created immediately");
                Check(cards.All(card => card.Content is TextBlock label && label.Text.Length > 0),
                    "all cards show room-number placeholders before thumbnail work completes");

                timer.Start();
                await Task.Delay(200);
                Check(ticks > 3, $"dispatcher timer remained responsive during background thumbnail work ({ticks} ticks)");
                var tree = Control<TreeView>("ResourceTree");
                var room4 = tree.Items.OfType<TreeViewItem>().SelectMany(group => group.Items.OfType<TreeViewItem>())
                    .Select(item => item.Tag).First(resource => resource?.GetType().GetProperty("Kind")?.GetValue(resource)?.ToString() == "Room" &&
                        (int?)resource.GetType().GetProperty("ResourceId")?.GetValue(resource) == 4)!;
                Call("SelectResourceInTree", room4);
                var until = DateTime.UtcNow + TimeSpan.FromSeconds(12);
                while (DateTime.UtcNow < until)
                {
                    await Task.Delay(25);
                    if (Field("_currentRoomResource") is { } current && Equals(current, room4) &&
                        cards.First(card => Equals(card.Tag, room4)).Content is Image { Source: BitmapSource }) break;
                }
                var liveRoom = Field("_currentRoom")!;
                var liveResource = Field("_currentRoomResource")!;
                Check(Equals(liveResource, room4), "room 04 opened while its card loader was active");
                var liveSelection = Field("_objectSelection")!;
                var compose = liveSelection.GetType().GetMethod("Compose")!.Invoke(liveSelection, [liveRoom])!;
                var session = Field("_session")!;
                var cameraX = (int?)session.GetType().GetProperty("CameraX")!.GetValue(session) ?? 0;
                var expected = (BitmapSource)Call("CreateRoomCardBitmap", liveRoom, compose, cameraX)!;
                var selectedCard = cards.First(card => Equals(card.Tag, room4));
                var selectedImage = (BitmapSource)((Image)selectedCard.Content).Source!;
                Check(selectedImage.PixelWidth == expected.PixelWidth && selectedImage.PixelHeight == expected.PixelHeight &&
                      Pixels(selectedImage).SequenceEqual(Pixels(expected)),
                    "selected card remains pixel-identical to the live composed room crop");

                // Reopening a different game replaces the cards while old background work is still outstanding.
                Call("OpenGameFolder", args[1]);
                var secondCards = Control<WrapPanel>("RoomCardList").Children.OfType<Button>().ToArray();
                Check(secondCards.Length > 50 && secondCards.All(card => card.Content is TextBlock),
                    "folder switch immediately replaces cards with numbered placeholders");
                Check(secondCards.All(card => ((string)card.Tag!.GetType().GetProperty("FilePath")!.GetValue(card.Tag)!).StartsWith(args[1], StringComparison.OrdinalIgnoreCase)),
                    "new card set belongs only to the second folder");
                var beforeWait = ticks;
                await Task.Delay(250);
                Check(ticks > beforeWait, "dispatcher continues processing after folder switch");
                Check(Control<WrapPanel>("RoomCardList").Children.OfType<Button>().All(card =>
                    ((string)card.Tag!.GetType().GetProperty("FilePath")!.GetValue(card.Tag)!).StartsWith(args[1], StringComparison.OrdinalIgnoreCase)),
                    "late first-folder results did not replace second-folder cards");
            }
            catch (Exception ex)
            {
                report.Add(ex.ToString());
                Environment.ExitCode = 1;
            }
            finally
            {
                timer.Stop();
                File.WriteAllLines(Path.Combine(args[2], "thumbnail-check.txt"), report);
                window.Close();
                app.Shutdown();
            }
        };

        window.Show();
        app.Run();
    }
}

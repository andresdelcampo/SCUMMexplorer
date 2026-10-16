using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: PlayWindowCheck <Indy3 EGA game folder> <output folder>
// S6 (16 October 2026): opens the real MainWindow on Indiana Jones and the Last Crusade EGA with a private profile (SCUMM_EXPLORER_PROFILE_ROOT in the
// output folder), selects Room 73 through the tree and lets the window's own play timer run the game. Clicks go to the window's live session at screen
// points read from its verb area (ScummSession.ClickScreen, the call the image's MouseLeftButtonDown makes after mapping the pointer); a key goes through
// the window's own text-input handler. Saves window captures and play-check.txt.
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
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
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
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                await Task.Delay(1500);
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 73 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 300 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 73");
                var image = (Image)window.FindName("PlayScreenImage");
                report.Add($"Play started: room {session.CurrentRoomId}, image visible {image.Visibility}, cursor {(image.Cursor == Cursors.None ? "system pointer hidden" : "system pointer")}, " +
                           $"room view {((FrameworkElement)window.FindName("RoomDisplayGroup")).Visibility}, Normal-mode note \"{((TextBlock)window.FindName("ObjectsNote")).Text}\"");
                session.PointAtScreen((200, 60));
                await Task.Delay(800);
                Save("play-01-room-73.png");
                async Task Click(int x, int y, string what)
                {
                    session.PointAtScreen((x, y));
                    var input = session.ClickScreen(x, y, out var refused);
                    report.Add($"{what}: {(input is null ? "refused: " + refused : $"runInputScript({input.Area}, {input.Value}, {input.Mode}) {input.Description}")}");
                    await Task.Delay(250);
                }
                var pickUp = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x0B);
                await Click(pickUp.X + 2, pickUp.Y + 2, "click on Pick up");
                await Click(176 - session.ViewLeft, 112 + session.MainScreen.Top, "click on the mallet");
                Save("play-02-sentence.png");
                report.Add($"sentence line: \"{ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim()}\"");
                await Click(176 - session.ViewLeft, 112 + session.MainScreen.Top, "second click on the mallet");
                for (var n = 0; n < 100 && !session.Snapshot.Inventory.Contains(836); n++) await Task.Delay(100);
                await Task.Delay(500);
                report.Add($"inventory: [{string.Join(", ", session.Snapshot.Inventory)}], verb 0x65 \"{ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x65))).Trim()}\"");
                session.PointAtScreen((20, 182));
                await Task.Delay(400);
                Save("play-03-inventory.png");
                // The window's own key path: 'x' is Look's shortcut (Script 96 +0x005B), delivered as runInputScript(1, 0x09, 1).
                var composition = new TextComposition(InputManager.Current, window, "x");
                var text = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = UIElement.PreviewTextInputEvent };
                window.RaiseEvent(text);
                await Task.Delay(400);
                report.Add($"key 'x': last input {session.DeliveredInput.LastOrDefault()?.Description}; sentence line \"{ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim()}\"");
                Save("play-04-key.png");
                report.Add($"frames played by the window: {Field("_playFrame")}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "play-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

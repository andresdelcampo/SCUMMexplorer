using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: SpanishWindowCheck <Indy3 game folder (Spanish copy)> <output folder>
// S9 (17 October 2026), adapted from .verification/indy3-play-20261015/PlayWindowCheck: opens the real MainWindow on a Spanish copy with a private profile
// (SCUMM_EXPLORER_PROFILE_ROOT in the output folder), selects Room 73 through the tree, lets the window's own play timer run the game, walks out through the
// open door (the default verb, one click), then Talk (0x0D) on Marcus in Room 1 so that Script 29 puts its dialogue choices on the verb line. The choices
// carry the copy's CP437 bytes (0xA8 is the inverted question mark); the capture shows how the window draws them with the copy's own charset.
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
                string Glyphs(int verb) => session.Verbs.FirstOrDefault(v => v.VerbId == verb) is { } v ? ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(v)).Trim() : "";
                async Task<bool> Until(Func<bool> condition, int seconds)
                {
                    for (var n = 0; n < seconds * 10 && !condition(); n++) await Task.Delay(100);
                    return condition();
                }
                async Task Click(int x, int y, string what)
                {
                    session.PointAtScreen((x, y));
                    var input = session.ClickScreen(x, y, out var refused);
                    report.Add($"{what}: {(input is null ? "refused: " + refused : input.Description)}");
                    await Task.Delay(300);
                }
                report.Add($"Play started: room {session.CurrentRoomId}; verbs: {string.Join(" | ", Enumerable.Range(1, 12).Select(Glyphs))}");
                await Until(() => session.UserInputOn && !session.InCutscene, 20);
                Save("es-01-room-73.png");
                await Click(56 - session.ViewLeft, 60 + session.MainScreen.Top, "click on the open door (Walk to)");
                report.Add($"left Room 73: {await Until(() => session.CurrentRoomId == 1, 30)}");
                report.Add($"Talk on in Room 1: {await Until(() => session.UserInputOn && !session.InCutscene && session.Verbs.Any(v => v.VerbId == 0x0D && v.Mode == 1), 60)}");
                var marcus = session.Actors.Single(actor => actor.Number == 4 && actor.Room == 1);
                var talk = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x0D);
                await Click(talk.X + 2, talk.Y + 2, "click on Talk");
                await Click(marcus.X!.Value - session.ViewLeft, marcus.Y!.Value - 20 + session.MainScreen.Top, "click on Marcus");
                await Click(marcus.X!.Value - session.ViewLeft, marcus.Y!.Value - 20 + session.MainScreen.Top, "second click on Marcus");
                report.Add($"choices offered: {await Until(() => session.Verbs.Any(v => v.VerbId == 0x78 && v.Mode == 1) && session.UserInputOn, 60)}");
                await Task.Delay(500);
                foreach (var verb in new[] { 0x78, 0x79, 0x7A })
                    report.Add($"verb 0x{verb:X2}: \"{Glyphs(verb)}\" bytes [{string.Join(" ", session.Verbs.First(v => v.VerbId == verb) is { } v ? session.VerbGlyphs(v).Where(b => b >= 0x80).Select(b => b.ToString("X2")) : [])}]");
                session.PointAtScreen((160, 60));
                await Task.Delay(400);
                Save("es-02-room-1-choices.png");
                report.Add($"frames played by the window: {Field("_playFrame")}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "window-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

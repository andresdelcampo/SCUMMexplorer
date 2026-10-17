using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: MonkeyCdPlayWindowCheck <Monkey Island CD game folder> <output folder>
// S6 MonkeyCd (17 October 2026): opens the real MainWindow on The Secret of Monkey Island CD (SCUMM v5, disc image) with a private profile
// (SCUMM_EXPLORER_PROFILE_ROOT in the output folder), selects Room 38 (the lookout, where the boot ends) through the tree and lets the window's own play
// timer run the game: the lookout's CD track ends and the game goes on to the dock. Then "Look at" + the poster (clicks at screen points read from the
// verb area, through ScummSession.ClickScreen as the image's MouseLeftButtonDown does), Esc through the window's key handler, a right click on the poster
// through the window's own right-button path (PlayRightClick), and a verb's key through the window's text input. Writes window captures, the scene-sound
// log (which sounds started, whether their waves opened: CD tracks and effects) and play-check.txt.
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
            var player = (SceneSoundPlayer)Field("_sceneSound")!;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                await Task.Delay(2500);
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 38 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 900 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 38");
                var image = (Image)window.FindName("PlayScreenImage");
                string Sentence() => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim();
                report.Add($"Play started: room {session.CurrentRoomId}, image visible {image.Visibility}, cursor {(image.Cursor == Cursors.None ? "system pointer hidden" : "system pointer")}, " +
                           $"room view {((FrameworkElement)window.FindName("RoomDisplayGroup")).Visibility}, Normal-mode note \"{((TextBlock)window.FindName("ObjectsNote")).Text}\", " +
                           $"tooltip {(image.ToolTip is null ? "none" : "shown")}; the scene sound player follows it: {player.Follows(session)}");
                session.PointAtScreen((160, 100));
                await Task.Delay(500);
                Save("play-01-lookout.png");
                // The lookout's track (sound 108) plays to its end, then Room 96 (title, sound 104) and the dock.
                for (var n = 0; n < 2400 && !(session.CurrentRoomId == 33 && session.UserInputOn && !session.InCutscene && !session.EgoMoving); n++) await Task.Delay(100);
                report.Add($"Room {session.CurrentRoomId} after {Field("_playFrame")} window frames; Guybrush at ({session.Actors.Single(a => a.Number == 1).X}, {session.Actors.Single(a => a.Number == 1).Y})");
                report.Add($"verbs drawn: {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.Width > 0).Select(v => $"0x{v.VerbId:X2} \"{ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(x => x.Slot == v.Slot))).Trim()}\" ({v.X},{v.Y})"))}");
                async Task Click(int x, int y, string what)
                {
                    session.PointAtScreen((x, y));
                    await Task.Delay(150);
                    var input = session.ClickScreen(x, y, out var refused);
                    report.Add($"{what}: {(input is null ? "refused: " + refused : $"runInputScript({input.Area}, {input.Value}, {input.Mode}) {input.Description}")}");
                    await Task.Delay(250);
                }
                var look = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x08);
                await Click(look.X + 2, look.Y + 2, "click on Look at");
                for (var n = 0; n < 40 && !Sentence().EndsWith("poster", StringComparison.Ordinal); n++)
                {
                    session.PointAtScreen((268 - session.ViewLeft, 104));
                    await Task.Delay(100);
                }
                report.Add($"pointer on the poster: sentence line \"{Sentence()}\"");
                Save("play-02-sentence.png");
                await Click(268 - session.ViewLeft, 104, "click on the poster");
                for (var n = 0; n < 200 && !(session.InCutscene && session.CutsceneSkippable); n++) await Task.Delay(100);
                await Task.Delay(300);
                report.Add($"cutscene {session.InCutscene}, skippable {session.CutsceneSkippable}, messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                Save("play-03-cutscene.png");
                var source = PresentationSource.FromVisual(window) ?? throw new Exception("no presentation source");
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent };
                var taken = (bool)Call("PlayKeyDown", escape)!;
                for (var n = 0; n < 100 && session.InCutscene; n++) await Task.Delay(100);
                report.Add($"Esc through the window's key handler: taken {taken}; cutscene {session.InCutscene}; messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                for (var n = 0; n < 100 && !session.UserInputOn; n++) await Task.Delay(100);
                // A verb's key through the window's text path: the key Script 22 gave "Look at", delivered as runInputScript(1, 8, 1).
                var lookKey = session.Verbs.Single(v => v.VerbId == 0x08).Key;
                var composition = new TextComposition(InputManager.Current, window, ((char)lookKey).ToString());
                window.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = UIElement.PreviewTextInputEvent });
                await Task.Delay(500);
                report.Add($"key '{(char)lookKey}': last input {session.DeliveredInput.LastOrDefault()?.Description}; sentence line \"{Sentence()}\"");
                // R-7.16: a right click on the poster through the window's own right-button path: runInputScript(2, 0, 2); Script 4 +0x0016 takes the default verb.
                for (var n = 0; n < 40 && !Sentence().EndsWith("poster", StringComparison.Ordinal); n++)
                {
                    session.PointAtScreen((268 - session.ViewLeft, 104));
                    await Task.Delay(100);
                }
                const string looked = "sentence script 2 runs verb 0x08 with objects 429";
                var before = session.SessionTrace.Count(line => line.StartsWith(looked, StringComparison.Ordinal));
                var right = (ScummPlayInput?)Call("PlayRightClick", 268 - session.ViewLeft, 104);
                for (var n = 0; n < 50 && session.SessionTrace.Count(line => line.StartsWith(looked, StringComparison.Ordinal)) == before; n++) await Task.Delay(100);
                for (var n = 0; n < 150 && !session.Messages.OnScreen.Any(m => m.Text.Trim().Length > 0); n++) await Task.Delay(100);
                report.Add($"right click on the poster through the window: {(right is null ? "not delivered" : $"runInputScript({right.Area}, {right.Value}, {right.Mode}) {right.Description}")}; " +
                           $"Look at poster sentences {before} -> {session.SessionTrace.Count(line => line.StartsWith(looked, StringComparison.Ordinal))}; messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                Save("play-04-right-click.png");
                report.Add($"frames played by the window: {Field("_playFrame")}");
                report.Add($"-- scene sound log (device {SceneSoundPlayer.DeviceName(player.Mode)})");
                foreach (var entry in player.Log) report.Add($"   frame {entry.Frame} {entry.Milliseconds} ms {entry.Action} {entry.SoundId} {entry.Kind} | {entry.Source}");
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

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: Monkey2PlayWindowCheck <Monkey Island 2 game folder> <output folder>
// S6 Monkey2 (17 October 2026): opens the real MainWindow on Monkey Island 2 (SCUMM v5) with a private profile (SCUMM_EXPLORER_PROFILE_ROOT in the output
// folder), selects Room 4 (the campfire, where the play boot ends) through the tree and lets the window's own play timer run the game to Woodtick (Room 7).
// Then "Look at" + the sign (clicks at screen points through ScummSession.ClickScreen, as the image's MouseLeftButtonDown does), Esc through the window's key
// handler, "Pick up" + the sign (the shovel's icon in the inventory), a walk west into Largo's dialogue and a click on its first choice, and a right click on
// the sign through the window's own right-button path. Writes window captures, the scene-sound log (which iMUSE songs started, on which device, and whether
// their waves opened) and play-check.txt.
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
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 4 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 1800 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 4");
                var image = (Image)window.FindName("PlayScreenImage");
                bool Quiet() => session.UserInputOn && !session.InCutscene && !session.EgoMoving && session.Messages.OnScreen.Count == 0;
                string Sentence() => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim();
                report.Add($"Play started: room {session.CurrentRoomId}, image visible {image.Visibility}, cursor {(image.Cursor == Cursors.None ? "system pointer hidden" : "system pointer")}, " +
                           $"room view {((FrameworkElement)window.FindName("RoomDisplayGroup")).Visibility}, Normal-mode note \"{((TextBlock)window.FindName("ObjectsNote")).Text}\", " +
                           $"tooltip {(image.ToolTip is null ? "none" : "shown")}; the scene sound player follows it: {player.Follows(session)}; device {SceneSoundPlayer.DeviceName(player.Mode)}");
                session.PointAtScreen((160, 100));
                await Task.Delay(500);
                Save("play-01-campfire.png");
                for (var n = 0; n < 3000 && !(session.CurrentRoomId == 7 && Quiet()); n++) await Task.Delay(100);
                report.Add($"Room {session.CurrentRoomId} after {Field("_playFrame")} window frames; Guybrush at ({session.Actors.Single(a => a.Number == 1).X}, {session.Actors.Single(a => a.Number == 1).Y})");
                report.Add($"verbs drawn: {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.Width > 0).Select(v => $"0x{v.VerbId:X2} ({v.X},{v.Y}){(v.IsText ? "" : $" image {v.ImageObject}")}"))}");
                async Task Click(int x, int y, string what)
                {
                    session.PointAtScreen((x, y));
                    await Task.Delay(150);
                    var input = session.ClickScreen(x, y, out var refused);
                    report.Add($"{what}: {(input is null ? "refused: " + refused : $"runInputScript({input.Area}, {input.Value}, {input.Mode}) {input.Description}")}");
                    await Task.Delay(250);
                }
                async Task Verb(int id, string what)
                {
                    var verb = session.PlayVerbArea().Items.Single(entry => entry.VerbId == id);
                    await Click(verb.X + 1, verb.Y + 1, what);
                }
                await Verb(0x08, "click on Look at");
                for (var n = 0; n < 40 && !Sentence().EndsWith("sign", StringComparison.Ordinal); n++)
                {
                    session.PointAtScreen((736 - session.ViewLeft, 28));
                    await Task.Delay(100);
                }
                report.Add($"pointer on the sign: sentence line \"{Sentence()}\"");
                Save("play-02-sentence.png");
                await Click(736 - session.ViewLeft, 28, "click on the sign");
                for (var n = 0; n < 200 && !(session.InCutscene && session.CutsceneSkippable); n++) await Task.Delay(100);
                report.Add($"cutscene {session.InCutscene}, skippable {session.CutsceneSkippable}, messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                Save("play-03-cutscene.png");
                var source = PresentationSource.FromVisual(window) ?? throw new Exception("no presentation source");
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent };
                var taken = (bool)Call("PlayKeyDown", escape)!;
                for (var n = 0; n < 100 && session.InCutscene; n++) await Task.Delay(100);
                report.Add($"Esc through the window's key handler: taken {taken}; cutscene {session.InCutscene}");
                for (var n = 0; n < 300 && !Quiet(); n++) await Task.Delay(100);
                await Verb(0x09, "click on Pick up");
                session.PointAtScreen((736 - session.ViewLeft, 28));
                await Task.Delay(300);
                await Click(736 - session.ViewLeft, 28, "click on the sign");
                for (var n = 0; n < 300 && !session.Snapshot.Inventory.Contains(45); n++) await Task.Delay(100);
                for (var n = 0; n < 300 && !Quiet(); n++) await Task.Delay(100);
                report.Add($"inventory [{string.Join(", ", session.Snapshot.Inventory)}]; slots {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.VerbId is >= 0xC8 and <= 0xCF).Select(v => $"0x{v.VerbId:X2} image {v.ImageObject}"))}");
                Save("play-04-inventory.png");
                // A right click on the sign through the window's own right-button path: runInputScript(2, 0, 2); Script 4 takes the default verb (Look at).
                session.PointAtScreen((736 - session.ViewLeft, 28));
                await Task.Delay(300);
                var right = (ScummPlayInput?)Call("PlayRightClick", 736 - session.ViewLeft, 28);
                for (var n = 0; n < 100 && !session.Messages.OnScreen.Any(m => m.Text.Contains("Woodtick")); n++) await Task.Delay(100);
                report.Add($"right click on the sign through the window: {(right is null ? "not delivered" : $"runInputScript({right.Area}, {right.Value}, {right.Mode})")}; messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                for (var n = 0; n < 300 && !Quiet(); n++) await Task.Delay(100);
                // Walk west: Largo stops Guybrush and the dialogue's choices replace the verbs.
                await Click(10, 66, "click in the room to the west");
                for (var n = 0; n < 600 && !(session.PlayVerbArea().Items.Any(v => v.VerbId == 0x78) && session.UserInputOn && session.Messages.OnScreen.Count == 0); n++) await Task.Delay(100);
                await Task.Delay(500);
                Save("play-05-dialogue.png");
                report.Add($"dialogue: input script {session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.VerbScript)}, choices {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.VerbId is >= 0x78 and <= 0x80).Select(v => $"0x{v.VerbId:X2}"))}");
                await Verb(0x78, "click on the first choice");
                for (var n = 0; n < 200 && !session.Messages.OnScreen.Any(); n++) await Task.Delay(100);
                report.Add($"after the choice: messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
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

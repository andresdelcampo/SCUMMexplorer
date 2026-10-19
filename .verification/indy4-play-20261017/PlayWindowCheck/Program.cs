using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: Indy4PlayWindowCheck <Indy 4 folder (ATLANTIS.cue, or ATLANTIS.000/.001 for the Spanish talkie and floppy folders, S9 18 October 2026)> <output folder>
// S6 Indy4 (17 October 2026), copied from .verification/monkeycd-play-20261017/PlayWindowCheck: opens the real MainWindow on Fate of Atlantis (talkie CD) with a
// private profile (SCUMM_EXPLORER_PROFILE_ROOT in the output folder), selects Room 4 (Barnett College, where the boot ends) and lets the window's play timer run.
// Esc through the window's key handler skips the playable credits; in New York (Room 10) "Look at" + the marquee, then "Talk to" + the ticket taker and the
// first dialogue line (verb 0x78). Writes captures, the speech player's log (which spoken lines the window started) and play-check.txt.
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
            var speech = (TalkieSpeechPlayer)Field("_playSpeech")!;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                await Task.Delay(2500);
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 4 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 900 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 4");
                var image = (Image)window.FindName("PlayScreenImage");
                // S9 (18 October 2026): CP437 accents kept; an escape (0xFF/0xFE) and its code draw nothing, codes 9, 10, 12, 13 and 14 skip two operand bytes.
                string Glyphs(int id)
                {
                    var glyphs = session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == id));
                    var shown = new List<byte>();
                    for (var i = 0; i < glyphs.Count; i++)
                    {
                        if (glyphs[i] is 0xFF or 0xFE) { if (++i < glyphs.Count && glyphs[i] is 9 or 10 or 12 or 13 or 14) i += 2; continue; }
                        if (glyphs[i] is >= 0x20 and not 0x7F) shown.Add(glyphs[i]);
                    }
                    return ScummTextDecoder.DecodeGlyphs(shown).Trim();
                }
                string Sentence() => Glyphs(0x64);
                report.Add($"Play started: room {session.CurrentRoomId}, image visible {image.Visibility}, cursor {(image.Cursor == Cursors.None ? "system pointer hidden" : "system pointer")}, " +
                           $"Normal-mode note \"{((TextBlock)window.FindName("ObjectsNote")).Text}\", tooltip {(image.ToolTip is null ? "none" : "shown")}; speech player follows it: {speech.Follows(session.Messages)}");
                session.PointAtScreen((80, 92));
                await Task.Delay(800);
                report.Add($"prologue: input script {session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.VerbScript)}, messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                Save("play-01-prologue.png");
                var source = PresentationSource.FromVisual(window) ?? throw new Exception("no presentation source");
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent };
                var taken = (bool)Call("PlayKeyDown", escape)!;
                report.Add($"Esc through the window's key handler: taken {taken}; trace: {session.SessionTrace.LastOrDefault(l => l.StartsWith("abortCutscene", StringComparison.Ordinal))}");
                for (var n = 0; n < 1200 && !(session.CurrentRoomId == 10 && session.UserInputOn && !session.InCutscene && session.Verbs.Any(v => v.VerbId == 3 && v.Mode == 1)); n++) await Task.Delay(100);
                report.Add($"Room {session.CurrentRoomId} after {Field("_playFrame")} window frames; verbs drawn: {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.Width > 0 && v.IsText).Select(v => $"0x{v.VerbId:X2} \"{Glyphs(v.VerbId)}\""))}");
                async Task Click(int x, int y, string what)
                {
                    session.PointAtScreen((x, y));
                    await Task.Delay(150);
                    var input = session.ClickScreen(x, y, out var refused);
                    report.Add($"{what}: {(input is null ? "refused: " + refused : $"runInputScript({input.Area}, {input.Value}, {input.Mode}) {input.Description}")}");
                    await Task.Delay(250);
                }
                var look = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x09);
                await Click(look.X + 2, look.Y + 2, "click on Look at");
                for (var n = 0; n < 40 && Sentence() == Glyphs(0x09); n++) { session.PointAtScreen((116 - session.ViewLeft, 16 + 0)); await Task.Delay(100); }
                report.Add($"pointer on the marquee: sentence line \"{Sentence()}\"");
                Save("play-02-sentence.png");
                await Click(116 - session.ViewLeft, 16, "click on the marquee");
                for (var n = 0; n < 200 && !session.Messages.OnScreen.Any(m => m.Actor == 1); n++) await Task.Delay(100);
                report.Add($"messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                for (var n = 0; n < 300 && !(session.UserInputOn && !session.InCutscene && session.Messages.Talk is null); n++) await Task.Delay(100);
                var talk = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x0C);
                await Click(talk.X + 2, talk.Y + 2, "click on Talk to");
                for (var n = 0; n < 40 && Sentence() == Glyphs(0x0C); n++) { session.PointAtScreen((116 - session.ViewLeft, 48)); await Task.Delay(100); }
                await Click(116 - session.ViewLeft, 48, "click on the ticket taker");
                for (var n = 0; n < 300 && !(session.UserInputOn && session.Verbs.Any(v => v.VerbId == 0x78 && v.Mode == 1)); n++) await Task.Delay(100);
                report.Add($"dialogue lines: {string.Join(", ", session.PlayVerbArea().Items.Where(v => v.VerbId is >= 0x78 and <= 0x80 && v.Width > 0).Select(v => $"0x{v.VerbId:X2} \"{Glyphs(v.VerbId)}\" ({v.X},{v.Y})"))}");
                Save("play-03-dialogue.png");
                var line = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x78);
                await Click(line.X + 2, line.Y + 2, "click on the first line");
                for (var n = 0; n < 100 && !session.Messages.OnScreen.Any(m => m.Actor == 1); n++) await Task.Delay(50);
                await Task.Delay(200);
                report.Add($"messages [{string.Join(" | ", session.Messages.OnScreen.Select(m => m.Text))}]");
                Save("play-04-said.png");
                for (var n = 0; n < 200 && !(session.UserInputOn && session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.VerbScript) == 4); n++) await Task.Delay(100);
                report.Add($"frames played by the window: {Field("_playFrame")}; spoken lines the session started: {session.Messages.SpeechEvents.Count(e => !e.IsStop)}");
                report.Add("-- speech player log");
                foreach (var entry in speech.Log) report.Add("   " + entry);
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

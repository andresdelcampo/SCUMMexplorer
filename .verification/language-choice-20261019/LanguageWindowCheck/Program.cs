using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: LanguageWindowCheck <Monkey Island CD game folder> <LOOM folder> <output folder>
// R-3.16 (19 October 2026): opens the real MainWindow on The Secret of Monkey Island CD with a private profile (SCUMM_EXPLORER_PROFILE_ROOT) and room cache
// (SCUMM_ROOM_CACHE_DIR) in the output folder. The language list under the game's name is read, Spanish is chosen in it (SelectedIndex, as a click does, so
// the window's own SelectionChanged reopens the game), Room 38 is selected through the tree and the window's play timer runs the game to the dock; the verbs
// drawn and the sentence line with "Look at" on the poster are read. Then LOOM is opened (no list) and the CD again: the remembered Spanish comes back.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[2];
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
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
            var failed = false;
            void Check(bool ok, string what) { report.Add($"{(ok ? "PASS" : "FAIL")} {what}"); failed |= !ok; }
            try
            {
                var combo = (ComboBox)window.FindName("LanguageCombo");
                string Items() => string.Join(", ", combo.Items.OfType<ComboBoxItem>().Select(i => $"{i.Content}"));
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                Check(combo.Visibility == Visibility.Visible && combo.Items.Count == 5, $"Monkey Island CD opened: language list {combo.Visibility}, items [{Items()}], selected \"{(combo.SelectedItem as ComboBoxItem)?.Content}\", game language {game.Language}");
                await Task.Delay(1500);
                Save("language-01-default.png");
                combo.SelectedIndex = combo.Items.OfType<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag == "SPANISH");
                game = (GameLibrary)Field("_game")!;
                var remembered = File.ReadAllText(Path.Combine(output, "profile", "game-languages.txt")).Trim();
                Check(game.Language == "SPANISH" && (combo.SelectedItem as ComboBoxItem)?.Content as string == "Spanish", $"Spanish chosen in the list: game reopened in {game.Language}, list shows \"{(combo.SelectedItem as ComboBoxItem)?.Content}\"; profile game-languages.txt: \"{remembered}\"");
                var tree = (TreeView)window.FindName("ResourceTree");
                await Task.Delay(2500);
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => i.Tag is GameResource { Kind: ResourceKind.Room, ResourceId: 38 });
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 900 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 38");
                Check((string?)Field("_playFolder") == game.Key, $"play runs the chosen data: play session key \"{Field("_playFolder")}\"");
                for (var n = 0; n < 2400 && !(session.CurrentRoomId == 33 && session.UserInputOn && !session.InCutscene && !session.EgoMoving); n++) await Task.Delay(100);
                var verbs = session.PlayVerbArea().Items.Where(v => v.Width > 0).Select(v => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(x => x.Slot == v.Slot))).Trim()).ToList();
                Check(verbs.Contains("Mirar") && verbs.Contains("Coger") && !verbs.Contains("Look at"), $"Room {session.CurrentRoomId}: verbs drawn [{string.Join(", ", verbs)}]");
                string Sentence() => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim();
                var look = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x08);
                session.PointAtScreen((look.X + 2, look.Y + 2));
                await Task.Delay(150);
                session.ClickScreen(look.X + 2, look.Y + 2, out _);
                for (var n = 0; n < 40 && !Sentence().EndsWith("cartel", StringComparison.Ordinal); n++)
                {
                    session.PointAtScreen((268 - session.ViewLeft, 104));
                    await Task.Delay(100);
                }
                Check(Sentence() == "Mirar cartel", $"pointer on the poster with verb 0x08: sentence line \"{Sentence()}\"");
                Save("language-02-spanish-play.png");
                Call("OpenGameFolder", args[1]);
                Check(combo.Visibility == Visibility.Collapsed, $"LOOM opened: language list {combo.Visibility} (one language)");
                Call("OpenGameFolder", args[0]);
                game = (GameLibrary)Field("_game")!;
                Check(game.Language == "SPANISH" && combo.Visibility == Visibility.Visible, $"Monkey Island CD opened again: remembered language {game.Language}, list shows \"{(combo.SelectedItem as ComboBoxItem)?.Content}\"");
                await Task.Delay(1500);
                Save("language-03-reopened.png");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failed = true; }
            finally
            {
                if (failed) Environment.ExitCode = 1;
                File.WriteAllLines(Path.Combine(output, "language-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: MonkeyCdEditionsWindowCheck <Monkey Island CD game folder> <output folder>
// S9 MonkeyCd FRENCH, GERMAN, ITALIAN (19 October 2026), after .verification/language-choice-20261019/LanguageWindowCheck: the real MainWindow with a private
// profile (SCUMM_EXPLORER_PROFILE_ROOT) and room cache in the output folder. For each language the list under the game's name is set (SelectedIndex, as a
// click, so the window's own SelectionChanged reopens the game), Room 38 is selected through the tree and the window's play timer runs the game to the dock;
// the verbs drawn and the sentence line with the copy's Look at verb (0x08) on the poster are read and the window is captured. Nothing edits session state
// but the pointer and the one verb click.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly (string Folder, string Label, string[] Verbs, string Poster)[] Copies =
    [
        ("FRENCH", "French", ["Donne", "Prends", "Regarde", "Parle"], "Regarde affiche"),
        ("GERMAN", "German", ["Gebe", "Nimm", "Schau", "Öffne", "Schließe"], "Schau an Plakat"),
        ("ITALIAN", "Italian", ["Dai", "Prendi", "Esamina", "Parla"], "Esamina il manifesto"),
    ];

    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
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
                Check(combo.Visibility == Visibility.Visible && combo.Items.Count == 5, $"Monkey Island CD opened: items [{Items()}]");
                await Task.Delay(1500);
                var index = 0;
                foreach (var copy in Copies)
                {
                    index++;
                    combo.SelectedIndex = combo.Items.OfType<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag == copy.Folder);
                    var game = (GameLibrary)Field("_game")!;
                    Check(game.Language == copy.Folder && (combo.SelectedItem as ComboBoxItem)?.Content as string == copy.Label,
                        $"{copy.Label} chosen: game reopened in {game.Language}, list shows \"{(combo.SelectedItem as ComboBoxItem)?.Content}\"");
                    var tree = (TreeView)window.FindName("ResourceTree");
                    await Task.Delay(2500);
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => i.Tag is GameResource { Kind: ResourceKind.Room, ResourceId: 38 });
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 900 && !(Field("_play") is not null && (string?)Field("_playFolder") == game.Key); n++) await Task.Delay(100);
                    var session = (ScummGameSession?)Field("_play") ?? throw new Exception($"{copy.Folder}: play did not start in Room 38");
                    Check((string?)Field("_playFolder") == game.Key, $"{copy.Folder}: play session key \"{Field("_playFolder")}\"");
                    for (var n = 0; n < 2400 && !(session.CurrentRoomId == 33 && session.UserInputOn && !session.InCutscene && !session.EgoMoving); n++) await Task.Delay(100);
                    var verbs = session.PlayVerbArea().Items.Where(v => v.Width > 0).Select(v => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(x => x.Slot == v.Slot))).Trim()).ToList();
                    Check(session.CurrentRoomId == 33 && copy.Verbs.All(verbs.Contains) && !verbs.Contains("Look at"),
                        $"{copy.Folder} Room {session.CurrentRoomId}: verbs drawn [{string.Join(", ", verbs)}]");
                    string Sentence() => ScummTextDecoder.DecodeGlyphs(session.VerbGlyphs(session.Verbs.Single(v => v.VerbId == 0x64))).Trim();
                    var look = session.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x08);
                    session.PointAtScreen((look.X + 2, look.Y + 2));
                    await Task.Delay(150);
                    session.ClickScreen(look.X + 2, look.Y + 2, out _);
                    for (var n = 0; n < 40 && Sentence() != copy.Poster; n++)
                    {
                        session.PointAtScreen((268 - session.ViewLeft, 104));
                        await Task.Delay(100);
                    }
                    Check(Sentence() == copy.Poster, $"{copy.Folder}: pointer on the poster with verb 0x08: sentence line \"{Sentence()}\"");
                    Save($"edition-0{index}-{copy.Folder.ToLowerInvariant()}-play.png");
                }
                var remembered = File.ReadAllText(Path.Combine(output, "profile", "game-languages.txt")).Trim();
                Check(remembered.EndsWith("ITALIAN", StringComparison.Ordinal), $"profile game-languages.txt: \"{remembered}\"");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failed = true; }
            finally
            {
                if (failed) Environment.ExitCode = 1;
                File.WriteAllLines(Path.Combine(output, "editions-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

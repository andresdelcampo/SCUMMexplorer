// 26 October 2026: opens each game through the real MainWindow.OpenGameFolder and reports what the per-game seams hand back (cutscene picker, Game Texts
// cutscenes, replay catalog), so the same run on the commit before the IGameModule change and on the current tree can be compared line by line.
// Usage: GameModuleCheck <games root> <private output> [fixture...]
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Fixtures = ["loom ega mt32 en", "loom ega es", "loom demo short ega en", "loom vga en", "indy3 ega en", "monkey1 ega en",
        "mi2 talkie en", "mmansionE ega es", "zak ega en"];

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var fixtures = args.Length > 2 ? args[2..] : Fixtures;
        var report = new List<string>();
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var fixture in fixtures)
                {
                    var folder = Path.Combine(args[0], fixture);
                    if (!Directory.Exists(folder)) { report.Add($"{fixture}: missing"); continue; }
                    typeof(MainWindow).GetMethod("OpenGameFolder", Private)!.Invoke(window, [folder]);
                    for (var n = 0; n < 400; n++) { await Task.Delay(25); if (Field("_game") is not null) break; }
                    await Task.Delay(3000); // the background derivations start once the room cards have their artwork
                    var game = (GameLibrary)Field("_game")!;
                    var controls = Prop("CurrentRoomControls")!;
                    var global = controls.GetType().GetMethod("GlobalCutscenes")!.Invoke(controls, [false])!;
                    var entries = ((System.Collections.IEnumerable)global.GetType().GetProperty("Entries")!.GetValue(global)!).Cast<object>().ToList();
                    var titles = entries.Select(e => (string)e.GetType().GetProperty("Title")!.GetValue(e)!).ToList();
                    var gallery = (System.Collections.IDictionary)typeof(MainWindow).GetMethod("LoomGalleryCutscenes", Private)?.Invoke(window, null)!
                        ?? new Dictionary<int, object>();
                    var catalog = Field("_replayCatalog");
                    var catalogReplays = catalog is null ? -1 : ((Array)catalog.GetType().GetProperty("Replays")!.GetValue(catalog)!).Length;
                    var galleryViaWindow = typeof(MainWindow).GetMethod("GalleryCutscenes", Private) is { } old ? (System.Collections.IDictionary)old.Invoke(window, null)! : gallery;
                    string Hash(string t) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t)))[..12];
                    string Page(GameResource? r, bool script)
                    {
                        if (r is null) return "none";
                        typeof(MainWindow).GetMethod("SelectResourceInTree", Private)!.Invoke(window, [r]);
                        var title = ((System.Windows.Controls.TextBlock)window.FindName("PageTitle")).Text;
                        var sub = ((System.Windows.Controls.TextBlock)window.FindName("PageSubtitle")).Text;
                        var text = script ? ((System.Windows.Controls.TextBox)window.FindName("ScriptOutput")).Text : "";
                        return $"{title} | {sub} | {Hash(text)}/{text.Length}";
                    }
                    var room = game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Room);
                    var script = game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Script && r.ScriptRoom is null);
                    var roomPage = Page(room, false);
                    var scriptPage = Page(script, true);
                    await Task.Delay(300);
                    var usedBy = string.Join(";", ((System.Windows.Controls.Panel)window.FindName("ScriptUsedByList")).Children.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text));
                    var session = Field("_session");
                    var sceneSound = Field("_sceneSound")!;
                    bool Follows(object? owner) => owner is not null && session is not null && (bool)owner.GetType().GetMethod("Follows")!.Invoke(owner, [session])!;
                    var soundBtn = ((System.Windows.UIElement)window.FindName("RoomSoundsButton")).Visibility;
                    var voices = ((System.Windows.Controls.ComboBox)window.FindName("CdSoundCombo")).SelectedIndex;
                    var scriptNote = ((System.Windows.Controls.TextBlock)window.FindName("ScriptNote")).Text;
                    report.Add($"{fixture}: open charset={Field("_charset") is not null} cdData={Field("_cdData") is not null} states={((System.Collections.IDictionary)Field("_indexedStates")!).Count} snapshot={Field("_indexedObjectSnapshot") is not null} scriptNote={Hash(scriptNote)}/{scriptNote.Length}");
                    var statusBtn = (System.Windows.Controls.Button)window.FindName("GameStatusButton");
                    report.Add($"{fixture}: status button={statusBtn.Visibility}/{statusBtn.IsEnabled} tip={statusBtn.ToolTip}");
                    report.Add($"{fixture}: sound session={session is not null} scene={Follows(sceneSound)} voice={Follows(Field("_cdVoice"))} soundsBtn={soundBtn} voicesCombo={voices}");
                    report.Add($"{fixture}: room=[{roomPage}] script=[{scriptPage}] usedBy={Hash(usedBy)}/{usedBy.Length}");
                    report.Add($"{fixture}: family={GameSupport.FamilyOf(game)} picker={entries.Count} first=[{string.Join("; ", titles.Take(3))}] last=[{titles.LastOrDefault()}] gallery={galleryViaWindow.Count} replays={Math.Max(catalogReplays, 0)}");
                }
                report.Add("DONE");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            File.WriteAllLines(Path.Combine(output, "report.txt"), report);
            window.Close(); app.Shutdown();
        };
        app.Run(window);
    }
}

using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, ShowActivated = false, Left = -2000 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        GameLibrary Game() => (GameLibrary)Field("_game")!;
        var report = new List<string>();
        void Check(bool ok, string label) { report.Add((ok ? "PASS " : "FAIL ") + label); if (!ok) throw new InvalidDataException(label); }
        async Task WaitFor(Func<bool> condition, string label)
        {
            for (var i = 0; i < 800 && !condition(); i++) await Task.Delay(25);
            Check(condition(), label);
        }
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                Find<ToggleButton>("SessionModeToggle").IsChecked = true;
                typeof(MainWindow).GetField("_playInstantly", flags)!.SetValue(window, true);
                Call("SelectResourceInTree", Game().Rooms.Single(r => r.ResourceId == 46));
                await WaitFor(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == 46 && Session().Snapshot.RoomId == 46,
                    "fresh room-card selection finishes in Room 46");
                Check(Session().StoryBit("bit[0xD9C]") == true, "the App runs the real dream prerequisite");
                Check(Session().SessionTrace.Any(t => t.Contains("endCutscene at Script 87 +0x0984")), "preparation reaches the exact source end");
                Check(Session().Snapshot.Uncertainties.Any(t => t.StartsWith("Catalog preparation (explicit):")), "preparation is disclosed in session assumptions");
                var data = ScummDataSources.For(Game());
                var hole = data.ReadRoom(46).Objects.Single(o => o.Id == 685);
                Call("HandleRoomPoint", hole.X + hole.Width / 2, hole.Y + hole.Height / 2, true);
                await WaitFor(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == 34 && !Session().InCutscene,
                    "actual App hole click returns to Room 34");
                Check(Session().Actors.Single(a => a.Number == 3).Look.Costume == 120, "return selects ghost costume 120");
                Check(Field("_lastDoorwayFallback") is null, "return uses no static doorway fallback");
                Capture("01-return-ghost");
                var portal = data.ReadRoom(34).Objects.Single(o => o.Id == 560);
                Call("HandleRoomPoint", portal.X + portal.Width / 2, portal.Y + portal.Height / 2, true);
                await WaitFor(() => Session().SessionTrace.Any(t => t.StartsWith("endCutscene at Script 98 +0x0605")) && !Session().InCutscene,
                    "actual portal click completes the ghost conversation");
                Check(Session().Actors.Single(a => a.Number == 3).Look.Costume == 120, "conversation retains the ghost costume");
                Check(Find<Image>("RoomActorOverlay").Source is not null, "the real WPF actor layer is rendered");
                Capture("02-conversation-ghost");
                // A fresh early visit must still have sleeping Rusty and a closed portal.
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                Call("SelectResourceInTree", Game().Rooms.Single(r => r.ResourceId == 34));
                await WaitFor(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == 34 && Session().Snapshot.RoomId == 34,
                    "fresh early Room 34 is available");
                Check(Session().Actors.Single(a => a.Number == 3).Look.Costume == 112, "early Room 34 keeps sleeping Rusty");
                Check(Session().Snapshot.IsUntouchable(560), "early closed portal remains untouchable");
                Call("HandleRoomPoint", portal.X + portal.Width / 2, portal.Y + portal.Height / 2, false);
                Check(Session().Snapshot.RoomId == 34 && Field("_lastDoorwayFallback") is null, "early closed portal still refuses traversal");
                Capture("03-early-closed-portal");
                var catalogType = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomReplayCatalog")!;
                var startsProperty = catalogType.GetProperty("CatalogStarts")!;
                var spanish = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(args[0], "..", "ALTERNATIVE LOOMS", "loom ega mt32 es")));
                var otherCatalog = catalogType.GetMethod("Load")!.Invoke(null, [spanish])!;
                Check(((LoomCatalogStartRecipe[])startsProperty.GetValue(otherCatalog)!).Length == 0,
                    "English recipe is withheld from a different source fingerprint");
                // Negative control: replace only harness recipe metadata, never the game state or source files.
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                var starts = (LoomCatalogStartRecipe[])startsProperty.GetValue(Field("_replayCatalog"))!;
                var recipe = starts.Single();
                starts[0] = recipe with { Stages = [new("var[0x0D1]", 1,
                    [new("visit", 34), new("verb", 562, 0x44),
                     new("scene", Frames: 40, EndBoundary: "absent source end", ExpectedRoom: 34)]), ..recipe.Stages.Skip(1)] };
                Call("SelectResourceInTree", Game().Rooms.Single(r => r.ResourceId == 46));
                await WaitFor(() => Find<TextBlock>("PreviewTitle").Text == "Could not prepare this scene", "failed preparation is surfaced by the App");
                Check(Session().Snapshot.RoomId == 34, "failed preparation cancels the target Room 46 visit");
                Check(Session().StoryBit("bit[0xD9C]") != true, "failure does not manufacture the ghost flag");
                Capture("04-preparation-failure");
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); app.Shutdown(report.Any(t => t.StartsWith("FAIL ")) ? 1 : 0); }
        };
        app.Run(window);
    }
}

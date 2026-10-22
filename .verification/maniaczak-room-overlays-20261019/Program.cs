using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        var report = new List<string>(); var watch = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{watch.Elapsed.TotalSeconds:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "result.txt"), report); }
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethods(Private).Single(method => method.Name == name && method.GetParameters().Length == values.Length).Invoke(window, values);
        T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        ScummGameSession Play() => (ScummGameSession)Field("_play")!;
        void StopClock() => ((DispatcherTimer?)Field("_playTimer"))?.Stop();
        void Frames(int frames) { for (var i = 0; i < frames; i++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
        void Until(Func<bool> condition, string message, int limit = 2500)
        {
            for (var i = 0; i < limit && !condition(); i++) Frames(1);
            Require(condition(), message + "; " + string.Join(" | ", Play().SessionTrace.TakeLast(8)));
        }
        void Click(int x, int y) { Require(Play().ClickScreen(x, y, out var why) is not null, why ?? "Native click refused"); Frames(1); }
        void Key(char key) { Require(Play().PressPlayKey(key, out var why) is not null, why ?? "Native key refused"); Frames(2); }
        async Task Wait(Func<bool> condition, string message, int seconds = 90)
        {
            var timer = Stopwatch.StartNew(); while (!condition() && timer.Elapsed.TotalSeconds < seconds) await Task.Delay(20);
            Require(condition(), "Timeout: " + message);
        }
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        int Children() => ((Canvas)Field("_playOverlayCanvas")!).Children.Count;
        void Refresh() { Call("RefreshPlayOverlays"); window.UpdateLayout(); Call("RefreshPlayOverlays"); }
        string State() => Play().SessionTrace.Count + ":" + string.Join(";", Play().Snapshot.ObjectStates.OrderBy(pair => pair.Key)) + ":" + string.Join(";", Play().Actors);
        void Always(string option) { Control<RadioButton>(option + "Always").IsChecked = true; Refresh(); }
        void Never(string option) { Control<RadioButton>(option + "Never").IsChecked = true; Refresh(); }
        void CheckSelector(int roomId)
        {
            window.UpdateLayout();
            Require(((GameResource?)Field("_selectedResource"))?.ResourceId == roomId, "Native room transition did not update selector resource");
            var card = Control<WrapPanel>("RoomCardList").Children.OfType<Button>().Single(item => item.Tag is GameResource resource && resource.ResourceId == roomId);
            var scroll = Control<ScrollViewer>("RoomCardScroll");
            var bounds = card.TransformToAncestor(scroll).TransformBounds(new Rect(card.RenderSize));
            Require(bounds.IntersectsWith(new Rect(scroll.RenderSize)), "Native selected card was not revealed in room selector");
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
                Call("OpenGameFolder", args[0]); Control<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
                if (Field("_roomCardLoadTask") is Task cards) await cards;
                var game = (GameLibrary)Field("_game")!; var family = GameSupport.FamilyOf(game);
                Log($"Opened {family}; no autoplay expected; starting explicit intro");
                Require(Field("_playTimer") is null, "Game load autoplayed");
                Control<Button>("GameIntroButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(() => Field("_playTimer") is not null, "explicit native intro clock"); StopClock();
                var maniac = family is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;
                if (maniac)
                {
                    foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Click(x, y + Play().MainScreen.Top); Frames(100); }
                    Log("Delivered the three native party-selection clicks");
                    Until(() => Play().CurrentRoomId == 44 && Play().CutsceneSkippable, "Party choice did not reach native outdoor intro");
                    Play().PressKey(27); Until(() => Play().PlayerHasControl, "Native intro Skip did not reach control");
                    Click(8, 120 + Play().MainScreen.Top); Until(() => Play().CurrentRoomId == 1, "Native walk did not reach Room01"); Frames(120);
                    var count = family == GameFamily.ManiacClassic ? 4 : 2;
                    for (var i = 0; i < count; i++) { Click(8, 120 + Play().MainScreen.Top); Frames(200); }
                }
                else { Until(() => Play().CurrentRoomId == 1 && Play().PlayerHasControl, "Native Zak intro did not reach Bedroom", 6000); Frames(150); }
                StopClock();
                CheckSelector(1);
                Log($"{family}: actual native intro/party inputs and Room01; native selector follows; camera={Play().ViewLeft}");
                var transform = typeof(MainWindow).GetMethod("PlayPointAt", Static)!;
                foreach (var (position, width, height, expected) in new[]
                {
                    (new Point(320, 200), 640d, 400d, ((int X, int Y)?)(160, 100)),
                    (new Point(100, 20), 640d, 600d, ((int X, int Y)?)null),
                    (new Point(320, 300), 640d, 600d, ((int X, int Y)?)(160, 100)),
                    (new Point(10, 100), 800d, 400d, ((int X, int Y)?)null),
                    (new Point(400, 200), 800d, 400d, ((int X, int Y)?)(160, 100))
                }) Require(Equals(transform.Invoke(null, [position, width, height]), expected), "Uniform native input mapping failed");
                var before = State();
                Log("Native control reached; checking overlay options");
                Never("Objects"); Never("Doorways"); Require(Children() == 0, "Never leaves overlays visible");
                Always("Objects"); var objectCount = Children(); Require(objectCount > 0, "Always hotspots did not draw over native screen");
                Require(((Canvas)Field("_playOverlayCanvas")!).IsHitTestVisible == false, "Overlay intercepts native clicks");
                Capture("01-normal-hotspots");
                var scene = (ScummRoomScene)Field("_playOverlayScene")!; var targets = (Dictionary<int, int[]>)Field("_playDoorwayTargets")!;
                var objects = ScummRoomOverlays.Objects(scene, targets); var selected = objects.First(obj => !obj.IsDoorway && obj.X + obj.Width > Play().ViewLeft && obj.X < Play().ViewLeft + 320);
                Control<RadioButton>("ObjectsHover").IsChecked = true; Call("UpdatePlayOverlayHover", (object?)null); Refresh(); Require(Children() == 0, "Hover draws with no pointer");
                Call("UpdatePlayOverlayHover", (object)(selected.X + selected.Width / 2 - Play().ViewLeft, selected.Y + selected.Height / 2 + Play().MainScreen.Top));
                Refresh(); Require(Children() > 0 && Children() <= objectCount, "Hover did not track native object geometry");
                Never("Objects"); Always("Doorways"); Require(Children() > 0, "Doorway overlay missing for visible source-backed exit");
                Capture("02-normal-doorways");
                var doorwayCount = Children();
                var doorway = objects.First(obj => obj.IsDoorway && obj.X + obj.Width > Play().ViewLeft && obj.X < Play().ViewLeft + 320);
                Control<RadioButton>("DoorwaysHover").IsChecked = true; Call("UpdatePlayOverlayHover", (object?)null); Refresh(); Require(Children() == 0, "Doorway hover draws with no pointer");
                Call("UpdatePlayOverlayHover", (object)(doorway.X + doorway.Width / 2 - Play().ViewLeft, doorway.Y + doorway.Height / 2 + Play().MainScreen.Top));
                Refresh(); Require(Children() > 0 && Children() <= doorwayCount, "Doorway hover did not track its separate geometry");
                Never("Doorways");
                Control<CheckBox>("AdvancedModeToggle").IsChecked = true;
                Control<CheckBox>("AdvancedModeToggle").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Refresh();
                Require(Control<CheckBox>("RoomMaskToggle").IsEnabled && Control<CheckBox>("WalkBoxToggle").IsEnabled, "Native depth/walkbox data unavailable");
                Control<CheckBox>("RoomMaskToggle").IsChecked = true; Refresh(); Require(Children() == 1, "Native depth tint not over visible play screen");
                var depth = ((Canvas)Field("_playOverlayCanvas")!).Children.OfType<Image>().Single().Source as BitmapSource;
                var depthPixels = new uint[320 * 200]; depth!.CopyPixels(depthPixels, 320 * 4, 0); Require(depthPixels.Any(pixel => pixel != 0), "Depth tint is empty");
                Control<CheckBox>("WalkBoxToggle").IsChecked = true; Refresh();
                Require(((Canvas)Field("_playOverlayCanvas")!).Children.OfType<System.Windows.Shapes.Polygon>().Count() == scene.State.WalkBoxes!.Boxes.Count, "Walkbox polygons missing");
                Capture("03-advanced-depth-walkboxes");
                Require(State() == before, "Viewing aids changed native runtime state");
                Log($"All modes pass: hotspots={objectCount}; source doorways={targets.Count}; walkboxes={scene.State.WalkBoxes.Boxes.Count}; native depth tint nonempty; masks and aids leave source state unchanged");
                if (!maniac)
                {
                    Control<CheckBox>("RoomMaskToggle").IsChecked = false; Control<CheckBox>("WalkBoxToggle").IsChecked = false;
                    Control<CheckBox>("AdvancedModeToggle").IsChecked = false;
                    Control<CheckBox>("AdvancedModeToggle").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    Key('w'); Click(307, 81); Click(10, 145); Frames(180); Key('e'); Click(307, 81);
                    Until(() => Play().CurrentRoomId == 2, "Native opened bedroom door failed to enter Room02", 240);
                    CheckSelector(2);
                    Capture("04-native-room02-selector");
                    Until(() => Play().CurrentRoomId == 16 && Play().CutsceneSkippable, "Native doorway entry suppressed its own delayed scene", 1200);
                    CheckSelector(16); Capture("05-native-entry-scene-selector");
                    Log("Native Open/Walk enters Room02 then native delayed Script126 reaches Room16; both selector cards follow and source entry scene is preserved");
                }
                Log("PASS");
            }
            catch (Exception ex) { Environment.ExitCode = 1; Log(ex.ToString()); try { Capture("failed-window"); } catch { } }
            finally
            {
                ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel(); Call("StopPlay"); Call("StopScenePlayback");
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

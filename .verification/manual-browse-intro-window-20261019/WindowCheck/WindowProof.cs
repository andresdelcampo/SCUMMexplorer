using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;

internal static class WindowProof
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string Hash(BitmapSource image)
    {
        var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(bitmap.PixelWidth)); hash.AppendData(BitConverter.GetBytes(bitmap.PixelHeight)); hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static void Run(string[] args)
    {
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var elapsed = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{elapsed.Elapsed.TotalSeconds:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "progress.txt"), report); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, Private)!.SetValue(window, value);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        async Task Wait(Func<bool> condition, string description, int seconds = 90)
        {
            var wait = Stopwatch.StartNew(); while (!condition() && wait.Elapsed.TotalSeconds < seconds) await Task.Delay(20);
            Require(condition(), "Timed out: " + description);
        }
        ScummGameSession Play() => (ScummGameSession?)Field("_play") ?? throw new InvalidOperationException("No native play session");
        void StopClock() => ((DispatcherTimer?)Field("_playTimer"))?.Stop();
        void Frames(int count) { for (var i = 0; i < count; i++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
        void Click(int x, int y)
        {
            var delivered = Play().ClickScreen(x, y, out var reason); Require(delivered is not null, reason ?? "Native click refused"); Frames(1);
        }
        void Until(Func<bool> condition, string description, int maximum = 2500)
        {
            for (var i = 0; i < maximum && !condition(); i++) Frames(1);
            Require(condition(), description + "; " + string.Join(" | ", Play().SessionTrace.TakeLast(8)));
        }
        async Task<GameLibrary> Open(bool warm)
        {
            ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
            Set("_roomCardsMayUseCache", warm); Call("OpenGameFolder", args[1]); Set("_roomCardsMayUseCache", false);
            Control<ComboBox>("SoundModeCombo").SelectedIndex = 0;
            ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
            if (Field("_roomCardLoadTask") is Task automatic) await automatic;
            Require(Field("_playTimer") is null && Field("_playback") is null, "Opening the game autoplayed a scene clock");
            Require(Field("_play") is null, "Opening the game started its interactive intro without a click");
            var game = (GameLibrary)Field("_game")!; Log($"Real OpenGameFolder {GameSupport.FamilyOf(game)}: no intro autoplay"); return game;
        }
        async Task Cards(GameLibrary game, bool warm)
        {
            var ids = args[3].Split(',').Select(int.Parse).ToArray();
            var rooms = ids.Select(id => game.Scenes.Single(r => r.ResourceId == id)).ToArray(); var panel = Control<Panel>("RoomCardList");
            foreach (var button in panel.Children.OfType<Button>().Where(b => !rooms.Contains((GameResource)b.Tag)).ToArray()) panel.Children.Remove(button);
            ((IDictionary)Field("_roomCardStages")!).Clear();
            Set("_roomCardsMayUseCache", warm); var cancellation = new CancellationTokenSource(); Set("_roomCardCancellation", cancellation);
            var generation = (int)Field("_roomCardGeneration")! + 1; Set("_roomCardGeneration", generation);
            var task = (Task)Call("LoadRoomCardThumbnailsAsync", game, rooms, Field("_indexedObjectSnapshot"), Field("_indexedStates"), generation, cancellation.Token)!;
            Set("_roomCardLoadTask", task); Set("_roomCardsMayUseCache", false);
            await Wait(() => task.IsCompleted, "bounded card/native prewarm", 360); await task;
            Require(Field("_roomCardLoadPhase")!.ToString() == "Completed", "Card loader did not complete");
            var cache = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.RoomThumbnailCache")!;
            var saved = (IReadOnlyDictionary<int, BitmapSource>?)cache.GetMethod("TryLoad")!.Invoke(null, [game.FolderPath, ids, game.Language]);
            Require(saved is not null && Field("_roomCardCacheCommitted") is true, "Complete native cache missing");
            foreach (var room in rooms)
            {
                var background = (BitmapSource)typeof(MainWindow).GetMethod("RenderRoomCardBackground", Static)!.Invoke(null, [game, room, CancellationToken.None])!;
                var card = (BitmapSource)((Image)panel.Children.OfType<Button>().Single(b => Equals(b.Tag, room)).Content).Source;
                var loom = game is { Id: GameId.Loom, Version: ScummVersion.V3 };
                Require(loom ? Hash(card) == Hash(saved![room.ResourceId!.Value]) && Hash(card) != Hash(background)
                    : Hash(card) == Hash(background), "Selector did not retain its requested background/gap-fill presentation");
                if (!loom) Require(Hash(saved![room.ResourceId!.Value]) != Hash(background), "Sample has no native actor/object difference to test display-only background policy");
                Log($"{(warm ? "Warm" : "Cold")} Room{room.ResourceId}: selector {(loom ? "gap-filled native" : "exact plain background")}; native cache differs from background={Hash(saved![room.ResourceId!.Value]) != Hash(background)}");
            }
            Capture(warm ? "warm-selector" : "cold-selector");
        }
        async Task<bool> Manual(GameLibrary game, int id, bool allowStatic = false)
        {
            var previous = Field("_play"); Call("SelectResourceInTree", game.Scenes.Single(r => r.ResourceId == id));
            if (Field("_roomOpenTask") is Task opening)
            {
                await Wait(() => opening.IsCompleted, $"manual Room{id} setup task", 180); await opening;
            }
            await Wait(() => (Field("_play") is ScummGameSession session && !ReferenceEquals(previous, session) && session.CurrentRoomId == id)
                || (Field("_catalogPreviewOnly") is true && Field("_currentRoomResource") is GameResource shown && shown.ResourceId == id), $"manual Room{id} preparation");
            if (Field("_catalogPreviewOnly") is true)
            {
                Require(Field("_playTimer") is null && Control<Image>("PlayScreenImage").Visibility == Visibility.Collapsed
                    && Control<FrameworkElement>("RoomDisplayGroup").Visibility == Visibility.Visible && Field("_browsedScene") is not null,
                    "Failed manual preparation did not show the quiet static fallback");
                Capture("manual-room-" + id + "-static-fallback");
                Log($"Manual Room{id}: bounded preparation chose static fallback; old session not clocked; no native input proof claimed");
                Require(Control<Button>("GameIntroButton").Visibility == Visibility.Visible && Control<Button>("GameIntroButton").IsEnabled,
                    "Quiet static fallback left no available explicit native intro restart");
                if (allowStatic) return false;
                throw new InvalidOperationException($"Quiet static fallback verified for Room{id}; selected acceptance route needs a native prepared session and remains unproved");
            }
            StopClock(); Require(!Play().InCutscene, "Manual browsing autoplayed/retained a cutscene");
            Require(!Control<Button>("SkipCutsceneButton").IsEnabled, "Manual settled room retained enabled cutscene skip");
            Capture("manual-room-" + id); Log($"Manual Room{id}: native session replaced; no active cutscene; Skip disabled");
            return true;
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var game = await Open(false); await Cards(game, false);
                game = await Open(true); await Cards(game, true);
                var intro = Control<Button>("GameIntroButton");
                if (game is { Id: GameId.Loom, Version: ScummVersion.V3 })
                {
                    Require(intro.Visibility != Visibility.Visible, "LOOM's Room01 intro gained a duplicate header button");
                    Capture("loom-no-duplicate-intro"); Log("Existing LOOM Room01 intro: no duplicate top-level button");
                }
                else
                {
                    Require(intro.Visibility == Visibility.Visible && intro.IsEnabled && intro.Content?.ToString() == "Play Game Intro", "Explicit top-level intro button missing");
                    intro.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Wait(() => Field("_playTimer") is not null, "explicit native intro clock"); StopClock();
                    Capture("explicit-native-intro"); Log($"Explicit header intro: room{Play().CurrentRoomId}; InCutscene={Play().InCutscene}; Skippable={Play().CutsceneSkippable}");
                    if (GameSupport.FamilyOf(game) == GameFamily.ManiacEnhanced)
                    {
                        Until(() => Play().CurrentRoomId == 45, "Native party menu did not appear");
                        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Click(x, y + Play().MainScreen.Top); Frames(100); }
                        Until(() => Play().CurrentRoomId == 44 && Play().InCutscene, "Party Start did not reach native outdoor intro");
                        Require(Play().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x030]") == 1 && Play().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x031]") == 5, "Native player party choices were not retained");
                        Capture("native-party-outdoor-intro"); await Manual(game, 1, allowStatic: true);
                        Require(Play().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x030]") == 1 && Play().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x031]") == 5, "Manual browse lost the chosen party");
                        Log("ManiacEnhanced: actual party inputs retained through manual intro abort");
                        // An unfinished opening can validly produce a quiet static fallback. Restart explicitly,
                        // answer its real menu again and finish by the game's native override before further input proofs.
                        intro.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await Wait(() => Field("_playTimer") is not null, "explicit fresh native intro restart"); StopClock();
                        Until(() => Play().CurrentRoomId == 45, "Fresh native party menu did not appear");
                        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Click(x, y + Play().MainScreen.Top); Frames(100); }
                        Until(() => Play().CurrentRoomId == 44 && Play().CutsceneSkippable, "Fresh native outdoor intro did not provide its override");
                        Call("UpdatePlaybackControls");
                        Require(Control<Button>("SkipCutsceneButton").IsEnabled, "Real opening override did not enable its Skip control");
                        Control<Button>("SkipCutsceneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Until(() => Play().PlayerHasControl, "Native opening skip did not return player control");
                        Log("Explicit fresh Intro: actual party inputs repeated; native Skip131 returned control before telescope route");
                        await Manual(game, 1);
                        await Manual(game, 28);
                        var use = Play().PlayVerbArea().Items.Single(v => v.VerbId == 11 && v.Mode == 1);
                        Click(use.X + 2, use.Y + 2);
                        var telescope = ScummDataSources.For(game).ReadRoom(28).Objects.Single(o => o.Id == 413);
                        var telescopeX = telescope.X + telescope.Width / 2 - Play().ViewLeft;
                        Require(telescopeX is >= 0 and < 320, "Telescope not within native viewport; no camera shortcut applied");
                        for (var i = 0; i < 2; i++) Click(telescopeX, telescope.Y + telescope.Height / 2 + Play().MainScreen.Top);
                        Until(() => Play().InCutscene, "Native Use telescope did not begin source Script23");
                        Require(!Play().CutsceneSkippable, "Telescope did not exercise the no-override scope");
                        Call("ShowPlayFrame"); Call("UpdatePlaybackControls");
                        Require(!Control<Button>("SkipCutsceneButton").IsEnabled, "Native no-override scene incorrectly offers Skip");
                        Capture("native-telescope-no-override");
                        Log("Native Use413: active no-override cutscene; Skip disabled; source Script23");
                        await Manual(game, 1);
                        Frames(180); Require(!Play().InCutscene && Play().CurrentRoomId == 1, "Retired telescope owner/child resumed after manual browsing");
                        Log("Manual browse aborts a no-override scene and its old continuation does not resume");
                    }
                    else
                    {
                        await Manual(game, 51);
                        Frames(30); Require(!Play().InCutscene, "Manual Room51 auto-played its entry cutscene");
                        Log("Manual Room51: native entry cutscene settled offscreen; no autoplay");
                        await Manual(game, 52);
                        var doorway = ScummDataSources.For(game).ReadRoom(52).Objects.Single(o => o.Id == 621);
                        var x = doorway.X + doorway.Width / 2 - Play().ViewLeft;
                        Require(x is >= 0 and < 320, "Native doorway621 lies outside viewport; no placement shortcut applied");
                        Play().PointAtScreen((x, doorway.Y + doorway.Height / 2 + Play().MainScreen.Top)); Frames(2);
                        Click(x, doorway.Y + doorway.Height / 2 + Play().MainScreen.Top);
                        Until(() => Play().CurrentRoomId == 51, "Native WalkTo621 failed to enter Room51");
                        Require(Play().InCutscene && !Play().CutsceneSkippable, "Actual doorway did not preserve Room51's native no-override entry cutscene");
                        Call("ShowPlayFrame"); Call("UpdatePlaybackControls"); Capture("native-doorway-entry-cutscene");
                        Log("Actual WalkTo doorway52/621→51: native entry cutscene runs, unlike manual selection");
                        await Manual(game, 33);
                    }
                }
                Log("PASS");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; try { Capture("failed-window"); } catch { } }
            finally
            {
                ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel(); Call("StopPlay"); Call("StopScenePlayback");
                File.WriteAllLines(Path.Combine(output, "window-check.txt"), report); window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

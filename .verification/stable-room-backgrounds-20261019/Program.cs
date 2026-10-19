using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;

// policy: headless predicate proof; no App/Window instance.
// batch <fixture> <new private output> <comma-separated room ids>: serial window lease required.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static object? StaticCall(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Static)!.Invoke(null, values);
    private static string Hash(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return $"{converted.PixelWidth}x{converted.PixelHeight}:" + Convert.ToHexString(SHA256.HashData(pixels));
    }
    private static BitmapSource Background(GameLibrary game, GameResource room) =>
        (BitmapSource)StaticCall("RenderRoomCardBackground", game, room, CancellationToken.None)!;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["policy"])
        {
            var count = 0;
            foreach (var id in Enum.GetValues<GameId>())
            foreach (var version in Enum.GetValues<ScummVersion>())
            foreach (var demo in new[] { false, true })
            {
                var game = new GameLibrary("synthetic identity only", "", version, "DOS", [], "", id, demo);
                Require((bool)StaticCall("UsesInitializedRoomCards", game)! == (id == GameId.Loom && version == ScummVersion.V3),
                    $"Incorrect selector policy: {id}/{version}/demo={demo}"); count++;
            }
            Console.WriteLine($"PASS: {count} identity combinations; only Loom/V3 fills native selector details; no Window constructed");
            return;
        }
        Require(args.Length == 4 && args[0] == "batch", "Use policy or batch <fixture> <new private output> <rooms>");
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>();
        void Log(string line) { report.Add(line); File.WriteAllLines(Path.Combine(output, "result.txt"), report); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, Private)!.SetValue(window, value);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        var cache = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.RoomThumbnailCache")!;
        IReadOnlyDictionary<int, BitmapSource>? Cached(GameLibrary game, IReadOnlyList<GameResource> rooms) =>
            (IReadOnlyDictionary<int, BitmapSource>?)cache.GetMethod("TryLoad")!.Invoke(null,
                [game.FolderPath, rooms.Select(r => r.ResourceId!.Value), game.Language]);
        var panel = (Panel)window.FindName("RoomCardList");
        IEnumerable<Button> Cards() => panel.Children.OfType<Button>();
        void Capture(string name)
        {
            window.UpdateLayout();
            var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        string Card(GameResource room) => Cards().Single(b => Equals(b.Tag, room)).Content is Image { Source: BitmapSource image } ? Hash(image) : "placeholder";
        IDictionary Stages() => (IDictionary)Field("_roomCardStages")!;
        bool NativePrepared(GameLibrary game, int room)
        {
            var scenes = typeof(MainWindow).GetField("RoomPreviewScenes", Static)!.GetValue(null)!;
            var arguments = new object?[] { game, null };
            if (!(bool)scenes.GetType().GetMethod("TryGetValue")!.Invoke(scenes, arguments)!) return false;
            var values = (IDictionary)arguments[1]!;
            return values.Contains(room) && (bool)values[room]!.GetType().GetProperty("IsValueCreated")!.GetValue(values[room])!;
        }
        async Task Prepare(GameLibrary game, IReadOnlyList<GameResource> rooms)
        {
            Call("StopPlay"); Set("_play", null); Set("_playFolder", null); Call("StopScenePlayback");
            ((CancellationTokenSource?)Field("_previewCancellation"))?.Cancel();
            Set("_game", game); Set("_glossary", LoomGlossary.Load(game));
            var replay = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomReplayCatalog")!;
            Set("_replayCatalog", replay.GetMethod("Load", Static | BindingFlags.Public)!.Invoke(null, [game]));
            Set("_scriptReader", new ScummScriptReader(ScummProfile.For(game).Dialect));
            Set("_verbReader", new ScummObjectVerbReader(ScummProfile.For(game).Dialect));
            var indexed = game is { Id: GameId.Loom, Version: ScummVersion.V3 }
                ? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")) : null;
            Set("_indexedObjectSnapshot", indexed); Set("_indexedStates", indexed?.States ?? new Dictionary<int, byte>());
            Set("_session", indexed is null ? null : await Task.Run(() => new LoomExplorationSession(game, indexed)));
            Set("_drawBatchesTask", null); Set("_drawBatchesGame", null); Set("_sceneBatches", null);
            ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
            Call("ShowGame", game);
            ((ComboBox)window.FindName("SoundModeCombo")).SelectedIndex = (int)SceneSoundPlayer.SoundMode.None;
            ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
            if (Field("_roomCardLoadTask") is Task automatic) await automatic;
            foreach (var card in Cards().Where(c => !rooms.Contains((GameResource)c.Tag)).ToArray()) panel.Children.Remove(card);
            Stages().Clear(); Set("_roomCardCacheCommitted", false);
            Set("_roomCardBackgroundsReady", new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }
        Task Start(GameLibrary game, IReadOnlyList<GameResource> rooms, bool warm)
        {
            Set("_roomCardsMayUseCache", warm); var cancellation = new CancellationTokenSource(); Set("_roomCardCancellation", cancellation);
            var generation = (int)Field("_roomCardGeneration")! + 1; Set("_roomCardGeneration", generation);
            var task = (Task)Call("LoadRoomCardThumbnailsAsync", game, rooms, Field("_indexedObjectSnapshot"), Field("_indexedStates"), generation, cancellation.Token)!;
            Set("_roomCardLoadTask", task); Set("_roomCardsMayUseCache", false); return task;
        }
        window.ContentRendered += async (_, _) =>
        {
            DispatcherTimer? observer = null;
            try
            {
                var game = new GameFolderScanner().Scan(args[1]);
                var ids = args[3].Split(',').Select(int.Parse).ToArray();
                var fill = (bool)StaticCall("UsesInitializedRoomCards", game)!;
                for (var pass = 0; pass < 2; pass++)
                {
                    if (pass == 1) game = GameFolderScanner.Reopen(game);
                    var rooms = ids.Select(id => game.Rooms.Single(r => r.ResourceId == id)).ToArray();
                    await Prepare(game, rooms);
                    if (ScummProfile.For(game).HasFirstVisitScenes)
                        Require(rooms.All(r => !NativePrepared(game, r.ResourceId!.Value)), "New library unexpectedly reused old native cache");
                    var backgrounds = rooms.ToDictionary(r => r.ResourceId!.Value, r => Hash(Background(game, r)));
                    var observed = 0; Exception? failure = null;
                    observer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(5) };
                    observer.Tick += (_, _) =>
                    {
                        try
                        {
                            if (fill) return;
                            foreach (var room in rooms)
                                if (Card(room) != "placeholder") { Require(Card(room) == backgrounds[room.ResourceId!.Value], "Native/cache image replaced a plain card"); observed++; }
                            Require(Stages().Values.Cast<object>().All(v => v.ToString() == "Background"), "Non-LOOM card advanced to native display stage");
                        }
                        catch (Exception ex) { failure ??= ex; }
                    };
                    observer.Start(); await Start(game, rooms, pass == 1).WaitAsync(TimeSpan.FromMinutes(6)); observer.Stop();
                    if (failure is not null) throw failure;
                    Require(Field("_roomCardLoadPhase")!.ToString() == "Completed" && Field("_roomCardCacheCommitted") is true, "Native batch/cache did not complete");
                    var saved = Cached(game, rooms) ?? throw new InvalidOperationException("Full initialized cache cannot reopen");
                    if (ScummProfile.For(game).HasFirstVisitScenes)
                        Require(rooms.All(r => NativePrepared(game, r.ResourceId!.Value)), "Cold/warm pass skipped native preparation");
                    var nativeDifferences = 0;
                    foreach (var room in rooms)
                    {
                        var id = room.ResourceId!.Value; var expected = fill ? Hash(saved[id]) : backgrounds[id];
                        Require(Card(room) == expected, "Completed selector has the wrong publication policy");
                        if (Hash(saved[id]) != backgrounds[id]) nativeDifferences++;
                        Call("SelectResourceInTree", room); await Task.Delay(100);
                        Require(Card(room) == expected, "Selection replaced canonical selector pixels");
                    }
                    Require(nativeDifferences > 0, "Batch does not distinguish plain cards from cached native artwork");
                    Capture(pass == 0 ? "cold-completed-selected" : "warm-completed-selected");
                    Log($"{(pass == 0 ? "Cold" : "Warm")} {GameSupport.FamilyOf(game)}: {rooms.Length} stable cards; fill={fill}; full initialized cache differs from background in {nativeDifferences}; native preparation complete; {observed} plain-card observations");
                }
                Log("PASS");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; }
            finally { observer?.Stop(); ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel(); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

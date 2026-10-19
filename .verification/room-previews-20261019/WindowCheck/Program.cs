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
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// census <games root> <private output>
// batch <fixture> <private output> <comma-separated room ids> [actions]
// Real MainWindow loader batches, with the complete original GameLibrary retained.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly string[] Fixtures = ["loom ega mt32 en", "loom ega es", "loom demo short ega en", "loom vga en",
        "indy3 ega en", "indy3 vga en", "monkey1 ega en", "monkey1 vga en", "mi2 talkie en", "indy4 vga en",
        "mmansionE ega es", "mmansion ega en", "zakE ega en", "zak ega en"];

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object? CallStatic(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Static)!.Invoke(null, values);
    private static string Hash(BitmapSource source)
    {
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(bitmap.PixelWidth)); hash.AppendData(BitConverter.GetBytes(bitmap.PixelHeight)); hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static BitmapSource Background(GameLibrary game, GameResource room) =>
        (BitmapSource)CallStatic("RenderRoomCardBackground", game, room, CancellationToken.None)!;
    private static string NativeBackground(GameLibrary game, GameResource resource)
    {
        var room = game.V4ArchiveFor(resource) is { } archive ? archive.DecodeRoom(resource.ResourceId!.Value)
            : game.Version == ScummVersion.V1 ? new ScummV1RoomDecoder(game.V1Game!.Value).Decode(File.ReadAllBytes(resource.FilePath))
            : game.Version == ScummVersion.V2 ? new ScummV2RoomDecoder().Decode(File.ReadAllBytes(resource.FilePath))
            : new ScummV3EgaRoomDecoder().Decode(File.ReadAllBytes(resource.FilePath));
        var width = Math.Min(320, room.Width); var bytes = new byte[width * room.Height * 4];
        var indices = room.Pixels.Span;
        for (var y = 0; y < room.Height; y++)
        for (var x = 0; x < width; x++)
        {
            var color = room.Palette[indices[y * room.Width + x]]; var at = (y * width + x) * 4;
            bytes[at] = (byte)color; bytes[at + 1] = (byte)(color >> 8); bytes[at + 2] = (byte)(color >> 16); bytes[at + 3] = (byte)(color >> 24);
        }
        return Hash(BitmapSource.Create(width, room.Height, 96, 96, PixelFormats.Bgra32, null, bytes, width * 4));
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>();
        void Log(string text) { report.Add(text); File.WriteAllLines(Path.Combine(output, "progress.txt"), report); }
        if (args[0] == "census")
        {
            try
            {
                var total = 0;
                foreach (var fixture in Fixtures)
                {
                    var game = new GameFolderScanner().Scan(Path.Combine(args[1], fixture));
                    var count = 0;
                    foreach (var room in game.Scenes)
                    {
                        Require(Hash(Background(game, room)) == NativeBackground(game, room), $"Background decoder mismatch: {fixture}, room{room.ResourceId}"); count++;
                    }
                    total += count; Log($"{GameSupport.FamilyOf(game)}: {count} plain room backgrounds match native decoded pixels/dimensions");
                }
                Log($"PASS: {Fixtures.Length} supplied families; {total} plain backgrounds; no native session initialized");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; }
            File.WriteAllLines(Path.Combine(output, "background-census.txt"), report); return;
        }

        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, Private)!.SetValue(window, value);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        var cache = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.RoomThumbnailCache")!;
        string CacheFile(GameLibrary game) => (string)cache.GetMethod("CacheFile", Static)!.Invoke(null, [game.FolderPath, game.Language])!;
        IReadOnlyDictionary<int, BitmapSource>? Cached(GameLibrary game, IReadOnlyList<GameResource> rooms) =>
            (IReadOnlyDictionary<int, BitmapSource>?)cache.GetMethod("TryLoad")!.Invoke(null, [game.FolderPath, rooms.Select(r => r.ResourceId!.Value), game.Language]);
        void Save(string name, FrameworkElement? target = null)
        {
            target ??= window; target.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth), (int)Math.Ceiling(target.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            DispatcherTimer? watch = null;
            try
            {
                var game = new GameFolderScanner().Scan(args[1]);
                var ids = args[3].Split(',').Select(int.Parse).ToArray();
                var rooms = ids.Select(id => game.Rooms.Single(r => r.ResourceId == id)).ToArray();
                var buttons = (Panel)window.FindName("RoomCardList");
                string Phase() => Field("_roomCardLoadPhase")!.ToString()!;
                IDictionary Stages() => (IDictionary)Field("_roomCardStages")!;
                int InitialCount() => Stages().Values.Cast<object>().Count(s => s.ToString() == "InitialState");
                bool NativePrepared(GameLibrary library, int id)
                {
                    var scenes = typeof(MainWindow).GetField("RoomPreviewScenes", Static)!.GetValue(null)!;
                    var values = new object?[] { library, null };
                    if (!(bool)scenes.GetType().GetMethod("TryGetValue")!.Invoke(scenes, values)!) return false;
                    var dictionary = (IDictionary)values[1]!;
                    return dictionary.Contains(id) && (bool)dictionary[id]!.GetType().GetProperty("IsValueCreated")!.GetValue(dictionary[id])!;
                }
                IEnumerable<Button> Cards() => buttons.Children.OfType<Button>();
                string Card(GameResource resource) => Cards().Single(b => Equals(b.Tag, resource)).Content is Image { Source: BitmapSource bitmap }
                    ? Hash(bitmap) : "placeholder";
                async Task Prepare(GameLibrary library, IReadOnlyList<GameResource> batch)
                {
                    Call("StopPlay"); Set("_play", null); Set("_playFolder", null); Call("StopScenePlayback");
                    ((CancellationTokenSource?)Field("_previewCancellation"))?.Cancel();
                    Set("_game", library); Set("_glossary", LoomGlossary.Load(library));
                    var replayType = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomReplayCatalog")!;
                    Set("_replayCatalog", replayType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, [library]));
                    Set("_scriptReader", new ScummScriptReader(ScummProfile.For(library).Dialect)); Set("_verbReader", new ScummObjectVerbReader(ScummProfile.For(library).Dialect));
                    ScummIndexedObjectSnapshot? indexed = library.Id == GameId.Loom && library.Version == ScummVersion.V3
                        ? new LflObjectStateReader().ReadSnapshot(Path.Combine(library.FolderPath, "00.LFL")) : null;
                    Set("_indexedObjectSnapshot", indexed); Set("_indexedStates", indexed?.States ?? new Dictionary<int, byte>());
                    Set("_session", indexed is null ? null : await Task.Run(() => new LoomExplorationSession(library, indexed)));
                    Set("_drawBatchesTask", null); Set("_drawBatchesGame", null); Set("_sceneBatches", null);
                    ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
                    Call("ShowGame", library);
                    ((ComboBox)window.FindName("SoundModeCombo")).SelectedIndex = (int)SceneSoundPlayer.SoundMode.None;
                    Require(((SceneSoundPlayer)Field("_sceneSound")!).Mode == SceneSoundPlayer.SoundMode.None, "Actual scene sound is not muted");
                    ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
                    if (Field("_roomCardLoadTask") is Task automatic) await automatic;
                    foreach (var button in Cards().Where(b => !batch.Contains((GameResource)b.Tag)).ToArray()) buttons.Children.Remove(button);
                    Stages().Clear(); Set("_roomCardCacheCommitted", false);
                    Require(Cards().Count() == batch.Count, "Scoped card batch lost an original resource");
                }
                Task Start(GameLibrary library, IReadOnlyList<GameResource> batch, bool warm)
                {
                    Set("_roomCardsMayUseCache", warm);
                    var token = new CancellationTokenSource(); Set("_roomCardCancellation", token);
                    var generation = (int)Field("_roomCardGeneration")! + 1; Set("_roomCardGeneration", generation);
                    var task = (Task)Call("LoadRoomCardThumbnailsAsync", library, batch, Field("_indexedObjectSnapshot"), Field("_indexedStates"), generation, token.Token)!;
                    Set("_roomCardLoadTask", task); Set("_roomCardsMayUseCache", false); return task;
                }
                async Task Complete(Task task)
                {
                    var elapsed = Stopwatch.StartNew();
                    while (!task.IsCompleted && elapsed.Elapsed < TimeSpan.FromMinutes(6)) await Task.Delay(25);
                    Require(task.IsCompleted, "Scoped native batch exceeded six minutes"); await task;
                }
                Log($"Starting {GameSupport.FamilyOf(game)} scoped actual-window batch rooms[{string.Join(',', ids)}]; original library has {game.Scenes.Count()} scenes");
                await Prepare(game, rooms);
                Require(!File.Exists(CacheFile(game)), "Cold private packet unexpectedly contains an earlier complete cache");
                var backgrounds = await Task.Run(() => rooms.ToDictionary(r => r.ResourceId!.Value, r => Hash(Background(game, r))));
                var observations = 0; var sawAllBackgrounds = false; var sawInitial = false; var greatestGap = TimeSpan.Zero;
                var time = Stopwatch.StartNew(); var last = time.Elapsed; Exception? observationError = null;
                watch = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(10) };
                watch.Tick += (_, _) =>
                {
                    try
                    {
                        var now = time.Elapsed; if (now - last > greatestGap) greatestGap = now - last; last = now; observations++;
                        if (Phase() == "Backgrounds") Require(InitialCount() == 0, "Native card published before background phase completed");
                        if (Cards().All(c => c.Content is Image) && InitialCount() == 0 && !sawAllBackgrounds)
                        {
                            foreach (var room in rooms) Require(Card(room) == backgrounds[room.ResourceId!.Value], "Phase one painted objects or actors");
                            sawAllBackgrounds = true; Save("all-backgrounds");
                        }
                        if (InitialCount() > 0) { Require(sawAllBackgrounds, "First native publication preceded all plain backgrounds"); sawInitial = true; }
                        if (InitialCount() < rooms.Length)
                        {
                            Require(Field("_roomCardCacheCommitted") is false, "Incomplete cold batch was committed as complete");
                            Require(!File.Exists(CacheFile(game)), "Incomplete cold native pass published a ZIP archive");
                        }
                    }
                    catch (Exception ex) { observationError ??= ex; }
                };
                watch.Start(); var cold = Start(game, rooms, false);
                // Select as soon as the first background is visible, before that card's native state publishes.
                for (var wait = 0; wait < 2400 && Cards().First().Content is not Image; wait++) await Task.Delay(5);
                Require(Cards().First().Content is Image, "First plain background never appeared");
                var clickTime = time.Elapsed;
                Cards().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Log($"Early card click synchronous dispatch: {(time.Elapsed - clickTime).TotalMilliseconds:F0}ms; phase={Phase()}");
                await Complete(cold); watch.Stop();
                if (observationError is not null) throw observationError;
                Require(sawAllBackgrounds && sawInitial && Phase() == "Completed", "Both loader phases did not complete");
                Require(Field("_roomCardCacheCommitted") is true, "Complete cold native batch has no committed cache");
                var initialized = rooms.ToDictionary(r => r.ResourceId!.Value, Card);
                var saved = Cached(game, rooms) ?? throw new InvalidOperationException("Completed cache cannot reopen the exact batch");
                foreach (var room in rooms) Require(Hash(saved[room.ResourceId!.Value]) == initialized[room.ResourceId.Value], "Cache pixels differ from stable initialized card");
                Log($"Cold phase heartbeat diagnostic: {observations} ticks; max gap {greatestGap.TotalMilliseconds:F0}ms; elapsed {time.Elapsed.TotalSeconds:F2}s");
                Require(observations > 0 && greatestGap < TimeSpan.FromSeconds(3), "Dispatcher was unresponsive during two-phase work");
                Save("all-initial-states");
                Log($"Cold {GameSupport.FamilyOf(game)} rooms[{string.Join(',', ids)}]: all backgrounds before native cards; exact plain pixels; stable cache; {observations} heartbeat ticks, max gap{greatestGap.TotalMilliseconds:F0}ms; elapsed{time.Elapsed.TotalSeconds:F2}s");
                foreach (var room in rooms)
                {
                    Call("SelectResourceInTree", room); await Task.Delay(100);
                    typeof(MainWindow).GetMethod("UpdateCurrentRoomCardThumbnail", Private)?.Invoke(window, null);
                    Require(Card(room) == initialized[room.ResourceId!.Value], "Click/current-room composition overwrote stable initialized selector art");
                    var stage = typeof(MainWindow).GetNestedType("RoomCardPreviewStage", BindingFlags.NonPublic)!;
                    Call("SetRoomCardThumbnail", room, Background(game, room), Enum.Parse(stage, "Background"));
                    Require(Card(room) == initialized[room.ResourceId!.Value], "Late plain background downgraded an initialized selector card");
                    Log($"Room{room.ResourceId}: background/native differ={backgrounds[room.ResourceId!.Value] != initialized[room.ResourceId.Value]}; stable after selection and current-room publication");
                }
                if (args.Contains("actions"))
                {
                    var room = rooms.Single(r => r.ResourceId == 1);
                    Call("SelectResourceInTree", room);
                    for (var n = 0; n < 2400 && Field("_playTimer") is null; n++) await Task.Delay(5);
                    var play = (ScummGameSession?)Field("_play") ?? throw new InvalidOperationException("Native Play did not start for transient-art check");
                    ((DispatcherTimer?)Field("_playTimer"))?.Stop();
                    Require(play.CurrentRoomId == 1 && play.PlayerHasControl, "No source-established Room1 control for native drawer action");
                    void Frames(int count) { for (var n = 0; n < count; n++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
                    void Click(int x, int y) { Require(play.ClickScreen(x, y, out var refusal) is not null, refusal ?? "Native click refused"); Frames(5); }
                    var before = play.Snapshot.ObjectStates.GetValueOrDefault(92);
                    var verb = play.PlayVerbArea().Items.Single(v => v.VerbId == 1 && v.Mode == 1);
                    Click(verb.X + 3, verb.Y + 3);
                    var drawer = ScummDataSources.For(game).ReadRoom(1).Objects.Single(o => o.Id == 92);
                    Click(drawer.X + drawer.Width / 2 - play.ViewLeft, drawer.Y + Math.Max(1, drawer.Height / 2) + play.MainScreen.Top);
                    Click(8, play.MainScreen.Bottom + 4); Frames(120);
                    Require(play.Snapshot.ObjectStates.GetValueOrDefault(92) != before, "Native Open did not change the actual drawer state");
                    Require(Card(room) == initialized[1], "Native transient drawer composition replaced the selector's first-visit image");
                    Save("native-drawer-card-stable"); Log("Native Open drawer input changed live Room1 object92 while initialized selector pixels stayed fixed");
                }
                if (game.Id == GameId.Loom && rooms.Any(r => r.ResourceId == 56))
                    Require(initialized[56] != backgrounds[56], "LOOM56 gap-filled face/mouth art vanished from initialized card");
                // Reopen the real copy, with all native resources intact, then use the valid complete cache.
                var reopened = GameFolderScanner.Reopen(game);
                var warmRooms = ids.Select(id => reopened.Rooms.Single(r => r.ResourceId == id)).ToArray();
                await Prepare(reopened, warmRooms);
                if (ScummProfile.For(reopened).HasFirstVisitScenes)
                    Require(warmRooms.All(r => !NativePrepared(reopened, r.ResourceId!.Value)), "Fresh warm reopening already shared the previous library's native cache");
                var warm = Start(reopened, warmRooms, true);
                await Complete(warm);
                Require(Phase() == "Completed" && Field("_roomCardCacheCommitted") is true, "Warm cache bypassed completing the native prewarm pass");
                if (ScummProfile.For(reopened).HasFirstVisitScenes)
                    Require(warmRooms.All(r => NativePrepared(reopened, r.ResourceId!.Value)), "Warm image cache skipped preparing new-library native states");
                else if (reopened is { Id: GameId.Loom, Version: ScummVersion.V3 })
                    Require(Field("_drawBatchesTask") is Task { IsCompletedSuccessfully: true }, "Warm LOOM cache skipped native draw-batch preparation");
                foreach (var room in warmRooms) Require(Card(room) == initialized[room.ResourceId!.Value], "Warm reopen lost the completed initial state");
                Save("warm-initial-states"); Log("Warm reopen: exact initialized pixels and full loader completion; full original GameLibrary retained");
                if (args.Contains("actions"))
                {
                    var root = Path.GetDirectoryName(game.FolderPath)!;
                    var cancelledGame = new GameFolderScanner().Scan(Path.Combine(root, "zakE ega en"));
                    var cancelledRooms = new[] { 1, 5, 7 }.Select(id => cancelledGame.Rooms.Single(r => r.ResourceId == id)).ToArray();
                    await Prepare(cancelledGame, cancelledRooms); var cancelled = Start(cancelledGame, cancelledRooms, false);
                    for (var n = 0; n < 12000 && InitialCount() == 0 && !cancelled.IsCompleted; n++) await Task.Delay(5);
                    Require(InitialCount() > 0 && InitialCount() < cancelledRooms.Length, "Cancellation was not exercised during partial native initialization");
                    ((CancellationTokenSource)Field("_roomCardCancellation")!).Cancel();
                    var replacement = new GameFolderScanner().Scan(Path.Combine(root, "mmansionE ega es"));
                    var replacementRooms = new[] { replacement.Rooms.Single(r => r.ResourceId == 1) };
                    await Prepare(replacement, replacementRooms); var replacementTask = Start(replacement, replacementRooms, false);
                    await Complete(cancelled); await Complete(replacementTask);
                    Require(ReferenceEquals(Field("_game"), replacement) && Phase() == "Completed", "Cancelled game's status replaced the newly loaded game");
                    Require(Cards().All(c => replacementRooms.Contains((GameResource)c.Tag)), "Old game's cards leaked across a game switch");
                    Require(Cached(cancelledGame, cancelledRooms) is null, "Cancelled partial native pass became a complete cache");
                    var replacementCache = Cached(replacement, replacementRooms) ?? throw new InvalidOperationException("Replacement complete cache missing");
                    Require(Card(replacementRooms[0]) == Hash(replacementCache[1]), "Stale publication replaced the replacement game's initialized card");
                    Save("cancelled-game-switch-isolation"); Log("Cancellation during partial native initialization: no complete old cache; overlapping room1 of replacement game stays isolated");
                }
                Log("PASS");
            }
            catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; try { Save("failed-window"); } catch { } }
            finally
            {
                watch?.Stop(); ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
                File.WriteAllLines(Path.Combine(output, "room-previews-check.txt"), report); window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: WindowCheck <game folder> <output folder> <room,object,verb>...
// 31 October 2026 (REQUIREMENTS §8.6): the real MainWindow opens the game, waits for the background scene derivation, then plays each named scene
// from the whole-game list (its Play action, as a pick in the picker does) and logs the room, the texts on screen and the setup, with a picture.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        // WINDOWCHECK_SIZE=WxH: the window's size (3 November 2026: the room changed size at the user's window size).
        if (Environment.GetEnvironmentVariable("WINDOWCHECK_SIZE") is { Length: > 0 } size && size.Split('x') is [var w, var h])
        { window.Width = double.Parse(w); window.Height = double.Parse(h); }
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        async Task Pump(double seconds) { var until = clock.Elapsed.TotalSeconds + seconds; while (clock.Elapsed.TotalSeconds < until) await Task.Delay(20); }
        void State(string stage)
        {
            var play = (ScummSession?)Field("_play") ?? (ScummSession?)Field("_session");
            Log($"{stage}: room={play?.CurrentRoomId} cutscene={play?.InCutscene} texts=[{string.Join(" / ", play?.Messages.OnScreen.SelectMany(m => m.Lines).Where(l => l.Length > 0).Take(3) ?? [])}] printed={play?.Messages.Log.Count} last=[{play?.Messages.Log.LastOrDefault()?.Text.Replace('\n', ' ')}]");
        }
        window.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await Pump(3);
                // A slow game (Indy 4) starts its scene derivation later; a game without one (LOOM) never does.
                for (var i = 0; i < 40 && Field("_playPlanner") is null && Field("_session") is null; i++) await Pump(1);
                for (var i = 0; i < 300 && Field("_playPlanner") is not null && !(bool)Field("_playRoomListsDone")!; i++) await Pump(1);
                // LOOM's scanned editions (31 October: on the shared engine) derive in LOOM's own code; wait for their room lists.
                for (var i = 0; i < 300 && Field("_cdSetupPlanner") is not null && !(bool)Field("_cdRoomListsDone")!; i++) await Pump(1);
                Log($"derivation done={Field("_playRoomListsDone")}, LOOM lists={Field("_cdRoomListsDone")}");
                foreach (var raw in args.Skip(2))
                {
                    // a lists (LOOM) the objects with verbs that are not hotspots now: what the Interact overview adds since 31 October.
                    if (raw == "a")
                    {
                        var session = Field("_session");
                        var snapshot = session?.GetType().GetProperty("Snapshot")!.GetValue(session);
                        var records = (System.Collections.IEnumerable)Field("_currentRoomObjectRecords")!;
                        var absent = new List<string>();
                        foreach (ScummRoomObjectRecord record in records)
                        {
                            var verbs = (System.Collections.ICollection)Call("InteractVerbs", record)!;
                            if (verbs.Count == 0 || (record.X, record.Y) == (0, 0) || snapshot is null) continue;
                            if (!(bool)Call("IsVerbHotspotAvailable", record, snapshot)!) absent.Add(record.Id.ToString());
                        }
                        Log($"  absent with verbs: [{string.Join(", ", absent)}]");
                        continue;
                    }
                    // x:object lists the window's source actions of one object (executable or not).
                    if (raw.StartsWith("x:", StringComparison.Ordinal))
                    {
                        foreach (var action in (System.Collections.IEnumerable)Field("_genericSourceActions")!)
                        {
                            var t = action!.GetType();
                            if ((int)t.GetProperty("Object")!.GetValue(action)! != int.Parse(raw[2..])) continue;
                            Log($"  verb 0x{(int)t.GetProperty("Verb")!.GetValue(action)!:X2} executable={t.GetProperty("Executable")!.GetValue(action)} {t.GetProperty("Source")!.GetValue(action)}");
                        }
                        continue;
                    }
                    // t expands the tree's Cutscenes branch and lists its room groups.
                    // s:id opens a global script's page and logs the first lines of what it shows (the readable view by default).
                    if (raw.StartsWith("s:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        // s:id:text logs only the lines that contain the text.
                        var parts = raw.Split(':', 3);
                        var script = game.AllScripts.First(r => r.ScriptRoom is null && r.ResourceId == int.Parse(parts[1]));
                        Call("SelectResourceInTree", script);
                        await Pump(3);
                        var shown = ((System.Windows.Controls.TextBox)window.FindName("ScriptOutput")).Text;
                        var lines = shown.Split('\n');
                        foreach (var line in parts.Length > 2 ? lines.Where(l => l.Contains(parts[2], StringComparison.Ordinal)).Take(12) : lines.Take(22)) Log("  | " + line.TrimEnd());
                        Capture(raw.Replace(':', '-'));
                        continue;
                    }
                    // w:seconds waits (a room's entry scene finishing before a pick).
                    if (raw.StartsWith("w:", StringComparison.Ordinal))
                    {
                        for (var i = 0; i < int.Parse(raw[2..]); i += 3) { await Pump(3); State($"{raw} +{i + 3}s"); }
                        continue;
                    }
                    if (raw == "t")
                    {
                        var tree = (System.Windows.Controls.TreeView)window.FindName("ResourceTree");
                        var branch = tree.Items.OfType<System.Windows.Controls.TreeViewItem>().FirstOrDefault(i => i.Header?.ToString()?.StartsWith("Cutscenes") == true);
                        if (branch is null) { Log("  no Cutscenes branch"); continue; }
                        branch.IsExpanded = true;
                        await Pump(0.5);
                        Log($"  {branch.Header}: {string.Join(" | ", branch.Items.OfType<System.Windows.Controls.TreeViewItem>().Take(8).Select(i => i.Header))}");
                        continue;
                    }
                    // l lists the room's Play Cutscene entries (the room controls' own state).
                    if (raw == "l")
                    {
                        var controls = typeof(MainWindow).GetProperty("CurrentRoomControls", Private)!.GetValue(window)!;
                        var room = (int?)((SCUMMexplorer.Core.Domain.GameResource?)Field("_currentRoomResource"))?.ResourceId;
                        var state = (RoomControlState)controls.GetType().GetMethod("ReadState")!.Invoke(controls, [room, false])!;
                        Log($"  room {room} cutscenes ({state.RoomCutscenes.Count}): {string.Join(" | ", state.RoomCutscenes.Select(e => e.Title))}");
                        continue;
                    }
                    // o lists the Interact overview (every game, the room controls' own); c:group,choice runs one of its choices.
                    if (raw == "o" || raw.StartsWith("c:", StringComparison.Ordinal))
                    {
                        var controls = typeof(MainWindow).GetProperty("CurrentRoomControls", Private)!.GetValue(window)!;
                        var overview = (RoomInteractionOverview?)controls.GetType().GetMethod("InteractionOverview")!.Invoke(controls, [false]);
                        if (overview is null) { Log($"{raw}: no Interact overview"); continue; }
                        if (raw == "o")
                        {
                            for (var g = 0; g < overview.Groups.Count; g++)
                                Log($"  [{g}] {overview.Groups[g].Heading}: {string.Join(", ", overview.Groups[g].Choices.Select(c => c.Label))}");
                            continue;
                        }
                        var at = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var choice = overview.Groups[at[0]].Choices[at[1]];
                        Log($"{raw}: {overview.Groups[at[0]].Heading} · {choice.Label}");
                        choice.Run();
                        for (var i = 1; i <= 6; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); if (i == 2) Capture(raw.Replace(':', '-').Replace(',', '-')); }
                        continue;
                    }
                    // e presses Skip (Esc), as the playback bar does (6 November 2026: a scene skipped, then played again).
                    if (raw == "e")
                    {
                        Call("SkipPlayCutscene");
                        Log("e: skip pressed");
                        await Pump(2);
                        State("e +2s");
                        continue;
                    }
                    // k:x,y clicks the game's screen as the player does and watches 4 seconds (6 November 2026: walking on the castle plans).
                    if (raw.StartsWith("k:", StringComparison.Ordinal))
                    {
                        var at = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var clicked = (ScummSession)Field("_play")!;
                        var delivered = clicked.ClickScreen(at[0], at[1], out var refused);
                        Log($"{raw}: {(delivered is null ? "refused: " + refused : delivered.Description)}");
                        await Pump(4);
                        var hero = clicked.Actors.FirstOrDefault(a => a.Number == 1);
                        Log($"  hero at ({hero?.X},{hero?.Y}) box {hero?.WalkBox}; draw order {string.Join(",", clicked.Snapshot.DrawOrder)}");
                        Capture(raw.Replace(':', '-').Replace(',', '-'));
                        continue;
                    }
                    // K:x,y presses a point of the shown frame as the play image's own handler does (WPF's hit testing first: an overlay on top would take it), then watches 4 seconds (7 November 2026: "Indy won't move").
                    if (raw.StartsWith("K:", StringComparison.Ordinal))
                    {
                        var at = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var image = (System.Windows.Controls.Image)window.FindName("PlayScreenImage");
                        var frameWidth = ((ScummPlayFrame?)Field("_playShown"))?.PixelWidth ?? ScummPlayFrame.Width;
                        var scale = Math.Min(image.ActualWidth / frameWidth, image.ActualHeight / ScummPlayFrame.Height);
                        var inImage = new Point((image.ActualWidth - frameWidth * scale) / 2 + (at[0] + 0.5) * scale, (image.ActualHeight - ScummPlayFrame.Height * scale) / 2 + (at[1] + 0.5) * scale);
                        var inWindow = image.TranslatePoint(inImage, window);
                        var before = (ScummSession?)Field("_play");
                        var hit = window.InputHitTest(inWindow) as FrameworkElement;
                        var heroBefore = before?.Actors.FirstOrDefault(a => a.Number == 1);
                        var inputsBefore = before?.SessionTrace.Count ?? 0;
                        var refusal = before is null ? null : typeof(ScummSession).GetMethod("InputRefusal", Private)!.Invoke(before, []);
                        Log($"{raw}: hit {hit?.GetType().Name} \"{hit?.Name}\"; takes input {typeof(MainWindow).GetProperty("PlayTakesInput", Private)!.GetValue(window)}; room {before?.CurrentRoomId} hero ({heroBefore?.X},{heroBefore?.Y}); refusal [{refusal}] delivered {before?.DeliveredInput.Count} pending {before?.HasPendingPlayInput}");
                        // Posted mouse messages reach WPF only while the window is active with the real cursor over it, so the press goes in as the image's
                        // handler passes it on, where what is under the point is the image itself (an overlay on top would take it in the app).
                        if (ReferenceEquals(hit, image) && (bool)typeof(MainWindow).GetProperty("PlayTakesInput", Private)!.GetValue(window)!) Call("PlayScreenPress", (at[0], at[1]), 1);
                        else Log("  not pressed: the image is not under the point or play takes no input");
                        Log($"  sent: pending {before?.HasPendingPlayInput} delivered {before?.DeliveredInput.Count} tooltip [{((System.Windows.Controls.TextBlock)window.FindName("StatusText")).ToolTip}] focus {Keyboard.FocusedElement?.GetType().Name} captured {Mouse.Captured?.GetType().Name}");
                        for (var i = 1; i <= 8; i++)
                        {
                            await Pump(0.5);
                            var shown = (ScummSession?)Field("_play");
                            var hero = shown?.Actors.FirstOrDefault(a => a.Number == 1);
                            var sound = Field("_sceneSound")!;
                            Log($"  +{i * 0.5:F1}s room {shown?.CurrentRoomId} hero ({hero?.X},{hero?.Y}) box {hero?.WalkBox} cutscene {shown?.InCutscene} draw {string.Join(",", shown?.Snapshot.DrawOrder ?? [])} | frame {Field("_playFrame")} paused {Field("_playPaused")} preparing {sound.GetType().GetProperty("IsPreparing")!.GetValue(sound)} clock {(Field("_playTimer") as System.Windows.Threading.DispatcherTimer)?.IsEnabled}");
                            if (i == 4) Capture(raw.Replace(':', '-').Replace(',', '-') + "-2s");
                        }
                        foreach (var line in before?.SessionTrace.Skip(inputsBefore).Where(l => l.StartsWith("input", StringComparison.Ordinal) || l.Contains("sentence")).Take(6) ?? []) Log("    " + (line.Length > 170 ? line[..170] : line));
                        continue;
                    }
                    // S lists what the talkie speech player played (lines and effects).
                    if (raw == "S")
                    {
                        var player = Field("_playSpeech")!;
                        var log = (System.Collections.IEnumerable)player.GetType().GetProperty("Log")!.GetValue(player)!;
                        foreach (var line in log) Log($"  speech: {line}");
                        continue;
                    }
                    // p:direction,seconds holds a scroll arrow (or a key) that long and logs the camera (6 November 2026: scrolling in long rooms).
                    if (raw.StartsWith("p:", StringComparison.Ordinal))
                    {
                        var at = raw[2..].Split(',').Select(double.Parse).ToArray();
                        var shown = (ScummSession?)Field("_play");
                        Log($"{raw}: before camera {shown?.CameraX} range {shown?.CameraPanRange} view {((System.Windows.Controls.ScrollViewer)window.FindName("RoomViewport")).HorizontalOffset:F0} playActive {typeof(MainWindow).GetProperty("PlayActive", Private)!.GetValue(window)} ambient {Field("_playAmbient")} framed {((System.Windows.Controls.Primitives.ToggleButton)window.FindName("NormalRoomWidthToggle")).IsChecked} arrows {((UIElement)window.FindName("PlayScrollButtonsPanel")).Visibility}/{((UIElement)window.FindName("RoomScrollButtonsPanel")).Visibility} left {((UIElement)window.FindName("PlayScrollLeftButton")).IsEnabled} right {((UIElement)window.FindName("PlayScrollRightButton")).IsEnabled}");
                        Call("BeginRoomPan", (int)at[0], 64.0, null, System.Windows.Input.Key.Right);
                        await Pump(at[1]);
                        Log($"  during camera {shown?.CameraX} view {((System.Windows.Controls.ScrollViewer)window.FindName("RoomViewport")).HorizontalOffset:F0} active {Field("_roomPanActive")}");
                        Call("StopRoomPan");
                        await Pump(0.5);
                        Log($"  after camera {shown?.CameraX} view left {((ScummPlayFrame?)Field("_playShown"))?.ViewLeft}");
                        Capture(raw.Replace(':', '-').Replace(',', '-'));
                        continue;
                    }
                    // m:direction,seconds presses a scroll arrow with the mouse (its own handlers, the button captured) that long.
                    if (raw.StartsWith("m:", StringComparison.Ordinal))
                    {
                        var at = raw[2..].Split(',').Select(double.Parse).ToArray();
                        var shown = (ScummSession?)Field("_play");
                        var button = (System.Windows.Controls.Button)window.FindName(at[0] < 0 ? "PlayScrollLeftButton" : "PlayScrollRightButton");
                        Log($"{raw}: before camera {shown?.CameraX} range {shown?.CameraPanRange} enabled {button.IsEnabled} visible {button.IsVisible} ambient {Field("_playAmbient")}");
                        MouseButtonEventArgs Press(RoutedEvent routed) => new(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = routed };
                        typeof(MainWindow).GetMethod("PanButton_PreviewMouseLeftButtonDown", Private)!.Invoke(window, [button, Press(UIElement.PreviewMouseLeftButtonDownEvent)]);
                        Log($"  pressed: active {Field("_roomPanActive")} captured {button.IsMouseCaptured}");
                        await Pump(at[1]);
                        Log($"  held: camera {shown?.CameraX} active {Field("_roomPanActive")}");
                        typeof(MainWindow).GetMethod("PanButton_PreviewMouseLeftButtonUp", Private)!.Invoke(window, [button, Press(UIElement.PreviewMouseLeftButtonUpEvent)]);
                        await Pump(0.5);
                        Log($"  released: camera {shown?.CameraX} view left {((ScummPlayFrame?)Field("_playShown"))?.ViewLeft}");
                        Capture(raw.Replace(':', '-').Replace(',', '-'));
                        continue;
                    }
                    // O:id,id logs objects' states and pictures the room (6 November 2026: a door that opens does not show open).
                    if (raw.StartsWith("O:", StringComparison.Ordinal))
                    {
                        var shown = (ScummSession?)Field("_play") ?? (ScummSession?)Field("_session");
                        Log($"{raw}: room {shown?.CurrentRoomId} ambient {Field("_playAmbient")}: " + string.Join(", ", raw[2..].Split(',').Select(int.Parse).Select(id => $"{id}={shown?.Snapshot.ObjectStates.GetValueOrDefault(id)}")));
                        foreach (var line in shown?.SessionTrace.Where(l => !l.Contains("delay") && !l.Contains("Script 17")).TakeLast(24) ?? []) Log("    " + line);
                        foreach (var line in shown?.Snapshot.Uncertainties.Where(l => !l.Contains("getRandomNr")).TakeLast(6) ?? []) Log("    ? " + line);
                        Capture(raw.Replace(':', '-').Replace(',', '-'));
                        continue;
                    }
                    // H lists the room's hotspots and doorways as the shared views get them, whatever draws them, and pictures them Always (6 November 2026).
                    if (raw == "H")
                    {
                        var provider = typeof(MainWindow).GetProperty((bool)typeof(MainWindow).GetProperty("PlayActive", Private)!.GetValue(window)! ? "PlayHotspots" : "LoomHotspots", Private)!.GetValue(window)!;
                        var hotspots = (IReadOnlyList<RoomHotspot>)provider.GetType().GetMethod("Hotspots")!.Invoke(provider, [])!;
                        Log($"H: playActive {typeof(MainWindow).GetProperty("PlayActive", Private)!.GetValue(window)} ambient {Field("_playAmbient")} previewOnly {Field("_catalogPreviewOnly")} {hotspots.Count} objects");
                        foreach (var item in hotspots)
                            Log($"  {(item.IsDoorway ? "doorway" : "hotspot")} {item.ObjectId} \"{item.Name}\" ({item.X},{item.Y}) {item.Width}x{item.Height}{(item.IsDoorway ? " to " + string.Join("/", item.Destinations) : "")} actions {provider.GetType().GetMethod("HasActions")!.Invoke(provider, [item])}");
                        foreach (var option in new[] { "ObjectsAlways", "DoorwaysAlways" })
                            if (window.FindName(option) is System.Windows.Controls.RadioButton radio) radio.IsChecked = true; else Log($"  no option {option}");
                        await Pump(0.6);
                        Capture("hotspots-" + (Field("_playOverlayRoom") ?? ((SCUMMexplorer.Core.Domain.GameResource?)Field("_currentRoomResource"))?.ResourceId));
                        continue;
                    }
                    // d lists the doorways the room shows (VisibleDoorways, 2 November 2026).
                    if (raw == "d")
                    {
                        var shown = (ScummSession?)Field("_play") ?? (ScummSession?)Field("_session");
                        if (shown is null) { Log("  d: no session"); continue; }
                        var doors = (System.Collections.IEnumerable)Call("VisibleDoorways", shown.Snapshot)!;
                        foreach (ScummRoomObjectRecord door in doors) Log($"  doorway object {door.Id} at ({door.X},{door.Y}) {door.Width}x{door.Height}: {Call("DoorwayLabel", door)}");
                        continue;
                    }
                    // A lists the actors in the shown session's room with their costumes (2 November 2026).
                    if (raw == "A")
                    {
                        var shown = (ScummSession)Field("_play")!;
                        var runtime = typeof(ScummSession).GetField("_runtime", Private)!.GetValue(shown)!;
                        var table = (ScummActorTable)runtime.GetType().GetProperty("Actors")!.GetValue(runtime)!;
                        foreach (var actor in table.Snapshot().Where(a => a.Room == shown.CurrentRoomId))
                            Log($"  actor {actor.Number} costume {actor.Look.Costume} scale {actor.Look.Scale} at ({actor.X},{actor.Y}) visible {actor.Visible}");
                        Log($"  ambient={Field("_playAmbient")} previewOnly={Field("_catalogPreviewOnly")} sameAsLive={ReferenceEquals(shown, Field("_playLiveSource"))} " +
                            $"uncertainties: {string.Join(" | ", shown.Snapshot.Uncertainties.Where(u => u.Contains("outfit") || u.Contains("opening") || u.Contains("arrival") || u.Contains("selected manually") || u.Contains("entered again") || u.Contains("Room 82")).TakeLast(12))}");
                        continue;
                    }
                    // g:seconds presses Play Game Intro and captures the screen every 3 seconds (2 November 2026, Indy 4's title).
                    if (raw.StartsWith("g:", StringComparison.Ordinal))
                    {
                        typeof(MainWindow).GetMethod("GameIntro_Click", Private)!.Invoke(window, [window.FindName("GameIntroButton"), new RoutedEventArgs()]);
                        for (var i = 3; i <= int.Parse(raw[2..]); i += 3) { await Pump(3); State($"{raw} +{i}s"); Capture($"intro-{i:00}"); }
                        continue;
                    }
                    // P:room picks the room and goes on at once (quick successive picks, 3 November 2026).
                    if (raw.StartsWith("P:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", Private)!;
                        Call("SelectRoomWithIntent", game.Rooms.First(r => r.ResourceId == int.Parse(raw[2..])), Enum.Parse(kind, "Catalog"), true);
                        await Pump(0.3);
                        continue;
                    }
                    // F:room opens the room and logs every 0.1 s for 4 s which of the room area, the loading panel, the room picture and the play screen show
                    // (3 November 2026: picking a room flickered).
                    if (raw.StartsWith("F:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        var resource = game.Rooms.First(r => r.ResourceId == int.Parse(raw[2..]));
                        var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", Private)!;
                        Call("SelectRoomWithIntent", resource, Enum.Parse(kind, "Catalog"), true);
                        string V(string name) => window.FindName(name) is System.Windows.UIElement e ? e.Visibility.ToString()[0].ToString() : "?";
                        string last = "";
                        for (var i = 1; i <= 40; i++)
                        {
                            await Pump(0.1);
                            var image = (System.Windows.Controls.Image)window.FindName("PlayScreenImage");
                            var state = $"layout {V("RoomDisplayLayout")} loading {V("PreviewMessage")} roomview {V("RoomDisplayGroup")} play {V("PlayScreenImage")} {image.ActualWidth:F0}x{image.ActualHeight:F0}";
                            string H2(string name) => window.FindName(name) is System.Windows.FrameworkElement fe ? $"{name}={fe.Visibility.ToString()[0]}{fe.ActualHeight:F0}" : "";
                            state += " | " + string.Join(" ", new[] { "StatusText", "PageTitle", "PageSubtitle", "ObjectsNote", "PlaybackBar", "SceneInteractionCard", "RoomOptionsExpander", "PlayScrollButtonsPanel" }.Select(H2));
                            var shown = Field("_playShown");
                            if (shown is not null) state += $" | frame {shown.GetType().GetProperty("PixelWidth")!.GetValue(shown)} rows {shown.GetType().GetProperty("RoomTop")!.GetValue(shown)}-{shown.GetType().GetProperty("RoomBottom")!.GetValue(shown)} left {shown.GetType().GetProperty("ViewLeft")!.GetValue(shown)}";
                            state += $" src {(image.Source as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth}";
                            if (state != last) { Log($"{raw} +{i * 0.1:F1}s: {state}"); if (i <= 30) Capture($"pick-{raw[2..]}-{i:00}"); }
                            last = state;
                        }
                        continue;
                    }
                    // R:room opens the room as r: does and captures the window every half second for 4 seconds (2 November 2026: the room's size jump).
                    if (raw.StartsWith("R:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        var resource = game.Rooms.First(r => r.ResourceId == int.Parse(raw[2..]));
                        var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", Private)!;
                        Call("SelectRoomWithIntent", resource, Enum.Parse(kind, "Catalog"), true);
                        for (var i = 1; i <= 8; i++)
                        {
                            await Pump(0.5);
                            var image = (System.Windows.Controls.Image)window.FindName("PlayScreenImage");
                            var preview = (System.Windows.Controls.Image)window.FindName("RoomPreview");
                            string H(string name) => window.FindName(name) is System.Windows.FrameworkElement e ? $"{name}={e.Visibility.ToString()[0]}{e.ActualHeight:F0}" : "";
                            Log("   " + string.Join(" ", new[] { "RoomViewportHost", "PlayScrollButtonsPanel", "ObjectsNote", "CutsceneScanNote", "CutsceneScanProgress", "GenericRoomActionsHost", "RoomDisplayLayout", "StatusText", "PageTitle", "PageSubtitle", "PageHeader", "BusyOverlay", "SceneInteractionCard", "SceneSessionBar", "SessionStatusText", "SessionActions", "PlaybackBar", "RoomOptionsExpander" }.Select(H)));
                            Log($"{raw} +{i * 0.5:F1}s: play {image.Visibility} {image.ActualWidth:F0}x{image.ActualHeight:F0} src {(image.Source as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth}; preview {preview.Visibility} {preview.ActualWidth:F0}x{preview.ActualHeight:F0}");
                            Capture($"room-{raw[2..]}-{i:00}");
                        }
                        continue;
                    }
                    // r:room opens the room as a pick in the room selector does (a catalog visit).
                    if (raw.StartsWith("r:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        var resource = game.Rooms.FirstOrDefault(r => r.ResourceId == int.Parse(raw[2..]));
                        if (resource is null) { Log($"{raw}: no such room; rooms {string.Join(",", game.Rooms.Select(r => r.ResourceId))}"); continue; }
                        var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", Private)!;
                        Call("SelectRoomWithIntent", resource, Enum.Parse(kind, "Catalog"), true);
                        for (var i = 1; i <= 4; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); if (i == 1) Capture("room-" + raw[2..] + "-early"); }
                        Capture("room-" + raw[2..]);
                        continue;
                    }
                    // i:room,object,verb picks an interaction of the room shown (the Interact menu's choice, RunGenericRoomAction).
                    if (raw.StartsWith("i:", StringComparison.Ordinal))
                    {
                        var parts = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var actions = (System.Collections.IList)Field("_genericSourceActions")!;
                        object? action = null;
                        foreach (var candidate in actions)
                            if ((int)candidate!.GetType().GetProperty("Object")!.GetValue(candidate)! == parts[1] &&
                                (int)candidate.GetType().GetProperty("Verb")!.GetValue(candidate)! == parts[2]) action = candidate;
                        if (action is null) { Log($"{raw}: no such action in the room shown ({Field("_genericActionsRoom")})"); continue; }
                        Log($"{raw}: interact \"{action.GetType().GetProperty("Title")!.GetValue(action)}\"");
                        typeof(MainWindow).GetMethod("RunGenericRoomAction", Private)!.Invoke(window, [action]);
                        for (var i = 1; i <= 6; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); if (i == 2) Capture(raw.Replace(':', '-').Replace(',', '-')); }
                        continue;
                    }
                    var spec = raw;
                    var items = (System.Collections.IList)Call("PlayCutsceneItems", new object?[] { null })!;
                    // LOOM's list: PlayableCutsceneItems, ids cd-room-object-verb.
                    if (items.Count == 0) items = (System.Collections.IList)Call("PlayableCutsceneItems", new object?[] { null })!;
                    object? picked = null;
                    foreach (var item in items)
                        if ((string)item!.GetType().GetField("Item1")!.GetValue(item)! is var id && (id == "scumm-" + spec.Replace(',', '-') || id == "cd-" + spec.Replace(',', '-')))
                            picked = item.GetType().GetField("Item2")!.GetValue(item);
                    if (picked is null) { Log($"{spec}: not in the list"); continue; }
                    Log($"{spec}: playing \"{picked.GetType().GetProperty("Title")!.GetValue(picked)}\"");
                    ((Action)picked.GetType().GetProperty("Play")!.GetValue(picked)!).Invoke();
                    // WINDOWCHECK_SCENE=seconds: how long a scene is watched (6 November 2026: full scenes, played twice).
                    var watched = int.TryParse(Environment.GetEnvironmentVariable("WINDOWCHECK_SCENE"), out var sceneSeconds) ? sceneSeconds * 2 / 3 : 8;
                    // WINDOWCHECK_EARLY=n: n pictures and actor lists half a second apart as the scene begins (its first state).
                    var repeat = report.Count(line => line.Contains($"{spec}: playing"));
                    for (var i = 1; i <= (int.TryParse(Environment.GetEnvironmentVariable("WINDOWCHECK_EARLY"), out var early) ? early : 0); i++)
                    {
                        await Pump(0.5);
                        var shown = (ScummSession?)Field("_play");
                        if (shown is not null)
                        {
                            var runtime = typeof(ScummSession).GetField("_runtime", Private)!.GetValue(shown)!;
                            var table = (ScummActorTable)runtime.GetType().GetProperty("Actors")!.GetValue(runtime)!;
                            Log($"  +{i * 0.5:F1}s room {shown.CurrentRoomId} actors: {string.Join(", ", table.Snapshot().Where(a => a.Room == shown.CurrentRoomId).Select(a => $"{a.Number} c{a.Look.Costume} ({a.X},{a.Y}){(a.Visible == true ? "" : " hidden")}"))}");
                        }
                        Capture($"{spec.Replace(',', '-')}-run{repeat}-early{i:00}");
                    }
                    for (var i = 1; i <= watched; i++) { await Pump(1.5); State($"{spec} +{i * 1.5:F1}s"); if (i == 3 || i % 6 == 0) Capture($"{spec.Replace(',', '-')}-run{repeat}-{i:00}"); }
                    var play = (ScummSession?)Field("_play") ?? (ScummSession?)Field("_session");
                    Log($"{spec} setup: {string.Join(" | ", play?.Snapshot.Uncertainties.Where(u => u.Contains("setup") || u.Contains("picked") || u.Contains("started directly")).TakeLast(3) ?? [])}");
                }
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
    }
}

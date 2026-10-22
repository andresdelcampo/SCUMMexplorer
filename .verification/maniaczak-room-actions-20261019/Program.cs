using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        if (args[0] == "source") { Source(args[1], output); return; }
        if (args[0] != "window") throw new ArgumentException("Expected source or window.");
        Window(args[1], output, args.Length > 3 ? args[3] : "mmansionE ega es");
    }

    private static void Source(string root, string output)
    {
        // No App or MainWindow is constructed, and no WPF window is created in this mode.
        var title = typeof(MainWindow).GetMethod("GenericCutsceneTitle", BindingFlags.Static | BindingFlags.NonPublic)!;
        var lines = new List<string>();
        foreach (var fixture in new[] { "mmansion ega en", "mmansionE ega es", "zak ega en", "zakE ega en" })
        {
            var game = new GameFolderScanner().Scan(Path.Combine(root, fixture));
            var planner = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced
                ? ManiacCutsceneSetup.Planner(game) : new ScummCutscenePlanner(game);
            var titles = planner.Groups.Select(g => (string)title.Invoke(null, [g.First])!).ToArray();
            Require(titles.Length > 0, "No source scene groups for " + fixture);
            Require(titles.All(t => !t.Contains("verb 0x", StringComparison.OrdinalIgnoreCase)), "Raw verb in title: " + fixture);
            var data = ScummDataSources.For(game);
            var room = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced ? 28 : 1;
            var stored = data.ReadRoom(room);
            var entries = data.ReadObjectVerbs(room, stored.Objects);
            var counts = entries.SelectMany(p => p.Value.Select(v => (Object: p.Key, Verb: v))).ToArray();
            var executable = 0;
            foreach (var (obj, verb) in counts)
            {
                var code = data.ReadObjectVerb(room, stored.Objects.Last(o => o.Id == obj), verb.VerbId, out _);
                if (verb.VerbId < 250 && code.UndecodedOffset is null && code.Instructions.Any(i => i.Offset == code.EntryOffset)) executable++;
            }
            Require(executable > 0, "No fully decoded ordinary source actions: " + fixture);
            lines.Add($"{GameSupport.FamilyOf(game)} | {titles.Length} clean source scene titles | Room {room}: {counts.Length} source entries, {executable} fully decoded ordinary actions");
            lines.Add($"  Own source: entry={data.RoomEntryScript(room) is not null}, exit={data.RoomExitScript(room) is not null}, local scripts={data.RoomLocalScriptIds(room).Count}");
            lines.AddRange(titles.Take(4).Select(t => "  " + t));
            if (planner.PreparationNote is { } note) lines.Add("  Declared planner start: " + note);
        }
        lines.Add("PASS: four own-data title/source checks; no App/MainWindow/window constructed; no scene run or native fidelity claim.");
        File.WriteAllLines(Path.Combine(output, "source-results.txt"), lines);
        Console.WriteLine(string.Join(Environment.NewLine, lines));
    }

    private static void Window(string root, string output, string fixture)
    {
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string line) { report.Add($"{clock.Elapsed.TotalSeconds:F2}s {line}"); File.WriteAllLines(Path.Combine(output, "window-results.txt"), report); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        ScummGameSession Play() => (ScummGameSession)Field("_play")!;
        void StopClock() => ((DispatcherTimer?)Field("_playTimer"))?.Stop();
        void Frames(int count) { for (var i = 0; i < count; i++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
        void Until(Func<bool> ready, string reason, int maximum = 3000)
        {
            for (var i = 0; i < maximum && !ready(); i++) Frames(1);
            Require(ready(), reason);
        }
        async Task Wait(Func<bool> ready, string reason, int maximumSeconds = 120)
        {
            var wait = Stopwatch.StartNew(); while (!ready() && wait.Elapsed.TotalSeconds < maximumSeconds) await Task.Delay(20);
            Require(ready(), "Timed out: " + reason);
        }
        IEnumerable<Button> Buttons() => ((IEnumerable)Field("_genericActionButtons")!).Cast<Button>();
        int Value(Button button, string property) => (int)button.Tag.GetType().GetProperty(property)!.GetValue(button.Tag)!;
        string Caption(Button button) => ((TextBlock)button.Content).Text;
        IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
        }
        IEnumerable<Button> PopupButtons() => PresentationSource.CurrentSources.Cast<PresentationSource>()
            .Where(s => s.RootVisual is not null && !ReferenceEquals(s.RootVisual, window))
            .SelectMany(s => Descendants(s.RootVisual)).OfType<Button>();
        void Capture(string name)
        {
            window.UpdateLayout(); var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        async Task Select(GameLibrary game, int room)
        {
            Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == room));
            if (Field("_roomOpenTask") is Task opening) { await Wait(() => opening.IsCompleted, "manual room " + room); await opening; }
            StopClock();
        }
        async Task<GameLibrary> Open(string fixture)
        {
            Call("OpenGameFolder", Path.Combine(root, fixture));
            Control<ComboBox>("SoundModeCombo").SelectedIndex = 0;
            ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
            if (Field("_roomCardLoadTask") is Task cards) { await Wait(() => cards.IsCompleted, "initial room cards", 180); await cards; }
            return (GameLibrary)Field("_game")!;
        }
        async Task Menu(bool advanced, bool launch)
        {
            Control<CheckBox>("AdvancedModeToggle").IsChecked = advanced; Call("UpdateAdvancedMode");
            Call("UpdatePlaybackControls");
            var items = ((IEnumerable)Call("RoomCutsceneItems", 1)!).Cast<object>().ToArray();
            Require(items.Length > 1, "Expected multiple Room01 scene choices");
            Require(Control<Button>("RoomCutsceneButton").IsVisible && Control<Button>("RoomCutsceneButton").IsEnabled,
                "Room scenes are not visibly available in " + (advanced ? "Advanced" : "Normal"));
            Control<Button>("RoomCutsceneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);
            var rows = PopupButtons().Where(b => b.Content is TextBlock).ToArray();
            Require(rows.Length == items.Length, "Actual popup scene count differs from room source choices");
            Require(rows.All(b => !Caption(b).Contains("verb 0x", StringComparison.OrdinalIgnoreCase)), "Actual scene popup has raw verb name");
            Require(rows.All(b => advanced ? b.ToolTip is string : b.ToolTip is null), "Scene evidence tooltip is in the wrong mode");
            Log($"{(advanced ? "Advanced" : "Normal")} actual Room01 scene popup: {rows.Length} readable choices, evidence mode correct");
            // Clicking a real source row closes the popup through its normal handler.
            rows[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (Field("_roomOpenTask") is Task opening) { await Wait(() => opening.IsCompleted, "explicit scene room"); await opening; }
            await Wait(() => Field("_playTimer") is not null, "explicit scene starts"); StopClock();
            Require(Play().CurrentRoomId is not null, "Explicit scene has no native room");
            if (launch) Log($"Explicit popup launch adopted native Room{Play().CurrentRoomId}, LastAction={Play().LastAction?.ObjectId}/{Play().LastAction?.VerbId}");
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var game = await Open(fixture);
                var maniac = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;
                await Select(game, 1);
                if (maniac)
                {
                    Require(Field("_playTimer") is null, "Unanswered party unexpectedly became live");
                    Require(Buttons().Any() && Buttons().All(b => !b.IsEnabled), "Static room has executable direct actions");
                }
                var planner = (Lazy<ScummCutscenePlanner>)Field("_playPlanner")!;
                await Wait(() => planner.IsValueCreated && (bool)Field("_playRoomListsDone")!, "source scene readiness");
                Call("UpdatePlaybackControls");
                Log($"Opened {GameSupport.FamilyOf(game)}; initial direct actions={Buttons().Count()}, enabled={Buttons().Count(b => b.IsEnabled)}");
                Capture("normal-room-actions");
                await Menu(advanced: false, launch: true);
                await Select(game, 1);
                await Menu(advanced: true, launch: true);
                await Select(game, 1);
                Control<CheckBox>("AdvancedModeToggle").IsChecked = false; Call("UpdateAdvancedMode");
                Control<Button>("GameIntroButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(() => Field("_playTimer") is not null, "native intro"); StopClock();
                if (maniac)
                {
                    Until(() => Play().CurrentRoomId == 45, "Native party screen absent");
                    foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) })
                    {
                        Require(Play().ClickScreen(x, y + Play().MainScreen.Top, out _) is not null, "Native party input refused"); Frames(101);
                    }
                    Until(() => Play().CutsceneSkippable, "Native outdoor intro skip absent");
                    Play().PressKey(27); Until(() => Play().PlayerHasControl, "Native intro did not finish");
                    Require(Play().ClickScreen(8, 120 + Play().MainScreen.Top, out _) is not null, "Native room input refused");
                    Until(() => Play().CurrentRoomId == 1, "Native entrance absent"); Frames(120);
                    await Select(game, 28);
                }
                else { Until(() => Play().PlayerHasControl && !Play().BootRunning, "Native Zak intro did not finish", 6000); Frames(150); }
                var actionRoom = maniac ? 28 : 1;
                Require(Field("_playTimer") is not null && Play().CurrentRoomId == actionRoom, "Own native direct-action room is not live");
                Call("RefreshPlayRoomActions");
                var action = Buttons().Single(b => Value(b, "Object") == (maniac ? 413 : 92) && Value(b, "Verb") == (maniac ? 11 : 1));
                var interact = Control<Panel>("GenericRoomActionsHost").Children.OfType<Expander>().Single();
                interact.IsExpanded = true; window.UpdateLayout();
                Require(action.IsVisible, "Real Interact expander did not expose direct action row");
                Require(action.IsEnabled && !Caption(action).StartsWith("Action", StringComparison.Ordinal), "Native action name/control did not replace early fallback");
                var codeList = Field("_genericSourceActions"); var firstSource = Buttons().First().Tag;
                Call("RefreshPlayRoomActions");
                Require(ReferenceEquals(codeList, Field("_genericSourceActions")) && ReferenceEquals(firstSource, Buttons().First().Tag), "Room source data was reread on unchanged refresh");
                var inputs = Play().DeliveredInput.Count;
                var sentences = Play().SessionTrace.Count(line => line.StartsWith("sentence script", StringComparison.Ordinal));
                action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(Play().LastAction?.ObjectId == (maniac ? 413 : 92) && Play().LastAction?.VerbId == (maniac ? 11 : 1) && Play().DeliveredInput.Count == inputs,
                    "Direct Interact composed/dispatched player input");
                Require(Play().SessionTrace.Count(line => line.StartsWith("sentence script", StringComparison.Ordinal)) == sentences, "Direct Interact dispatched a native sentence");
                if (maniac) Require(Play().InCutscene && !Play().CutsceneSkippable, "Native telescope no-override scene not reached");
                else Require((Play().Snapshot.ObjectStates.GetValueOrDefault(92) & 8) != 0, "Direct native Open did not open the drawer");
                Log($"Direct named {Caption(action)}: own object/verb LastAction; unchanged delivered input and sentence trace; native result reached"); Capture("direct-native-source-action");
                File.WriteAllLines(Path.Combine(output, "direct-source-trace.txt"), Play().SessionTrace.TakeLast(80));
                Control<CheckBox>("AdvancedModeToggle").IsChecked = true; Call("UpdateAdvancedMode");
                Require(Control<Panel>("GenericRoomSourceHost").Visibility == Visibility.Visible && Control<Panel>("GenericRoomSourceHost").Children.Count > 3, "Advanced own-source sections absent");
                var sourceHost = Control<Panel>("GenericRoomSourceHost");
                var roomCode = sourceHost.Children.OfType<Expander>().ToArray();
                var ownData = ScummDataSources.For(game);
                Require(ownData.RoomEntryScript(actionRoom) is null || roomCode.Any(e => (string)e.Header == "Room entry"), "Available entry section missing");
                Require(ownData.RoomExitScript(actionRoom) is null || roomCode.Any(e => (string)e.Header == "Room exit"), "Available exit section missing");
                Require(ownData.RoomLocalScriptIds(actionRoom).All(id => roomCode.Any(e => (string)e.Header == "Local script " + id)), "Available local-script section missing");
                Log($"Advanced own-data sections: entry={ownData.RoomEntryScript(actionRoom) is not null}, exit={ownData.RoomExitScript(actionRoom) is not null}, locals={ownData.RoomLocalScriptIds(actionRoom).Count}; v1/v2 have no local directory");
                foreach (var code in roomCode.Where(e => (string)e.Header is "Room entry" or "Room exit")) code.IsExpanded = true;
                var objectCode = roomCode.First(e => e.Content is StackPanel stack && stack.Children.OfType<Expander>().Any());
                objectCode.IsExpanded = true;
                var raw = ((StackPanel)objectCode.Content).Children.OfType<Expander>().Single(); raw.IsExpanded = true;
                Require(((StackPanel)raw.Content).Children.OfType<TextBox>().Any(t => t.Text.Length > 0), "Object raw source did not expand");
                Require(sourceHost.Children.OfType<TextBlock>().Any(t => t.Text.StartsWith("RELATIONSHIPS", StringComparison.Ordinal)), "Possible relationship section missing");
                Capture("advanced-room-source"); Log("Advanced room scripts/interactions/possible source relationships visible; titles retain no verb byte");
                await Open("loom ega mt32 en");
                Require(Control<Button>("InteractButton").Visibility == Visibility.Visible, "Generic-to-LOOM switch lost legacy Interact");
                Require(Control<Panel>("GenericRoomActionsHost").Visibility == Visibility.Collapsed && Control<Panel>("GenericRoomSourceHost").Visibility == Visibility.Collapsed, "Generic source hosts leaked into LOOM");
                Log("PASS bounded real-window direct action, static scene availability, Advanced source and generic-to-LOOM checks");
            }
            catch (Exception ex) { Log("FAIL " + ex); Environment.ExitCode = 1; }
            finally { Call("StopPlay"); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

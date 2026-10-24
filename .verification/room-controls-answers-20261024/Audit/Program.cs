using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Serial private-profile parity audit: dumps the room controls of one game in Normal and Advanced.
// args: root output fixture room [static|live]
internal static class Program
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [STAThread]
    private static void Main(string[] args)
    {
        var root = args[0]; var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var fixture = args[2]; var roomNumber = int.Parse(args[3]); var live = args.Length > 4 ? args[4] : "static";
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string line) { report.Add(line); File.WriteAllLines(Path.Combine(output, $"audit-{live}.txt"), report); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        var generic = !fixture.StartsWith("loom");
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        ScummGameSession Play() => (ScummGameSession)Field("_play")!;
        void StopClock() => ((DispatcherTimer?)Field("_playTimer"))?.Stop();
        void Frames(int count) { for (var i = 0; i < count; i++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
        void Until(Func<bool> ready, string reason, int maximum = 6000) { for (var i = 0; i < maximum && !ready(); i++) Frames(1); if (!ready()) throw new InvalidOperationException(reason); }
        async Task Wait(Func<bool> ready, string reason, int seconds = 180)
        { var w = Stopwatch.StartNew(); while (!ready() && w.Elapsed.TotalSeconds < seconds) await Task.Delay(20); if (!ready()) throw new TimeoutException(reason); }
        IEnumerable<DependencyObject> Descendants(DependencyObject r, bool skipLists)
        {
            yield return r;
            if (skipLists && r is TreeView or ListBox or ListView) yield break;
            if (r is not Visual) yield break;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(r); i++)
                foreach (var c in Descendants(VisualTreeHelper.GetChild(r, i), skipLists)) yield return c;
        }
        string Txt(object? c) => c switch { null => "", string s => s, TextBlock t => t.Text, ContentControl cc => Txt(cc.Content), Panel p => string.Join(" ", p.Children.OfType<object>().Select(Txt)), _ => c.ToString() ?? "" };
        string Tip(FrameworkElement e) => e.ToolTip is null ? "" : " | tip: " + Txt(e.ToolTip).Replace("\n", " / ");
        string Named(DependencyObject d)
        {
            for (var p = d; p is not null; p = (p as FrameworkElement)?.Parent ?? (p is Visual ? VisualTreeHelper.GetParent(p) : null))
                if (p is FrameworkElement f && !string.IsNullOrEmpty(f.Name)) return f.Name;
            return "-";
        }
        void Dump(string title, DependencyObject rootVisual, bool skipLists)
        {
            Log($"--- {title}");
            var seen = new HashSet<string>();
            foreach (var d in Descendants(rootVisual, skipLists))
            {
                if (d is not FrameworkElement e || !e.IsVisible) continue;
                string? line = e switch
                {
                    Expander x => $"Expander \"{Txt(x.Header)}\" expanded={x.IsExpanded}",
                    CheckBox c => $"CheckBox \"{Txt(c.Content)}\" checked={c.IsChecked} enabled={c.IsEnabled}",
                    RadioButton r => $"Radio \"{Txt(r.Content)}\" checked={r.IsChecked}",
                    ToggleButton t and not CheckBox and not RadioButton => $"Toggle \"{Txt(t.Content)}\" checked={t.IsChecked} enabled={t.IsEnabled}",
                    Button b when b.TemplatedParent is null => $"Button \"{Txt(b.Content)}\" enabled={b.IsEnabled}",
                    ComboBox cb => $"Combo selected=\"{Txt(cb.SelectedItem)}\"",
                    TextBlock tb when tb.Text.Length > 0 && tb.TemplatedParent is null => $"Text \"{(tb.Text.Length > 160 ? tb.Text[..160] + "..." : tb.Text).Replace("\n", " / ")}\"",
                    TextBox tx when tx.Text.Length > 0 => $"TextBox \"{(tx.Text.Length > 80 ? tx.Text[..80] : tx.Text).Replace("\n", " / ")}\"",
                    _ => null
                };
                if (line is null || Named(e) is "RoomCardList" or "HeaderSite" or "Toggle") continue;
                var full = $"[{Named(e)}] {line}{Tip(e)}";
                if (seen.Add(full)) Log("  " + full);
            }
        }
        IEnumerable<Visual> PopupRoots() => PresentationSource.CurrentSources.Cast<PresentationSource>()
            .Where(s => s.RootVisual is Visual v && !ReferenceEquals(v, window)).Select(s => (Visual)s.RootVisual);
        void ClosePopups()
        {
            foreach (var v in PopupRoots().ToArray())
                if (v is FrameworkElement fe && fe.Parent is Popup p) p.IsOpen = false;
            foreach (var v in PopupRoots().ToArray())
                foreach (var d in Descendants(v, false)) if (d is FrameworkElement f && LogicalTreeHelper.GetParent(f) is Popup q) q.IsOpen = false;
        }
        void Capture(string name)
        {
            window.UpdateLayout(); var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        async Task Select(GameLibrary game, int room)
        {
            Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == room));
            if (Field("_roomOpenTask") is Task opening) { await Wait(() => opening.IsCompleted, "room " + room); await opening; }
            if (live == "static") StopClock();
        }
        async Task Modes(string label)
        {
            foreach (var advanced in new[] { false, true })
            {
                Control<CheckBox>("AdvancedModeToggle").IsChecked = advanced; Call("UpdateAdvancedMode"); Call("UpdatePlaybackControls");
                Control<Expander>("RoomOptionsExpander").IsExpanded = true;
                window.UpdateLayout();
                Log($"==={label} {(advanced ? "ADVANCED" : "NORMAL")}");
                Log("  options: " + string.Join("; ", new[] { "DoorwaysNever", "DoorwaysHover", "DoorwaysAlways", "ObjectsNever", "ObjectsHover", "ObjectsAlways" }.Select(n => $"{n}={Control<RadioButton>(n).IsChecked}/vis={Control<RadioButton>(n).IsVisible}")) + $"; DoorwayOptions={Control<Panel>("DoorwayOptions").Visibility}; ObjectAreaOptions={Control<Panel>("ObjectAreaOptions").Visibility}");
                Dump("window", window, true);
                {
                    var provider = (IRoomControlProvider)typeof(MainWindow).GetProperty("CurrentRoomControls", Private)!.GetValue(window)!;
                    var st = provider.ReadState(roomNumber, advanced);
                    Log($"  COUNT room cutscenes={st.RoomCutscenes.Count}: " + string.Join(" | ", st.RoomCutscenes.Select(e => e.Title)));
                    var tr = provider.ReadTransport();
                    Log($"  TRANSPORT pauseEnabled={tr.PauseEnabled} paused={tr.Paused} step={tr.StepEnabled} inCutscene={tr.InCutscene} skippable={tr.CutsceneSkippable} skipLine={tr.SkipLineEnabled}");
                    var ri = provider.ReadInteract();
                    var ov = provider.InteractionOverview(advanced);
                    Log($"  INTERACT visible={ri.Visible} enabled={ri.Enabled} groups={ov?.Groups.Count ?? 0} actions={ov?.Actions ?? 0}: " + string.Join(" | ", (ov?.Groups ?? []).Take(40).Select(g => $"{g.Heading}[{g.Choices.Count}]")));
                }
                Capture($"{label}-{(advanced ? "adv" : "norm")}");
                foreach (var name in new[] { "RoomCutsceneButton", "InteractButton", "RoomSoundsButton" })
                {
                    var b = Control<Button>(name);
                    if (b.IsVisible && b.IsEnabled && !(name == "RoomCutsceneButton" && !Txt(b.Content).Contains("▴")))
                    {
                        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80); window.UpdateLayout();
                        Log($"--- popup after {name}");
                        foreach (var v in PopupRoots().ToArray()) Dump("popup", v, false);
                        Capture($"{label}-{(advanced ? "adv" : "norm")}-{name}");
                        ClosePopups();
                    }
                    else Log($"  ({name}: visible={b.IsVisible} enabled={b.IsEnabled})");
                }
                if (generic)
                {
                    foreach (var ex in Control<Panel>("GenericRoomActionsHost").Children.OfType<Expander>()) ex.IsExpanded = true;
                    window.UpdateLayout();
                    Dump("generic actions host (expanded)", Control<Panel>("GenericRoomActionsHost"), false);
                }
            }
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                { var r = Control<RadioButton>("ObjectsNever"); Log("probe: group=" + r.GroupName + " checked=" + r.IsChecked + " loaded=" + r.IsLoaded + " parent=" + r.Parent); } Call("OpenGameFolder", Path.Combine(root, fixture)); Log("probe after open: ObjectsNever=" + Control<RadioButton>("ObjectsNever").IsChecked);
                if (generic) Control<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                if (generic) ((SceneSoundPlayer)Field("_sceneSound")!).SetMode(SceneSoundPlayer.SoundMode.None);
                if (Field("_roomCardLoadTask") is Task cards) { await Wait(() => cards.IsCompleted, "cards", 240); await cards; }
                var game = (GameLibrary)Field("_game")!;
                Log($"Game {GameSupport.FamilyOf(game)} {fixture}");
                
                await Select(game, roomNumber);
                if (!generic) Log("status tooltip: " + Txt(Control<TextBlock>("StatusText").ToolTip) + " / status: " + Control<TextBlock>("StatusText").Text);
                if (generic && Field("_playPlanner") is { } lazyObj)
                {
                    var planner = (Lazy<ScummCutscenePlanner>)lazyObj;
                    await Wait(() => planner.IsValueCreated && (bool)Field("_playRoomListsDone")!, "scene lists", 240);
                }
                else await Task.Delay(1500);
                if (live == "live" && generic)
                {
                    Control<Button>("GameIntroButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Wait(() => Field("_playTimer") is not null, "native intro"); StopClock();
                    if (GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced)
                    {
                        Until(() => Play().CurrentRoomId == 45, "party screen");
                        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Play().ClickScreen(x, y + Play().MainScreen.Top, out _); Frames(101); }
                        Until(() => Play().CutsceneSkippable, "skip"); Play().PressKey(27); Until(() => Play().PlayerHasControl, "control");
                        Play().ClickScreen(8, 120 + Play().MainScreen.Top, out _); Until(() => Play().CurrentRoomId == 1, "room1"); Frames(120);
                    }
                    else { Until(() => Play().PlayerHasControl && !Play().BootRunning, "zak intro"); Frames(150); }
                    
                await Select(game, roomNumber);
                if (!generic) Log("status tooltip: " + Txt(Control<TextBlock>("StatusText").ToolTip) + " / status: " + Control<TextBlock>("StatusText").Text);
                    Log($"Live native room {Play().CurrentRoomId}, control={Play().PlayerHasControl}");
                }
                if (!generic)
                {
                    try { await Wait(() => Field("_session") is not null && Field("_catalogPreviewOnly") is false, "LOOM session", 20); } catch (TimeoutException) { Log("LOOM session still catalog-preview-only after 20s"); }
                    Log($"interactiveToggle={Control<CheckBox>("SessionModeToggle").IsChecked}");
                    await Task.Delay(1500);
                    Log($"LOOM session={Field("_session") is not null} previewOnly={Field("_catalogPreviewOnly")}"); if (live == "live") { typeof(MainWindow).GetField("_catalogPreviewOnly", Private)!.SetValue(window, false); Call("UpdatePlaybackControls"); Log("harness cleared catalog-preview-only (emulates the first in-room action)"); }
                    var records = ((IEnumerable)Field("_currentRoomObjectRecords")!).Cast<object>().ToArray();
                    foreach (var rec in records)
                    {
                        /* upfront overview: no target selected */ break;
                        if (Control<Button>("InteractButton").IsEnabled) break;
                    }
                    Log($"LOOM interact target: {Control<Button>("InteractButton").Content}");
                }
                if (generic)
                {
                    var acts = ((IEnumerable)Field("_genericSourceActions")!).Cast<object>().Where(a => (bool)a.GetType().GetProperty("Executable")!.GetValue(a)!).ToArray();
                    if (false && acts.Length > 0) { typeof(MainWindow).GetField("_genericInteractTarget", Private)!.SetValue(window, (int?)(int)acts[0].GetType().GetProperty("Object")!.GetValue(acts[0])!); Call("UpdatePlaybackControls"); }
                    Log($"generic interact target set; button={Control<Button>("InteractButton").Content} enabled={Control<Button>("InteractButton").IsEnabled}");
                }
                await Modes($"{fixture.Replace(' ', '_')}-r{roomNumber}-{live}");
                Log("DONE " + clock.Elapsed.TotalSeconds.ToString("F1") + "s");
            }
            catch (Exception ex) { Log("FAIL " + ex); Environment.ExitCode = 1; }
            finally { try { Call("StopPlay"); } catch { } window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}







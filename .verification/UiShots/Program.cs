using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Normal-mode layout, the corner full-screen button and the Advanced script view (bytecode toggle, play button) in the real MainWindow.
// Arguments: <Games\loom> <output folder>
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string n) => (T)window.FindName(n);
        void SaveWindow(string file)
        {
            window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render((Visual)window.Content);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
            using var s = File.Create(Path.Combine(output, file)); enc.Save(s);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1500);
                // Game Texts: room 06 conversations, first-visit picture, speaker names, compact dropdowns
                {
                    var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                    var extraction = new LoomTextExtractor().Extract(game);
                    Func<int, Task<System.Windows.Media.Imaging.BitmapSource?>> roomImage = id => (Task<System.Windows.Media.Imaging.BitmapSource?>)typeof(MainWindow).GetMethod("RenderFirstVisitRoomAsync", Private)!.Invoke(window, [id])!;
                    var gallery = new TextGalleryWindow(game, extraction, _ => { }, null, roomImage) { Owner = window };
                    gallery.Show();
                    await Task.Delay(1500);
                    var roomFilter = (ComboBox)gallery.FindName("RoomFilter");
                    var pick = roomFilter.Items.Cast<object>().First(i => i.ToString()!.Contains("Room 06"));
                    roomFilter.SelectedItem = pick;
                    await Task.Delay(800);
                    var list = (ListBox)gallery.FindName("TextList");
                    report.Add($"gallery: room 06 rows {list.Items.Count}");
                    list.SelectedIndex = Enumerable.Range(0, list.Items.Count).First(i => (list.Items[i].GetType().GetProperty("ListHeading")!.GetValue(list.Items[i]) as string ?? "").Contains("CUTSCENE"));
                    await Task.Delay(2500);
                    report.Add("gallery lines: " + string.Join(" | ", ((StackPanel)gallery.FindName("ConversationLines")).Children.OfType<Grid>().Select(g => string.Join(": ", g.Children.OfType<TextBlock>().Select(t => t.Text)))));
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    list.SelectedIndex = list.SelectedIndex == 0 ? 1 : 0; await Task.Delay(50);
                    list.SelectedIndex = Enumerable.Range(0, list.Items.Count).First(i => (list.Items[i].GetType().GetProperty("ListHeading")!.GetValue(list.Items[i]) as string ?? "").Contains("CUTSCENE"));
                    await Task.Delay(50);
                    report.Add($"second pick in the same room: picture ready after {sw.ElapsedMilliseconds} ms, shown {((Image)gallery.FindName("RoomArtworkPreview")).Visibility}");
                    var gb = new RenderTargetBitmap((int)gallery.ActualWidth, (int)gallery.ActualHeight, 96, 96, PixelFormats.Pbgra32); gb.Render((Visual)gallery.Content);
                    var ge = new PngBitmapEncoder(); ge.Frames.Add(BitmapFrame.Create(gb)); using (var gs = File.Create(Path.Combine(output, "gallery.png"))) ge.Save(gs);
                    roomFilter.IsDropDownOpen = true; await Task.Delay(500);
                    report.Add($"combo height {roomFilter.ActualHeight:0}");
                    gallery.Close();
                }
                var tree = Find<TreeView>("ResourceTree");
                var items = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).ToList();
                string KindOf(TreeViewItem i) => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() ?? "";
                int? IdOf(TreeViewItem i) => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag);
                Call("SelectResourceInTree", items.First(i => KindOf(i) == "Room" && IdOf(i) == 17).Tag);
                await Task.Delay(2500);
                Call("SelectResourceInTree", items.First(i => KindOf(i) == "Room" && IdOf(i) == 3).Tag);
                await Task.Delay(2500);
                var opts = Find<Expander>("RoomOptionsExpander");
                report.Add($"options expander: expanded {opts.IsExpanded}; doorway radios {Find<FrameworkElement>("DoorwayOptions").Visibility}, object radios {Find<FrameworkElement>("ObjectAreaOptions").Visibility}");
                SaveWindow("options-collapsed.png");
                opts.IsExpanded = true; await Task.Delay(300);
                var navOverlay = Find<Canvas>("RoomNavigationOverlay"); var hot = Find<Canvas>("HotspotHighlightOverlay");
                report.Add($"defaults: doorway overlay {navOverlay.Visibility} ({navOverlay.Children.Count}), object overlay {hot.Visibility} ({hot.Children.Count})");
                Find<RadioButton>("DoorwaysAlways").IsChecked = true; await Task.Delay(500);
                report.Add($"doorways Always: overlay {navOverlay.Visibility} children {navOverlay.Children.Count}");
                Find<RadioButton>("ObjectsAlways").IsChecked = true; await Task.Delay(500);
                report.Add($"objects Always: overlay {hot.Visibility} children {hot.Children.Count}");
                SaveWindow("options-always.png");
                Find<RadioButton>("DoorwaysHover").IsChecked = true; Find<RadioButton>("ObjectsNever").IsChecked = true; await Task.Delay(500);
                report.Add($"doorways Hover (nothing hovered): children {navOverlay.Children.Count}; objects Never: overlay {hot.Visibility}");
                Find<RadioButton>("DoorwaysNever").IsChecked = true; Find<RadioButton>("ObjectsHover").IsChecked = true; await Task.Delay(300);
                report.Add("saved options file: " + string.Join(" | ", File.ReadAllLines(Path.Combine(Path.GetDirectoryName((string)typeof(MainWindow).GetField("LastFolderFile", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)!, "view-options.txt"))));
                Call("SelectResourceInTree", items.First(i => KindOf(i) == "Room" && IdOf(i) == 17).Tag);
                await Task.Delay(2000);
                var group = Find<FrameworkElement>("RoomDisplayGroup");
                report.Add($"normal: room view {group.ActualWidth:0}x{group.ActualHeight:0}; note '{Find<TextBlock>("ObjectsNote").Text}'; verb tooltip {Find<Border>("VerbStripBorder").ToolTip ?? "none"}");
                var fs = Find<Button>("FullscreenButton");
                report.Add($"full-screen button before hover: {fs.Visibility}");
                Call("VerbStrip_MouseEnter", null, null);
                report.Add($"after hover: {fs.Visibility}");
                SaveWindow("normal-hover.png");
                Call("Fullscreen_Click", fs, new RoutedEventArgs());
                await Task.Delay(1000);
                window.UpdateLayout();
                report.Add($"full screen: state {window.WindowState}, style {window.WindowStyle}, header {Find<FrameworkElement>("AppHeader").Visibility}, side {Find<FrameworkElement>("SidePanel").Visibility}, room view {group.ActualWidth:0}x{group.ActualHeight:0}, window {window.ActualWidth:0}x{window.ActualHeight:0}");
                SaveWindow("fullscreen.png");
                Call("SetFullscreen", false);
                await Task.Delay(800);
                report.Add($"left full screen: state {window.WindowState}, style {window.WindowStyle}, header {Find<FrameworkElement>("AppHeader").Visibility}, side {Find<FrameworkElement>("SidePanel").Visibility}, room view {group.ActualWidth:0}x{group.ActualHeight:0}");
                // Advanced: the inspector on Room 02 after one object action
                var adv0 = Find<CheckBox>("AdvancedModeToggle");
                adv0.IsChecked = true; Call("AdvancedModeToggle_Click", adv0, new RoutedEventArgs());
                Call("SelectResourceInTree", items.First(i => KindOf(i) == "Room" && IdOf(i) == 2).Tag);
                await Task.Delay(2500);
                var sess = (LoomExplorationSession)Field("_session")!;
                sess.ActivateObjectVerb(154, 0x36);
                for (var i = 0; i < 90; i++) sess.AdvanceFrame();
                Call("ShowLastObjectAction", 2);
                await Task.Delay(500);
                // Interaction menu: in the right panel, and the scene runs on its own clock after a pick
                {
                    var recs = (System.Collections.IEnumerable)Field("_currentRoomObjectRecords")!;
                    var hoja = recs.Cast<object>().First(r => (int)r.GetType().GetProperty("Id")!.GetValue(r)! == 154);
                    int hx = (int)hoja.GetType().GetProperty("X")!.GetValue(hoja)! + 2, hy = (int)hoja.GetType().GetProperty("Y")!.GetValue(hoja)! + 2;
                    var choices = Call("FindVerbChoicesAt", hx, hy)!;
                    Call("ShowObjectVerbChoices", choices);
                    await Task.Delay(300);
                    var panelList = Find<StackPanel>("ObjectActionsList");
                    report.Add($"actions: section {Find<Expander>("ObjectActionsSection").Visibility}, header '{((TextBlock)Find<Expander>("ObjectActionsSection").Header).Text}', rows {string.Join(" | ", panelList.Children.OfType<Button>().Select(b => b.Content))}; center panel {Find<FrameworkElement>("ObjectActionPanel").Visibility}");
                    SaveWindow("actions.png");
                    var s0 = (LoomExplorationSession)Field("_session")!;
                    var jBefore = s0.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x02F]");
                    panelList.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(2500);
                    var jAfter = s0.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x02F]");
                    report.Add($"after a pick, with no mouse movement: timer {jBefore} -> {jAfter}; playback running {Field("_playback") is not null}");
                }
                foreach (var n in new[] { "RoomEntryTraceExpander", "ObjectActionTraceExpander", "StateIssuesSection" })
                {
                    var ex = Find<Expander>(n); report.Add($"{n}: visible {ex.Visibility}, header '{ex.Header}'"); ex.IsExpanded = true;
                }
                await Task.Delay(500);
                report.Add($"entry summary: {Find<TextBlock>("RoomEntrySummary").Text}; event rows {Find<StackPanel>("RoomEntryEventButtons").Children.Count}");
                report.Add($"status text {Find<TextBlock>("SessionStatusText").Visibility}, objects note {Find<TextBlock>("ObjectsNote").Visibility}, last action: {Find<TextBlock>("LastActionSummary").Text}");
                foreach (var tb in Find<StackPanel>("StateIssueList").Children.OfType<TextBlock>()) report.Add("  ISSUE: " + tb.ToolTip);
                SaveWindow("inspector.png");
                // Advanced: scripts
                var adv = Find<CheckBox>("AdvancedModeToggle");
                adv.IsChecked = true; Call("AdvancedModeToggle_Click", adv, new RoutedEventArgs());
                var s91 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 91);
                Call("SelectResourceInTree", s91.Tag);
                await Task.Delay(1200);
                var toggle = Find<CheckBox>("ScriptBytesToggle");
                report.Add($"script 91 default: bytecode toggle {toggle.IsChecked}");
                toggle.IsChecked = true; await Task.Delay(300);
                var firstLines = (string t) => string.Join(" / ", t.Split('\n').Skip(6).Take(2).Select(l => l.TrimEnd()));
                report.Add($"script 91: bytecode on -> {firstLines(Find<TextBox>("ScriptOutput").Text)}; play button {Find<Button>("ScriptPlayButton").Visibility}");
                SaveWindow("script-bytes.png");
                toggle.IsChecked = false;
                await Task.Delay(300);
                report.Add($"bytecode off -> {firstLines(Find<TextBox>("ScriptOutput").Text)}");
                var s92 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 92);
                Call("SelectResourceInTree", s92.Tag);
                await Task.Delay(800);
                report.Add($"next script keeps the choice: toggle {toggle.IsChecked}, output {firstLines(Find<TextBox>("ScriptOutput").Text)}");
                SaveWindow("script-nobytes.png");
                var s5 = items.First(i => KindOf(i) == "Script" && IdOf(i) == 5);
                Call("SelectResourceInTree", s5.Tag);
                await Task.Delay(600);
                report.Add($"script 05 (not a scene): play button {Find<Button>("ScriptPlayButton").Visibility}");
                // Play as cutscene
                Call("SelectResourceInTree", s91.Tag);
                await Task.Delay(800);
                Call("ScriptPlay_Click", null, null);
                await Task.Delay(3000);
                var session = (LoomExplorationSession)Field("_session")!;
                report.Add($"play as cutscene: session room {session.Snapshot.RoomId}, in scene {session.InCutscene}, playback {(Field("_playback") is not null)}, selected view room {Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"))}");
                SaveWindow("script-played.png");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

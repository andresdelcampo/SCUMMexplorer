using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(".verification/options-layout-20261011/out");
        Directory.CreateDirectory(output);
        // Prevent an earlier harness run from reopening a game before this run selects its room.
        File.Delete(Path.Combine(output, "profile", "last-folder.txt"));
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        var report = new List<string>();
        int failures = 0;
        T Find<T>(string name) => (T)window.FindName(name);
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Check(bool ok, string message)
        {
            report.Add((ok ? "PASS " : "FAIL ") + message);
            if (!ok) failures++;
        }
        void Save(string name)
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var pixels = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            pixels.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pixels));
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(stream);
        }
        void SelectRadio(string name)
        {
            var radio = Find<RadioButton>(name);
            var peer = new RadioButtonAutomationPeer(radio);
            ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)).Select();
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", Path.GetFullPath(args.Length > 0 ? args[0] : "Games/loom ega en"));
                var game = (GameLibrary)Field("_game")!;
                var room = game.Rooms.First(r => r.ResourceId == 3);
                Call("SelectResourceInTree", room);
                for (int i = 0; i < 200 && !Equals(Field("_currentRoomResource"), room); i++) await Task.Delay(100);
                Check(Equals(Field("_currentRoomResource"), room), "Room 03 loaded in the real MainWindow");
                await Task.Delay(500);
                var options = Find<Expander>("RoomOptionsExpander");
                options.ApplyTemplate();
                var header = (System.Windows.Controls.Primitives.ToggleButton)options.Template.FindName("HeaderToggle", options);
                var headerPeer = new ToggleButtonAutomationPeer(header);
                var headerToggle = (IToggleProvider)headerPeer.GetPattern(PatternInterface.Toggle);
                options.IsExpanded = false;
                headerToggle.Toggle();
                Check(options.IsExpanded, "Options header opens its expander through the real toggle");
                headerToggle.Toggle();
                Check(!options.IsExpanded, "Options header closes its expander");
                options.IsExpanded = true;
                SelectRadio("DoorwaysAlways");
                Check(Find<RadioButton>("DoorwaysAlways").IsChecked == true && Find<RadioButton>("DoorwaysNever").IsChecked == false, "Doorway radio choices remain exclusive");
                window.UpdateLayout();
                Check(Find<Canvas>("RoomNavigationOverlay").Visibility == Visibility.Visible, "Always enables the room's doorway overlay");
                SelectRadio("ObjectsAlways");
                Check(Find<RadioButton>("ObjectsAlways").IsChecked == true && Find<RadioButton>("ObjectsNever").IsChecked == false, "Hotspot radio choices remain exclusive");
                Check(Find<Canvas>("HotspotHighlightOverlay").Visibility == Visibility.Visible, "Always shows the object overlay");
                SelectRadio("ObjectsNever");
                Check(Find<Canvas>("HotspotHighlightOverlay").Visibility == Visibility.Collapsed, "Never removes the object overlay");
                SelectRadio("DoorwaysHover");
                var framing = Find<CheckBox>("NormalRoomWidthToggle");
                ((IToggleProvider)new CheckBoxAutomationPeer(framing).GetPattern(PatternInterface.Toggle)).Toggle();
                Check(framing.IsChecked == false, "Styled framing checkbox still toggles panorama");
                framing.IsChecked = true;
                foreach (var (width, height) in new[] { (1180, 760), (900, 600) })
                foreach (var advanced in new[] { false, true })
                {
                    window.Width = width;
                    window.Height = height;
                    Find<CheckBox>("AdvancedModeToggle").IsChecked = advanced;
                    Call("UpdateAdvancedMode");
                    await Task.Delay(150);
                    window.UpdateLayout();
                    var bar = Find<FrameworkElement>("RoomOptionsBar");
                    var viewport = Find<FrameworkElement>("RoomViewportHost");
                    report.Add($"Layout {width}x{height} {(advanced ? "advanced" : "normal")}: options {options.ActualWidth:0}x{options.ActualHeight:0}; viewport {viewport.ActualWidth:0}x{viewport.ActualHeight:0}");
                    Check(framing.IsVisible, $"{width} {(advanced ? "advanced" : "normal")} original framing is visible when expanded");
                    foreach (var name in new[] { "NormalRoomWidthToggle", "DoorwaysAlways", "ObjectsAlways", "RoomMaskToggle", "WalkBoxToggle" })
                    {
                        var control = Find<FrameworkElement>(name);
                        if (!control.IsVisible) continue;
                        var rect = new Rect(control.TranslatePoint(new Point(), bar), control.RenderSize);
                        Check(rect.Left >= -0.5 && rect.Right <= bar.ActualWidth + 0.5 && rect.Bottom <= bar.ActualHeight + 0.5, $"{width} {(advanced ? "advanced" : "normal")} {name} fits inside the options");
                    }
                    Check(viewport.ActualHeight > 90, $"{width} {(advanced ? "advanced" : "normal")} expanded options leave the scene visible");
                    Check(Find<CheckBox>("RoomMaskToggle").Visibility == (advanced ? Visibility.Visible : Visibility.Collapsed), "Depth control follows mode visibility");
                    Save($"{width}-{(advanced ? "advanced" : "normal")}-expanded");
                    options.IsExpanded = false;
                    Save($"{width}-{(advanced ? "advanced" : "normal")}-collapsed");
                    Check(!framing.IsVisible, $"{width} {(advanced ? "advanced" : "normal")} original framing is hidden when collapsed");
                    options.IsExpanded = true;
                }
                window.Width = 1180;
                window.Height = 760;
                Find<CheckBox>("AdvancedModeToggle").IsChecked = false;
                Call("UpdateAdvancedMode");
                SelectRadio("CostumesTab");
                Call("NormalCatalogTab_Click", Find<RadioButton>("CostumesTab"), new RoutedEventArgs());
                Check(Find<ScrollViewer>("CostumeCardScroll").Visibility == Visibility.Visible && Find<ScrollViewer>("RoomCardScroll").Visibility == Visibility.Collapsed, "Costumes tab opens the costume catalog");
                await Task.Delay(500);
                Save("costumes-tab");
                SelectRadio("RoomsTab");
                Call("NormalCatalogTab_Click", Find<RadioButton>("RoomsTab"), new RoutedEventArgs());
                Check(Find<RadioButton>("CostumesTab").IsChecked == false && Find<ScrollViewer>("RoomCardScroll").Visibility == Visibility.Visible, "Rooms tab restores the room catalog");
                var saved = File.ReadAllLines(Path.Combine(output, "profile", "view-options.txt"));
                Check(saved.Contains("optionsOpen=1") && saved.Contains("framing=1") && saved.Contains("doorways=Hover"), "Existing option persistence is retained");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally
            {
                report.Add($"{failures} failures; isolated profile, no user's saved settings touched.");
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
                Environment.ExitCode = failures == 0 ? 0 : 1;
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Relationships;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(args[1], "cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1500, Height = 950, ShowActivated = false, Left = -2000 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        var report = new List<string>();
        void Check(bool ok, string message) { report.Add((ok ? "PASS " : "FAIL ") + message); }
        void Save(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(args[1], name + ".png")); encoder.Save(output);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var edition in new[] { "loom ega mt32 en", "loom vga en" })
                {
                    bool cd = edition.Contains("vga"); string prefix = cd ? "vga" : "ega";
                    Call("OpenGameFolder", Path.Combine(args[0], edition));
                    Find<ToggleButton>("AdvancedModeToggle").IsChecked = true;
                    Call("AdvancedModeToggle_Click", Find<ToggleButton>("AdvancedModeToggle"), new RoutedEventArgs());
                    var game = (GameLibrary)Field("_game")!;
                    for (int i = 0; i < 200 && ((LoomUsageIndex)Field("_usageIndex")!).UsedBy(LoomUsageTarget.Script, 16).Count == 0; i++) await Task.Delay(25);
                    for (int i = 0; i < 200 && ((LoomRelationshipIndex?)Field("_relationshipIndex"))?.Relationships.Count is null or 0; i++) await Task.Delay(25);
                    foreach (var (id, expected) in new[] { (5, "input script"), (7, "run a sentence (verb on an object)"), (16, "set the text line") })
                    {
                        var resource = game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == id && r.ScriptRoom is null);
                        Call("SelectResourceInTree", resource);
                        Find<CheckBox>("ScriptReadableToggle").IsChecked = true;
                        await Task.Delay(100);
                        var text = Find<TextBox>("ScriptOutput").Text;
                        Check(Find<TextBlock>("PageTitle").Text.Contains(expected), $"{prefix} Script {id:00} page title");
                        var tree = Find<TreeView>("ResourceTree");
                        var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).Single(i => ReferenceEquals(i.Tag, resource));
                        Check(item.Header.ToString()!.Contains(expected), $"{prefix} Script {id:00} sidebar title");
                        Check(Find<Expander>("ScriptUsedByExpander").Visibility == Visibility.Visible, $"{prefix} Script {id:00} Used by shown");
                        Check(Find<Button>("ConnectionsButton").Visibility == Visibility.Visible && Find<Button>("ConnectionsButton").IsEnabled,
                            $"{prefix} Script {id:00} Explore links available");
                        if (id == 7) Check(text.Contains("object under the pointer or clicked"), prefix + " Script 07 clicked-object role");
                        if (id == 16) Check(text.Contains("Bobbin") && text.Contains("at 160, text line y"), prefix + " Script 16 readable actor and layout");
                        if (id == 16) Check(Find<StackPanel>("ScriptUsedByList").Children.Count > 0, prefix + " Script 16 references populated");
                        if (cd && id == 16) Check(Find<StackPanel>("ScriptEventButtons").Children.OfType<TextBlock>().Any(t => t.Text.Contains("text layout")),
                            "vga Script 16 layout is not counted as spoken dialogue");
                        Save($"{prefix}-script{id:00}");
                        Find<CheckBox>("ScriptReadableToggle").IsChecked = false;
                        Check(Find<TextBox>("ScriptOutput").Text.Contains(cd ? "DISK01.LEC" : ".LFL"), prefix + " Raw original source");
                        Find<CheckBox>("ScriptReadableToggle").IsChecked = true;
                        if (cd && id == 7)
                        {
                            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                            timer.Tick += (_, _) =>
                            {
                                timer.Stop();
                                var popup = app.Windows.OfType<Window>().Single(w => w.Title.StartsWith("Connections"));
                                popup.UpdateLayout();
                                var bitmap = new RenderTargetBitmap((int)popup.ActualWidth, (int)popup.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                                bitmap.Render(popup); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                using (var file = File.Create(Path.Combine(args[1], "vga-links07.png"))) encoder.Save(file);
                                var list = (StackPanel)((ScrollViewer)((DockPanel)popup.Content).Children.OfType<ScrollViewer>().Single()).Content;
                                var link = list.Children.OfType<Button>().Single(b => b.Tag is GameResource { Kind: ResourceKind.Script, ResourceId: 8, ScriptRoom: null });
                                link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            };
                            timer.Start(); Call("ConnectionsButton_Click", Find<Button>("ConnectionsButton"), new RoutedEventArgs());
                            Check(Find<TextBlock>("PageTitle").Text.Contains("show or hide the close-up"), "vga Explore links navigates from Script 07 to Script 08");
                        }
                    }
                }
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                app.Shutdown(report.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
            }
            catch (Exception exception)
            {
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report.Append(exception.ToString())); app.Shutdown(1);
            }
        };
        app.Run(window);
    }
}

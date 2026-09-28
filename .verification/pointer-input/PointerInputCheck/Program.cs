using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// "Pointer as game input": in the real MainWindow, gives room points to the
// handler's own path (HotspotRowRecordAt, then SendPointerToGame, as
// RoomPreview_MouseMove does) and checks what the session and the verb-area panel
// show. WPF synthetic mouse events carry no position, so points are room pixels.
// No physical input. Arguments: <Games\loom> <output folder>.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Save(string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; room points are given to the handler's own methods. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var toggle = (CheckBox)window.FindName("GamePointerToggle");
                var panel = (FrameworkElement)window.FindName("VerbAreaPanel");
                var image = (Image)window.FindName("VerbAreaImage");
                var text = (TextBlock)window.FindName("VerbAreaText");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                LoomVerbSnapshot? CloseUp() => Session().Verbs.FirstOrDefault(verb => verb.VerbId == 0x36);
                string Var(string name) => Session().Snapshot.KnownGlobalVariables.TryGetValue(name, out var v) ? v.ToString() : "?";
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                }
                int? RowAt(int x, int y) => Call("HotspotRowRecordAt", x, y) is { } record
                    ? (int)record.GetType().GetProperty("Id")!.GetValue(record)! : null;
                void Hover(int x, int y) => Call("SendPointerToGame", ((int, int)?)(x, y), (int?)(RowAt(x, y) ?? 0));
                void Leave() { Call("RoomPreview_MouseLeave", null, null); window.UpdateLayout(); }

                await Open(2);
                Check(Session().Snapshot.RoomId == 2, $"Room 02 is the session's room after the boot (room {Session().Snapshot.RoomId})");

                // Off (the default): nothing reaches the game.
                Check(toggle.IsChecked == false, "the setting is off by default");
                var before = Var("var[0x066]");
                Hover(700, 40);
                window.UpdateLayout();
                Check(Session().Pointer is null && Var("var[0x066]") == before && panel.Visibility != Visibility.Visible,
                    $"off: hovering (700, 40) gives no pointer to the game (pointer {Session().Pointer?.ToString() ?? "none"}, var[0x066] {Var("var[0x066]")}, panel {panel.Visibility})");

                // On: the hover runs script 006's hover path.
                toggle.IsChecked = true;
                Hover(700, 40);
                window.UpdateLayout();
                var closeUp = CloseUp();
                Check(Session().Pointer == (700, 40), $"on: the session's pointer is (700, 40) ({Session().Pointer})");
                Check(Var("var[0x066]") == "156" && Var("var[0x068]") == "159",
                    $"on: script 008 ran for object 156 (row under the pointer: {RowAt(700, 40)}): var[0x066] = {Var("var[0x066]")}, var[0x068] = {Var("var[0x068]")} (object 156 verb 0x39 sets 159)");
                Check(closeUp is { Mode: 1, ImageObject: 159, ImageRoom: 2, X: 248, Y: 152 },
                    $"on: verb 0x36 is on with object 159's image from Room 02 at (248, 152): {closeUp?.Describe()}");
                Check(panel.Visibility == Visibility.Visible && image.Source is BitmapSource { PixelWidth: 48, PixelHeight: 40 },
                    $"on: the verb-area panel shows a 48x40 close-up ({(image.Source as BitmapSource)?.PixelWidth}x{(image.Source as BitmapSource)?.PixelHeight}): \"{text.Text}\"");
                Save("room-02-hover-156.png");

                // Same object: the pointer moves, but no new frame is needed.
                var pending = Session().Snapshot.Uncertainties.Count;
                Hover(702, 42);
                Check(Session().Pointer == (702, 42) && Session().Snapshot.Uncertainties.Count == pending,
                    $"on: moving within object 156 updates the pointer ({Session().Pointer}) without a new frame");

                // Leaving the room: no pointer; script 006 starts script 008 with 0, which turns the close-up off.
                Leave();
                closeUp = CloseUp();
                Check(Session().Pointer is null && Var("var[0x066]") == "0" && closeUp is { Mode: 0 } && panel.Visibility != Visibility.Visible,
                    $"leave: no pointer, var[0x066] = {Var("var[0x066]")}, verb 0x36 {closeUp?.Describe()}, panel {panel.Visibility}");
                Check(Session().Snapshot.Uncertainties.Any(line => line.Contains("no pointer over the room", StringComparison.Ordinal)),
                    "leave: the next frame reads the \"no pointer over the room\" assumption again");

                // Room 03 (catalog visit): object 165's close-up is object 166.
                await Open(3);
                var records = ((System.Collections.IEnumerable)Field("_currentRoomObjectRecords")!).Cast<object>().ToArray();
                int Prop(object record, string name) => (int)record.GetType().GetProperty(name)!.GetValue(record)!;
                var lake = records.First(r => Prop(r, "Id") == 165);
                var (x165, y165) = (Prop(lake, "X") + Prop(lake, "Width") / 2, Prop(lake, "Y") + Prop(lake, "Height") / 2);
                Hover(x165, y165);
                window.UpdateLayout();
                closeUp = CloseUp();
                Check(Session().Snapshot.RoomId == 3 && Var("var[0x066]") == "165" && closeUp is { Mode: 1, ImageObject: 166 } && panel.Visibility == Visibility.Visible,
                    $"Room 03: hovering object 165 at ({x165}, {y165}) (row {RowAt(x165, y165)}) shows object 166's close-up: var[0x066] = {Var("var[0x066]")}, {closeUp?.Describe()}");
                Save("room-03-hover-165.png");

                // A doorway under the pointer is hovered, not clicked: the room does not change.
                Hover(24, 24);
                window.UpdateLayout();
                Check(Session().Snapshot.RoomId == 3,
                    $"Room 03: hovering object 162 (a doorway) at (24, 24) (row {RowAt(24, 24)}) sends no click; the room stays 03; var[0x066] = {Var("var[0x066]")}, {CloseUp()?.Describe()}");

                // Turning the setting off takes the pointer away and hides the panel.
                toggle.IsChecked = false;
                window.UpdateLayout();
                Check(Session().Pointer is null && panel.Visibility != Visibility.Visible,
                    $"off again: pointer {Session().Pointer?.ToString() ?? "none"}, panel {panel.Visibility}");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "pointer-input-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "pointer-input-check.txt")));
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fallback)!);
                        System.IO.File.WriteAllLines(fallback, report, System.Text.Encoding.UTF8);
                        Console.Error.WriteLine($"Report not written ({writeError.Message}); wrote {fallback} instead.");
                    }
                    catch (Exception fallbackError) when (fallbackError is System.IO.IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Report not written ({writeError.Message}; {fallbackError.Message}):");
                        foreach (var line in report) Console.Error.WriteLine(line);
                    }
                    Environment.ExitCode = 2;
                }
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

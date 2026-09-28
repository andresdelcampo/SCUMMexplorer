using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// The game's click (the explore mode's default since 28 September): in the real
// MainWindow, gives room points to the handler's own paths (HandleRoomPoint, which
// the double-click timer calls; QueueGameClick and the timer; the mouse-down
// pairing) and checks where the session goes and what it says. Inspection mode
// cycles artwork instead. WPF synthetic mouse events carry no position,
// so points are room pixels. No physical input. Arguments: <Games\loom> <output folder>.
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
                var explore = (CheckBox)window.FindName("SessionModeToggle");
                var status = (TextBlock)window.FindName("StatusText");
                var panel = (FrameworkElement)window.FindName("VerbAreaPanel");
                var panelText = (TextBlock)window.FindName("VerbAreaText");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    await Settle(room);
                }
                async Task Settle(int room)
                {
                    for (int n = 0; n < 100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) != room; n++)
                        await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                }
                bool Traced(string text) => Session().SessionTrace.Any(line => line.Contains(text, StringComparison.Ordinal));

                await Open(2);
                Check(Session().Snapshot.RoomId == 2, $"Room 02 is the session's room after the boot (room {Session().Snapshot.RoomId})");
                Check(explore.IsChecked == true && window.FindName("GameClickToggle") is null,
                    "the explore mode is on by default and has no \"Click as game input\" setting: the game's click is the click");

                // A click on object 153 goes through scripts 005, 006 and 007 to Room 03.
                Call("HandleRoomPoint", 450, 60, false);
                await Settle(3);
                var action = Session().LastAction;
                Check(Session().Snapshot.RoomId == 3 && action is { IsGameInput: true, DestinationRoomId: 3 },
                    $"a click at (450, 60) in Room 02 reaches Room 03 through the game's input: {action?.Evidence}");
                Check(Traced("runs script 5 with [2, 0, 0]") && Traced("Script 05 chains to script 6") &&
                      Traced("Script 07 +0x002E: startObject runs object 153 verb 0x38"),
                    "the trace shows runInputScript → script 005 → script 006 → script 007 → object 153 verb 0x38");
                var note = (TextBlock)window.FindName("ObjectActionNote");
                Check(note.IsVisible && note.Text.Contains("new scene", StringComparison.OrdinalIgnoreCase) &&
                      note.Text.Contains("preview", StringComparison.OrdinalIgnoreCase),
                    $"the contextual action card labels the scene change and preview limits: \"{note.Text}\" (status \"{status.Text}\")");
                Save("room-03-after-click.png");

                // The double-click timer: a queued single click is sent once the double-click time passes.
                Call("QueueGameClick", 24, 24);
                Check(Session().Snapshot.RoomId == 3, "a queued click waits for the double-click time before it is sent");
                await Task.Delay(1500);
                await Settle(4);
                action = Session().LastAction;
                Check(Session().Snapshot.RoomId == 4 && action is { IsGameInput: true } &&
                      action.Evidence.Contains("Room 03 local script 200", StringComparison.Ordinal),
                    $"after the wait, the click at (24, 24) reaches Room 04 through object 162 and local 200: {action?.Evidence}");
                Save("room-04-after-click.png");

                // A second press while the first waits: a double click (the mouse-down pairing), here on Room 02 object 156.
                await Open(2);
                Call("QueueGameClick", 700, 40);
                Call("RoomPreview_MouseLeftButtonDown", null, null);
                Call("RoomPreview_MouseLeftButtonUp", null, null);
                window.UpdateLayout();
                Check(Traced("the second click of a double click") || Session().Snapshot.Uncertainties.Any(line => line.Contains("the second click of a double click", StringComparison.Ordinal)),
                    "a second press while the first click waits is sent as a double click");
                Check(Traced("startObject runs object 156 verb 0x36"),
                    "the double click on object 156 runs its verb 0x36 through script 007");
                Check(panel.Visibility == Visibility.Visible && panelText.Text.Contains("assumed click", StringComparison.Ordinal),
                    $"the verb-area panel shows the close-up reached from the click: \"{panelText.Text}\"");
                Save("room-02-double-click-156.png");

                // Inspection mode: a click cycles artwork and sends nothing to the game.
                var before = Session().LastAction;
                explore.IsChecked = false;
                Call("HandleRoomPoint", 700, 40, false);
                window.UpdateLayout();
                var objectsNote = (TextBlock)window.FindName("SessionStatusText");
                Check(Session().LastAction == before && Session().Snapshot.RoomId == 2,
                    "inspection: a click sends nothing to the game and stays in Room 02");
                Check(objectsNote.Text.StartsWith("Inspection mode: a click cycles an object's stored images", StringComparison.Ordinal),
                    $"inspection: the mode's text says clicks cycle the stored artwork: \"{objectsNote.Text}\" (status \"{status.Text}\")");
                explore.IsChecked = true;
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "click-input-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    var fallback = Path.Combine(Path.GetTempPath(), "SCUMMexplorer-harness", "click-input-check.txt");
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                        File.WriteAllLines(fallback, report, System.Text.Encoding.UTF8);
                        Console.Error.WriteLine($"Report not written ({writeError.Message}); wrote {fallback} instead.");
                    }
                    catch (Exception fallbackError) when (fallbackError is IOException or UnauthorizedAccessException)
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

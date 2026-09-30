using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// The cutscene menu per room, in the real MainWindow: rooms are picked in the resource tree (catalog visits, interactive
// mode on), the menu is built as its button builds it (BuildCutsceneMenu), and its entries are compared with the rooms
// each known cutscene starts in or loads. Last step: in Room 37 the firewood entry is clicked (MenuItem.Click raised,
// no physical input) and the cutscene must start. Arguments: <Games\loom> <output folder>.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application();
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        object? Property(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        int? CurrentRoom() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
        const string Opening = "The opening (script 044)", S052 = "Script 052: Room 10 → Room 11 → Room 13",
            Trial = "Script 046: the Elders' trial", Firewood = "Script 086: the firewood scene (Room 37)";
        var expected = new (int Room, string[] Titles)[]
        {
            (1, [Opening]), (2, [Opening]), (53, [Opening]), (8, [Trial]), (9, [Trial]), (3, [Trial]), (10, [S052]),
            (11, [S052, Trial]), (13, [S052]), (36, [Firewood]), (37, [Firewood]), (38, [Firewood]), (20, []), (41, []),
        };
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; rooms picked in the tree, the menu built by BuildCutsceneMenu (as its button does), the Room 37 entry clicked by raising MenuItem.Click. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                ((CheckBox)window.FindName("SessionModeToggle")).IsChecked = true;
                for (var n = 0; n < 100 && Field("_currentRoomResource") is null; n++) await Task.Delay(100);
                ContextMenu? last = null;
                foreach (var (room, titles) in expected)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && CurrentRoom() != room; n++) await Task.Delay(100);
                    var menu = (ContextMenu)Call("BuildCutsceneMenu")!;
                    var entries = menu.Items.OfType<MenuItem>().ToList();
                    report.Add($"Room {room:00} (view {CurrentRoom():00}):");
                    foreach (var entry in entries) report.Add($"   {(entry.IsEnabled ? "entry" : "note ")} {entry.Header}");
                    var shown = entries.Where(entry => entry.IsEnabled).Select(entry => (string)entry.Header).ToArray();
                    Check(CurrentRoom() == room && shown.SequenceEqual(titles),
                        $"Room {room:00} lists {(titles.Length == 0 ? "no cutscene" : string.Join(" + ", titles))}");
                    if (titles.Length == 0)
                        Check(entries.Count == 1 && !entries[0].IsEnabled && ((string)entries[0].Header).StartsWith($"No known cutscene plays in Room {room:00}.", StringComparison.Ordinal),
                            $"Room {room:00} shows one disabled note instead");
                    last = room == 37 ? menu : last;
                }

                var firewood = last!.Items.OfType<MenuItem>().Single();
                firewood.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var status = ((TextBlock)window.FindName("StatusText")).Text;
                await Task.Delay(3000);
                var session = (LoomExplorationSession)Field("_session")!;
                report.Add($"After the Room 37 entry: status \"{status}\"; 3 s later view room {CurrentRoom()}, session room {session.Snapshot.RoomId}, in scene {Property("SessionInScene")}, playback {Property("PlaybackRunning")}");
                Check(status.Contains("firewood", StringComparison.Ordinal) && (bool)Property("PlaybackRunning")!,
                    "the entry plays the firewood scene");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "cutscene-menu-check.txt"), report, System.Text.Encoding.UTF8);
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

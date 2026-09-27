using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// Drives the real MainWindow through steps and records the room music row after each.
// A step is "R[:expect]" (browse to Room R) or "+[:expect]" (press Continue once).
// expect is "none" (no Started cue), or a sound number N (the row shows "Started: â€¦ N"),
// optionally with "*" when the cue should be labelled as started on an earlier visit.
// Arguments: <Games\loom> <output folder> <steps>. Synthetic calls, no physical input.
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
        string Text(string name) => window.FindName(name) switch
        {
            TextBlock block => block.Text,
            _ => "?"
        };
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; synthetic tree selection and Continue clicks. No physical input." };
            var failures = 0;
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                report.Add($"Exploration mode: {((System.Windows.Controls.Primitives.ToggleButton)window.FindName("SessionModeToggle")).IsChecked}");
                var index = 0;
                foreach (var step in args.Skip(2))
                {
                    var parts = step.Split(':');
                    string label;
                    if (parts[0] == "+")
                    {
                        Call("AdvanceFrame_Click", null, new RoutedEventArgs());
                        label = "Continue";
                    }
                    else
                    {
                        var room = int.Parse(parts[0]);
                        var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                            .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                        Call("SelectResourceInTree", item.Tag);
                        for (int n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                        label = $"Room {room:00}";
                    }
                    await Task.Delay(300);
                    window.UpdateLayout();
                    var current = Field("_currentRoomResource");
                    var roomId = (int?)current?.GetType().GetProperty("ResourceId")?.GetValue(current);
                    var title = Text("RoomCueTitle");
                    var note = Text("RoomMusicNote");
                    var ok = true;
                    if (parts.Length > 1)
                    {
                        var expect = parts[1];
                        if (expect == "none") ok = !title.StartsWith("Started", StringComparison.Ordinal);
                        else
                        {
                            var earlier = expect.EndsWith('*');
                            ok = title.StartsWith("Started", StringComparison.Ordinal) &&
                                 title.EndsWith(" " + int.Parse(expect.TrimEnd('*')), StringComparison.Ordinal) &&
                                 note.Contains("earlier visit") == earlier;
                        }
                        if (!ok) failures++;
                    }
                    var file = $"step-{++index:00}-room-{roomId:00}{(parts[0] == "+" ? "-continue" : "")}.png";
                    Save(file);
                    report.Add($"{(parts.Length > 1 ? ok ? "PASS" : "FAIL" : "INFO")} {label} (now Room {roomId:00}), expect {(parts.Length > 1 ? parts[1] : "-")}: " +
                               $"title \"{title}\"; evidence \"{Text("RoomCueEvidence")}\" [{((TextBlock)window.FindName("RoomCueEvidence")).ToolTip}]; " +
                               $"note \"{note}\"; more \"{Text("RoomMusicMoreHeader")}\"; capture {file}");
                }
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "music-row-check.txt"), report, System.Text.Encoding.UTF8);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

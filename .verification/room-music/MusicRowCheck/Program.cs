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
            Button button => button.Content?.ToString() ?? "",
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
                    await Task.Delay(500);
                    window.UpdateLayout();
                    var current = Field("_currentRoomResource");
                    var roomId = (int?)current?.GetType().GetProperty("ResourceId")?.GetValue(current);
                    var title = Text("RoomCueTitle");
                    var note = Text("RoomMusicNote");
                    var more = Text("RoomMusicMoreButton");
                    var panelVisible = ((FrameworkElement)window.FindName("RoomMusicPanel")).Visibility == Visibility.Visible;
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
                    if (roomId == 4 && panelVisible)
                    {
                        ok = false;
                        failures++;
                        report.Add("FAIL no qualifying music should hide the player panel.");
                    }
                    if (roomId == 8)
                    {
                        var choices = (StackPanel)window.FindName("RoomMusicButtons");
                        var currentCue = Field("_roomSelectedCue");
                        var currentId = (int?)currentCue?.GetType().GetProperty("ResourceId")?.GetValue(currentCue);
                        var alternate = choices.Children.OfType<Button>().FirstOrDefault();
                        if (currentId != 31 || alternate is null)
                        {
                            failures++;
                            report.Add($"FAIL expected Sound 31 default plus an alternate; selected {currentId}, menu choices {choices.Children.Count}.");
                        }
                        else
                        {
                            alternate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, alternate));
                            await Task.Delay(600);
                            var defaultReturned = choices.Children.OfType<Button>()
                                .FirstOrDefault(button => button.Content?.ToString()?.Contains("Sound 31", StringComparison.Ordinal) == true);
                            var alternateId = (int?)Field("_roomSelectedCue")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_roomSelectedCue"));
                            if (alternateId == 31 || defaultReturned is null)
                            {
                                failures++;
                                report.Add($"FAIL alternate should become selected and Sound 31 should return to the menu; selected {alternateId}.");
                            }
                            else
                            {
                                defaultReturned.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, defaultReturned));
                                await Task.Delay(600);
                                var restoredId = (int?)Field("_roomSelectedCue")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_roomSelectedCue"));
                                if (restoredId != 31)
                                {
                                    failures++;
                                    report.Add($"FAIL choosing Sound 31 again should restore the default; selected {restoredId}.");
                                }
                                else report.Add("PASS Room 08: longest default selected; alternate can replace it and original Sound 31 can be selected again.");
                            }
                        }
                    }
                    window.UpdateLayout();
                    var file = $"step-{++index:00}-room-{roomId:00}{(parts[0] == "+" ? "-continue" : "")}.png";
                    Save(file);
                    report.Add($"{(parts.Length > 1 ? ok ? "PASS" : "FAIL" : "INFO")} {label} (now Room {roomId:00}), expect {(parts.Length > 1 ? parts[1] : "-")}: " +
                               $"title \"{title}\"; evidence \"{Text("RoomCueEvidence")}\" [{((TextBlock)window.FindName("RoomCueEvidence")).ToolTip}]; " +
                               $"note \"{note}\"; player visible {panelVisible}; more \"{more}\"; capture {file}");
                }
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "music-row-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "music-row-check.txt")));
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

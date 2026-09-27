using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var report = new List<string>();
        Directory.CreateDirectory(args[1]);
        var app = new Application();
        var main = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string method, params object[] values) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(main, values);
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(main);
        void Check(bool pass, string message) { if (!pass) throw new Exception(message); report.Add("PASS: " + message); }
        TextGalleryWindow? gallery = null;
        main.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var extraction = new LoomTextExtractor().Extract(game);
                report.Add($"Entries={extraction.Entries.Count}; ranges={extraction.ResourcesVisited}; unresolved={extraction.UnresolvedPaths}; skipped={extraction.ResourcesSkipped}");
                report.AddRange(extraction.Entries.GroupBy(e => e.Kind).Select(g => $"{g.Key}={g.Count()}"));
                var conversations = extraction.Entries.Where(e => e.ConversationId is not null)
                    .GroupBy(e => e.ConversationId).ToArray();
                report.Add($"DialogueGroups={conversations.Length}; groupedLines={conversations.Sum(g => g.Count())}; objectLinked={extraction.Entries.Count(e => e.ObjectId.HasValue)}");
                report.AddRange(conversations.GroupBy(group => group.First().ConversationKind)
                    .Select(group => $"{group.Key} groups={group.Count()}; entries={group.Sum(conversation => conversation.Count())}"));
                report.AddRange(extraction.Diagnostics);
                Check(extraction.Entries.All(entry =>
                {
                    var visible = Regex.Replace(entry.Text, @"\[(?:continue|pause|text control [^\]]+)\]", "").Trim();
                    return visible.Length > 1 && visible.Any(char.IsLetterOrDigit);
                }), "No empty, single-character or symbol-only text remains after ignoring control markers");
                gallery = new TextGalleryWindow(game, extraction, room => Call("SelectResourceInTree", game.Rooms.First(r => r.ResourceId == room))) { Owner = main };
                gallery.Show();
                T Named<T>(string name) where T : FrameworkElement => (T)gallery.FindName(name);
                void Capture(string filename)
                {
                    gallery.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)gallery.ActualWidth, (int)gallery.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(gallery);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(args[1], filename));
                    encoder.Save(stream);
                }
                await Task.Delay(300);
                var list = Named<ListBox>("TextList");
                var search = Named<TextBox>("SearchBox");
                Check(list.Items.Count == extraction.Entries.Count && list.Items.Count > 0, "Gallery exposes every extracted entry");
                search.Text = "acantilado";
                Check(list.Items.Count > 0 && list.Items.Count < extraction.Entries.Count, "Search narrows to matching original text");
                var rooms = Named<ComboBox>("RoomFilter");
                rooms.SelectedItem = rooms.Items.OfType<ComboBoxItem>().First(i => i.Tag is int id && id == 3);
                Check(list.Items.Count > 0, "Room 03 filter retains its cliff text");
                search.Text = "cielo";
                Check(list.Items.Cast<object>().Any(row =>
                    row.GetType().GetProperty("Entry")!.GetValue(row) is LoomTextEntry { ObjectId: 165, Kind: "Spoken line" }),
                    "Searching the stored object name finds its linked spoken lines");
                search.Text = "Primero me tendré";
                Check(list.Items.Count > 0 && Named<TextBlock>("SelectedText").Text.Contains("más"), "Spanish accents render as Unicode in original dialogue");
                Check(Named<TextBlock>("SelectedHeading").Text.Contains("165"), "Object 165 association is visible beside Room 03 without expanding source details");
                await Task.Delay(400);
                Check(Named<Image>("RoomArtworkPreview").Source is BitmapSource, "Linked original room artwork renders");
                var visit = Named<Button>("VisitRoomButton");
                Check(visit.IsVisible && visit.Focusable, "Room visit action visible and keyboard focusable");
                Capture("gallery.png");
                search.Text = "NO_SUCH_TEXT_928374";
                Check(list.Items.Count == 0 && Named<TextBlock>("SelectedText").Text.Length == 0 && !visit.IsVisible, "Empty search clears previous text and room action");
                search.Text = ""; rooms.SelectedIndex = 0;
                Named<ComboBox>("KindFilter").SelectedItem = Named<ComboBox>("KindFilter").Items.OfType<ComboBoxItem>()
                    .Single(item => item.Tag as string == "Object name");
                Check(list.Items.Count > 0 && list.Items.Count == extraction.Entries.Count(e => e.Kind == "Object name"), "Object-name filter includes extracted names");
                var kinds = Named<ComboBox>("KindFilter");
                kinds.SelectedItem = kinds.Items.OfType<ComboBoxItem>().Single(item => item.Tag as string == "Conversation");
                Check(conversations.Length > 0 && list.Items.Count == conversations.Length,
                    "Conversation view has one selectable row per extracted group");
                var conversation = conversations.First(group => group.First().ConversationKind == "Cutscene" && group.Count() >= 3 && group.Select(e => e.Text).Distinct().Count() >= 3);
                var lastLine = conversation.OrderBy(e => e.ConversationLine).Last();
                search.Text = lastLine.Text;
                Check(list.Items.Count > 0, "Searching a later cutscene line finds its group");
                var matchingRow = list.Items.Cast<object>().First(row =>
                    ((LoomTextEntry)row.GetType().GetProperty("Entry")!.GetValue(row)!).ConversationId == conversation.Key);
                list.SelectedItem = matchingRow;
                var transcript = Named<TextBlock>("SelectedText").Text;
                Check(conversation.All(line => transcript.Contains(line.Text)), "Search preserves the complete extracted group transcript");
                var orderedLines = conversation.OrderBy(e => e.ConversationLine).ToArray();
                Check(transcript.StartsWith($"{orderedLines[0].Speaker ?? "Unknown speaker"}: {orderedLines[0].Text}"),
                    "Transcript begins with the first stored line and its decoded speaker");
                Check(Named<TextBlock>("SelectedHeading").Text.Contains("source order", StringComparison.OrdinalIgnoreCase),
                    "Cutscene transcript visibly identifies source order");
                await Task.Delay(300);
                Capture("cutscene.png");
                report.Add($"Checked cutscene={conversation.Key}; lines={conversation.Count()}; source={lastLine.Source}");
                var scriptDialogue = conversations.First(group => group.First().ConversationKind == "Script dialogue" &&
                    group.Select(line => line.Speaker).Distinct().Count() >= 2);
                search.Text = scriptDialogue.Last().Text;
                list.SelectedItem = list.Items.Cast<object>().First(row =>
                    ((LoomTextEntry)row.GetType().GetProperty("Entry")!.GetValue(row)!).ConversationId == scriptDialogue.Key);
                Check(scriptDialogue.All(line => Named<TextBlock>("SelectedText").Text.Contains(line.Text)),
                    "Unmarked script dialogue exposes its complete extracted group with multiple speakers");
                Check(Named<TextBlock>("SelectedHeading").Text.Contains("Script dialogue in stored source order"),
                    "Unmarked dialogue is visibly distinguished from cutscene markers");
                await Task.Delay(300);
                Capture("script-dialogue.png");
                report.Add($"Checked script dialogue={scriptDialogue.Key}; lines={scriptDialogue.Count()}; source={scriptDialogue.First().Source}");
                Named<ComboBox>("KindFilter").SelectedIndex = 0;
                rooms.SelectedItem = rooms.Items.OfType<ComboBoxItem>().First(i => i.Tag is int id && id == 3);
                search.Text = "Primero me tendré";
                visit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (var i = 0; i < 100 && (Field("_currentRoomResource") as GameResource)?.ResourceId != 3; i++) await Task.Delay(100);
                Check((Field("_currentRoomResource") as GameResource)?.ResourceId == 3, "Visit room dispatch opens Room 03 in MainWindow");
                report.Add("Actual WPF windows rendered; synthetic routed button event used. Physical input not tested.");
            }
            catch (Exception ex) { report.Add("FAIL: " + ex); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(args[1], "window-check.txt"), report);
                gallery?.Close(); main.Close(); app.Shutdown();
            }
        };
        app.Run(main);
    }
}

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// M3c: "Play this cutscene" from Game Texts, in the real MainWindow and the real TextGalleryWindow.
// The gallery is built as GameTexts_Click builds it (the extraction, and the main window's GalleryCutscenes()),
// but shown modeless so the check can drive it. It checks which texts offer the button, then clicks it
// (ButtonBase.ClickEvent) on a line of script 046 with the interactive mode off, and on a line of script 044.
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
        var window = new MainWindow { Width = 1400, Height = 900 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        void SaveElement(FrameworkElement element, string file)
        {
            element.UpdateLayout();
            var width = Math.Max(1, (int)element.ActualWidth);
            var height = Math.Max(1, (int)element.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file));
            encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow and TextGalleryWindow; the gallery's \"Play this cutscene\" button is clicked through its Click event. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool HasPlayback() => Field("_playback") is not null;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                string Status() => Find<TextBlock>("StatusText").Text;
                for (var n = 0; n < 100 && Shown() is null; n++) await Task.Delay(100);

                var extraction = new LoomTextExtractor().Extract(game);
                TextGalleryWindow OpenGallery()
                {
                    var cutscenes = (IReadOnlyDictionary<int, GalleryCutscene>)Call("GalleryCutscenes")!;
                    var opened = new TextGalleryWindow(game, extraction, _ => { }, cutscenes) { Owner = window };
                    opened.Show();
                    return opened;
                }
                // Selects the first list item whose entry matches.
                bool Select(TextGalleryWindow gallery, Func<LoomTextEntry, bool> match)
                {
                    var list = (ListBox)gallery.FindName("TextList");
                    foreach (var item in list.Items)
                    {
                        var entry = (LoomTextEntry)item.GetType().GetProperty("Entry")!.GetValue(item)!;
                        if (!match(entry)) continue;
                        list.SelectedItem = item;
                        list.ScrollIntoView(item);
                        return true;
                    }
                    return false;
                }
                Button PlayButton(TextGalleryWindow gallery) => (Button)gallery.FindName("PlayCutsceneButton");

                // 1. Which texts offer the button.
                var cutsceneMap = (IReadOnlyDictionary<int, GalleryCutscene>)Call("GalleryCutscenes")!;
                report.Add($"Known cutscenes by script: {string.Join("; ", cutsceneMap.Select(pair => $"{pair.Key:000} = {pair.Value.Title}"))}");
                var byScript = extraction.Entries.Where(entry => entry.ScriptId is int id && cutsceneMap.ContainsKey(id))
                    .GroupBy(entry => entry.ScriptId!.Value).ToDictionary(group => group.Key, group => group.Count());
                report.Add($"Texts per known cutscene script: {string.Join(", ", byScript.Select(pair => $"{pair.Key:000}: {pair.Value}"))}; " +
                           $"script 052: {extraction.Entries.Count(entry => entry.ScriptId == 52)} (decoded: it prints none)");
                var gallery = OpenGallery();
                await Task.Delay(300);
                Check(Select(gallery, entry => entry.ScriptId == 44 && entry.Text.StartsWith("Lev", StringComparison.Ordinal)) &&
                      PlayButton(gallery).Visibility == Visibility.Visible,
                    $"a line of script 044 (the fairy) offers \"Play this cutscene\": {PlayButton(gallery).ToolTip}");
                await Task.Delay(300);
                SaveElement(gallery, "game-texts-044-button.png");
                Check(Select(gallery, entry => entry.ScriptId == 46) && PlayButton(gallery).Visibility == Visibility.Visible,
                    "a line of script 046 (the Elders' trial) offers it");
                Check(Select(gallery, entry => entry.ScriptId is null && entry.Kind == "Object name") && PlayButton(gallery).Visibility == Visibility.Collapsed,
                    "an object name offers no cutscene");
                Check(Select(gallery, entry => entry.ScriptId is int id && !cutsceneMap.ContainsKey(id)) && PlayButton(gallery).Visibility == Visibility.Collapsed,
                    "a line of another indexed script offers no cutscene");
                // Conversation groups carry their first line's script.
                var kind = (ComboBox)gallery.FindName("KindFilter");
                kind.SelectedIndex = 2;
                await Task.Delay(200);
                Check(Select(gallery, entry => entry.ScriptId == 46) && PlayButton(gallery).Visibility == Visibility.Visible,
                    "the trial's cutscene group (Conversations and cutscenes) offers it");
                kind.SelectedIndex = 0;
                await Task.Delay(200);

                // 2. Click it on a line of script 046 with the interactive mode off: the mode turns on and the trial plays.
                Find<CheckBox>("SessionModeToggle").IsChecked = false;
                Select(gallery, entry => entry.ScriptId == 46 && entry.CodeOffset == 0x002F);
                PlayButton(gallery).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                // The status is read at once: the room load that follows collapses and resets it (as after the menu).
                var trialStatus = Status();
                await Task.Delay(100);
                Check(!gallery.IsVisible, "the gallery closes on the click");
                Check(Find<CheckBox>("SessionModeToggle").IsChecked == true, "the interactive mode is turned on");
                Check(HasPlayback() && Session().InCutscene && trialStatus.Contains("Script 046", StringComparison.Ordinal),
                    $"the trial plays: status \"{trialStatus}\"");
                var clock = Stopwatch.StartNew();
                string? firstText = null;
                while (clock.ElapsedMilliseconds < 30000 && firstText is null)
                {
                    await Task.Delay(50);
                    var session = Session();
                    if (Shown() == session.Snapshot.RoomId && session.Messages.Talk is { } talk && talk.Lines.Any(line => line.Trim().Length > 0))
                        firstText = $"Room {session.Snapshot.RoomId:00}, {talk.SourceLabel} +0x{talk.Offset:X4}: {string.Join(" / ", talk.Lines)}";
                }
                await Task.Delay(150);
                SaveElement(Find<FrameworkElement>("RoomDisplayGroup"), "trial-from-game-texts-app.png");
                Check(firstText?.Contains("+0x002F", StringComparison.Ordinal) == true,
                    $"its first line is on screen after {clock.ElapsedMilliseconds} ms: {firstText}");
                Call("SkipCutscene");
                clock.Restart();
                while (clock.ElapsedMilliseconds < 20000 && HasPlayback()) await Task.Delay(100);
                Check(!HasPlayback() && !Session().InCutscene && Shown() == 9,
                    $"Escape ends it in Room 09 after {clock.ElapsedMilliseconds} ms (view Room {Shown():00})");

                // 3. Click it on a line of script 044: the opening plays from its stated start.
                gallery = OpenGallery();
                await Task.Delay(300);
                Select(gallery, entry => entry.ScriptId == 44 && entry.Text.StartsWith("Lev", StringComparison.Ordinal));
                PlayButton(gallery).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var openingStatus = Status();
                await Task.Delay(100);
                Check(!gallery.IsVisible && HasPlayback() && Session().BootRunning && openingStatus.Contains("The opening (script 044)", StringComparison.Ordinal),
                    $"the opening plays: status \"{openingStatus}\", boot stopped after {Session().BootFramesBeforeOpening} frames");
                clock.Restart();
                while (clock.ElapsedMilliseconds < 30000 && !(Shown() == 1 && Session().Snapshot.RoomId == 1 &&
                       Session().Actors.Count(actor => actor.Room == 1 && actor.Visible == true && actor.Number is >= 1 and <= 9) >= 3))
                    await Task.Delay(50);
                SaveElement(Find<FrameworkElement>("RoomDisplayGroup"), "opening-from-game-texts-app.png");
                Check(Shown() == 1, $"Room 01 is shown with its points of light after {clock.ElapsedMilliseconds} ms");
                Call("StopScenePlayback");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "game-texts-cutscene-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    foreach (var line in report) Console.Error.WriteLine(line);
                    Environment.ExitCode = 2;
                }
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

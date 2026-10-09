using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// M1c/M1d in the real MainWindow: a double click on Room 02 object 156 (the sky)
// through HandleRoomPoint (the game's click, assumed input) plays on the app's
// playback clock. The check follows the frames as they are drawn, captures the
// room view with its message layer, measures how long "Amanecer." stays, checks
// "cielo" in the verb screen, and clicks text away (the talk-stop key, assumed
// input). No physical input. Arguments: <Games\loom> <output folder>.
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
        void Save(string file)
        {
            window.UpdateLayout();
            var group = Find<FrameworkElement>("RoomDisplayGroup");
            // A VisualBrush draws the element at the origin (Render would keep its offset in the parent).
            var width = Math.Max(1, (int)group.ActualWidth);
            var height = Math.Max(1, (int)group.ActualHeight);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(group), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file));
            encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; room points are given to HandleRoomPoint and the text-skip handler. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Find<TreeView>("ResourceTree");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool Playing() => (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && Shown() != room; n++) await Task.Delay(100);
                    await Task.Delay(300);
                    window.UpdateLayout();
                }
                var overlay = Find<Image>("RoomMessageOverlay");
                var verbOverlay = Find<Image>("VerbMessageOverlay");
                var charset = (ScummV3Charset?)Field("_charset");
                Check(charset is not null, $"charset 0 was read ({charset?.FilePath ?? (string?)Field("_charsetError")})");

                await Open(2);
                Check(Session().Snapshot.RoomId == 2, "Room 02 is open in the explore mode after the boot");
                var before = Session().Messages.OnScreen;
                Check(overlay.Visibility != Visibility.Visible || before.Count > 0,
                    $"before the click the layer shows only what the session has on screen: [{string.Join(" | ", before.Select(m => $"\"{m.Text}\" {m.Kind} at ({m.X}, {m.Y}) from {m.Source}"))}]");
                Save("room02-before.png");

                // The double click on object 156 (the sky), as the mouse-down pairing sends it.
                var clock = Stopwatch.StartNew();
                Call("HandleRoomPoint", 700, 40, true);
                Check(Playing(), "after the click the playback clock runs (the frames are not played in one go)");
                var frames = new List<string>();
                long? amanecerShown = null, amanecerGone = null, stopped = null;
                var savedTalk = false;
                var cieloDrawn = false;
                while (clock.ElapsedMilliseconds < 15000)
                {
                    await Task.Delay(20);
                    var talk = Session().Messages.Talk;
                    var drawn = Session().Messages.OnScreen.Select(m => m.Text).ToArray();
                    var line = $"{clock.ElapsedMilliseconds,5} ms: on screen [{string.Join(" | ", drawn)}], overlay {(overlay.Visibility == Visibility.Visible ? "shown" : "hidden")}, verb text {(verbOverlay.Visibility == Visibility.Visible ? "shown" : "hidden")}";
                    if (frames.Count == 0 || !frames[^1].EndsWith(line[(line.IndexOf(':') + 1)..], StringComparison.Ordinal)) frames.Add(line);
                    if (talk?.Text == "Amanecer." && amanecerShown is null)
                    {
                        amanecerShown = clock.ElapsedMilliseconds;
                        window.UpdateLayout();
                        Save("room02-156-amanecer-app.png");
                        savedTalk = true;
                        var source = overlay.Source as BitmapSource;
                        Check(overlay.Visibility == Visibility.Visible && source is not null,
                            "\"Amanecer.\" is drawn on the room's message layer");
                        if (source is not null)
                        {
                            // Screen (160, 112) is room row 96 (the room screen starts at row 16), x = camera left + 160.
                            var rect = ((int Left, int Top, int Right, int Bottom)?)Field("_talkTextRect");
                            var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
                            source.CopyPixels(pixels, source.PixelWidth * 4, 0);
                            var opaque = Enumerable.Range(0, source.PixelWidth * source.PixelHeight).Where(i => pixels[i * 4 + 3] != 0).ToArray();
                            var rows = opaque.Select(i => i / source.PixelWidth).ToArray();
                            var cols = opaque.Select(i => i % source.PixelWidth).ToArray();
                            report.Add($"      text pixels: {opaque.Length}, room rows {rows.Min()}–{rows.Max()}, room columns {cols.Min()}–{cols.Max()}; " +
                                       $"camera x {Session().CameraX}; talk rectangle {rect}; message at ({talk.X}, {talk.Y}), colour 0x{talk.Color:X2}, delay {talk.TalkDelay}");
                            Check(rows.Min() >= 96 && rows.Min() <= 97, $"the text's top row is room row 96 (screen y 112 − 16), shadow included: {rows.Min()}");
                        }
                    }
                    if (drawn.Contains("cielo") && verbOverlay.Visibility == Visibility.Visible) cieloDrawn = true;
                    if (amanecerShown is not null && amanecerGone is null && talk is null)
                    {
                        amanecerGone = clock.ElapsedMilliseconds;
                        Save("room02-156-after-talk-app.png");
                    }
                    if (!Playing())
                    {
                        stopped = clock.ElapsedMilliseconds;
                        break;
                    }
                }
                report.Add("Frames as drawn (changes only):");
                report.AddRange(frames.Select(f => "      " + f));
                Check(savedTalk, "\"Amanecer.\" appeared while the clock played");
                var shownFor = amanecerGone - amanecerShown;
                report.Add($"      \"Amanecer.\" shown at {amanecerShown} ms, gone at {amanecerGone} ms ({shownFor} ms); playback stopped at {stopped} ms");
                // 105 jiffies at 6 per frame: the text is on screen for 17 frames and gone in the 18th, ~1.7–1.8 s at 10 frames a second.
                Check(shownFor is >= 1400 and <= 2600, $"\"Amanecer.\" stays for about its talk delay (105 jiffies, 17–18 frames of 100 ms): {shownFor} ms");
                Check(stopped is not null && Session().Messages.Talk is null, "the clock stops once no talk is shown and the scripts wait for input");
                Check(cieloDrawn, "\"cielo\" (script 007 +0x001E, print 0xFE, text code 6) is drawn in the verb screen at (272, 181)");
                Check(Find<FrameworkElement>("VerbStripPanel").Visibility == Visibility.Visible,
                    "with the verb strip off, the verb screen shows for the text printed there");
                Save("room02-156-final-app.png");

                // Clicking the text away: ScummVM's talk-stop key ('.'), assumed input.
                Call("HandleRoomPoint", 700, 40, true);
                for (var n = 0; n < 100 && Session().Messages.Talk?.Text != "Amanecer."; n++) await Task.Delay(20);
                var rectNow = ((int Left, int Top, int Right, int Bottom)?)Field("_talkTextRect");
                Check(rectNow is not null, $"the talk text has a clickable rectangle: {rectNow}");
                if (rectNow is { } r)
                {
                    var skipClock = Stopwatch.StartNew();
                    var skipped = (bool)Call("SkipTalkAt", (r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2)!;
                    for (var n = 0; n < 100 && Session().Messages.Talk is not null; n++) await Task.Delay(20);
                    Check(skipped && Session().Messages.Talk is null && skipClock.ElapsedMilliseconds < 600,
                        $"a click on the text ends it on the next frame ({skipClock.ElapsedMilliseconds} ms); status \"{Find<TextBlock>("StatusText").Text}\"");
                    Check(!(bool)Call("SkipTalkAt", 5, 5)!, "a click away from the text does not skip it");
                }
                for (var n = 0; n < 200 && Playing(); n++) await Task.Delay(20);

                // Clicking one object after another: script 008 erases the name area with drawBox
                // (232,152)–(312,192) before script 007 prints the next name, so one name shows at a time,
                // under the close-up (verb 0x36) at (248, 152).
                var records = ((IEnumerable<ScummRoomObjectRecord>)Field("_currentRoomObjectRecords")!).ToArray();
                var verbImage = Find<Image>("VerbStripImage");
                foreach (var id in new[] { 155, 157, 156 })
                {
                    var record = records.First(r => r.Id == id);
                    Call("HandleRoomPoint", record.X + record.Width / 2, record.Y + Math.Min(record.Height / 2, 20), false);
                    for (var n = 0; n < 300 && Playing(); n++) await Task.Delay(20);
                    await Task.Delay(100);
                    window.UpdateLayout();
                    var names = Session().Messages.Drawn.Where(m => m.X == 272 && m.Y == 181).Select(m => m.Text).ToArray();
                    var closeUp = Session().Verbs.FirstOrDefault(v => v.VerbId == 0x36);
                    Check(names.Length <= 1,
                        $"after a click on object {id}: {names.Length} name(s) at (272, 181) [{string.Join(", ", names)}]; " +
                        $"close-up {(closeUp is { Mode: 1 } ? $"object {closeUp.ImageObject}'s image at ({closeUp.X}, {closeUp.Y})" : "off")}; verb screen {(verbImage.IsVisible ? "shown" : "hidden")}");
                    if (names.Length == 1 && closeUp is { Mode: 1 })
                        Check(verbImage.Source is BitmapSource { Format: var format } && format == PixelFormats.Indexed8,
                            $"object {id}: the verb screen draws the verb slots, so the close-up is above the name");
                    Save($"room02-click-{id}-name-app.png");
                }
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "message-app-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Report not written ({writeError.Message}):");
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

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// OBSOLETE: "Show verb area" (presentation): in the real MainWindow, checks that the toggle
// is off by default while the interactive mode keeps the verb screen, that the strip sits under the room at the room's scale without clipping,
// draws the verbs the session's slots have on (after script 018, and the close-up
// after a hover), sends clicks on verbs that are on to the game when "Click as
// game input" is on (a note with nothing aimed at, then after aiming at an
// object), and goes away again. Script 018 is started through the
// session's internal test seam, as the tests do. No physical input.
// Arguments: <Games\loom> <output folder>.
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
        // Since M1d a click plays on a timer; the harness runs the frames at once, as before.
        typeof(MainWindow).GetField("_playInstantly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, true);
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
            var report = new List<string> { "Real MainWindow; verb state changed through the session's test seam and the pointer handler. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                Check(window.FindName("VerbAreaToggle") is null && window.FindName("CameraPanToggle") is null, "obsolete Advanced controls are absent");
                var pointer = (CheckBox)window.FindName("GamePointerToggle");
                var strip = (FrameworkElement)window.FindName("VerbStripPanel");
                var image = (Image)window.FindName("VerbStripImage");
                // Since task D (30 Sep) the explanation is the verb screen's tooltip, not a row under it: its first line is the summary.
                var border = (FrameworkElement)window.FindName("VerbStripBorder");
                string NoteText() => ((border.ToolTip as string) ?? "").Split(Environment.NewLine)[0];
                Check(window.FindName("VerbStripNote") is null, "the verb screen has no note row under it (task D)");
                var viewport = (FrameworkElement)window.FindName("RoomViewport");
                var host = (FrameworkElement)window.FindName("RoomViewportHost");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource")) != room; n++)
                        await Task.Delay(100);
                    await Settle();
                }
                async Task Settle() { await Task.Delay(300); window.UpdateLayout(); }
                byte PixelAt(int x, int y)
                {
                    var source = (BitmapSource)image.Source;
                    var row = new byte[source.PixelWidth];
                    source.CopyPixels(new Int32Rect(0, y, source.PixelWidth, 1), row, source.PixelWidth, 0);
                    return row[x];
                }
                // Whether the strip image still shows verb slots (the slot picture is Indexed8; the text-only verb screen is plain black).
                bool AnyPixel(int left, int top, int width, int height)
                {
                    for (var y = top; y < top + height; y++)
                        for (var x = left; x < left + width; x++)
                            if (PixelAt(x, y) != 0) return true;
                    return false;
                }

                await Open(2);
                var roomHeightOff = viewport.ActualHeight;
                Check(strip.Visibility == Visibility.Visible,
                    $"the interactive mode keeps the verb screen under the room without a toggle: \"{NoteText()}\"");

                await Settle();
                Check(strip.Visibility == Visibility.Visible, "on: the strip is shown under the room");
                Check(Math.Abs(image.ActualWidth - viewport.ActualWidth) < 1.5 &&
                      Math.Abs(image.ActualHeight - image.ActualWidth * 56 / 320) < 1.5,
                    $"on: the strip is 320 x 56 screen pixels at the room's scale ({image.ActualWidth:0.#} x {image.ActualHeight:0.#}; room view {viewport.ActualWidth:0.#} wide)");
                var bottom = strip.TranslatePoint(new Point(0, strip.ActualHeight), host).Y;
                Check(bottom <= host.ActualHeight + 0.5 && Math.Abs(viewport.ActualHeight - roomHeightOff) < 0.5,
                    $"on: the room keeps its size, the strip already fits (strip bottom {bottom:0.#} of {host.ActualHeight:0.#}; room {roomHeightOff:0.#} → {viewport.ActualHeight:0.#})");
                Check(NoteText().Contains("presentation", StringComparison.Ordinal) && NoteText().Contains("not the game's drawing", StringComparison.Ordinal) &&
                      NoteText().Contains("no image verb is on", StringComparison.Ordinal),
                    $"on, after the boot: nothing is on, and the note says it is a presentation: \"{NoteText()}\"");
                Check(!AnyPixel(0, 0, 320, 56), "on, after the boot: the strip is empty");
                Save("room-02-verb-area-boot.png");

                // Script 018 [2] turns the distaff notes on (as script 003 does in play).
                var script = new LoomScriptDisassembly(250, 1, 0, 32, 16,
                    [new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"), new(5, 0, "stopObjectCode", "", "00")],
                    null, null, [], [], "Synthetic test script");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript", Private)!.Invoke(Session(), [script]);
                Call("UpdateVerbArea");
                await Settle();
                Check(NoteText().Contains("verbs 0x01–0x08, 0x31, 0x32 on", StringComparison.Ordinal) &&
                      (border.ToolTip as string ?? "").Contains("verb 0x01 (slot 1): object 100's image from Room 01 at (16, 144)", StringComparison.Ordinal),
                    $"after script 018: the note lists the verbs that are on, the tooltip their images: \"{NoteText()}\"");
                Check(AnyPixel(16, 0, 24, 16) && AnyPixel(192, 0, 24, 16),
                    "after script 018: the first and last distaff notes (x 16 and 192, row 144) are drawn");
                Save("room-02-verb-area-distaff.png");

                // Task E (30 Sep): script 030 [3] plays note 3 on the distaff; script 031 draws verbs 0x13–0x17 over it
                // (the shine) and last verb 0x03 with its plain image (Room 01 object 112). The strip shows the plain note.
                var play = new LoomScriptDisassembly(250, 1, 0, 32, 16,
                    [new(0, 0x0A, "startScript", "0x1E, [0x0003]", "0A 1E 03 00 FF"), new(5, 0, "stopObjectCode", "", "00")],
                    null, null, [], [], "Synthetic test script");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript", Private)!.Invoke(Session(), [play]);
                for (var frame = 0; frame < 40; frame++) Session().AdvanceFrame();
                Call("UpdateVerbArea");
                await Settle();
                var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(args[0]);
                var plain = new SCUMMexplorer.Core.Rendering.LoomRoomObjects()
                    .Decode(File.ReadAllBytes(game.Rooms.Single(room => room.ResourceId == 1).FilePath)).Single(item => item.Id == 112);
                var differing = 0;
                for (var y = 0; y < plain.Height / 8 * 8; y++)
                    for (var x = 0; x < plain.Width / 8 * 8; x++)
                        if (PixelAt(72 + x, y) != plain.Pixels[y * plain.Width + x]) differing++;
                var onThere = Session().Verbs.Where(verb => verb is { Mode: 1, X: 72, Y: 144 }).Select(verb => $"0x{verb.VerbId:X2}");
                Check(differing == 0,
                    $"after note 3 played (script 030 [3], 40 frames): the strip shows the plain note (object 112) at x 72, " +
                    $"not the shine; {differing} pixels differ; verbs on there: {string.Join(", ", onThere)}");
                Save("room-02-verb-area-note-played.png");

                // The pointer over object 156 (class 1): script 008 turns the close-up (verb 0x36) on at (248, 152).
                pointer.IsChecked = true;
                Call("SendPointerToGame", (ValueTuple<int, int>?)(700, 40), (int?)156);
                await Settle();
                var closeUp = Session().Verbs.FirstOrDefault(verb => verb.VerbId == 0x36);
                Check(closeUp is { Mode: 1, X: 248, Y: 152 } && NoteText().Contains("0x36", StringComparison.Ordinal) &&
                      AnyPixel(248, 8, 16, 8),
                    $"hover on object 156: the close-up (verb 0x36, {closeUp?.Describe()}) is drawn at (248, 152): \"{NoteText()}\"");
                Save("room-02-verb-area-close-up.png");
                pointer.IsChecked = false;

                // The game's click on the strip (the explore mode's click since 28 September): verbs that
                // are on go to script 005 as runInputScript(1, verb, 0). The inspection mode sends nothing.
                var clicks = (CheckBox)window.FindName("SessionModeToggle");
                clicks.IsChecked = false;
                await Settle();
                var status = (TextBlock)window.FindName("StatusText");
                Point StripPoint(int x, int y) => new((x + 0.5) * image.ActualWidth / 320, (y - 144 + 0.5) * image.ActualHeight / 56);
                bool ClickStrip(int x, int y) => (bool)Call("ClickVerbStrip", StripPoint(x, y))!;
                bool TracedSince(int from, string text) => Session().SessionTrace.Skip(from).Any(line => line.Contains(text, StringComparison.Ordinal));
                ushort? Var(int number) => Session().Snapshot.KnownGlobalVariables.TryGetValue($"var[0x{number:X3}]", out var value) ? value : null;
                var third = Session().VerbArea().Items.Single(item => item.VerbId == 0x03);
                var before = Session().SessionTrace.Count;
                Check(!ClickStrip(third.X + 1, third.Y + 1) && Session().SessionTrace.Count == before,
                    "inspection mode: a click on note 0x03 in the strip sends nothing");
                clicks.IsChecked = true;
                await Settle();
                Check(NoteText().Contains("Clicks on verbs that are on go to the game as assumed input", StringComparison.Ordinal),
                    $"explore mode: the note says verb clicks are assumed input: \"{NoteText()}\"");
                before = Session().SessionTrace.Count;
                Check(!ClickStrip(100, 195) && Session().SessionTrace.Count == before,
                    "explore mode: a click on the strip where no verb is on sends nothing");
                var sent = ClickStrip(third.X + 1, third.Y + 1);
                await Settle();
                // Since the note was played (task E), verbs 0x13-0x17 are on over it too: findVerbAtPos takes the highest
                // slot, verb 0x17 (slot 23), and script 005 maps it back to note 3 ((0x17 - 9) / 5 + 1).
                Check(sent && TracedSince(before, "verb 0x17 (slot 23), runs script 5 with [1, 23, 0]") && TracedSince(before, "Script 10 chains to script 13") &&
                      status.Text.Contains("Click on verb 0x17 sent to the game (assumed input)", StringComparison.Ordinal),
                    $"a click on note 3 with nothing aimed at finds verb 0x17 (the last glow verb, highest slot) and reaches script 013 through scripts 005 and 010: \"{status.Text}\"");
                // Aim at object 156 (class 1) with a room click, then play the note: script 010 records it.
                Call("SendClickToGame", 700, 40, false);
                await Settle();
                before = Session().SessionTrace.Count;
                sent = ClickStrip(third.X + 1, third.Y + 1);
                await Settle();
                Check(sent && Var(0x067) == 156 && Var(0x056) == 3 && Var(0x062) == 0 && TracedSince(before, "Script 10 chains to script 27"),
                    $"after aiming at object 156, note 0x03 is recorded (var[0x056] = {Var(0x056)}), script 027 runs and instant playback completes the draft (var[0x062] = {Var(0x062)})");
                Save("room-02-verb-area-note-click.png");

                await Settle();
                // Since M1c the verb screen stays under the room while a script's text is there (the click on
                // object 156 drew its name, "cielo"); then only that text is shown. The room fits the space it has
                // (the controls under it may have grown since the first check, so its height is not compared with it).
                var verbText = Session().Messages.OnScreen.Where(m => m.Y >= 144).Select(m => m.Text).ToArray();
                // The layout's rule: the room and the verb screen (56 rows; no note row since task D) share the host's height.
                var roomImage = Field("_currentRoom")!;
                var roomWidth = (int)roomImage.GetType().GetProperty("Width")!.GetValue(roomImage)!;
                // The view shows LOOM's 144 room rows (the boot's initScreens(0, 144)), and the pan buttons' panel, not a scroll bar,
                // sits under a room wider than the view.
                var roomHeight = Math.Min(144, (int)roomImage.GetType().GetProperty("Height")!.GetValue(roomImage)!);
                var normal = ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("NormalRoomWidthToggle")).IsChecked == true;
                var visibleWidth = normal ? Math.Min(320, roomWidth) : roomWidth;
                var panPanel = (FrameworkElement)window.FindName("RoomScrollButtonsPanel");
                var pan = normal ? panPanel.Height + panPanel.Margin.Top + panPanel.Margin.Bottom : 0;
                var shown = strip.Visibility == Visibility.Visible;
                var expectedScale = Math.Min(host.ActualWidth / visibleWidth,
                    (host.ActualHeight - pan) / (roomHeight + (shown ? 56 : 0)));
                var roomFits = Math.Abs((double)Field("_roomDisplayScale")! - expectedScale) < 0.01 &&
                               Math.Abs(viewport.ActualHeight - roomHeight * expectedScale) < 1;
                // With the game's text there, the verb screen stays and draws the slots that text goes with.
                Check(shown && roomFits,
                    $"off again: {(verbText.Length == 0 ? $"the verb screen stays, as the interactive mode keeps it: \"{NoteText()}\"" : $"the verb screen stays for the game's text [{string.Join(", ", verbText)}]: \"{NoteText()}\"")}; " +
                    $"the room fills its space (room {viewport.ActualWidth:0.#} x {viewport.ActualHeight:0.#} in {host.ActualWidth:0.#} x {host.ActualHeight:0.#}; first {roomHeightOff:0.#} high)");
                Save("room-02-verb-area-off-again.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written must not crash the harness: fall back to the temp folder.
                try { File.WriteAllLines(Path.Combine(output, "verb-area-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    var fallback = Path.Combine(Path.GetTempPath(), "SCUMMexplorer-harness", "verb-area-check.txt");
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

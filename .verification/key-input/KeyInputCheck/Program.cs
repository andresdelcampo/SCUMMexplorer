using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// Keys as game input: in the real MainWindow, raises the window's own
// PreviewKeyDown and PreviewTextInput events (synthetic events, no physical
// keyboard) and checks that keys reach script 005 in the interactive mode (since
// 30 Sep always, with no option; not in the inspection mode), that '.' skips the
// text shown (the engine's talk-stop key),
// that the engine's own keys are refused with a label, that note letters are
// recorded after the distaff is on and an object is aimed at, that Escape
// cancels the draft being played, and that F5 opens Room 70 (the save screen).
// Script 018 is started through the session's internal test seam, as the tests do.
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
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; keys raised as synthetic PreviewKeyDown/PreviewTextInput events on the window. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var interactive = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("SessionModeToggle");
                var status = (TextBlock)window.FindName("StatusText");
                var viewport = (ScrollViewer)window.FindName("RoomViewport");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                int? CurrentRoom() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                async Task Settle() { await Task.Delay(300); window.UpdateLayout(); }
                async Task Open(int room)
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 100 && CurrentRoom() != room; n++) await Task.Delay(100);
                    await Settle();
                }
                ushort? Var(string name) => Session().Snapshot.KnownGlobalVariables.TryGetValue(name, out var value) ? value : null;
                bool TracedSince(int from, string text) => Session().SessionTrace.Skip(from).Any(line => line.Contains(text, StringComparison.Ordinal));
                bool Text(string text)
                {
                    var composition = new TextComposition(InputManager.Current, window, text);
                    var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
                    window.RaiseEvent(e);
                    return e.Handled;
                }
                bool Press(Key key)
                {
                    var e = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                    window.RaiseEvent(e);
                    return e.Handled;
                }

                await Open(2);
                viewport.Focus();
                Check(window.FindName("GameKeysToggle") is null && interactive.IsChecked == true,
                    "there is no \"Keys as game input\" option; the interactive mode is on after opening the game");
                interactive.IsChecked = false;
                await Settle();
                viewport.Focus();
                var before = Session().SessionTrace.Count;
                Check(!Text(">") && !Press(Key.F6) && Session().SessionTrace.Count == before, "inspection mode: '>' and F6 are not sent (not handled)");

                interactive.IsChecked = true;
                await Settle();
                viewport.Focus();
                before = Session().SessionTrace.Count;
                Check(Text(">") && TracedSince(before, "key: '>' runs script 5 with [4, 0x3E, 1]") && Var("var[0x0A5]") == 1 && Var("var[0x025]") == 8 &&
                      status.Text.Contains("Key '>' sent to the game (assumed input)", StringComparison.Ordinal),
                    $"on: '>' goes to script 005, which sets var[0x0A5] = {Var("var[0x0A5]")} and VAR_CHARINC var[0x025] = {Var("var[0x025]")}: \"{status.Text}\"");
                // Script 005 +0x0211 prints the text speed as a system line (print 0xFF): '.' ends its delay.
                // This step runs on the real playback clock, so the text is still shown when '.' is pressed.
                typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, false);
                Check(Text("<") && Session().Messages.Talk is not null, $"'<' prints the text speed as talk: \"{Session().Messages.Talk?.Text}\"");
                var talk = Session().Messages.Talk;
                var delay = Session().Messages.TalkDelay;
                before = Session().SessionTrace.Count;
                Check(talk is not null && delay > 0 && Text(".") && Session().Messages.TalkDelay == 0 && !TracedSince(before, "runs script 5") &&
                      status.Text.Contains("Text skipped (the game's talk-stop key '.'", StringComparison.Ordinal),
                    $"'.' (the engine's talk-stop key) ends the delay of \"{talk?.Text}\" ({delay} → {Session().Messages.TalkDelay} jiffies) and is not sent to script 005: \"{status.Text}\"");
                for (int n = 0; n < 50 && Session().Messages.Talk is not null; n++) await Task.Delay(100);
                Check(Session().Messages.Talk is null, "after '.', the next frames end the text");
                for (int n = 0; n < 100 && (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!; n++) await Task.Delay(100);
                typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, true);
                Check(Text(".") && status.Text.Contains("no text is shown", StringComparison.Ordinal),
                    $"'.' with no text shown does nothing and says so: \"{status.Text}\"");
                Check(Press(Key.F6) && Var("var[0x0A2]") == 1, $"on: F6 (0x140) cycles var[0x0A2] to {Var("var[0x0A2]")} (music off)");
                before = Session().SessionTrace.Count;
                Check(Press(Key.F8) && status.Text.Contains("Key F8 not sent to the game", StringComparison.Ordinal) &&
                      (status.ToolTip as string ?? "").Contains("restart key", StringComparison.Ordinal) && !TracedSince(before, "runs script 5"),
                    $"on: F8 (restart) is the engine's and is not sent; the status says why: \"{status.Text}\"");
                Check(Press(Key.Space) && status.Text.Contains("Key space not sent to the game", StringComparison.Ordinal),
                    $"on: space (pause) is not sent: \"{status.Text}\"");

                var textBox = (TextBox)window.FindName("RoomEntryTraceOutput");
                if (textBox.Focus())
                {
                    before = Session().SessionTrace.Count;
                    Check(!Text("e") && Session().SessionTrace.Count == before, "on: typing in a text box is not sent to the game");
                }
                else report.Add("INFO the room entry trace box could not take focus; the text-box check was not run");
                viewport.Focus();

                // Script 018 [2] turns the distaff on; a room click aims at Room 02 object 156 (class 1).
                var script = new LoomScriptDisassembly(250, 1, 0, 32, 16,
                    [new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"), new(5, 0, "stopObjectCode", "", "00")],
                    null, null, [], [], "Synthetic test script");
                typeof(LoomExplorationSession).GetMethod("RunTopLevelScript", Private)!.Invoke(Session(), [script]);
                Call("SendClickToGame", 700, 40, false);
                await Settle();
                Check(Text("e") && Var("var[0x056]") == 3 && Var("bit[0xD66]") == 1,
                    $"'e' (note 3) is recorded in var[0x056] = {Var("var[0x056]")} and script 020 plays it back (bit[0xD66] = {Var("bit[0xD66]")})");
                before = Session().SessionTrace.Count;
                Check(Press(Key.Escape) && TracedSince(before, "cutscene at Script 05 +0x01C0") && Var("var[0x067]") == 0,
                    $"Escape with a draft playing: script 005's cutscene runs scripts 028 and 021, and the aim is cleared (var[0x067] = {Var("var[0x067]")})");

                Check(Press(Key.F5), "F5 is handled");
                for (int n = 0; n < 50 && CurrentRoom() != 70; n++) await Task.Delay(100);
                await Settle();
                Check(CurrentRoom() == 70 && Session().Snapshot.RoomId == 70 && TracedSince(before, "key: F5 runs script 5 with [4, 0x13F, 1]") &&
                      TracedSince(before, "cutscene at Script 04 +0x0000"),
                    $"F5 (0x13F) runs script 004, which loads Room 70 (the save screen); the app shows it: \"{status.Text}\"");

                // Task G (30 Sep): '.' during a cutscene. The trial (script 046) from its known-cutscene entry, on the real clock.
                typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, false);
                var known = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var trial = known.Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 46);
                Call("PlayKnownCutscene", trial);
                for (int n = 0; n < 300 && string.IsNullOrWhiteSpace(Session().Messages.Talk?.Text); n++) await Task.Delay(100);
                viewport.Focus();
                var line = Session().Messages.Talk;
                var lineDelay = Session().Messages.TalkDelay;
                var inCutscene = Session().InCutscene;
                report.Add($"   before '.': keys active {typeof(MainWindow).GetProperty("GameKeysActive", Private)!.GetValue(window)}, focus {Keyboard.FocusedElement?.GetType().Name}, playback {typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)}");
                var skipped = Text(".");
                var afterDelay = Session().Messages.TalkDelay;
                var nextSeen = false;
                for (int n = 0; n < 30 && !nextSeen; n++)
                {
                    await Task.Delay(100);
                    nextSeen = !ReferenceEquals(Session().Messages.Talk, line);
                }
                Check(line is not null && inCutscene && skipped && lineDelay > 0 && afterDelay == 0 && nextSeen && Session().InCutscene,
                    $"in the trial's cutscene, '.' ends the delay of \"{line?.Text}\" ({lineDelay} → {afterDelay} jiffies) and within 3 s the text moves on " +
                    $"(now \"{Session().Messages.Talk?.Text}\"); the cutscene goes on: \"{status.Text}\" (handled {skipped}, keys active {typeof(MainWindow).GetProperty("GameKeysActive", Private)!.GetValue(window)}, focus {Keyboard.FocusedElement?.GetType().Name}, room {CurrentRoom()}, talk from {line?.Source})");
                Call("StopScenePlayback");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                // A report that cannot be written must not crash the harness: fall back to the temp folder.
                try { File.WriteAllLines(Path.Combine(output, "key-input-check.txt"), report, System.Text.Encoding.UTF8); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    var fallback = Path.Combine(Path.GetTempPath(), "SCUMMexplorer-harness", "key-input-check.txt");
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

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

// M3c playback controls in the real MainWindow, on the real playback clock (10 frames a second).
// 1. "Play a cutscene" → script 052: a fresh session after the boot, Room 10 from the catalog, object 295's
//    verb 0x38 run directly; the frames play through Room 11 into Room 13 with actors, text and the delay.
//    During Room 11 the playback is paused (the frames stop), stepped one frame (Continue ▸) and played on.
// 2. Script 052 again, with Escape (SkipCutscene) once the cutscene set its override: it skips to Room 13.
// 3. The Elders' trial (script 046), skipped with Escape as soon as it allows.
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
        void Save(string file)
        {
            window.UpdateLayout();
            var group = Find<FrameworkElement>("RoomDisplayGroup");
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
            var report = new List<string> { "Real MainWindow; the playback controls are called as the buttons and keys call them, on the playback clock. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = Find<TreeView>("ResourceTree");
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool Playing() => (bool)typeof(MainWindow).GetProperty("PlaybackRunning", Private)!.GetValue(window)!;
                bool HasPlayback() => Field("_playback") is not null;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                string Status() => Find<TextBlock>("StatusText").Text;
                int Jiffies() => Session().Snapshot.KnownGlobalVariables.TryGetValue("var[0x02F]", out var total) ? total : -1;
                var cutscenes = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 2 &&
                        i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var n = 0; n < 100 && Shown() != 2; n++) await Task.Delay(100);
                await Task.Delay(300);
                window.UpdateLayout();
                Check(Find<FrameworkElement>("PlaybackBar").Visibility == Visibility.Visible &&
                      !Find<Button>("PlaybackPauseButton").IsEnabled && !Find<Button>("SkipCutsceneButton").IsEnabled,
                    "Room 02 after the boot: the playback bar is shown; pause and skip are off with nothing playing");

                // Plays a known cutscene and watches it until the playback ends; the hook runs on every poll.
                async Task<(long Ms, List<string> Log, Dictionary<int, long> Entered, List<string> Texts, HashSet<int> ActorRooms)> Play(
                    int index, Func<long, Task>? hook = null, int limitMs = 60000)
                {
                    var clock = Stopwatch.StartNew();
                    var log = new List<string>();
                    var entered = new Dictionary<int, long>();
                    var texts = new List<string>();
                    var actorRooms = new HashSet<int>();
                    Call("PlayKnownCutscene", cutscenes.GetValue(index));
                    log.Add($"{0,6} ms: status \"{Status()}\"");
                    (int?, int?, bool, bool) last = default;
                    string? lastText = null;
                    while (clock.ElapsedMilliseconds < limitMs)
                    {
                        await Task.Delay(20);
                        var session = Session();
                        var room = session.Snapshot.RoomId;
                        if (Shown() is int shown && shown == room && !entered.ContainsKey(shown)) entered[shown] = clock.ElapsedMilliseconds;
                        var now = (room, Shown(), session.InCutscene, session.CutsceneSkippable);
                        if (now != last)
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: session Room {room:00}, view Room {Shown():00}, cutscene {now.Item3}, skippable {now.Item4}, playing {Playing()}");
                        last = now;
                        var text = string.Join(" | ", session.Messages.OnScreen.Where(m => (m.RoomId is null || m.RoomId == room) && m.Lines.Any(line => line.Trim().Length > 0))
                            .Select(m => $"{m.Kind} {m.SourceLabel} +0x{m.Offset:X4}: {string.Join(" / ", m.Lines)}"));
                        if (text.Length > 0 && text != lastText)
                        {
                            texts.Add($"Room {room:00}: {text}");
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: text \"{text}\"");
                        }
                        lastText = text;
                        if (Find<Image>("RoomActorOverlay").Visibility == Visibility.Visible && Shown() == room && room is int r) actorRooms.Add(r);
                        if (hook is not null) await hook(clock.ElapsedMilliseconds);
                        if (!HasPlayback() && clock.ElapsedMilliseconds > 500) break;
                    }
                    await Task.Delay(400);
                    log.Add($"{clock.ElapsedMilliseconds,6} ms: playback ended; status \"{Status()}\"");
                    return (clock.ElapsedMilliseconds, log, entered, texts, actorRooms);
                }

                // 1. Script 052 played through, with a pause and a one-frame step in Room 11.
                var paused = false;
                var pauseNotes = new List<string>();
                var savedText = false;
                var full = await Play(0, async ms =>
                {
                    var session = Session();
                    if (!savedText && session.Snapshot.RoomId == 11 && Shown() == 11 && session.CutsceneSkippable)
                    {
                        savedText = true;
                        Save("script052-room11-actors-app.png");
                    }
                    if (paused || session.Snapshot.RoomId != 11 || Shown() != 11 || ms < 100) return;
                    paused = true;
                    Call("PlaybackPause_Click", null, null);
                    var before = Jiffies();
                    var label = Find<Button>("PlaybackPauseButton").Content;
                    await Task.Delay(1000);
                    var after = Jiffies();
                    pauseNotes.Add($"paused: button \"{label}\", timer total {before} → {after} jiffies in 1 s, playing {Playing()}");
                    Check(before == after && !Playing() && Equals(label, "▶ Play"), "Pause holds the frames: VAR_TIMER_TOTAL did not move in 1 s, and the button says Play");
                    Call("AdvanceFrame_Click", null, null);
                    var stepped = Jiffies();
                    Check(stepped - after == Session().FrameJiffies && !Playing(),
                        $"Continue ▸ while paused plays one frame: {after} → {stepped} jiffies (one frame is {Session().FrameJiffies}), still paused");
                    Call("PlaybackPause_Click", null, null);
                    Check(Playing(), "Play goes on");
                });
                report.Add("1. Script 052 from its stated start, played through:");
                report.AddRange(full.Log.Select(line => "      " + line));
                report.AddRange(pauseNotes.Select(line => "      " + line));
                var toRoom11 = full.Entered.GetValueOrDefault(11, -1);
                Check(full.Entered.ContainsKey(10) || toRoom11 > 0, $"the view shows Room 10, then Room 11 at {toRoom11} ms");
                // +0x0041 delay 60: 11 frames at 6 jiffies (1.1 s) before the load at +0x0045.
                Check(toRoom11 >= 1000, $"the delay holds Room 10 for at least 1 s before Room 11 ({toRoom11} ms)");
                // Script 052 and Room 11's locals have no print (decoded), so no talk text is expected on this route.
                Check(!full.Texts.Any(text => text.Contains("Talk", StringComparison.Ordinal) && !text.StartsWith("Room 13", StringComparison.Ordinal)),
                    $"no talk text in Rooms 10 and 11, as script 052 prints none ({full.Texts.Count} text changes, listed above)");
                Check(full.ActorRooms.Contains(11), $"actors are drawn in Room 11 (rooms with actors drawn: {string.Join(", ", full.ActorRooms.Order())})");
                Check(Shown() == 13 && Session().Snapshot.RoomId == 13 && !Session().InCutscene,
                    $"the playback ends in Room 13 with the cutscene over (view Room {Shown():00}) after {full.Ms} ms");
                Save("script052-room13-end-app.png");

                // 2. Script 052 with Escape once the cutscene allows it.
                long skippedAt = -1;
                var skipDisabledEarly = true;
                var skipped = await Play(0, async ms =>
                {
                    await Task.CompletedTask;
                    var session = Session();
                    if (skippedAt >= 0) return;
                    if (!session.CutsceneSkippable)
                    {
                        if (session.InCutscene && Find<Button>("SkipCutsceneButton").IsEnabled) skipDisabledEarly = false;
                        return;
                    }
                    await Task.Delay(1500);
                    skippedAt = ms + 1500;
                    Call("SkipCutscene");
                });
                report.Add("2. Script 052 again, Escape once skippable:");
                report.AddRange(skipped.Log.Select(line => "      " + line));
                Check(skipDisabledEarly, "Skip is off until the cutscene sets its override (+0x00C6 in Room 11)");
                var skippedTo13 = skipped.Entered.GetValueOrDefault(13, -1);
                Check(skippedAt > 0 && skippedTo13 > skippedAt && skippedTo13 < full.Entered.GetValueOrDefault(13, long.MaxValue),
                    $"Escape at {skippedAt} ms skips to Room 13 at {skippedTo13} ms (played through: {full.Entered.GetValueOrDefault(13, -1)} ms)");
                Check(Session().SessionTrace.Any(line => line.StartsWith("abortCutscene: Script 52 continues at its skip jump", StringComparison.Ordinal)),
                    "the session trace shows abortCutscene at script 052's skip jump");
                Check(Shown() == 13 && !Session().InCutscene, $"the skipped playback ends in Room 13: view Room {Shown():00}");

                // 3. The Elders' trial: its first talk text is shown, then Escape skips it.
                long trialSkip = -1;
                long talkEnds = -1;
                var trial = await Play(1, async ms =>
                {
                    await Task.CompletedTask;
                    if (trialSkip >= 0 || !Session().CutsceneSkippable) return;
                    var talk = Session().Messages.Talk;
                    if (talkEnds < 0 && talk is not null && talk.Lines.Any(line => line.Trim().Length > 0) && Shown() == Session().Snapshot.RoomId)
                    {
                        talkEnds = ms + 800;
                        Save("trial-first-text-app.png");
                    }
                    if (talkEnds < 0 || ms < talkEnds) return;
                    trialSkip = ms;
                    Call("SkipCutscene");
                }, limitMs: 180000);
                report.Add("3. Script 046 (the Elders' trial), Escape after its first talk text:");
                report.AddRange(trial.Log.Select(line => "      " + line));
                Check(trial.Texts.Any(text => text.Contains("Talk", StringComparison.Ordinal)), "the trial shows talk text in the game's font before the skip");
                Check(trialSkip > 0 && !Session().InCutscene && !HasPlayback(),
                    $"the trial skips at {trialSkip} ms and its playback ends in Room {Session().Snapshot.RoomId:00} after {trial.Ms} ms");
                Save("trial-after-skip-app.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "cutscene-playback-check.txt"), report, System.Text.Encoding.UTF8); }
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

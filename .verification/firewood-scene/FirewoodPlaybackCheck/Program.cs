using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

// M3: script 086 (the firewood scene) played in the real MainWindow, on the real playback clock.
// 1. "Play a cutscene" → "Script 086": a fresh session after the boot, a catalog visit to Room 36, object 599's
//    verb 0x38 run directly (the doorway into Room 37), Bobbin's arrival walk, then the game's click in box 8.
//    The frames play through Rooms 37, 58, 37 and 38 with actors and text, and end in Room 38.
// 2. The same, with Escape (SkipCutscene) one second after the skip point: it ends in Room 38 sooner.
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
            var report = new List<string> { "Real MainWindow; PlayKnownCutscene and SkipCutscene are called as the menu and Escape call them, on the playback clock. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
                bool HasPlayback() => Field("_playback") is not null;
                int? Shown() => (int?)Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
                string Status() => Find<TextBlock>("StatusText").Text;
                ScummActorSnapshot Ego(LoomExplorationSession session) => session.Actors.Single(actor => actor.Number == 1);
                string Describe(LoomExplorationSession session)
                {
                    var ego = Ego(session);
                    return $"Room {session.Snapshot.RoomId:00}, ego ({ego.X}, {ego.Y}) in Room {ego.Room:00} facing {ego.Look.Facing}, " +
                           $"in cutscene {session.InCutscene}, bit[0xD9A] = {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("bit[0xD9A]")}";
                }
                for (var n = 0; n < 100 && Shown() is null; n++) await Task.Delay(100);
                var known = ((IEnumerable)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
                    .Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 86);
                report.Add($"Known cutscene: {known}");

                async Task<(long Ms, List<string> Log, List<int> Rooms, List<string> Texts, string StartStatus)> Play(
                    Func<long, Task>? hook = null, int limitMs = 240000)
                {
                    var clock = Stopwatch.StartNew();
                    var log = new List<string>();
                    var rooms = new List<int>();
                    var texts = new List<string>();
                    Call("PlayKnownCutscene", known);
                    // Read at once: the room load that follows collapses and resets the status line.
                    var startStatus = Status();
                    log.Add($"{0,6} ms: status \"{startStatus}\"; {Describe(Session())}");
                    (int?, int?, bool, bool) last = default;
                    string? lastText = null;
                    while (clock.ElapsedMilliseconds < limitMs)
                    {
                        await Task.Delay(20);
                        var session = Session();
                        var room = session.Snapshot.RoomId;
                        if (Shown() is int shown && shown == room && (rooms.Count == 0 || rooms[^1] != shown)) rooms.Add(shown);
                        var now = (room, Shown(), session.InCutscene, session.CutsceneSkippable);
                        if (now != last)
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: session Room {room:00}, view Room {Shown():00}, in cutscene {now.Item3}, skippable {now.Item4}, " +
                                    $"skip button {Find<Button>("SkipCutsceneButton").IsEnabled}");
                        last = now;
                        var text = session.Messages.Talk is { } talk && talk.Lines.Any(line => line.Trim().Length > 0) && Shown() == room
                            ? $"{talk.SourceLabel} +0x{talk.Offset:X4}: {string.Join(" / ", talk.Lines)}" : null;
                        if (text is not null && text != lastText)
                        {
                            texts.Add($"Room {room:00}: {text}");
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: text in Room {room:00} \"{text}\"");
                        }
                        lastText = text;
                        if (hook is not null) await hook(clock.ElapsedMilliseconds);
                        if (!HasPlayback() && clock.ElapsedMilliseconds > 500) break;
                    }
                    await Task.Delay(400);
                    log.Add($"{clock.ElapsedMilliseconds,6} ms: playback ended; status \"{Status()}\"");
                    return (clock.ElapsedMilliseconds, log, rooms, texts, startStatus);
                }

                // 1. Played through.
                var saved = new HashSet<string>();
                async Task SaveOnce(string name, bool when)
                {
                    if (!when || !saved.Add(name)) return;
                    await Task.Delay(120);
                    Save(name);
                }
                var full = await Play(async ms =>
                {
                    var session = Session();
                    var room = session.Snapshot.RoomId;
                    var talk = session.Messages.Talk;
                    await SaveOnce("firewood-room37-stoke-app.png", room == 37 && Shown() == 37 && talk?.Offset == 0x0064);
                    await SaveOnce("firewood-room58-closeup-app.png", room == 58 && Shown() == 58 && talk?.Offset == 0x0193);
                    await SaveOnce("firewood-room37-stick-app.png", room == 37 && Shown() == 37 && talk?.Offset == 0x03C7);
                    await SaveOnce("firewood-room38-app.png", room == 38 && Shown() == 38 && talk?.Offset == 0x04DC);
                });
                report.Add("1. Script 086 from its stated start, played through:");
                report.AddRange(full.Log.Select(line => "      " + line));
                var played = Session();
                report.Add($"   End: {Describe(played)}");
                Check(full.StartStatus.Contains("Script 086", StringComparison.Ordinal), $"the status names the cutscene: \"{full.StartStatus}\"");
                Check(played.SessionTrace.Contains("Room 37 local script 200 chains to script 86"),
                    "Room 37's local 200 chains to script 086 (the walk into box 8), not the explorer");
                Check(string.Join(", ", full.Rooms) == "37, 58, 37, 38", $"the view shows script 086's rooms in order: {string.Join(", ", full.Rooms)}");
                var distinct = full.Texts.Select(text => text[9..]).Distinct().ToList();
                Check(distinct.Count == 13 && full.Texts.Any(text => text.StartsWith("Room 58", StringComparison.Ordinal)),
                    $"all 13 lines of script 086 are shown, in Rooms 37, 58 and 38 ({distinct.Count} distinct)");
                var ego = Ego(played);
                Check(!played.InCutscene && !HasPlayback() && Shown() == 38 && (ego.Room, ego.X, ego.Y) == (38, 196, 132),
                    $"the playback ends in Room 38 after {full.Ms} ms with Bobbin at (196, 132): {Describe(played)}");
                Check(saved.Count == 4, $"screenshots: {string.Join(", ", saved)}");
                Save("firewood-end-app.png");

                // 2. Escape one second after the skip point.
                long skippedAt = -1;
                var skipped = await Play(async ms =>
                {
                    if (skippedAt >= 0 || !Session().CutsceneSkippable || !Find<Button>("SkipCutsceneButton").IsEnabled) return;
                    await Task.Delay(1000);
                    skippedAt = ms + 1000;
                    Call("SkipCutscene");
                });
                report.Add("2. Script 086 again, Escape one second after the skip point:");
                report.AddRange(skipped.Log.Select(line => "      " + line));
                var afterSkip = Session();
                report.Add($"   End: {Describe(afterSkip)}");
                Check(afterSkip.SessionTrace.Any(line => line.StartsWith("abortCutscene: Script 86 continues at its skip jump", StringComparison.Ordinal)),
                    "the session trace shows abortCutscene at script 086's skip jump");
                var egoSkipped = Ego(afterSkip);
                Check(skippedAt > 0 && skipped.Ms < full.Ms / 2 && !afterSkip.InCutscene && Shown() == 38 &&
                      (egoSkipped.Room, egoSkipped.X, egoSkipped.Y) == (38, 196, 132),
                    $"Escape at {skippedAt} ms ends it in Room 38 after {skipped.Ms} ms (played through: {full.Ms} ms): {Describe(afterSkip)}");
                Save("firewood-after-skip-app.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "firewood-playback-check.txt"), report, System.Text.Encoding.UTF8); }
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

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// M3: the boot's opening cutscene (script 044) played in the real MainWindow, on the real playback clock.
// 1. "Play a cutscene" → "The opening (script 044)": a fresh session whose boot stops where script 001 starts
//    script 044; the frames play through Rooms 01, 02, 01, 02, 53 and 02 with actors and text, and end as the
//    app's own boot does (ego position and facing, var[0x071], camera), compared with a fresh booted session.
// 2. The opening again, with Escape (SkipCutscene) one second after the first skip point: it ends in Room 02
//    sooner, with Bobbin facing the screen (script 044 +0x063E).
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
            var report = new List<string> { "Real MainWindow; PlayOpening and SkipCutscene are called as the menu and Escape call them, on the playback clock. No physical input." };
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
                    var vars = session.Snapshot.KnownGlobalVariables;
                    return $"Room {session.Snapshot.RoomId:00}, ego ({ego.X}, {ego.Y}) in Room {ego.Room:00} facing {ego.Look.Facing}, " +
                           $"var[0x071] = {(vars.TryGetValue("var[0x071]", out var v) ? v : -1)}, camera {session.CameraX}, boot frames {session.BootFrames}";
                }
                // The app's own boot, for the end state.
                var booted = new LoomExplorationSession((GameLibrary)Field("_game")!, (ScummIndexedObjectSnapshot)Field("_indexedObjectSnapshot")!);
                report.Add($"The app's boot (constructor): {Describe(booted)}");
                for (var n = 0; n < 100 && Shown() is null; n++) await Task.Delay(100);

                async Task<(long Ms, List<string> Log, Dictionary<int, long> Entered, List<int> Rooms, List<string> Texts, int MaxLights)> Play(
                    Func<long, Task>? hook = null, int limitMs = 240000)
                {
                    var clock = Stopwatch.StartNew();
                    var log = new List<string>();
                    var entered = new Dictionary<int, long>();
                    var rooms = new List<int>();
                    var texts = new List<string>();
                    var maxLights = 0;
                    Call("PlayOpening");
                    log.Add($"{0,6} ms: status \"{Status()}\"; stopped after {Session().BootFramesBeforeOpening} boot frames in Room {Session().Snapshot.RoomId:00}");
                    (int?, int?, bool, bool) last = default;
                    string? lastText = null;
                    while (clock.ElapsedMilliseconds < limitMs)
                    {
                        await Task.Delay(20);
                        var session = Session();
                        var room = session.Snapshot.RoomId;
                        if (Shown() is int shown && shown == room)
                        {
                            if (!entered.ContainsKey(shown)) entered[shown] = clock.ElapsedMilliseconds;
                            if (rooms.Count == 0 || rooms[^1] != shown) rooms.Add(shown);
                        }
                        var now = (room, Shown(), session.BootRunning, session.CutsceneSkippable);
                        if (now != last)
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: session Room {room:00}, view Room {Shown():00}, boot running {now.Item3}, skippable {now.Item4}, " +
                                    $"skip button {Find<Button>("SkipCutsceneButton").IsEnabled}");
                        last = now;
                        var text = string.Join(" | ", session.Messages.OnScreen.Where(m => (m.RoomId is null || m.RoomId == room) && m.Lines.Any(line => line.Trim().Length > 0))
                            .Select(m => $"{m.Kind} {m.SourceLabel} +0x{m.Offset:X4}: {string.Join(" / ", m.Lines)}"));
                        if (text.Length > 0 && text != lastText)
                        {
                            texts.Add($"Room {room:00}: {text}");
                            log.Add($"{clock.ElapsedMilliseconds,6} ms: text \"{text}\"");
                        }
                        lastText = text;
                        if (room == 1 && Shown() == 1)
                            maxLights = Math.Max(maxLights, session.Actors.Count(actor => actor.Room == 1 && actor.Visible == true && actor.Number is >= 1 and <= 9));
                        if (hook is not null) await hook(clock.ElapsedMilliseconds);
                        if (!HasPlayback() && clock.ElapsedMilliseconds > 500) break;
                    }
                    await Task.Delay(400);
                    log.Add($"{clock.ElapsedMilliseconds,6} ms: playback ended; status \"{Status()}\"");
                    return (clock.ElapsedMilliseconds, log, entered, rooms, texts, maxLights);
                }

                // 1. Played through.
                var savedLights = false;
                var saved53 = false;
                var skipOnBeforeOverride = false;
                var full = await Play(async ms =>
                {
                    await Task.CompletedTask;
                    var session = Session();
                    if (session.BootRunning && !session.CutsceneSkippable && Find<Button>("SkipCutsceneButton").IsEnabled) skipOnBeforeOverride = true;
                    if (!savedLights && session.Snapshot.RoomId == 1 && Shown() == 1 &&
                        session.Actors.Count(actor => actor.Room == 1 && actor.Visible == true && actor.Number is >= 1 and <= 9) >= 4)
                    {
                        savedLights = true;
                        Save("opening-room01-lights-app.png");
                    }
                    if (!saved53 && session.Snapshot.RoomId == 53 && Shown() == 53 && session.Messages.Talk is { } talk && talk.Lines.Any(line => line.Trim().Length > 0))
                    {
                        saved53 = true;
                        Save("opening-room53-text-app.png");
                    }
                });
                report.Add("1. The opening from its stated start, played through:");
                report.AddRange(full.Log.Select(line => "      " + line));
                var played = Session();
                report.Add($"   End: {Describe(played)}");
                Check(string.Join(", ", full.Rooms) == "69, 1, 2, 1, 2, 53, 2" || string.Join(", ", full.Rooms) == "1, 2, 1, 2, 53, 2",
                    $"the view shows script 044's rooms in order: {string.Join(", ", full.Rooms)}");
                Check(savedLights, $"Room 01's points of light are drawn (at most {full.MaxLights} of actors 1–9 at once)");
                Check(full.Texts.Any(text => text.StartsWith("Room 53", StringComparison.Ordinal)) && full.Texts.Any(text => text.StartsWith("Room 02", StringComparison.Ordinal)),
                    "text is shown in Room 53 (the fairy) and Room 02 (Bobbin)");
                Check(!skipOnBeforeOverride, "Skip is off until script 044 sets its skip point");
                var ego = Ego(played);
                var egoBooted = Ego(booted);
                Check(!played.BootRunning && !HasPlayback() && Shown() == 2 &&
                      (ego.Room, ego.X, ego.Y, ego.Look.Facing) == (egoBooted.Room, egoBooted.X, egoBooted.Y, egoBooted.Look.Facing) &&
                      played.BootFrames == booted.BootFrames && played.CameraX == booted.CameraX &&
                      played.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x071]") == booted.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x071]"),
                    $"the playback ends as the app's boot does, after {full.Ms} ms: {Describe(played)}");
                Save("opening-end-room02-app.png");

                // 2. Escape one second after the first skip point.
                long skippedAt = -1;
                var skipped = await Play(async ms =>
                {
                    if (skippedAt >= 0 || !Session().CutsceneSkippable || !Find<Button>("SkipCutsceneButton").IsEnabled) return;
                    await Task.Delay(1000);
                    skippedAt = ms + 1000;
                    Call("SkipCutscene");
                });
                report.Add("2. The opening again, Escape one second after the first skip point:");
                report.AddRange(skipped.Log.Select(line => "      " + line));
                var afterSkip = Session();
                report.Add($"   End: {Describe(afterSkip)}");
                Check(afterSkip.SessionTrace.Any(line => line.StartsWith("abortCutscene: Script 44 continues at its skip jump", StringComparison.Ordinal)),
                    "the session trace shows abortCutscene at script 044's skip jump");
                var egoSkipped = Ego(afterSkip);
                Check(skippedAt > 0 && skipped.Ms < full.Ms && !afterSkip.BootRunning && Shown() == 2 &&
                      (egoSkipped.Room, egoSkipped.X, egoSkipped.Y, egoSkipped.Look.Facing) == (2, 828, 60, 180),
                    $"Escape at {skippedAt} ms ends the opening in Room 02 after {skipped.Ms} ms (played through: {full.Ms} ms), Bobbin facing the screen: {Describe(afterSkip)}");
                Save("opening-after-skip-app.png");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "opening-playback-check.txt"), report, System.Text.Encoding.UTF8); }
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

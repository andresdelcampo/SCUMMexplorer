using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;

// Sound in cutscenes, in the real MainWindow. For the opening (script 044) and script 046 (the Elders' trial), compares
// the sounds the app's scene player starts and stops, frame by frame, with the sound events a separate session reaches
// on the same frames from the same stated start. The opening plays on the real playback clock and checks that its music
// is audible (the MediaPlayer's position advances); script 046 is stepped with the playback's own timer tick as fast as
// the room loads allow. No physical input. Arguments: <Games\loom> <output folder>.
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
        object? Field(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string> { "Real MainWindow; PlayOpening and PlayKnownCutscene are called as the menu calls them. No physical input." };
            var failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); }
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = new GameFolderScanner().Scan(args[0]);
                var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
                int? Shown() => (int?)Field(window, "_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field(window, "_currentRoomResource"));
                for (var n = 0; n < 100 && Shown() is null; n++) await Task.Delay(100);
                var player = (SceneSoundPlayer)Field(window, "_sceneSound")!;
                bool HasPlayback() => Field(window, "_playback") is not null;

                // The reached starts and stops by frame (1 = the first frame the clock plays; earlier events play with it).
                List<string> Expected(LoomExplorationSession session, int frames)
                {
                    var lines = new List<string>();
                    var seen = session.SoundEvents.Count - PreFrame;
                    for (var frame = 1; frame <= frames; frame++)
                    {
                        session.AdvanceFrame();
                        for (; seen < session.SoundEvents.Count; seen++)
                        {
                            var item = session.SoundEvents[seen].Event;
                            if (item.Certainty != ScummScriptEventCertainty.Reached) continue;
                            if (item.Kind is ScummScriptEventKind.SoundStarted or ScummScriptEventKind.MusicStarted)
                                lines.Add($"f{frame} start {item.TargetId}");
                            else if (item.Kind == ScummScriptEventKind.SoundStopped) lines.Add($"f{frame} stop {item.TargetId}");
                        }
                    }
                    return lines;
                }
                List<string> Played(int from) => player.Log.Skip(from).Where(entry => entry.Action is "start" or "stop")
                    .Select(entry => $"f{entry.Frame} {entry.Action} {entry.SoundId}").ToList();
                void Compare(string title, List<string> expected, List<string> played, int frames)
                {
                    // Starts are logged once rendered, so compare as sorted sets per frame.
                    var a = expected.Where(line => int.Parse(line[1..line.IndexOf(' ')]) <= frames).Order().ToList();
                    var b = played.Where(line => int.Parse(line[1..line.IndexOf(' ')]) <= frames).Order().ToList();
                    report.Add($"{title}: expected (reached by the scripts): {string.Join(", ", a)}");
                    report.Add($"{title}: played by the app: {string.Join(", ", b)}");
                    Check(a.Count > 0 && a.SequenceEqual(b), $"{title}: the app starts and stops each sound on the frame the scripts reach it ({a.Count} expected, {b.Count} played)");
                }

                // 1. The opening on the real clock for 20 s.
                var from = player.Log.Count;
                PreFrame = 0;
                var clock = Stopwatch.StartNew();
                Call("PlayOpening");
                await Task.Delay(20000);
                var voices = (System.Collections.IList)Field(player, "_voices")!;
                var positions = voices.Cast<object>().Select(voice => (
                    Id: (int)voice.GetType().GetProperty("SoundId")!.GetValue(voice)!,
                    Player: (MediaPlayer?)voice.GetType().GetProperty("Player")!.GetValue(voice))).ToList();
                var openingFrames = player.Log.Count > from ? (int)Field(player, "_frames")! : 0;
                report.Add($"The opening, {clock.ElapsedMilliseconds} ms on the playback clock, {openingFrames} frames:");
                foreach (var entry in player.Log.Skip(from)) report.Add($"      {entry}");
                Compare("The opening", Expected(new LoomExplorationSession(game, objects, stopAtOpening: true), openingFrames),
                    Played(from), openingFrames);
                var music = positions.FirstOrDefault(voice => voice.Id == 25).Player;
                Check(music is not null && music.HasAudio && music.Position > TimeSpan.FromSeconds(10),
                    $"the opening's music (sound 25, AdLib) is playing: position {music?.Position:mm\\:ss\\.f}, audio {music?.HasAudio}");
                var kind25 = player.Log.Skip(from).FirstOrDefault(entry => entry is { Action: "start", SoundId: 25 })?.Kind;
                Check(kind25 == "music (AdLib)", $"sound 25 plays as {kind25}");
                Call("StopScenePlayback");

                // 2. Script 046, stepped with the playback's timer tick (its timer stopped), 2500 frames.
                var cutscenes = (Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var trial = cutscenes.Cast<object>().Single(item => (int)item.GetType().GetProperty("Script")!.GetValue(item)! == 46);
                from = player.Log.Count;
                Call("PlayKnownCutscene", trial);
                ((System.Windows.Threading.DispatcherTimer)Field(window, "_playbackTimer")!).Stop();
                var steps = 0;
                const int trialFrames = 2500;
                while (HasPlayback() && (int)Field(player, "_frames")! < trialFrames && steps++ < 20000)
                {
                    Call("PlaybackTimer_Tick", null, EventArgs.Empty);
                    ((System.Windows.Threading.DispatcherTimer)Field(window, "_playbackTimer")!).Stop();
                    await Task.Delay(1);
                }
                await Task.Delay(3000);
                var trialPlayed = (int)Field(player, "_frames")!;
                report.Add($"Script 046, {trialPlayed} frames stepped:");
                foreach (var entry in player.Log.Skip(from)) report.Add($"      {entry}");
                var expectedSession = new LoomExplorationSession(game, objects);
                expectedSession.VisitRoom(game.Rooms.First(room => room.ResourceId == 8));
                var before = expectedSession.SoundEvents.Count;
                expectedSession.ActivateObjectVerb(255, 0x38);
                PreFrame = expectedSession.SoundEvents.Count - before;
                Compare("Script 046", Expected(expectedSession, trialPlayed), Played(from), trialPlayed);
                var kinds = player.Log.Skip(from).Where(entry => entry.Action == "start").GroupBy(entry => entry.SoundId)
                    .Select(group => $"{group.Key}: {group.First().Kind}").ToList();
                report.Add("Kinds: " + string.Join("; ", kinds));
                Check(kinds.Contains("31: music (AdLib)") && kinds.Contains("8: effect (PC speaker)"),
                    "the trial's music (31) plays as AdLib and the notes (8, 4, 5, 1) as PC speaker effects");
                Call("StopScenePlayback");
                Call("PlayOpening");
                Check(((System.Collections.IList)Field(player, "_voices")!).Count == 0, "a new stated start stops the previous session's sounds");
                Call("StopScenePlayback");
                report.Add($"Failures: {failures}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(output, "scene-sound-check.txt"), report, System.Text.Encoding.UTF8); }
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

    // Events the stated start reached before the first frame (the verb's); they play with frame 1.
    static int PreFrame;
}

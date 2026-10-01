using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app = new Application(); var w = new MainWindow();
        var report = new List<string>(); int failures = 0;
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(w);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(w, values);
        T Find<T>(string name) => (T)w.FindName(name);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        void Capture(string name)
        {
            w.UpdateLayout(); var b = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            b.Render(w); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
            using var f = File.Create(Path.Combine(args[1], name + ".png")); enc.Save(f);
        }
        w.ContentRendered += async (_, _) =>
        {
            async Task Wait(Func<bool> condition, int limit = 700)
            { for (int i = 0; i < limit && !condition(); i++) await Task.Delay(50); if (!condition()) throw new TimeoutException(); }
            try
            {
                Call("OpenGameFolder", args[0]);
                Call("SelectResourceInTree", ((GameLibrary)Field("_game")!).Rooms.Single(r => r.ResourceId == 4));
                await Wait(() => Find<Image>("RoomPreview").IsVisible && Field("_session") is not null);
                var type = typeof(MainWindow).GetNestedType("KnownCutscene", BindingFlags.NonPublic)!;
                // Diagnostic explorer recipe: decoded direct verb, no state injections; not a menu promotion.
                var scene = Activator.CreateInstance(type, ["Sound completion diagnostic", 4, 177, 0x3A, 0,
                    "Room 04 object 177 response starts native sound helper.", null, "Dialogue and reactions", null])!;
                var player = (SceneSoundPlayer)Field("_sceneSound")!;
                Call("PlayKnownCutscene", scene);
                await Wait(() => Session().Snapshot.ActiveSounds.Contains(23) && Find<Image>("RoomPreview").IsVisible);
                Check(Session().InCutscene && Session().Snapshot.KnownGlobalVariables["var[0x0A4]"] == 23,
                    "normal clock reaches actual script040 sound23 wait");
                await Wait(() => player.Log.Any(row => row.Action == "start" && row.SoundId == 23));
                Check(player.Log.Any(row => row.Action == "start" && row.SoundId == 23), "native AdLib MediaPlayer opens on reached cue");
                Call("PauseScenePlayback"); int trace = Session().SessionTrace.Count;
                Capture("sound-wait-paused"); await Task.Delay(800);
                Check(Session().SessionTrace.Count == trace && Session().Snapshot.ActiveSounds.Contains(23), "pause suspends native clock and helper wait");
                Check(!Find<ComboBox>("SoundModeCombo").IsEnabled && !Find<Button>("CutsceneMenuButton").IsEnabled,
                    "playing and paused scene retains sound/start guards");
                Call("ResumeScenePlayback");
                await Wait(() => !Session().InCutscene && Field("_playback") is null);
                Check(Session().SessionTrace.Any(line => line.Contains("Sound 23 completed on its native")) &&
                    Session().SessionTrace.Any(line => line.StartsWith("endCutscene at ")), "normal-clock completion reaches matching scene end");
                Check(Session().Snapshot.KnownGlobalVariables["var[0x0A4]"] == 0 && !Session().Snapshot.ActiveSounds.Contains(23),
                    "script040 clears its own flag after native sound completion");
                Check(Find<Button>("CutsceneMenuButton").IsEnabled && Find<ComboBox>("SoundModeCombo").IsEnabled,
                    "finished preview restores idle controls");
                Capture("sound-wait-completed");
                player.SetMode(SceneSoundPlayer.SoundMode.None); int log = player.Log.Count;
                Call("PlayKnownCutscene", scene);
                await Wait(() => Session().InCutscene); await Wait(() => !Session().InCutscene && Field("_playback") is null);
                Check(Session().Snapshot.KnownGlobalVariables["var[0x0A4]"] == 0 &&
                    !player.Log.Skip(log).Any(row => row.Action == "start"), "muted output still completes native helper without opening media");
                report.Add("Visible MainWindow; real Dispatcher playback clock; synthetic diagnostic replay and pause handlers. No physical input or independent listening.");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1], "sound-lifetime-check.txt"), report); w.Close(); app.Shutdown(); Environment.ExitCode = failures == 0 ? 0 : 1; }
        };
        app.Run(w);
    }
}

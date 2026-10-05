using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// The Roland MT-32 device through the real window: the combo, the sound card the session reports, the sound panel for a Roland sound and
// for its AdLib twin, the refusal on another device, and the opening's sound commands under the Roland card.
// Arguments: <Games\loom ega en> <output folder>; the ROMs come from SCUMM_MT32_ROM_ROOT (else the folder beside the game folder: ..\mt32).
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Width = 1200, Height = 760 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        T Find<T>(string name) => (T)window.FindName(name);
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                typeof(MainWindow).GetMethod("OpenGameFolder", Private)!.Invoke(window, [args[0]]);
                await Task.Delay(1500);
                var game = (GameLibrary)Field("_game")!;
                var sceneSound = (SceneSoundPlayer)Field("_sceneSound")!;
                var combo = Find<ComboBox>("SoundModeCombo");
                report.Add($"combo items: {string.Join(", ", combo.Items.Cast<object>())}; selected {combo.SelectedItem}");
                report.Add($"game has Roland sounds: {SceneSoundPlayer.HasRolandSounds(game)}");
                var session = () => (LoomExplorationSession)Field("_session")!;
                report.Add($"AdLib: session sound card {session().SoundCard}");

                async Task Panel(string label, int id)
                {
                    var sound = game.Resources.First(r => r.Kind == ResourceKind.Sound && r.ResourceId == id);
                    var task = (Task)typeof(MainWindow).GetMethod("ShowAudio", Private)!.Invoke(window, [sound, false, false])!;
                    await task;
                    report.Add($"{label} sound {id}: time \"{Find<TextBlock>("AudioTime").Text}\", Play enabled {Find<Button>("PlayButton").IsEnabled}; \"{Find<TextBlock>("AudioDescription").Text}\"");
                }

                await Panel("AdLib, Roland-only", 53);
                combo.SelectedIndex = (int)SceneSoundPlayer.SoundMode.Roland;
                await Task.Delay(300);
                report.Add($"after choosing Roland: mode {sceneSound.Mode}, ROMs {sceneSound.Roms?.Model}, session sound card {session().SoundCard}, combo \"{combo.SelectedItem}\"");
                await Panel("Roland, Roland sound", 53);
                await Panel("Roland, its AdLib twin", 21);
                await Panel("Roland, slot 24 (its twin 56 is the overture, which no other edition has)", 24);

                // The opening under the Roland card: fresh session, the game's own sound commands.
                typeof(MainWindow).GetMethod("PlayOpening", Private)!.Invoke(window, null);
                // The overture is still being rendered (about 9 s): the scene must hold, then run with the music from its start.
                for (var second = 1; second <= 14; second++)
                {
                    await Task.Delay(1000);
                    report.Add($"  t={second,2}s: preparing {sceneSound.IsPreparing}, room {session().Snapshot.RoomId}, cutscene frames run {session().CutscenesBegun}, log {string.Join("/", sceneSound.Log.Where(e => e.Action != "mode").Select(e => e.Action))}");
                }
                var log = sceneSound.Log.Where(e => e.Action != "mode").Take(12).Select(e => $"frame {e.Frame} {e.Action} sound {e.SoundId} {e.Kind}").ToList();
                report.Add($"opening: session sound card {session().SoundCard}; sound log: {(log.Count == 0 ? "(empty)" : string.Join(" | ", log))}");
                var started = session().SoundEvents.Select(e => e.Event.TargetId).OfType<int>().Distinct().OrderBy(i => i).ToList();
                report.Add($"sound ids the scripts started: {string.Join(", ", started)}");

                combo.SelectedIndex = (int)SceneSoundPlayer.SoundMode.Adlib;
                await Task.Delay(300);
                report.Add($"device change while the opening plays: mode {sceneSound.Mode} (refused until the scene is over)");
                // Restart: a fresh session at the Roland card, no scene running, so the device may change.
                typeof(MainWindow).GetMethod("ResetSession_Click", Private)!.Invoke(window, [window, new RoutedEventArgs()]);
                await Task.Delay(500);
                report.Add($"after Restart: session sound card {session().SoundCard}");
                combo.SelectedIndex = (int)SceneSoundPlayer.SoundMode.Adlib;
                await Task.Delay(300);
                report.Add($"back on AdLib: mode {sceneSound.Mode}, session sound card {session().SoundCard}");
                await Panel("AdLib, Roland-only", 53);
            }
            catch (Exception ex) { report.Add("FAILED: " + ex); }
            File.WriteAllLines(Path.Combine(args[1], "roland-check.txt"), report);
            app.Shutdown();
        };
        app.Run(window);
    }
}

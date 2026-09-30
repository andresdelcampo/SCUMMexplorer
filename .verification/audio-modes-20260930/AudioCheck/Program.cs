using System.Buffers.Binary;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var app = new Application(); var window = new MainWindow();
        var report = new List<string>(); int failures = 0;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var game = new GameFolderScanner().Scan(args[0]);
                var sounds = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue).ToDictionary(r => r.ResourceId!.Value);
                foreach (var sound in sounds.Values.Where(r => r.ResourceId <= 33))
                {
                    var file = File.ReadAllBytes(sound.FilePath); for (int i = 0; i < file.Length; i++) file[i] ^= 255;
                    int start = sound.Offset!.Value, offset = start + BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(start));
                    report.Add($"Sound {sound.ResourceId}: offset {start}, speaker {offset-start}, next header {Convert.ToHexString(file.AsSpan(offset, 10))}");
                }
                Call("OpenGameFolder", args[0]); await Task.Delay(1000);
                var player = (SceneSoundPlayer)Field("_sceneSound")!;
                var combo = (ComboBox)window.FindName("SoundModeCombo");
                Check(combo.IsVisible && combo.SelectedIndex == 2, "AdLib is the visible default");
                var startMethod = typeof(SceneSoundPlayer).GetMethod("Start", Private)!;
                void Start(int id) => startMethod.Invoke(player, [id, "Synthetic audio verification command", false]);
                var voicesField = typeof(SceneSoundPlayer).GetField("_voices", Private)!;
                object[] Voices() => ((System.Collections.IEnumerable)voicesField.GetValue(player)!).Cast<object>().ToArray();
                async Task Opened(int count)
                {
                    for (int i = 0; i < 200 && player.Log.Count(e => e.Action == "opened") < count; i++) await Task.Delay(50);
                }
                Start(25); await Opened(1); Start(1); await Opened(2);
                Check(player.Log.Any(e => e.Action == "opened" && e.SoundId == 25 && e.Kind == "music (AdLib)") && player.Log.Any(e => e.Action == "opened" && e.SoundId == 1 && e.Kind == "effect (AdLib)"), "native music and effect both opened");
                Check(Voices().Length == 2, "music and effect overlap");
                combo.SelectedIndex = 0;
                Check(Voices().Length == 0 && player.Mode == SceneSoundPlayer.SoundMode.None, "mute stops every voice");
                Start(2); await Task.Delay(200);
                Check(Voices().Length == 0 && player.Log.Any(e => e.Action == "muted" && e.SoundId == 2), "new commands remain silent while muted");
                combo.SelectedIndex = 1; Start(25); Start(1); await Opened(4);
                Check(player.Log.Any(e => e.Action == "opened" && e.SoundId == 25 && e.Kind == "music (PC speaker)") && player.Log.Any(e => e.Action == "opened" && e.SoundId == 1 && e.Kind == "effect (PC speaker)"), "speaker mode applies to music and effects");
                combo.SelectedIndex = 2; Start(2); combo.SelectedIndex = 0; await Task.Delay(1000);
                Check(Voices().Length == 0, "muting a pending render prevents late playback");
                combo.SelectedIndex = 2;
                await (Task)Call("ShowAudio", sounds[1], false, false)!;
                Check((bool)Field("_audioReady")!, "Advanced sound preview uses native AdLib effects");
                combo.SelectedIndex = 0; await Task.Delay(200);
                Check(!(bool)Field("_audioReady")! && ((TextBlock)window.FindName("AudioDescription")).Text.Contains("No Sound"), "mute closes the manual sound preview");
                combo.SelectedIndex = 2;
                await (Task)Call("ShowAudio", sounds[24], false, false)!;
                Check(!(bool)Field("_audioReady")! && ((TextBlock)window.FindName("AudioDescription")).Text.Contains("No DOS"), "unsupported bundle is explicit; no speaker fallback");
                report.AddRange(player.Log.Select(e => e.ToString()));
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1], "audio-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: PlaySoundWindowCheck <Indy3 EGA folder> <Monkey Island floppy folder> <output folder>
// S6 sound (16 October 2026): opens the real MainWindow with a private profile (SCUMM_EXPLORER_PROFILE_ROOT in the output folder) and starts play
// through the tree, as S6 did. Indiana Jones: in Room 73, "Close" + the door (Script 27 +0x002B startSound 3). Monkey Island: play from the dock,
// then Room 28 picked from the list (its entry +0x008A starts the bar music 101), Space through the window's key handler (pause and resume), and
// a sound resource picked in the tree (play is left). Writes the window's scene-sound log (what was started, on which device, and when the wave
// opened) to sound-check.txt. The agent cannot hear: the log shows the events were issued and the waves opened, not how they sound.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[2];
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            var player = (SceneSoundPlayer)Field("_sceneSound")!;
            var logged = 0;
            void Flush(string step)
            {
                report.Add($"-- {step} (device {SceneSoundPlayer.DeviceName(player.Mode)})");
                foreach (var entry in player.Log.Skip(logged)) report.Add($"   frame {entry.Frame} {entry.Milliseconds} ms {entry.Action} {entry.SoundId} {entry.Kind} | {entry.Source}");
                logged = player.Log.Count;
            }
            async Task Select(int id, string kind)
            {
                var tree = (TreeView)window.FindName("ResourceTree");
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == id &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == kind);
                Call("SelectResourceInTree", item.Tag);
                await Task.Delay(300);
            }
            async Task WaitFor(Func<bool> condition, int tenths) { for (var n = 0; n < tenths && !condition(); n++) await Task.Delay(100); }
            bool Started(int id) => player.Log.Any(entry => entry.SoundId == id && entry.Action is "start" or "preview start");
            bool Opened(int id) => player.Log.Any(entry => entry.SoundId == id && entry.Action == "opened");
            try
            {
                // Indiana Jones EGA, Room 73.
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1500);
                await Select(73, "Room");
                await WaitFor(() => Field("_playTimer") is not null, 600);
                var indy = (ScummGameSession?)Field("_play") ?? throw new Exception("Indy3 play did not start");
                await WaitFor(() => indy.UserInputOn && !indy.InCutscene, 100);
                report.Add($"Indy3 play in Room {indy.CurrentRoomId}; the scene sound player follows the play session: {player.Follows(indy)}");
                var close = indy.PlayVerbArea().Items.Single(entry => entry.VerbId == 0x02);
                indy.ClickScreen(close.X + 1, close.Y + 1, out _);
                await Task.Delay(300);
                for (var i = 0; i < 2; i++)
                {
                    indy.PointAtScreen((56 - indy.ViewLeft, 60 + indy.MainScreen.Top));
                    await Task.Delay(150);
                    indy.ClickScreen(56 - indy.ViewLeft, 60 + indy.MainScreen.Top, out _);
                    await Task.Delay(300);
                }
                await WaitFor(() => Opened(3), 150);
                Flush("Indy3 Room 73: Close + door (Script 27 +0x002B startSound 3)");
                report.Add($"sound 3 started {Started(3)}, wave opened {Opened(3)}; last session sound events: " +
                           string.Join("; ", indy.SoundEvents.Skip(Math.Max(0, indy.SoundEvents.Count - 3)).Select(e => $"{e.Event.Kind} {e.Event.TargetId} {e.Event.SourceLabel} +0x{e.Event.Offset:X4}")));

                // Monkey Island floppy: play from the dock, then the SCUMM Bar from the list.
                Call("OpenGameFolder", args[1]);
                await Task.Delay(1500);
                Flush("another game opened");
                await Select(33, "Room");
                await WaitFor(() => Field("_playTimer") is not null && Field("_play") is ScummGameSession s && !ReferenceEquals(s, indy), 600);
                var monkey = (ScummGameSession?)Field("_play") ?? throw new Exception("Monkey play did not start");
                report.Add($"Monkey play in Room {monkey.CurrentRoomId}; follows {player.Follows(monkey)}");
                await Select(28, "Room");
                await WaitFor(() => Opened(101), 300);
                Flush("Room 28 picked from the list (entry +0x008A startSound var[0x04B] = 101)");
                report.Add($"Room {monkey.CurrentRoomId}; sound 101 started {Started(101)}, wave opened {Opened(101)}; the session has 101 running: {monkey.Snapshot.ActiveSounds.Contains(101)}");
                var source = PresentationSource.FromVisual(window) ?? throw new Exception("no presentation source");
                Call("PlayKeyDown", new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = UIElement.PreviewKeyDownEvent });
                await Task.Delay(400);
                report.Add($"Space: game paused {Field("_playPaused")}, sounds paused {Field("_playSoundPaused")}");
                Call("PlayKeyDown", new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = UIElement.PreviewKeyDownEvent });
                await Task.Delay(400);
                report.Add($"Space again: game paused {Field("_playPaused")}, sounds paused {Field("_playSoundPaused")}");
                var game = (GameLibrary)Field("_game")!;
                var sound = game.Resources.First(r => r.Kind == ResourceKind.Sound && r.ResourceId is not null);
                await Select(sound.ResourceId!.Value, "Sound");
                await Task.Delay(500);
                Flush("a sound picked in the tree (play left)");
                report.Add($"play timer after leaving: {(Field("_playTimer") is null ? "stopped" : "running")}");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "sound-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

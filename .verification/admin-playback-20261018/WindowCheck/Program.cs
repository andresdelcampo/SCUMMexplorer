using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// <fixture> <private output>. Actual MainWindow, real source scripts and event
// handlers. Explicit visits and direct scene starts are exploration, not a route.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG", Path.Combine(output, "failure.log"));
        Environment.SetEnvironmentVariable("SCUMM_CD_INTRO_AUTOANSWER", "1"); // Existing disclosed harness assumption for LOOM CD's skill question.
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, Private)!.SetValue(window, value);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethods(Private).Single(m => m.Name == name && m.GetParameters().Length == values.Length).Invoke(window, values);
        T Control<T>(string name) => (T)window.FindName(name);
        var log = new List<string>();
        void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); log.Add("PASS " + text); }
        void Save(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Control<ComboBox>("SoundModeCombo").SelectedIndex = 0; // Private profile; avoid rendering audio in a control-state check.
                Call("OpenGameFolder", args[0]);
                ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
                ((ManualResetEventSlim)Field("_scanGate")!).Reset(); // This is a control check, not a scene-census rerun.
                var game = (GameLibrary)Field("_game")!;
                var cd = game.Id == GameId.LoomVga;
                var sound = Control<ComboBox>(cd ? "CdSoundCombo" : "SoundModeCombo");
                if (cd) sound.SelectedIndex = 0;
                Require(Control<CheckBox>("AdvancedModeToggle").IsChecked == false, "Normal mode default");
                Require(sound.Visibility == Visibility.Visible, "Existing sound selector visible in Normal before a room is opened");
                var roomId = game.Id is GameId.Loom or GameId.LoomVga ? 2 : 1;
                Call("SelectResourceInTree", game.Rooms.First(r => r.ResourceId == roomId));
                for (var i = 0; i < 1200 && (game.Id is GameId.Loom or GameId.LoomVga ? Field("_currentRoomResource") is not GameResource : Field("_playTimer") is null); i++) await Task.Delay(50);
                Save("01-normal-sound");
                if (args.Length > 2 && args[2] == "device-only")
                {
                    var timer = (DispatcherTimer?)Field("_playTimer") ?? throw new Exception("Play timer missing"); timer.Stop();
                    var session = (ScummGameSession)Field("_play")!;
                    Require(!session.InCutscene, "Device regression starts outside a native cutscene");
                    var before = session.Snapshot.KnownGlobalVariables.Where(p => p.Key != ScummVars.SoundCard).OrderBy(p => p.Key).ToArray();
                    sound.SelectedIndex = 2;
                    Require(session.SoundCard == 3, "V3+ AdLib selection sets native sound-card value 3");
                    sound.SelectedIndex = 1;
                    Require(session.SoundCard == 0, "V3+ Speaker selection sets native sound-card value 0");
                    Require(before.SequenceEqual(session.Snapshot.KnownGlobalVariables.Where(p => p.Key != ScummVars.SoundCard).OrderBy(p => p.Key)), "V3+ device switch preserves all unrelated game words");
                    Save("02-device-only");
                    Call("StopPlay"); File.WriteAllLines(Path.Combine(output, "result.txt"), log); app.Shutdown(0); return;
                }
                if (game.Id is GameId.Loom or GameId.LoomVga)
                {
                    Call(cd ? "PlayCdIntro" : "PlayOpening");
                    ((DispatcherTimer?)Field("_playbackTimer"))?.Stop();
                    var session = (LoomExplorationSession)Field("_session")!;
                    for (var i = 0; i < 400 && !session.CutsceneSkippable; i++)
                    { Call("PlaybackTimer_Tick", null, EventArgs.Empty); ((DispatcherTimer?)Field("_playbackTimer"))?.Stop(); await Task.Delay(1); }
                    Call("UpdatePlaybackControls");
                    Require(session.CutsceneSkippable && Control<Button>("SkipCutsceneButton").IsEnabled, "LOOM native opening override enables Skip");
                    Require(!sound.IsEnabled, "LOOM scene locks its sound selector");
                    Call("PlaybackPause_Click", window, new RoutedEventArgs());
                    Require((bool)Field("_playbackPaused")! && Control<Button>("PlaybackPauseButton").Content.ToString()!.Contains("Play"), "LOOM paused caption updates immediately");
                    Save("02-paused-native-scene");
                    Call("SkipCutscene_Click", window, new RoutedEventArgs());
                    Require(!(bool)Field("_playbackPaused")!, "LOOM Skip resumes its paused native scene");
                    Save("03-skip-resumed");
                    Call("StopScenePlayback");
                }
                else
                {
                    var timer = (DispatcherTimer?)Field("_playTimer") ?? throw new Exception("Play timer missing");
                    await Task.Delay(120); timer.Stop();
                    var session = (ScummGameSession)Field("_play")!;
                    async Task Frame()
                    {
                        var before = (int)Field("_playFrame")!;
                        for (var i = 0; i < 1000 && (int)Field("_playFrame")! == before; i++)
                        { Call("PlayTimer_Tick", null, EventArgs.Empty); if ((int)Field("_playFrame")! == before) await Task.Delay(5); }
                        Require((int)Field("_playFrame")! > before, "Native frame advances");
                    }
                    if (GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced)
                        SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.PrepareIfNeeded(game, session);
                    else
                    {
                        for (var i = 0; i < 600 && session.InCutscene; i++) { if (session.CutsceneSkippable) session.PressKey(27); await Frame(); }
                    }
                    Call("UpdatePlaybackControls");
                    Require(!session.InCutscene && Control<Button>("PlaybackPauseButton").IsEnabled && !Control<Button>("SkipCutsceneButton").IsEnabled,
                        "Ordinary live Play can Pause while Skip is unavailable");
                    Require(sound.IsEnabled, "Ordinary Play sound selector is unlocked");
                    var words = session.Snapshot.KnownGlobalVariables.ToDictionary();
                    sound.SelectedIndex = 1;
                    Require(words.OrderBy(x => x.Key).SequenceEqual(session.Snapshot.KnownGlobalVariables.OrderBy(x => x.Key)), "Changing a v1/v2 audio renderer never overwrites game variable words");
                    sound.SelectedIndex = 0;
                    Call("PlaybackPause_Click", window, new RoutedEventArgs());
                    Require((bool)Field("_playPaused")! && Control<Button>("PlaybackPauseButton").Content.ToString()!.Contains("Play"), "Pause button immediately shows Play");
                    var image = Control<Image>("PlayScreenImage"); image.Focus();
                    var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent };
                    Call("PlayKeyDown", key);
                    Require(!(bool)Field("_playPaused")! && Control<Button>("PlaybackPauseButton").Content.ToString()!.Contains("Pause"), "Space immediately restores Pause caption without a frame");
                    Save("02-ordinary-pause-without-skip");
                    var maniac = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;
                    session.VisitRoom(game.Rooms.First(r => r.ResourceId == (maniac ? 17 : 2)));
                    if (maniac) session.ActivateObjectVerb(190, 4);
                    for (var i = 0; i < 1500 && !session.CutsceneSkippable; i++) await Frame();
                    Call("RefreshPlayControlsOnFrame");
                    Require(session.CutsceneSkippable && Control<Button>("SkipCutsceneButton").IsEnabled, "Native override is reflected on the next frame, independent of the room-list refresh cadence");
                    Require(!sound.IsEnabled, "Generic native cutscene locks sound device");
                    sound.SelectedIndex = 1;
                    Require(sound.SelectedIndex == 0, "Selection handler rejects a device change during a native scene");
                    Call("PlaybackPause_Click", window, new RoutedEventArgs());
                    Save("03-paused-native-scene");
                    // A launch list is independent of the actual native cutscene.
                    // Simulate an absent catalog while keeping this real source scene.
                    Set("_playPlanner", null); Set("_playRoomListsDone", true); Set("_playSavedRoomLists", null);
                    Call("UpdatePlaybackControls");
                    Require(Control<Button>("RoomCutsceneButton").Visibility == Visibility.Collapsed && Control<Button>("SkipCutsceneButton").IsEnabled,
                        "A native skippable scene still offers Skip when no room launch entry is offered");
                    Save("03b-native-skip-without-room-launch");
                    Call("SkipCutscene_Click", window, new RoutedEventArgs());
                    Require(!(bool)Field("_playPaused")!, "Generic Skip resumes from its paused native override, like LOOM");
                    Require(Control<Button>("SkipCutsceneButton").IsEnabled == (session.InCutscene && session.CutsceneSkippable), "Skip state updates immediately after the click");
                    await Frame();
                    Call("ShowPlayFrame"); Save("04-skip-resumed");
                    Call("UpdatePlaybackControls");
                    Require(Control<Button>("RoomCutsceneButton").Visibility == Visibility.Collapsed, "No room Play Cutscene launch without an offered derived entry");
                    Require(Control<Button>("SkipCutsceneButton").IsEnabled == (session.InCutscene && session.CutsceneSkippable), "Empty room launch list does not control native Skip");
                    Call("StopPlay");
                }
                Require(Control<Button>("CutsceneMenuButton").Visibility == Visibility.Collapsed, "Global launch picker remains Advanced only");
                log.Add("PASS completed real-window control-state checks; prepared source execution, not audiovisual fidelity.");
                File.WriteAllLines(Path.Combine(output, "result.txt"), log);
                app.Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllLines(Path.Combine(output, "failure.txt"), log.Append(ex.ToString())); app.Shutdown(1); }
        };
        app.Run(window);
    }
}

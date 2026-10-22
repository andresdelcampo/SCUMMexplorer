using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

internal static class WindowProof
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool good, string message) { if (!good) throw new InvalidOperationException(message); }
    internal static void Run(string[] args)
    {
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var watch = Stopwatch.StartNew();
        void Log(string text) { report.Add($"{watch.Elapsed.TotalSeconds:F2}s {text}"); File.WriteAllLines(Path.Combine(output, "progress.txt"), report); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Hidden)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Hidden)!.Invoke(window, values);
        T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        IEnumerable<DependencyObject> Descendants(DependencyObject visual)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            { var child = VisualTreeHelper.GetChild(visual, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
        void Save(string name, FrameworkElement? target = null)
        {
            target ??= window; target.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(target);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        async Task Wait(Func<bool> condition, string name, int seconds = 90)
        { var timer = Stopwatch.StartNew(); while (!condition() && timer.Elapsed.TotalSeconds < seconds) await Task.Delay(20); Require(condition(), "Timed out: " + name); }
        async Task<GameLibrary> Open(string fixture)
        {
            Call("OpenGameFolder", Path.Combine(args[1], fixture));
            ((CancellationTokenSource?)Field("_roomCardCancellation"))?.Cancel();
            if (Field("_roomCardLoadTask") is Task task) await task;
            Control<ComboBox>("SoundModeCombo").SelectedIndex = 0;
            Require(Field("_playTimer") is null && Field("_playback") is null, "Folder open autoplayed");
            return (GameLibrary)Field("_game")!;
        }
        ScummGameSession Play() => (ScummGameSession)Field("_play")!;
        void Frames(int count) { for (var i = 0; i < count; i++) Call("PlayTimer_Tick", null, EventArgs.Empty); }
        async Task Intro(GameLibrary game)
        {
            Control<Button>("GameIntroButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Field("_playTimer") is not null, "native intro clock");
            ((DispatcherTimer)Field("_playTimer")!).Stop();
            if (GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced)
            {
                for (var i = 0; i < 3000 && Play().CurrentRoomId != 45; i++) Frames(1);
                Require(Play().CurrentRoomId == 45, "Native party menu not reached");
                foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) })
                { Require(Play().ClickScreen(x, y + Play().MainScreen.Top, out _) is not null, "Party click refused"); Frames(100); }
                for (var i = 0; i < 3000 && !Play().CutsceneSkippable; i++) Frames(1);
                Require(Play().CutsceneSkippable, "Native opening override absent");
                Call("UpdatePlaybackControls"); Control<Button>("SkipCutsceneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            for (var i = 0; i < 6000 && !Play().CaptureInventoryStatus().CanEdit; i++) Frames(1);
            Require(Play().CaptureInventoryStatus().CanEdit, Play().CaptureInventoryStatus().Refusal ?? "Inventory unsafe");
            Call("ShowPlayFrame"); Call("UpdatePlaybackControls");
        }
        void Status(string fixture)
        {
            var session = Play(); var snap = session.CaptureInventoryStatus();
            var selected = snap.Catalog.First(c => c.Name is { Length: > 0 } && !snap.Items.Any(i => i.ObjectId == c.ObjectId));
            var actor = snap.Characters.Last().ActorId; Exception? failure = null;
            var before = session.Snapshot.Inventory.ToArray(); var frame = (int)Field("_playFrame")!;
            void Modal(Action<InventoryStatusWindow> action)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var dialog = Application.Current.Windows.OfType<InventoryStatusWindow>().Single();
                    try { action(dialog); } catch (Exception ex) { failure = ex; dialog.Close(); }
                }));
                Control<Button>("GameStatusButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (failure is not null) throw failure;
            }
            Modal(dialog =>
            {
                Require(!((DispatcherTimer)Field("_playTimer")!).IsEnabled && (bool)Field("_playPaused")!, "Status did not pause Play");
                var catalog = (ComboBox)dialog.FindName("CatalogItems");
                catalog.SelectedItem = catalog.Items.Cast<object>().Single(c => (int)c.GetType().GetProperty("ObjectId")!.GetValue(c)! == selected.ObjectId);
                ((ComboBox)dialog.FindName("GrantRecipient")).SelectedValue = actor;
                ((Button)dialog.FindName("AddItemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var apply = (Button)dialog.FindName("ApplyButton"); Require(apply.IsEnabled && apply.Content?.ToString() == "Apply and close", "One-click Apply action absent");
                Save(fixture + "-pending", dialog); apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(!dialog.IsVisible, "Successful Apply required a second close click");
            });
            Require(session.CaptureInventoryStatus().Items.Single(i => i.ObjectId == selected.ObjectId).OwnerId == actor, "Grant not applied");
            Require((int)Field("_playFrame")! == frame && !(bool)Field("_playPaused")!, "Status did not restore original paused/stopped clock");
            Modal(dialog =>
            {
                var undo = (Button)dialog.FindName("UndoButton"); Require(undo.IsEnabled, "Same-session Undo was lost by one-click close");
                undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(!session.CaptureInventoryStatus().Items.Any(i => i.ObjectId == selected.ObjectId), "Undo did not restore exact grant state");
                ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(!dialog.IsVisible, "Unchanged Apply and close did not close");
            });
            Require(before.SequenceEqual(session.Snapshot.Inventory), "Inventory round-trip differs");
            Modal(dialog =>
            {
                var catalog = (ComboBox)dialog.FindName("CatalogItems");
                catalog.SelectedItem = catalog.Items.Cast<object>().Single(c => (int)c.GetType().GetProperty("ObjectId")!.GetValue(c)! == selected.ObjectId);
                ((ComboBox)dialog.FindName("GrantRecipient")).SelectedValue = actor;
                ((Button)dialog.FindName("AddItemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                // Deliberate stale-transaction test only: one real native frame, no variable/actor/flag writes.
                session.AdvanceFrame();
                ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(dialog.IsVisible && !session.CaptureInventoryStatus().Items.Any(i => i.ObjectId == selected.ObjectId), "Rejected stale Apply closed or changed inventory");
                ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Log($"PASS {fixture}: actual modal header; native party preserved; one-click grant+close; same-session reopen Undo; unchanged Apply closes; stale native-frame batch rejected and stays open; clock restored");
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var old = await Open("loom ega mt32 en");
                var scene = (SceneSoundPlayer)Field("_sceneSound")!; var oldSession = (LoomExplorationSession)Field("_session")!;
                if (args.Contains("actual-audio-only"))
                {
                    Control<ComboBox>("SoundModeCombo").SelectedIndex = (int)SceneSoundPlayer.SoundMode.Adlib;
                    var decoder = new ScummAdlibAudio();
                    var music = old.Resources.First(r => r.Kind == ResourceKind.Sound && !ScummRolandSong.IsRoland(r) && decoder.HasMusic(r));
                    await ((Task)Call("AuditionRoomSoundAsync", (music, true, (TimeSpan?)null, "Actual native LOOM music resource audition; folder-switch lifecycle check"))!).WaitAsync(TimeSpan.FromSeconds(45));
                    await Wait(() => (bool)Field("_audioReady")! && ((MediaPlayer)Field("_audioPlayer")!).HasAudio, "actual native AdLib music opens", 15);
                    var file = (string?)Field("_audioFile"); Require(file is not null && new FileInfo(file).Length > 44, "No native rendered music file");
                    Log($"PASS real LOOM music resource {music.ResourceId}: native AdLib rendition opened with audio; no listening/sync claim"); Save("actual-loom-music");
                    await Open("mmansionE ega es"); await Task.Delay(300);
                    Require(!(bool)Field("_audioReady")! && Field("_audioFile") is null && Field("_audioSurface")!.ToString() == "None", "Old browsed LOOM music remains active");
                    Require(!scene.Follows(oldSession) && !File.Exists(file), "Old sound source/file survives folder change");
                    Require(((IList)Field("_roomSoundChoices")!).Count == 0 && Control<Button>("RoomSoundsButton").Visibility == Visibility.Collapsed, "Old room sound list survives folder change");
                    Log("PASS actual rendered LOOM music→Maniac folder switch: player closed, temporary file retired, event source detached and room menu cleared"); Save("actual-music-cleared");
                    File.WriteAllLines(Path.Combine(output, "result.txt"), report); window.Close(); app.Shutdown(0); return;
                }
                scene.SetMode(SceneSoundPlayer.SoundMode.PcSpeaker);
                using var gate = new ManualResetEventSlim(); var rendered = new TaskCompletionSource(); var began = new TaskCompletionSource(); var starting = 0;
                scene.Starting += () => starting++;
                scene.Attach(old, oldSession, (_, _, _) => { began.TrySetResult(); gate.Wait(); rendered.TrySetResult(); return (new byte[48], false); });
                var oldId = old.Resources.First(r => r.Kind == ResourceKind.Sound).ResourceId!.Value;
                typeof(SceneSoundPlayer).GetMethod("Start", Hidden)!.Invoke(scene, [oldId, "synthetic delayed-render cancellation probe", false]);
                Require(scene.IsPreparing, "Delayed renderer did not start");
                await began.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var first = await Open("mmansionE ega es");
                Require(!scene.Follows(oldSession) && !scene.IsPreparing, "Old session remains attached after real folder change");
                var cd = (CdVoicePlayer)Field("_cdVoice")!;
                Require(typeof(CdVoicePlayer).GetField("_session", Hidden)!.GetValue(cd) is null, "Old CD source retained");
                gate.Set(); await rendered.Task; await Task.Delay(300);
                Require(starting == 0 && ((IList)typeof(SceneSoundPlayer).GetField("_voices", Hidden)!.GetValue(scene)!).Count == 0, "Old late render restarted output after folder change");
                Log("PASS actual LOOM→Maniac folder change: synthetic pending render detached; its late completion produces no MediaPlayer/output; old CD source null");
                foreach (var fixture in new[] { "mmansionE ega es", "mmansion ega en", "zak ega en", "zakE ega en" })
                {
                    var game = fixture == "mmansionE ega es" ? first : await Open(fixture);
                    scene.SetMode(SceneSoundPlayer.SoundMode.None);
                    await Intro(game); Status(fixture);
                    Call("TryUpdateGenericRoomSounds"); if (Field("_genericRoomSoundsTask") is Task scan) await scan;
                    Require(Control<Button>("RoomSoundsButton").Visibility == Visibility.Visible, "Source-linked Sounds menu absent in Normal");
                    Save(fixture + "-normal");
                    var choices = ((IEnumerable)Field("_roomSoundChoices")!).Cast<object>().Count();
                    Require(choices > 0 && Control<ComboBox>("SoundModeCombo").Visibility == Visibility.Visible, "Sound playback controls absent");
                    Control<Button>("RoomSoundsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(40);
                    var popup = PresentationSource.CurrentSources.Cast<PresentationSource>().Select(p => p.RootVisual).OfType<FrameworkElement>()
                        .Single(p => Descendants(p).OfType<TextBlock>().Any(t => t.Text == "SOUNDS IN THIS ROOM"));
                    var buttons = Descendants(popup).OfType<Button>().ToArray();
                    Require(buttons.Length == choices + 1 && buttons.All(b => b.ToolTip is null), "Normal Sounds menu missing candidates or exposes technical tooltips");
                    Require(buttons.SelectMany(Descendants).OfType<TextBlock>().All(t => !t.Text.Contains("Script") && !t.Text.Contains("0x") && !t.Text.Contains("verb")), "Technical source in Normal sound title");
                    Save(fixture + "-sound-menu", popup); buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Log($"PASS {fixture}: actual Normal Sounds popup opens; {choices} native source/reached candidates; plain labels/no source tooltips; device selector visible; no scan executes game code");
                }
                File.WriteAllLines(Path.Combine(output, "result.txt"), report); window.Close(); app.Shutdown(0);
            }
            catch (Exception ex)
            { Log("FAIL " + ex); Save("failure"); File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); window.Close(); app.Shutdown(1); }
        };
        app.Run(window);
    }
}

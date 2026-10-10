using System.IO;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG", Path.Combine(output, "failures.log"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, Left = -4000, Top = -4000, ShowActivated = false, ShowInTaskbar = false };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        var report = new List<string>();
        void Check(bool ok, string label) => report.Add((ok ? "PASS " : "FAIL ") + label);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Find<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                Find<ComboBox>("CdSoundCombo").SelectedIndex = 0;
                Field("_cdVoice")!.GetType().GetProperty("Enabled")!.SetValue(Field("_cdVoice"), false);
                Set("_playInstantly", true);
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                Find<ToggleButton>("SessionModeToggle").IsChecked = true;
                var game = (GameLibrary)Field("_game")!;
                var data = ScummDataSources.For(game);
                Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 9));
                for (var n = 0; n < 800 && (Session().CurrentRoomId != 9 || Field("_playback") is not null); n++) await Task.Delay(25);
                (Field("_idleFrameTimer") as DispatcherTimer)?.Stop();
                Check(Session().CurrentRoomId == 9, "real room-card selection opens Room 09");
                var scanner = LoomSceneStateScanner.For(game);
                Save("placements", new[] { 4, 5 }.Select(a => new { actor = a, placements = scanner.FindActorPlacers(a, 9) }));
                foreach (var id in new[] { 47, 101, 105 }) File.WriteAllText(Path.Combine(output, $"global-{id}.txt"), data.GlobalScript(id)?.Format());
                foreach (var id in new[] { 209, 216, 217, 218, 221, 223, 224 }) File.WriteAllText(Path.Combine(output, $"local-{id}.txt"), data.RoomLocalScript(9, id)?.Format());
                var indexed = game.V4Archive?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
                if (game.Id == GameId.LoomVga)
                {
                    var triggers = LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value));
                    var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
                    Set("_cdSetupPlanner", new Lazy<LoomCdSceneSetupPlanner>(() => planner));
                    var targets = LoomCutsceneIndex.Group(triggers).Where(g => g.First.Room == 9 && g.First.VerbId >= 0x39 && g.First.ObjectName is "Hetchel" or "Dead One").ToArray();
                    Save("triggers", targets);
                    Check(targets.Any(g => g.First.ObjectName == "Hetchel") && targets.Any(g => g.First.ObjectName == "Dead One"), "picker has both actor targets");
                    foreach (var group in targets)
                    foreach (var mode in new[] { "picker", "interact" })
                    {
                        Call("StopScenePlayback");
                        var session = new LoomExplorationSession(game, indexed);
                        Set("_session", session);
                        session.VisitRoomById(9);
                        LoomCdSceneSetupPlanner.Settle(session);
                        // Prepared status only checks retention; this is not an earned route.
                        session.UpdateAvailableNotes(8);
                        session.UpdateDraftLearned(15, false);
                        var actor = group.First.ObjectName == "Hetchel" ? 5 : 4;
                        var label = $"actor{actor}-{group.First.VerbId:X2}-{mode}";
                        Save(label + "-before", session.Actors);
                        Set("_playInstantly", false);
                        if (mode == "picker") Call("PlayCdCutscene", group);
                        else
                        {
                            var record = data.ReadRoom(9).Objects.Single(o => o.Id == group.First.ObjectId);
                            var verb = data.ReadObjectVerbs(9, data.ReadRoom(9).Objects)[record.Id].Single(v => v.VerbId == group.First.VerbId);
                            Call("ActivateObjectVerb", record, verb, true);
                        }
                        (Field("_playbackTimer") as DispatcherTimer)?.Stop();
                        Check(ReferenceEquals(session, Session()), label + " retains session identity");
                        Check(session.GameStatus.AvailableNoteCount == 8 && session.GameStatus.Drafts[15].Learned == false, label + " retains chosen notes and draft state");
                        var visible = false;
                        var pictured = false;
                        for (var frame = 0; frame < 2400; frame++)
                        {
                            var shown = session.Actors.Single(a => a.Number == actor);
                            visible |= shown.Room == 9 && shown.Visible == true && shown.Look.Costume is > 0;
                            if (shown.Room == 9 && shown.Visible == true && shown.Look.Costume is > 0)
                            {
                                var room = data.ReadRoom(9);
                                var layer = ScummActorRenderer.Render([shown], 9, room.Width, room.Height, data.ReadCostume);
                                if (layer.Actors.Any(a => a.Number == actor && a.Pixels > 0))
                                {
                                    Call("ShowPlaybackFrame");
                                    var overlay = Find<Image>("RoomActorOverlay").Source as BitmapSource;
                                    if (!pictured && overlay is not null)
                                    {
                                        var pixels = new byte[overlay.PixelWidth * overlay.PixelHeight * 4];
                                        overlay.CopyPixels(pixels, overlay.PixelWidth * 4, 0);
                                        var matching = Enumerable.Range(0, layer.Pixels.Length).Count(i => layer.IsDrawn(i) && pixels[i * 4 + 3] != 0);
                                        if (matching > 0)
                                        {
                                            Save(label + "-visible", new { frame, actor = shown, rendered = layer.Actors, matching, messages = session.Messages.OnScreen });
                                            Capture(label);
                                            pictured = true;
                                        }
                                    }
                                }
                            }
                            if (!(bool)Call("PlaybackStep")!) break;
                        }
                        Call("ShowPlaybackFrame");
                        if (!pictured) Capture(label + "-missing");
                        Check(visible, label + " target actor is present and visible during playback");
                        Check(pictured, label + " target costume has nonblank pixels in the real WPF view");
                        Check(!session.InCutscene, label + " settles outside a cutscene");
                        Save(label + "-after", session.Actors);
                        File.WriteAllLines(Path.Combine(output, label + "-trace.txt"), session.SessionTrace);
                        Call("FinishPlayback");
                    }
                }
                // A short manual-test candidate: the ordinary Use entry, run from Interact.
                Call("StopScenePlayback");
                Set("_session", new LoomExplorationSession(game, indexed));
                Session().VisitRoomById(9);
                LoomCdSceneSetupPlanner.Settle(Session());
                Set("_playInstantly", true);
                Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 9));
                for (var n = 0; n < 800 && (Session().CurrentRoomId != 9 || Field("_playback") is not null); n++) await Task.Delay(25);
                var room9 = data.ReadRoom(9);
                var staff = room9.Objects.Single(o => room9.ObjectNames.TryGetValue(o.Id, out var bytes) && Encoding.Latin1.GetString(bytes).Trim().Equals("distaff", StringComparison.OrdinalIgnoreCase));
                var pickup = data.ReadObjectVerbs(9, room9.Objects)[staff.Id].Single(v => v.VerbId == 0x38);
                Call("ActivateObjectVerb", staff, pickup, true);
                for (var n = 0; n < 800 && Field("_playback") is not null; n++) await Task.Delay(25);
                Check(Session().GameStatus.StaffHeld == true, "Interact Use runs the actual distaff pickup");
                Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 13));
                for (var n = 0; n < 800 && (Session().CurrentRoomId != 13 || Field("_playback") is not null); n++) await Task.Delay(25);
                var beforeNotes = Session().GameStatus.AvailableNoteCount;
                var room13 = data.ReadRoom(13);
                Save("room13-objects", room13.Objects.Select(o => new { o.Id, o.X, o.Y, o.Width, o.Height, name = room13.ObjectNames.TryGetValue(o.Id, out var bytes) ? Encoding.Latin1.GetString(bytes) : "" }));
                var crystal = room13.Objects.Single(o => data.ReadObjectVerbs(13, room13.Objects).TryGetValue(o.Id, out var verbs) &&
                    verbs.Any(v => v.VerbId == 0x38) && data.ReadObjectVerb(13, o, 0x38, out _).Instructions.Any(i => i.Operation == "startScript" && i.Operands.StartsWith("0x36,", StringComparison.Ordinal)));
                var crystalUse = data.ReadObjectVerbs(13, room13.Objects)[crystal.Id].Single(v => v.VerbId == 0x38);
                (Field("_idleFrameTimer") as DispatcherTimer)?.Start();
                Call("ActivateObjectVerb", crystal, crystalUse, true);
                for (var n = 0; n < 800 && (Field("_playback") is not null || !Session().GameStatus.CanEdit); n++) await Task.Delay(25);
                Check(beforeNotes < 5 && Session().GameStatus.AvailableNoteCount == 5, "Interact Use raises fresh pitches to five through the crystal source scene");
                Save("crystal", new { beforeNotes, afterNotes = Session().GameStatus.AvailableNoteCount, staff.Id, crystalId = crystal.Id, room = Session().CurrentRoomId });
                File.WriteAllLines(Path.Combine(output, "crystal-trace.txt"), Session().SessionTrace);
                Capture("crystal-after");
                Call("StopScenePlayback");
                var fresh = new LoomExplorationSession(game, indexed);
                fresh.VisitRoomById(9);
                LoomCdSceneSetupPlanner.Settle(fresh);
                fresh.ActivateObjectVerb(staff.Id, 0x38);
                LoomCdSceneSetupPlanner.Settle(fresh);
                Set("_session", fresh);
                var pickerBefore = fresh.GameStatus.AvailableNoteCount;
                string title;
                if (game.Id == GameId.LoomVga)
                {
                    var trigger = LoomCutsceneIndex.Scan(data, [13]).Single(t => t.ObjectId == crystal.Id && t.VerbId == 0x38);
                    title = (string)Call("CdCutsceneTitle", trigger)!;
                    Call("PlayCdCutscene", new LoomCutsceneGroup(trigger, []));
                }
                else
                {
                    var scene = ((IEnumerable)Call("AllCutscenes")!).Cast<object>().Single(s =>
                        (int)s.GetType().GetProperty("RoomId")!.GetValue(s)! == 13 &&
                        (int)s.GetType().GetProperty("ObjectId")!.GetValue(s)! == crystal.Id &&
                        (int)s.GetType().GetProperty("VerbId")!.GetValue(s)! == 0x38);
                    title = (string)scene.GetType().GetProperty("Title")!.GetValue(scene)!;
                    Call("PlayKnownCutscene", scene);
                }
                for (var n = 0; n < 800 && (Field("_playback") is not null || !Session().GameStatus.CanEdit); n++) await Task.Delay(25);
                Check(pickerBefore == 3 && Session().GameStatus.AvailableNoteCount == 5, "the exact crystal picker choice earns five pitches from three");
                Save("crystal-picker", new { title, beforeNotes = pickerBefore, afterNotes = Session().GameStatus.AvailableNoteCount, room = Session().CurrentRoomId });
                File.WriteAllLines(Path.Combine(output, "crystal-picker-trace.txt"), Session().SessionTrace);
                Capture("crystal-picker-after");
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
                app.Shutdown(report.Any(s => s.StartsWith("FAIL ")) ? 1 : 0);
            }
        };
        app.Run(window);
    }
}

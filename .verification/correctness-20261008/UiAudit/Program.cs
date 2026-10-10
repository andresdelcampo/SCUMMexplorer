using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.GetFullPath(Path.Combine(args[1], "cache")));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, ShowActivated = false, Left = -2000 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        var report = new List<string>();
        void Check(bool ok, string message) => report.Add((ok ? "PASS " : "FAIL ") + message);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        int? Shown() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId;
        async Task Wait(Func<bool> condition, int milliseconds = 12000)
        {
            for (var n = 0; n < milliseconds / 25 && !condition(); n++) await Task.Delay(25);
            if (!condition()) throw new TimeoutException("UI audit wait expired.");
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var edition in new[] { "loom ega mt32 en", "loom vga en" })
                {
                    var cd = edition.Contains("vga"); var prefix = cd ? "VGA" : "EGA";
                    Call("OpenGameFolder", Path.GetFullPath(Path.Combine(args[0], edition)));
                    await Wait(() => Field("_game") is GameLibrary);
                    var game = (GameLibrary)Field("_game")!; var data = ScummDataSources.For(game);
                    Find<System.Windows.Controls.Primitives.ToggleButton>("SessionModeToggle").IsChecked = true;
                    async Task Visit(int room)
                    {
                        Call("StopScenePlayback");
                        Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == room));
                        await Wait(() => Shown() == room && Session().Snapshot.RoomId == room);
                        await Task.Delay(100);
                    }
                    await Visit(4);
                    for (int frame = 0; frame < 60; frame++) Session().AdvanceFrame();
                    var rd = data.ReadRoom(4);
                    var grave = rd.Objects.Single(o => rd.ObjectNames.TryGetValue(o.Id, out var name) && System.Text.Encoding.Latin1.GetString(name).Trim() == "grave");
                    // The object's own walk point is the labelled setup; dispatch goes through MainWindow and scripts 005–007.
                    Session().PlaceEgo(grave.WalkX, grave.WalkY);
                    typeof(MainWindow).GetField("_playInstantly", flags)!.SetValue(window, true);
                    var before = Session().SessionTrace.Count;
                    var candidates = (System.Collections.IEnumerable)Field("_roomNavigationCandidates")!;
                    foreach (var candidate in candidates)
                    {
                        var record = candidate.GetType().GetProperty("Object")!.GetValue(candidate)!;
                        if ((int)record.GetType().GetProperty("Id")!.GetValue(record)! != grave.Id) continue;
                        var verb = candidate.GetType().GetProperty("Verb")!.GetValue(candidate)!;
                        report.Add($"NOTE {prefix} grave navigation: verb {verb.GetType().GetProperty("VerbId")!.GetValue(verb)}, target {candidate.GetType().GetProperty("TargetRoomId")!.GetValue(candidate)}, path {candidate.GetType().GetProperty("Path")!.GetValue(candidate)}");
                    }
                    Call("HandleRoomPoint", grave.X + grave.Width / 2, grave.Y + grave.Height / 2, true);
                    report.Add($"NOTE {prefix} after double click: {Session().LastAction?.Evidence}; shown {Shown()}; status {Find<TextBlock>("StatusText").Text}");
                    Check(Session().LastAction?.IsGameInput == true, prefix + " double click is native game input");
                    Check(Session().SessionTrace.Skip(before).Any(t => t.Contains($"startObject runs object {grave.Id} verb 0x36")), prefix + " double click reaches the grave's look verb through script 007");
                    Check(Field("_lastDoorwayFallback") is null, prefix + " object interaction uses no doorway fallback");
                    Check(Shown() == 4, prefix + " look keeps the shown room");
                    await Visit(3);
                    var sourceRoom = game.Rooms.Single(r => r.ResourceId == 3);
                    var source = data.ReadRoom(3);
                    var finder = new LoomDoorwayFinder(game);
                    var exit = source.Objects.First(o => data.ReadObjectVerbs(3, source.Objects).TryGetValue(o.Id, out var verbs) &&
                        verbs.Any(v => v.VerbId == 0x38 && finder.Find(sourceRoom, data.ReadObjectVerb(3, o, v.VerbId, out _)).Any(t => t.RoomId == 4)));
                    Session().PlaceEgo(exit.WalkX, exit.WalkY);
                    var point = (X: -1, Y: -1);
                    for (var y = exit.Y; y < exit.Y + exit.Height && point.X < 0; y += 4)
                        for (var x = exit.X; x < exit.X + exit.Width && point.X < 0; x += 4)
                            if (ScummObjectHitTest.Find(source.Objects, Session().Snapshot.ObjectStates, Session().Snapshot.ObjectPositions, Session().ObjectClasses, x, y).Found == exit.Id)
                                point = (x, y);
                    if (point.X < 0) throw new InvalidDataException("No native hit point for the Room 03 exit.");
                    Call("HandleRoomPoint", point.X, point.Y, false);
                    await Wait(() => Shown() == 4);
                    Check(Session().LastAction?.IsGameInput == true && Session().Snapshot.RoomId == 4, prefix + " single click reaches Room 04 through native input");
                    Check(Field("_lastDoorwayFallback") is null, prefix + " native doorway navigation needs no fallback");
                    await Visit(cd ? 9 : 8);
                    typeof(MainWindow).GetField("_playInstantly", flags)!.SetValue(window, false);
                    object scene;
                    string play;
                    if (cd)
                    {
                        var triggers = LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value));
                        scene = LoomCutsceneIndex.Group(triggers).Single(g => g.First.Room == 9 && g.First.ObjectId == 199 && g.First.VerbId == 0x38);
                        play = "PlayCdCutscene";
                    }
                    else
                    {
                        scene = ((Array)typeof(MainWindow).GetField("KnownCutscenes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
                            .Cast<object>().Single(s => (int)s.GetType().GetProperty("Script")!.GetValue(s)! == 46);
                        play = "PlayKnownCutscene";
                    }
                    Call(play, scene);
                    await Wait(() => Session().InCutscene && Shown() == Session().Snapshot.RoomId);
                    Call("UpdatePlaybackControls");
                    Check(!Find<Button>("CutsceneMenuButton").IsEnabled && !Find<Button>("RoomCutsceneButton").IsEnabled, prefix + " both scene pickers are disabled during playback");
                    var session = Session(); var begun = session.CutscenesBegun;
                    Call(play, scene);
                    Check(ReferenceEquals(session, Session()) && Session().CutscenesBegun == begun, prefix + " a repeated scene start is refused during playback");
                    Call("PauseScenePlayback");
                    ushort Timer() => Session().Snapshot.KnownGlobalVariables["var[0x02F]"];
                    var timer = Timer();
                    await Task.Delay(400);
                    Check(Timer() == timer && (bool)Field("_playbackPaused")!, prefix + " pause holds the session clock");
                    Call("StepPausedPlayback");
                    Check(Timer() == unchecked((ushort)(timer + Session().FrameJiffies)) && (bool)Field("_playbackPaused")!, prefix + " step advances exactly one frame and stays paused");
                    Call(play, scene);
                    Check(ReferenceEquals(session, Session()), prefix + " a paused scene cannot be replaced");
                    Call("ResumeScenePlayback");
                    await Wait(() => Session().CutsceneSkippable, 30000);
                    Call("UpdatePlaybackControls");
                    Check(Find<Button>("SkipCutsceneButton").IsEnabled, prefix + " Skip is enabled at the script's override");
                    Call("SkipCutscene");
                    await Wait(() => !Session().InCutscene && Field("_playback") is null, 30000);
                    Check(Session().SessionTrace.Any(t => t.StartsWith("abortCutscene:")), prefix + " Skip follows a recorded override");
                    Check(Shown() == Session().Snapshot.RoomId, prefix + " the view follows the skipped scene's room");
                    Call("UpdatePlaybackControls");
                    Check(Find<Button>("CutsceneMenuButton").IsEnabled, prefix + " the picker is available after playback settles");
                    report.Add(prefix + " done");
                    File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                }
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally
            {
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                Environment.ExitCode = report.Any(r => r.StartsWith("FAIL")) ? 1 : 0;
                window.Close(); app.Shutdown(Environment.ExitCode);
            }
        };
        app.Run(window);
    }
}

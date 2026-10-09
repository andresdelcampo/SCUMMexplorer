using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        int? Shown() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId;
        bool? SourceBit(LoomExplorationSession session, string key)
        {
            var vars = session.Snapshot.KnownGlobalVariables;
            if (vars.TryGetValue(key, out var direct)) return direct != 0;
            if (vars.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage)) return false;
            var bit = Convert.ToInt32(key[6..^1], 16);
            return vars.TryGetValue($"var[0x{bit >> 4:X3}]", out var backing)
                ? ((backing >> (bit & 15)) & 1) != 0 : null;
        }
        var report = new List<string>();
        void Check(bool ok, string message) => report.Add((ok ? "PASS " : "FAIL ") + message);
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(args[1], name + ".png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var (edition, prefix) in new[] { ("loom ega mt32 en", "en"), ("ALTERNATIVE LOOMS/loom ega mt32 es", "es"), ("loom vga en", "vga") })
                {
                    Call("OpenGameFolder", Path.GetFullPath(Path.Combine(args[0], edition)));
                    Find<ToggleButton>("SessionModeToggle").IsChecked = true;
                    var game = (GameLibrary)Field("_game")!;
                    Call("StopScenePlayback");
                    Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 34));
                    await Task.Delay(100);
                    var session = Session();
                    for (var frame = 0; frame < 40; frame++) session.AdvanceFrame();
                    typeof(MainWindow).GetField("_playInstantly", flags)!.SetValue(window, true);
                    Call("ApplyRoomState", game.Rooms.Single(r => r.ResourceId == 34), Field("_currentRoomObjectRecords"), Field("_objects"), false);
                    Call("UpdateRoomNavigationActions", game.Rooms.Single(r => r.ResourceId == 34));
                    Call("RefreshRoomPreview");
                    Capture(prefix + "-closed-before");
                    var records = ((IEnumerable)Field("_roomNavigationCandidates")!).Cast<object>()
                        .Where(c => (int)c.GetType().GetProperty("TargetRoomId")!.GetValue(c)! == 46)
                        .Select(c => (ScummRoomObjectRecord)c.GetType().GetProperty("Object")!.GetValue(c)!)
                        .DistinctBy(r => r.Id).ToArray();
                    report.Add($"NOTE {prefix} candidates to 46: {string.Join("; ", records.Select(r => $"{r.Id} rect {r.X},{r.Y},{r.Width},{r.Height} parent {r.ParentLocalIndex}/{r.ParentRequiredState} untouchable {session.Snapshot.IsUntouchable(r.Id)}"))}");
                    var portal = records.OrderBy(r => r.Width).First();
                    Check(session.Snapshot.IsUntouchable(portal.Id), prefix + " source entry marks the closed portal untouchable");
                    Check(!((IEnumerable)Call("VisibleDoorways", session.Snapshot)!).Cast<ScummRoomObjectRecord>().Any(r => r.Id == portal.Id),
                        prefix + " closed portal has no active doorway overlay");
                    var marker = session.SessionTrace.Count;
                    var actorBefore = session.Actors.Single(a => a.Number == 3).Look.Costume;
                    var egoBefore = session.Actors.Single(a => a.Number == 1).Look.Costume;
                    var lateKey = prefix == "vga" ? "bit[0x01B]" : prefix == "es" ? "bit[0xD7B]" : "bit[0xD7A]";
                    var lateBefore = SourceBit(session, lateKey);
                    Check(lateBefore == false, prefix + " closed-portal reproduction starts before the late story");
                    report.Add($"NOTE {prefix} initial costumes: Rusty {actorBefore}, Bobbin {egoBefore}, late flag {lateBefore}");
                    Call("HandleRoomPoint", portal.X + portal.Width / 2, portal.Y + portal.Height / 2, false);
                    report.Add($"NOTE {prefix} click: shown {Shown()}, session {session.Snapshot.RoomId}, fallback {Field("_lastDoorwayFallback")}, status {Find<TextBlock>("StatusText").Text}");
                    report.AddRange(session.SessionTrace.Skip(marker).Select(t => "TRACE " + prefix + " " + t));
                    Check(Shown() == 34 && session.Snapshot.RoomId == 34, prefix + " closed portal click stays in Room 34");
                    Check(Field("_lastDoorwayFallback") is null, prefix + " closed portal click uses no direct navigation fallback");
                    Check(session.Actors.Single(a => a.Number == 3).Look.Costume == actorBefore, prefix + " closed portal preserves Rusty's early costume");
                    Check(session.Actors.Single(a => a.Number == 1).Look.Costume == egoBefore, prefix + " closed portal preserves Bobbin's costume");
                    Check(SourceBit(session, lateKey) == lateBefore, prefix + " closed portal does not set Room 46's late-story flag");
                    Call("HandleRoomPoint", portal.X + portal.Width / 2, portal.Y + portal.Height / 2, true);
                    Check(Shown() == 34 && session.Snapshot.RoomId == 34 && Field("_lastDoorwayFallback") is null,
                        prefix + " double click also respects the closed portal");
                    Capture(prefix + "-closed-after");
                    if (prefix == "en")
                    {
                        var scanner = new LoomSceneStateScanner(game);
                        foreach (var bit in new[] { "bit[0xD9C]", "bit[0xD7A]" })
                            foreach (var writer in scanner.FindConstantWriters(bit, 1))
                                report.Add($"SOURCE {bit} writer {writer.Where}, room {writer.Room}, object {writer.ObjectId}, verb {writer.VerbId}");
                        // Prepared preview: use the application's existing Firewood setup, whose
                        // actual scenes set the ghost flag, followed by disclosed catalog visits.
                        // No private state, costume or story-bit assignment is used.
                        var firewood = (LoomReplayStep[])typeof(MainWindow).GetProperty("FirewoodSteps", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                        void Replay(string label, IReadOnlyList<LoomReplayStep> steps)
                        {
                            var sequence = new LoomReplaySequence(session, steps);
                            var frames = 0;
                            while (sequence.Continue() && frames++ < 16000) { session.AdvanceFrame(); sequence.FrameAdvanced(); }
                            Check(sequence.Complete && sequence.Failure is null, label + " source sequence completes");
                            report.Add($"NOTE {label}: {frames} frames, {sequence.Failure}");
                            if (!sequence.Complete) throw new InvalidDataException(label + ": " + sequence.Failure);
                        }
                        Replay("en Firewood/ghost prerequisites", [..firewood,
                            new("verb", 611, 0x36), new("scene", EndBoundary: "endCutscene at Script 87 +0x0984", ExpectedRoom: 38),
                            new("visit", 35), new("frames", Frames: 8), new("visit", 34), new("frames", Frames: 40),
                            new("visit", 46), new("frames", Frames: 40), new("verb", 685, 0x36),
                            new("scene", EndBoundary: "endCutscene at Script 97 +0x0042", ExpectedRoom: 34)]);
                        Check(session.StoryBit("bit[0xD9C]") == true, "en actual Script 87 establishes ghost Rusty");
                        Check(session.Actors.Single(a => a.Number == 3).Look.Costume == 120, "en prepared Room 46 return selects Rusty's source ghost costume");
                        // The enabled portal's use callback refuses traversal while Rusty is a ghost.
                        // Even a direct retry must honor that answer rather than open a static target.
                        Call("FollowDoorway", portal);
                        Check(session.Snapshot.RoomId == 34 && Shown() == 34, "en conditional portal callback cannot force its static Room 46 destination");
                        Check(session.StoryBit("bit[0xD9C]") == true && session.Actors.Single(a => a.Number == 3).Look.Costume == 120,
                            "en refused portal traversal preserves the ghost state and costume");
                        Replay("en ghost conversation", [new("verb", 560, 0x37), new("scene", EndBoundary: "endCutscene at Script 98 +0x0605", ExpectedRoom: 34)]);
                        Check(session.Actors.Single(a => a.Number == 3).Look.Costume == 120, "en Rusty retains the ghost costume after the close-up and eastward walk");
                        Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 34));
                        await Task.Delay(100);
                        Call("ApplyRoomState", game.Rooms.Single(r => r.ResourceId == 34), Field("_currentRoomObjectRecords"), Field("_objects"), false);
                        Call("UpdateRoomNavigationActions", game.Rooms.Single(r => r.ResourceId == 34));
                        Call("RefreshRoomPreview");
                        Capture("en-valid-ghost-after");
                        Replay("en Rusty restoration", [new("verb", 587, 0x4D), new("scene", EndBoundary: "endCutscene at Room 34 local script 209 +0x03A7", ExpectedRoom: 34), new("frames", Frames: 40)]);
                        Check(!session.Snapshot.IsUntouchable(portal.Id), "en source late state enables the portal");
                        Call("ApplyRoomState", game.Rooms.Single(r => r.ResourceId == 34), Field("_currentRoomObjectRecords"), Field("_objects"), false);
                        Call("UpdateRoomNavigationActions", game.Rooms.Single(r => r.ResourceId == 34));
                        Call("RefreshRoomPreview");
                        var openStart = session.SessionTrace.Count;
                        Call("HandleRoomPoint", portal.X + portal.Width / 2, portal.Y + portal.Height / 2, false);
                        for (var i = 0; i < 400 && Shown() != 46; i++) await Task.Delay(25);
                        Check(Shown() == 46 && session.Snapshot.RoomId == 46, "en enabled portal still reaches Room 46");
                        Check(session.SessionTrace.Skip(openStart).Any(t => t.Contains("startObject runs object 560 verb 0x38")),
                            "en enabled portal enters through native input's use sentence");
                        Check(Field("_lastDoorwayFallback") is null, "en enabled portal needs no navigation fallback");
                        Capture("en-enabled-portal-room46");
                        Replay("en enabled-portal return", [new("verb", 685, 0x36),
                            new("scene", EndBoundary: "endCutscene at Script 97 +0x0042", ExpectedRoom: 34), new("frames", Frames: 40)]);
                        Check(session.Snapshot.RoomId == 34 && session.Actors.Single(a => a.Number == 3).Room == 0,
                            "en return after restoration does not resurrect sleeping Rusty");
                    }
                    File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                }
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally
            {
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                app.Shutdown(report.Any(r => r.StartsWith("FAIL ")) ? 1 : 0);
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// LOOM CD in the real window: the room scene, the costume cards and one costume's poses. Arguments: <disc folder or .cue> <output folder>. No physical input.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        Directory.CreateDirectory(output);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Shot(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                // CD_USECACHE=1 opens as "reopen the last game" does (saved room lists and thumbnails may be used); the default is the Open button.
                var useCache = Environment.GetEnvironmentVariable("CD_USECACHE") == "1";
                var cacheFlag = typeof(MainWindow).GetField("_roomCardsMayUseCache", Private)!;
                if (useCache) cacheFlag.SetValue(window, true);
                try { Call("OpenGameFolder", args[0]); } finally { cacheFlag.SetValue(window, false); }
                var game = (GameLibrary)Field("_game")!;
                var report = new List<string> { $"{game.Title} | {game.Id} | rooms {game.Rooms.Count()} ({string.Join(",", game.Rooms.Select(r => r.ResourceId))}) | costumes {game.Resources.Count(r => r.Kind == ResourceKind.Costume)}" };
                // CD_INTRO=1 plays "The intro" from the Play a cutscene list on the playback clock and shoots it every CD_INTRO_STEP ms (default 4000) for CD_INTRO_MS (default 105000).
                // CD_CUT=room,object,verb (verb 0 = the room's entry) plays that derived cutscene instead; CD_ESCAPE_MS presses Escape (the window's own key handler) that many ms after the start.
                // CD_ROOMBTN=1,4,35 reports the per-room "Play Cutscene" button (visibility, enabled, content) in a live session after selecting each room; shoots room-btn-<n>.png.
                // CD_LOOK=2,4,69 shoots each room in the live session at once (no waiting for the background runs); CD_LOOK_ADV=1 turns Advanced on and opens Options first.
                if (Environment.GetEnvironmentVariable("CD_LOOK") is { } lookList)
                {
                    ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("SessionModeToggle")).IsChecked = true;
                    if (Environment.GetEnvironmentVariable("CD_LOOK_ADV") == "1")
                    {
                        ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("AdvancedModeToggle")).IsChecked = true;
                        Call("AdvancedModeToggle_Click", window.FindName("AdvancedModeToggle")!, new RoutedEventArgs());
                        ((Expander)window.FindName("RoomOptionsExpander")).IsExpanded = true;
                    }
                    await Task.Delay(1500);
                    foreach (var number in lookList.Split(',').Select(int.Parse))
                    {
                        var room = game.Rooms.First(r => r.ResourceId == number);
                        Call("SelectResourceInTree", room);
                        for (var n = 0; n < 100 && Field("_currentRoomResource") != room; n++) await Task.Delay(100);
                        await Task.Delay(2500);
                        // CD_LOOK_STAFF=1 gives Bobbin the staff through Game Status's own call (as the window's edit does), then shoots again.
                        if (Environment.GetEnvironmentVariable("CD_LOOK_STAFF") == "1" && Field("_session") is SCUMMexplorer.Core.Formats.LoomExplorationSession status)
                        {
                            Shot($"look-{number:00}-before.png");
                            var gs = status.GameStatus;
                            Console.WriteLine($"room {number}: status supported {gs.Supported}, can edit {gs.CanEdit}, staff {gs.StaffHeld}, {gs.Evidence[..Math.Min(120, gs.Evidence.Length)]}");
                            try { status.UpdateStaffPossession(true); } catch (Exception ex) { Console.WriteLine("edit failed: " + ex.Message); }
                            Call("RefreshRoomPreview"); Call("UpdateActorLayer"); Call("UpdateVerbArea");
                            await Task.Delay(1500);
                            var area = status.VerbArea();
                            Console.WriteLine($"verb area items {area.Items.Count}: " + string.Join("; ", area.Items.Select(i => $"verb {i.VerbId} mode {i.Mode} at ({i.X},{i.Y}) {i.Width}x{i.Height} obj {i.ImageObject} room {i.ImageRoom}")) + " | notes: " + string.Join("; ", area.Notes));
                            foreach (var v in status.Verbs) Console.WriteLine($"  slot {v.Slot} verb {v.VerbId} mode {v.Mode} at ({v.X},{v.Y}) image {v.ImageObject}/{v.ImageRoom} name {v.Name}");
                        }
                        Shot($"look-{number:00}.png");
                    }
                    window.Close();
                    return;
                }
                if (Environment.GetEnvironmentVariable("CD_ROOMBTN") is { } roomList)
                {
                    ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("SessionModeToggle")).IsChecked = true;
                    await Task.Delay(1500);
                    // CD_FIRSTLOOK=<room> reports that room's button right away, before the background lists are ready (first-launch placeholder / instant reopen).
                    if (Environment.GetEnvironmentVariable("CD_FIRSTLOOK") is { } firstLook)
                    {
                        var first = game.Rooms.First(r => r.ResourceId == int.Parse(firstLook));
                        Call("SelectResourceInTree", first);
                        for (var n = 0; n < 100 && !ReferenceEquals(Field("_currentRoomResource"), first); n++) await Task.Delay(100);
                        await Task.Delay(500);
                        var early = (Button)window.FindName("RoomCutsceneButton");
                        report.Add($"FIRST LOOK room {firstLook}: button {early.Visibility} enabled {early.IsEnabled} '{early.Content}' tooltip '{early.ToolTip}' | lists done {Field("_cdRoomListsDone")}");
                        Shot($"first-look-{firstLook}.png");
                    }
                    // The rooms each scene plays out in are derived in the background; wait for the last one (up to 8 minutes).
                    {
                        var groups = ((System.Collections.IEnumerable)Call("CdCutsceneGroups")!).Cast<SCUMMexplorer.Core.Formats.LoomCdCutsceneGroup>().ToList();
                        var lazy = Field("_cdSetupPlanner")!; var planner = (SCUMMexplorer.Core.Formats.LoomCdSceneSetupPlanner)lazy.GetType().GetProperty("Value")!.GetValue(lazy)!;
                        var wait = System.Diagnostics.Stopwatch.StartNew();
                        while (wait.Elapsed < TimeSpan.FromMinutes(8) && !(planner.IntroRoomsIfKnown is not null && groups.All(g => planner.TryGetVisitedRooms(g.First, out _)))) await Task.Delay(1000);
                        report.Add($"visited rooms ready after {wait.Elapsed.TotalSeconds:0} s");
                    }
                    var button = (Button)window.FindName("RoomCutsceneButton");
                    foreach (var number in roomList.Split(',').Select(int.Parse))
                    {
                        var room = game.Rooms.First(r => r.ResourceId == number);
                        Call("SelectResourceInTree", room);
                        for (var n = 0; n < 100 && Field("_currentRoomResource") != room; n++) await Task.Delay(100);
                        await Task.Delay(1500);
                        var items = (System.Collections.ICollection)Call("RoomCutsceneItems", number)!;
                        report.Add($"room {number}: items {items.Count}, button {button.Visibility} enabled {button.IsEnabled} '{button.Content}' | bar {((UIElement)window.FindName("PlaybackBar")).Visibility} | session {(Field("_session") is null ? "none" : "live")}");
                        Shot($"room-btn-{number:00}.png");
                    }
                    File.WriteAllLines(Path.Combine(output, "report.txt"), report);
                    window.Close();
                    return;
                }
                var cut = Environment.GetEnvironmentVariable("CD_CUT");
                if (Environment.GetEnvironmentVariable("CD_INTRO") == "1" || cut is not null)
                {
                    var toggle = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("SessionModeToggle");
                    toggle.IsChecked = true;
                    await Task.Delay(1500);
                    var step = int.Parse(Environment.GetEnvironmentVariable("CD_INTRO_STEP") ?? "4000");
                    var total = int.Parse(Environment.GetEnvironmentVariable("CD_INTRO_MS") ?? "105000");
                    if (cut is null) Call("PlayCdIntro");
                    else
                    {
                        var want = cut.Split(',').Select(int.Parse).ToArray();
                        var group = ((System.Collections.IEnumerable)Call("CdCutsceneGroups")!).Cast<SCUMMexplorer.Core.Formats.LoomCdCutsceneGroup>()
                            .First(g => g.First.Room == want[0] && g.First.ObjectId == want[1] && (g.First.VerbId == want[2] || g.First.IsRoomEntry && want[2] == 0));
                        report.Add($"cutscene room {group.First.Room} object {group.First.ObjectId} {group.First.ObjectName} {group.First.VerbLabel}, scene {group.IsScene}");
                        { var lazy = Field("_cdSetupPlanner")!; var planner = (SCUMMexplorer.Core.Formats.LoomCdSceneSetupPlanner)lazy.GetType().GetProperty("Value")!.GetValue(lazy)!; var setup = planner.Derive(group.First);
                          report.Add($"setup: plays {setup.Plays}, before {setup.Before.Count}, after [{string.Join(",", setup.After.Select(s => s.Kind + s.Box))}]"); }
                        Call("PlayCdCutscene", group);
                    }
                    var escapeMs = int.Parse(Environment.GetEnvironmentVariable("CD_ESCAPE_MS") ?? "-1");
                    var escaped = false;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    for (var t = 0; t < total; t += step)
                    {
                        if (escapeMs >= 0 && !escaped && t + step > escapeMs)
                        {
                            await Task.Delay(Math.Max(0, escapeMs - t));
                            var source = PresentationSource.FromVisual(window);
                            var key = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                            window.RaiseEvent(key);
                            escaped = true;
                            report.Add($"+{clock.ElapsedMilliseconds} ms: Escape raised, handled {key.Handled}: {((TextBlock)window.FindName("StatusText")).Text}");
                            await Task.Delay(Math.Max(0, step - (escapeMs - t)));
                        }
                        else await Task.Delay(step);
                        Shot($"intro-{t + step:000000}.png");
                        var session = Field("_session")!;
                        var snap = session.GetType().GetProperty("Snapshot")!.GetValue(session)!;
                        var msgs = session.GetType().GetProperty("Messages")!.GetValue(session)!;
                        var on = string.Join(" / ", ((System.Collections.IEnumerable)msgs.GetType().GetProperty("OnScreen")!.GetValue(msgs)!).Cast<object>().Select(m => m.GetType().GetProperty("Text")!.GetValue(m)!.ToString()!.Replace("\n", "|")));
                        report.Add($"+{clock.ElapsedMilliseconds} ms: room {snap.GetType().GetProperty("RoomId")!.GetValue(snap)} boot {session.GetType().GetProperty("BootRunning")!.GetValue(session)} timer {msgs.GetType().GetProperty("CdMusicTimer")!.GetValue(msgs)} | {on} | {((TextBlock)window.FindName("StatusText")).Text}");
                    }
                    { var voice = Field("_cdVoice"); foreach (var line in (IEnumerable<string>)voice!.GetType().GetProperty("Log")!.GetValue(voice)!) report.Add("voice " + line); }
                    File.WriteAllLines(Path.Combine(output, "report.txt"), report);
                    window.Close();
                    return;
                }
                // First-visit scenes (the entry script's objects over the background), a few rooms; cards need a moment to render too.
                foreach (var number in new[] { 1, 4, 10, 13, 16, 31, 35, 40, 53, 86 })
                {
                    var room = game.Rooms.First(r => r.ResourceId == number);
                    Call("SelectResourceInTree", room);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != room; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    Shot($"room-{number:00}.png");
                    report.Add($"room {number}: {((IReadOnlyCollection<object>)((System.Collections.IEnumerable)Field("_objects")!).Cast<object>().ToList()).Count} object images, view offset {((ScrollViewer)window.FindName("RoomViewport")).HorizontalOffset:0} of {((ScrollViewer)window.FindName("RoomViewport")).ScrollableWidth:0}, pending camera {Field("_pendingCameraX")}");
                }
                // The game's click in the live CD session, several rooms: the first object that has a use verb (0x38) or a walk-to verb (0x37).
                var cd = (SCUMMexplorer.Core.Formats.IScummDataSource)Field("_cdData")!;
                if (Environment.GetEnvironmentVariable("CD_DUMP2") == "1")
                    foreach (var id in cd.GlobalScriptIds()) { var sc = cd.GlobalScript(id); if (sc is not null) foreach (var ins in sc.Instructions) if (ins.Operation is "drawObject" or "setState" or "startObject") report.Add($"   global {id} +{ins.Offset:X4} {ins.Operation} {ins.Operands}"); }
                // CD_STATUS=1 opens the Game Status window on room 35 and saves a picture of it.
                if (Environment.GetEnvironmentVariable("CD_STATUS") == "1")
                {
                    var r35 = game.Rooms.First(r => r.ResourceId == 35);
                    Call("SelectResourceInTree", r35);
                    await Task.Delay(1500);
                    // CD_STATUS_HELD=1 holds the staff first (Game Status's own edits: staff, 5 notes, draft 0 and 1 learned with notes) so the window shows the artwork of a held staff.
                    if (Environment.GetEnvironmentVariable("CD_STATUS_HELD") == "1")
                    {
                        var s0 = Field("_session")!;
                        void S(string name, params object[] a) => s0.GetType().GetMethod(name)!.Invoke(s0, a);
                        S("UpdateStaffPossession", true); S("UpdateAvailableNotes", 5);
                        S("UpdateDraftSequence", 0, new[] { 1, 2, 3, 4 }); S("UpdateDraftLearned", 0, true);
                        S("UpdateDraftSequence", 1, new[] { 2, 3, 4, 5 }); S("UpdateDraftLearned", 1, true);
                        for (var f = 0; f < 10; f++) S("AdvanceFrame");
                        report.Add("staff held for the status window");
                    }
                    var status = (Window)Call("BuildGameStatusWindow")!;
                    status.Show();
                    await Task.Delay(1500);
                    var rtb = new RenderTargetBitmap((int)status.ActualWidth, (int)status.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(status);
                    var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                    using (var fs = File.Create(Path.Combine(output, "status.png"))) enc.Save(fs);
                    report.Add("status window shown");
                    { var sess = Field("_session")!; var gs = sess.GetType().GetProperty("GameStatus")!.GetValue(sess)!; foreach (var d in (System.Collections.IEnumerable)gs.GetType().GetProperty("Drafts")!.GetValue(gs)!) report.Add("   draft " + d); }
                    status.Close();
                }
                { var cues = ((System.Collections.IEnumerable)Field("_cdCues")!).Cast<object>().ToList(); report.Add($"CD audio cues: {cues.Count}"); var longest = cues.OrderByDescending(c => (int)c.GetType().GetProperty("Cue")!.GetValue(c)!.GetType().GetProperty("Length")!.GetValue(c.GetType().GetProperty("Cue")!.GetValue(c))!).Take(4); foreach (var c in longest) report.Add("   long cue " + c); }
                // CD_SOUND=1,9,16 selects those sounds in the tree and reports what the audio panel says (AdLib by default).
                if (Environment.GetEnvironmentVariable("CD_SOUND") is { } soundIds)
                    foreach (var id in soundIds.Split(',').Select(int.Parse))
                    {
                        var sound = game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Sound && r.ResourceId == id)
                            ?? ((TreeView)window.FindName("ResourceTree")).Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).Select(i => i.Tag).OfType<GameResource>().FirstOrDefault(r => r.ResourceId == id); // the CD audio list lives in the tree only
                        if (sound is null) { report.Add($"sound {id}: not listed"); continue; }
                        Call("SelectResourceInTree", sound);
                        for (var n = 0; n < 100 && ((TextBlock)window.FindName("AudioTime")).Text.StartsWith("Preparing"); n++) await Task.Delay(200);
                        await Task.Delay(300);
                        report.Add($"sound {id}: {((TextBlock)window.FindName("AudioDescription")).Text} | {((TextBlock)window.FindName("AudioTime")).Text} | {((TextBlock)window.FindName("StatusText")).Text}");
                    }
                // CD_PRINT=17,18 lists every instruction of those global scripts.
                if (Environment.GetEnvironmentVariable("CD_PRINT") is { } printIds)
                    foreach (var id in printIds.Split(',').Select(int.Parse)) { report.Add($"== script {id}"); if (cd.GlobalScript(id) is { } sc) foreach (var ins in sc.Instructions) report.Add($"   {id:000} +{ins.Offset:X4} {ins.Operation} {ins.Operands}"); }
                if (Environment.GetEnvironmentVariable("CD_DUMP3") == "1")
                {
                    var all = new List<(string, SCUMMexplorer.Core.Formats.LoomScriptDisassembly?)>();
                    foreach (var id in cd.GlobalScriptIds()) all.Add(($"global {id}", cd.GlobalScript(id)));
                    foreach (var r in game.Rooms.Select(r => r.ResourceId!.Value)) { all.Add(($"room {r} entry", cd.RoomEntryScript(r))); all.Add(($"room {r} exit", cd.RoomExitScript(r)));
                        { var rd = cd.ReadRoom(r); foreach (var (oid, list) in cd.ReadObjectVerbs(r, rd.Objects)) { var rec = rd.Objects.First(o => o.Id == oid); foreach (var v in list) { try { all.Add(($"room {r} object {oid} verb 0x{v.VerbId:X2}", cd.ReadObjectVerb(r, rec, v.VerbId, out _))); } catch (Exception) { } } } } foreach (var id in cd.RoomLocalScriptIds(r)) all.Add(($"room {r} local {id}", cd.RoomLocalScript(r, id))); }
                    // CD_OPS=verbOps lists every use of that operation instead of the charset switches.
                    var op = Environment.GetEnvironmentVariable("CD_OPS");
                    var grep = Environment.GetEnvironmentVariable("CD_GREP"); // CD_GREP=text lists every instruction whose text contains it
                    if (Environment.GetEnvironmentVariable("CD_ISSUES") == "1") foreach (var (name, sc) in all) if (sc?.Issues is { Count: > 0 } issues) report.Add($"   ISSUES {name}: {sc.DecodedBytes}/{sc.CodeLength} bytes, {string.Join(" | ", issues.Take(3).Select(i => i.ToString()))}");
                    foreach (var (name, sc) in all) if (sc is not null) foreach (var ins in sc.Instructions) if (grep is not null ? $"{ins.Operation} {ins.Operands}".Contains(grep) : op is null ? ins.Operation == "cursorCommand" && ins.Operands.StartsWith("charset") : ins.Operation == op) report.Add($"   {name} +{ins.Offset:X4} {ins.Operation} {ins.Operands}");
                }
                var rooms = args.Length > 2 ? args[2].Split(',').Select(int.Parse).ToArray() : new[] { 4, 10, 16, 35 };
                foreach (var number in rooms)
                {
                    var room = game.Rooms.FirstOrDefault(r => r.ResourceId == number);
                    if (room is null) { report.Add($"room {number}: not on the disc"); continue; }
                    Call("SelectResourceInTree", room);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != room; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    var data = cd.ReadRoom(number);
                    if (Environment.GetEnvironmentVariable("CD_DUMP") == "1")
                    {
                        var scripts = new List<(string, SCUMMexplorer.Core.Formats.LoomScriptDisassembly?)> { ("entry", cd.RoomEntryScript(number)) };
                        foreach (var id in cd.RoomLocalScriptIds(number)) scripts.Add(($"local {id}", cd.RoomLocalScript(number, id)));
                        foreach (var (name, sc) in scripts) if (sc is not null) foreach (var ins in sc.Instructions) if (ins.Operation == "drawObject") report.Add($"   {name} +{ins.Offset:X4} {ins.Operation} {ins.Operands}");
                    }
                    var verbs = cd.ReadObjectVerbs(number, data.Objects);
                    var target = data.Objects.FirstOrDefault(o => verbs.TryGetValue(o.Id, out var list) && list.Any(v => v.VerbId == (Environment.GetEnvironmentVariable("CD_DOUBLE") == "1" ? 0x36 : 0x38)) && o.Width > 0);
                    if (target is null) { report.Add($"click room {number}: no clickable object"); continue; }
                    var x = target.X + target.Width / 2; var y = target.Y + Math.Max(1, target.Height) / 2;
                    Call("HandleRoomPoint", x, y, Environment.GetEnvironmentVariable("CD_DOUBLE") == "1");
                    foreach (var ms in new[] { 400, 800, 1300, 2500, 5000 })
                    {
                        await Task.Delay(ms == 400 ? 400 : ms == 800 ? 400 : ms == 1300 ? 500 : ms == 2500 ? 1200 : 2500);
                        Shot($"click-{number:00}-{ms}.png");
                        var session = Field("_session")!; var actors = (System.Collections.IEnumerable)session.GetType().GetProperty("Actors")!.GetValue(session)!;
                        foreach (var a in actors) { var t = a.GetType(); var room2 = t.GetProperty("Room")!.GetValue(a); if (room2 is int r2 && r2 == number) { var look = t.GetProperty("Look")!.GetValue(a)!; report.Add($"   actor {t.GetProperty("Number")!.GetValue(a)} x {t.GetProperty("X")!.GetValue(a)} y {t.GetProperty("Y")!.GetValue(a)} visible {t.GetProperty("Visible")!.GetValue(a)} look {look}"); } }
                        { var la = session.GetType().GetProperty("LastAction")!.GetValue(session); if (la is not null) { foreach (var u in (IEnumerable<string>)la.GetType().GetProperty("Uncertainties")!.GetValue(la)!) report.Add("   U " + u); } report.Add("   evidence " + la?.GetType().GetProperty("Evidence")!.GetValue(la)); }
                        { var ov = (Image)window.FindName("RoomMessageOverlay"); report.Add($"   overlay {ov.Visibility} source {(ov.Source is null ? "none" : ov.Source.Width + "x" + ov.Source.Height)} actual {ov.ActualWidth}x{ov.ActualHeight}"); }
                        { var msgs = session.GetType().GetProperty("Messages")!.GetValue(session)!; var on = (System.Collections.IEnumerable)msgs.GetType().GetProperty("OnScreen")!.GetValue(msgs)!; foreach (var m in on) report.Add("   message " + m); }
                        if (ms == 5000 && Environment.GetEnvironmentVariable("CD_TRACE") == "1") foreach (var line in (IEnumerable<string>)session.GetType().GetProperty("SessionTrace")!.GetValue(session)!) if (line.Contains("object 11") || line.Contains("object 12")) report.Add("   T " + line);
                        if (ms == 5000) { var snap = session.GetType().GetProperty("Snapshot")!.GetValue(session)!; var states = (System.Collections.IDictionary)snap.GetType().GetProperty("ObjectStates")!.GetValue(snap)!; var imgs = cd.ReadObjectImages(number); foreach (var o in data.Objects) report.Add($"   obj {o.Id} at {o.X},{o.Y} {o.Width}x{o.Height} state {(states.Contains(o.Id) ? states[o.Id] : "-")} image {imgs.Any(i => i.Id == o.Id)}"); }
                        if (ms == 5000) { var sess = Field("_session")!; var msgs2 = sess.GetType().GetProperty("Messages")!.GetValue(sess)!; foreach (var cue in (System.Collections.IEnumerable)msgs2.GetType().GetProperty("VoiceEvents")!.GetValue(msgs2)!) report.Add("   cue " + cue); }
                        { var voice = Field("_cdVoice"); foreach (var line in (IEnumerable<string>)voice!.GetType().GetProperty("Log")!.GetValue(voice)!) report.Add("   voice " + line); }
                        { var ss = Field("_sceneSound")!; foreach (var e in (System.Collections.IEnumerable)ss.GetType().GetProperty("Log")!.GetValue(ss)!) report.Add("   sound " + e); }
                        report.Add($"click room {number} object {target.Id} at {x},{y} +{ms} ms: {((TextBlock)window.FindName("StatusText")).Text}");
                    }
                }
                var costumesTab = (RadioButton)window.FindName("CostumesTab");
                costumesTab.IsChecked = true;
                Call("NormalCatalogTab_Click", costumesTab, new RoutedEventArgs());
                await Task.Delay(4000);
                Shot("costume-cards.png");
                var cards = (WrapPanel)window.FindName("CostumeCardList");
                report.Add($"costume cards: {cards.Children.Count}");
                var costume = game.Resources.First(r => r.Kind == ResourceKind.Costume && r.ResourceId == (game.Id == GameId.LoomVga ? 5 : 3));
                Call("SelectResourceInTree", costume);
                await Task.Delay(1500);
                Shot("costume-5.png");
                var panel = (FrameworkElement)window.FindName("CostumePanel"); var poses = (Panel)window.FindName("CostumePoseGallery");
                report.Add($"costume panel {panel.Visibility}, poses {poses.Children.Count}, message {((TextBlock)window.FindName("CostumeMessage")).Text}");
                File.WriteAllLines(Path.Combine(output, "report.txt"), report);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
            window.Close();
        };
        app.Run(window);
    }
}

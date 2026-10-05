using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                var report = new List<string> { $"{game.Title} | {game.Id} | rooms {game.Rooms.Count()} | costumes {game.Resources.Count(r => r.Kind == ResourceKind.Costume)}" };
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
                var rooms = args.Length > 2 ? args[2].Split(',').Select(int.Parse).ToArray() : new[] { 4, 10, 16, 35 };
                foreach (var number in rooms)
                {
                    var room = game.Rooms.First(r => r.ResourceId == number);
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
                    foreach (var ms in new[] { 800, 2500, 5000 })
                    {
                        await Task.Delay(ms == 800 ? 800 : ms == 2500 ? 1700 : 2500);
                        Shot($"click-{number:00}-{ms}.png");
                        var session = Field("_session")!; var actors = (System.Collections.IEnumerable)session.GetType().GetProperty("Actors")!.GetValue(session)!;
                        foreach (var a in actors) { var t = a.GetType(); var room2 = t.GetProperty("Room")!.GetValue(a); if (room2 is int r2 && r2 == number) { var look = t.GetProperty("Look")!.GetValue(a)!; report.Add($"   actor {t.GetProperty("Number")!.GetValue(a)} x {t.GetProperty("X")!.GetValue(a)} y {t.GetProperty("Y")!.GetValue(a)} visible {t.GetProperty("Visible")!.GetValue(a)} look {look}"); } }
                        { var la = session.GetType().GetProperty("LastAction")!.GetValue(session); if (la is not null) { foreach (var u in (IEnumerable<string>)la.GetType().GetProperty("Uncertainties")!.GetValue(la)!) report.Add("   U " + u); } report.Add("   evidence " + la?.GetType().GetProperty("Evidence")!.GetValue(la)); }
                        { var msgs = session.GetType().GetProperty("Messages")!.GetValue(session)!; var on = (System.Collections.IEnumerable)msgs.GetType().GetProperty("OnScreen")!.GetValue(msgs)!; foreach (var m in on) report.Add("   message " + m); }
                        if (ms == 5000 && Environment.GetEnvironmentVariable("CD_TRACE") == "1") foreach (var line in (IEnumerable<string>)session.GetType().GetProperty("SessionTrace")!.GetValue(session)!) if (line.Contains("object 11") || line.Contains("object 12")) report.Add("   T " + line);
                        if (ms == 5000) { var snap = session.GetType().GetProperty("Snapshot")!.GetValue(session)!; var states = (System.Collections.IDictionary)snap.GetType().GetProperty("ObjectStates")!.GetValue(snap)!; var imgs = cd.ReadObjectImages(number); foreach (var o in data.Objects) report.Add($"   obj {o.Id} at {o.X},{o.Y} {o.Width}x{o.Height} state {(states.Contains(o.Id) ? states[o.Id] : "-")} image {imgs.Any(i => i.Id == o.Id)}"); }
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

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: ControlsWindowCheck <game folder> <output folder> [rooms: comma list | auto] [advanced] [scene]
// Static-preview controls (20 October 2026). Opens the real MainWindow with a private profile (SCUMM_EXPLORER_PROFILE_ROOT) and room cache
// (SCUMM_ROOM_CACHE_DIR) in the output folder, selects rooms in the tree as a user does, waits for the room to open, and records which room
// controls are visible and enabled: the playback bar (Pause/Play, Skip line, Skip), Play Cutscene (room button and header picker), Interact,
// Sounds and Play Game Intro, whether the room is a static preview, whether a play clock runs, the frames played, whether the picture changed
// over 3 s, and whether the story moved (room left, cutscene, message). "auto" walks the rooms in index order until it has found one live and
// one static room (at most 30). "scene" then plays the static room's first listed cutscene and records that it played. Writes controls.txt.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string> { $"game {args[0]}" };
        var roomsArg = args.Length > 2 ? args[2] : "auto";
        var advanced = args.Contains("advanced");
        var scene = args.Contains("scene");
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        T Named<T>(string name) where T : class => (T)window.FindName(name);
        string Control(string name)
        {
            var element = Named<FrameworkElement>(name);
            // Visible on screen: the element and every parent up to the window.
            for (DependencyObject? item = element; item is not null; item = System.Windows.Media.VisualTreeHelper.GetParent(item))
                if (item is UIElement { Visibility: not Visibility.Visible }) return "-";
            return element.IsEnabled ? "on" : "off";
        }
        int Hash(ScummPlayFrame? frame) => frame is null ? 0 : frame.Pixels.Aggregate(17, (h, b) => unchecked(h * 31 + (int)b));
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                if (advanced) ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("AdvancedModeToggle")).IsChecked = true;
                var open = Stopwatch.StartNew();
                Call("OpenGameFolder", args[0]);
                report.Add($"OpenGameFolder returned after {open.Elapsed.TotalSeconds:0.0} s; mode {(advanced ? "Advanced" : "Normal")}");
                var tree = Named<TreeView>("ResourceTree");
                var rooms = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .Where(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room")
                    .Select(i => (Id: (int)i.Tag!.GetType().GetProperty("ResourceId")!.GetValue(i.Tag)!, Tag: i.Tag!)).ToList();
                var wanted = roomsArg == "auto" ? rooms.Take(30).ToList()
                    : roomsArg.Split(',').Select(text => rooms.First(r => r.Id == int.Parse(text, System.Globalization.CultureInfo.InvariantCulture))).ToList();
                report.Add("room | kind | clock | frames 3 s | picture changed | story moved | bar | Pause | Skip line | Skip | Play Cutscene (room) | Play Cutscene (header) | Interact | Sounds | Play Game Intro");
                bool? sawLive = null, sawStatic = null;
                int? staticRoom = null;
                foreach (var (id, tag) in wanted)
                {
                    Call("SelectResourceInTree", tag);
                    await Task.Delay(300);
                    if (Field("_roomOpenTask") is Task opening) { try { await opening; } catch { } }
                    // The play clock or the preparation may still be starting; wait for one of them.
                    for (var n = 0; n < 150 && Field("_playTimer") is null && !(bool)Field("_catalogPreviewOnly")!; n++) await Task.Delay(100);
                    await Task.Delay(1500);
                    var isStatic = (bool)Field("_catalogPreviewOnly")!;
                    var play = Field("_play") as ScummSession;
                    var frames0 = (int)(Field("_playFrame") ?? 0);
                    var hash0 = Hash(Field("_playShown") as ScummPlayFrame);
                    var room0 = play?.CurrentRoomId;
                    var moved = "";
                    for (var n = 0; n < 30; n++)
                    {
                        await Task.Delay(100);
                        if (Field("_play") is ScummSession p)
                        {
                            if (p.CurrentRoomId != room0 && !moved.Contains("room")) moved += $"room {room0}->{p.CurrentRoomId} ";
                            if (p.InCutscene && !moved.Contains("cutscene")) moved += "cutscene ";
                            if (p.Messages.HaveMsg != 0 && !moved.Contains("message")) moved += "message ";
                        }
                    }
                    var frames = (int)(Field("_playFrame") ?? 0) - frames0;
                    var changed = Hash(Field("_playShown") as ScummPlayFrame) != hash0;
                    var clock = Field("_playTimer") is not null || Field("_idleFrameTimer") is not null && !isStatic && Prop("GameClickActive") is true;
                    report.Add($"{id:00} | {(isStatic ? "static" : "live")} | {(clock ? "on" : "off")} | {frames} | {(changed ? "yes" : "no")} | {(moved.Length == 0 ? "no" : moved.Trim())} | " +
                               $"{Control("PlaybackBar")} | {Control("PlaybackPauseButton")} | {Control("SkipLineButton")} | {Control("SkipCutsceneButton")} | " +
                               $"{Control("RoomCutsceneButton")} | {Control("CutsceneMenuButton")} | {Control("InteractButton")} | {Control("RoomSoundsButton")} | {Control("GameIntroButton")}");
                    if (isStatic) { sawStatic = true; staticRoom ??= id; } else sawLive = true;
                    if (roomsArg == "auto" && sawLive == true && sawStatic == true) break;
                }
                if (scene && staticRoom is int shown)
                {
                    var tag = rooms.First(r => r.Id == shown).Tag;
                    Call("SelectResourceInTree", tag);
                    if (Field("_roomOpenTask") is Task opening) { try { await opening; } catch { } }
                    // The room's list waits for the background runs of the scenes (cached after the first launch).
                    List<object>? items = null;
                    for (var n = 0; n < 1800; n++)
                    {
                        items = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("RoomCutsceneItems", Private)!.Invoke(window, [shown])!).Cast<object>().ToList();
                        if (items.Count > 0 && Control("RoomCutsceneButton") == "on") break;
                        await Task.Delay(100);
                    }
                    report.Add($"Room {shown:00}'s Play Cutscene lists {items!.Count} scene(s) after {open.Elapsed.TotalSeconds:0} s: " +
                               string.Join("; ", items.Select(i => i.GetType().GetProperty("Title")!.GetValue(i))) + $"; button {Control("RoomCutsceneButton")}");
                    if (items.Count > 0)
                    {
                        var first = items[0];
                        ((Action)first.GetType().GetProperty("Play")!.GetValue(first)!).Invoke();
                        var begun = false; var frames0 = (int)(Field("_playFrame") ?? 0);
                        for (var n = 0; n < 100 && !begun; n++)
                        {
                            await Task.Delay(100);
                            begun = Field("_play") is ScummSession { InCutscene: true } || Field("_play") is ScummSession { Messages.HaveMsg: not 0 };
                        }
                        report.Add($"played \"{first.GetType().GetProperty("Title")!.GetValue(first)}\": cutscene or line began {begun}; frames {(int)(Field("_playFrame") ?? 0) - frames0}; " +
                                   $"static preview {Field("_catalogPreviewOnly")}; bar {Control("PlaybackBar")}, Pause {Control("PlaybackPauseButton")}, Skip {Control("SkipCutsceneButton")}, Skip line {Control("SkipLineButton")}; status \"{Named<TextBlock>("StatusText").Text}\"");
                        if (!begun) Environment.ExitCode = 1;
                    }
                    else Environment.ExitCode = 1;
                }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "controls.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: PlayStartWindowCheck <game folder> <output folder> [room]
// MI2 play-start regression (20 October 2026). Headless first: times ScummGameSession.BootForPlay and the catalog preparation
// (PrepareManualRoom) of the room the boot ends in. Then opens the real MainWindow with a private profile (SCUMM_EXPLORER_PROFILE_ROOT) and room
// cache (SCUMM_ROOM_CACHE_DIR) in the output folder, selects that room (or [room]) in the tree as a user does, and logs every 100 ms change of:
// the play session, the play clock (DispatcherTimer), the static-preview fallback, the frames played and the background derivation. Writes play-start.txt.
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
        var report = new List<string>();
        if (args.Length > 2 && args[2] == "probe")
        {
            // Headless only: every room's catalog preparation from the boot state, and where the boot state goes by itself.
            var game = new GameFolderScanner().Scan(args[0]);
            var boot = ScummGameSession.BootForPlay(game, out _);
            report.Add($"boot ends in Room {boot.CurrentRoomId}, in cutscene {boot.InCutscene}, input {boot.UserInputOn}, cutscenes begun {boot.CutscenesBegun}");
            var ok = 0;
            foreach (var r in game.Rooms.Take(args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1000))
            {
                var w = Stopwatch.StartNew();
                var p = boot.PrepareManualRoom(r);
                if (p.Succeeded) ok++;
                report.Add($"  Room {r.ResourceId}: {w.Elapsed.TotalSeconds:0.00} s, {p.Frames} frames, {(p.Succeeded ? "live" : "static: " + p.Reason)}");
            }
            report.Add($"live {ok} of {game.Rooms.Count()}");
            var room = boot.CurrentRoomId;
            for (var f = 0; f < 6000; f++)
            {
                boot.AdvanceFrame();
                if (boot.CurrentRoomId != room) { report.Add($"  boot state +{f} frames: Room {room} -> {boot.CurrentRoomId}, in cutscene {boot.InCutscene}"); room = boot.CurrentRoomId; }
            }
            File.WriteAllLines(Path.Combine(output, "probe.txt"), report);
            return;
        }
        int? roomArg = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : null;
        int roomId;
        {
            var game = new GameFolderScanner().Scan(args[0]);
            var watch = Stopwatch.StartNew();
            var boot = ScummGameSession.BootForPlay(game, out _);
            report.Add($"headless BootForPlay: {watch.Elapsed.TotalSeconds:0.00} s, ends in Room {boot.CurrentRoomId}, input script {boot.PlayInputScript}, in cutscene {boot.InCutscene}");
            // Through reflection, so the harness also builds at commits before the catalog preparation existed (bisecting).
            var prepare = typeof(ScummSession).GetMethod("PrepareManualRoom");
            (bool Live, string Text) Prepare(int id)
            {
                if (prepare is null) return (true, "no catalog preparation at this commit");
                var w = Stopwatch.StartNew();
                dynamic result = prepare.Invoke(boot, [game.Rooms.First(r => r.ResourceId == id), 2400, 90, CancellationToken.None])!;
                return ((bool)result.Succeeded, $"{w.Elapsed.TotalSeconds:0.00} s, {result.Frames} frames, live {result.Succeeded}; reason \"{result.Reason}\"");
            }
            if (roomArg is { } chosen) roomId = chosen;
            else
            {
                // The boot's room when its catalog preparation is live (R-5.3a, R-8.5b); otherwise the first room in index order whose preparation is live.
                roomId = boot.CurrentRoomId ?? 1;
                var first = Prepare(roomId);
                report.Add($"headless PrepareManualRoom({roomId}), the boot's room: {first.Text}");
                if (!first.Live)
                    roomId = game.Rooms.Select(r => r.ResourceId!.Value).First(id => id != roomId && Prepare(id).Live);
            }
            report.Add($"headless PrepareManualRoom({roomId}), the room selected: {Prepare(roomId).Text}");
        }        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var open = Stopwatch.StartNew();
                Call("OpenGameFolder", args[0]);
                report.Add($"OpenGameFolder returned after {open.Elapsed.TotalSeconds:0.0} s");
                var tree = (TreeView)window.FindName("ResourceTree");
                var room = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == roomId &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room);
                report.Add($"Room {roomId} selected after {open.Elapsed.TotalSeconds:0.0} s");
                string last = "";
                double? firstFrame = null;
                for (var n = 0; n < 1500; n++)
                {
                    var play = (ScummGameSession?)Field("_play");
                    var state = $"play {(play is null ? "none" : $"Room {play.CurrentRoomId}")}, clock {(Field("_playTimer") is null ? "off" : "on")}, " +
                                $"static preview {Field("_catalogPreviewOnly")}, scan {Field("_scanDone")} of {Field("_scanTotal")}";
                    var frame = (int)(Field("_playFrame") ?? 0);
                    if (frame > 0 && firstFrame is null) firstFrame = open.Elapsed.TotalSeconds;
                    if (state != last) { report.Add($"  {open.Elapsed.TotalSeconds,6:0.0} s: {state}; frame {frame}; tooltip \"{((TextBlock)window.FindName("StatusText")).ToolTip}\""); last = state; }
                    if (firstFrame is { } f && open.Elapsed.TotalSeconds > f + 5) break;
                    if (firstFrame is null && open.Elapsed.TotalSeconds > 120) break;
                    await Task.Delay(100);
                }
                report.Add(firstFrame is { } at
                    ? $"FIRST FRAME {at:0.0} s after opening; frames after 5 more s: {Field("_playFrame")}"
                    : $"NO FRAME within {open.Elapsed.TotalSeconds:0} s; frames {Field("_playFrame")}");
                // Guard (20 October 2026): play reaches its first frame within 30 s of opening the game.
                if (firstFrame is not { } seen || seen > 30) { report.Add("FAIL: play did not reach its first frame within 30 s of opening"); Environment.ExitCode = 1; }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "play-start.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}


using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Usage: LaunchStalls <game folder> <room> <output folder> [seconds]
// 30 October 2026 (Indy 4 "first launch about 68 s"): the real MainWindow opens the game with a fresh private profile and room cache and picks the
// room as a catalog click does. A 100 ms UI timer logs every gap over 0.5 s (the UI thread was busy), and the times the play view first shows a
// frame and the background room lists finish. With dotnet-stack-like detail off: each stall is reported with the UI's last known activity.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[2]);
        if (Directory.Exists(output)) Directory.Delete(output, true);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var seconds = args.Length > 3 ? double.Parse(args[3]) : 90;
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        // "reopen" (startup): the private profile names the game as the last folder, and the window reopens it on its own.
        var reopen = Environment.GetEnvironmentVariable("SCUMM_LAUNCH_MODE") == "reopen";
        if (reopen)
        {
            Directory.CreateDirectory(Path.Combine(output, "profile"));
            File.WriteAllText(Path.Combine(output, "profile", "last-folder.txt"), args[0]);
        }
        // App.xaml's StartupUri would open a second MainWindow when Application.Run starts (it too would reopen the last folder), so the harness
        // pumps the dispatcher itself (Dispatcher.Run): one window only. The App still loads its resources.
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        var last = clock.Elapsed.TotalSeconds;
        var stallTotal = 0.0;
        var shown = false; var listsDone = false;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            var now = clock.Elapsed.TotalSeconds;
            if (now - last > 0.5) { stallTotal += now - last - 0.1; Log($"UI stall {now - last:F2} s (GC paused {GC.GetTotalPauseDuration().TotalSeconds:F1} s so far, gen2 {GC.CollectionCount(2)}, gen0 {GC.CollectionCount(0)}, heap {GC.GetTotalMemory(false) / 1048576} MB; scan {Field("_scanDone")}/{Field("_scanTotal")}, play shown {shown})"); }
            last = now;
            if (!shown && Field("_playShown") is not null) { shown = true; Log("play view shows its first frame"); }
            if (!listsDone && Field("_playRoomListsDone") is true) { listsDone = true; Log($"room lists done ({Field("_scanTotal")} groups)"); }
        };
        // Every dispatcher operation that runs over 0.5 s, by its delegate (DispatcherOperation's private _method, read 30 October 2026).
        var started = new Dictionary<DispatcherOperation, double>();
        var totals = new Dictionary<string, (int Count, double Seconds)>();
        var methodField = typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic);
        string Describe(DispatcherOperation op)
        {
            var d = methodField?.GetValue(op) as Delegate;
            var target = d?.Target;
            var inner = target?.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(f => f.GetValue(target)).OfType<Delegate>().FirstOrDefault();
            var args = typeof(DispatcherOperation).GetField("_args", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(op);
            string Of(object? o) => o switch
            {
                Delegate del => $"{del.Method.DeclaringType?.FullName}.{del.Method.Name}" + (del.Target is { } t ? $" [{t.GetType().FullName}]" : ""),
                object[] array => string.Join(", ", array.Select(Of)),
                null => "",
                _ => o.GetType().FullName ?? ""
            };
            var argsText = Of(args);
            // An async continuation's state machine names the method that awaited.
            return $"{d?.Method.DeclaringType?.FullName}.{d?.Method.Name}" + (inner is null ? "" : $" -> {inner.Method.DeclaringType?.FullName}.{inner.Method.Name}") + (argsText.Length > 0 ? $" args: {argsText}" : "");
        }
        app.Dispatcher.Hooks.OperationStarted += (_, e) => started[e.Operation] = clock.Elapsed.TotalSeconds;
        app.Dispatcher.Hooks.OperationCompleted += (_, e) =>
        {
            if (!started.Remove(e.Operation, out var at)) return;
            var took = clock.Elapsed.TotalSeconds - at;
            if (took > 0.5) Log($"  long UI operation {took:F2} s: {Describe(e.Operation)} ({e.Operation.Priority})");
            else
            {
                // Short operations, totalled by kind (the state machine's method for an await continuation) and priority.
                var text = Describe(e.Operation);
                var key = $"{e.Operation.Priority} {(text.Contains("+<") ? text[(text.LastIndexOf("+<", StringComparison.Ordinal) + 2)..text.IndexOf('>', text.LastIndexOf("+<", StringComparison.Ordinal))] : text[..Math.Min(90, text.Length)])}";
                totals[key] = (totals.GetValueOrDefault(key).Count + 1, totals.GetValueOrDefault(key).Seconds + took);
            }
        };
        window.Loaded += async (_, _) =>
        {
            try
            {
                timer.Start();
                // The window has shown: open the game in a later operation, as a user's Open Game Folder does, so its work is not part of the first show.
                if (reopen)
                {
                    Log("waiting for the window to reopen the last folder");
                    while (Field("_game") is null) await Task.Delay(20);
                    Log("the last folder is open");
                }
                else
                {
                    await Task.Delay(1500);
                    Log("opening");
                    Call("OpenGameFolder", args[0]);
                    Log("OpenGameFolder returned");
                }
                if (Environment.GetEnvironmentVariable("SCUMM_LAYOUT_PROBE") is not null)
                {
                    if (Environment.GetEnvironmentVariable("SCUMM_LAYOUT_PROBE") == "wait") { await Task.Delay(12000); Log("waited 12 s"); }
                    var probe = Stopwatch.StartNew();
                    window.UpdateLayout();
                    Log($"UpdateLayout {probe.Elapsed.TotalSeconds:F2} s");
                    probe.Restart();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    Log($"Invoke(Render) {probe.Elapsed.TotalSeconds:F2} s");
                    probe.Restart();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    Log($"second Invoke(Render) {probe.Elapsed.TotalSeconds:F2} s");
                }
                var game = (GameLibrary)Field("_game")!;
                var room = game.Rooms.First(r => r.ResourceId == int.Parse(args[1]));
                var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", BindingFlags.NonPublic)!;
                Call("SelectRoomWithIntent", room, Enum.Parse(kind, "Catalog"), false);
                Log($"picked Room {args[1]}");
                var until = clock.Elapsed.TotalSeconds + seconds;
                while (clock.Elapsed.TotalSeconds < until && !(shown && listsDone)) await Task.Delay(50);
                foreach (var (key, value) in totals.OrderByDescending(pair => pair.Value.Seconds).Take(12)) Log($"  short ops {value.Count,6} x, {value.Seconds,6:F2} s: {key}");
                await Task.Delay(3000);
                var actions = (System.Collections.ICollection?)Field("_genericSourceActions");
                var doorways = (System.Collections.ICollection?)Field("_playDoorwayTargets");
                Log($"room actions {actions?.Count}, doorway objects {doorways?.Count}");
                Log($"end: play shown {shown}, lists done {listsDone}, UI stalled {stallTotal:F1} s in total");
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { timer.Stop(); window.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        };
        window.Show();
        Dispatcher.Run();
    }
}

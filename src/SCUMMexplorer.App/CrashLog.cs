using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SCUMMexplorer.App;

/// <summary>The app's own folder: <c>SCUMM_EXPLORER_PROFILE_ROOT</c> when a harness sets it, else <c>%LOCALAPPDATA%\SCUMMexplorer</c>.</summary>
internal static class AppProfile
{
    public static string Root { get; } = Environment.GetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer");
}

/// <summary>
/// Crash log of the shipped app (30 October 2026; roadmap E1): a crash reported by the user left no trace, because only the harnesses log failures
/// (<c>.verification/HarnessFailurePolicy.cs</c>). Unhandled exceptions on the UI thread or any other thread are written to
/// <c>&lt;profile&gt;\crash-logs\crash-&lt;time&gt;.log</c> with the last actions. A failure no handler sees (a native fault, a stack overflow, a forced close)
/// leaves the <c>running.txt</c> trail behind; the next start keeps it as an <c>unclean-exit</c> log. Logging never throws.
/// Harnesses construct <see cref="MainWindow"/> without <see cref="App"/>, so <see cref="Note"/> does nothing for them.
/// </summary>
internal static class CrashLog
{
    private const int TrailLength = 20;
    private static readonly Queue<string> Trail = new();
    private static readonly object Gate = new();
    private static bool _started;

    private static string Folder => Path.Combine(AppProfile.Root, "crash-logs");
    private static string RunningFile => Path.Combine(Folder, "running.txt");
    private static string Version => typeof(CrashLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "?";

    public static void Start(Application app)
    {
        Try(() =>
        {
            Directory.CreateDirectory(Folder);
            if (File.Exists(RunningFile))
                File.Move(RunningFile, Path.Combine(Folder, $"crash-{File.GetLastWriteTime(RunningFile):yyyyMMdd-HHmmss}-unclean-exit.log"), overwrite: true);
        });
        _started = true;
        Note($"Started SCUMMexplorer {Version} on {Environment.OSVersion}");
        app.DispatcherUnhandledException += (_, e) => Fail("UI thread", e.Exception, fatal: true);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fail("background thread", e.ExceptionObject, fatal: e.IsTerminating);
        // An unobserved task failure does not end the process; it is logged so a silent failure behind a missing picture or sound leaves a trace.
        TaskScheduler.UnobservedTaskException += (_, e) => Fail("unobserved task", e.Exception, fatal: false);
    }

    /// <summary>Records one user-visible step (game opened, resource picked) in the trail written with a crash.</summary>
    public static void Note(string step)
    {
        if (!_started) return;
        lock (Gate)
        {
            Trail.Enqueue(DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " " + step);
            while (Trail.Count > TrailLength) Trail.Dequeue();
            var text = "SCUMMexplorer ended without a clean exit (a crash no handler saw, a forced close or a power loss). Last actions:\n" + string.Join("\n", Trail);
            Try(() => File.WriteAllText(RunningFile, text));
        }
    }

    /// <summary>A clean exit: the trail is no longer evidence of anything.</summary>
    public static void Stop()
    {
        _started = false;
        Try(() => File.Delete(RunningFile));
    }

    private static void Fail(string where, object error, bool fatal)
    {
        string path;
        lock (Gate)
        {
            path = Path.Combine(Folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
            var text = $"SCUMMexplorer {Version} on {Environment.OSVersion}, {DateTime.Now:O}\nUnhandled exception on the {where}{(fatal ? "" : " (not fatal)")}:\n{error}\n\nLast actions:\n{string.Join("\n", Trail)}\n";
            Try(() => File.AppendAllText(path, text));
        }
        if (!fatal) return;
        // The process ends after this handler; the crash log replaces the trail.
        Stop();
        Try(() => MessageBox.Show($"SCUMMexplorer stopped because of an error.\nThe details were saved to:\n{path}", "SCUMMexplorer", MessageBoxButton.OK, MessageBoxImage.Error));
    }

    private static void Try(Action action)
    {
        try { action(); } catch { /* Logging must never open another failure path. */ }
    }
}

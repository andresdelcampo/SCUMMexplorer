using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Applies to every .verification C# executable through Directory.Build.targets.
internal static class HarnessFailurePolicy
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fail(e.ExceptionObject);
        if (OperatingSystem.IsWindows())
        {
            SetErrorMode(0x0001 | 0x0002 | 0x8000); // critical-error, GP-fault and file-error UI off
            WerSetFlags(0x0020); // WER_FAULT_REPORTING_NO_UI (SetErrorMode documentation)
        }
#if SCUMM_WPF_HARNESS
        // The UI dispatcher otherwise offers its own unhandled-exception route.
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Fail(e.Exception);
        };
#endif
    }

    private static void Fail(object error)
    {
        var message = $"Harness {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name} failed at {DateTime.UtcNow:O}\n{error}";
        try
        {
            var path = Environment.GetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG");
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine("Exe", "HarnessFailures", $"failure-{Environment.ProcessId}.log");
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (parent is not null) Directory.CreateDirectory(parent);
            File.AppendAllText(path, message + Environment.NewLine);
        }
        catch { /* Logging must never open another failure path. */ }
        try { Console.Error.WriteLine(message); } catch { }
        Environment.Exit(1);
    }
}

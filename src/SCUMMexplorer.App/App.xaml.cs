using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SCUMMexplorer.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Diagnostic switch (24 October 2026): with the environment variable <c>SCUMM_EXPLORER_RENDER=software</c> WPF draws on the CPU only, which
    /// tells a graphics-driver or compositor problem (a desktop-wide stall with the hardware pointer still moving) from an application problem.
    /// Unset, WPF chooses its default render mode.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SCUMM_EXPLORER_RENDER"), "software", StringComparison.OrdinalIgnoreCase))
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        CrashLog.Start(this);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CrashLog.Stop();
        base.OnExit(e);
    }
}

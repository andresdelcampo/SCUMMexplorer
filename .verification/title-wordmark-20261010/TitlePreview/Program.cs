using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var output = Path.GetFullPath(".verification/title-wordmark-20261010/out");
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { ShowInTaskbar = false, Left = -10000, Top = -10000 };
        window.ContentRendered += (_, _) =>
        {
            var header = (Border)window.FindName("AppHeader");
            var title = (Image)window.FindName("AppTitleImage");
            var report = new List<string>();
            foreach (var width in new[] { 1180, 900 })
            foreach (var fullControls in new[] { false, true })
            {
                window.Width = width;
                foreach (var name in new[] { "CutsceneMenuButton", "GameTextsButton", "GameStatusButton" })
                    ((Button)window.FindName(name)).Visibility = fullControls ? Visibility.Visible : Visibility.Collapsed;
                window.UpdateLayout();
                var pixels = new RenderTargetBitmap((int)Math.Ceiling(header.ActualWidth), (int)Math.Ceiling(header.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                pixels.Render(header);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(pixels));
                var namePart = $"header-{width}-{(fullControls ? "full" : "welcome")}";
                using (var stream = File.Create(Path.Combine(output, namePart + ".png"))) encoder.Save(stream);
                report.Add($"{namePart}: header {header.ActualWidth}x{header.ActualHeight}, title {title.ActualWidth}x{title.ActualHeight}, source {title.Source.Width}x{title.Source.Height}");
            }
            File.WriteAllLines(Path.Combine(output, "layout.txt"), report);
            window.Close();
            app.Shutdown();
        };
        app.Run(window);
    }
}

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Render only the real status window on an isolated session. No MainWindow, profile access,
// edits, native ScummVM interaction or synthetic apply-button actions.
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                foreach (var (name, path) in new[] { ("ega-en", "loom ega mt32 en"), ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
                {
                    var game = new GameFolderScanner().Scan(Path.Combine(root, path));
                    var indexed = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
                    var session = new LoomExplorationSession(game, indexed);
                    var window = new GameStatusWindow(() => session, () => session.GameStatus.CanEdit, () => { }, game)
                        { ShowActivated = false };
                    window.Show(); await Task.Delay(250); window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(output, name + "-status.png"))) encoder.Save(stream);
                    window.Close();
                }
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); Environment.ExitCode = 1; }
            finally { app.Shutdown(); }
        }, DispatcherPriority.ApplicationIdle);
        app.Run();
    }
}

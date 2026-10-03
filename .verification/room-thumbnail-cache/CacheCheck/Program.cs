using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// Phase 1: the Open button path (OpenGameFolder) rebuilds the cache; wait for every card's image.
// Phase 2: a second window reopens the last folder (ReopenLastFolder); cards must have images at once.
// Phase 3: delete one cached room; reopening must fall back to the loader and rebuild it.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var folder = args[0]; var log = args[1];
        var app = new App(); app.InitializeComponent();
        var lines = new List<string>();
        int Images(MainWindow w) => ((WrapPanel)w.FindName("RoomCardList")).Children.OfType<Button>().Count(b => b.Content is Image);
        int Cards(MainWindow w) => ((WrapPanel)w.FindName("RoomCardList")).Children.OfType<Button>().Count();
        void Call(MainWindow w, string name, params object[] a) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(w, a);
        var cacheRoot = Environment.GetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR") ?? throw new Exception("Set SCUMM_ROOM_CACHE_DIR to a private folder.");
        int Pngs() => Directory.Exists(cacheRoot) ? Directory.GetFiles(cacheRoot, "room-*.png", SearchOption.AllDirectories).Length : 0;

        var w1 = new MainWindow();
        w1.ContentRendered += async (_, _) =>
        {
            try
            {
                Call(w1, "OpenGameFolder", folder);
                lines.Add($"button open: cards={Cards(w1)} imagesImmediately={Images(w1)}");
                for (var n = 0; n < 3000 && Images(w1) < Cards(w1); n++) await Task.Delay(100);
                await Task.Delay(3000); lines.Add($"after loader: images={Images(w1)}/{Cards(w1)} cachedPngs={Pngs()}");
                foreach (var b in ((WrapPanel)w1.FindName("RoomCardList")).Children.OfType<Button>().Where(b => b.Content is not Image))
                    lines.Add("  no image: " + b.ToolTip);
                w1.Close();

                var w2 = new MainWindow();
                w2.Show(); await Task.Delay(300);
                Call(w2, "ReopenLastFolder");
                lines.Add($"reopen (cache): cards={Cards(w2)} imagesImmediately={Images(w2)}");
                w2.Close();

                var one = Directory.GetFiles(cacheRoot, "room-03.png", SearchOption.AllDirectories).First();
                File.Delete(one);
                var w3 = new MainWindow();
                w3.Show(); await Task.Delay(300);
                Call(w3, "ReopenLastFolder");
                lines.Add($"reopen (incomplete cache): imagesImmediately={Images(w3)} of {Cards(w3)}");
                for (var n = 0; n < 3000 && Images(w3) < Cards(w3); n++) await Task.Delay(100);
                lines.Add($"after loader: images={Images(w3)}/{Cards(w3)} cachedPngs={Pngs()}"); for (var k = 0; k < 4; k++) { await Task.Delay(3000); lines.Add($"  +{(k + 1) * 3}s cachedPngs={Pngs()} room03Back={File.Exists(one)}"); }
                w3.Close();
            }
            catch (Exception ex) { lines.Add("FAILED " + ex); }
            File.WriteAllLines(log, lines);
            Application.Current.Shutdown();
        };
        app.Run(w1);
    }
}

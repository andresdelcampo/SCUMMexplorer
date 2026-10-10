using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;

// Times OpenGameFolder and the first room selection through the real MainWindow.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(Path.GetTempPath(), "frt-profile"));
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                if (args[0].Contains("probe")) { }
                var g = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(args[0]);
                sw.Restart(); var b = SCUMMexplorer.Core.Scumm.ScummSceneDrawBatches.Build(g); Console.WriteLine($"DrawBatches.Build: {sw.ElapsedMilliseconds} ms");
                sw.Restart(); var pl = new SCUMMexplorer.Core.Loom.LoomSceneSetupPlanner(g); Console.WriteLine($"SetupPlanner ctor: {sw.ElapsedMilliseconds} ms");
                sw.Restart(); var df = new SCUMMexplorer.Core.Loom.LoomDoorwayFinder(g); Console.WriteLine($"DoorwayFinder ctor: {sw.ElapsedMilliseconds} ms");
                sw.Restart(); SCUMMexplorer.Core.Loom.LoomGlossary.Load(g); Console.WriteLine($"Glossary.Load: {sw.ElapsedMilliseconds} ms");
                sw.Restart(); System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(g.FolderPath,"00.LFL"))); Console.WriteLine($"one hash: {sw.ElapsedMilliseconds} ms");
                sw.Restart();
                Call("OpenGameFolder", args[0]);
                Console.WriteLine($"OpenGameFolder: {sw.ElapsedMilliseconds} ms");
                var tree = (TreeView)window.FindName("ResourceTree");
                foreach (var room in args.Skip(1).Select(int.Parse))
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    sw.Restart();
                    Call("SelectResourceInTree", item.Tag);
                    Console.WriteLine($"Room {room} SelectResourceInTree returned: {sw.ElapsedMilliseconds} ms");
                    for (int n = 0; n < 600 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(50);
                    Console.WriteLine($"Room {room} loaded: {sw.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally { app.Shutdown(); }
        };
        app.Run(window);
    }
}

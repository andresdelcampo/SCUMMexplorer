using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app = new Application(); var window = new MainWindow();
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] values) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, values);
        var report = new List<string>(); int failures = 0;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]); await Task.Delay(1000);
                var tree = (TreeView)window.FindName("ResourceTree");
                var player = (SceneSoundPlayer)Field("_sceneSound")!;
                foreach (int id in new[] { 16, 24, 30, 36 })
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).Single(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room" && (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == id);
                    int from = player.Log.Count; Call("SelectResourceInTree", item.Tag);
                    for (int n = 0; n < 150 && !player.Log.Skip(from).Any(e => e.Action == "opened" && e.Kind.StartsWith("music")); n++) await Task.Delay(100);
                    var session = (LoomExplorationSession)Field("_session")!;
                    Check(player.Log.Skip(from).Any(e => e.Action == "opened" && e.Kind == "music (AdLib)"), $"Room {id}: music opens on arrival without game input");
                    Check(session.Snapshot.RoomId == id && !session.SessionTrace.TakeLast(10).Any(s => s.StartsWith("click:")), $"Room {id}: arrival keeps the catalog room and does not send a click");
                    report.AddRange(player.Log.Skip(from).Select(e => e.ToString()));
                }
                report.Add("Real WPF catalog selections and playback clock; no game click or manufactured sound event. Catalog arrival remains estimated.");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally { report.Add($"Failures: {failures}"); File.WriteAllLines(Path.Combine(args[1], "arrival-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

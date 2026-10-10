// Findings E-1: the room's "Play Cutscene" button is visible in Normal mode (R-1.13 keeps status text out of Normal, not this button).
// Usage: RoomCutsceneButton <game folder> [room...]   (default rooms 2 and 13 of the English EGA reference)
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static int Main(string[] args)
    {
        var failures = 0;
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var advanced = (CheckBox)window.FindName("AdvancedModeToggle");
                var button = (Button)window.FindName("RoomCutsceneButton");
                foreach (var room in args.Length > 1 ? args.Skip(1).Select(int.Parse) : [2, 13])
                {
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room &&
                            i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    foreach (var mode in new[] { false, true })
                    {
                        advanced.IsChecked = mode;
                        Call("UpdateAdvancedMode");
                        window.UpdateLayout();
                        var shown = button.Visibility == Visibility.Visible && button.IsVisible;
                        Console.WriteLine($"Room {room:00} {(mode ? "Advanced" : "Normal")}: Play Cutscene visible={shown} enabled={button.IsEnabled} content=\"{button.Content}\"");
                        if (!shown) failures++;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("FAILED: " + ex); failures++; }
            Console.WriteLine(failures == 0 ? "PASS" : $"FAIL ({failures})");
            app.Shutdown(failures == 0 ? 0 : 1);
        };
        // Off-screen and unfocused, so nobody mistakes the harness window for a stray app and closes it mid-run.
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000; window.ShowInTaskbar = false; window.ShowActivated = false;
        window.Show();
        return app.Run();
    }
}

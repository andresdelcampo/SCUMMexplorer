// Repro: switch rooms (02 -> 01) while the first-launch scan runs, then click Play Cutscene. Usage: MidscanClick <game folder>
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
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        async Task Select(int room)
        {
            var tree = (TreeView)window.FindName("ResourceTree");
            var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == room && i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
            Call("SelectResourceInTree", item.Tag);
            for (var n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
            await Task.Delay(500);
            window.UpdateLayout();
        }
        string State(string label)
        {
            var b = (Button)window.FindName("RoomCutsceneButton");
            var rooms = Field("_currentRoomResource")?.GetType().GetProperty("ResourceId")?.GetValue(Field("_currentRoomResource"));
            return $"{label}: room={rooms} button vis={b.Visibility} enabled={b.IsEnabled} scan={Field("_scanDone")}/{Field("_scanTotal")} listsDone={Field("_cdRoomListsDone")} session={(Field("_session") is null ? "null" : "set")} status={((TextBlock)window.FindName("StatusText")).Text}";
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                Console.WriteLine("scanned=" + Prop("ScannedCutscenes"));
                await Select(2);
                await Select(1);
                Console.WriteLine(State("room 1 mid-scan"));
                Call("RoomCutscene_Click", window, new RoutedEventArgs());
                for (var i = 0; i < 4; i++) { await Task.Delay(2000); window.UpdateLayout(); Console.WriteLine(State($"after click +{(i + 1) * 2}s")); }
            }
            catch (Exception ex) { Console.WriteLine("FAILED: " + ex); }
            app.Shutdown();
        };
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000; window.ShowInTaskbar = false; window.ShowActivated = false;
        window.Show();
        return app.Run();
    }
}

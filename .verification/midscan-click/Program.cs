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
                await Select(1);
                await Task.Delay(3000);
                Call("RoomCutscene_Click", window, new RoutedEventArgs());
                var gate = (System.Threading.ManualResetEventSlim)Field("_scanGate")!; var sn = Field("_sceneSound")!;
                for (var i = 0; i < 12; i++) { await Task.Delay(1000); if (i % 5 == 0) Console.WriteLine($"t={i}s gateOpen={gate.IsSet} preparing={sn.GetType().GetProperty("IsPreparing")!.GetValue(sn)} scan={Field("_scanDone")}"); }
                var ss = Field("_session")!;
                var evs = (System.Collections.IEnumerable)ss.GetType().GetProperty("SoundEvents")!.GetValue(ss)!;
                foreach (var e in evs) { var ev = e.GetType().GetProperty("Event")!.GetValue(e)!; Console.WriteLine("EVENT " + string.Join(" | ", ev.GetType().GetProperties().Select(pr => pr.Name + "=" + pr.GetValue(ev)))); }
                var snd = Field("_sceneSound")!;
                foreach (var e in (System.Collections.IEnumerable)snd.GetType().GetProperty("Log")!.GetValue(snd)!) Console.WriteLine("LOG " + string.Join(" | ", e.GetType().GetProperties().Select(pr => pr.Name + "=" + pr.GetValue(e))));
            }
            catch (Exception ex) { Console.WriteLine("FAILED: " + ex); }
            app.Shutdown();
        };
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000; window.ShowInTaskbar = false; window.ShowActivated = false;
        window.Show();
        return app.Run();
    }
}

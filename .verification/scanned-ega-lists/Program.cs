// Scan-derived cutscene lists for an EGA edition without a validated replay catalog (the Spanish comparison edition), and no change for the reference edition.
// Usage: ScannedEgaLists <game folder> [expectScanned=1|0]
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
        void Check(bool ok, string text) { Console.WriteLine((ok ? "ok   " : "FAIL ") + text); if (!ok) failures++; }
        var expectScanned = args.Length < 2 || args[1] != "0";
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Prop(string name) => typeof(MainWindow).GetProperty(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                Check((bool)Prop("ScannedCutscenes")! == expectScanned, $"ScannedCutscenes == {expectScanned}");
                if (args.Length > 2 && args[2] == "cd") // the CD game: the shipped seed is used at once (no waiting), a different disc would not match
                {
                    Check((bool)Field("_cdRoomListsDone")!, "room lists are known straight away (shipped seed matched)");
                    Check(Field("_cdSavedRoomLists") is System.Collections.IDictionary { Count: > 100 }, "the seed's lists are in use");
                    var cdItems = (System.Collections.IList)Call("RoomCutsceneItems", 1)!;
                    Check(cdItems.Count > 0 && cdItems[0]!.GetType().GetProperty("Title")!.GetValue(cdItems[0]) as string == "The Intro", $"Room 01 lists The Intro first ({cdItems.Count} entries)");
                    var cdRoom4 = (System.Collections.IList)Call("RoomCutsceneItems", 4)!;
                    Check(cdRoom4.Count >= 5, $"Room 04 lists {cdRoom4.Count} entries");
                    Console.WriteLine(failures == 0 ? "PASS" : $"FAIL ({failures})");
                    app.Shutdown(failures == 0 ? 0 : 1);
                    return;
                }
                if (expectScanned)
                {
                    for (var n = 0; n < 600 && !(bool)Field("_cdRoomListsDone")!; n++) await Task.Delay(1000);
                    Check((bool)Field("_cdRoomListsDone")!, "background derivation finished");
                    var total = (int)Field("_scanTotal")!;
                    Check(total > 0, $"the scan counts its steps ({total})");
                    for (var n = 0; n < 600 && (bool)Prop("ScanRunning")!; n++) await Task.Delay(1000);
                    Check(!(bool)Prop("ScanRunning")!, $"the scan finished all its steps ({Field("_scanDone")} of {total})");
                    window.UpdateLayout();
                    Check(!((ProgressBar)window.FindName("CutsceneScanProgress")).IsVisible, "the progress bar is gone when the scan is over");
                    var groups = (System.Collections.ICollection)Call("CdCutsceneGroups")!;
                    Check(groups.Count > 150, $"{groups.Count} scanned groups");
                    var items = (System.Collections.IList)Call("RoomCutsceneItems", 2)!;
                    Check(items.Count > 0, $"Room 02 lists {items.Count} entries");
                    var tree = (TreeView)window.FindName("ResourceTree");
                    var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                        .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 2 && i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                    Call("SelectResourceInTree", item.Tag);
                    for (var n = 0; n < 100 && Field("_currentRoomResource") != item.Tag; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    window.UpdateLayout();
                    var button = (Button)window.FindName("RoomCutsceneButton");
                    Check(button.IsVisible && button.IsEnabled, "Room 02 Play Cutscene visible and enabled");
                    // play the room's first entry that is a verb (a scan group), through the same path the button's popup uses
                    var group = ((System.Collections.IEnumerable)groups).Cast<object>().First(g => { var f = g.GetType().GetProperty("First")!.GetValue(g)!; return (int)f.GetType().GetProperty("Room")!.GetValue(f)! == 2 && (int)f.GetType().GetProperty("ObjectId")!.GetValue(f)! != 0; });
                    Call("PlayCdCutscene", group);
                    await Task.Delay(4000);
                    var status = ((TextBlock)window.FindName("StatusText")).Text;
                    Console.WriteLine("status: " + status);
                    Check(status.StartsWith("Playing", StringComparison.Ordinal), "a scanned scene starts playing");
                }
                else
                {
                    var items = (System.Collections.IList)Call("RoomCutsceneItems", 2)!;
                    Check(items.Count > 0, $"reference Room 02 lists {items.Count} catalog entries");
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

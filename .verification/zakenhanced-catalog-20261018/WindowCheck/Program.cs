using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var report = new List<string>();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        void Save(string name, FrameworkElement target)
        {
            target.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", @"D:\Juegos\Lucas\zakE ega en");
                await Task.Delay(1000);
                var tree = (TreeView)window.FindName("ResourceTree");
                var room = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).First(i =>
                    (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 1 &&
                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", room.Tag);
                for (var f = 0; f < 1200 && Field("_playTimer") is null; f++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("No live session");
                var timer = (DispatcherTimer)Field("_playTimer")!;
                await Task.Delay(300);
                timer.Stop();
                for (var f = 0; f < 3000 && !session.CaptureInventoryStatus().CanEdit; f++)
                {
                    Call("PlayTimer_Tick", null, EventArgs.Empty);
                    if (f % 10 == 0) await Task.Delay(10);
                }
                Require(session.CaptureInventoryStatus().CanEdit, "No safe admin edit point");
                Require(session.CaptureInventoryStatus().Catalog.Count == 76, "Catalog roster differs");
                string State() => JsonSerializer.Serialize(new {session.Snapshot.ObjectStates, session.Snapshot.ObjectOwners,
                    session.Snapshot.KnownGlobalVariables, session.Snapshot.Inventory, session.Actors, session.CurrentRoomId});
                var original = State();
                Exception? failure = null;
                _ = window.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    InventoryStatusWindow? dialog = null;
                    try
                    {
                        dialog = Application.Current.Windows.OfType<InventoryStatusWindow>().Single();
                        await Task.Delay(100);
                        Require(((TextBlock)dialog.FindName("AdvancedEvidence")).Visibility == Visibility.Collapsed, "Evidence leaked into Normal");
                        var search = (TextBox)dialog.FindName("ItemSearch");
                        var catalog = (ComboBox)dialog.FindName("CatalogItems");
                        var recipients = (ComboBox)dialog.FindName("GrantRecipient");
                        Require(catalog.Items.Count == 76, "All-item picker differs from source roster");
                        search.Text = "letter";
                        Require(catalog.Items.Count == 0, "Letter container wrongly offered as inventory");
                        Save("01-no-letter-container", dialog);
                        foreach (var target in new[] { (Id: 53, Search: "bird nest", Room: 13), (Id: 27, Search: "fan club card", Room: 3) })
                        {
                            search.Text = target.Search;
                            Require(catalog.Items.Count == 1, "Expected one source item: " + target.Search);
                            catalog.SelectedIndex = 0;
                            Require((int)catalog.SelectedItem.GetType().GetProperty("ObjectId")!.GetValue(catalog.SelectedItem)! == target.Id, "Wrong source item");
                            recipients.SelectedValue = 1;
                            ((Button)dialog.FindName("AddItemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Require(((Button)dialog.FindName("ApplyButton")).IsEnabled, "Pending grant unavailable");
                            Save($"02-pending-{target.Id}", dialog);
                            ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            var held = session.CaptureInventoryStatus().Items.Single(i => i.ObjectId == target.Id);
                            Require(held.OwnerId == 1 && held.RetainedRoomId == target.Room, "Wrong retained native source");
                            Save($"03-granted-{target.Id}", dialog);
                            ((Button)dialog.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Require(State() == original, "Undo changed unrelated state");
                            report.Add($"{target.Search} ({target.Id}): search, grant to Zak, retained room {target.Room}, exact-state Undo passed");
                        }
                        search.Clear();
                        Require(catalog.Items.Count == 76, "Search clear did not restore roster");
                        Save("04-restored-catalog", dialog);
                        ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { if (dialog?.IsVisible == true) dialog.Close(); }
                }));
                Call("GameStatus_Click", window.FindName("GameStatusButton"), new RoutedEventArgs(Button.ClickEvent));
                if (failure is not null) throw failure;
                Require(State() == original, "Closing admin changed state");
                report.Add("Normal catalog 76; no letter; both admin round trips; PASS");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally { File.WriteAllLines(Path.Combine(output, "window-check.txt"), report); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
    }
}

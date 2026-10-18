using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// PlayWindowCheck <fixture> <output> <selected room> <actions.json>
// Genuine click/key inputs in the actual MainWindow session. After checking its
// timer starts, drive that timer's handler deterministically; this is not a timing check.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var actions = JsonSerializer.Deserialize<ActionStep[]>(File.ReadAllText(args[3]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Save(string name, Window? target = null)
        {
            target ??= window; target.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            ScummGameSession? session = null;
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1000);
                var tree = (TreeView)window.FindName("ResourceTree");
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).First(i =>
                    (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == int.Parse(args[2]) &&
                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var i = 0; i < 1200 && Field("_playTimer") is null; i++) await Task.Delay(100);
                session = (ScummGameSession?)Field("_play") ?? throw new Exception("Play did not start");
                var timer = (DispatcherTimer?)Field("_playTimer") ?? throw new Exception("No play timer");
                await Task.Delay(300);
                Require((int)Field("_playFrame")! > 0, "Window timer did not advance");
                timer.Stop();
                var image = (Image)window.FindName("PlayScreenImage");
                Require(image.IsVisible && image.Cursor == Cursors.None, "Play image/cursor missing");
                Require(image.ToolTip is null && ((TextBlock)window.FindName("ObjectsNote")).Text == "", "Technical note in Normal");
                report.Add($"Initial room={session.CurrentRoomId}; timer frames={Field("_playFrame")}; Normal clean; cursor hidden");
                async Task Frames(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var before = (int)Field("_playFrame")!;
                        for (var wait = 0; wait < 2000 && (int)Field("_playFrame")! == before; wait++)
                        {
                            Call("PlayTimer_Tick", null, EventArgs.Empty);
                            if ((int)Field("_playFrame")! == before) await Task.Delay(10);
                        }
                        Require((int)Field("_playFrame")! > before, "Window frame did not advance (sound preparation?)");
                        if (i % 10 == 0) await Task.Delay(1);
                    }
                }
                foreach (var step in actions)
                {
                    switch (step.Op)
                    {
                        case "click":
                            Require(session.ClickScreen(step.X, step.Y, out var why) is not null, "Click refused: " + why);
                            break;
                        case "roomclick":
                            var screenX = step.X - session.ViewLeft;
                            Require(screenX is >= 0 and < 320, "Room point is outside the current view");
                            Require(session.ClickScreen(screenX, step.Y + session.MainScreen.Top, out var roomWhy) is not null, "Room click refused: " + roomWhy);
                            break;
                        case "point": session.PointAtScreen((step.X, step.Y)); break;
                        case "text":
                            var composition = new TextComposition(InputManager.Current, window, step.Text!);
                            Call("PlayTextInput", new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
                            break;
                        case "escape":
                            var source = PresentationSource.FromVisual(window)!;
                            Require((bool)Call("PlayKeyDown", new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent })!, "Escape not handled");
                            break;
                        case "frames": await Frames(step.Count); break;
                        case "room": Require(session.CurrentRoomId == step.Value, $"Expected room {step.Value}; got {session.CurrentRoomId}"); break;
                        case "untilroom":
                            for (var i = 0; i < step.Count && session.CurrentRoomId != step.Value; i++) await Frames(1);
                            Require(session.CurrentRoomId == step.Value, $"Did not reach room {step.Value}");
                            break;
                        case "untilskip":
                            for (var i = 0; i < step.Count && !session.CutsceneSkippable; i++) await Frames(1);
                            Require(session.CutsceneSkippable, "Did not reach cutscene override");
                            break;
                        case "untilcontrol":
                            for (var i = 0; i < step.Count && (!session.UserInputOn || session.InCutscene); i++) await Frames(1);
                            Require(session.UserInputOn && !session.InCutscene, "Control did not return");
                            break;
                        case "owner": Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) == step.Value, $"Object {step.X} not owned by {step.Value}"); break;
                        case "untilowner":
                            for (var i = 0; i < step.Count && session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) != step.Value; i++) await Frames(1);
                            Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) == step.Value, $"Object {step.X} not owned by {step.Value}");
                            break;
                        case "variable": Require(session.Snapshot.KnownGlobalVariables.GetValueOrDefault(step.Text!) == step.Value, "Unexpected variable: " + step.Text); break;
                        case "trace": Require(session.SessionTrace.Any(s => s.Contains(step.Text!, StringComparison.Ordinal)), "Missing trace: " + step.Text); break;
                        case "message": Require(session.Messages.OnScreen.Any(m => m.Text.Contains(step.Text!, StringComparison.Ordinal)), "Missing message: " + step.Text); break;
                        case "control": Require(session.UserInputOn && !session.InCutscene, "No player control"); break;
                        case "save": Call("ShowPlayFrame"); await Task.Delay(30); Save(step.Text!); break;
                        default: throw new Exception("Unknown action: " + step.Op);
                    }
                    report.Add($"{step.Op} {step.Text} ({step.X},{step.Y}) n={step.Count} value={step.Value}: room={session.CurrentRoomId}, inputs={session.DeliveredInput.Count}, frame={Field("_playFrame")}");
                    File.WriteAllLines(Path.Combine(output, "progress.txt"), report);
                }
                for (var n = 0; n < 3000 && !session.CaptureInventoryStatus().CanEdit; n++) await Frames(1);
                Require(session.CaptureInventoryStatus().CanEdit, "No safe edit point: " + session.CaptureInventoryStatus().Refusal);
                var held = actions.Last(a => a.Op == "owner");
                var targetActor = session.CaptureInventoryStatus().Characters.First(c => c.ActorId != held.Value && c.Name is not null).ActorId;
                var originalInventory = session.Snapshot.Inventory.ToArray();
                var header = (Button)window.FindName("GameStatusButton");
                Require(header.IsVisible && header.IsEnabled, "Inventory header action missing");
                string Unrelated() => JsonSerializer.Serialize(new {
                    session.Snapshot.ObjectStates, session.Snapshot.KnownGlobalVariables, session.Snapshot.ObjectClasses,
                    session.Snapshot.ObjectPositions, session.Actors, session.CurrentRoomId, session.CutscenesBegun,
                    Others = session.Snapshot.ObjectOwners.Where(p => p.Key != held.X).OrderBy(p => p.Key)
                });
                var beforeState = Unrelated();
                void DialogCheck(bool roundTrip, bool paused)
                {
                    if (paused)
                    {
                        Call("PlaybackPause_Click", window, new RoutedEventArgs(Button.ClickEvent));
                        Call("PlayTimer_Tick", null, EventArgs.Empty);
                        Require((bool)Field("_playPaused")!, "Pause action did not pause");
                    }
                    timer.Start();
                    var frame = (int)Field("_playFrame")!;
                    Exception? failure = null;
                    window.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        InventoryStatusWindow? dialog = null;
                        try
                        {
                            dialog = Application.Current.Windows.OfType<InventoryStatusWindow>().Single();
                            await Task.Delay(250);
                            Require(!timer.IsEnabled && (bool)Field("_playPaused")! && (int)Field("_playFrame")! == frame, "Clock advanced in modal status");
                            Require(((TextBlock)dialog.FindName("AdvancedEvidence")).Visibility == Visibility.Collapsed, "Technical evidence in Normal status");
                            ComboBox Selector() => ((StackPanel)dialog.FindName("InventoryRows")).Children.OfType<Grid>()
                                .SelectMany(g => g.Children.OfType<ComboBox>()).Single(c => (int)c.Tag == held.X);
                            var selector = Selector();
                            Require(selector.IsEnabled && selector.ToolTip is null, "Held item is not editable in Normal");
                            selector.SelectedValue = targetActor;
                            Require(selector.SelectedItem?.ToString() == session.CaptureInventoryStatus().Characters.Single(c => c.ActorId == targetActor).Name,
                                "Recipient label exposes a record or identifier instead of its source name");
                            Require(((Button)dialog.FindName("ApplyButton")).IsEnabled, "Apply not enabled for pending choice");
                            if (roundTrip)
                            {
                                ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(session.Snapshot.ObjectOwners[held.X] == targetActor, "Apply did not change owner");
                                Require(!session.Snapshot.Inventory.Contains(held.X), "Native current-character inventory did not refresh");
                                Require(Unrelated() == beforeState, "Apply changed unrelated game state");
                                Save("status-applied", dialog);
                                ((Button)dialog.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(session.Snapshot.ObjectOwners[held.X] == held.Value, "Undo did not restore owner");
                                Require(session.Snapshot.Inventory.SequenceEqual(originalInventory) && Unrelated() == beforeState, "Undo did not restore inventory or preserve state");
                                Save("status-undone", dialog);
                                Selector().SelectedValue = targetActor;
                            }
                            ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        catch (Exception ex) { failure = ex; }
                        finally { if (dialog?.IsVisible == true) dialog.Close(); }
                    }));
                    Call("GameStatus_Click", header, new RoutedEventArgs(Button.ClickEvent));
                    if (failure is not null) throw failure;
                    Require(timer.IsEnabled && (bool)Field("_playPaused")! == paused, "Previous timer/pause state was not restored");
                    timer.Stop();
                    Require((int)Field("_playFrame")! == frame, "Opening/closing status advanced the session");
                    Require(session.Snapshot.ObjectOwners[held.X] == held.Value && Unrelated() == beforeState, "Cancel changed game state");
                    report.Add($"Status modal paused={paused}: Apply/Undo={roundTrip}; Cancel retained owner={held.Value}; frame={frame}; timer restored");
                    File.WriteAllLines(Path.Combine(output, "progress.txt"), report);
                }
                DialogCheck(roundTrip: true, paused: false);
                DialogCheck(roundTrip: false, paused: true);
                Save("status-returned");
                report.Add("PASS");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "play-check.txt"), report);
                if (session is not null) File.WriteAllLines(Path.Combine(output, "session-trace.txt"), session.SessionTrace);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
    private sealed record ActionStep(string Op, int X = 0, int Y = 0, int Count = 0, int Value = 0, string? Text = null);
}

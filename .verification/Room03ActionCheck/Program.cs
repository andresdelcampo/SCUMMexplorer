using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        var objectId = int.Parse(args[2]);
        var expectedRoom = int.Parse(args[3]);
        // Optional reverse check: object and verb in the destination that should lead back to Room 03.
        int? reverseObject = args.Length > 4 ? int.Parse(args[4]) : null;
        var reverseVerb = args.Length > 5 ? Convert.ToInt32(args[5], 16) : 0;
        Directory.CreateDirectory(output);
        var report = new List<string> { "Real MainWindow; in-process coordinate and routed-key dispatch. No physical mouse/keyboard input." };
        var app = new Application();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        T Named<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        int? DisplayedRoom() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId;
        async Task WaitRoom(int room)
        {
            // The room view loads asynchronously; wait for its object records as well.
            for (int i = 0; i < 100 && (DisplayedRoom() != room ||
                     ((IReadOnlyList<LoomRoomObjectRecord>)Field("_currentRoomObjectRecords")!).Count == 0); i++)
                await Task.Delay(100);
            Check(DisplayedRoom() == room, $"Room {room:00} displayed");
            await Task.Delay(300);
            window.UpdateLayout();
            Check(Named<Image>("RoomPreview").Source is BitmapSource, "Destination/background bitmap exists");
        }
        async Task BrowseRoom(int room)
        {
            var item = Named<TreeView>("ResourceTree").Items.OfType<TreeViewItem>()
                .SelectMany(g => g.Items.OfType<TreeViewItem>())
                .First(i => i.Tag is GameResource r && r.Kind == ResourceKind.Room && r.ResourceId == room);
            Call("SelectResourceInTree", item.Tag);
            await WaitRoom(room);
            report.Add($"Setup only: Room {room:00} selected by catalog browsing.");
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            report.Add("PASS: " + message);
        }
        void Capture(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(stream);
        }
        void Enter(Button button)
        {
            Check(button.Focusable && button.IsTabStop && button.IsEnabled, "Action button is enabled, focusable and a tab stop");
            button.BringIntoView();
            window.UpdateLayout();
            Check(button.Focus(), "Action button accepts keyboard focus");
            var source = PresentationSource.FromVisual(button) ?? throw new Exception("No WPF presentation source");
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
                { RoutedEvent = Keyboard.KeyDownEvent });
            report.Add("Dispatched synthetic routed Enter (not physical keyboard input).");
        }
        void CompleteChoice(bool keyboard, int room = 3, int? id = null, int verb = 0)
        {
            var target = id ?? objectId;
            if (Session().LastAction is { } action && action.SourceRoomId == room && (action.ObjectId == target || action.IsGameInput)) return;
            var choices = Named<StackPanel>("ObjectVerbButtons").Children.OfType<Button>()
                .Where(b => b.ToolTip?.ToString()?.Contains($"Object {target} ") == true &&
                            (verb == 0 || b.ToolTip!.ToString()!.Contains($"0x{verb:X2}"))).ToArray();
            Check(choices.Length > 0, $"Decoded object {target} interaction offered");
            if (keyboard) Enter(choices[0]);
            else choices[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        async Task VerifyAction(string label)
        {
            var action = Session().LastAction;
            if (expectedRoom == 0)
            {
                Check(action?.SourceRoomId == 3 && action.ObjectId == objectId, $"{label}: object {objectId} verb executed");
                Check(action!.DestinationRoomId is null && action.ReachedRoomLoad is null,
                    "Waiting child does not invent a reached destination");
                Check(Session().Snapshot.PendingScripts > 0, "Child script remains pending");
                report.Add(action.Evidence);
                report.AddRange(action.Uncertainties.Select(value => "Unresolved: " + value));
                await WaitRoom(3);
                Call("AdvanceFrame_Click", window, new RoutedEventArgs());
                Check(DisplayedRoom() == 3 && Session().Snapshot.RoomId == 3 && Session().Snapshot.PendingScripts > 0,
                    "Advancing a frame retains Room03 and its unresolved waiting script");
                Check(Named<TextBlock>("ObjectActionNote").Text.Contains("incomplete", StringComparison.OrdinalIgnoreCase),
                    "Visible action label retains incomplete-state warning");
                report.Add("ObjectActionNote: " + Named<TextBlock>("ObjectActionNote").Text);
                report.Add("SessionStatusText: " + Named<TextBlock>("SessionStatusText").Text);
                Capture(label);
                return;
            }
            if (action?.DestinationRoomId is null)
            {
                Check(Session().Snapshot.PendingScripts > 0, $"{label}: the started script is pending after the click");
                report.Add("After the click: " + Named<TextBlock>("ObjectActionNote").Text);
                await WaitRoom(3);
                Capture(label + "-pending");
                for (var frame = 1; frame <= 3 && Session().Snapshot.RoomId == 3; frame++)
                {
                    Check(Named<Button>("AdvanceFrameButton").IsEnabled, "Continue button is enabled while scripts are pending");
                    var pendingBefore = Session().Snapshot.PendingScripts;
                    var guard = $"_currentRoom {(Field("_currentRoom") is null ? "null" : "set")}, _currentRoomResource {DisplayedRoom():00}";
                    Call("AdvanceFrame_Click", window, new RoutedEventArgs());
                    report.Add($"Continue pressed (frame {frame}); pending {pendingBefore} -> {Session().Snapshot.PendingScripts}; {guard}; session room {Session().Snapshot.RoomId:00}.");
                }
                action = Session().LastAction;
                report.AddRange(Session().SessionTrace.TakeLast(4).Select(line => "Session: " + line));
                report.AddRange(Session().Snapshot.Uncertainties.TakeLast(6).Select(line => "Session uncertainty: " + line));
            }
            Check(action?.SourceRoomId == 3 && (action.ObjectId == objectId || action.IsGameInput) && action.DestinationRoomId == expectedRoom,
                $"{label}: object {objectId} execution reached Room {expectedRoom:00}");
            Check(action!.ReachedRoomLoad is not null, "Destination came from a reached room-load event");
            report.Add(action.Evidence);
            await WaitRoom(expectedRoom);
            var note = Named<TextBlock>("ObjectActionNote").Text;
            Check(note.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                  note.Contains("unresolved", StringComparison.OrdinalIgnoreCase),
                "Visible action label retains the state-preview or unresolved-result warning");
            report.Add("ObjectActionNote: " + note);
            report.Add("SessionStatusText: " + Named<TextBlock>("SessionStatusText").Text);
            Capture(label);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await BrowseRoom(3);
                Check(Named<System.Windows.Controls.Primitives.ToggleButton>("SessionModeToggle").IsChecked == true, "Exploration mode enabled");
                Capture("room-03-before");
                var records = (IReadOnlyList<LoomRoomObjectRecord>)Field("_currentRoomObjectRecords")!;
                var record = records.Single(r => r.Id == objectId);
                // The upper-left quarter also covers the stored image overlapping object 162.
                int x = record.X + record.Width / 3, y = record.Y + record.Height / 3;
                report.Add($"Coordinate dispatch at room pixel ({x},{y}) in decoded rectangle ({record.X},{record.Y},{record.Width},{record.Height}).");
                Call("HandleRoomPoint", x, y, false);
                CompleteChoice(keyboard: false);
                await VerifyAction("coordinate-action");

                await BrowseRoom(3);
                Call("ResetSession_Click", window, new RoutedEventArgs());
                // The text-based hotspot catalog is an Advanced-mode aid; enter it explicitly
                // before exercising the keyboard path, then focus the contextual choice.
                var advancedToggle = Named<System.Windows.Controls.Primitives.ToggleButton>("AdvancedModeToggle");
                advancedToggle.IsChecked = true;
                Call("AdvancedModeToggle_Click", advancedToggle, new RoutedEventArgs());
                Named<Expander>("ObjectListSection").IsExpanded = true;
                window.UpdateLayout();
                var hotspot = Named<StackPanel>("ObjectList").Children.OfType<Button>().Single(b => b.Tag is int id && id == objectId);
                Enter(hotspot);
                CompleteChoice(keyboard: true);
                await VerifyAction("keyboard-action");
                await BrowseRoom(3);
                Call("ResetSession_Click", window, new RoutedEventArgs());
                Named<System.Windows.Controls.Primitives.ToggleButton>("SessionModeToggle").IsChecked = false;
                var inspectSelection = (RoomObjectSelection)Field("_objectSelection")!;
                var inspectObjects = (IReadOnlyList<RoomObjectImage>)Field("_objects")!;
                report.Add($"Inspection setup: {inspectObjects.Count} images; hit at ({x},{y}) = {inspectSelection.HitTest(x, y)?.Id.ToString() ?? "none"}; room {DisplayedRoom():00}; current room image {(Field("_currentRoom") is null ? "null" : "set")}.");
                var beforeInspection = Session().LastAction;
                Call("HandleRoomPoint", x, y, false);
                Check(Session().LastAction == beforeInspection && DisplayedRoom() == 3,
                    "Inspection-mode click at the same overlap does not execute an action or navigate");
                report.Add("Inspection StatusText: " + Named<TextBlock>("StatusText").Text);
                // 166 is object 165's close-up, a verb-area image (27 September): it is not room artwork.
                Check(!Named<TextBlock>("StatusText").Text.Contains("Object 166", StringComparison.Ordinal),
                    "Inspection-mode click does not paint verb-area close-up 166 onto the room");
                Capture("inspection-artwork");
                Named<System.Windows.Controls.Primitives.ToggleButton>("SessionModeToggle").IsChecked = true;
                if (expectedRoom == 0)
                {
                    await BrowseRoom(4);
                    Capture("room-04-browse-only");
                    report.Add("Room04 image rendered through direct catalog browsing only, not a reached transition. Reverse traversal unresolved.");
                }
                else if (reverseObject is int back)
                {
                    await BrowseRoom(3);
                    Call("ResetSession_Click", window, new RoutedEventArgs());
                    var hotspotAgain = Named<StackPanel>("ObjectList").Children.OfType<Button>().Single(b => b.Tag is int id && id == objectId);
                    hotspotAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    CompleteChoice(keyboard: false);
                    await VerifyAction("forward-for-reverse");
                    var reverseHotspot = Named<StackPanel>("ObjectList").Children.OfType<Button>().Single(b => b.Tag is int id && id == back);
                    reverseHotspot.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    CompleteChoice(keyboard: false, room: expectedRoom, id: back, verb: reverseVerb);
                    await WaitRoom(expectedRoom);
                    for (var frame = 1; frame <= 3 && Session().Snapshot.RoomId == expectedRoom; frame++)
                        Call("AdvanceFrame_Click", window, new RoutedEventArgs());
                    var reverse = Session().LastAction;
                    Check(reverse?.SourceRoomId == expectedRoom && reverse.ObjectId == back && reverse.DestinationRoomId == 3 &&
                          reverse.ReachedRoomLoad is not null,
                        $"Reverse: Room {expectedRoom:00} object {back} reached a room load back to Room 03");
                    report.Add(reverse!.Evidence);
                    await WaitRoom(3);
                    report.Add("ObjectActionNote: " + Named<TextBlock>("ObjectActionNote").Text);
                    Capture("reverse-room-03");
                }
                else report.Add("Reverse traversal is not asserted by this harness; catalog return was setup only.");
            }
            catch (Exception ex)
            {
                report.Add("FAIL: " + ex);
                Capture("failure");
                Environment.ExitCode = 1;
            }
            finally
            {
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(Path.Combine(output, "window-check.txt"), report); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(Path.Combine(output, "window-check.txt")));
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fallback)!);
                        System.IO.File.WriteAllLines(fallback, report);
                        Console.Error.WriteLine($"Report not written ({writeError.Message}); wrote {fallback} instead.");
                    }
                    catch (Exception fallbackError) when (fallbackError is System.IO.IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Report not written ({writeError.Message}; {fallbackError.Message}):");
                        foreach (var line in report) Console.Error.WriteLine(line);
                    }
                    Environment.ExitCode = 2;
                }
                window.Close();
                app.Shutdown();
            }
        };
        app.Run(window);
    }
}

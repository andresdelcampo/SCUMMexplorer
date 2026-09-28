using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// For every room with doorway boxes on the real MainWindow: browse to the room,
// click (in-process HandleRoomPoint: the game's click, with the direct verb as a
// labelled fallback) a point of each box that no other box
// covers, and report whether the view entered one of that box's rooms.
// Arguments: <Games\loom> <out.txt> [room numbers]. No physical input.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static void Main(string[] args)
    {
        var output = args[1];
        var only = args.Skip(2).Select(int.Parse).ToHashSet();
        var app = new Application();
        var window = new MainWindow();
        // Since M1d a click plays on a timer; the harness runs the frames at once, as before.
        typeof(MainWindow).GetField("_playInstantly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, true);
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        static T Prop<T>(object item, string name) => (T)item.GetType().GetProperty(name)!.GetValue(item)!;
        int? Displayed() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId;
        var report = new List<string> { "Real MainWindow; in-process HandleRoomPoint calls. No physical mouse input." };

        async Task<bool> Browse(GameResource room)
        {
            Call("SelectResourceInTree", room);
            for (var n = 0; n < 100 && (Displayed() != room.ResourceId ||
                     ((IReadOnlyList<LoomRoomObjectRecord>)Field("_currentRoomObjectRecords")!).Count == 0); n++)
                await Task.Delay(50);
            await Task.Delay(150);
            window.UpdateLayout();
            return Displayed() == room.ResourceId;
        }

        window.ContentRendered += async (_, _) =>
        {
            int boxes = 0, passed = 0, byGame = 0;
            try
            {
                Call("OpenGameFolder", args[0]);
                var game = (GameLibrary)Field("_game")!;
                foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue && (only.Count == 0 || only.Contains(r.ResourceId.Value)))
                             .OrderBy(r => r.ResourceId))
                {
                    if (!await Browse(room)) { report.Add($"Room {room.ResourceId:00}: did not load"); continue; }
                    var candidates = ((System.Collections.IEnumerable)Field("_roomNavigationCandidates")!).Cast<object>().ToArray();
                    var groups = candidates.GroupBy(c => Prop<LoomRoomObjectRecord>(c, "Object").Id).ToArray();
                    foreach (var group in groups)
                    {
                        var record = Prop<LoomRoomObjectRecord>(group.First(), "Object");
                        if (record.Width <= 0 || record.Height <= 0) continue;
                        var targets = group.Select(c => Prop<int>(c, "TargetRoomId")).Distinct().Order().ToArray();
                        var others = groups.Where(g => g.Key != group.Key).Select(g => Prop<LoomRoomObjectRecord>(g.First(), "Object")).ToArray();
                        // A point of this box that no other doorway box covers, else its centre.
                        (int X, int Y)? own = null;
                        for (var y = record.Y; y < record.Y + record.Height && own is null; y += 4)
                            for (var x = record.X; x < record.X + record.Width && own is null; x += 4)
                                if (!others.Any(o => x >= o.X && x < o.X + o.Width && y >= o.Y && y < o.Y + o.Height))
                                    own = (x + 1, y + 1);
                        var point = own ?? (record.X + record.Width / 2, record.Y + record.Height / 2);
                        if (!await Browse(room)) continue;
                        // Doorways the room does not hold now (owner or parent state) are not clickable.
                        var present = ((System.Collections.IEnumerable)Call("DoorwaysAt", point.X, point.Y)!).Cast<LoomRoomObjectRecord>().ToArray();
                        if (present.All(item => item.Id != record.Id))
                        {
                            report.Add($"NOT PRESENT Room {room.ResourceId:00} object {record.Id} → targets {string.Join('/', targets.Select(t => t.ToString("00")))}");
                            continue;
                        }
                        boxes++;
                        var snapshot = ((LoomExplorationSession)Field("_session")!).Snapshot;
                        var live = (snapshot.IsUntouchable(record.Id) ? " (untouchable now)" : "") + $" [var71=0x{snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x071]"):X}]";
                        var menuBefore = Field("_doorwayMenu");
                        Call("HandleRoomPoint", point.X, point.Y, false);
                        var via = "";
                        if (Field("_doorwayMenu") is ContextMenu menu && menu != menuBefore && Displayed() == room.ResourceId)
                        {
                            var items = menu.Items.OfType<MenuItem>().ToArray();
                            via = $" via menu [{string.Join(", ", items.Select(i => i.Header))}]";
                            var pick = items.FirstOrDefault(i => targets.Any(t => i.Header?.ToString() == $"Room {t:00}" ||
                                i.ToolTip?.ToString()?.StartsWith($"Object {record.Id}:") == true));
                            menu.IsOpen = false;
                            pick?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        }
                        for (var n = 0; n < 60 && Displayed() == room.ResourceId; n++) await Task.Delay(50);
                        await Task.Delay(100);
                        var now = Displayed();
                        var ok = now is int entered && targets.Contains(entered);
                        // A box with no point of its own that is untouchable now: findObject picks the touchable
                        // doorway over it (the game's rule), so that doorway's room is the expected one, labelled.
                        var picked = own is null && snapshot.IsUntouchable(record.Id)
                            ? present.FirstOrDefault(item => item.Id != record.Id && !snapshot.IsUntouchable(item.Id)) : null;
                        if (!ok && picked is not null && now is int reachedRoom &&
                            candidates.Where(c => Prop<LoomRoomObjectRecord>(c, "Object").Id == picked.Id).Any(c => Prop<int>(c, "TargetRoomId") == reachedRoom))
                        {
                            ok = true;
                            via += $" [overlapped and untouchable now: findObject picks object {picked.Id}, which leads there]";
                        }
                        if (ok) passed++;
                        // Since 28 September the click is the game's (scripts 005–007); the doorway's verb runs directly only as a fallback.
                        var gameClick = ((LoomExplorationSession)Field("_session")!).LastAction is { IsGameInput: true };
                        if (ok && gameClick) byGame++;
                        via += gameClick ? " [game's click]" : " [direct verb, fallback: " +
                            Field("_lastDoorwayFallback") + "]";
                        var note = ((TextBlock)window.FindName("ObjectActionNote")).Text.Replace("\n", " ");
                        var choices = ((Panel)window.FindName("ObjectVerbButtons")).Children.OfType<Button>().Select(b => b.Content?.ToString()).ToArray();
                        report.Add($"{(ok ? "PASS" : "FAIL")} Room {room.ResourceId:00} object {record.Id} box ({record.X},{record.Y}) {record.Width}x{record.Height}" +
                                   $"{live} → targets {string.Join('/', targets.Select(t => t.ToString("00")))}; click ({point.X},{point.Y}){(own is null ? " [overlapped centre]" : "")}" +
                                   $" → Room {now:00}{via}" + (ok ? "" : $"; note: {note}; choices: [{string.Join(", ", choices)}]"));
                    }
                }
            }
            catch (Exception ex) { report.Add("ERROR " + ex); Environment.ExitCode = 1; }
            finally
            {
                report.Insert(1, $"Doorway boxes: {boxes}; navigated to one of their rooms: {passed} ({byGame} through the game's click, {passed - byGame} through the direct-verb fallback); failed: {boxes - passed}");
                // A report that cannot be written (for example a sandbox denying the folder)
                // must not crash the harness: fall back to the temp folder and say so.
                try { File.WriteAllLines(output, report); }
                catch (Exception writeError) when (writeError is System.IO.IOException or UnauthorizedAccessException)
                {
                    var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCUMMexplorer-harness", System.IO.Path.GetFileName(output));
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

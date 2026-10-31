using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: WindowCheck <game folder> <output folder> <room,object,verb>...
// 31 October 2026 (REQUIREMENTS §8.6): the real MainWindow opens the game, waits for the background scene derivation, then plays each named scene
// from the whole-game list (its Play action, as a pick in the picker does) and logs the room, the texts on screen and the setup, with a picture.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var report = new List<string>(); var clock = Stopwatch.StartNew();
        void Log(string message) { report.Add($"{clock.Elapsed.TotalSeconds,6:F2}s {message}"); File.WriteAllLines(Path.Combine(output, "report.txt"), report); Console.WriteLine(report[^1]); }
        var app = new App(); app.InitializeComponent(); var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Capture(string name)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        async Task Pump(double seconds) { var until = clock.Elapsed.TotalSeconds + seconds; while (clock.Elapsed.TotalSeconds < until) await Task.Delay(20); }
        void State(string stage)
        {
            var play = (ScummSession?)Field("_play") ?? (ScummSession?)Field("_session");
            Log($"{stage}: room={play?.CurrentRoomId} cutscene={play?.InCutscene} texts=[{string.Join(" / ", play?.Messages.OnScreen.SelectMany(m => m.Lines).Where(l => l.Length > 0).Take(3) ?? [])}]");
        }
        window.Loaded += async (_, _) =>
        {
            try
            {
                Call("OpenGameFolder", args[0]);
                await Pump(3);
                for (var i = 0; i < 300 && Field("_playPlanner") is not null && !(bool)Field("_playRoomListsDone")!; i++) await Pump(1);
                Log($"derivation done={Field("_playRoomListsDone")}");
                foreach (var raw in args.Skip(2))
                {
                    // a lists (LOOM) the objects with verbs that are not hotspots now: what the Interact overview adds since 31 October.
                    if (raw == "a")
                    {
                        var session = Field("_session");
                        var snapshot = session?.GetType().GetProperty("Snapshot")!.GetValue(session);
                        var records = (System.Collections.IEnumerable)Field("_currentRoomObjectRecords")!;
                        var absent = new List<string>();
                        foreach (ScummRoomObjectRecord record in records)
                        {
                            var verbs = (System.Collections.ICollection)Call("InteractVerbs", record)!;
                            if (verbs.Count == 0 || (record.X, record.Y) == (0, 0) || snapshot is null) continue;
                            if (!(bool)Call("IsVerbHotspotAvailable", record, snapshot)!) absent.Add(record.Id.ToString());
                        }
                        Log($"  absent with verbs: [{string.Join(", ", absent)}]");
                        continue;
                    }
                    // o lists the Interact overview (every game, the room controls' own); c:group,choice runs one of its choices.
                    if (raw == "o" || raw.StartsWith("c:", StringComparison.Ordinal))
                    {
                        var controls = typeof(MainWindow).GetProperty("CurrentRoomControls", Private)!.GetValue(window)!;
                        var overview = (RoomInteractionOverview?)controls.GetType().GetMethod("InteractionOverview")!.Invoke(controls, [false]);
                        if (overview is null) { Log($"{raw}: no Interact overview"); continue; }
                        if (raw == "o")
                        {
                            for (var g = 0; g < overview.Groups.Count; g++)
                                Log($"  [{g}] {overview.Groups[g].Heading}: {string.Join(", ", overview.Groups[g].Choices.Select(c => c.Label))}");
                            continue;
                        }
                        var at = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var choice = overview.Groups[at[0]].Choices[at[1]];
                        Log($"{raw}: {overview.Groups[at[0]].Heading} · {choice.Label}");
                        choice.Run();
                        for (var i = 1; i <= 6; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); if (i == 2) Capture(raw.Replace(':', '-').Replace(',', '-')); }
                        continue;
                    }
                    // r:room opens the room as a pick in the room selector does (a catalog visit).
                    if (raw.StartsWith("r:", StringComparison.Ordinal))
                    {
                        var game = (SCUMMexplorer.Core.Domain.GameLibrary)Field("_game")!;
                        var resource = game.Rooms.First(r => r.ResourceId == int.Parse(raw[2..]));
                        var kind = typeof(MainWindow).GetNestedType("RoomOpenKind", Private)!;
                        Call("SelectRoomWithIntent", resource, Enum.Parse(kind, "Catalog"), true);
                        for (var i = 1; i <= 4; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); }
                        continue;
                    }
                    // i:room,object,verb picks an interaction of the room shown (the Interact menu's choice, RunGenericRoomAction).
                    if (raw.StartsWith("i:", StringComparison.Ordinal))
                    {
                        var parts = raw[2..].Split(',').Select(int.Parse).ToArray();
                        var actions = (System.Collections.IList)Field("_genericSourceActions")!;
                        object? action = null;
                        foreach (var candidate in actions)
                            if ((int)candidate!.GetType().GetProperty("Object")!.GetValue(candidate)! == parts[1] &&
                                (int)candidate.GetType().GetProperty("Verb")!.GetValue(candidate)! == parts[2]) action = candidate;
                        if (action is null) { Log($"{raw}: no such action in the room shown ({Field("_genericActionsRoom")})"); continue; }
                        Log($"{raw}: interact \"{action.GetType().GetProperty("Title")!.GetValue(action)}\"");
                        typeof(MainWindow).GetMethod("RunGenericRoomAction", Private)!.Invoke(window, [action]);
                        for (var i = 1; i <= 6; i++) { await Pump(1.5); State($"{raw} +{i * 1.5:F1}s"); if (i == 2) Capture(raw.Replace(':', '-').Replace(',', '-')); }
                        continue;
                    }
                    var spec = raw;
                    var items = (System.Collections.IList)Call("PlayCutsceneItems", new object?[] { null })!;
                    object? picked = null;
                    foreach (var item in items)
                        if ((string)item!.GetType().GetField("Item1")!.GetValue(item)! == "scumm-" + spec.Replace(',', '-')) picked = item.GetType().GetField("Item2")!.GetValue(item);
                    if (picked is null) { Log($"{spec}: not in the list"); continue; }
                    Log($"{spec}: playing \"{picked.GetType().GetProperty("Title")!.GetValue(picked)}\"");
                    ((Action)picked.GetType().GetProperty("Play")!.GetValue(picked)!).Invoke();
                    for (var i = 1; i <= 8; i++) { await Pump(1.5); State($"{spec} +{i * 1.5:F1}s"); if (i == 3) Capture($"{spec.Replace(',', '-')}"); }
                    var play = (ScummGameSession?)Field("_play");
                    Log($"{spec} setup: {string.Join(" | ", play?.Snapshot.Uncertainties.Where(u => u.Contains("setup") || u.Contains("picked") || u.Contains("started directly")).TakeLast(3) ?? [])}");
                }
            }
            catch (Exception ex) { Log("FAILED: " + ex); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
    }
}

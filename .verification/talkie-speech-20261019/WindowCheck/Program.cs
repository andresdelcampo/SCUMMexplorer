using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// Usage: TalkieSpeechWindowCheck <talkie folder> <output folder> [scene title]...
// S10 talkie speech (19 October 2026): opens the real MainWindow on a talkie (Monkey Island 2's fan talkie, Indy 4) with a private profile
// (SCUMM_EXPLORER_PROFILE_ROOT) and room-list cache (SCUMM_ROOM_CACHE_DIR) in the output folder; opens Room 4 so play starts and lets the game play 25 s;
// then, once the background list is done, plays each named scene from the picker for 12 s. Writes the speech player's log (each spoken line from
// monster.sou), the session's speech events and its mouth-sync events (the talking actor's mouth from the samples' VCTL times) to talkie-check.txt.
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
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private, values.Select(v => v!.GetType()).ToArray())!.Invoke(window, values);
        string Lines(ScummGameSession s) => string.Join(" / ", s.Messages.OnScreen.Select(m => m.Text.Replace("\n", " ")));
        void Save(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            var speech = (TalkieSpeechPlayer)Field("_playSpeech")!;
            void Speech(ScummGameSession session, int speechFrom, int eventsFrom, int mouthFrom)
            {
                report.Add("-- speech player log");
                foreach (var line in speech.Log.Skip(speechFrom)) report.Add("   " + line);
                report.Add("-- session speech events");
                foreach (var e in session.Messages.SpeechEvents.Skip(eventsFrom))
                    report.Add(e.IsStop ? $"   stop at jiffy {e.Jiffies}" : $"   0x{e.Offset:X8}/{e.Length} at jiffy {e.Jiffies}, {e.DurationJiffies} jiffies");
                var mouth = session.Messages.MouthSyncEvents.Skip(mouthFrom).ToList();
                report.Add($"-- mouth-sync events: {mouth.Count}");
                foreach (var e in mouth.Take(40)) report.Add($"   jiffy {e.Jiffies}: actor {e.Actor} mouth {(e.Open ? "open" : "closed")}");
            }
            try
            {
                Call("OpenGameFolder", args[0]);
                var tree = (TreeView)window.FindName("ResourceTree");
                var room4 = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>())
                    .First(i => (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == 4 &&
                                i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room").Tag!;
                Call("SelectResourceInTree", room4);
                for (var n = 0; n < 1800 && (Field("_playTimer") is null || Field("_play") is not ScummGameSession { } started || !speech.Follows(started.Messages)); n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new Exception("play did not start in Room 4");
                report.Add($"Play started in Room {session.CurrentRoomId}; speech bank attached: {speech.Follows(session.Messages)}");
                var (s0, e0, m0) = (speech.Log.Count, session.Messages.SpeechEvents.Count, session.Messages.MouthSyncEvents.Count);
                await Task.Delay(25000);
                report.Add($"25 s of play: room {session.CurrentRoomId}, on screen \"{Lines(session)}\", frame {Field("_playFrame")}");
                Speech(session, s0, e0, m0);
                Save("talkie-01-play.png");
                if (args.Length > 2)
                {
                    for (var n = 0; n < 6000 && !(bool)Field("_playRoomListsDone")!; n++) await Task.Delay(100);
                    await Task.Delay(500);
                    var picker = (Window)typeof(MainWindow).GetMethod("BuildCutscenePicker", Private)!.Invoke(window, null)!;
                    picker.Close();
                    var all = ((System.Collections.IEnumerable)typeof(MainWindow).GetMethod("PlayableCutsceneItems", Private)!.Invoke(window, [null])!).Cast<object>()
                        .Select(t => t.GetType().GetField("Item2")!.GetValue(t)!).ToList();
                    report.Add($"list done: {(bool)Field("_playRoomListsDone")!}; picker entries {all.Count}");
                    var index = 2;
                    foreach (var title in args.Skip(2))
                    {
                        (s0, e0, m0) = (speech.Log.Count, session.Messages.SpeechEvents.Count, session.Messages.MouthSyncEvents.Count);
                        var item = all.FirstOrDefault(i => (string)i.GetType().GetProperty("Title")!.GetValue(i)! == title);
                        if (item is null) { report.Add($"no picker entry \"{title}\""); continue; }
                        ((Action)item.GetType().GetProperty("Play")!.GetValue(item)!).Invoke();
                        await Task.Delay(12000);
                        report.Add($"played \"{title}\": room {session.CurrentRoomId}, in cutscene {session.InCutscene}, on screen \"{Lines(session)}\", talking actor {session.Messages.TalkingActor}, talk message actor {session.Messages.Talk?.Actor}");
                        Speech(session, s0, e0, m0);
                        Save($"talkie-{index++:00}-scene.png");
                    }
                }
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "talkie-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

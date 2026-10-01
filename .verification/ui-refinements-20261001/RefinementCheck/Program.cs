using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]); var app = new Application(); var w = new MainWindow();
        var report = new List<string>(); int failures = 0;
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(w);
        object? Call(string n, params object?[] v) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(w, v);
        T Find<T>(string n) => (T)w.FindName(n);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        void Check(bool ok, string text) { report.Add($"{(ok ? "PASS" : "FAIL")} {text}"); if (!ok) failures++; }
        void Capture(Window window, string name)
        {
            window.UpdateLayout(); var b = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            b.Render(window); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
            using var f = File.Create(Path.Combine(args[1], name + ".png")); enc.Save(f);
        }
        w.ContentRendered += async (_, _) =>
        {
            async Task Wait(Func<bool> c) { for (int i = 0; i < 600 && !c(); i++) await Task.Delay(50); if (!c()) throw new TimeoutException(); }
            async Task Pick(int id)
            {
                Call("SelectResourceInTree", ((GameLibrary)Field("_game")!).Rooms.Single(r => r.ResourceId == id));
                await Wait(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == id && Find<Image>("RoomPreview").IsVisible);
                await Task.Delay(300);
            }
            void Advanced(bool value) { Find<CheckBox>("AdvancedModeToggle").IsChecked = value; Call("AdvancedModeToggle_Click", null, new RoutedEventArgs()); w.UpdateLayout(); }
            try
            {
                Call("OpenGameFolder", args[0]); await Pick(2);
                var status = Find<Button>("GameStatusButton"); var global = Find<Button>("CutsceneMenuButton"); var room = Find<Button>("RoomCutsceneButton");
                Check(status.IsVisible && !global.IsVisible, "normal: top Game Status visible, global Play Cutscene hidden");
                Check(ReferenceEquals(room.Parent, Find<WrapPanel>("PlaybackBar")) && ((Panel)room.Parent).Children.IndexOf(room) == ((Panel)room.Parent).Children.IndexOf(Find<Button>("SkipLineButton")) + 1,
                    "room Play Cutscene immediately follows Skip line");
                Check(room.IsVisible && room.IsEnabled, "normal: multiple-room access remains available"); Capture(w, "normal-placement");
                var session = Session(); Advanced(true);
                var top = (Panel)status.Parent;
                Check(status.IsVisible && global.IsVisible && ReferenceEquals(top, global.Parent) && top.Children.IndexOf(global) == top.Children.IndexOf(status) + 1,
                    "Advanced: Game Status immediately beside global Play Cutscene");
                Check(ReferenceEquals(session, Session()), "Advanced transition retains exploration session");
                w.Width = 900; w.Height = 600; await Task.Delay(300); Capture(w, "advanced-minimum-placement");
                Check(top.Children.OfType<FrameworkElement>().Where(e => e.IsVisible).All(e => { var p = e.TranslatePoint(new Point(), w); return p.X >= 0 && p.X + e.ActualWidth <= w.ActualWidth && p.Y + e.ActualHeight <= w.ActualHeight; }), "minimum width: top controls stay inside window");
                Advanced(false); Check(status.IsVisible && !global.IsVisible && ReferenceEquals(session, Session()), "normal return retains session and control visibility");
                var rooms = ((GameLibrary)Field("_game")!).Rooms.Select(r => r.ResourceId!.Value).ToArray();
                int Count(int id) => ((IEnumerable)Call("RoomCutsceneItems", id)!).Cast<object>().Count();
                await Pick(rooms.First(id => Count(id) == 0)); Check(!room.IsVisible, "zero memberships: room control hidden");
                await Pick(rooms.First(id => Count(id) == 1)); Check(room.IsVisible && room.IsEnabled && (string)room.Content == "Play Cutscene", "one membership: direct room control");
                room.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await Wait(() => Session().BootRunning || Session().InCutscene); Call("PauseScenePlayback");
                Check(!room.IsEnabled && !global.IsEnabled && status.IsEnabled, "paused replay locks starts and retains Game Status inspection");
                await Pick(2); Check(!Find<Button>("PlaybackPauseButton").IsEnabled && room.IsEnabled, "departure restores idle playback controls");
                Capture(w, "normal-minimum-placement");
                var all = ((IEnumerable)Call("PlayableCutsceneItems")!).Cast<object>().ToArray();
                var titles = all.Select(v => { var item = v.GetType().GetField("Item2")!.GetValue(v)!; return (string)item.GetType().GetProperty("Title")!.GetValue(item)!; }).ToArray();
                Check(titles.Length == 334 && titles.All(t => t.StartsWith("Room ") && t.Contains(" · ")), "all 334 starts use shared room/descriptor title format");
                Check(titles.Contains("Room 01 · Opening") && titles.Contains("Room 08 · The Elders' trial") && titles.Contains("Room 10 · Walk to Room 13") && titles.Contains("Room 36 · The firewood scene"), "original four have uniform scene names");
                var gallery = ((IDictionary)Call("GalleryCutscenes")!);
                Check(new[] { 44, 46, 52, 86 }.All(id => titles.Contains((string)gallery[id]!.GetType().GetProperty("Title")!.GetValue(gallery[id])!)), "Game Texts uses the same original scene titles");
                var boot = new LoomExplorationSession((GameLibrary)Field("_game")!, (LoomIndexedObjectSnapshot)Field("_indexedObjectSnapshot")!, stopAtOpening: true);
                var before = boot.Snapshot.RoomId;
                var observed = new List<int>();
                for (int i = 0; i < 80; i++) { boot.AdvanceFrame(); if (boot.Snapshot.RoomId is int id && id != before) observed.Add(id); }
                Check(before == 69 && observed.FirstOrDefault() == 1, "opening boot setup stops in69; first actual scene room is01");
            }
            catch (Exception ex) { report.Add(ex.ToString()); failures++; }
            finally
            {
                report.Add("Synthetic visible WPF controls, normal clock, no physical input/listening claim."); report.Add($"Failures: {failures}");
                File.WriteAllLines(Path.Combine(args[1], "refinement-check.txt"), report); w.Close(); app.Shutdown(); Environment.ExitCode = failures == 0 ? 0 : 1;
            }
        };
        app.Run(w);
    }
}

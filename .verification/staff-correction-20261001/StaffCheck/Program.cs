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
            GameStatusWindow? statusWindow = null;
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
                await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                object? StatusCall(string name) => typeof(GameStatusWindow).GetMethod(name, Private)!.Invoke(statusWindow, [statusWindow, new RoutedEventArgs()]);
                T S<T>(string name) => (T)statusWindow!.FindName(name);
                void Note(int count) => ((Button)S<Canvas>("NoteButtons").Children[count - 1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                void Staff() => S<Button>("StaffButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                var grid = S<DataGrid>("DraftGrid"); var row = (GameStatusWindow.DraftRow)grid.Items[0];
                Check(S<Button>("StaffButton").IsEnabled && !grid.IsReadOnly && grid.Items.Count == 16, "status opens ready to edit sixteen source drafts including Transcend");
                Check(((Panel)S<Button>("ApplyButton").Parent).Children.OfType<Button>().Select(b => (string)b.Content).SequenceEqual(new[] { "Apply Changes", "Close" }), "exactly Apply Changes and Close footer buttons");
                Check(!S<Expander>("SourceExpander").IsVisible && grid.Columns.Single(c => (string)c.Header == "State source").Visibility == Visibility.Collapsed, "normal status hides sources/provenance");
                bool Nonblank(Image image) { if (image.Source is not BitmapSource b) return false; var pixels = new byte[b.PixelWidth * b.PixelHeight * 4]; b.CopyPixels(pixels, b.PixelWidth * 4, 0); return pixels.Where((_, i) => i % 4 == 3).Count(p => p > 0) > 2; }
                Check(Nonblank(S<Image>("StaffImage")) && S<Canvas>("NoteButtons").Children.OfType<Button>().All(b => Nonblank((Image)((Panel)b.Content).Children[0])), "decoded native staff and all eight note images are nonblank");
                report.Add("Artwork diagnostics: " + typeof(GameStatusWindow).GetField("_artworkError", Private)!.GetValue(statusWindow));
                Capture(statusWindow, "status-normal");
                var initial = Session().GameStatus; var originalRow = grid.Items[0]; var originalStaffImage = S<Image>("StaffImage").Source;
                Staff(); Note(8);
                grid.CurrentCell = new DataGridCellInfo(row, grid.Columns[1]); grid.BeginEdit(); grid.UpdateLayout();
                var combo = (ComboBox)grid.Columns[1].GetCellContent(row);
                combo.SelectedValue = 8; grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
                Check(row.Note1 == 8, "actual sequence combo converts upper C selection to stored ID8");
                var upperButton = (Button)S<Canvas>("NoteButtons").Children[7];
                Check(upperButton.Focusable && upperButton.IsTabStop && System.Windows.Automation.AutomationProperties.GetName(upperButton).Contains("C′"), "graphical note selector exposes keyboard focus and an accessible upper C label");
                Check(Session().GameStatus.StaffHeld == initial.StaffHeld && Session().GameStatus.AvailableNoteCount == initial.AvailableNoteCount && S<Button>("ApplyButton").IsEnabled, "selector and sequence choices stay pending until Apply");
                Check(!ReferenceEquals(originalStaffImage, S<Image>("StaffImage").Source) && S<TextBlock>("NoteIdentities").Text.EndsWith("C′"), "staff and contiguous C through upper C preview immediately");
                Staff(); Check(S<TextBlock>("NoteIdentities").Text == "Staff required" && !S<Canvas>("NoteButtons").IsEnabled, "staff removal disables note choices without changing stored level"); Staff();
                Check(S<Canvas>("NoteButtons").Children.Count == 8 && S<Canvas>("NoteButtons").Children.OfType<Button>().Select(b => (int)b.Tag).SequenceEqual(Enumerable.Range(1,8)), "eight correctly numbered choices with no None");
                Note(1); StatusCall("Apply_Click");
                Check(Session().GameStatus.AvailableNoteCount == 1 && Session().Snapshot.KnownGlobalVariables["var[0x07A]"] == 1, "C selection writes native inclusive level1");
                Note(8);
                Note(8); row.Note1 = initial.Drafts[0].Notes[0]; row.Learned = false;
                var noteColumn = (DataGridComboBoxColumn)grid.Columns[1];
                Check(noteColumn.SelectedValuePath == "Id" && noteColumn.DisplayMemberPath == "Label" && ((IEnumerable)noteColumn.ItemsSource).Cast<object>().Last().GetType().GetProperty("Label")!.GetValue(((IEnumerable)noteColumn.ItemsSource).Cast<object>().Last())!.Equals("C′"), "sequence editors display letters and retain numeric IDs");
                StatusCall("Apply_Click");
                Check(statusWindow.IsVisible && Session().GameStatus.StaffHeld == true && Session().GameStatus.AvailableNoteCount == 8 && Session().GameStatus.Drafts[0].Learned == false, "Apply stays open and updates staff/eight notes/Learned");
                Check(Session().GameStatus.Drafts[0].Reversible == initial.Drafts[0].Reversible && Session().GameStatus.Drafts[0].ForwardVerb == initial.Drafts[0].ForwardVerb, "Learned edit preserves reverse capability and response");
                Check(grid.Columns.Any(c => (string)c.Header == "Learned" && !c.IsReadOnly) && grid.Columns.Any(c => (string)c.Header == "Can reverse" && c.IsReadOnly), "Learned editable and Can reverse separately read-only");
                Check(row.Title == "Opening / Closing" && ((GameStatusWindow.DraftRow)grid.Items[2]).Title == "Dyeing / Bleaching" && ((GameStatusWindow.DraftRow)grid.Items[15]).Title == "Transcend" && ((GameStatusWindow.DraftRow)grid.Items[15]).Notes.SequenceEqual(new[] {8,4,5,1}), "verified display names include native Transcend C-prime F G C");
                await Task.Delay(300); Check(ReferenceEquals(originalRow, grid.Items[0]), "live refresh retains draft row identity");
                Capture(statusWindow, "status-applied");
                row.Learned = true; StatusCall("Apply_Click");
                Check(Session().GameStatus.Drafts[0].Learned == true, "Learned can be explicitly restored through Apply");
                Note(3); StatusCall("Close_Click"); statusWindow = null;
                Check(Session().GameStatus.AvailableNoteCount == 8, "Close discards unapplied note selection");
                Advanced(true); statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                grid = S<DataGrid>("DraftGrid");
                Check(S<Expander>("SourceExpander").IsVisible && grid.Columns.Single(c => (string)c.Header == "State source").Visibility == Visibility.Visible && S<TextBlock>("StaffOrigin").Text.Contains("Explicit user"), "Advanced reveals sources and explicit-edit provenance");
                statusWindow.Width = 820; statusWindow.Height = 520; await Task.Delay(250); Capture(statusWindow, "status-advanced-minimum");
                Check(grid.ActualHeight > 100 && S<Button>("CloseButton").TranslatePoint(new Point(), statusWindow).Y + S<Button>("CloseButton").ActualHeight < statusWindow.ActualHeight, "minimum status retains usable grid and visible footer");
                S<Expander>("SourceExpander").IsExpanded = true; await Task.Delay(250); Capture(statusWindow, "status-advanced-minimum-sources");
                Check(grid.ActualHeight > 90 && S<Button>("CloseButton").TranslatePoint(new Point(), statusWindow).Y + S<Button>("CloseButton").ActualHeight < statusWindow.ActualHeight, "expanded sources at minimum size retain grid and footer");
                S<Expander>("SourceExpander").IsExpanded = false;
                Session().UpdateAvailableNotes(4); await Task.Delay(350);
                Check(S<TextBlock>("NoteIdentities").Text == "Available: C, D, E, F", "clean status tracks live session changes"); Capture(statusWindow, "status-four-notes");
                Note(8); Session().UpdateAvailableNotes(3); StatusCall("Apply_Click");
                Check(Session().GameStatus.AvailableNoteCount == 3 && !S<Button>("ApplyButton").IsEnabled && S<TextBlock>("EditMessage").Text.Contains("changed"), "stale baseline blocks Apply without overwriting game changes");
                Call("PlayOpening"); await Task.Delay(300);
                Check(!S<Button>("StaffButton").IsEnabled && S<DataGrid>("DraftGrid").IsReadOnly && S<TextBlock>("EditMessage").Text.Contains("Finish"), "session replacement discards pending changes and boot locks editing");
                Call("PauseScenePlayback"); var paused = Session().GameStatus; Staff(); Note(8); StatusCall("Apply_Click");
                Check(Session().GameStatus.StaffHeld == paused.StaffHeld && Session().GameStatus.AvailableNoteCount == paused.AvailableNoteCount && !S<Button>("ApplyButton").IsEnabled, "paused handlers refuse changes");
                statusWindow.Close(); statusWindow = null; await Pick(2); Advanced(false);
                await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                Staff(); Note(8); ((GameStatusWindow.DraftRow)S<DataGrid>("DraftGrid").Items[0]).Learned = false;
                StatusCall("Apply_Click"); statusWindow.Close(); statusWindow = null;
                async Task<int> Cast()
                {
                    if (Session().Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x067]") != 156)
                    { Call("HandleRoomPoint", 700, 40, false); await Wait(() => Field("_playback") is null); }
                    int from = Session().SessionTrace.Count;
                    foreach (int id in Session().GameStatus.Drafts[0].Notes)
                        Call("SendInputToGame", (Func<LoomObjectActionResult>)(() => { var n = Session().VerbArea().Items.Single(i => i.VerbId == id); return Session().ClickVerbArea(n.X + 1, n.Y + 1); }), "Native status cast", null);
                    return from;
                }
                int castFrom = await Cast();
                await Wait(() => Session().SessionTrace.Skip(castFrom).Any(l => l.Contains("Script 22 chains to script 26")));
                await Wait(() => Field("_playback") is null);
                await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(!Session().SessionTrace.Skip(castFrom).Any(l => l.Contains("cutscene at Script 49")), "unlearned status choice reaches native refusal without target dispatch on the WPF clock");
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                ((GameStatusWindow.DraftRow)S<DataGrid>("DraftGrid").Items[0]).Learned = true; StatusCall("Apply_Click"); statusWindow.Close(); statusWindow = null;
                Check(Session().GameStatus.Drafts[0].Learned == true, "status Apply restores learned bit before subsequent input");
                castFrom = await Cast(); await Wait(() => Session().SessionTrace.Skip(castFrom).Any(l => l.Contains("cutscene at Script 49")));
                Check(Session().SessionTrace.Skip(castFrom).Any(l => l.Contains("startObject runs object 156 verb 0x3A")), "Learned restored through Apply lets native notes reach the actual target response on the WPF clock");
                Call("PauseScenePlayback"); Capture(w, "status-native-cast");
                await Pick(26); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.AvailableNoteCount == 8, "catalog departure preserves higher explicit eight-note choice");
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                var transcendRow = (GameStatusWindow.DraftRow)S<DataGrid>("DraftGrid").Items[15];
                S<DataGrid>("DraftGrid").ScrollIntoView(transcendRow); await Task.Delay(200); Capture(statusWindow, "status-transcend");
                Note(1); StatusCall("Apply_Click"); StatusCall("Close_Click"); statusWindow=null;
                await Pick(47); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.StaffHeld == true && Session().GameStatus.AvailableNoteCount >= 3, "native held-staff entry raises low browsing choice to the verified initial three-note level");
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                Note(8); StatusCall("Apply_Click"); StatusCall("Close_Click"); statusWindow=null;
                await Pick(26); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.AvailableNoteCount == 8, "later room selection never reduces higher user level");
            }
            catch (Exception ex) { report.Add(ex.ToString()); if (Field("_session") is LoomExplorationSession s) report.AddRange(s.SessionTrace.TakeLast(25)); failures++; }
            finally
            {
                statusWindow?.Close();
                report.Add("Synthetic visible WPF controls, normal clock, no physical input/listening claim."); report.Add($"Failures: {failures}");
                File.WriteAllLines(Path.Combine(args[1], "staff-check.txt"), report); w.Close(); app.Shutdown(); Environment.ExitCode = failures == 0 ? 0 : 1;
            }
        };
        app.Run(w);
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(args[1], "cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 950, ShowActivated = false, Left = -2000 };
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] a) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, a);
        var report = new List<string>();
        void Check(bool ok, string detail) { report.Add((ok ? "PASS " : "FAIL ") + detail); }
        IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
        {
            if (node is T found) yield return found;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
        }
        void Save(FrameworkElement element, string name)
        {
            element.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var file = File.Create(Path.Combine(args[1], name + ".png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                foreach (var edition in Environment.GetEnvironmentVariable("PARITY_EDITION") is { } pickedEdition
                    ? new[] { pickedEdition } : new[] { "loom ega mt32 en", "loom vga en" })
                {
                    var cd = edition.Contains("vga"); var prefix = cd ? "vga" : "ega";
                    Call("OpenGameFolder", Path.Combine(args[0], edition));
                    ((ToggleButton)window.FindName("AdvancedModeToggle")).IsChecked = true;
                    Call("AdvancedModeToggle_Click", window.FindName("AdvancedModeToggle"), new RoutedEventArgs());
                    var game = (GameLibrary)Field("_game")!;
                    if (Environment.GetEnvironmentVariable("READABLE_ONLY") == "1")
                    {
                        Call("SelectResourceInTree", game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == 28 && r.ScriptRoom is null));
                        ((CheckBox)window.FindName("ScriptReadableToggle")).IsChecked = true;
                        for (int i = 0; i < 60 && !((TextBox)window.FindName("ScriptOutput")).Text.Contains("clear the draft buffer"); i++) await Task.Delay(50);
                        var text = ((TextBox)window.FindName("ScriptOutput")).Text;
                        Check(text.Contains("Readable view:") && text.Contains("distaff in use (2 when held)") && text.Contains("difficulty (0 practice, 1 standard, 2 expert)") && text.Contains("wrong tries = 0"), prefix + " Script 28 readable symbols match their meanings");
                        Check(!cd || text.Contains("fill draft buffer with 4 values"), prefix + " named CD draft buffer");
                        Save(window, prefix + "-script28");
                        ((CheckBox)window.FindName("ScriptReadableToggle")).IsChecked = false;
                        Check(((TextBox)window.FindName("ScriptOutput")).Text.Contains(cd ? "var[0x096]" : "var[0x064]"), prefix + " Raw retains original variable addresses");
                        ((CheckBox)window.FindName("ScriptReadableToggle")).IsChecked = true;
                        var title = (string)typeof(MainWindow).GetField(cd ? "CdIntroTitle" : "OpeningTitle", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
                        Check(title == "The Intro", prefix + " introductory sequence is The Intro");
                        continue;
                    }
                    async Task Visit(int number)
                    {
                        Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == number));
                        for (var f = 0; f < 100 && ((GameResource?)Field("_currentRoomResource"))?.ResourceId != number; f++) await Task.Delay(50);
                        var session = (LoomExplorationSession)Field("_session")!;
                        for (var f = 0; f < 600 && (!session.GameStatus.CanEdit || f < 40); f++) session.AdvanceFrame();
                        Call("ApplyRoomState", game.Rooms.Single(r => r.ResourceId == number), Field("_currentRoomObjectRecords"), Field("_objects"), false);
                        Call("RefreshRoomPreview"); Call("UpdateActorLayer"); Call("UpdateMessageLayer"); Call("UpdateVerbArea");
                        window.UpdateLayout();
                    }
                    await Visit(3);
                    var session = (LoomExplorationSession)Field("_session")!;
                    session.UpdateStaffPossession(true);
                    await Visit(5);
                    for (var f = 0; f < 40; f++) session.AdvanceFrame();
                    Call("RefreshRoomPreview"); Call("UpdateActorLayer"); Call("UpdateMessageLayer"); Call("UpdateVerbArea");
                    Check(session.GameStatus.StaffHeld == true && session.VerbArea().Items.Count(v => v.VerbId is >= 1 and <= 8) == 8, prefix + " staff survives 03 to 05");
                    Save(window, prefix + "-staff05");
                    var statusWindow = new GameStatusWindow(() => session, () => session.GameStatus.CanEdit,
                        () => { }, game, () => true) { Owner = window, Resources = window.Resources };
                    statusWindow.Show();
                    ((Button)statusWindow.FindName("StaffButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Save(statusWindow, prefix + "-status");
                    ((Button)statusWindow.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(!statusWindow.IsVisible && session.GameStatus.StaffHeld == false, prefix + " Apply and close takes one click and applies");
                    var cancelWindow = new GameStatusWindow(() => session, () => session.GameStatus.CanEdit,
                        () => { }, game) { Owner = window, Resources = window.Resources };
                    cancelWindow.Show();
                    ((Button)cancelWindow.FindName("StaffButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ((Button)cancelWindow.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(!cancelWindow.IsVisible && session.GameStatus.StaffHeld == false, prefix + " Cancel takes one click and discards");
                    session.UpdateStaffPossession(true);
                    await Visit(69);
                    Check(session.GameStatus.StaffHeld == true && !session.VerbArea().Items.Any(v => v.VerbId is >= 1 and <= 8 or >= 0x31 and <= 0x35), prefix + " menu hides staff and keeps possession");
                    Save(window, prefix + "-menu69");
                    await Visit(2);
                    var buttons = ((StackPanel)window.FindName("RoomEntryEventButtons")).Children.OfType<Button>().ToList();
                    Check(buttons.Any(b => b.Content.ToString()!.Contains("Script", StringComparison.OrdinalIgnoreCase)), prefix + " Room 02 entry script links: " + buttons.Count);
                    ((Expander)window.FindName("RoomEntryTraceExpander")).IsExpanded = true;
                    Save(window, prefix + "-entry02");
                    Call("UpdateRoomMusicActions");
                    for (int i = 0; i < 60 && !cd && ((System.Collections.ICollection)Field("_roomSoundChoices")!).Count == 0; i++) await Task.Delay(50);
                    Call("RoomSounds_Click", window.FindName("RoomSoundsButton"), new RoutedEventArgs());
                    await Task.Delay(50);
                    var popupRoot = PresentationSource.CurrentSources.Cast<PresentationSource>().Select(p => p.RootVisual)
                        .OfType<FrameworkElement>().FirstOrDefault(root => Descendants<TextBlock>(root).Any(t => t.Text == "SOUNDS IN THIS ROOM") &&
                            Descendants<TextBlock>(root).Any(t => t.Text.StartsWith(cd ? "▶ CD cue " : "▶ Sound ")));
                    var soundRows = popupRoot is null ? [] : Descendants<TextBlock>(popupRoot).Select(t => t.Text).Where(t => t.StartsWith("▶")).ToArray();
                    Check(soundRows.Length > 0 && soundRows.All(r => r.Split(" · ").Length == 4 && !r.Contains('→') && r.StartsWith(cd ? "▶ CD cue " : "▶ Sound ")), prefix + " Advanced sound rows share resource / kind / source / duration: " + string.Join("; ", soundRows.Take(2)));
                    if (popupRoot is not null) Save(popupRoot, prefix + "-sounds");
                    // Close the popup by moving focus back to the main window before the next view.
                    window.Activate();
                    var formatted = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomReplayTitle")!.GetMethod("Format")!.Invoke(null, [2, "Room 02 · Use response · Reached Room 03"]);
                    Check((string)formatted! == "Room 02 · Reached Room 03", prefix + " concise doorway title");
                    Check((string)Call("PickerVerbTitle", formatted, 0x38)! == "Room 02 · Reached Room 03 (verb 0x38)", prefix + " advanced verb title");
                    if (cd)
                    {
                        var trigger = new LoomCdCutscene(2, 78, "", 0x38, [], 0, 1, 0, "");
                        var title = (string)Call("CdCutsceneTitle", trigger)!;
                        Check(title == "Room 02 · Reached Room 03 (verb 0x38)", "VGA doorway destination from object 78's own code: " + title);
                        var entry = session.LastVisitEvents.First(ev => ev.TargetKind == ResourceKind.Script && ev.TargetId == 17);
                        var target = (GameResource)Call("FindEventTarget", entry)!;
                        Call("SelectResourceInTree", target); await Task.Delay(100);
                        var scriptText = ((TextBox)window.FindName("ScriptOutput")).Text;
                        Check(scriptText.Contains("Readable view:") && scriptText.Contains("give the ego a costume (by size)") && scriptText.Contains("DISK01.LEC") && !scriptText.Contains("00.LFL"), "VGA entry link opens readable v4 script with verified title and correct container");
                        ((CheckBox)window.FindName("ScriptReadableToggle")).IsChecked = false;
                        Check(((TextBox)window.FindName("ScriptOutput")).Text.Contains("actorOps"), "VGA Raw toggle still shows decoded instructions");
                        ((CheckBox)window.FindName("ScriptReadableToggle")).IsChecked = true;
                        Save(window, "vga-script17");
                        var intro = Call("CdIntroItem")!;
                        Check((string)intro.GetType().GetProperty("Title")!.GetValue(intro)! == "The Intro", "VGA intro title");
                    }
                    else
                    {
                        await Visit(68);
                        var batches = ScummSceneDrawBatches.Build(game);
                        var resource = game.Rooms.Single(r => r.ResourceId == 68);
                        var thumb = (BitmapSource)typeof(MainWindow).GetMethod("RenderRoomCardThumbnail", BindingFlags.Static | BindingFlags.NonPublic)!
                            .Invoke(null, [game, resource, Field("_indexedObjectSnapshot"), Field("_indexedStates"), batches, CancellationToken.None])!;
                        Check(thumb.Palette!.Colors[9] == thumb.Palette.Colors[11] && thumb.Palette.Colors[15] == thumb.Palette.Colors[11], "EGA Room 68 thumbnail uses script palette");
                        Call("SetRoomCardThumbnail", resource, thumb);
                        Call("UpdateCurrentRoomCardThumbnail");
                        var card = ((Panel)window.FindName("RoomCardList")).Children.OfType<Button>().Single(b => Equals(b.Tag, resource));
                        var actualThumb = (BitmapSource)((Image)card.Content).Source;
                        Check(actualThumb.Palette!.Colors[9] == actualThumb.Palette.Colors[11] && actualThumb.Palette.Colors[15] == actualThumb.Palette.Colors[11], "EGA Room 68 selected card retains palette after background and live updates");
                        Save(window, "ega-room68");
                        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(thumb)); using var fs = File.Create(Path.Combine(args[1], "ega-thumb68.png")); enc.Save(fs);
                        var scenes = (System.Collections.IEnumerable)Call("RoomCutsceneItems", 3)!;
                        var titles = scenes.Cast<object>().Select(s => (string)s.GetType().GetProperty("Title")!.GetValue(s)!).ToArray();
                        Check(titles.Any(t => t.Contains("played from Room 02")), "EGA Room 03 list identifies scenes played from Room 02");
                    }
                }
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally
            {
                File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
                Environment.ExitCode = report.Any(r => r.StartsWith("FAIL")) ? 1 : 0;
                window.Close(); app.Shutdown(Environment.ExitCode);
            }
        };
        app.Run(window);
    }
}

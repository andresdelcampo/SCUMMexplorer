using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(args[1], "cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 950 };
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] a) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, a);
        var report = new List<string>();
        void Check(bool ok, string detail) { report.Add((ok ? "PASS " : "FAIL ") + detail); }
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
                    await Visit(69);
                    Check(session.GameStatus.StaffHeld == true && !session.VerbArea().Items.Any(v => v.VerbId is >= 1 and <= 8 or >= 0x31 and <= 0x35), prefix + " menu hides staff and keeps possession");
                    Save(window, prefix + "-menu69");
                    await Visit(2);
                    var buttons = ((StackPanel)window.FindName("RoomEntryEventButtons")).Children.OfType<Button>().ToList();
                    Check(buttons.Any(b => b.Content.ToString()!.Contains("Script", StringComparison.OrdinalIgnoreCase)), prefix + " Room 02 entry script links: " + buttons.Count);
                    ((Expander)window.FindName("RoomEntryTraceExpander")).IsExpanded = true;
                    Save(window, prefix + "-entry02");
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
                        Check(scriptText.Contains("actorOps") || scriptText.Contains("costume"), "VGA entry link opens decoded v4 script: " + scriptText[..Math.Min(scriptText.Length, 600)]);
                        Save(window, "vga-script17");
                    }
                    else
                    {
                        await Visit(68);
                        var batches = LoomSceneDrawBatches.Build(game);
                        var resource = game.Rooms.Single(r => r.ResourceId == 68);
                        var thumb = (BitmapSource)typeof(MainWindow).GetMethod("RenderRoomCardThumbnail", BindingFlags.Static | BindingFlags.NonPublic)!
                            .Invoke(null, [game, resource, Field("_indexedObjectSnapshot"), Field("_indexedStates"), batches, CancellationToken.None])!;
                        Check(thumb.Palette!.Colors[9] == thumb.Palette.Colors[11] && thumb.Palette.Colors[15] == thumb.Palette.Colors[11], "EGA Room 68 thumbnail uses script palette");
                        Save(window, "ega-room68");
                        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(thumb)); using var fs = File.Create(Path.Combine(args[1], "ega-thumb68.png")); enc.Save(fs);
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

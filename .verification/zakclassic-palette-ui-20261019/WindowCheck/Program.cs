using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// ZakPaletteWindowCheck <fixture> <private output> <zak|other> <initial room>
// Only serial execution under the parent's exclusive WPF lease. No game-state edits or original interpreter.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var report = new List<string>();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Save(string name, FrameworkElement? target = null)
        {
            target ??= window; target.UpdateLayout();
            Require(target.ActualWidth > 0 && target.ActualHeight > 0, "Capture target has no layout");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth), (int)Math.Ceiling(target.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        void Log(string text)
        {
            report.Add(text);
            File.WriteAllLines(Path.Combine(output, "progress.txt"), report);
        }
        window.ContentRendered += async (_, _) =>
        {
            DispatcherTimer? heartbeat = null;
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(500);
                var game = (GameLibrary?)Field("_game") ?? throw new InvalidOperationException("No game");
                GameResource Resource(ResourceKind kind, int id) => game.Resources.Single(r => r.Kind == kind && r.ResourceId == id);
                Call("SelectResourceInTree", Resource(ResourceKind.Room, int.Parse(args[3])));
                for (var n = 0; n < 1800 && Field("_playTimer") is null; n++) await Task.Delay(100);
                var session = (ScummGameSession?)Field("_play") ?? throw new InvalidOperationException("Native Play did not start");
                var playTimer = (DispatcherTimer?)Field("_playTimer") ?? throw new InvalidOperationException("Native Play timer did not start");
                await Task.Delay(250);
                playTimer.Stop();
                await Task.Delay(100);
                var frame = (int)Field("_playFrame")!;
                string State() => JsonSerializer.Serialize(new
                {
                    session.Snapshot, session.Actors, session.CurrentRoomId, session.ViewLeft,
                    Messages = session.Messages.OnScreen, session.DeliveredInput
                });
                var before = State();
                Log($"Native Play stopped at frame {frame}; beginning display-only colour checks");
                void Neutral()
                {
                    Require(ReferenceEquals(Field("_play"), session), "Colour inspection replaced native Play");
                    Require((int)Field("_playFrame")! == frame && State() == before, "Colour inspection changed the stopped native session");
                    Require(Field("_playTimer") is not DispatcherTimer running || !running.IsEnabled, "Colour inspection restarted native frames");
                }
                var gallery = (WrapPanel)window.FindName("CostumePoseGallery");
                var panel = (FrameworkElement)window.FindName("CostumePanel");
                var advanced = (ToggleButton)window.FindName("AdvancedModeToggle");
                string[] VisibleText(DependencyObject root)
                {
                    var result = new List<string>();
                    void Visit(DependencyObject item)
                    {
                        if (item is TextBlock { IsVisible: true } text && !string.IsNullOrWhiteSpace(text.Text)) result.Add(text.Text);
                        for (var n = 0; n < VisualTreeHelper.GetChildrenCount(item); n++) Visit(VisualTreeHelper.GetChild(item, n));
                    }
                    Visit(root); return result.ToArray();
                }
                void PlainLabels(DependencyObject root)
                {
                    var text = VisibleText(root);
                    Require(text.Length > 0, "No actual colour choice text is displayed");
                    Require(text.All(t => !t.Contains("CostumeColorChoice", StringComparison.Ordinal) &&
                        !t.Contains("ScummV1", StringComparison.Ordinal) && !t.Contains("Evidence =", StringComparison.Ordinal) &&
                        !t.Contains("estimate", StringComparison.OrdinalIgnoreCase)), "Technical object/source details leak into the Normal colour choice");
                }
                string Images()
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var images = gallery.Children.OfType<StackPanel>().SelectMany(c => c.Children.OfType<Image>()).ToArray();
                    Require(images.Length > 0, "No costume pose is drawn");
                    foreach (var image in images)
                    {
                        Require(image.Source is BitmapSource, "A stored pose has no decoded bitmap");
                        var bitmap = new FormatConvertedBitmap((BitmapSource)image.Source, PixelFormats.Bgra32, null, 0);
                        Require(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0, "A stored pose is empty");
                        var pixels = new byte[bitmap.PixelHeight * bitmap.PixelWidth * 4];
                        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                        hash.AppendData(BitConverter.GetBytes(bitmap.PixelWidth));
                        hash.AppendData(BitConverter.GetBytes(bitmap.PixelHeight));
                        hash.AppendData(pixels);
                    }
                    return Convert.ToHexString(hash.GetHashAndReset());
                }
                string ExpectedImages(int id, ScummV1CostumeColor? color)
                {
                    var resource = Resource(ResourceKind.Costume, id);
                    var bytes = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
                    var offset = resource.Offset ?? throw new InvalidDataException("No costume offset");
                    var summary = new ScummV3CostumeReader().Decode(bytes, offset);
                    var renderer = new ScummCostumeRenderer();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var count = 0;
                    foreach (var slot in summary.AnimationSlots.Where(s => s.HasData))
                    {
                        var pose = renderer.DecodePose(bytes, offset, slot.Index, game.V1Game, color);
                        if (pose is null) continue;
                        var indices = pose.Pixels.Span;
                        var pixels = new byte[indices.Length * 4];
                        for (var n = 0; n < indices.Length; n++)
                        {
                            var rgba = pose.Palette[indices[n]];
                            pixels[n * 4] = (byte)rgba; pixels[n * 4 + 1] = (byte)(rgba >> 8);
                            pixels[n * 4 + 2] = (byte)(rgba >> 16); pixels[n * 4 + 3] = (byte)(rgba >> 24);
                        }
                        hash.AppendData(BitConverter.GetBytes(pose.Width));
                        hash.AppendData(BitConverter.GetBytes(pose.Height)); hash.AppendData(pixels); count++;
                    }
                    Require(count == gallery.Children.Count && count > 0, "Window omitted a native decodable stored pose");
                    return Convert.ToHexString(hash.GetHashAndReset());
                }
                void Select(int id) => Call("SelectResourceInTree", Resource(ResourceKind.Costume, id));
                if (args[2] != "zak")
                {
                    var classicManiac = game.V1Game == ScummV1Game.ManiacMansion;
                    var id = classicManiac ? 3 : 13;
                    Select(id); await Task.Delay(200);
                    var pixels = Images();
                    var originalColor = ScummV1ActorColors.For(game)?.ColorOf(id);
                    if (classicManiac) Require(originalColor?.Suit == 6, "Dave's native suit 6 changed");
                    Require(pixels == ExpectedImages(id, originalColor), "Window dropped the family's native palette or stored poses");
                    Require(Field("_costumeColorHeader") is not FrameworkElement header || !header.IsVisible,
                        "Zak colour selector is offered for another family");
                    advanced.IsChecked = true; Call("UpdateAdvancedMode"); await Task.Delay(100);
                    Require(Field("_costumeColorHeader") is not FrameworkElement advancedHeader || !advancedHeader.IsVisible,
                        "Zak colour selector leaked to another family in Advanced");
                    Require(Images() == pixels, "Advanced changed this family's native costume palette");
                    Neutral(); Save("other-family-native-palette", panel);
                    Log($"Other-family selector hidden; costume{id} {gallery.Children.Count} native poses/palette match Core; frame={frame}");
                }
                else
                {
                    var pulses = 0; var maxGap = TimeSpan.Zero;
                    var elapsed = Stopwatch.StartNew(); var lastPulse = elapsed.Elapsed;
                    heartbeat = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
                    heartbeat.Tick += (_, _) =>
                    {
                        var now = elapsed.Elapsed; var gap = now - lastPulse;
                        if (gap > maxGap) maxGap = gap;
                        lastPulse = now; pulses++;
                    };
                    heartbeat.Start();
                    var cold = Stopwatch.StartNew();
                    Select(13);
                    Require(Field("_costumeColorsLoading") is true, "Cold colour contexts did not start asynchronously");
                    Save("cold-colours-progress");
                    await Task.Delay(30);
                    Select(32); // Cancel one pending selection, then inspect only the latest result.
                    Log("Cold costume 13 load cancelled; latest selection is costume 32");
                    async Task Ready(int expected)
                    {
                        var wait = Stopwatch.StartNew();
                        while (Field("_costumeColorsLoading") is true && wait.Elapsed < TimeSpan.FromMinutes(3))
                        { await Task.Delay(50); Neutral(); }
                        Require(Field("_costumeColorsLoading") is false, "Colour derivation exceeded its bound");
                        var picker = (ComboBox?)Field("_costumeColorPicker") ?? throw new InvalidOperationException("No colour picker");
                        Require(picker.Items.Count > 1, "No alternate source colour is offered");
                        foreach (var choice in picker.Items)
                        {
                            var color = (ScummV1CostumeColor)choice.GetType().GetProperty("Color")!.GetValue(choice)!;
                            Require(color.Costume == expected, "Cancelled costume results replaced the latest selection");
                        }
                        Require(((GameResource?)Field("_selectedResource"))?.ResourceId == expected, "Colour results changed selected costume");
                        Neutral();
                    }
                    await Ready(32);
                    Log($"Cold/cancelled load {cold.Elapsed.TotalSeconds:F2}s; UI heartbeat={pulses}, largest gap={maxGap.TotalMilliseconds:F0}ms");
                    Require(pulses > 0 && maxGap < TimeSpan.FromSeconds(3), "UI did not remain responsive during colour derivation");
                    async Task Check(int id, int originalSuit, int alternateSuit)
                    {
                        Select(id); await Ready(id);
                        await Task.Delay(50);
                        var picker = (ComboBox)Field("_costumeColorPicker")!;
                        var evidence = (TextBlock)Field("_costumeColorEvidence")!;
                        ScummV1CostumeColor Color(object choice) => (ScummV1CostumeColor)choice.GetType().GetProperty("Color")!.GetValue(choice)!;
                        Require(picker.SelectedIndex == 0 && Color(picker.Items[0]).Suit == originalSuit, "Default colours changed");
                        PlainLabels(picker);
                        Require(!evidence.IsVisible && picker.ToolTip is null, "Source/estimate details leaked into Normal");
                        Require(picker.Items.Cast<object>().All(c => !c.GetType().GetProperty("Label")!.GetValue(c)!.ToString()!.Contains("estimate", StringComparison.OrdinalIgnoreCase)),
                            "Technical estimate label leaked into Normal picker");
                        var count = gallery.Children.Count; var original = Images();
                        Require(original == ExpectedImages(id, Color(picker.Items[0])), "Default window pixels differ from native decoder");
                        Save($"costume-{id}-default-normal");
                        Save($"costume-{id}-all-default-poses", gallery);
                        var alternate = picker.Items.Cast<object>().First(c => Color(c).Suit == alternateSuit);
                        var expectedAlternate = ExpectedImages(id, Color(alternate));
                        picker.SelectedItem = alternate;
                        await Task.Delay(80);
                        Require(gallery.Children.Count == count && Images() == expectedAlternate, "Alternate colours do not match native stored poses/pixels");
                        var changed = expectedAlternate != original;
                        if (id == 13) Require(changed && Images() != original, "Costume 13's used suit channel did not visibly change");
                        PlainLabels(picker);
                        Neutral(); Save($"costume-{id}-alternate-normal");
                        Save($"costume-{id}-all-alternate-poses", gallery);
                        picker.IsDropDownOpen = true; await Task.Delay(80);
                        var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
                        PlainLabels(popup.Child); Save($"costume-{id}-normal-dropdown", (FrameworkElement)popup.Child);
                        picker.IsDropDownOpen = false;
                        var width = window.Width; var height = window.Height;
                        window.Width = 900; window.Height = 640; await Task.Delay(100);
                        var corner = picker.TranslatePoint(new Point(0, 0), window);
                        Require(corner.X >= 0 && corner.Y >= 0 && corner.X + picker.ActualWidth <= window.ActualWidth &&
                            corner.Y + picker.ActualHeight <= window.ActualHeight, "Compact colour picker is clipped outside the window");
                        Neutral(); Save($"costume-{id}-alternate-normal-900x640");
                        advanced.IsChecked = true; Call("UpdateAdvancedMode"); await Task.Delay(80);
                        Require(evidence.IsVisible && !string.IsNullOrWhiteSpace(evidence.Text) && picker.ToolTip is not null,
                            "Advanced hides source/estimate provenance");
                        Save($"costume-{id}-alternate-advanced");
                        window.Width = width; window.Height = height; await Task.Delay(80);
                        picker.SelectedIndex = 0; await Task.Delay(80);
                        Require(gallery.Children.Count == count && Images() == original, "Default colour selection did not restore original pixels");
                        advanced.IsChecked = false; Call("UpdateAdvancedMode"); await Task.Delay(80);
                        Require(!evidence.IsVisible && picker.ToolTip is null, "Returning to Normal retained technical provenance");
                        Neutral(); Log($"Costume{id}: {count} stored poses; default{originalSuit}/alternate{alternateSuit} match Core; used channel changes pixels={changed}; default restored; plain Normal labels and Advanced provenance; compact 900x640; native frame={frame} unchanged");
                    }
                    await Check(32, 7, 0);
                    await Check(13, 11, 0);
                    heartbeat.Stop();
                }
                Neutral(); Log("PASS");
            }
            catch (Exception ex)
            {
                Log(ex.ToString()); Environment.ExitCode = 1;
                try { Save("failed-window"); } catch { }
            }
            finally
            {
                heartbeat?.Stop();
                File.WriteAllLines(Path.Combine(output, "palette-window-check.txt"), report);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
}

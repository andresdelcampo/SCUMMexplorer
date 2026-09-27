using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Rendering;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application();
        var window = new MainWindow();
        var report = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        KeyEventArgs KeyEvent(Key key, bool up = false) => new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, key) { RoutedEvent = up ? Keyboard.PreviewKeyUpEvent : Keyboard.PreviewKeyDownEvent };
        MouseButtonEventArgs MouseEvent(bool up = false) => new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = up ? Mouse.PreviewMouseUpEvent : Mouse.PreviewMouseDownEvent };
        T Control<T>(string name) => (T)window.FindName(name);
        void Check(bool condition, string description)
        {
            report.Add((condition ? "PASS " : "FAIL ") + description);
            if (!condition) throw new Exception(description);
        }
        void Capture(string name)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(args[1], name + ".png")); encoder.Save(stream);
        }
        var started = false;
        window.ContentRendered += async (_, _) =>
        {
            if (started) return;
            started = true;
            try
            {
                Directory.CreateDirectory(args[1]);
                Call("OpenGameFolder", args[0]);
                var items = Control<TreeView>("ResourceTree").Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).ToArray();
                object Resource(string kind, int? id = null) => items.First(i => i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == kind && (id is null || (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == id)).Tag!;
                async Task Select(object resource)
                {
                    Call("SelectResourceInTree", resource);
                    await Task.Delay(900);
                    window.UpdateLayout();
                }
                await Select(Resource("Room", 4));
                var viewport = Control<ScrollViewer>("RoomViewport");
                var preview = Control<Image>("RoomPreview");
                var source = (BitmapSource)preview.Source;
                Check(Control<CheckBox>("NormalRoomWidthToggle").IsChecked == true, "normal width enabled by default");
                var roomScale = preview.ActualWidth / source.PixelWidth;
                Check(viewport.ViewportWidth / roomScale <= 320.1, $"viewport capped at 320 source pixels: {viewport.ViewportWidth / roomScale}");
                Check(roomScale > 1, $"room enlarged to use available area: scale {roomScale}");
                Check(viewport.ActualHeight <= source.PixelHeight * roomScale + 24, $"normal viewport hugs scaled room height: {viewport.ActualHeight}/{source.PixelHeight * roomScale}");
                Check(viewport.ScrollableWidth > 0, "wide Room 04 scrolls");
                // Since 27 September the view opens at the session's camera, which Room 04's
                // entry puts on its right half (local 205); start the pan checks from the left edge.
                viewport.ScrollToHorizontalOffset(0); window.UpdateLayout();
                var beforeTap = viewport.HorizontalOffset;
                Call("PanButton_Click", Control<Button>("ScrollRoomRightButton"), new RoutedEventArgs());
                await Task.Delay(100); window.UpdateLayout();
                Check(viewport.HorizontalOffset > 0, $"right button pans to {viewport.HorizontalOffset}");
                var tapSourcePixels = (viewport.HorizontalOffset - beforeTap) / roomScale;
                Check(tapSourcePixels > 0 && tapSourcePixels <= 4, $"button tap is small in source pixels: {tapSourcePixels}");
                var stepMethod = typeof(MainWindow).GetMethod("GetRoomPanStep", BindingFlags.Static | BindingFlags.NonPublic)!;
                double Step(double seconds, double cap) => (double)stepMethod.Invoke(null, [TimeSpan.FromSeconds(seconds), cap])!;
                foreach (var cap in new[] { 64d, 160d })
                    Check(Step(0, cap) == 2 && Step(.8, cap) > 2 && Step(.8, cap) < cap && Step(1.75, cap) == cap && Step(9, cap) == cap, $"held pan accelerates from 2 to bounded cap {cap}");
                viewport.Focus();
                var beforeKey = viewport.HorizontalOffset;
                Call("RoomViewport_PreviewKeyDown", viewport, KeyEvent(Key.Right));
                Check((bool)Field("_roomPanActive")! && ((DispatcherTimer)Field("_roomPanTimer")!).IsEnabled, "key hold starts active repeat timer");
                await Task.Delay(230); window.UpdateLayout();
                Check(viewport.HorizontalOffset > beforeKey + 2 * roomScale, "held keyboard repeats beyond tap");
                Call("MainWindow_PreviewKeyUp", window, KeyEvent(Key.Right, true));
                var releasedOffset = viewport.HorizontalOffset;
                await Task.Delay(160); window.UpdateLayout();
                Check(!(bool)Field("_roomPanActive")! && !((DispatcherTimer)Field("_roomPanTimer")!).IsEnabled && Math.Abs(viewport.HorizontalOffset - releasedOffset) < .01, "key release cancels repeat and movement");
                Call("PanButton_PreviewMouseLeftButtonDown", Control<Button>("ScrollRoomLeftButton"), MouseEvent());
                Check((bool)Field("_roomPanActive")! && ((DispatcherTimer)Field("_roomPanTimer")!).IsEnabled, "actual button mouse-down starts repeat timer");
                var beforeButtonHold = viewport.HorizontalOffset;
                await Task.Delay(230); window.UpdateLayout();
                Check(viewport.HorizontalOffset < beforeButtonHold - 2 * roomScale, "held button repeats beyond tap");
                Call("PanButton_PreviewMouseLeftButtonUp", Control<Button>("ScrollRoomLeftButton"), MouseEvent(true));
                Check(!(bool)Field("_roomPanActive")!, "button release cancels hold");
                Call("PanRoomBySourcePixels", 10000d); await Task.Delay(100); window.UpdateLayout();
                Check(Math.Abs(viewport.HorizontalOffset - viewport.ScrollableWidth) < .01, "pan clamps at room right edge");
                Call("PanRoomBySourcePixels", -10000d); await Task.Delay(100); window.UpdateLayout();
                Check(viewport.HorizontalOffset == 0, "pan clamps at room left edge");
                Call("BeginRoomPan", 1, 64d, null!, Key.Right);
                Control<CheckBox>("NormalRoomWidthToggle").IsChecked = false;
                Check(!(bool)Field("_roomPanActive")!, "panorama switch cancels hold");
                Control<CheckBox>("NormalRoomWidthToggle").IsChecked = true;
                await Task.Delay(100); window.UpdateLayout();
                var screenSample = viewport.TranslatePoint(new Point(40, 30), preview);
                object[] mapArguments = [screenSample, 0, 0];
                var mapped = (bool)typeof(MainWindow).GetMethod("TryGetRoomImagePoint", flags)!.Invoke(window, mapArguments)!;
                Check(mapped && Math.Abs((int)mapArguments[1] - screenSample.X / roomScale) <= 1, $"scrolled point maps to source x {(int)mapArguments[1]} at offset {viewport.HorizontalOffset}");
                var overlay = Control<Canvas>("RoomNavigationOverlay");
                Check(Math.Abs(overlay.ActualWidth - preview.ActualWidth) < .1 && Math.Abs(overlay.ActualHeight - preview.ActualHeight) < .1, "doorway overlay and image share size after pan");
                var music = Control<StackPanel>("RoomMusicPanel");
                Check(music.TranslatePoint(new Point(), window).Y >= viewport.TranslatePoint(new Point(0, viewport.ActualHeight), window).Y, "music under viewport");
                var layout = Control<Grid>("RoomDisplayLayout");
                var musicBottom = music.TranslatePoint(new Point(0, music.ActualHeight), window).Y;
                var layoutBottom = layout.TranslatePoint(new Point(0, layout.ActualHeight), window).Y;
                Check(layoutBottom - musicBottom >= 0 && layoutBottom - musicBottom < 20, $"music bottom aligned: gap {layoutBottom - musicBottom}");
                Check(Control<TextBlock>("StatusText").Visibility == Visibility.Collapsed, "room ready status removed from bottom");
                var viewportCenter = viewport.TranslatePoint(new Point(viewport.ActualWidth / 2, 0), window).X;
                var arrows = Control<StackPanel>("RoomScrollButtonsPanel");
                Check(Math.Abs(arrows.TranslatePoint(new Point(arrows.ActualWidth / 2, 0), window).X - viewportCenter) < 2, "bottom arrow group centered beneath viewport");
                Check(Math.Abs(music.TranslatePoint(new Point(music.ActualWidth / 2, 0), window).X - viewportCenter) < 2, "music player centered beneath viewport");
                Capture("normal-room-04");
                Control<CheckBox>("NormalRoomWidthToggle").IsChecked = false;
                await Task.Delay(200); window.UpdateLayout();
                Check(viewport.ScrollableWidth < .1, $"panorama all visible: {viewport.ScrollableWidth}");
                var surface = Control<Grid>("RoomSceneSurface");
                var availableHeight = layout.RowDefinitions[1].ActualHeight;
                var expectedScale = Math.Min(layout.ActualWidth / source.PixelWidth, availableHeight / source.PixelHeight);
                var actualScale = Math.Min(preview.ActualWidth / source.PixelWidth, preview.ActualHeight / source.PixelHeight);
                Check(actualScale >= expectedScale * .97, $"panorama fills available room area: scale {actualScale:F3}/{expectedScale:F3}");
                Capture("panorama-room-04");
                Control<CheckBox>("NormalRoomWidthToggle").IsChecked = true;
                window.Width = 1050; window.Height = 700;
                await Task.Delay(200); window.UpdateLayout();
                Check(viewport.ViewportWidth / (preview.ActualWidth / source.PixelWidth) <= 320.1, "smaller window keeps source-pixel cap");
                Capture("small-room-04");
                var depth = Control<CheckBox>("RoomMaskToggle");
                depth.IsChecked = true;
                await Select(Resource("Room", 3));
                Check(depth.IsChecked == true, "depth preference survives room change");
                await Select(Resource("Room", 69));
                if (depth.IsEnabled)
                {
                    var current = (RoomImage)Field("_currentRoom")!;
                    typeof(MainWindow).GetField("_currentRoom", flags)!.SetValue(window, current with { DepthMask = null });
                    Call("RefreshRoomPreview");
                    report.Add("INFO Room 69 has mask data; synthetic absent-mask presentation used for disabled-toggle branch");
                }
                Check(depth.IsChecked == true && !depth.IsEnabled, "absent-mask room disables depth without clearing preference");
                await Select(Resource("Room", 4));
                Check(depth.IsChecked == true && depth.IsEnabled, "return to mask room restores enabled depth preference");
                Call("BeginRoomPan", 1, 64d, null!, Key.Right);
                await Select(Resource("Costume"));
                Check(!(bool)Field("_roomPanActive")!, "resource switch cancels active room pan");
                var gallery = Control<WrapPanel>("CostumePoseGallery");
                IEnumerable<Image> Images(DependencyObject parent)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = VisualTreeHelper.GetChild(parent, i);
                        if (child is Image image) yield return image;
                        foreach (var nested in Images(child)) yield return nested;
                    }
                }
                var poses = Images(gallery).Where(i => i.Source is BitmapSource).ToArray();
                Check(poses.Length > 1, $"all-pose gallery contains {poses.Length} images");
                var scales = poses.Select(i => i.ActualWidth / ((BitmapSource)i.Source).PixelWidth).ToArray();
                Check(scales.All(s => Math.Abs(s - scales[0]) < .001), $"all costume poses share scale {scales[0]}");
                Check(scales[0] > 1, "costume poses enlarged together for available space");
                Check(poses.All(i => i.Stretch is Stretch.Uniform or Stretch.Fill), "costume raster stretches into enlarged image boxes");
                Capture("costume-gallery");
                await Select(Resource("Script"));
                Check(Control<Grid>("RoomDisplayLayout").Visibility == Visibility.Collapsed, "script hides room display");
                await Select(Resource("Sound"));
                Check(Control<Grid>("RoomDisplayLayout").Visibility == Visibility.Collapsed, "sound hides room display");
                await Select(Resource("Room", 4));
                Check(Control<Grid>("RoomDisplayLayout").Visibility == Visibility.Visible && preview.Source is not null, "room restores after costume/script/sound selection");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(args[1], "geometry-check.txt"), report); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); Environment.ExitCode = 1; }
                finally { window.Close(); app.Shutdown(); }
            }
        };
        app.Run(window);
    }
}

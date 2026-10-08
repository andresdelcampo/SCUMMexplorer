using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Real offscreen MainWindow, private profile/cache, no activation or OS input.
// Text is a routed WPF event; note clicks use the handler's Point-based seam.
// Prepared catalog arrivals and manually driven WPF frames do not prove native routes/timing.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG", Path.Combine(output, "failures.log"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, Left = -4000, Top = -4000,
            ShowActivated = false, ShowInTaskbar = false };
        object? Field(string n) => typeof(MainWindow).GetField(n, Private)!.GetValue(window);
        object? Call(string n, params object?[] a) => typeof(MainWindow).GetMethod(n, Private)!.Invoke(window, a);
        T Find<T>(string n) => (T)window.FindName(n);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        var checks = new List<string>(); var cases = new List<object>();
        void Check(bool ok, string name) { checks.Add((ok ? "PASS " : "FAIL ") + name); }
        async Task WaitFor(Func<bool> predicate, string name)
        {
            for (int n = 0; n < 600 && !predicate(); n++) await Task.Delay(25);
            if (!predicate()) throw new TimeoutException(name);
        }
        void Frames(int count)
        {
            for (int n = 0; n < count; n++)
            {
                (Field("_playbackTimer") as System.Windows.Threading.DispatcherTimer)?.Stop();
                if (Field("_playback") is not null) Call("PlaybackTimer_Tick", null, EventArgs.Empty);
            }
            (Field("_playbackTimer") as System.Windows.Threading.DispatcherTimer)?.Stop();
        }
        void Capture(string name)
        {
            window.UpdateLayout();
            var image = Find<Image>("VerbStripImage");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(image.ActualWidth),
                (int)Math.Ceiling(image.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(image);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + "-strip.png")); encoder.Save(file);
        }
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                // Do not render or play audible cues in this verification process.
                var sound = Field("_sceneSound")!;
                sound.GetType().GetMethod("SetMode")!.Invoke(sound,
                    [Enum.Parse(sound.GetType().GetProperty("Mode")!.PropertyType, "None")]);
                Find<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                Find<ComboBox>("CdSoundCombo").SelectedIndex = 0;
                Field("_cdVoice")!.GetType().GetProperty("Enabled")!.SetValue(Field("_cdVoice"), false);
                typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, true);
                foreach (var (edition, relative) in new[] { ("ega-en", "loom ega mt32 en"),
                    ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
                foreach (var route in new[] { "text", "notes" })
                foreach (bool reverse in new[] { false, true })
                {
                    string label = $"{edition}-{route}-{(reverse ? "reverse" : "forward")}";
                    typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, true);
                    Call("OpenGameFolder", Path.Combine(root, relative));
                    Find<ToggleButton>("SessionModeToggle").IsChecked = true;
                    var game = (GameLibrary)Field("_game")!;
                    Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 4));
                    await WaitFor(() => ((GameResource?)Field("_currentRoomResource"))?.ResourceId == 4 &&
                        Session().CurrentRoomId == 4 && Field("_playback") is null, label + " catalog arrival");
                    var session = Session();
                    session.UpdateStaffPossession(true); session.UpdateAvailableNotes(8);
                    Call("RefreshRoomPreview"); window.UpdateLayout();
                    var data = ScummDataSources.For(game); var room = data.ReadRoom(4);
                    int targetId = game.Id == GameId.LoomVga ? 101 : 171;
                    var target = room.Objects.Single(o => o.Id == targetId);
                    Call("HandleRoomPoint", target.X + target.Width / 2, target.Y + target.Height / 2, false);
                    await WaitFor(() => Field("_playback") is null && !session.EgoMoving && !session.InCutscene,
                        label + " aim");
                    string aimKey = game.Id == GameId.LoomVga ? "var[0x099]" : "var[0x067]";
                    Check(session.Snapshot.KnownGlobalVariables.GetValueOrDefault(aimKey) == targetId, label + " target aimed");
                    var draft = session.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3A);
                    var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes;
                    var strip = Find<Image>("VerbStripImage");
                    Check(strip.Source is not null && strip.ActualWidth > 0 && strip.ActualHeight > 0,
                        label + " actual note strip rendered");
                    int traceStart = session.SessionTrace.Count, messageStart = session.Messages.Log.Count;
                    typeof(MainWindow).GetField("_playInstantly", Private)!.SetValue(window, false);
                    File.WriteAllText(Path.Combine(output, label + "-verbs.json"),
                        JsonSerializer.Serialize(session.VerbArea().Items, new JsonSerializerOptions { WriteIndented = true }));
                    foreach (int note in notes)
                    {
                        if (route == "text")
                        {
                            var composition = new TextComposition(InputManager.Current, window, "qwertyui"[note - 1].ToString());
                            var input = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                                { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
                            window.RaiseEvent(input);
                            Check(input.Handled, label + $" note {note} routed text handled");
                        }
                        else
                        {
                            int noteVerb = game.Id == GameId.LoomVga ? 0x59 + note : note;
                            var slot = session.VerbArea().Items.Single(i => i.VerbId == noteVerb && i.Mode == 1);
                            var position = new Point((slot.X + slot.Width / 2.0) * strip.ActualWidth / 320,
                                (slot.Y + slot.Height / 2.0 - LoomVerbArea.Top) * strip.ActualHeight / LoomVerbArea.Height);
                            var hit = window.InputHitTest(strip.TranslatePoint(position, window));
                            Check(ReferenceEquals(hit, strip), label + $" note {note} layout hit reaches strip");
                            Check((bool)Call("ClickVerbStrip", position)!, label + $" note {note} strip click accepted");
                        }
                        Frames(8);
                    }
                    for (int n = 0; n < 600 && Field("_playback") is not null; n++) Frames(1);
                    Check(Field("_playback") is null, label + " WPF playback finishes within bound");
                    var trace = session.SessionTrace.Skip(traceStart).ToArray();
                    int verb = reverse ? 0x3B : 0x3A;
                    Check(trace.Any(t => t.Contains($"startObject runs object {targetId} verb 0x{verb:X2}")),
                        label + " full draft reaches own object response");
                    var boundaries = trace.Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
                    Check(boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at ")),
                        label + " scene scopes balanced");
                    Check(session.CurrentRoomId == 4 && !session.InCutscene && !session.DraftPlaying && !session.EgoMoving,
                        label + " settled in Room 04");
                    var text = session.Messages.Log.Skip(messageStart).Where(m => !m.SourceLabel.StartsWith("Script 18"))
                        .SelectMany(m => m.Lines).ToArray();
                    Check(text.Any(t => t.Length > 8 && t.Any(char.IsLetter)), label + " source response text observed");
                    Check(!window.IsActive, label + " verification window never activated");
                    File.WriteAllLines(Path.Combine(output, label + "-trace.txt"), trace);
                    cases.Add(new { edition, route, reverse, targetId, notes, draft.Origin, draft.Learned,
                        boundaries, text, finalRoom = session.CurrentRoomId, window.IsActive,
                        stripWidth = strip.ActualWidth, stripHeight = strip.ActualHeight });
                    if (route == "notes" && !reverse) Capture(label);
                }
            }
            catch (Exception e) { checks.Add("FAIL " + e); }
            finally
            {
                int failures = checks.Count(c => c.StartsWith("FAIL "));
                File.WriteAllLines(Path.Combine(output, "checks.txt"),
                    new[] { $"{checks.Count - failures}/{checks.Count} assertions passed; {cases.Count}/12 cases recorded" }.Concat(checks));
                File.WriteAllText(Path.Combine(output, "cases.json"), JsonSerializer.Serialize(cases,
                    new JsonSerializerOptions { WriteIndented = true }));
                window.Close(); app.Shutdown(failures == 0 && cases.Count == 12 ? 0 : 1);
            }
        };
        app.Run(window);
    }
}

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        Environment.SetEnvironmentVariable("SCUMM_HARNESS_FAILURE_LOG", Path.Combine(output, "failures.log"));
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow { Width = 1400, Height = 900, Left = -4000, Top = -4000, ShowActivated = false, ShowInTaskbar = false };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        T Find<T>(string name) => (T)window.FindName(name);
        LoomExplorationSession Session() => (LoomExplorationSession)Field("_session")!;
        var report = new List<string>();
        void Check(bool ok, string name) => report.Add((ok ? "PASS " : "FAIL ") + name);
        window.ContentRendered += async (_, _) =>
        {
            try
            {
                Find<ComboBox>("SoundModeCombo").SelectedIndex = 0;
                Find<ComboBox>("CdSoundCombo").SelectedIndex = 0;
                Field("_cdVoice")!.GetType().GetProperty("Enabled")!.SetValue(Field("_cdVoice"), false);
                typeof(MainWindow).GetField("_playInstantly", flags)!.SetValue(window, true);
                Call("OpenGameFolder", Path.GetFullPath(args[0]));
                Find<ToggleButton>("SessionModeToggle").IsChecked = true;
                var game = (GameLibrary)Field("_game")!;
                Call("SelectResourceInTree", game.Rooms.Single(r => r.ResourceId == 4));
                for (int n = 0; n < 600 && (Session().CurrentRoomId != 4 || Field("_playback") is not null); n++) await Task.Delay(25);
                var original = Session();
                Check(original.GameStatus.CanEdit, "initial prepared session is settled");
                original.UpdateStaffPossession(true); original.UpdateAvailableNotes(8); original.UpdateDraftLearned(15, false);
                original.UpdateStoryBit("bit[0x03D]", true); // Own Room 34 entry's ghost selector; prepared state, not earned.
                var data = ScummDataSources.For(game);
                var triggers = LoomCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value));
                var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
                var trigger = triggers.Single(t => t.Room == 41 && t.ObjectId == 559 && t.VerbId == 0x4B);
                var setup = planner.Derive(trigger);
                Check(setup.Plays && setup.HasSteps, "own-source sword trigger requires a derived start");
                File.WriteAllText(Path.Combine(output, "setup.json"), JsonSerializer.Serialize(setup, new JsonSerializerOptions { WriteIndented = true }));
                typeof(MainWindow).GetField("_cdSetupPlanner", flags)!.SetValue(window, new Lazy<LoomCdSceneSetupPlanner>(() => planner));
                Call("PlayCdCutscene", new LoomCutsceneGroup(trigger, []));
                (Field("_playbackTimer") as System.Windows.Threading.DispatcherTimer)?.Stop();
                Check(ReferenceEquals(original, Session()), "single CD scene retains the running session");
                Check(Session().GameStatus.AvailableNoteCount == 8, "single CD scene retains eight chosen notes");
                Check(Session().GameStatus.Drafts.Single(d => d.Slot == 15).Learned == false, "single CD scene retains later draft choice");
                Check(Session().StoryBit("bit[0x03D]") == true, "single CD scene retains the chosen ghost story state");
                File.WriteAllLines(Path.Combine(output, "trace.txt"), Session().SessionTrace);
                File.WriteAllText(Path.Combine(output, "status.json"), JsonSerializer.Serialize(Session().GameStatus, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { report.Add("FAIL " + ex); }
            finally { File.WriteAllLines(Path.Combine(output, "report.txt"), report); app.Shutdown(report.Any(s => s.StartsWith("FAIL ")) ? 1 : 0); }
        };
        app.Run(window);
    }
}

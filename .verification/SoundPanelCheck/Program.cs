using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Domain;

// The sound panel's time text while a sound is prepared, once it is ready, and for a sound with no playable audio.
// Arguments: <Games\loom> <output folder>
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow { Width = 1200, Height = 760 };
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        T Find<T>(string name) => (T)window.FindName(name);
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            try
            {
                typeof(MainWindow).GetMethod("OpenGameFolder", Private)!.Invoke(window, [args[0]]);
                await Task.Delay(1500);
                var game = (GameLibrary)Field("_game")!;
                var audio = new ScummAdlibAudio();
                var sounds = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue).ToArray();
                var unplayable = sounds.First(s => audio.ReadLifetime(s).Kind == ScummSoundLifetimeKind.Unknown && !s.Name.Contains('.'));
                foreach (var sound in new[] { sounds.First(s => s.ResourceId == 28), unplayable })
                {
                    var task = (Task)typeof(MainWindow).GetMethod("ShowAudio", Private)!.Invoke(window, [sound, false, false])!;
                    var time = Find<TextBlock>("AudioTime");
                    report.Add($"{sound.Name}: right after the call: \"{time.Text}\", Play enabled {Find<Button>("PlayButton").IsEnabled}");
                    await task;
                    report.Add($"{sound.Name}: when ready: \"{time.Text}\", Play enabled {Find<Button>("PlayButton").IsEnabled}; \"{Find<TextBlock>("AudioDescription").Text}\"");
                    await Task.Delay(1500);
                    report.Add($"{sound.Name}: 1.5 s later: \"{time.Text}\"");
                }
            }
            catch (Exception ex) { report.Add("FAILED: " + ex); }
            File.WriteAllLines(Path.Combine(args[1], "sound-panel-check.txt"), report);
            app.Shutdown();
        };
        app.Run(window);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private readonly Dictionary<GameResource, string> _soundLines = new();

    private async void ResourceTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem branch || branch.Items.Count < 1000 ||
            branch.Items[0] is not TreeViewItem { Tag: GameResource { Kind: ResourceKind.Sound } }) return;

        using var busy = BeginBusy(null);
        // Expansion realizes and lays out thousands of rows on the UI thread. Set the cursor before
        // returning to that work, and retain it until the pending layout/render passes have finished.
        try { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); }
        catch (TaskCanceledException) when (Dispatcher.HasShutdownStarted) { }
    }

    private void ShowSoundPresentation(GameResource resource, string format)
    {
        AudioFormat.Text = IsSpeech(resource) ? "Recorded speech" : format;
        AudioSource.Text = resource.Description?.Replace(" Its text is in Game Texts.", "", StringComparison.Ordinal) ?? "";
        AudioLine.Text = _soundLines.TryGetValue(resource, out var line) ? SoundDisplayLine(line) : "";
        AudioLine.Visibility = AudioLine.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        AudioPlaybackState.Text = "Preparing…";
        AudioProgress.Value = 0;
    }

    // Timing and formatting annotations stay in the source views, rather than in the listening card.
    private static string SoundDisplayLine(string line)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(line, @"\[(?:pause|continue|text control [^\]]*)\]", " ");
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

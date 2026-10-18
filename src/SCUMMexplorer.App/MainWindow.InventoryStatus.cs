using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private bool InventoryStatusAvailable => _game is { } game && PlayActive && _playFolder == game.FolderPath &&
        GameSupport.For(game)[SupportFeature.Status] != SupportLevel.None &&
        ScummProfile.For(game).EngineVersion is ScummVersion.V1 or ScummVersion.V2;

    private InventoryStatusWindow BuildInventoryStatusWindow()
    {
        var session = _play ?? throw new InvalidOperationException("There is no live inventory session.");
        var folder = _playFolder;
        return new(session, () => ReferenceEquals(_play, session) && _playFolder == folder && _game?.FolderPath == folder,
            () => { ShowPlayFrame(); UpdatePlaybackControls(); }, IsAdvancedMode, _game is { } game ? ManiacPreviewText.For(game) : null)
        { Owner = this, Resources = Resources };
    }

    private void ShowInventoryStatus()
    {
        var session = _play;
        var timer = _playTimer;
        var timerRunning = timer?.IsEnabled == true;
        var wasPaused = _playPaused;
        var soundWasPaused = _playSoundPaused;
        timer?.Stop(); _playPaused = true;
        if (!soundWasPaused) _sceneSound.Pause();
        _playSoundPaused = true;
        try { BuildInventoryStatusWindow().ShowDialog(); }
        finally
        {
            if (ReferenceEquals(_play, session) && ReferenceEquals(_playTimer, timer))
            {
                _playPaused = wasPaused; _playSoundPaused = soundWasPaused;
                if (!soundWasPaused) _sceneSound.Resume();
                if (timerRunning) timer?.Start();
                ShowPlayFrame(); UpdatePlaybackControls();
            }
        }
    }
}

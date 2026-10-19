using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private ScummGameSession? _inventoryUndoSession;
    private ScummInventoryUndoToken? _inventoryLastUndo;
    private bool InventoryStatusAvailable => _game is { } game && PlayActive && _playFolder == game.Key &&
        _play?.InventoryCharactersAvailable == true &&
        GameSupport.For(game)[SupportFeature.Status] != SupportLevel.None &&
        ScummProfile.For(game).EngineVersion is ScummVersion.V1 or ScummVersion.V2;

    private InventoryStatusWindow BuildInventoryStatusWindow()
    {
        var session = _play ?? throw new InvalidOperationException("There is no live inventory session.");
        var folder = _playFolder;
        var dialog = new InventoryStatusWindow(session, () => ReferenceEquals(_play, session) && _playFolder == folder && _game?.Key == folder,
            () => { ShowPlayFrame(); UpdatePlaybackControls(); }, IsAdvancedMode, _game is { } game ? ManiacPreviewText.For(game) : null,
            _game is { } current ? (int)ScummProfile.For(current).EngineVersion : 2,
            ReferenceEquals(_inventoryUndoSession, session) ? _inventoryLastUndo : null)
        { Owner = this };
        dialog.Resources.MergedDictionaries.Add(Resources);
        return dialog;
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
        try
        {
            var dialog = BuildInventoryStatusWindow();
            dialog.ShowDialog();
            _inventoryUndoSession = session;
            _inventoryLastUndo = dialog.LastUndo;
        }
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

using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private bool InventoryStatusAvailable => _ctx.Game is { } game && PlayActive && _ctx.PlayFolder == game.Key &&
        _ctx.Play?.InventoryCharactersAvailable == true &&
        GameSupport.For(game)[SupportFeature.Status] != SupportLevel.None &&
        ScummProfile.For(game).EngineVersion is ScummVersion.V1 or ScummVersion.V2 or ScummVersion.V3;


    private ScummStatusSchema? StatusSchema(ScummGameSession session)
    {
        if (_ctx.Game is not { } game || ScummProfile.For(game).EngineVersion != ScummVersion.V3) return null;
        if (_ctx.StatusSchema is { } kept && kept.Key == game.Key) return kept.Schema;
        var schema = ScummStatusSchema.Read(game, ScummCutscenePlanner.VerbsOf(session));
        _ctx.StatusSchema = (game.Key, schema);
        return schema;
    }

    private InventoryStatusWindow BuildInventoryStatusWindow()
    {
        var session = _ctx.Play ?? throw new InvalidOperationException("There is no live inventory session.");
        var folder = _ctx.PlayFolder;
        var dialog = new InventoryStatusWindow(session, () => ReferenceEquals(_ctx.Play, session) && _ctx.PlayFolder == folder && _ctx.Game?.Key == folder,
            () => { ShowPlayFrame(); UpdatePlaybackControls(); }, IsAdvancedMode, _ctx.Game is { } game ? ManiacPreviewText.For(game) : null,
            _ctx.Game is { } current ? (int)ScummProfile.For(current).EngineVersion : 2,
            ReferenceEquals(_ctx.InventoryUndoSession, session) ? _ctx.InventoryLastUndo : null, StatusSchema(session))
        { Owner = this };
        dialog.Resources.MergedDictionaries.Add(Resources);
        return dialog;
    }

    private void ShowInventoryStatus()
    {
        var session = _ctx.Play;
        var timer = _ctx.PlayTimer;
        var timerRunning = timer?.IsEnabled == true;
        var wasPaused = _ctx.PlayPaused;
        var soundWasPaused = _ctx.PlaySoundPaused;
        timer?.Stop(); _ctx.PlayPaused = true;
        if (!soundWasPaused) _sceneSound.Pause();
        _ctx.PlaySoundPaused = true;
        try
        {
            var dialog = BuildInventoryStatusWindow();
            dialog.ShowDialog();
            _ctx.InventoryUndoSession = session;
            _ctx.InventoryLastUndo = dialog.LastUndo;
        }
        finally
        {
            if (ReferenceEquals(_ctx.Play, session) && ReferenceEquals(_ctx.PlayTimer, timer))
            {
                _ctx.PlayPaused = wasPaused; _ctx.PlaySoundPaused = soundWasPaused;
                if (!soundWasPaused) _sceneSound.Resume();
                if (timerRunning) timer?.Start();
                ShowPlayFrame(); UpdatePlaybackControls();
            }
        }
    }
}

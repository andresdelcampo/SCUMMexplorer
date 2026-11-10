using System.IO;
using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    /// <summary>LOOM CD: the disc's narration and spoken lines the session's scripts ask for.</summary>
    private readonly CdVoicePlayer _cdVoice = new();

    private IGameVoice? _loomVoiceInstance;
    private IGameVoice LoomVoice => _loomVoiceInstance ??= new LoomCdVoice(this);


    private sealed class LoomCdVoice(MainWindow w) : IGameVoice
    {
        public void Stop() => w._cdVoice.Stop();
        public void Pause() => w._cdVoice.Pause();
        public void Resume() => w._cdVoice.Resume();
        public void Detach() => w._cdVoice.Detach();
        public void Update() => w._cdVoice.Update();
        public void SkipPending(LoomExplorationSession? session) { if (w._cdVoice.Follows(session)) w._cdVoice.SkipPending(); }
        public bool Follows(LoomExplorationSession? session) => w._cdVoice.Follows(session);
        public bool ReplacesSceneSound => true;

        public bool Attach(LoomExplorationSession session)
        {
            if (w._ctx.Game?.V4Archive is not { } vga) return false;
            w._cdVoice.Attach(vga.Disc, session);
            return true;
        }
    }

    /// <summary>The CD game's Sounds list: the disc's audio the room's own scripts ask for (music or narration first, then the spoken lines), played on demand.</summary>
    private bool UpdateCdRoomSounds(GameResource? room)
    {
        if (_ctx.Game?.Id != GameId.LoomVga) return false;
        if (_ctx.Game.V4Archive?.Disc is not null && room is { ResourceId: int cdRoomId })
        {
            try { _ctx.CdRoomCues = LoomCdCueIndex.ScanRoom(_ctx.CdData ?? ScummDataSources.For(_ctx.Game), cdRoomId).OrderByDescending(cue => cue.IsMusicOrNarration).ThenBy(cue => cue.Cue.Offset).ToArray(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _ctx.CdRoomCues = []; }
            RoomSoundsButton.Visibility = _ctx.CdRoomCues.Count > 0 && _cdVoice.Enabled ? Visibility.Visible : Visibility.Collapsed;
        }
        return true;
    }
}

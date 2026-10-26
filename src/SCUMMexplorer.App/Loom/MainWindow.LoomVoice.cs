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

    private IReadOnlyList<LoomCdCue> _cdRoomCues = [];

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
            if (w._game?.V4Archive is not { } vga) return false;
            w._cdVoice.Attach(vga.Disc, session);
            return true;
        }
    }

    /// <summary>The CD game's Sounds list: the disc's audio the room's own scripts ask for (music or narration first, then the spoken lines), played on demand.</summary>
    private bool UpdateCdRoomSounds(GameResource? room)
    {
        if (_game?.Id != GameId.LoomVga) return false;
        if (_game.V4Archive?.Disc is not null && room is { ResourceId: int cdRoomId })
        {
            try { _cdRoomCues = LoomCdCueIndex.ScanRoom(_cdData ?? ScummDataSources.For(_game), cdRoomId).OrderByDescending(cue => cue.IsMusicOrNarration).ThenBy(cue => cue.Cue.Offset).ToArray(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _cdRoomCues = []; }
            RoomSoundsButton.Visibility = _cdRoomCues.Count > 0 && _cdVoice.Enabled ? Visibility.Visible : Visibility.Collapsed;
        }
        return true;
    }
}

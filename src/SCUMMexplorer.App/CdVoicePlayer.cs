using System.IO;
using System.Windows.Media;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Storage;

namespace SCUMMexplorer.App;

/// <summary>
/// Plays the voice lines of LOOM on CD: each <c>sayVoice</c> a session's reached prints issued (<see cref="LoomMessageTable.VoiceEvents"/>)
/// is cut from the disc's audio track and played; a new line replaces the one still playing, and offset 0 with length 0 stops it.
/// It follows one session at a time and plays what that session issued since it was attached.
/// </summary>
public sealed class CdVoicePlayer
{
    private DiscImage? _disc;
    private LoomExplorationSession? _session;
    private int _seen;
    private MediaPlayer? _player;
    private string? _file;
    private readonly Queue<LoomVoiceCue> _queue = new();
    private bool _busy;
    private int _generation;
    private bool _paused;

    /// <summary>False while the user has voices off; cues are skipped, not queued.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>One line of what the player did, for the harness.</summary>
    public List<string> Log { get; } = [];

    public bool Follows(LoomExplorationSession? session) => session is not null && ReferenceEquals(session, _session);

    public void Attach(DiscImage? disc, LoomExplorationSession session)
    {
        Stop();
        _disc = disc;
        _session = session;
        _seen = session.Messages.VoiceEvents.Count;
    }

    /// <summary>
    /// Drops the cues issued so far without playing them: a room entered from the catalog runs its entry script (and the CD boot's tail)
    /// as a state preview, which the player did not do, so its lines are not spoken.
    /// </summary>
    public void SkipPending()
    {
        if (_session is not { } session) return;
        if (session.Messages.VoiceEvents.Count > _seen) Log.Add($"skipped {session.Messages.VoiceEvents.Count - _seen} cue(s) issued by a room visit");
        _seen = session.Messages.VoiceEvents.Count;
    }

    /// <summary>Starts or stops the voice for the cues the session issued since the last call.</summary>
    public void Update()
    {
        if (_session is not { } session) return;
        var cues = session.Messages.VoiceEvents;
        if (cues.Count < _seen) _seen = 0;
        // A click runs its whole interaction at once, so its lines and the stops after them (the game's own, issued when the text ends)
        // arrive together, without the time between them. The lines play one after the other, each for its own length; stops after a
        // line in the batch are left to that length, and a batch of stops alone silences the voice.
        var first = _seen;
        var started = false;
        for (; _seen < cues.Count; _seen++)
        {
            if (cues[_seen].IsStop) continue;
            started = true;
            if (Enabled && _disc is not null) _queue.Enqueue(cues[_seen]);
            else Log.Add($"skipped {cues[_seen].Offset}/{cues[_seen].Length}");
        }
        if (_seen > first && !started) { Stop(); Log.Add("stop"); return; }
        if (!_busy && _queue.Count > 0) PlayNext();
    }

    public void Pause() { _paused = true; _player?.Pause(); }

    public void Resume() { _paused = false; _player?.Play(); }

    public void Stop()
    {
        _queue.Clear();
        End();
    }

    private void End()
    {
        _busy = false;
        _generation++;
        if (_player is { } player) { player.Stop(); player.Close(); _player = null; }
        if (_file is { } file) { try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } _file = null; }
    }

    private void PlayNext()
    {
        if (_queue.Count > 0) Play(_queue.Dequeue());
    }

    private async void Play(LoomVoiceCue cue)
    {
        End();
        _busy = true;
        var generation = _generation;
        var disc = _disc!;
        byte[] wave;
        try { wave = await Task.Run(() => disc.ReadAudioWave(cue.TrackStartFrame, cue.TrackFrameCount)); }
        catch (IOException ex) { Log.Add($"failed {ex.Message}"); return; }
        if (generation != _generation) return;
        if (wave.Length == 0) { Log.Add($"no audio at frame {cue.TrackStartFrame}"); PlayNext(); return; }
        var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-voice-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, wave);
        if (generation != _generation) { File.Delete(path); return; }
        _file = path;
        var player = _player = new MediaPlayer();
        player.MediaEnded += (_, _) =>
        {
            if (generation != _generation) return;
            End();
            PlayNext();
        };
        player.MediaOpened += (_, _) => { if (generation == _generation && !_paused) player.Play(); };
        player.MediaFailed += (_, error) =>
        {
            Log.Add($"failed {error.ErrorException.Message}");
            if (generation != _generation) return;
            End();
            PlayNext();
        };
        player.Open(new Uri(path));
        Log.Add($"play {cue.Offset}/{cue.Length}: frame {cue.TrackStartFrame}, {cue.TrackFrameCount} frames");
    }
}

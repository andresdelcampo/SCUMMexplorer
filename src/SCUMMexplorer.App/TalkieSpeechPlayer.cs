using System.IO;
using System.Windows.Media;

namespace SCUMMexplorer.App;

/// <summary>
/// S6 Indy4 (17 October 2026): plays the spoken lines of a talkie (<c>MONSTER.SOU</c>) during play. The session records each line a shown text starts
/// (<see cref="ScummMessageTable.SpeechEvents"/>: the text's code 10, ScummVM <c>talkSound</c>) and each stop (<c>stopTalk</c>); the play loop runs the
/// game frame by frame, so a line starts at the frame its text appears and stops when the game stops it. Lines the session started before it was
/// attached (the boot's) are not played.
/// </summary>
public sealed class TalkieSpeechPlayer
{
    private ScummSpeechBank? _bank;
    private ScummMessageTable? _messages;
    private int _seen;
    private MediaPlayer? _player;
    private string? _file;
    private int _generation;
    private bool _paused;

    /// <summary>One line per start or stop, for the harness.</summary>
    public List<string> Log { get; } = [];

    public bool Follows(ScummMessageTable messages) => ReferenceEquals(messages, _messages);

    /// <param name="from">The first speech event to play; by default none the session has already recorded. A cutscene started from the list in a room
    /// opened for it passes the count before that room's visit, so the scene's first line is spoken (S7 Indy4, 18 October 2026).</param>
    public void Attach(ScummSpeechBank bank, ScummMessageTable messages, int? from = null)
    {
        Stop();
        _bank = bank;
        _messages = messages;
        _seen = Math.Clamp(from ?? messages.SpeechEvents.Count, 0, messages.SpeechEvents.Count);
    }

    public void Detach()
    {
        Stop();
        _bank = null;
        _messages = null;
    }

    /// <summary>Plays the newest line the session started since the last call, or stops the voice if the game stopped it.</summary>
    public void Update()
    {
        if (_messages is not { } messages || _bank is not { } bank) return;
        var events = messages.SpeechEvents;
        if (events.Count <= _seen) return;
        var latest = events[^1];
        _seen = events.Count;
        if (latest.IsStop)
        {
            Stop();
            Log.Add("stop");
            return;
        }
        Play(bank, latest);
    }

    public void Pause() { _paused = true; _player?.Pause(); }

    public void Resume() { _paused = false; _player?.Play(); }

    public void Stop()
    {
        _generation++;
        if (_player is { } player) { player.Stop(); player.Close(); _player = null; }
        if (_file is { } file) { try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } _file = null; }
    }

    private async void Play(ScummSpeechBank bank, ScummSpeechEvent line)
    {
        Stop();
        var generation = _generation;
        byte[] wave;
        try { wave = await Task.Run(() => bank.ReadWave(line.Offset, line.Length)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { Log.Add($"failed {line.Offset}: {ex.Message}"); return; }
        if (generation != _generation) return;
        var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-speech-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, wave);
        if (generation != _generation) { File.Delete(path); return; }
        _file = path;
        var player = _player = new MediaPlayer();
        player.MediaOpened += (_, _) => { if (generation == _generation && !_paused) player.Play(); };
        player.MediaEnded += (_, _) => { if (generation == _generation) Stop(); };
        player.MediaFailed += (_, error) => { Log.Add($"failed {error.ErrorException.Message}"); if (generation == _generation) Stop(); };
        player.Open(new Uri(path));
        Log.Add($"play {line.Offset}/{line.Length}: {line.DurationJiffies} jiffies");
    }
}

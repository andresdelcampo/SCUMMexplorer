using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

/// <summary>
/// Plays the sounds a session's scripts start and stop while the playback clock runs (cutscenes, the opening and the
/// frames after an input), from <see cref="LoomExplorationSession.SoundEvents"/>. Each reached <c>startSound</c> starts
/// its track on the frame that reached it; <c>stopSound</c> stops that track and <c>stopMusic</c> stops them all, as
/// ScummVM's <c>stopAllSounds</c> does. Tracks overlap, as the game's sound channels do.
/// Music tracks play as the AdLib rendition. The game's AdLib sound effects are not decoded yet, so effects play as the
/// PC speaker rendition of the same resource (presentation). A track that is still being rendered when its frame comes
/// starts late, at the position the game would have reached.
/// This player has its own outputs: the room music panel's player is separate, and starting it stops these sounds.
/// </summary>
public sealed class SceneSoundPlayer
{
    /// <summary>One line of what the player did, for the harness: the frame count, the time and the sound.</summary>
    public sealed record Entry(int Frame, long Milliseconds, string Action, int? SoundId, string Kind, string Source);

    private sealed class Voice
    {
        public required int SoundId { get; init; }
        public required long StartedAt { get; init; }
        public MediaPlayer? Player { get; set; }
        public bool Stopped { get; set; }
    }

    private readonly LoomAdlibAudio _adlib = new();
    private readonly LoomPcSpeakerAudio _speaker = new();
    private readonly Dictionary<int, Task<(string? Path, bool Music)>> _renders = [];
    private readonly List<Voice> _voices = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private GameLibrary? _game;
    private LoomExplorationSession? _session;
    private int _seen;
    private int _frames;
    private bool _paused;

    public List<Entry> Log { get; } = [];

    /// <summary>Follows <paramref name="session"/> from its next sound event on; a new session stops the old one's sounds.</summary>
    public void Attach(GameLibrary game, LoomExplorationSession session)
    {
        StopAll("session replaced");
        if (!ReferenceEquals(game, _game))
        {
            _renders.Clear();
            _game = game;
        }
        _session = session;
        _seen = session.SoundEvents.Count;
        _frames = 0;
    }

    public bool Follows(LoomExplorationSession? session) => session is not null && ReferenceEquals(session, _session);

    /// <summary>Plays the sound events the session reached since the last call (one playback frame).</summary>
    public void Update()
    {
        if (_session is not { } session || _game is null) return;
        _frames++;
        var events = session.SoundEvents;
        for (; _seen < events.Count; _seen++)
        {
            var item = events[_seen].Event;
            if (item.Certainty != LoomScriptEventCertainty.Reached) continue;
            var source = $"{item.SourceLabel} +0x{item.Offset:X4} {item.Operation} {item.Operands}".TrimEnd();
            switch (item.Kind)
            {
                case LoomScriptEventKind.SoundStarted or LoomScriptEventKind.MusicStarted when item.TargetId is int id:
                    Stop(id);
                    Start(id, source);
                    break;
                case LoomScriptEventKind.SoundStopped when item.TargetId is int id:
                    Stop(id);
                    Log.Add(new(_frames, _clock.ElapsedMilliseconds, "stop", id, "", source));
                    break;
                case LoomScriptEventKind.MusicStopped:
                    StopAll(source);
                    break;
                default:
                    // A start or stop whose target the session could not resolve: nothing to play.
                    Log.Add(new(_frames, _clock.ElapsedMilliseconds, "unresolved", null, "", source));
                    break;
            }
        }
    }

    public void Pause()
    {
        _paused = true;
        foreach (var voice in _voices) voice.Player?.Pause();
    }

    public void Resume()
    {
        _paused = false;
        foreach (var voice in _voices) voice.Player?.Play();
    }

    public void StopAll(string reason)
    {
        if (_voices.Count > 0) Log.Add(new(_frames, _clock.ElapsedMilliseconds, "stop all", null, "", reason));
        foreach (var voice in _voices.ToArray()) End(voice);
    }

    private void Stop(int id)
    {
        foreach (var voice in _voices.Where(voice => voice.SoundId == id).ToArray()) End(voice);
    }

    private void End(Voice voice)
    {
        voice.Stopped = true;
        voice.Player?.Stop();
        voice.Player?.Close();
        _voices.Remove(voice);
    }

    private async void Start(int id, string source)
    {
        var voice = new Voice { SoundId = id, StartedAt = _clock.ElapsedMilliseconds };
        _voices.Add(voice);
        var frame = _frames;
        var render = Render(id);
        var (path, music) = render.IsCompleted ? render.Result : await render;
        var kind = path is null ? "unavailable" : music ? "music (AdLib)" : "effect (PC speaker)";
        Log.Add(new(frame, voice.StartedAt, "start", id, kind, source));
        if (voice.Stopped || path is null)
        {
            _voices.Remove(voice);
            return;
        }
        var player = new MediaPlayer();
        voice.Player = player;
        var late = TimeSpan.FromMilliseconds(_clock.ElapsedMilliseconds - voice.StartedAt);
        player.MediaOpened += (_, _) =>
        {
            if (voice.Stopped) return;
            // A track rendered after its frame starts where the game's would be by now.
            if (late > TimeSpan.FromMilliseconds(100)) player.Position = late;
            if (!_paused) player.Play();
        };
        player.MediaEnded += (_, _) => End(voice);
        player.MediaFailed += (_, _) => End(voice);
        player.Open(new Uri(path));
    }

    private Task<(string? Path, bool Music)> Render(int id)
    {
        if (_renders.TryGetValue(id, out var cached)) return cached;
        var resource = _game?.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Sound && item.ResourceId == id);
        var task = resource is null
            ? Task.FromResult<(string?, bool)>((null, false))
            : Task.Run<(string?, bool)>(() =>
            {
                try
                {
                    var music = _adlib.HasMusic(resource);
                    var wave = music
                        ? LoomAdlibAudio.ApplyOutputGain(_adlib.RenderWave(resource), 6)
                        : _speaker.RenderWave(resource);
                    var path = Path.Combine(Path.GetTempPath(), $"scummexplorer-scene-{Guid.NewGuid():N}.wav");
                    File.WriteAllBytes(path, wave);
                    return (path, music);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
                {
                    return (null, false);
                }
            });
        _renders[id] = task;
        return task;
    }
}

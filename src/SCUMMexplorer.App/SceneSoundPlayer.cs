using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// Plays the sound commands actually executed by a session's scripts, on playback frames and interactive actions,
/// from <see cref="ScummSession.SoundEvents"/> (LOOM's session, and since S6 sound, 16 October 2026, the generic play session of the verb-based games). Commands on an estimated preview route retain that label;
/// commands collected from unexecuted possible branches stay silent. Each executed <c>startSound</c> starts
/// its track on the frame that reached it; <c>stopSound</c> stops that track and <c>stopMusic</c> stops them all, as
/// ScummVM's <c>stopAllSounds</c> does. Tracks overlap, as the game's sound channels do.
/// Music and effects use the selected device's native rendition, without a fallback. Each rendition has its own chip;
/// overlap does not emulate shared OPL channel allocation. A track that is still being rendered when its frame comes
/// starts late, at the position the game would have reached.
/// This player has its own outputs: the room music panel's player is separate, and starting it stops these sounds.
/// </summary>
public sealed class SceneSoundPlayer
{
    /// <summary>
    /// A sound that keeps playing until a script stops it: once its wave reaches <paramref name="End"/> (null: the wave's end) it goes on from
    /// <paramref name="Restart"/>. A CD track with loop count 0xFF restarts from its beginning (S6 MonkeyCd); an iMUSE song from its loop point (S6 Monkey2).
    /// </summary>
    public sealed record Loop(TimeSpan Restart, TimeSpan? End);

    public enum SoundMode { None, PcSpeaker, Adlib, Roland, Pcjr }
    public SoundMode Mode { get; private set; } = SoundMode.Adlib;

    /// <summary>The AdLib rendition's default level; the room panel's slider can change it.</summary>
    public const double AdlibGainDb = 6;

    /// <summary>The MT-32 ROM images the Roland device needs (the user's own files); null until a folder with them is known.</summary>
    public Mt32RomSet? Roms { get; set; }

    /// <summary>True for a LOOM folder that carries Roland streams: the Roland upgrade (sounds 33 to 64) or a demo with its own Roland sounds (9 to 16, 29, 41).</summary>
    public static bool HasRolandSounds(GameLibrary game) =>
        game.Id == GameId.Loom && game.Resources.Where(item => item.Kind == ResourceKind.Sound).OrderBy(item => item.ResourceId != 33).Any(ScummRolandSong.IsRoland) ||
        HasV5RolandSounds(game);

    /// <summary>True for a v5 game (Monkey Island 2, Indiana Jones and the Fate of Atlantis) whose archive holds <c>ROL </c> sounds, the MT-32 version of its music.</summary>
    public static bool HasV5RolandSounds(GameLibrary game) =>
        game.Version == ScummVersion.V5 && game.V4Disks?.Disks.OfType<ScummV5Archive>().Any(archive => archive.ReadSounds().Any(sound => sound.ImuseRoland is not null)) == true;

    /// <summary>The sound card number the scripts see for a device: 4 for the Roland upgrade, which makes them choose its sounds; the other devices keep 0.</summary>
    public static int SoundCardFor(SoundMode mode) => mode == SoundMode.Roland ? LoomExplorationSession.RolandSoundCard : 0;

    public static string DeviceName(SoundMode mode) => mode switch { SoundMode.Adlib => "AdLib", SoundMode.Roland => "Roland MT-32", SoundMode.Pcjr => "PCjr/Tandy", _ => "PC speaker" };

    /// <summary>Changing device stops current voices; subsequent script commands use the new device.</summary>
    public void SetMode(SoundMode mode)
    {
        if (Mode == mode) return;
        StopAll("sound mode changed");
        Mode = mode;
        Log.Add(new(_frames, _clock.ElapsedMilliseconds, "mode", null, mode.ToString(), ""));
    }
    /// <summary>One line of what the player did, for the harness: the frame count, the time and the sound.</summary>
    public sealed record Entry(int Frame, long Milliseconds, string Action, int? SoundId, string Kind, string Source);

    private sealed class Voice
    {
        public required int SoundId { get; init; }
        public required long StartedAt { get; set; }
        public MediaPlayer? Player { get; set; }
        /// <summary>True while the track is still being rendered; the game's frames wait for it (see <see cref="IsPreparing"/>).</summary>
        public bool Preparing { get; set; }
        public bool Stopped { get; set; }
        public Loop? Loop { get; set; }
    }

    private readonly ScummAdlibAudio _adlib = new();
    private readonly ScummPcSpeakerAudio _speaker = new();
    private readonly Dictionary<(SoundMode Mode, int Id), Task<(string? Path, bool Music)>> _renders = [];
    private CancellationTokenSource _renderLifetime = new();
    private readonly List<Voice> _voices = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private GameLibrary? _game;
    private ScummSession? _session;
    // How a sound becomes a wave for a session whose game the built-in renderer does not cover (the browsed sound player's own rendering); null: built-in.
    private Func<GameResource, SoundMode, CancellationToken, (byte[] Wave, bool Music)>? _render;
    private Func<int, SoundMode, Loop?>? _loops;
    private int _seen;
    private int _frames;
    private bool _paused;

    public List<Entry> Log { get; } = [];

    /// <summary>
    /// True while a track the game just started is still being rendered. The caller holds the game's frames until it is false, so the
    /// music starts from its beginning, in step with the scene, instead of being cut by the time the rendering took.
    /// </summary>
    public bool IsPreparing => _voices.Any(voice => voice.Preparing && !voice.Stopped);

    /// <summary>Raised on the UI thread when <see cref="IsPreparing"/> may have changed, so the window can show that it is working.</summary>
    public event Action? PreparingChanged;

    /// <summary>Raised when a reached <c>startSound</c> is about to play, so the app's other player can stop.</summary>
    public event Action? Starting;

    /// <summary>A sound on an executed estimated route, for the UI's evidence tooltip.</summary>
    public event Action<string>? PreviewStarting;

    /// <summary>Follows <paramref name="session"/> from its next sound event on; a new session stops the old one's sounds.</summary>
    /// <param name="render">Renders a sound with the device's rendition (AdLib already at its output level); null uses the built-in LOOM renderer.</param>
    /// <param name="loops">Sounds that go on until a script stops them, and from where (<see cref="Loop"/>: a CD track with loop count 0xFF, S6 MonkeyCd;
    /// an iMUSE song that jumps back, S6 Monkey2), for the device that plays them; null: none.</param>
    public void Attach(GameLibrary game, ScummSession session, Func<GameResource, SoundMode, CancellationToken, (byte[] Wave, bool Music)>? render = null,
        Func<int, SoundMode, Loop?>? loops = null, bool replayReachedEvents = false)
    {
        StopAll("session replaced");
        _loops = loops;
        if (!ReferenceEquals(game, _game) || !Equals(render, _render))
        {
            RetireRenders();
            _game = game;
            _render = render;
        }
        _session = session;
        _seen = replayReachedEvents ? 0 : session.SoundEvents.Count;
        _frames = 0;
    }

    public bool Follows(ScummSession? session) => session is not null && ReferenceEquals(session, _session);

    /// <summary>Retires a game, including pending renders and its event cursor.</summary>
    public void Detach()
    {
        StopAll("another game was opened");
        _session = null;
        _game = null;
        _render = null;
        _loops = null;
        RetireRenders();
        _seen = 0;
        _paused = false;
    }

    private void RetireRenders()
    {
        _renderLifetime.Cancel();
        _renderLifetime.Dispose();
        _renderLifetime = new CancellationTokenSource();
        foreach (var pending in _renders.Values)
            _ = pending.ContinueWith(done =>
            {
                if (done.IsCompletedSuccessfully && done.Result.Path is { } path) DeleteRender(path);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _renders.Clear();
    }

    private static void DeleteRender(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Plays the sound events the session reached since the last call (one playback frame).</summary>
    public void Update()
    {
        if (_session is not { } session || _game is null) return;
        _frames++;
        // A looping song whose wave has reached its loop's end goes on from its loop point, as the game's player jumps back (S6 Monkey2).
        foreach (var voice in _voices)
            if (voice is { Loop.End: TimeSpan end, Player: { } looping } && looping.Position >= end)
            {
                looping.Position = voice.Loop.Restart;
                Log.Add(new(_frames, _clock.ElapsedMilliseconds, "loop", voice.SoundId, "", $"from {voice.Loop.Restart.TotalSeconds:0.###} s at {end.TotalSeconds:0.###} s"));
            }
        var events = session.SoundEvents;
        for (; _seen < events.Count; _seen++)
        {
            var item = events[_seen].Event;
            // Follow commands actually executed in the state preview, including its labelled estimated route.
            // Commands merely collected from an unresolved branch (Executed false) must stay silent.
            if (item.Certainty != ScummScriptEventCertainty.Reached && !item.Executed) continue;
            var source = $"{item.SourceLabel} +0x{item.Offset:X4} {item.Operation} {item.Operands}".TrimEnd();
            var estimated = item.Certainty != ScummScriptEventCertainty.Reached;
            if (estimated) source = "Estimated preview route: " + source;
            switch (item.Kind)
            {
                case ScummScriptEventKind.SoundStarted or ScummScriptEventKind.MusicStarted when item.TargetId is int id:
                    Stop(id);
                    Start(id, source, estimated);
                    break;
                case ScummScriptEventKind.SoundStopped when item.TargetId is int id:
                    Stop(id);
                    Log.Add(new(_frames, _clock.ElapsedMilliseconds, "stop", id, "", source));
                    break;
                case ScummScriptEventKind.MusicStopped:
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
        var wasPreparing = voice.Preparing;
        voice.Preparing = false;
        voice.Stopped = true;
        voice.Player?.Stop();
        voice.Player?.Close();
        _voices.Remove(voice);
        if (wasPreparing) PreparingChanged?.Invoke();
    }

    private async void Start(int id, string source, bool estimated)
    {
        if (Mode == SoundMode.None)
        {
            Log.Add(new(_frames, _clock.ElapsedMilliseconds, "muted", id, "No Sound", source));
            return;
        }
        var mode = Mode;
        var voice = new Voice { SoundId = id, StartedAt = _clock.ElapsedMilliseconds, Loop = _loops?.Invoke(id, mode) };
        _voices.Add(voice);
        var frame = _frames;
        var render = Render(id, mode);
        // A track that is not rendered yet holds the game (IsPreparing); its clock starts when the sound is ready.
        voice.Preparing = !render.IsCompleted;
        if (voice.Preparing) PreparingChanged?.Invoke();
        (string? Path, bool Music) rendered;
        try { rendered = await render; }
        catch (OperationCanceledException) { End(voice); return; }
        finally
        {
            var wasPreparing = voice.Preparing;
            voice.Preparing = false;
            if (wasPreparing) PreparingChanged?.Invoke();
        }
        var (path, music) = rendered;
        voice.StartedAt = _clock.ElapsedMilliseconds;
        var device = DeviceName(mode);
        var kind = path is null ? $"unavailable ({device})" : $"{(music ? "music" : "effect")} ({device})";
        Log.Add(new(frame, voice.StartedAt, estimated ? "preview start" : "start", id, kind, source));
        if (voice.Stopped || path is null)
        {
            _voices.Remove(voice);
            return;
        }
        Starting?.Invoke();
        if (estimated) PreviewStarting?.Invoke(source);
        var player = new MediaPlayer();
        voice.Player = player;
        var late = TimeSpan.FromMilliseconds(_clock.ElapsedMilliseconds - voice.StartedAt);
        player.MediaOpened += (_, _) =>
        {
            if (voice.Stopped) return;
            Log.Add(new(frame, _clock.ElapsedMilliseconds, "opened", id, kind,
                $"audio {player.HasAudio}, duration {(player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan.TotalSeconds : 0):0.###} s; {source}"));
            // A track rendered after its frame starts where the game's would be by now.
            if (late > TimeSpan.FromMilliseconds(100)) player.Position = late;
            if (!_paused) player.Play();
        };
        player.MediaEnded += (_, _) =>
        {
            if (!voice.Stopped && voice.Loop is { } loop)
            {
                player.Position = loop.Restart;
                Log.Add(new(_frames, _clock.ElapsedMilliseconds, "loop", id, kind, $"from {loop.Restart.TotalSeconds:0.###} s at the end"));
                player.Play();
                return;
            }
            End(voice);
        };
        player.MediaFailed += (_, error) =>
        {
            Log.Add(new(frame, _clock.ElapsedMilliseconds, "failed", id, kind, error.ErrorException.Message));
            End(voice);
        };
        player.Open(new Uri(path));
    }

    private Task<(string? Path, bool Music)> Render(int id, SoundMode mode)
    {
        if (_renders.TryGetValue((mode, id), out var cached)) return cached;
        var game = _game;
        var renderer = _render;
        var roms = Roms;
        var token = _renderLifetime.Token;
        var resource = game?.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Sound && item.ResourceId == id);
        // A sound the game's list does not show (a CD-track sound of Monkey Island CD has no card parts, so only its track is listed) still goes to the session's
        // own renderer, which knows it by number (S6 MonkeyCd).
        if (resource is null && renderer is not null && game is not null)
            resource = new GameResource($"Sound {id:00}", ResourceKind.Sound, game.FolderPath, id, "A sound the scripts started.", null, null);
        var task = resource is null
            ? Task.FromResult<(string?, bool)>((null, false))
            : Task.Run<(string?, bool)>(() =>
            {
                string? path = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    bool music;
                    byte[] wave;
                    if (renderer is not null)
                    {
                        (wave, music) = renderer(resource, mode, token);
                    }
                    else if (game?.V4Archive is { } vga)
                    {
                        // The CD game's sounds hold an AdLib and a PC speaker part; it has no Roland data.
                        if (mode == SoundMode.Roland || vga.ReadSounds().FirstOrDefault(item => item.Id == id) is not { } cdSound) return (null, false);
                        music = cdSound.IsMusic;
                        wave = mode == SoundMode.Adlib
                            ? ScummAdlibAudio.ApplyOutputGain(_adlib.RenderWave(cdSound.Adlib ?? throw new InvalidDataException("No AdLib part."), token, ScummProfile.For(game).AdlibTiming), AdlibGainDb)
                            : _speaker.RenderWave(cdSound.Speaker ?? throw new InvalidDataException("No PC speaker part."), 6, token);
                    }
                    else if (mode == SoundMode.Roland)
                    {
                        // Only the upgrade's own sounds have a Roland rendition; the scripts choose them when the sound card is 4.
                        if (roms is null || !ScummRolandSong.IsRoland(resource)) return (null, false);
                        var song = ScummRolandSong.Read(resource);
                        music = song.Duration.TotalSeconds > 3;
                        wave = new ScummMt32Audio(roms).RenderWave(song, token);
                    }
                    else
                    {
                        music = _adlib.HasMusic(resource);
                        wave = mode == SoundMode.Adlib
                            ? ScummAdlibAudio.ApplyOutputGain(_adlib.RenderWave(resource, ScummProfile.For(game!).AdlibTiming, token), AdlibGainDb)
                            : _speaker.RenderWave(resource, token);
                    }
                    token.ThrowIfCancellationRequested();
                    path = Path.Combine(Path.GetTempPath(), $"scummexplorer-scene-{Guid.NewGuid():N}.wav");
                    File.WriteAllBytes(path, wave);
                    token.ThrowIfCancellationRequested();
                    return (path, music);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException
                    or Mt32emu.Mt32EmuException or InvalidOperationException or DllNotFoundException)
                {
                    if (path is not null) DeleteRender(path);
                    return (null, false);
                }
            }, token);
        _renders[(mode, id)] = task;
        return task;
    }
}

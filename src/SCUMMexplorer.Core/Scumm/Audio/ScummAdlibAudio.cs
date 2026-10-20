using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The constants of ScummVM's <c>Player_AD</c> that differ between games (<c>player_ad.cpp</c>, <c>startMusic</c> and <c>updateMusic</c>):
/// LOOM's timer limit is 473, its tempo byte counts double and its event delays are shifted right by 2; the other game it plays
/// (Indiana Jones and the Last Crusade, EGA and 256 colours) uses 256, a plain tempo byte and a shift of 1.
/// <para>
/// <see cref="ImuseClock"/> (sound polish, 19 October 2026): ScummVM plays the AD music of every other game (Monkey Island 1 floppy and EGA) through iMUSE,
/// not <c>Player_AD</c> (<c>scumm.cpp</c> <c>setupMusic</c>: <c>Player_AD</c> only for LOOM and Indy3; <c>sound.cpp</c> <c>readSoundResourceSmallHeader</c>
/// converts the AD block with <c>convertADResource</c>; GitHub master read 19 October 2026). The conversion keeps the event bytes as one MIDI track at 480 ticks
/// a quarter, with a tempo of 500000 × 256 / the tempo byte microseconds a quarter, 160 ticks before the first event, and a jump back to that first event
/// (beat 1, tick 160) in place of a looping song's end; iMUSE's parser (<c>MidiParser::onTimer</c>, <c>setTempo</c>) plays it on the AdLib driver's 250 Hz
/// timer (<c>getBaseTempo</c> 4000 µs, speed 128). Only the clock is iMUSE's: the notes still go through this player's port of <c>Player_AD</c>'s voices.
/// </para>
/// </summary>
public sealed record ScummAdlibTiming(int TimerLimit, int TempoMultiplier, int DelayShift, bool ImuseClock = false)
{
    public static readonly ScummAdlibTiming Loom = new(473, 2, 2);
    public static readonly ScummAdlibTiming Standard = new(256, 1, 1);
    /// <summary>The AD music of a game ScummVM gives to iMUSE (Monkey Island 1 floppy and EGA): see <see cref="ImuseClock"/>.</summary>
    public static readonly ScummAdlibTiming ImuseConversion = new(256, 1, 1, ImuseClock: true);
}

/// <summary>Renders old-bundle LOOM AdLib music and effects through an OPL chip emulator.</summary>
public sealed partial class ScummAdlibAudio
{
    private const int SampleRate = 49716;
    private const int CallbackRate = 472;
    // A looping song renders at least this long; one whose first pass and loop take longer renders through its first loop restart (sound polish, 19 October 2026).
    private const int MaxSeconds = 180;
    private const int LoopTailSeconds = 10;
    private const int MaxLifetimeSeconds = 1800;
    private const int MaxReleaseSeconds = 2;
    private const int QuietSampleLevel = 8;
    private static readonly int[] OperatorOffsets = [0, 3, 1, 4, 2, 5, 8, 11, 9, 12, 10, 13, 16, 19, 17, 20, 18, 21];
    private static readonly int[] NoteFNumbers = [0x200, 0x21e, 0x23f, 0x261, 0x285, 0x2ab, 0x2d4, 0x300, 0x32e, 0x35e, 0x390, 0x3c7];
    private static readonly int[] RhythmBits = [0, 0x10, 8, 4, 2, 1];
    private static readonly int[] RhythmOperators = [0, 0, 20, 18, 21, 17];
    private static readonly int[] RhythmChannels = [0, 0, 7, 8, 8, 7];

    public bool HasMusic(GameResource resource)
    {
        try { return ReadTrack(resource) is { Length: >= 0x94 } track && track[2] == 0x80; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { return false; }
    }

    /// <summary>Estimates the first pass duration from the track's event clock without rendering PCM.</summary>
    public TimeSpan EstimateDuration(GameResource resource)
    {
        var data = ReadTrack(resource);
        if (data.Length < 0x94 || data[2] != 0x80)
            throw new NotSupportedException("This AdLib resource is not a music track.");
        var player = new MusicPlayer(data);
        var callbacks = 0;
        while (player.Active && !player.LoopBoundaryReached && callbacks < CallbackRate * MaxLifetimeSeconds)
        {
            player.Tick();
            callbacks++;
        }
        if (callbacks == 0 || player.Active && !player.LoopBoundaryReached || !player.Active && !player.EndedNormally)
            throw new InvalidDataException("AdLib music has no established first-pass duration.");
        return TimeSpan.FromSeconds((double)callbacks / CallbackRate);
    }

    /// <summary>Native driver status lifetime, excluding PCM envelope release and preview truncation.</summary>
    public ScummSoundLifetime ReadLifetime(GameResource resource)
    {
        try { return ReadDataLifetime(ReadTrack(resource)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return new(ScummSoundLifetimeKind.Unknown, null, false, ex.Message);
        }
    }

    /// <summary>
    /// The lifetime as <c>Player_AD</c> (ScummVM) runs it with the given driver constants, with the music timer's markers: the game's
    /// <c>VAR_MUSIC_TIMER</c> is <c>getMusicTimer() * timerFrequency / 240</c> (<c>Sound::updateMusicTimer</c>; 236.7 Hz for these games), the engine timer
    /// going up once every 121 timer steps. Markers are in 240 Hz ticks, as <see cref="ScummSoundClock.MusicTimer"/> counts.
    /// Effects have none.
    /// </summary>
    public ScummSoundLifetime ReadLifetime(GameResource resource, ScummAdlibTiming timing)
    {
        try { return ReadLifetime(ReadTrack(resource), timing); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return new(ScummSoundLifetimeKind.Unknown, null, false, ex.Message);
        }
    }

    /// <summary>
    /// The same for AdLib data given directly (a v4 disk's <c>AD</c> block body). With <see cref="ScummAdlibTiming.ImuseClock"/> the music timer is iMUSE's
    /// (<c>Player::getMusicTimer</c>: the parser's tick × 2 / 480, times the timer frequency over 240; 0 when no song plays) and is given as
    /// <see cref="ScummSoundLifetime.TimerValues"/>, which start again at the loop's restart.
    /// </summary>
    public ScummSoundLifetime ReadLifetime(byte[] data, ScummAdlibTiming timing)
    {
        try
        {
            var lifetime = ReadDataLifetime(data, timing);
            if (data.Length < 3 || data[2] != 0x80) return lifetime;
            var limit = lifetime.Kind == ScummSoundLifetimeKind.Finite ? lifetime.Callbacks!.Value : CallbackRate * MaxMarkerSeconds;
            var player = new MusicPlayer(data, timing);
            if (timing.ImuseClock)
            {
                var values = new List<ScummSoundTimerPoint> { new(0, 0) };
                for (var callbacks = 1; callbacks <= limit && player.Active && player.LoopBoundaries == 0; callbacks++)
                {
                    player.Tick();
                    var value = (ushort)(int)(player.PlayTick * 2 / Ppqn * DosTimerFrequency / 240.0);
                    if (values[^1].Value != value) values.Add(new(callbacks, value));
                }
                return lifetime with
                {
                    TimerValues = values.ToArray(),
                    LoopStartCallback = player.LoopBoundaries > 0 ? player.LoopTargetCallback : null,
                    LoopEndCallback = player.LoopBoundaries > 0 ? player.FirstLoopCallback : null
                };
            }
            var markers = new List<long>();
            for (var callbacks = 1; callbacks <= limit && player.Active; callbacks++)
            {
                player.Tick();
                if ((int)(player.EngineMusicTimer * DosTimerFrequency / 240.0) >= markers.Count + 1) markers.Add(callbacks * 240L / CallbackRate);
            }
            // Player_AD does not reset its timer when a song stops (only startMusic does).
            return lifetime with { MarkerTicks = markers.ToArray(), KeepsMusicTimer = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return new(ScummSoundLifetimeKind.Unknown, null, false, ex.Message);
        }
    }

    private const int MaxMarkerSeconds = 600;

    /// <summary>iMUSE's ticks a quarter for a converted AD song (<c>convertADResource</c>: a constant 480).</summary>
    private const int Ppqn = 480;

    /// <summary>The iMUSE AdLib driver's timer step (<c>MidiDriver_ADLIB::getBaseTempo</c>: 1000000 / 250 µs), at the default speed 128.</summary>
    private const int ImuseTimerMicroseconds = 4000;

    /// <summary>ScummVM's <c>_timerFrequency</c> for a DOS game of version 2 to 4 (<c>setTimerAndShakeFrequency</c>): the PIT's 1193182 Hz over 5041.</summary>
    private const double DosTimerFrequency = 1193182.0 / 5041.0;

    /// <summary>The same for AdLib data given directly (the part after the resource header; the CD game's <c>AD</c> block body).</summary>
    public ScummSoundLifetime ReadDataLifetime(byte[] data, ScummAdlibTiming? timing = null)
    {
        try
        {
            if (data.Length < 3) throw new InvalidDataException("The AdLib resource is too short.");
            if (data[2] == 0x80)
            {
                var player = new MusicPlayer(data, timing);
                for (var callbacks = 1; callbacks <= CallbackRate * MaxLifetimeSeconds; callbacks++)
                {
                    player.Tick();
                    if (!player.Active)
                        return player.EndedNormally
                            ? new(ScummSoundLifetimeKind.Finite, callbacks, true)
                            : new(ScummSoundLifetimeKind.Unknown, null, true, "music has no native end marker");
                    // Validate one full repeated pass, including the loop target's commands.
                    if (player.LoopBoundaries >= 2)
                        return new(ScummSoundLifetimeKind.Looping, null, true);
                }
                return new(ScummSoundLifetimeKind.Unknown, null, true, "music exceeds the bounded lifetime analysis");
            }
            var effect = new EffectPlayer(data);
            for (var callbacks = 1; callbacks <= CallbackRate * MaxLifetimeSeconds; callbacks++)
            {
                effect.Tick();
                // Player_AD stops the resource on the next 118 Hz update after all channels go off.
                if (!effect.Active) return new(ScummSoundLifetimeKind.Finite, callbacks + 4, false);
                if (effect.LoopBoundaryReached) return new(ScummSoundLifetimeKind.Looping, null, false);
            }
            return new(ScummSoundLifetimeKind.Unknown, null, false, "effect repeats or exceeds the bounded lifetime analysis");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return new(ScummSoundLifetimeKind.Unknown, null, false, ex.Message);
        }
    }

    /// <summary>Renders an old-bundle sound's AdLib part with LOOM's driver constants.</summary>
    public byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default) =>
        RenderWave(ReadTrack(resource), cancellationToken);

    /// <summary>Renders an old-bundle sound's AdLib part with the game's driver constants (<see cref="ScummProfile.AdlibTiming"/>: Indy3's are not LOOM's, R-13.9).</summary>
    public byte[] RenderWave(GameResource resource, ScummAdlibTiming timing, CancellationToken cancellationToken = default) =>
        RenderWave(ReadTrack(resource), cancellationToken, timing);

    /// <summary>Where an old-bundle sound's AdLib song loops (<see cref="ReadLoop(byte[], ScummAdlibTiming)"/>), or null.</summary>
    public ScummImuseLoop? ReadLoop(GameResource resource, ScummAdlibTiming timing)
    {
        try { return ReadLoop(ReadTrack(resource), timing); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { return null; }
    }

    /// <summary>
    /// Where a looping AdLib song goes on (sound polish, 19 October 2026): the driver jumps back once its first pass ends (<see cref="ScummImuseLoop.EndSeconds"/>,
    /// <c>Player_AD</c> at the end-of-track event, iMUSE at the jump that replaced it) and plays on from the loop target, which it first reached at
    /// <see cref="ScummImuseLoop.RestartSeconds"/> of the rendered wave. Null for a song that ends, an effect, or a song whose first pass outlasts the analysis.
    /// </summary>
    public ScummImuseLoop? ReadLoop(byte[] data, ScummAdlibTiming timing)
    {
        try
        {
            if (data.Length < 0x94 || data[2] != 0x80) return null;
            var player = new MusicPlayer(data, timing);
            for (var callbacks = 1; callbacks <= CallbackRate * MaxLifetimeSeconds && player.Active; callbacks++)
            {
                player.Tick();
                if (player.LoopBoundaries > 0)
                    return player.LoopTargetCallback is int start && callbacks > start
                        ? new((double)start / CallbackRate, (double)callbacks / CallbackRate) : null;
            }
            return null;
        }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>
    /// Renders AdLib data given directly: the bytes after the resource header (old bundle: after 4 bytes; a v4 disk's <c>AD</c> block body), with the game's
    /// driver constants (LOOM's when null). A song that ends renders to its end; a looping one for three minutes, or through its first restart and ten
    /// seconds more when that takes longer (sound polish, 19 October 2026: a long loop was cut at three minutes).
    /// </summary>
    public byte[] RenderWave(byte[] data, CancellationToken cancellationToken = default, ScummAdlibTiming? timing = null)
    {
        if (data.Length < 3) throw new InvalidDataException("The AdLib resource is too short.");
        if (data[2] != 0x80) return RenderEffectWave(data, cancellationToken);
        if (data.Length < 0x94) throw new InvalidDataException("AdLib music header is incomplete.");
        timing ??= ScummAdlibTiming.Loom;
        var player = new MusicPlayer(data, timing);
        var lifetime = ReadDataLifetime(data, timing);
        var maxCallbacks = lifetime.Kind == ScummSoundLifetimeKind.Finite ? lifetime.Callbacks!.Value : CallbackRate * MaxSeconds;
        if (lifetime.Kind == ScummSoundLifetimeKind.Looping && ReadLoop(data, timing) is { } loop)
            maxCallbacks = Math.Min(CallbackRate * MaxLifetimeSeconds,
                Math.Max(maxCallbacks, (int)Math.Ceiling(loop.EndSeconds * CallbackRate) + CallbackRate * LoopTailSeconds));
        var samples = new List<short>();
        var frameRemainder = 0;
        for (var callback = 0; callback < maxCallbacks && player.Active; callback++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            player.Tick();
            frameRemainder += SampleRate;
            var frames = frameRemainder / CallbackRate;
            frameRemainder %= CallbackRate;
            var buffer = new short[frames * 2];
            player.Chip.GenerateStream(buffer);
            samples.AddRange(buffer);
        }
        if (player.EndedNormally)
        {
            // The game keeps clocking the OPL after the track ends, so let
            // keyed-off envelopes decay before closing the standalone WAV.
            var quietCallbacks = 0;
            for (var callback = 0; callback < CallbackRate * MaxReleaseSeconds; callback++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frameRemainder += SampleRate;
                var frames = frameRemainder / CallbackRate;
                frameRemainder %= CallbackRate;
                var buffer = new short[frames * 2];
                player.Chip.GenerateStream(buffer);
                samples.AddRange(buffer);
                quietCallbacks = buffer.All(sample => Math.Abs((int)sample) <= QuietSampleLevel)
                    ? quietCallbacks + 1 : 0;
                if (quietCallbacks >= CallbackRate / 5) break;
            }
            if (quietCallbacks < CallbackRate / 5)
            {
                // Some OPL envelopes linger indefinitely; bound the file and
                // avoid a click if the maximum release time was reached.
                var fadeSamples = Math.Min(samples.Count, SampleRate / 10 * 2);
                for (var i = 0; i < fadeSamples; i++)
                {
                    var index = samples.Count - fadeSamples + i;
                    samples[index] = (short)(samples[index] * (fadeSamples - 1 - i) / fadeSamples);
                }
            }
        }
        if (samples.Count == 0 || samples.All(sample => sample == 0))
            throw new InvalidDataException("The AdLib music produced no audio.");
        return ToWave(samples);
    }

    /// <summary>Applies an AdLib playback gain while keeping the WAV below full scale.</summary>
    public static byte[] ApplyOutputGain(byte[] wave, double gainDb)
    {
        ArgumentNullException.ThrowIfNull(wave);
        if (!double.IsFinite(gainDb) || gainDb is < 0 or > 24)
            throw new ArgumentOutOfRangeException(nameof(gainDb), "AdLib gain must be between 0 and 24 dB.");
        if (wave.Length < 44 ||
            !wave.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !wave.AsSpan(8, 4).SequenceEqual("WAVE"u8) ||
            !wave.AsSpan(12, 4).SequenceEqual("fmt "u8) ||
            !wave.AsSpan(36, 4).SequenceEqual("data"u8) ||
            BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(20, 2)) != 1 ||
            BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(34, 2)) != 16)
            throw new InvalidDataException("AdLib gain expects a 16-bit PCM WAV.");

        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(40, 4));
        if (dataLength < 0 || dataLength % 2 != 0 || dataLength > wave.Length - 44)
            throw new InvalidDataException("The AdLib WAV has an invalid sample-data length.");

        var peak = 0;
        for (var offset = 44; offset < 44 + dataLength; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(offset, 2));
            peak = Math.Max(peak, Math.Abs((int)sample));
        }

        var requestedGain = Math.Pow(10, gainDb / 20);
        const double PeakCeiling = 30000;
        var safeGain = peak == 0 ? requestedGain : Math.Min(requestedGain, PeakCeiling / peak);
        var output = (byte[])wave.Clone();
        for (var offset = 44; offset < 44 + dataLength; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(offset, 2));
            var adjusted = (short)Math.Clamp((int)Math.Round(sample * safeGain), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(offset, 2), adjusted);
        }
        return output;
    }

    private static byte[] ReadTrack(GameResource resource)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null || resource.ContainerFileNumber is null)
            throw new NotSupportedException("This is not an indexed old-bundle LOOM sound.");
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xff;
        var offset = resource.Offset.Value;
        if (offset < 0 || offset > file.Length - 4) throw new InvalidDataException("Invalid sound offset.");
        var speakerLength = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset, 2));
        if (speakerLength < 22 || speakerLength > file.Length - offset ||
            BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset + 6, 2)) != 22)
            throw new NotSupportedException("No DOS PC speaker / AdLib bundle at this sound index.");
        var adlibOffset = offset + speakerLength;
        if (speakerLength < 4 || adlibOffset < 0 || adlibOffset > file.Length - 4)
            throw new InvalidDataException("No AdLib resource follows the PC speaker resource.");
        var adlibLength = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(adlibOffset, 2));
        if (adlibLength < 8 || adlibLength > file.Length - adlibOffset)
            throw new InvalidDataException("Invalid AdLib resource length.");
        return file.AsSpan(adlibOffset + 4, adlibLength - 4).ToArray();
    }

    private static byte[] ToWave(List<short> samples)
    {
        using var stream = new MemoryStream(44 + samples.Count * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Count * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 4);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Count * 2);
        foreach (var sample in samples) writer.Write(sample);
        return stream.ToArray();
    }

    private sealed class MusicPlayer
    {
        private readonly byte[] _data;
        private readonly int[] _instruments = new int[16];
        private readonly Voice[] _voices = Enumerable.Range(0, 9).Select(_ => new Voice()).ToArray();
        private readonly int _voiceCount;
        private readonly int _loopStart;
        private readonly bool _loop;
        private int _position = 0x93;
        private int _nextEvent = 40;
        private int _timer;
        private int _ticks;
        private int _rhythm;

        public NukedOplChip Chip { get; }
        public bool Active { get; private set; } = true;
        public bool EndedNormally { get; private set; }
        public int LoopBoundaries { get; private set; }
        public bool LoopBoundaryReached => LoopBoundaries != 0;

        private readonly ScummAdlibTiming _timing;
        private int _internalTimer;

        /// <summary>Player_AD's <c>_engineMusicTimer</c>: what <c>getMusicTimer</c> reports (the game's variable sees a quarter of it).</summary>
        public int EngineMusicTimer { get; private set; }

        /// <summary>The 472 Hz callbacks run so far.</summary>
        public int Callbacks { get; private set; }

        /// <summary>The callback in which the loop target's event first played (the first event, for the iMUSE clock).</summary>
        public int? LoopTargetCallback { get; private set; }

        /// <summary>The callback in which the song first jumped back.</summary>
        public int? FirstLoopCallback { get; private set; }

        // The iMUSE clock (ScummAdlibTiming.ImuseClock): MidiParser's position in microseconds and ticks, the delta before the next event, and the 250 Hz
        // driver timer's phase against the 472 Hz callbacks (in callbacks × microseconds).
        private long _imusePhase;
        private long _playTime, _lastEventTime, _lastEventTick, _pendingDelta, _psecPerTick;

        /// <summary>The iMUSE parser's tick (<c>MidiParser::_position._playTick</c>), counted from the song's start and back to 160 at its loop.</summary>
        public long PlayTick { get; private set; }

        private int LoopTarget => _timing.ImuseClock ? 0x93 : _loopStart;

        public MusicPlayer(byte[] data, ScummAdlibTiming? timing = null)
        {
            _timing = timing ?? ScummAdlibTiming.Loom;
            Chip = new NukedOplChip(SampleRate);
            _data = data;
            Write(0x01, 0x20);
            if (data.Length <= 0x93) throw new InvalidDataException("AdLib music header is incomplete.");
            var instrumentCount = data[10];
            if (instrumentCount > 8 || data.Length < 19 + instrumentCount * 16)
                throw new InvalidDataException("AdLib instrument directory is invalid.");
            for (var i = 0; i < instrumentCount; i++)
            {
                var id = data[11 + i] - 1;
                if (id is < 0 or >= 16) continue;
                _instruments[id] = i * 16 + 19;
                if (Read(_instruments[id] + 13) != 0) _rhythm = 0x20;
            }
            _voiceCount = _rhythm == 0 ? 9 : 6;
            Write(0xbd, _rhythm);
            _ticks = data[3] * _timing.TempoMultiplier;
            if (_ticks == 0) throw new InvalidDataException("AdLib music has zero tempo.");
            _loop = data[4] == 0;
            _loopStart = _position + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5, 2));
            if (_timing.ImuseClock)
            {
                // convertADResource: dw = 500000 * 256 / ticks, written as a three-byte tempo event; the track starts ppqn / 3 ticks later.
                SetImuseTempo((500000 * 256 / data[3]) & 0xFFFFFF);
                _pendingDelta = Ppqn / 3;
            }
        }

        public void Tick()
        {
            Callbacks++;
            if (_timing.ImuseClock)
            {
                // The AdLib driver's timer runs at 250 Hz (4000 µs a step); this player's callbacks at 472 Hz.
                _imusePhase += 1_000_000;
                while (_imusePhase >= (long)ImuseTimerMicroseconds * CallbackRate && Active)
                {
                    _imusePhase -= (long)ImuseTimerMicroseconds * CallbackRate;
                    ImuseTimer();
                }
                return;
            }
            _timer += _ticks;
            if (_timer < _timing.TimerLimit) return;
            _timer -= _timing.TimerLimit;
            if (++_internalTimer > 120) { _internalTimer = 0; EngineMusicTimer++; }
            if (--_nextEvent > 0) return;
            for (var commands = 0; commands < 256 && Active; commands++)
            {
                var looped = ProcessEvent();
                if (!Active) return;
                if (looped) continue;
                var delay = ReadDelay();
                if (!Active) return;
                if (delay == 0) continue;
                _nextEvent = Math.Max(1, delay >> _timing.DelayShift);
                return;
            }
            if (Active) throw new InvalidDataException("AdLib music has too many simultaneous commands.");
        }

        /// <summary>
        /// One step of iMUSE's parser (<c>MidiParser::onTimer</c>): every event whose time has come by the step's end plays; a jump back (the converted
        /// song's loop) ends the step, and the parser goes on from the jump's time.
        /// </summary>
        private void ImuseTimer()
        {
            var endTime = _playTime + ImuseTimerMicroseconds;
            for (var commands = 0; Active; commands++)
            {
                if (commands >= 4096) throw new InvalidDataException("AdLib music has too many simultaneous commands.");
                var eventTime = _lastEventTime + _pendingDelta * _psecPerTick;
                if (eventTime > endTime) break;
                _lastEventTime = eventTime;
                _lastEventTick += _pendingDelta;
                PlayTick = _lastEventTick;
                var looped = ProcessEvent();
                if (!Active) return;
                if (looped)
                {
                    // jumpToTick(beat 1, tick 160): the parse stops here and goes on from the jump's time.
                    _playTime = _lastEventTime;
                    _lastEventTick = PlayTick = Ppqn / 3;
                    _pendingDelta = 0;
                    return;
                }
                _pendingDelta = ReadDelay();
                if (!Active) return;
            }
            _playTime = endTime;
            PlayTick = (endTime - _lastEventTime) / _psecPerTick + _lastEventTick;
        }

        private void SetImuseTempo(long microsecondsPerQuarter) => _psecPerTick = Math.Max(1, (microsecondsPerQuarter + (Ppqn >> 2)) / Ppqn);

        /// <summary>The delay after an event: 0, or the AD format's one- or two-byte count (the same bytes as a MIDI variable-length delta).</summary>
        private int ReadDelay()
        {
            if (_position >= _data.Length) { End(); return 0; }
            if (_data[_position] == 0) { _position++; return 0; }
            var delay = Read(_position++);
            if ((delay & 0x80) != 0) delay = ((delay & 0x7f) << 7) | Read(_position++);
            return delay;
        }

        /// <summary>Plays the event at the read position; true when it was the song's end and the song jumped back.</summary>
        private bool ProcessEvent()
        {
            if (_position >= _data.Length) { End(); return false; }
            if (_position == LoopTarget && LoopTargetCallback is null) LoopTargetCallback = Callbacks;
            var command = Read(_position++);
            {
                if (command == 0xff)
                {
                    var meta = Read(_position++);
                    if (meta == 0x2f)
                    {
                        if (!_loop) { End(true); return false; }
                        var target = LoopTarget;
                        if (target < 0x93 || target >= _data.Length)
                            throw new InvalidDataException("AdLib music loop target is outside the event stream.");
                        LoopBoundaries++;
                        FirstLoopCallback ??= Callbacks;
                        _position = target;
                        return true;
                    }
                    if (meta == 0x58) { _position += 5; return false; }
                    var length = Read(_position++);
                    if (meta == 0x51 && length >= 3)
                    {
                        // _position already points past the length byte, at the first payload byte.
                        if (_timing.ImuseClock)
                            // iMUSE reads a standard MIDI tempo: three bytes of microseconds a quarter (MidiParser_SMF).
                            SetImuseTempo((Read(_position) << 16) | (Read(_position + 1) << 8) | Read(_position + 2));
                        else
                        {
                            // Player_AD reads the first two payload bytes as its tempo word.
                            var timing = (Read(_position) << 8) | Read(_position + 1);
                            if (timing > 0) _ticks = 0x73000 / timing;
                        }
                    }
                    _position += length;
                }
                else
                {
                    var instrument = _instruments[command & 0x0f];
                    var note = Read(_position++);
                    _position++; // velocity is unused by this driver
                    if ((command & 0xf0) == 0x90)
                    {
                        if (instrument > 0)
                        {
                            var rhythmId = Read(instrument + 13);
                            if (rhythmId is > 0 and < 6) PlayRhythm(rhythmId, instrument);
                            else if (rhythmId == 0) PlayNote(command, note, instrument);
                        }
                    }
                    else if ((command & 0xf0) == 0x80)
                    {
                        var voice = Array.FindIndex(_voices, 0, _voiceCount, v => v.Event == command + 0x10 && v.Note == note);
                        if (voice >= 0) StopNote(voice);
                        else if (instrument > 0)
                        {
                            var rhythmId = Read(instrument + 13);
                            if (rhythmId is > 0 and < 6) { _rhythm &= ~RhythmBits[rhythmId]; Write(0xbd, _rhythm); }
                        }
                    }
                    else { End(); return false; }
                }
            }
            return false;
        }

        private void PlayNote(int command, int note, int instrument)
        {
            var channel = Array.FindIndex(_voices, 0, _voiceCount, v => v.Event == 0);
            if (channel < 0) return;
            SetupChannel(channel, instrument);
            // setupFrequency takes an int8: the note wraps as a signed byte, then clamps at 0.
            var pitch = Math.Max(0, (int)unchecked((sbyte)((sbyte)note - 31)));
            var octave = pitch / 12;
            var fNumber = NoteFNumbers[pitch % 12];
            Write(0xa0 + channel, fNumber & 0xff);
            Write(0xb0 + channel, ((octave << 2) | (fNumber >> 8) | 0x20) & 0xff);
            _voices[channel].Event = command;
            _voices[channel].Note = note;
        }

        private void StopNote(int channel)
        {
            var register = 0xb0 + channel;
            Write(register, _voices[channel].B0 & ~0x20);
            _voices[channel].Event = 0;
        }

        private void PlayRhythm(int id, int instrument)
        {
            if (id == 1)
            {
                SetupChannel(6, instrument);
                Write(0xa6, Read(instrument));
                Write(0xb6, Read(instrument + 1) & ~0x20);
            }
            else
            {
                var channel = RhythmChannels[id];
                SetupOperator(RhythmOperators[id], instrument + 8);
                Write(0xa0 + channel, Read(instrument));
                Write(0xb0 + channel, Read(instrument + 1) & ~0x20);
                Write(0xc0 + channel, Read(instrument + 2));
            }
            _rhythm |= RhythmBits[id];
            Write(0xbd, _rhythm);
        }

        private void SetupChannel(int channel, int instrument)
        {
            Write(0xc0 + channel, Read(instrument + 2));
            SetupOperator(OperatorOffsets[channel * 2], instrument + 3);
            SetupOperator(OperatorOffsets[channel * 2 + 1], instrument + 8);
        }

        private void SetupOperator(int offset, int instrument)
        {
            Write(0x20 + offset, Read(instrument));
            Write(0x40 + offset, Read(instrument + 1));
            Write(0x60 + offset, Read(instrument + 2));
            Write(0x80 + offset, Read(instrument + 3));
            Write(0xe0 + offset, Read(instrument + 4));
        }

        private int Read(int position) => position >= 0 && position < _data.Length
            ? _data[position] : throw new InvalidDataException("AdLib event extends past the resource.");

        private void Write(int register, int value)
        {
            Chip.WriteRegister((ushort)register, (byte)value);
            if (register is >= 0xb0 and <= 0xb8) _voices[register - 0xb0].B0 = value;
        }

        private void End(bool normal = false)
        {
            for (var i = 0; i < _voiceCount; i++) if (_voices[i].Event != 0) StopNote(i);
            _rhythm = 0;
            Write(0xbd, 0);
            Active = false;
            EndedNormally = normal;
        }

        private sealed class Voice
        {
            public int Event;
            public int Note;
            public int B0;
        }
    }
}

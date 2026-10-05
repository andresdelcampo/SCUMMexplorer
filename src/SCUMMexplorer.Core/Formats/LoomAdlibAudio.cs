using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>Renders old-bundle LOOM AdLib music and effects through an OPL chip emulator.</summary>
public sealed partial class LoomAdlibAudio
{
    private const int SampleRate = 49716;
    private const int CallbackRate = 472;
    private const int MaxSeconds = 180;
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
    public LoomSoundLifetime ReadLifetime(GameResource resource)
    {
        try
        {
            var data = ReadTrack(resource);
            if (data[2] == 0x80)
            {
                var player = new MusicPlayer(data);
                for (var callbacks = 1; callbacks <= CallbackRate * MaxLifetimeSeconds; callbacks++)
                {
                    player.Tick();
                    if (!player.Active)
                        return player.EndedNormally
                            ? new(LoomSoundLifetimeKind.Finite, callbacks, true)
                            : new(LoomSoundLifetimeKind.Unknown, null, true, "music has no native end marker");
                    // Validate one full repeated pass, including the loop target's commands.
                    if (player.LoopBoundaries >= 2)
                        return new(LoomSoundLifetimeKind.Looping, null, true);
                }
                return new(LoomSoundLifetimeKind.Unknown, null, true, "music exceeds the bounded lifetime analysis");
            }
            var effect = new EffectPlayer(data);
            for (var callbacks = 1; callbacks <= CallbackRate * MaxLifetimeSeconds; callbacks++)
            {
                effect.Tick();
                // Player_AD stops the resource on the next 118 Hz update after all channels go off.
                if (!effect.Active) return new(LoomSoundLifetimeKind.Finite, callbacks + 4, false);
                if (effect.LoopBoundaryReached) return new(LoomSoundLifetimeKind.Looping, null, false);
            }
            return new(LoomSoundLifetimeKind.Unknown, null, false, "effect repeats or exceeds the bounded lifetime analysis");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            return new(LoomSoundLifetimeKind.Unknown, null, false, ex.Message);
        }
    }

    public byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default)
    {
        var data = ReadTrack(resource);
        if (data[2] != 0x80) return RenderEffectWave(data, cancellationToken);
        if (data.Length < 0x94) throw new InvalidDataException("AdLib music header is incomplete.");
        var player = new MusicPlayer(data);
        var lifetime = ReadLifetime(resource);
        var maxCallbacks = lifetime.Kind == LoomSoundLifetimeKind.Finite ? lifetime.Callbacks!.Value : CallbackRate * MaxSeconds;
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

        public MusicPlayer(byte[] data)
        {
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
            _ticks = data[3] * 2;
            if (_ticks == 0) throw new InvalidDataException("AdLib music has zero tempo.");
            _loop = data[4] == 0;
            _loopStart = _position + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5, 2));
        }

        public void Tick()
        {
            _timer += _ticks;
            if (_timer < 473) return;
            _timer -= 473;
            if (--_nextEvent > 0) return;
            for (var commands = 0; commands < 256 && Active; commands++)
            {
                if (_position >= _data.Length) { End(); return; }
                var command = Read(_position++);
                if (command == 0xff)
                {
                    var meta = Read(_position++);
                    if (meta == 0x2f)
                    {
                        if (!_loop) { End(true); return; }
                        if (_loopStart < 0x93 || _loopStart >= _data.Length)
                            throw new InvalidDataException("AdLib music loop target is outside the event stream.");
                        LoopBoundaries++;
                        _position = _loopStart;
                        continue;
                    }
                    if (meta == 0x58) { _position += 5; goto afterCommand; }
                    var length = Read(_position++);
                    if (meta == 0x51 && length >= 3)
                    {
                        // The LOOM driver reads the first two payload bytes as its tempo word.
                        // _position already points past the length byte, at that first byte.
                        var timing = (Read(_position) << 8) | Read(_position + 1);
                        if (timing > 0) _ticks = 0x73000 / timing;
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
                    else { End(); return; }
                }
                afterCommand:
                if (_position >= _data.Length) { End(); return; }
                if (_data[_position] == 0) { _position++; continue; }
                var delay = Read(_position++);
                if ((delay & 0x80) != 0) delay = ((delay & 0x7f) << 7) | Read(_position++);
                _nextEvent = Math.Max(1, delay >> 2);
                return;
            }
            if (Active) throw new InvalidDataException("AdLib music has too many simultaneous commands.");
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

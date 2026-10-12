// PC speaker player of the classic SCUMM v1 games, ported from ScummVM's Player_V1 (speaker half) and the
// square generator and low-pass filter of Player_V2 (https://github.com/scummvm/scummvm/tree/master/engines/scumm/players).
// Copyright (C) ScummVM contributors. SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Plays the PC speaker sounds of classic Maniac Mansion (ScummVM <c>Player_V1</c>). A sound is a length word, two more bytes, the priority byte (bit 7: restartable)
/// at 6, then from 10 a list of chunks, each a type word followed by its own words: 0 notes (a tick length, then pairs of duration and divisor), 1 a frequency sweep,
/// 2 and 3 a sweep of the tick length (3 with a noise mask), 0xFFFE sets the repeat point, 0xFFFD jumps to it, 0xFFFF ends. The speaker is one square wave; a tick
/// lasts <c>mplex</c> cycles of the 1.193 MHz timer.
/// </summary>
public sealed class ScummPcSpeakerV1Audio
{
    private const int SampleRate = 44100; // ScummVM's mixer rate: at 22050 the noise and fast sweeps alias into harsh distortion
    private const int FixShift = 16;
    private const int MaxSamples = SampleRate * 180;
    private const int MaxTicks = 4_000_000;
    private const int VolumeLevel0 = (int)(255 * 128.0 / 3.0);

    public byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null)
            throw new NotSupportedException("This is not an indexed sound resource.");
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        return RenderWave(Slice(file, resource.Offset.Value), cancellationToken);
    }

    /// <summary>Whether the data is in the v2 player's format (a first channel pointer of 22 at byte 6, the others empty) rather than this one's.</summary>
    public static bool IsV2Format(byte[] data) =>
        data.Length >= 12 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2)) == 22 && data[8] == 0 && data[9] == 0 && data[10] == 0 && data[11] == 0;

    /// <summary>The header of this format: three zero bytes after the priority byte and a known type for the first chunk.</summary>
    public static bool LooksLikeV1(byte[] data)
    {
        if (data.Length < 12 || data[7] != 0 || data[8] != 0 || data[9] != 0) return false;
        var type = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10, 2));
        return type <= 3 || type >= 0xFFFC;
    }

    /// <summary>Renders a sound of either format (Zak McKracken and the enhanced games use the v2 player's, classic Maniac Mansion this one's).</summary>
    public static byte[] RenderOlder(GameResource resource, ScummPcSpeakerAudio v2Player, CancellationToken cancellationToken = default)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null)
            throw new NotSupportedException("This is not an indexed sound resource.");
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        var data = Slice(file, resource.Offset.Value);
        return IsV2Format(data) ? v2Player.RenderWave(resource, cancellationToken) : new ScummPcSpeakerV1Audio().RenderWave(data, cancellationToken);
    }

    /// <summary>The resource at <paramref name="offset"/> of a decoded room file, cut at its length word.</summary>
    public static byte[] Slice(byte[] file, int offset)
    {
        if (offset < 0 || offset > file.Length - 12) throw new InvalidDataException("The sound offset is outside its LFL file.");
        var size = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset, 2));
        if (size < 12 || size > file.Length - offset) throw new InvalidDataException("The sound resource has an invalid length.");
        return file.AsSpan(offset, size).ToArray();
    }

    /// <summary>Whether the data plays at least one note, tried without making any sound (for listing the sounds of a game).</summary>
    public static bool HasNotes(byte[] data)
    {
        try
        {
            var player = new Player(data);
            for (var tick = 0; tick < 20000 && player.Running; tick++)
            {
                if (player.Audible) return true;
                player.NextTick();
            }
            return player.Audible;
        }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException) { return false; }
    }

    public byte[] RenderWave(byte[] data, CancellationToken cancellationToken = default)
    {
        var player = new Player(data);
        var samples = new List<short>();
        var heard = false;
        for (var tick = 0; player.Running && tick < MaxTicks && samples.Count < MaxSamples; tick++)
        {
            if ((tick & 0xFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var count = player.StartTick();
            heard |= player.Audible;
            player.Generate(samples, count);
        }
        if (!heard) throw new NotSupportedException("This resource has no playable PC speaker notes.");
        player.Settle(samples);
        using var stream = new MemoryStream(44 + samples.Count * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Count * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Count * 2);
        foreach (var sample in samples) writer.Write(sample);
        return stream.ToArray();
    }

    private sealed class Player
    {
        private readonly byte[] _data;
        private readonly uint _mplexStep = (uint)(((long)SampleRate << FixShift) / 1193000);
        private readonly uint _updateStep = (uint)(((long)SampleRate << FixShift) / (1193000 * 2));
        private readonly uint _decay;
        private int _nextChunk = -1;     // -1: finished
        private int _repeatChunk;
        private uint _chunkType;
        private uint _tickLength;
        private uint _nextTick;
        private uint _freq;
        private int _forcedLevel;
        private ushort _randomLsr;
        private uint _timeLeft, _start, _end, _repeatCounter;
        private int _delta;
        private int _level;
        private int _timerCount;
        private bool _timerOutput;

        public Player(byte[] data)
        {
            _data = data;
            var decay = 0xa000u;
            for (var i = 0; (SampleRate << i) < 30000; i++) decay = decay * decay / 65536;
            _decay = decay;
            if (data.Length < 12) throw new InvalidDataException("The v1 sound is too short.");
            _repeatChunk = _nextChunk = 10;
            ParseChunk();
        }

        public bool Running => _nextChunk >= 0;

        /// <summary>The speaker is sounding: a note, or the click of a forced level.</summary>
        public bool Audible => _freq != 0 || _forcedLevel != 0;

        private ushort W(int position)
        {
            if (position < 0 || position + 2 > _data.Length) throw new InvalidDataException("The v1 sound runs past its end.");
            return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(position, 2));
        }

        private void SetMplex(uint mplex)
        {
            if (mplex == 0) mplex = 65536;
            _tickLength = unchecked(_mplexStep * mplex);
        }

        /// <summary>Runs the next tick's command and returns how many samples that tick lasts (the fraction carries over, as in readBuffer).</summary>
        public int StartTick()
        {
            if ((_nextTick >> FixShift) == 0)
            {
                _nextTick += _tickLength;
                NextTick();
            }
            var step = (int)(_nextTick >> FixShift);
            _nextTick -= (uint)step << FixShift;
            return step;
        }

        public void NextTick()
        {
            if (_nextChunk < 0) return;
            switch (_chunkType)
            {
                case 0:
                    if (--_timeLeft != 0) return;
                    _timeLeft = W(_nextChunk);
                    _nextChunk += 2;
                    if (_timeLeft == 0xfffb)
                    {
                        _timeLeft = W(_nextChunk);
                        _nextChunk += 2;
                    }
                    if (_timeLeft == 0) ParseChunk();
                    else
                    {
                        _freq = W(_nextChunk);
                        _nextChunk += 2;
                    }
                    break;
                case 1:
                    _freq = (uint)(_freq + _delta) & 0xffff;
                    if (_freq == _end)
                    {
                        if (--_repeatCounter == 0)
                        {
                            ParseChunk();
                            return;
                        }
                        _freq = _start;
                    }
                    break;
                case 2:
                    _start = (uint)(_start + _delta) & 0xffff;
                    if (_start == _end)
                    {
                        ParseChunk();
                        return;
                    }
                    SetMplex(_start);
                    _forcedLevel = -_forcedLevel;
                    break;
                case 3:
                    _start = (uint)(_start + _delta) & 0xffff;
                    if (_start == _end)
                    {
                        ParseChunk();
                        return;
                    }
                    var lsr = unchecked((ushort)(_randomLsr + 0x9248));
                    lsr = unchecked((ushort)((lsr >> 3) | (lsr << 13)));
                    _randomLsr = lsr;
                    SetMplex((_start & lsr) | 0x180);
                    _forcedLevel = -_forcedLevel;
                    break;
            }
        }

        private void ParseChunk()
        {
            SetMplex(3000);
            _forcedLevel = 0;
            for (var guard = 0; guard < 1000; guard++)
            {
                _chunkType = W(_nextChunk);
                _nextChunk += 2;
                switch (_chunkType)
                {
                    case 0xffff:
                        _freq = 0;
                        _nextChunk = -1;
                        return;
                    case 0xfffe:
                        _repeatChunk = _nextChunk;
                        continue;
                    case 0xfffd:
                        _nextChunk = _repeatChunk;
                        continue;
                    case 0xfffc:
                        continue;
                    case 0:
                        _timeLeft = 1;
                        SetMplex(W(_nextChunk));
                        _nextChunk += 2;
                        return;
                    case 1:
                        SetMplex(W(_nextChunk));
                        _start = W(_nextChunk + 2);
                        _end = W(_nextChunk + 4);
                        _delta = (short)W(_nextChunk + 6);
                        _repeatCounter = W(_nextChunk + 8);
                        _freq = _start;
                        _nextChunk += 10;
                        return;
                    case 2:
                    case 3:
                        _start = W(_nextChunk);
                        _end = W(_nextChunk + 2);
                        _delta = (short)W(_nextChunk + 4);
                        _freq = 0;
                        _nextChunk += 6;
                        _forcedLevel = -1;
                        return;
                    default:
                        return;
                }
            }
            throw new InvalidDataException("The v1 sound loops without playing.");
        }

        public void Generate(List<short> output, int length)
        {
            var buffer = new int[length];
            if (_freq == 0)
            {
                if (_forcedLevel != 0)
                {
                    // As Player_V1::generateSpkSamples: the forced level is a full-scale step (the notes' square wave is +-VolumeLevel0/2).
                    var sample = _forcedLevel * VolumeLevel0;
                    Array.Fill(buffer, sample);
                }
                else if (_level == 0)
                {
                    for (var i = 0; i < length; i++) output.Add(0);
                    return;
                }
            }
            else SquareGenerator((int)_freq, buffer);
            LowPass(buffer);
            foreach (var value in buffer) output.Add((short)value);
        }

        public void Settle(List<short> output)
        {
            for (var guard = 0; guard < SampleRate && _level is not (0 or -1); guard++)
                Generate(output, 1);
        }

        private void SquareGenerator(int freq, int[] samples)
        {
            var period = unchecked((int)(_updateStep * (uint)freq));
            if (period == 0) period = (int)_updateStep;
            for (var i = 0; i < samples.Length; i++)
            {
                uint duration = 0;
                if (_timerOutput) duration += unchecked((uint)_timerCount);
                _timerCount -= 1 << FixShift;
                while (_timerCount <= 0)
                {
                    _timerOutput = !_timerOutput;
                    if (_timerOutput) duration += (uint)period;
                    _timerCount += period;
                }
                if (_timerOutput) duration -= unchecked((uint)_timerCount);
                var value = samples[i] + (int)((long)unchecked((int)(duration - (1 << (FixShift - 1)))) * VolumeLevel0 >> FixShift);
                samples[i] = Math.Clamp(value, -0x8000, 0x7fff);
            }
        }

        private void LowPass(int[] samples)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                _level = unchecked((int)((uint)_level * _decay + (uint)samples[i] * (0x10000 - _decay))) >> 16;
                samples[i] = _level;
            }
        }
    }
}

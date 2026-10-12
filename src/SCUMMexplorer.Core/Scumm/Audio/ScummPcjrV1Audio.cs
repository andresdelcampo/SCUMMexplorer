// IBM PCjr / Tandy player of the classic SCUMM v1 games, ported from ScummVM's Player_V1 (PCjr half: parsePCjrChunk, nextPCjrCmd,
// generatePCjrSamples) and the square generator and low-pass filter of Player_V2 (https://github.com/scummvm/scummvm/tree/master/engines/scumm/players).
// Copyright (C) ScummVM contributors. SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Plays the PCjr/Tandy part of a classic Maniac Mansion sound. The word at byte 4 of the sound gives where that part starts; it begins with its
/// priority byte, then chunks (0xFFFE/0xFFFD/0xFFFF mark the repeat point, the jump to it and the end). The chip has three square voices and a noise voice,
/// each with 16 volume steps of 2 dB (15 is silent); chunk 0 runs four voices with attack/decay/sustain envelopes, chunks 1 and 3 sweep a voice or the noise,
/// chunk 2 is the speaker's forced-level sweep. A tick lasts <c>mplex</c> cycles of the 1.193 MHz timer.
/// </summary>
public sealed class ScummPcjrV1Audio
{
    private const int SampleRate = 44100;
    private const int FixShift = 16;
    private const int MaxSamples = SampleRate * 180;
    private const int MaxTicks = 4_000_000;
    private const double MaxVolume = 255 * 128.0 / 3.0;
    private const uint WhiteNoise = 0x12000, PeriodicNoise = 0x08000;

    /// <summary>Whether a v1-format sound has a PCjr part (the offset at byte 4 points inside the sound).</summary>
    public static bool HasPcjrPart(byte[] data) =>
        data.Length >= 12 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2)) is var offset && offset >= 8 && offset + 4 <= data.Length;

    public static byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null)
            throw new NotSupportedException("This is not an indexed sound resource.");
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        var data = ScummPcSpeakerV1Audio.Slice(file, resource.Offset.Value);
        if (ScummPcSpeakerV1Audio.IsV2Format(data) || !ScummPcSpeakerV1Audio.LooksLikeV1(data) || !HasPcjrPart(data))
            throw new NotSupportedException("This sound has no PCjr/Tandy version this app can play. Choose the PC speaker.");
        return new ScummPcjrV1Audio().RenderWave(data, cancellationToken);
    }

    public byte[] RenderWave(byte[] data, CancellationToken cancellationToken = default)
    {
        var player = new Player(data);
        var samples = new List<short>();
        for (var tick = 0; player.Running && tick < MaxTicks && samples.Count < MaxSamples; tick++)
        {
            if ((tick & 0xFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            player.Generate(samples, player.StartTick());
        }
        if (!player.Heard) throw new NotSupportedException("This resource has no playable PCjr notes.");
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

    private sealed class Channel
    {
        public uint Freq, Volume = 15, NoteLen, HullCounter, Attack, Decay, Level, Sustain1, Sustain2;
        public int CmdPtr = -1, SustCtr;
    }

    private sealed class Player
    {
        private readonly byte[] _data;
        private readonly int _currentData;
        private readonly Channel[] _channels = Enumerable.Range(0, 4).Select(_ => new Channel()).ToArray();
        private readonly uint _mplexStep = (uint)(((long)SampleRate << FixShift) / 1193000);
        private readonly uint _updateStep = (uint)(((long)SampleRate << FixShift) / (111860 * 2));
        private readonly uint _decay;
        private readonly int[] _volumeTable = new int[16];
        private readonly int[] _timerCount = new int[4];
        private int _timerOutput;
        private uint _rng = 0x0f35;
        private int _level;

        private int _nextChunk, _repeatChunk;
        private uint _chunkType, _tickLength, _nextTick, _repeatCounter, _timeLeft, _start, _end, _timeLeft2, _start2;
        private int _delta, _delta2, _forcedLevel;
        private int _valueChannel, _valueChannel2;
        private bool _valueIsVolume, _valueIsVolume2;

        public bool Running => _nextChunk >= 0;
        public bool Heard { get; private set; }

        public Player(byte[] data)
        {
            _data = data;
            _currentData = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2));
            var decay = 0xa000u;
            for (var i = 0; (SampleRate << i) < 30000; i++) decay = decay * decay / 65536;
            _decay = decay;
            var volume = MaxVolume;
            for (var i = 0; i < 15; i++)
            {
                _volumeTable[i] = (int)Math.Min(volume, 0xffff);
                volume /= 1.258925412;
            }
            _repeatChunk = _nextChunk = _currentData + 2;
            SetMplex(3000);
            ParseChunk();
        }

        private ushort W(int position)
        {
            if (position < 0) throw new InvalidDataException("The PCjr sound points before its start.");
            // A few sounds point a voice's envelope at the very end of the resource; ScummVM reads the bytes that follow in memory, here they are zeros.
            if (position + 2 > _data.Length) return 0;
            return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(position, 2));
        }

        private void SetMplex(uint mplex)
        {
            if (mplex == 0) mplex = 65536;
            _tickLength = unchecked(_mplexStep * mplex);
        }

        private void Set(int channel, bool volume, uint value)
        {
            if (volume) _channels[channel].Volume = value; else _channels[channel].Freq = value;
        }

        private void ClearChannels()
        {
            foreach (var channel in _channels) { channel.Freq = 0; channel.Volume = 15; }
        }

        /// <summary>Runs the next tick's command and returns how many samples that tick lasts (the fraction carries over, as in readBuffer).</summary>
        public int StartTick()
        {
            if ((_nextTick >> FixShift) == 0)
            {
                _nextTick += _tickLength;
                if (_nextChunk >= 0) NextCommand();
            }
            var step = (int)(_nextTick >> FixShift);
            _nextTick -= (uint)step << FixShift;
            return step;
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
                        ClearChannels();
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
                        SetMplex(W(_nextChunk));
                        _nextChunk += 2;
                        for (var i = 0; i < 4; i++)
                        {
                            int tmp = W(_nextChunk);
                            _nextChunk += 2;
                            var channel = _channels[i];
                            if (tmp == 0xffff) { channel.CmdPtr = -1; continue; }
                            var at = _currentData + tmp;
                            channel.Attack = W(at);
                            channel.Decay = W(at + 2);
                            channel.Level = W(at + 4);
                            channel.Sustain1 = W(at + 6);
                            channel.Sustain2 = W(at + 8);
                            channel.NoteLen = 1;
                            channel.Volume = 15;
                            channel.CmdPtr = at + 10;
                        }
                        return;
                    case 1:
                        ParseSweepChunk();
                        return;
                    case 2:
                        _start = W(_nextChunk);
                        _end = W(_nextChunk + 2);
                        _delta = (short)W(_nextChunk + 4);
                        _channels[0].Freq = 0;
                        _nextChunk += 6;
                        _forcedLevel = -1;
                        return;
                    case 3:
                    {
                        SetMplex(W(_nextChunk));
                        int tmp = W(_nextChunk + 2);
                        if ((tmp & 0xf0) != 0xe0) throw new InvalidDataException("The PCjr noise chunk names no noise voice.");
                        _channels[3].Freq = (uint)(tmp & 0xf);
                        if ((tmp & 3) == 3)
                        {
                            _nextChunk += 2;
                            _channels[2].Freq = W(_nextChunk + 2);
                        }
                        _channels[3].Volume = W(_nextChunk + 4);
                        _repeatCounter = W(_nextChunk + 6);
                        _delta = (short)W(_nextChunk + 8);
                        _nextChunk += 10;
                        return;
                    }
                    default:
                        return;
                }
            }
            throw new InvalidDataException("The PCjr sound loops without playing.");
        }

        private void ParseSweepChunk()
        {
            SetMplex(W(_nextChunk));
            int tmp = W(_nextChunk + 2);
            _channels[0].CmdPtr = tmp != 0xffff ? _currentData + tmp : -1;
            tmp = W(_nextChunk + 4);
            _start = W(_nextChunk + 6);
            _delta = (short)W(_nextChunk + 8);
            _timeLeft = W(_nextChunk + 10);
            _nextChunk += 12;
            if (tmp >= 0xe0)
            {
                _channels[3].Freq = (uint)(tmp & 0xf);
                (_valueChannel, _valueIsVolume) = (3, true);
            }
            else
            {
                if ((tmp & 0x10) != 0) throw new InvalidDataException("The PCjr sweep chunk names a volume as its target.");
                tmp = (tmp & 0x60) >> 5;
                (_valueChannel, _valueIsVolume) = (tmp, false);
                _channels[tmp].Volume = 0;
            }
            Set(_valueChannel, _valueIsVolume, _start);
            var cmd = _channels[0].CmdPtr;
            if (cmd < 0) return;
            tmp = W(cmd);
            _start2 = W(cmd + 2);
            _delta2 = (short)W(cmd + 4);
            _timeLeft2 = W(cmd + 6);
            _channels[0].CmdPtr += 8;
            if (_valueChannel == 3 && _valueIsVolume)
            {
                tmp = (tmp & 0x70) >> 4;
                (_valueChannel2, _valueIsVolume2) = (tmp >> 1, (tmp & 1) != 0);
            }
            else
            {
                if ((tmp & 0x10) != 0) throw new InvalidDataException("The PCjr sweep chunk names a volume as its target.");
                tmp = (tmp & 0x60) >> 5;
                (_valueChannel2, _valueIsVolume2) = (tmp, false);
                _channels[tmp].Volume = 0;
            }
            Set(_valueChannel2, _valueIsVolume2, _start2);
        }

        private void NextCommand()
        {
            switch (_chunkType)
            {
                case 0:
                    for (var i = 0; i < 4; i++)
                    {
                        var channel = _channels[i];
                        if (channel.CmdPtr < 0) continue;
                        if (--channel.NoteLen == 0)
                        {
                            int dummy = W(channel.CmdPtr);
                            if (dummy >= 0xfffe)
                            {
                                if (dummy == 0xfffe) _nextChunk = _currentData + 2;
                                ParseChunk();
                                return;
                            }
                            channel.NoteLen = (uint)(4 * dummy);
                            dummy = W(channel.CmdPtr + 2);
                            if (dummy == 0)
                            {
                                channel.HullCounter = 4;
                                channel.SustCtr = (int)channel.Sustain2;
                            }
                            else
                            {
                                channel.HullCounter = 1;
                                channel.Freq = (uint)dummy;
                            }
                            channel.CmdPtr += 4;
                        }
                        switch (channel.HullCounter)
                        {
                            case 1:
                                channel.Volume = unchecked(channel.Volume - channel.Attack);
                                if ((int)channel.Volume <= 0)
                                {
                                    channel.Volume = 0;
                                    channel.HullCounter++;
                                }
                                break;
                            case 2:
                                channel.Volume = unchecked(channel.Volume + channel.Decay);
                                if (channel.Volume >= channel.Level)
                                {
                                    channel.Volume = channel.Level;
                                    channel.HullCounter++;
                                }
                                break;
                            case 4:
                                if (--channel.SustCtr < 0)
                                {
                                    channel.SustCtr = (int)channel.Sustain2;
                                    channel.Volume = unchecked(channel.Volume + channel.Sustain1);
                                    if ((int)channel.Volume >= 15)
                                    {
                                        channel.Volume = 15;
                                        channel.HullCounter++;
                                    }
                                }
                                break;
                        }
                    }
                    break;
                case 1:
                    _start = unchecked((uint)(_start + _delta));
                    Set(_valueChannel, _valueIsVolume, _start);
                    if (--_timeLeft == 0)
                    {
                        _start = W(_nextChunk);
                        _nextChunk += 2;
                        if (_start == 0xffff)
                        {
                            ParseChunk();
                            return;
                        }
                        _delta = (short)W(_nextChunk);
                        _timeLeft = W(_nextChunk + 2);
                        _nextChunk += 4;
                        Set(_valueChannel, _valueIsVolume, _start);
                    }
                    if (_channels[0].CmdPtr >= 0)
                    {
                        _start2 = unchecked((uint)(_start2 + _delta2));
                        Set(_valueChannel2, _valueIsVolume2, _start2);
                        if (--_timeLeft2 == 0)
                        {
                            var cmd = _channels[0].CmdPtr;
                            _start2 = W(cmd);
                            if (_start2 == 0xffff)
                            {
                                _nextChunk = cmd + 2;
                                ParseChunk();
                                return;
                            }
                            _delta2 = (short)W(cmd + 2);
                            _timeLeft2 = W(cmd + 4);
                            _channels[0].CmdPtr += 6;
                        }
                    }
                    break;
                case 2:
                    _start = unchecked((uint)(_start + _delta));
                    if (_start == _end)
                    {
                        ParseChunk();
                        return;
                    }
                    SetMplex(_start);
                    _forcedLevel = -_forcedLevel;
                    break;
                case 3:
                {
                    var dummy = (int)_channels[3].Volume + _delta;
                    if (dummy >= 15) _channels[3].Volume = 15;
                    else if (dummy <= 0) _channels[3].Volume = 0;
                    else
                    {
                        _channels[3].Volume = (uint)dummy;
                        break;
                    }
                    if (--_repeatCounter == 0)
                    {
                        ParseChunk();
                        return;
                    }
                    _delta = (short)W(_nextChunk);
                    _nextChunk += 2;
                    break;
                }
            }
        }

        public void Generate(List<short> output, int length)
        {
            var buffer = new int[length];
            var hasData = false;
            if (_forcedLevel != 0)
            {
                Array.Fill(buffer, _forcedLevel * _volumeTable[0]);
                hasData = true;
            }
            for (var i = 1; i < 3; i++)
            {
                var freq = _channels[i].Freq;
                if (freq == 0) continue;
                for (var j = 0; j < i; j++)
                {
                    if (freq != _channels[j].Freq) continue;
                    // Same pitch as another voice: ScummVM synchronises the phase to prevent interference.
                    _timerCount[i] = _timerCount[j];
                    _timerOutput ^= (1 << i) & (_timerOutput ^ _timerOutput << (i - j));
                }
            }
            for (var i = 0; i < 4; i++)
            {
                var freq = _channels[i].Freq;
                var volume = (int)Math.Min(_channels[i].Volume, 15u);
                if (_volumeTable[volume] == 0)
                {
                    _timerCount[i] -= length << FixShift;
                    if (_timerCount[i] < 0) _timerCount[i] = 0;
                }
                else if (i < 3)
                {
                    hasData = true;
                    Square(i, (int)freq, volume, 0, buffer);
                }
                else
                {
                    var feedback = (freq & 4) != 0 ? WhiteNoise : PeriodicNoise;
                    var n = (int)(freq & 3);
                    var noiseFreq = n == 3 ? 2 * (int)_channels[2].Freq : 1 << (5 + n);
                    hasData = true;
                    Square(i, noiseFreq, volume, feedback, buffer);
                }
            }
            Heard |= hasData;
            if (_level != 0 || hasData) LowPass(buffer);
            foreach (var value in buffer) output.Add((short)value);
        }

        public void Settle(List<short> output)
        {
            for (var guard = 0; guard < SampleRate && _level is not (0 or -1); guard++)
                Generate(output, 1);
        }

        private void Square(int channel, int freq, int volume, uint noiseFeedback, int[] samples)
        {
            var period = unchecked((int)(_updateStep * (uint)freq));
            if (period == 0) period = (int)_updateStep;
            for (var i = 0; i < samples.Length; i++)
            {
                uint duration = 0;
                if ((_timerOutput & (1 << channel)) != 0) duration += unchecked((uint)_timerCount[channel]);
                _timerCount[channel] -= 1 << FixShift;
                while (_timerCount[channel] <= 0)
                {
                    if (noiseFeedback != 0)
                    {
                        if ((_rng & 1) != 0)
                        {
                            _rng ^= noiseFeedback;
                            _timerOutput ^= 1 << channel;
                        }
                        _rng >>= 1;
                    }
                    else _timerOutput ^= 1 << channel;
                    if ((_timerOutput & (1 << channel)) != 0) duration += (uint)period;
                    _timerCount[channel] += period;
                }
                if ((_timerOutput & (1 << channel)) != 0) duration -= unchecked((uint)_timerCount[channel]);
                var value = samples[i] + (int)((long)unchecked((int)(duration - (1 << (FixShift - 1)))) * _volumeTable[volume] >> FixShift);
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

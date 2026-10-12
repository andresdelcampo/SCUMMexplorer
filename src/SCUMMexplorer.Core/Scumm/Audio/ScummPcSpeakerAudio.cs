// PC speaker decoder and synthesis ported from ScummVM's Player_V2Base and
// Player_V2 (https://github.com/scummvm/scummvm/tree/master/engines/scumm/players).
// Copyright (C) ScummVM contributors. SPDX-License-Identifier: GPL-3.0-or-later
//
// The channel state is kept as the same array of 16-bit words as ScummVM's
// channel_data struct, so script parameter writes alias the same fields, and the
// tick, envelope and square-generator arithmetic (16.16 fixed point, integer
// low-pass filter) follows the C++ line by line. Only the 22 050 Hz output rate
// differs from the ScummVM mixer rate; ScummVM derives the filter decay from the
// rate in the same way (see _decay below).
using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Reads the PC speaker track in an old-bundle DOS LOOM sound resource.</summary>
public sealed class ScummPcSpeakerAudio
{
    private static readonly int[] NoteLengths =
        [0, 0, 0, 2, 0, 3, 4, 5, 6, 8, 9, 12, 16, 18, 24, 32, 36, 48, 64, 72, 96];
    private static readonly int[] Divisors =
        [36484, 34436, 32503, 30679, 28957, 27332, 25798, 24350, 22983, 21693, 20476, 19326];
    // The PCjr's divisors are for its 111860 Hz base (Player_V2Base pcjr_freq_table).
    private static readonly int[] PcjrDivisors =
        [65472, 61760, 58304, 55040, 52032, 49024, 46272, 43648, 41216, 38912, 36736, 34624];
    private const int TickRate = 236;
    private const int SampleRate = 22050;
    private const int PcjrSampleRate = 44100;
    private const uint WhiteNoise = 0x12000, PeriodicNoise = 0x08000;
    private const int MaxTicks = TickRate * 180;
    private const int FixShift = 16;
    private const int MaxScriptSteps = 1 << 16;

    // channel_data field indices (each a uint16 in ScummVM's struct).
    private const int TimeLeft = 0, NextCmd = 1, BaseFreq = 2, FreqDelta = 3, Freq = 4, Volume = 5, VolumeDelta = 6,
        Tempo = 7, InterNotePause = 8, Transpose = 9, NoteLength = 10, HullCurve = 11, HullOffset = 12,
        HullCounter = 13, FreqmodTable = 14, FreqmodOffset = 15, FreqmodIncr = 16, FreqmodMultiplier = 17,
        FreqmodModulo = 18, ChannelWords = 25;

    // ScummVM's `hulls` table, verbatim (including the (int16) casts of the large values).
    // Entries are (value, ticks) pairs; ticks -1 means "set the volume to value".
    private static readonly short[] Hulls = Array.ConvertAll(new int[]
    {
        3, -1, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0,
        3, -1, 0, 32, 0, -1, 0, 0, 0, -1, 0, 0,
        3, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        3, -1, 0, 2, 0, -1, 0, 0, 0, -1, 0, 0,
        3, -1, 0, 6, 0, -1, 0, 0, 0, -1, 0, 0,
        3, -1, 0, 16, 0, -1, 0, 0, 0, -1, 0, 0,
        60000, -1, -1000, 20, 0, 0, 0, 0, 40000, -1, -5000, 5, 0, -1, 0, 0,
        50000, -1, 0, 8, 30000, -1, 0, 0, 28000, -1, -5000, 5, 0, -1, 0, 0,
        60000, -1, -2000, 16, 0, 0, 0, 0, 28000, -1, -6000, 5, 0, -1, 0, 0,
        55000, -1, 0, 8, 35000, -1, 0, 0, 40000, -1, -2000, 10, 0, -1, 0, 0,
        60000, -1, 0, 4, -2000, 8, 0, 0, 40000, -1, -6000, 5, 0, -1, 0, 0,
        0, -1, 150, 340, -150, 340, 0, -1, 0, -1, 0, 0,
        20000, -1, 4000, 7, 1000, 15, 0, 0, 35000, -1, -2000, 15, 0, -1, 0, 0,
        35000, -1, 500, 20, 0, 0, 0, 0, 45000, -1, -500, 60, 0, -1, 0, 0,
        44000, -1, -4400, 10, 0, -1, 0, 0, 0, -1, 0, 0,
        53000, -1, -5300, 10, 0, -1, 0, 0, 0, -1, 0, 0,
        63000, -1, -6300, 10, 0, -1, 0, 0, 0, -1, 0, 0,
        44000, -1, -1375, 32, 0, -1, 0, 0, 0, -1, 0, 0,
        53000, -1, -1656, 32, 0, -1, 0, 0, 0, -1, 0, 0,
        63000, -1, -1968, 32, 0, -1, 0, 0, 0, -1, 0, 0,
        44000, -1, -733, 60, 0, -1, 0, 0, 0, -1, 0, 0,
        53000, -1, -883, 60, 0, -1, 0, 0, 0, -1, 0, 0,
        63000, -1, -1050, 60, 0, -1, 0, 0, 0, -1, 0, 0,
        44000, -1, -488, 90, 0, -1, 0, 0, 0, -1, 0, 0,
        53000, -1, -588, 90, 0, -1, 0, 0, 0, -1, 0, 0,
        63000, -1, -700, 90, 0, -1, 0, 0, 0, -1, 0, 0
    }, value => unchecked((short)value));
    private static readonly int[] HullOffsets = [0, 12, 24, 36, 48, 60, 72, 88, 104, 120, 136, 256, 152, 164, 180];
    private static readonly int[] ModulationPeriods = [0x1000, 0x1000, 0x20, 0x2000, 0x1000];

    public byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null || resource.ContainerFileNumber is null)
            throw new NotSupportedException("This is not an indexed LOOM sound resource.");
        // Numbered old-bundle LFL files are XOR-FF encoded throughout.
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        var offset = resource.Offset.Value;
        if (offset < 0 || offset > file.Length - 4)
            throw new InvalidDataException("The sound offset is outside its LFL file.");
        var size = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset, 2));
        if (size < 22 || size > file.Length - offset)
            throw new InvalidDataException("The sound resource has an invalid length.");
        var data = file.AsSpan(offset, size).ToArray();
        if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2)) != 22 ||
            data[8] != 0 || data[9] != 0 || data[10] != 0 || data[11] != 0)
            throw new NotSupportedException("This sound uses a different LOOM music encoding. PC speaker playback is available for the old-bundle tracks.");

        return RenderWave(data, 4, cancellationToken);
    }

    /// <summary>Renders the PCjr/Tandy part of a v2-format sound (Zak McKracken, enhanced Maniac Mansion): the channel pointers sit 10 bytes later in the header.</summary>
    public byte[] RenderPcjrWave(GameResource resource, CancellationToken cancellationToken = default)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null)
            throw new NotSupportedException("This is not an indexed sound resource.");
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        var data = ScummPcSpeakerV1Audio.Slice(file, resource.Offset.Value);
        if (!ScummPcSpeakerV1Audio.IsV2Format(data) || data.Length < 4 + 10 + 8)
            throw new NotSupportedException("This sound has no PCjr/Tandy version this app can play. Choose the PC speaker.");
        return RenderWave(data, 4, cancellationToken, pcjr: true);
    }

    /// <summary>
    /// Renders PC speaker data given directly. <paramref name="headerLength"/> is the resource header the data starts with:
    /// 4 in the old bundle (a word length), 6 in the CD game's <c>WA</c> block (a dword length and the tag). The channel
    /// pointers follow the header and a word; they count from the start of the data.
    /// </summary>
    public byte[] RenderWave(byte[] data, int headerLength, CancellationToken cancellationToken = default, bool pcjr = false)
    {
        if (data.Length < headerLength + (pcjr ? 18 : 10)) throw new InvalidDataException("The sound resource is too short.");
        var player = new Player(data, headerLength, pcjr);
        var samples = new List<short>();
        var hadSound = false;
        for (var tick = 0; tick < MaxTicks && player.Playing; tick++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            player.NextTick();
            hadSound |= player.HasWinner;
            player.Generate(samples, player.TakeTickSamples());
        }
        if (!hadSound)
            throw new NotSupportedException("This resource has no playable PC speaker notes.");
        // ScummVM keeps running the low-pass filter on silence until it has settled.
        player.Settle(samples);
        return ToWave(samples, pcjr ? PcjrSampleRate : SampleRate);
    }

    private static byte[] ToWave(List<short> samples, int sampleRate)
    {
        using var stream = new MemoryStream(44 + samples.Count * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Count * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Count * 2);
        foreach (var sample in samples) writer.Write(sample);
        return stream.ToArray();
    }

    /// <summary>Player_V2Base channel script and envelope logic plus Player_V2's speaker output.</summary>
    private sealed class Player
    {
        private readonly byte[] _data;
        // ScummVM has a fifth, never-ticked channel that absorbs out-of-range 0xFD targets.
        private readonly ushort[][] _channels = Enumerable.Range(0, 5).Select(_ => new ushort[ChannelWords]).ToArray();
        private int _returnAddress = -1;

        // Player_V2 output state.
        private readonly bool _pcjr;
        private readonly int _rate;
        private readonly uint _tickLength;
        private readonly uint _updateStep;
        private uint _rng = 0x0f35;
        private readonly int[] _volumeTable = new int[16];
        private readonly int[] _pcjrTimerCount = new int[4];
        private int _pcjrTimerOutput;
        private readonly uint _decay;
        private readonly int _volumeLevel0 = (int)(255 * 128.0 / 3.0);
        private uint _nextTick;
        private int _level;
        private int _timerCount;
        private bool _timerOutput;

        public Player(byte[] data, int headerLength, bool pcjr = false)
        {
            _data = data;
            _pcjr = pcjr;
            _rate = pcjr ? PcjrSampleRate : SampleRate;
            _tickLength = (uint)(((long)_rate << FixShift) / TickRate);
            _updateStep = (uint)(((long)_rate << FixShift) / ((pcjr ? 111860 : 1193000) * 2));
            var volume = 255 * 128.0 / 3.0;
            for (var i = 0; i < 15; i++)
            {
                _volumeTable[i] = (int)Math.Min(volume, 0xffff);
                volume /= 1.258925412;
            }
            for (var i = 0; i < 4; i++)
            {
                _channels[i][NextCmd] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(headerLength + (pcjr ? 10 : 2) + 2 * i, 2));
                if (_channels[i][NextCmd] != 0) _channels[i][TimeLeft] = 1;
            }
            // Adapt _decay to the sample rate; it is squared when the rate halves.
            var decay = 0xa000u;
            for (var i = 0; (_rate << i) < 30000; i++) decay = decay * decay / 65536;
            _decay = decay;
        }

        public bool Playing => _channels.Take(4).Any(channel => channel[TimeLeft] != 0);

        public bool HasWinner => WinningChannel() >= 0 || (_pcjr && _channels.Take(4).Any(channel => channel[Volume] != 0 && channel[TimeLeft] != 0));

        public void NextTick()
        {
            for (var i = 0; i < 4; i++)
            {
                if (_channels[i][TimeLeft] == 0) continue;
                NextFreqs(_channels[i]);
            }
        }

        /// <summary>The whole samples of the tick just started; the remainder carries over as in readBuffer.</summary>
        public int TakeTickSamples()
        {
            if ((_nextTick >> FixShift) == 0) _nextTick += _tickLength;
            var step = (int)(_nextTick >> FixShift);
            _nextTick -= (uint)step << FixShift;
            return step;
        }

        private int WinningChannel()
        {
            for (var i = 0; i < 4; i++)
                if (_channels[i][Volume] != 0 && _channels[i][TimeLeft] != 0) return i;
            return -1;
        }

        public void Generate(List<short> output, int length)
        {
            if (_pcjr)
            {
                GeneratePcjr(output, length);
                return;
            }
            var winner = WinningChannel();
            var buffer = new int[length];
            if (winner >= 0) SquareGenerator(_channels[winner][Freq], buffer);
            else if (_level == 0)
            {
                // Shortcut in ScummVM: no sound is being played.
                for (var i = 0; i < length; i++) output.Add(0);
                return;
            }
            LowPass(buffer);
            foreach (var value in buffer) output.Add((short)value);
        }

        public void Settle(List<short> output)
        {
            for (var guard = 0; guard < _rate && _level is not (0 or -1); guard++)
                Generate(output, 1);
        }

        /// <summary>Player_V2::generatePCjrSamples: three square voices and a noise voice with their own 2 dB volume steps.</summary>
        private void GeneratePcjr(List<short> output, int length)
        {
            var buffer = new int[length];
            var hasData = false;
            bool Active(int i) => _channels[i][Volume] != 0 && _channels[i][TimeLeft] != 0;
            for (var i = 1; i < 3; i++)
            {
                var freq = _channels[i][Freq] >> 6;
                if (!Active(i)) continue;
                for (var j = 0; j < i; j++)
                {
                    if (!Active(j) || freq != _channels[j][Freq] >> 6) continue;
                    // Same pitch as another voice: ScummVM synchronises the phase to prevent interference.
                    _pcjrTimerCount[i] = _pcjrTimerCount[j];
                    _pcjrTimerOutput ^= (1 << i) & (_pcjrTimerOutput ^ _pcjrTimerOutput << (i - j));
                }
            }
            for (var i = 0; i < 4; i++)
            {
                int freq = _channels[i][Freq] >> 6;
                var volume = (65535 - _channels[i][Volume]) >> 12;
                if (!Active(i))
                {
                    _pcjrTimerCount[i] -= length << FixShift;
                    if (_pcjrTimerCount[i] < 0) _pcjrTimerCount[i] = 0;
                }
                else if (i < 3)
                {
                    hasData = true;
                    PcjrSquare(i, freq, volume, 0, buffer);
                }
                else
                {
                    var feedback = (freq & 4) != 0 ? WhiteNoise : PeriodicNoise;
                    var n = freq & 3;
                    var noiseFreq = n == 3 ? 2 * (_channels[2][Freq] >> 6) : 1 << (5 + n);
                    hasData = true;
                    PcjrSquare(i, noiseFreq, volume, feedback, buffer);
                }
            }
            if (_level == 0 && !hasData)
            {
                for (var i = 0; i < length; i++) output.Add(0);
                return;
            }
            LowPass(buffer);
            foreach (var value in buffer) output.Add((short)value);
        }

        private void PcjrSquare(int channel, int freq, int volume, uint noiseFeedback, int[] samples)
        {
            var period = unchecked((int)(_updateStep * (uint)freq));
            if (period == 0) period = (int)_updateStep;
            for (var i = 0; i < samples.Length; i++)
            {
                uint duration = 0;
                if ((_pcjrTimerOutput & (1 << channel)) != 0) duration += unchecked((uint)_pcjrTimerCount[channel]);
                _pcjrTimerCount[channel] -= 1 << FixShift;
                while (_pcjrTimerCount[channel] <= 0)
                {
                    if (noiseFeedback != 0)
                    {
                        if ((_rng & 1) != 0)
                        {
                            _rng ^= noiseFeedback;
                            _pcjrTimerOutput ^= 1 << channel;
                        }
                        _rng >>= 1;
                    }
                    else _pcjrTimerOutput ^= 1 << channel;
                    if ((_pcjrTimerOutput & (1 << channel)) != 0) duration += (uint)period;
                    _pcjrTimerCount[channel] += period;
                }
                if ((_pcjrTimerOutput & (1 << channel)) != 0) duration -= unchecked((uint)_pcjrTimerCount[channel]);
                var value = samples[i] + (int)((long)unchecked((int)(duration - (1 << (FixShift - 1)))) * _volumeTable[volume] >> FixShift);
                samples[i] = Math.Clamp(value, -0x8000, 0x7fff);
            }
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
                var value = samples[i] + (int)((long)unchecked((int)(duration - (1 << (FixShift - 1)))) * _volumeLevel0 >> FixShift);
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

        private static int ReadModulation(int table, int index)
        {
            var at = table + index;
            if ((uint)at >= ScummPcSpeakerModulationTable.Values.Length)
                throw new InvalidDataException("Sound modulation index is invalid.");
            return ScummPcSpeakerModulationTable.Values[at];
        }

        private void NextFreqs(ushort[] channel)
        {
            channel[Volume] = unchecked((ushort)(channel[Volume] + channel[VolumeDelta]));
            channel[BaseFreq] = unchecked((ushort)(channel[BaseFreq] + channel[FreqDelta]));
            if (channel[FreqmodModulo] > 0)
                channel[FreqmodOffset] = (ushort)((channel[FreqmodOffset] + channel[FreqmodIncr]) % channel[FreqmodModulo]);
            else
                channel[FreqmodOffset] = 0;
            channel[Freq] = unchecked((ushort)(ReadModulation(channel[FreqmodTable], channel[FreqmodOffset] >> 4)
                * channel[FreqmodMultiplier] / 256 + channel[BaseFreq]));

            if (channel[NoteLength] != 0 && --channel[NoteLength] == 0)
            {
                channel[HullOffset] = 16;
                channel[HullCounter] = 1;
            }
            if (--channel[TimeLeft] == 0) ExecuteCommand(channel);
            if (channel[HullCounter] != 0 && --channel[HullCounter] == 0)
            {
                for (var guard = 0; ; guard++)
                {
                    var at = channel[HullCurve] + channel[HullOffset] / 2;
                    if (guard > 64 || at + 1 >= Hulls.Length) throw new InvalidDataException("Sound hull envelope is invalid.");
                    if (Hulls[at + 1] == -1)
                    {
                        channel[Volume] = unchecked((ushort)Hulls[at]);
                        if (Hulls[at] == 0) channel[VolumeDelta] = 0;
                        channel[HullOffset] += 4;
                    }
                    else
                    {
                        channel[VolumeDelta] = unchecked((ushort)Hulls[at]);
                        channel[HullCounter] = unchecked((ushort)Hulls[at + 1]);
                        channel[HullOffset] += 4;
                        break;
                    }
                }
            }
        }

        private byte Byte(ref int position)
        {
            if (position < 0 || position >= _data.Length) throw new ScriptOverrun();
            return _data[position++];
        }

        private ushort Word(ref int position) => (ushort)(Byte(ref position) | (Byte(ref position) << 8));

        private static void ClearFields(ushort[] channel)
        {
            channel[NextCmd] = 0;
            channel[BaseFreq] = 0;
            channel[FreqDelta] = 0;
            channel[Freq] = 0;
            channel[Volume] = 0;
            channel[VolumeDelta] = 0;
            channel[InterNotePause] = 0;
            channel[Transpose] = 0;
            channel[HullCurve] = 0;
            channel[HullOffset] = 0;
            channel[HullCounter] = 0;
            channel[FreqmodTable] = 0;
            channel[FreqmodOffset] = 0;
            channel[FreqmodIncr] = 0;
            channel[FreqmodMultiplier] = 0;
            channel[FreqmodModulo] = 0;
        }

        private sealed class ScriptOverrun : Exception;

        /// <summary>ScummVM would read past the resource here; the channel script ends instead.</summary>
        private void ExecuteCommand(ushort[] current)
        {
            try { ExecuteCommandCore(current); }
            catch (ScriptOverrun) { current[NextCmd] = 0; current[TimeLeft] = 0; }
        }

        /// <summary>execute_cmd: note that after 0xFD ScummVM keeps running the script on the cleared target channel.</summary>
        private void ExecuteCommandCore(ushort[] current)
        {
            var channel = current;
            if (channel[NextCmd] != 0)
            {
                var position = (int)channel[NextCmd];
                for (var steps = 0; ; steps++)
                {
                    if (steps > MaxScriptSteps) throw new InvalidDataException("The sound stream does not advance.");
                    var opcode = Byte(ref position);
                    if (opcode >= 0xf8)
                    {
                        switch (opcode)
                        {
                            case 0xf8:
                            {
                                var curve = Byte(ref position) / 2;
                                if (curve >= HullOffsets.Length) throw new InvalidDataException("Sound hull curve is invalid.");
                                channel[HullCurve] = (ushort)HullOffsets[curve];
                                break;
                            }
                            case 0xf9:
                            {
                                var curve = Byte(ref position) / 4;
                                if (curve >= ModulationPeriods.Length) throw new InvalidDataException("Sound modulation curve is invalid.");
                                channel[FreqmodTable] = (ushort)ScummPcSpeakerModulationTable.Offsets[curve];
                                channel[FreqmodModulo] = (ushort)ModulationPeriods[curve];
                                break;
                            }
                            case 0xfd:
                            {
                                var target = Word(ref position) / (ChannelWords * 2);
                                if (target >= 4) target = 4;
                                channel = _channels[target];
                                ClearFields(channel);
                                break;
                            }
                            case 0xfa:
                                ClearFields(channel);
                                break;
                            case 0xfb:
                                if (_returnAddress < 0) throw new InvalidDataException("Sound subroutine return has no call.");
                                position = _returnAddress;
                                break;
                            case 0xfc:
                            {
                                var destination = (short)Word(ref position);
                                _returnAddress = position;
                                position = destination;
                                break;
                            }
                            case 0xfe:
                            {
                                var parameter = Byte(ref position) / 2;
                                var relative = (short)Word(ref position);
                                if (parameter >= ChannelWords) throw new InvalidDataException("Sound loop parameter is invalid.");
                                if (channel[parameter] == 0 || --channel[parameter] != 0) position += relative;
                                break;
                            }
                            case 0xff:
                            {
                                var raw = Byte(ref position);
                                var value = Word(ref position);
                                if (raw / 2 >= ChannelWords) throw new InvalidDataException("Sound parameter is invalid.");
                                channel[raw / 2] = value;
                                if (raw == 0) goto end;
                                break;
                            }
                        }
                        continue;
                    }

                    // Note commands; one command can start notes on several channels.
                    while (true)
                    {
                        var destination = _channels[(opcode >> 5) & 3];
                        int note;
                        bool isLastNote;
                        if ((opcode & 0x80) == 0)
                        {
                            var tempo = channel[Tempo];
                            if (tempo == 0) tempo = 1;
                            channel[TimeLeft] = unchecked((ushort)(tempo * NoteLengths[opcode & 0x1f]));
                            note = Byte(ref position);
                            isLastNote = (note & 0x80) != 0;
                            note &= 0x7f;
                            if (note == 0x7f) goto end;
                        }
                        else
                        {
                            channel[TimeLeft] = (ushort)(((opcode & 7) << 8) | Byte(ref position));
                            if ((opcode & 0x10) != 0) goto end;
                            isLastNote = false;
                            note = Byte(ref position) & 0x7f;
                        }

                        destination[TimeLeft] = channel[TimeLeft];
                        destination[NoteLength] = unchecked((ushort)(channel[TimeLeft] - destination[InterNotePause]));
                        note = (short)(note + destination[Transpose]);
                        while (note < 0) note += 12;
                        var octave = note / 12;
                        note %= 12;
                        destination[HullOffset] = 0;
                        destination[HullCounter] = 1;
                        if (_pcjr && destination == _channels[3])
                        {
                            destination[HullCurve] = (ushort)(196 + note * 12);
                            destination[Freq] = destination[BaseFreq] = unchecked((ushort)(384 - 64 * octave));
                        }
                        else destination[Freq] = destination[BaseFreq] = (ushort)((_pcjr ? PcjrDivisors : Divisors)[note] >> octave);
                        if (isLastNote) goto end;
                        opcode = Byte(ref position);
                    }
                }
            end:
                channel = current;
                if (channel[TimeLeft] != 0)
                {
                    channel[NextCmd] = (ushort)position;
                    return;
                }
                // fall through: the channel script has finished
                channel[NextCmd] = 0;
                return;
            }
            // next_cmd == 0 (check_stopped): the sound ends when no channel has time left; the render loop checks that.
        }
    }
}

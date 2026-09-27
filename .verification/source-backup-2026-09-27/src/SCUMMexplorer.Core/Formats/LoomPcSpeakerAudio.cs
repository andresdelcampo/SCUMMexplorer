// PC speaker decoder and synthesis adapted from ScummVM's Player_V2Base and
// Player_V2 (https://github.com/scummvm/scummvm/tree/master/engines/scumm/players).
// Copyright (C) ScummVM contributors. SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>Reads the PC speaker track in an old-bundle DOS LOOM sound resource.</summary>
public sealed class LoomPcSpeakerAudio
{
    private static readonly int[] NoteLengths =
        [0, 0, 0, 2, 0, 3, 4, 5, 6, 8, 9, 12, 16, 18, 24, 32, 36, 48, 64, 72, 96];
    private static readonly int[] Divisors =
        [36484, 34436, 32503, 30679, 28957, 27332, 25798, 24350, 22983, 21693, 20476, 19326];
    private const int TickRate = 236;
    private const int SampleRate = 22050;
    private const int MaxTicks = TickRate * 180;
    private const double SpeakerAmplitude = 5440;
    private const double FilterInput = 0.609375;

    // The V2 sound bytecode selects an envelope for each channel. Each pair is
    // either an absolute volume (duration -1), or a per-tick change and the
    // number of ticks before the next pair. The release starts at pair 4.
    private static readonly HullStep[][] Hulls =
    [
        [new(3, -1), new(0, 0), new(0, 0), new(0, 0), new(0, -1), new(0, 0)],
        [new(3, -1), new(0, 32), new(0, -1), new(0, 0), new(0, -1), new(0, 0)],
        [new(3, -1), new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0)],
        [new(3, -1), new(0, 2), new(0, -1), new(0, 0), new(0, -1), new(0, 0)],
        [new(3, -1), new(0, 6), new(0, -1), new(0, 0), new(0, -1), new(0, 0)],
        [new(3, -1), new(0, 16), new(0, -1), new(0, 0), new(0, -1), new(0, 0)],
        [new(60000, -1), new(-1000, 20), new(0, 0), new(0, 0), new(40000, -1), new(-5000, 5), new(0, -1), new(0, 0)],
        [new(50000, -1), new(0, 8), new(30000, -1), new(0, 0), new(28000, -1), new(-5000, 5), new(0, -1), new(0, 0)],
        [new(60000, -1), new(-2000, 16), new(0, 0), new(0, 0), new(28000, -1), new(-6000, 5), new(0, -1), new(0, 0)],
        [new(55000, -1), new(0, 8), new(35000, -1), new(0, 0), new(40000, -1), new(-2000, 10), new(0, -1), new(0, 0)],
        [new(60000, -1), new(0, 4), new(-2000, 8), new(0, 0), new(40000, -1), new(-6000, 5), new(0, -1), new(0, 0)],
        [new(63000, -1), new(-1968, 32), new(0, -1), new(0, 0), new(44000, -1), new(-733, 60), new(0, -1), new(0, 0)],
        [new(0, -1), new(150, 340), new(-150, 340), new(0, -1), new(0, -1), new(0, 0)],
        [new(20000, -1), new(4000, 7), new(1000, 15), new(0, 0), new(35000, -1), new(-2000, 15), new(0, -1), new(0, 0)],
        [new(35000, -1), new(500, 20), new(0, 0), new(0, 0), new(45000, -1), new(-500, 60), new(0, -1), new(0, 0)]
    ];
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

        var channels = Enumerable.Range(0, 4).Select(_ => new Channel()).ToArray();
        for (var i = 0; i < 4; i++)
        {
            channels[i].Position = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6 + i * 2, 2));
            channels[i].Remaining = channels[i].Position == 0 ? 0 : 1;
        }
        var samples = new List<short>();
        double samplesToToggle = 0;
        var speakerHigh = false;
        double filteredSample = 0;
        var sampleRemainder = 0;
        var hadSound = false;
        var returnPosition = 0;
        for (var tick = 0; tick < MaxTicks && channels.Any(c => c.Remaining > 0); tick++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var channel in channels)
            {
                if (channel.Remaining == 0) continue;
                channel.Volume = (ushort)(channel.Volume + channel.Parameters[6]);
                channel.Divisor = (ushort)(channel.Divisor + (short)channel.Parameters[3]);
                if (channel.Parameters[18] > 0)
                {
                    channel.ModulationPosition = (channel.ModulationPosition + channel.Parameters[16]) % channel.Parameters[18];
                    var modulation = ModulationValue(channel.ModulationCurve, channel.ModulationPosition >> 4);
                    channel.CurrentDivisor = (ushort)(channel.Divisor + modulation * channel.Parameters[17] / 256);
                }
                else channel.CurrentDivisor = channel.Divisor;

                if (channel.NoteLength > 0 && --channel.NoteLength == 0)
                {
                    channel.HullPosition = 4;
                    channel.HullCounter = 1;
                }
                channel.Remaining--;
                if (channel.Remaining == 0) Execute(data, channels, channel, ref returnPosition);
                AdvanceHull(channel);
            }

            var active = channels.FirstOrDefault(c => c.Remaining > 0 && c.Volume != 0 && c.CurrentDivisor > 0);
            var frequency = active is null ? 0 : 1193000.0 / active.CurrentDivisor;
            if (frequency > 0) hadSound = true;
            sampleRemainder += SampleRate;
            var count = sampleRemainder / TickRate;
            sampleRemainder %= TickRate;
            var halfPeriodSamples = frequency > 0 ? SampleRate / (2 * frequency) : 0;
            for (var i = 0; i < count; i++)
            {
                var pulse = 0.0;
                if (halfPeriodSamples > 0)
                {
                    // Retain the PIT's time to the next transition across
                    // divisor changes, then integrate both levels over this
                    // sample. Point sampling creates inharmonic alias tones.
                    var highDuration = speakerHigh ? samplesToToggle : 0;
                    samplesToToggle -= 1;
                    while (samplesToToggle <= 0)
                    {
                        speakerHigh = !speakerHigh;
                        if (speakerHigh) highDuration += halfPeriodSamples;
                        samplesToToggle += halfPeriodSamples;
                    }
                    if (speakerHigh) highDuration -= samplesToToggle;
                    pulse = (2 * highDuration - 1) * SpeakerAmplitude;
                }
                filteredSample += (pulse - filteredSample) * FilterInput;
                samples.Add((short)Math.Clamp((int)Math.Round(filteredSample), short.MinValue, short.MaxValue));
            }
        }
        if (!hadSound)
            throw new NotSupportedException("This resource has no playable PC speaker notes.");
        // The real low-pass stage continues to settle after the final command.
        for (var i = 0; i < 32; i++)
        {
            filteredSample *= 1 - FilterInput;
            samples.Add((short)Math.Round(filteredSample));
        }
        return ToWave(samples);
    }

    private static int ModulationValue(int curve, int index)
    {
        var tableIndex = LoomPcSpeakerModulationTable.Offsets[curve] + index;
        if ((uint)tableIndex >= LoomPcSpeakerModulationTable.Values.Length)
            throw new InvalidDataException("Sound modulation index is invalid.");
        return LoomPcSpeakerModulationTable.Values[tableIndex];
    }

    private static void AdvanceHull(Channel channel)
    {
        if (channel.HullCounter == 0 || --channel.HullCounter != 0) return;
        var hull = Hulls[channel.HullCurve];
        while (channel.HullPosition < hull.Length)
        {
            var step = hull[channel.HullPosition++];
            if (step.Ticks == -1)
            {
                channel.Volume = (ushort)step.Value;
                if (step.Value == 0) channel.Parameters[6] = 0;
                continue;
            }
            channel.Parameters[6] = (ushort)step.Value;
            channel.HullCounter = step.Ticks;
            return;
        }
    }

    private static void Execute(byte[] data, Channel[] channels, Channel channel, ref int returnPosition)
    {
        var steps = 0;
        while (channel.Position > 0 && channel.Position < data.Length && ++steps < 2048)
        {
            var opcode = data[channel.Position++];
            switch (opcode)
            {
                case 0xF8 or 0xF9:
                    var curve = ReadByte(data, channel);
                    if (opcode == 0xF8)
                    {
                        if (curve / 2 >= Hulls.Length) throw new InvalidDataException("Sound hull curve is invalid.");
                        channel.HullCurve = curve / 2;
                    }
                    else
                    {
                        if (curve / 4 >= ModulationPeriods.Length) throw new InvalidDataException("Sound modulation curve is invalid.");
                        channel.ModulationCurve = curve / 4;
                        channel.Parameters[18] = ModulationPeriods[channel.ModulationCurve];
                    }
                    break;
                case 0xFD:
                    var target = ReadWord(data, channel) / 50;
                    if (target >= 0 && target < channels.Length)
                    {
                        if (channels[target] != channel) channels[target].Position = 0;
                        channels[target].Remaining = 0;
                        channels[target].Volume = 0;
                        channels[target].HullCurve = 0;
                        channels[target].HullPosition = 0;
                        channels[target].HullCounter = 0;
                        channels[target].ModulationCurve = 0;
                        channels[target].ModulationPosition = 0;
                        channels[target].NoteLength = 0;
                        Array.Clear(channels[target].Parameters);
                    }
                    break;
                case 0xFA:
                    channel.Position = 0;
                    channel.Volume = 0;
                    return;
                case 0xFB:
                    if (returnPosition == 0) throw new InvalidDataException("Sound subroutine return has no call.");
                    channel.Position = returnPosition;
                    returnPosition = 0;
                    break;
                case 0xFC:
                    var destination = ReadWord(data, channel);
                    returnPosition = channel.Position;
                    channel.Position = destination;
                    break;
                case 0xFE:
                    var parameter = ReadByte(data, channel) / 2;
                    var relative = (short)ReadWord(data, channel);
                    if (parameter >= channel.Parameters.Length) throw new InvalidDataException("Sound loop parameter is invalid.");
                    if (channel.Parameters[parameter] == 0 || --channel.Parameters[parameter] != 0)
                        channel.Position += relative;
                    break;
                case 0xFF:
                    var index = ReadByte(data, channel) / 2;
                    var value = ReadWord(data, channel);
                    if (index >= channel.Parameters.Length) throw new InvalidDataException("Sound parameter is invalid.");
                    channel.Parameters[index] = value;
                    if (index == 2) channel.Divisor = value;
                    if (index == 5) channel.Volume = value;
                    if (index == 0)
                    {
                        channel.Remaining = value;
                        if (value == 0) channel.Position = 0;
                        return;
                    }
                    break;
                default:
                    var destinationChannel = channels[(opcode >> 5) & 3];
                    int note, duration;
                    bool last;
                    if ((opcode & 0x80) == 0)
                    {
                        var lengthIndex = opcode & 31;
                        if (lengthIndex >= NoteLengths.Length) throw new InvalidDataException("Sound note length is invalid.");
                        duration = Math.Max(1, (channel.Parameters[7] == 0 ? 1 : channel.Parameters[7]) * NoteLengths[lengthIndex]);
                        note = ReadByte(data, channel);
                        last = (note & 0x80) != 0;
                        note &= 0x7F;
                        if (note == 0x7F)
                        {
                            channel.Remaining = duration;
                            return;
                        }
                    }
                    else
                    {
                        duration = Math.Max(1, ((opcode & 7) << 8) | ReadByte(data, channel));
                        if ((opcode & 0x10) != 0)
                        {
                            channel.Remaining = duration;
                            return;
                        }
                        note = ReadByte(data, channel) & 0x7F;
                        last = false;
                    }
                    var pitch = note + (short)destinationChannel.Parameters[9];
                    while (pitch < 0) pitch += 12;
                    var octave = pitch / 12;
                    destinationChannel.Divisor = Math.Max(1, Divisors[pitch % 12] >> Math.Min(octave, 15));
                    destinationChannel.CurrentDivisor = destinationChannel.Divisor;
                    destinationChannel.Remaining = duration;
                    destinationChannel.ModulationPosition = 0;
                    destinationChannel.NoteLength = (ushort)(duration - destinationChannel.Parameters[8]);
                    destinationChannel.HullPosition = 0;
                    destinationChannel.HullCounter = 1;
                    channel.Remaining = duration;
                    if (last) return;
                    break;
            }
        }
        if (steps >= 2048) throw new InvalidDataException("The sound stream does not advance.");
        channel.Position = 0;
        channel.Remaining = 0;
    }

    private static int ReadByte(byte[] data, Channel channel)
    {
        if (channel.Position < 0 || channel.Position >= data.Length) throw new InvalidDataException("The sound stream ends unexpectedly.");
        return data[channel.Position++];
    }

    private static int ReadWord(byte[] data, Channel channel) => ReadByte(data, channel) | (ReadByte(data, channel) << 8);

    private static byte[] ToWave(List<short> samples)
    {
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
        public int Position;
        public int Remaining;
        public int Divisor;
        public int CurrentDivisor;
        public int Volume;
        public int NoteLength;
        public int HullCurve;
        public int HullPosition;
        public int HullCounter;
        public int ModulationCurve;
        public int ModulationPosition;
        public int[] Parameters { get; } = new int[25];
    }

    private readonly record struct HullStep(int Value, int Ticks);
}

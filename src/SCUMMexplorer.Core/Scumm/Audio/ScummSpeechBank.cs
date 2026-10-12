using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The spoken lines of a talkie game (<c>MONSTER.SOU</c> of Monkey Island 2 and Indiana Jones 4, on the disc or in the folder). A script's text carries four
/// "play this sample" codes (0xFF 0x0A and two bytes each) giving the offset of the sample and its length; at that offset a <c>VCTL</c> block (tag, big-endian size
/// of the block, mouth-sync words) is followed by a complete Creative Voice File. ScummVM <c>sound.cpp</c> (<c>startTalkSound</c>) and <c>string.cpp</c> (text code 10).
/// </summary>
public sealed class ScummSpeechBank
{
    private readonly IGameFileSource _source;
    private readonly string _name;

    private ScummSpeechBank(IGameFileSource source, string name)
    {
        _source = source;
        _name = name;
    }

    public string FileName => _name;

    /// <summary>The bank of the game on this source, or null when it has no <c>.SOU</c> file.</summary>
    public static ScummSpeechBank? Find(IGameFileSource source)
    {
        var name = source.FileNames.FirstOrDefault(n => n.Equals("monster.sou", StringComparison.OrdinalIgnoreCase)) ??
                   source.FileNames.FirstOrDefault(n => n.EndsWith(".sou", StringComparison.OrdinalIgnoreCase));
        return name is null ? null : new ScummSpeechBank(source, name);
    }

    /// <summary>
    /// Every sample in the file, in file order. The file is the 8-byte "SOU " header followed by back-to-back samples, each a <c>VCTL</c> block (its size field is the
    /// distance to the voice file) and a Creative Voice File that runs to its end block; the scripts' offsets point at those <c>VCTL</c> blocks.
    /// <c>Length</c> is what <see cref="ReadWave"/> takes.
    /// </summary>
    public IReadOnlyList<(int Offset, int Length)> ListSamples()
    {
        var samples = new List<(int, int)>();
        var total = _source.Length(_name);
        long position = 8;
        while (position + 8 <= total && position < int.MaxValue)
        {
            var header = _source.ReadRange(_name, position, 8);
            if (header.Length < 8 || !header.AsSpan(0, 4).SequenceEqual("VCTL"u8)) break;
            var size = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
            if (size < 8) break;
            var voc = _source.ReadRange(_name, position + size, 26);
            if (voc.Length < 26 || !voc.AsSpan(0, 19).SequenceEqual("Creative Voice File"u8)) break;
            var block = position + size + BinaryPrimitives.ReadUInt16LittleEndian(voc.AsSpan(20));
            for (var guard = 0; guard < 4096 && block < total; guard++)
            {
                var head = _source.ReadRange(_name, block, 4);
                if (head.Length < 1) break;
                if (head[0] == 0) { block++; break; }
                if (head.Length < 4) break;
                block += 4 + (head[1] | head[2] << 8 | head[3] << 16);
            }
            samples.Add(((int)position, size));
            position = block;
        }
        return samples;
    }

    /// <summary>The line as a 16-bit mono wave. Throws <see cref="InvalidDataException"/> for an offset that holds no sample.</summary>
    public byte[] ReadWave(int offset, int length)
    {
        var header = _source.ReadRange(_name, offset, 8);
        if (header.Length < 8 || !header.AsSpan(0, 4).SequenceEqual("VCTL"u8)) throw new InvalidDataException("There is no speech sample at this offset.");
        long position = offset + (length > 8 ? length : 8);
        var voc = _source.ReadRange(_name, position, 32);
        if (voc.Length < 26 || !voc.AsSpan(0, 19).SequenceEqual("Creative Voice File"u8)) throw new InvalidDataException("The speech sample is not a voice file.");
        var dataStart = position + BinaryPrimitives.ReadUInt16LittleEndian(voc.AsSpan(20));
        return DecodeVoc(dataStart);
    }

    /// <summary>The voice blocks from <paramref name="position"/>: sound data (type 1), its continuation (2), silence (3), an extended rate (8), until the end block (0).</summary>
    private byte[] DecodeVoc(long position)
    {
        var samples = new List<byte>();
        var rate = 0;
        for (var guard = 0; guard < 4096; guard++)
        {
            var block = _source.ReadRange(_name, position, 4);
            if (block.Length < 1 || block[0] == 0) break;
            var size = block.Length >= 4 ? block[1] | block[2] << 8 | block[3] << 16 : 0;
            var body = _source.ReadRange(_name, position + 4, size);
            switch (block[0])
            {
                case 1 when body.Length >= 2:
                    if (rate == 0) rate = ScummV5Digital.RateFor(body[0]);
                    if (body[1] != 0) throw new NotSupportedException("The speech sample is packed.");
                    samples.AddRange(body.AsSpan(2).ToArray());
                    break;
                case 2:
                    samples.AddRange(body);
                    break;
                case 3 when body.Length >= 3 && rate != 0:
                    samples.AddRange(Enumerable.Repeat((byte)128, (BinaryPrimitives.ReadUInt16LittleEndian(body) + 1)));
                    break;
                case 8 when body.Length >= 4 && rate == 0:
                    rate = (int)(256000000L / (65536 - BinaryPrimitives.ReadUInt16LittleEndian(body)) / (body[3] + 1));
                    break;
            }
            position += 4 + size;
        }
        if (samples.Count == 0 || rate <= 0) throw new InvalidDataException("The speech sample is empty.");
        var wave = new byte[44 + samples.Count * 2];
        "RIFF"u8.CopyTo(wave);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 36 + samples.Count * 2);
        "WAVEfmt "u8.CopyTo(wave.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28), rate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34), 16);
        "data"u8.CopyTo(wave.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40), samples.Count * 2);
        for (var i = 0; i < samples.Count; i++) BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(44 + i * 2), (short)((samples[i] - 128) << 8));
        return wave;
    }
}

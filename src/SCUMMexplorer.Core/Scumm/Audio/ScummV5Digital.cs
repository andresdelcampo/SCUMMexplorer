using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The sampled effects of the v5 games (the <c>SBL </c> part of a sound, Sound Blaster): an <c>AUhd</c> block (3 bytes) and an <c>AUdt</c> block holding one Creative
/// Voice block (type 1: 3-byte length, rate byte, codec 0, then unsigned 8-bit samples). ScummVM <c>sound.cpp</c>, "Support for sampled sound effects in Monkey Island 1 and 2".
/// </summary>
public static class ScummV5Digital
{
    /// <summary>A 16-bit mono wave of the effect.</summary>
    public static byte[] RenderWave(byte[] part)
    {
        // The part's body starts with AUhd (tag, size 3, 3 bytes) and AUdt (tag, size); the VOC block follows at byte 19.
        const int voc = 19;
        if (part.Length < voc + 6 || part[voc] != 1) throw new InvalidDataException("The sampled sound holds no voice block.");
        var length = part[voc + 1] | part[voc + 2] << 8 | part[voc + 3] << 16;
        if (part[voc + 5] != 0) throw new NotSupportedException("The sampled sound is packed.");
        var count = Math.Min(length - 2, part.Length - voc - 6);
        if (count <= 0) throw new InvalidDataException("The sampled sound is empty.");
        var rate = RateFor(part[voc + 4]);
        var wave = new byte[44 + count * 2];
        "RIFF"u8.CopyTo(wave);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 36 + count * 2);
        "WAVEfmt "u8.CopyTo(wave.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28), rate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34), 16);
        "data"u8.CopyTo(wave.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40), count * 2);
        for (var i = 0; i < count; i++) BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(44 + i * 2), (short)((part[voc + 6 + i] - 128) << 8));
        return wave;
    }

    /// <summary>The sample rate of a voice-block rate byte (ScummVM rounds the two usual ones to 11025 and 22050 Hz).</summary>
    public static int RateFor(byte code) => code switch { 0xA5 => 11025, 0xD2 => 22050, _ => (int)(1000000.0 / (256 - code)) };
}

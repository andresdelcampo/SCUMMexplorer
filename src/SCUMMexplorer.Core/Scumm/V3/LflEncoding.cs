namespace SCUMMexplorer.Core.Scumm;

/// <summary>Reads the plain or XOR-FF DOS LFL data used by early SCUMM releases.</summary>
public static class LflEncoding
{
    public static byte[] Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 2)
            throw new InvalidDataException("The LFL file is too short.");

        if (bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return DecodeXorFF(bytes);
        }
        return bytes;
    }

    /// <summary>Decodes a numbered DOS LOOM LFL container, which is XOR-FF encoded without a signature.</summary>
    public static byte[] DecodeXorFF(ReadOnlySpan<byte> encoded)
    {
        var decoded = encoded.ToArray();
        for (var i = 0; i < decoded.Length; i++) decoded[i] ^= 0xFF;
        return decoded;
    }
}

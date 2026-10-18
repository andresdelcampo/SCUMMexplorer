using System.Security.Cryptography;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Loads reviewed native DOS fonts from the user's executable; no executable or font asset is shipped.</summary>
public static class ScummV2NativeFont
{
    private sealed record Source(string File, string ExecutableHash, int EncodedOffset, int EncodedLength,
        int DecodedPrefix, string FontHash);

    // Edition-specific resource locations are fingerprinted data, not language or folder-name tests.
    // The first zero run includes 34 bytes before the font; font boundaries are independently
    // pinned by all 114 unchanged ASCII/control glyphs and the complete decoded-font hash.
    private static readonly Source[] Sources =
    [
        new("MANIAC.EXE", "AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A",
            64128, 1012, 34, "098B285F43AEC7C97CA2C05D07D31365D2C8371304645AFFC20FEBCE7B12453D")
    ];

    public static byte[]? TryLoad(string folderPath)
    {
        foreach (var source in Sources)
        {
            try
            {
                var path = Path.Combine(folderPath, source.File);
                if (!File.Exists(path)) continue;
                var executable = File.ReadAllBytes(path);
                if (Convert.ToHexString(SHA256.HashData(executable)) != source.ExecutableHash ||
                    executable.Length < source.EncodedOffset + source.EncodedLength) continue;
                var decoded = Decode(executable.AsSpan(source.EncodedOffset, source.EncodedLength));
                if (decoded.Length != source.DecodedPrefix + 1024) continue;
                var font = decoded.AsSpan(source.DecodedPrefix, 1024).ToArray();
                if (Convert.ToHexString(SHA256.HashData(font)) == source.FontHash) return font;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Optional native data is unavailable; retain the existing built-in renderer.
            }
        }
        return null;
    }

    // ScummVM charset-fontdata.cpp's preserved executable font extraction loop,
    // source fetched 17 October and reviewed 18 October 2026. The seven-byte
    // marker expands a repeated byte; remaining bytes are literal bitmap rows.
    private static byte[] Decode(ReadOnlySpan<byte> encoded)
    {
        var decoded = new List<byte>();
        for (var offset = 0; offset < encoded.Length; offset++)
        {
            if (offset + 6 < encoded.Length && encoded[offset + 1] == 0 && encoded[offset + 2] == 0xB2 &&
                encoded[offset + 5] == 0 && encoded[offset + 6] == 0xB0)
            {
                for (var count = 0; count < encoded[offset + 4]; count++) decoded.Add(encoded[offset + 3]);
                offset += 6;
            }
            else decoded.Add(encoded[offset]);
        }
        return decoded.ToArray();
    }
}

using System.Security.Cryptography;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Reads reviewed DOS built-in fonts from local executable data without running it.
/// An unreviewed or absent executable leaves the existing built-in fallback intact.
/// </summary>
public static class ScummV2NativeFont
{
    private sealed record Descriptor(string FileName, string ExecutableHash, int Offset, string FontHash);

    // Data descriptors, not language rules. Read-only evidence, 18 October 2026:
    // .verification/maniacenhanced-cutscenes-20261018/font-proof.py.
    private static readonly Descriptor[] Reviewed =
    [
        new("MANIAC.EXE", "AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A",
            64116, "336B4DFC32EB17CDF6BA78D9116C7F219605807D9573F185A3CDB0340652813A")
    ];

    /// <summary>Returns a private copy of 128 eight-row glyphs only when both fingerprints match.</summary>
    public static byte[]? TryLoad(string folderPath)
    {
        foreach (var descriptor in Reviewed)
        {
            try
            {
                var path = Path.Combine(folderPath, descriptor.FileName);
                if (!File.Exists(path)) continue;
                var data = File.ReadAllBytes(path);
                if (Convert.ToHexString(SHA256.HashData(data)) != descriptor.ExecutableHash ||
                    descriptor.Offset > data.Length - 128 * 8) continue;
                var font = data.AsSpan(descriptor.Offset, 128 * 8);
                if (Convert.ToHexString(SHA256.HashData(font)) == descriptor.FontHash) return font.ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Read-only optional data; ordinary game detection and Play do not require the executable.
            }
        }
        return null;
    }
}

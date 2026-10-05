using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Storage;

public sealed record DiscFile(string Name, int FirstSector, int Length);

/// <summary>
/// An ISO 9660 data track read from a raw .bin (MODE1/2352 sectors, user data at byte 16) or a plain .iso (2048-byte sectors),
/// named by a .cue sheet, an image file or a folder that holds one. The other tracks (CD audio) are left alone.
/// </summary>
public sealed class DiscImage
{
    private const int UserBytes = 2048;
    private readonly int _sectorSize;
    private readonly int _userOffset;

    private DiscImage(string binPath, int sectorSize, int userOffset)
    {
        BinPath = binPath; _sectorSize = sectorSize; _userOffset = userOffset;
    }

    public string BinPath { get; }

    /// <summary>The cue sheet's CD audio track start (INDEX 01) in sectors from the image's start, when the cue has one.</summary>
    public int? AudioStartSector { get; private init; }

    public static bool IsImageFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".cue" or ".iso" or ".bin" or ".img";

    /// <summary>The image file (.cue first, then .iso, .bin, .img) inside a folder, or null.</summary>
    public static string? FindImageIn(string folder)
    {
        var files = Directory.EnumerateFiles(folder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        return files.FirstOrDefault(p => Path.GetExtension(p).Equals(".cue", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(p => Path.GetExtension(p).ToLowerInvariant() is ".iso" or ".bin" or ".img");
    }

    public static DiscImage Open(string path)
    {
        if (Directory.Exists(path)) path = FindImageIn(path) ?? throw new FileNotFoundException("No .cue, .iso or .bin image in the folder.", path);
        int? audio = null;
        var bin = path;
        if (Path.GetExtension(path).Equals(".cue", StringComparison.OrdinalIgnoreCase)) (bin, audio) = ReadCue(path);
        // A raw MODE1 sector starts with 00, ten FF bytes and 00; a plain ISO does not.
        using var stream = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[12];
        stream.ReadExactly(head);
        var raw = head[0] == 0 && head[11] == 0 && head.Skip(1).Take(10).All(b => b == 0xFF);
        return new DiscImage(bin, raw ? 2352 : 2048, raw ? 16 : 0) { AudioStartSector = audio };
    }

    private static (string Bin, int? AudioStart) ReadCue(string cuePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        string? file = null; int? audio = null; var trackIsAudio = false;
        foreach (var raw in File.ReadLines(cuePath))
        {
            var line = raw.Trim();
            if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase) && file is null)
            {
                var a = line.IndexOf('"'); var b = line.LastIndexOf('"');
                file = a >= 0 && b > a ? line[(a + 1)..b] : line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            }
            else if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
                trackIsAudio = line.Contains("AUDIO", StringComparison.OrdinalIgnoreCase);
            else if (line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase) && trackIsAudio && audio is null)
            {
                var t = line[8..].Trim().Split(':');
                if (t.Length == 3 && int.TryParse(t[0], out var m) && int.TryParse(t[1], out var s) && int.TryParse(t[2], out var f))
                    audio = (m * 60 + s) * 75 + f;
            }
        }
        if (file is null) throw new InvalidDataException("The cue sheet names no file.");
        var bin = Path.Combine(dir, file);
        if (!File.Exists(bin))
        {
            // Dumps get renamed without their cue being edited: take the one image file beside the cue.
            var images = Directory.EnumerateFiles(dir).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".bin" or ".img" or ".iso").ToArray();
            bin = images.Length == 1 ? images[0] : throw new FileNotFoundException($"The cue sheet names {file}, which is not beside it.", bin);
        }
        return (bin, audio);
    }

    private byte[] ReadSectors(int first, int count)
    {
        using var stream = new FileStream(BinPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = new byte[count * UserBytes];
        for (var i = 0; i < count; i++)
        {
            stream.Seek((long)(first + i) * _sectorSize + _userOffset, SeekOrigin.Begin);
            stream.ReadExactly(result.AsSpan(i * UserBytes, UserBytes));
        }
        return result;
    }

    public byte[] Read(DiscFile file) => ReadSectors(file.FirstSector, (file.Length + UserBytes - 1) / UserBytes)[..file.Length];

    /// <summary>The files in the root directory and below (names without the ;1 version suffix, sub-directories as a/b).</summary>
    public IReadOnlyList<DiscFile> ListFiles()
    {
        var pvd = ReadSectors(16, 1);
        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
            throw new InvalidDataException("No ISO 9660 volume descriptor at sector 16.");
        var files = new List<DiscFile>();
        Walk(BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(158)), BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(166)), "", files, 0);
        return files;
    }

    private void Walk(int sector, int length, string prefix, List<DiscFile> files, int depth)
    {
        if (depth > 8) return;
        var data = ReadSectors(sector, (length + UserBytes - 1) / UserBytes);
        var pos = 0;
        while (pos < length)
        {
            var size = data[pos];
            if (size == 0) { pos = (pos / UserBytes + 1) * UserBytes; continue; }
            var record = data.AsSpan(pos, size);
            pos += size;
            var nameLength = record[32];
            if (nameLength == 1 && record[33] <= 1) continue;
            var name = Encoding.Latin1.GetString(record.Slice(33, nameLength));
            var semicolon = name.IndexOf(';');
            if (semicolon >= 0) name = name[..semicolon];
            var start = BinaryPrimitives.ReadInt32LittleEndian(record[2..]);
            var len = BinaryPrimitives.ReadInt32LittleEndian(record[10..]);
            if ((record[25] & 2) != 0) Walk(start, len, prefix + name + "/", files, depth + 1);
            else files.Add(new DiscFile(prefix + name.TrimEnd('.'), start, len));
        }
    }
}

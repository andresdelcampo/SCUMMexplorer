using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

public sealed record DiscFile(string Name, int FirstSector, int Length);

/// <summary>A CD audio track of the image: its number in the cue sheet and its sectors (1/75 s each) from the start of the image, the end excluded.</summary>
public sealed record DiscAudioTrack(int Number, int StartSector, int EndSector)
{
    public int Frames => EndSector - StartSector;
    public double Seconds => Frames / 75.0;
}

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

    /// <summary>Every CD audio track the cue sheet names, in order (empty for an image without a cue or without audio).</summary>
    public IReadOnlyList<DiscAudioTrack> AudioTracks { get; private init; } = [];

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
        var cueTracks = new List<(int Number, bool Audio, int? Index0, int? Index1)>();
        if (Path.GetExtension(path).Equals(".cue", StringComparison.OrdinalIgnoreCase)) (bin, audio, cueTracks) = ReadCue(path);
        // A raw MODE1 sector starts with 00, ten FF bytes and 00; a plain ISO does not.
        using var stream = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[12];
        stream.ReadExactly(head);
        var raw = head[0] == 0 && head[11] == 0 && head.Skip(1).Take(10).All(b => b == 0xFF);
        // A track ends where the next begins: at its INDEX 00 (the pregap stored in the file) when it has one, else at its INDEX 01; the last at the end of the file.
        var sectorSize = raw ? 2352 : 2048;
        var tracks = new List<DiscAudioTrack>();
        for (var i = 0; i < cueTracks.Count; i++)
        {
            if (!cueTracks[i].Audio || cueTracks[i].Index1 is not int start) continue;
            var end = i + 1 < cueTracks.Count ? cueTracks[i + 1].Index0 ?? cueTracks[i + 1].Index1 : null;
            tracks.Add(new DiscAudioTrack(cueTracks[i].Number, start, end ?? (int)(stream.Length / sectorSize)));
        }
        return new DiscImage(bin, sectorSize, raw ? 16 : 0) { AudioStartSector = audio, AudioTracks = tracks.Where(t => t.Frames > 0).ToList() };
    }

    private static int? CueTime(string text)
    {
        var t = text.Trim().Split(':');
        return t.Length == 3 && int.TryParse(t[0], out var m) && int.TryParse(t[1], out var s) && int.TryParse(t[2], out var f) ? (m * 60 + s) * 75 + f : null;
    }

    private static (string Bin, int? AudioStart, List<(int Number, bool Audio, int? Index0, int? Index1)> Tracks) ReadCue(string cuePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        string? file = null; int? audio = null; var trackIsAudio = false;
        var tracks = new List<(int Number, bool Audio, int? Index0, int? Index1)>();
        foreach (var raw in File.ReadLines(cuePath))
        {
            var line = raw.Trim();
            if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase) && file is null)
            {
                var a = line.IndexOf('"'); var b = line.LastIndexOf('"');
                file = a >= 0 && b > a ? line[(a + 1)..b] : line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            }
            else if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
            {
                trackIsAudio = line.Contains("AUDIO", StringComparison.OrdinalIgnoreCase);
                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                tracks.Add((words.Length > 1 && int.TryParse(words[1], out var number) ? number : tracks.Count + 1, trackIsAudio, null, null));
            }
            else if (line.StartsWith("INDEX 00", StringComparison.OrdinalIgnoreCase) && tracks.Count > 0)
                tracks[^1] = tracks[^1] with { Index0 = CueTime(line[8..]) };
            else if (line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase) && tracks.Count > 0)
            {
                tracks[^1] = tracks[^1] with { Index1 = CueTime(line[8..]) };
                if (trackIsAudio && audio is null) audio = CueTime(line[8..]);
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
        return (bin, audio, tracks);
    }

    public const int AudioSectorBytes = 2352;
    public const int AudioSampleRate = 44100;

    /// <summary>
    /// Raw CD audio (44.1 kHz, 16-bit, stereo, little-endian) from the first audio track: <paramref name="frames"/> sectors of
    /// 1/75 s starting <paramref name="startFrame"/> frames after the track's INDEX 01. Empty when the cue names no audio track
    /// or the position is past the end of the image; a request that runs past the end is cut short.
    /// </summary>
    public byte[] ReadAudio(int startFrame, int frames)
    {
        if (AudioStartSector is not { } start || startFrame < 0 || frames <= 0) return [];
        using var stream = new FileStream(BinPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var offset = (long)(start + startFrame) * AudioSectorBytes;
        if (_sectorSize != AudioSectorBytes || offset >= stream.Length) return [];
        var count = (int)Math.Min(frames, (stream.Length - offset) / AudioSectorBytes);
        var data = new byte[count * AudioSectorBytes];
        stream.Seek(offset, SeekOrigin.Begin);
        stream.ReadExactly(data);
        return data;
    }

    /// <summary>A whole CD audio track as a WAV file's bytes, or empty when the track is not in the image.</summary>
    public byte[] ReadTrackWave(int trackNumber)
    {
        if (AudioTracks.FirstOrDefault(t => t.Number == trackNumber) is not { } track || _sectorSize != AudioSectorBytes) return [];
        using var stream = new FileStream(BinPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var offset = (long)track.StartSector * AudioSectorBytes;
        if (offset >= stream.Length) return [];
        var count = (int)Math.Min(track.Frames, (stream.Length - offset) / AudioSectorBytes);
        var pcm = new byte[count * AudioSectorBytes];
        stream.Seek(offset, SeekOrigin.Begin);
        stream.ReadExactly(pcm);
        return Wave(pcm);
    }

    /// <summary>The same audio as a WAV file's bytes (see <see cref="ReadAudio"/>), or empty.</summary>
    public byte[] ReadAudioWave(int startFrame, int frames)
    {
        var pcm = ReadAudio(startFrame, frames);
        return pcm.Length == 0 ? [] : Wave(pcm);
    }

    private static byte[] Wave(byte[] pcm)
    {
        var wave = new byte[44 + pcm.Length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wave, 0);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 36 + pcm.Length);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wave, 8);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22), 2);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24), AudioSampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28), AudioSampleRate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32), 4);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34), 16);
        Encoding.ASCII.GetBytes("data").CopyTo(wave, 36);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40), pcm.Length);
        pcm.CopyTo(wave, 44);
        return wave;
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

    /// <summary>A stretch of a file of the data track (shorter at its end), read without the rest of the file.</summary>
    public byte[] ReadRange(DiscFile file, long offset, int count)
    {
        if (offset >= file.Length) return [];
        var length = (int)Math.Min(count, file.Length - offset);
        var firstSector = (int)(offset / UserBytes);
        var skip = (int)(offset % UserBytes);
        var sectors = (skip + length + UserBytes - 1) / UserBytes;
        return ReadSectors(file.FirstSector + firstSector, sectors).AsSpan(skip, length).ToArray();
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

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>The sizes of a game's resource directories as its format lists them: how many rooms, costumes, scripts (global and room-local) and sounds.</summary>
public sealed record DirectoryShape(ScummVersion Version, int Rooms, int Costumes, int Scripts, int Sounds);

/// <summary>
/// What a game is told by, whatever its format: the SHA-256 of its data files and the sizes of its resource directories. The data files are the containers the
/// engine reads (<c>NN.LFL</c>, <c>DISKnn.LEC</c>, the v5 index <c>NAME.000</c>), keyed by their file name in the game's own folder; executables, sound drivers
/// and the big v5 resource and speech files are not part of it. Hashes are made only when asked for and kept.
/// </summary>
public sealed partial class GameFingerprint
{
    private readonly Dictionary<string, Func<byte[]>> _files;
    private readonly Dictionary<string, string?> _hashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<DirectoryShape?> _shape;

    public GameFingerprint(IReadOnlyDictionary<string, Func<byte[]>> files, Func<DirectoryShape?>? shape = null)
    {
        _files = new Dictionary<string, Func<byte[]>>(files, StringComparer.OrdinalIgnoreCase);
        _shape = new Lazy<DirectoryShape?>(shape ?? (() => null));
    }

    /// <summary>The data files of a folder (<paramref name="pathsByName"/>: file name to path), with no directory sizes.</summary>
    public static GameFingerprint OfFiles(IReadOnlyDictionary<string, string> pathsByName) =>
        new(pathsByName.Where(pair => IsDataFile(pair.Key)).ToDictionary(pair => pair.Key, pair => (Func<byte[]>)(() => File.ReadAllBytes(pair.Value))));

    /// <summary>The data files of <paramref name="folder"/> inside <paramref name="source"/> (null: the top of the source).</summary>
    public static GameFingerprint Of(IGameFileSource source, string? folder, Func<DirectoryShape?>? shape = null)
    {
        var prefix = folder is null ? "" : folder.TrimEnd('/') + "/";
        var files = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in source.FileNames)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var own = name[prefix.Length..];
            if (own.Contains('/') || !IsDataFile(own)) continue;
            files[own] = () => source.ReadAllBytes(name);
        }
        return new GameFingerprint(files, shape);
    }

    /// <summary>Whether a file name is one of the data containers a game is told by.</summary>
    public static bool IsDataFile(string fileName) => DataFile().IsMatch(fileName);

    [GeneratedRegex(@"^(\d{2,3}\.LFL|DISK\d{2}\.LEC|[^/\\]+\.000)$", RegexOptions.IgnoreCase)]
    private static partial Regex DataFile();

    public IEnumerable<string> FileNames => _files.Keys;

    public int FileCount => _files.Count;

    /// <summary>The sizes of the resource directories, read only when asked for; null when the format's index was not read.</summary>
    public DirectoryShape? Shape => _shape.Value;

    /// <summary>The SHA-256 of a data file (upper-case hex), or null when the game has no such file or it cannot be read.</summary>
    public string? Hash(string fileName)
    {
        if (_hashes.TryGetValue(fileName, out var known)) return known;
        string? hash = null;
        if (_files.TryGetValue(fileName, out var read))
            try { hash = Convert.ToHexString(SHA256.HashData(read())); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return _hashes[fileName] = hash;
    }
}

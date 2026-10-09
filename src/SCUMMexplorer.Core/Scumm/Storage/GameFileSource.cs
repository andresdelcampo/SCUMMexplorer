namespace SCUMMexplorer.Core.Scumm;

/// <summary>Where a game's files are read from: a folder, or the data track of a disc image.</summary>
public interface IGameFileSource
{
    /// <summary>What the user picked (a folder, a .cue, .iso or .bin path), for messages.</summary>
    string Location { get; }
    IReadOnlyList<string> FileNames { get; }
    bool Exists(string name);
    byte[] ReadAllBytes(string name);
}

public sealed class FolderFileSource(string folder) : IGameFileSource
{
    public string Location => folder;
    public IReadOnlyList<string> FileNames { get; } = Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().ToArray();
    public bool Exists(string name) => File.Exists(Path.Combine(folder, name));
    public byte[] ReadAllBytes(string name) => File.ReadAllBytes(Path.Combine(folder, name));
}

/// <summary>The files of a disc image's ISO 9660 data track. Files are read when asked for and kept (the game's are a few MB).</summary>
public sealed class DiscFileSource : IGameFileSource
{
    private readonly Dictionary<string, DiscFile> _files;
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DiscFileSource(string location)
    {
        Location = location;
        Image = DiscImage.Open(location);
        _files = Image.ListFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string Location { get; }
    public DiscImage Image { get; }
    public IReadOnlyList<string> FileNames => [.. _files.Keys];
    public bool Exists(string name) => _files.ContainsKey(name);

    public byte[] ReadAllBytes(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;
        if (!_files.TryGetValue(name, out var file)) throw new FileNotFoundException($"{name} is not on the disc image.", name);
        return _cache[name] = Image.Read(file);
    }
}

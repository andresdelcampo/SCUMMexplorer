using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One resource in an old-v3 LFL file: its start and its 16-bit length.</summary>
public readonly record struct LflChainResource(int Start, int Length);

/// <summary>
/// An index entry whose offset is not a resource start in its LFL file but
/// lies inside another resource. <see cref="Owner"/> names the index entry
/// that starts that resource, or is null when no entry does.
/// </summary>
public sealed record LflLeftoverIndexEntry(GameResource Entry, LflChainResource Container, GameResource? Owner)
{
    public string OwnerLabel => Owner?.Name ?? $"an unindexed resource at 0x{Container.Start:X4}";

    public string Describe() =>
        $"{Entry.Name}: leftover index entry (points inside {OwnerLabel}); " +
        $"{Entry.ContainerFileNumber:00}.LFL 0x{Entry.Offset:X4} is {Entry.Offset - Container.Start} bytes into " +
        $"the resource at 0x{Container.Start:X4}.";
}

/// <summary>
/// Walks the resources of an old-v3 LFL file (XOR 0xFF), each a 16-bit length
/// followed by its data, the way ScummVM's <c>loadResource</c> reads one. This
/// separates index entries that start a resource from entries left over in
/// 00.LFL that point inside another one.
/// </summary>
public sealed class LflResourceChain
{
    private readonly Dictionary<int, IReadOnlyList<LflChainResource>?> _chains = [];

    public static IReadOnlyList<LflChainResource> Read(byte[] encoded)
    {
        var data = LflEncoding.DecodeXorFF(encoded);
        var chain = new List<LflChainResource>();
        for (var position = 0; position + 2 <= data.Length;)
        {
            var length = data[position] | data[position + 1] << 8;
            // A resource holds at least its length and tag.
            if (length < 4) break;
            chain.Add(new LflChainResource(position, length));
            position += length;
        }
        return chain;
    }

    /// <summary>
    /// Returns the leftover description when <paramref name="entry"/> points
    /// strictly inside a resource of its LFL file, and null when it starts a
    /// resource, lies outside the chain or its file cannot be read.
    /// </summary>
    public LflLeftoverIndexEntry? FindLeftover(GameLibrary game, GameResource entry)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.ContainerFileNumber is not int file || entry.Offset is not int offset) return null;
        var chain = Chain(game, file);
        if (chain is null || chain.Any(item => item.Start == offset)) return null;
        var container = chain.FirstOrDefault(item => item.Start < offset && offset < item.Start + item.Length);
        if (container.Length == 0) return null;
        var owner = game.Resources.FirstOrDefault(item => item != entry && item.ContainerFileNumber == file &&
                                                          item.Offset == container.Start);
        return new LflLeftoverIndexEntry(entry, container, owner);
    }

    private IReadOnlyList<LflChainResource>? Chain(GameLibrary game, int file)
    {
        if (_chains.TryGetValue(file, out var chain)) return chain;
        var path = Path.Combine(game.FolderPath, $"{file:00}.LFL");
        try { chain = File.Exists(path) ? Read(File.ReadAllBytes(path)) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { chain = null; }
        _chains[file] = chain;
        return chain;
    }
}

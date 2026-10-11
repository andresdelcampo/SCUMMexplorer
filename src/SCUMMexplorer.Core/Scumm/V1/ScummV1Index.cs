using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Which of the two classic (SCUMM v1) games a folder holds; they differ in the colour table that turns the C64 colour numbers of the data into EGA colours.</summary>
public enum ScummV1Game { ManiacMansion, ZakMcKracken }

/// <summary>
/// The index of a classic SCUMM v1 game (<c>00.LFL</c>, XOR 0xFF): the magic 0x0A31, one byte per global object, then the directories of rooms, costumes, scripts and
/// sounds, whose sizes are fixed per game and are not stored (ScummVM's <c>readClassicIndexFile</c>). A room directory is a skipped byte per room and a word offset; the
/// others a room byte and a word offset per resource. The game is the one whose sizes add up to exactly the length of the file.
/// </summary>
public sealed record ScummV1Index(ScummV1Game Game, IReadOnlyList<IndexedResource> Resources)
{
    private sealed record Layout(ScummV1Game Game, int Objects, int Rooms, int Costumes, int Scripts, int Sounds);

    private static readonly Layout[] Layouts =
    [
        new(ScummV1Game.ManiacMansion, 800, 55, 35, 200, 100),
        new(ScummV1Game.ZakMcKracken, 775, 61, 37, 155, 120)
    ];

    /// <summary>The classic index, or null when the file has another magic or the sizes of no known game add up to its length.</summary>
    public static ScummV1Index? TryRead(string indexFilePath)
    {
        byte[] data;
        try { data = LflEncoding.DecodeXorFF(File.ReadAllBytes(indexFilePath)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        if (data.Length < 2 || data[0] != 0x31 || data[1] != 0x0A) return null;
        foreach (var layout in Layouts)
        {
            if (2 + layout.Objects + 3 * (layout.Rooms + layout.Costumes + layout.Scripts + layout.Sounds) != data.Length) continue;
            var resources = new List<IndexedResource>();
            var position = 2 + layout.Objects;
            position = ReadDirectory(data, position, layout.Rooms, ResourceKind.Room, roomDirectory: true, resources);
            position = ReadDirectory(data, position, layout.Costumes, ResourceKind.Costume, roomDirectory: false, resources);
            position = ReadDirectory(data, position, layout.Scripts, ResourceKind.Script, roomDirectory: false, resources);
            ReadDirectory(data, position, layout.Sounds, ResourceKind.Sound, roomDirectory: false, resources);
            return new ScummV1Index(layout.Game, resources);
        }
        return null;
    }

    private static int ReadDirectory(byte[] data, int position, int count, ResourceKind kind, bool roomDirectory, List<IndexedResource> output)
    {
        var rooms = position;
        var offsets = position + count;
        for (var i = 0; i < count; i++)
        {
            var offset = data[offsets + 2 * i] | data[offsets + 2 * i + 1] << 8;
            if (offset == 0xFFFF) continue;
            output.Add(new IndexedResource(kind, i, roomDirectory ? i : data[rooms + i], offset, offsets + 2 * i, roomDirectory ? null : rooms + i));
        }
        return offsets + 2 * count;
    }
}

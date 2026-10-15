namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What the browse-only games need from one archive of resources: its rooms with their pictures, its costumes with their poses and its sounds. The v4 archives
/// (<see cref="ScummV4Archive"/>) and the v5 one (<see cref="ScummV5Archive"/>) both give it, so the scanner and the window read either the same way.
/// </summary>
/// <summary>An object of a room as the pointer reveals it: its number, the name stored with it (empty when it has none) and its rectangle in room pixels.</summary>
public sealed record BrowsedObjectLabel(int Id, string Name, int X, int Y, int Width, int Height);

public interface IBrowsableArchive
{
    IReadOnlyList<ScummV4Room> Rooms { get; }

    /// <summary>The room's background as palette indices with its palette; throws <see cref="InvalidDataException"/> when the room has no picture or one that does not decode.</summary>
    RoomImage DecodeRoom(int number);

    /// <summary>The room's walk boxes (the box matrix is not kept where the archive stores it apart); null when the room has none or they do not read.</summary>
    ScummWalkBoxSet? ReadWalkBoxes(int number);

    /// <summary>The room's objects with a name and a rectangle, in directory order; empty when they cannot be read.</summary>
    IReadOnlyList<BrowsedObjectLabel> ReadObjectLabels(int number);

    IReadOnlyList<ScummV4Costume> ReadCostumes();

    ScummV4Costume? FindCostume(int id);

    /// <summary>Every stored pose of a costume that decodes, in the palette of the room that stores the costume.</summary>
    IReadOnlyList<(int Pose, RoomImage Image)> DecodeCostumePoses(ScummV4Costume costume);

    IReadOnlyList<ScummV4Sound> ReadSounds();

    /// <summary>The room's objects as stored: records (position, size, parent, walk-to point), every stored image (an object with several states has several, in file order) and the names. Nothing is run.</summary>
    BrowsedObjects ReadBrowsedObjects(int room);
}

/// <summary>What the browse-only games show of a room's objects: the stored records, the stored images (palette indices of the room's palette) and the stored names (by object id; an object without a name is absent).</summary>
public sealed record BrowsedObjects(IReadOnlyList<ScummRoomObjectRecord> Records, IReadOnlyList<RoomObjectImage> Images, IReadOnlyDictionary<int, string> Names)
{
    public static BrowsedObjects Empty { get; } = new([], [], new Dictionary<int, string>());

    /// <summary>
    /// An object with several stored images (its states, in file order) keeps its id for the first and gets <c>id | (n - 1) &lt;&lt; 16</c> for state n, so each image is its own
    /// entry of <see cref="RoomObjectSelection"/> (which cycles the entries of one place); <see cref="BaseId"/> gives the object's id back.
    /// </summary>
    public static IReadOnlyList<RoomObjectImage> NumberStates(IEnumerable<RoomObjectImage> images)
    {
        var seen = new Dictionary<int, int>();
        return images.Select(image =>
        {
            var n = seen.GetValueOrDefault(image.Id);
            seen[image.Id] = n + 1;
            return n == 0 ? image : image with { Id = image.Id | n << 16 };
        }).ToArray();
    }

    /// <summary>The object's own id for an entry numbered by <see cref="NumberStates"/>.</summary>
    public static int BaseId(int id) => id & 0xFFFF;
}

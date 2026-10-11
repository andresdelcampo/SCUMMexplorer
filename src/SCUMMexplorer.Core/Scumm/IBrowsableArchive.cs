namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What the browse-only games need from one archive of resources: its rooms with their pictures, its costumes with their poses and its sounds. The v4 archives
/// (<see cref="ScummV4Archive"/>) and the v5 one (<see cref="ScummV5Archive"/>) both give it, so the scanner and the window read either the same way.
/// </summary>
public interface IBrowsableArchive
{
    IReadOnlyList<ScummV4Room> Rooms { get; }

    /// <summary>The room's background as palette indices with its palette; throws <see cref="InvalidDataException"/> when the room has no picture or one that does not decode.</summary>
    RoomImage DecodeRoom(int number);

    /// <summary>The room's walk boxes (the box matrix is not kept where the archive stores it apart); null when the room has none or they do not read.</summary>
    ScummWalkBoxSet? ReadWalkBoxes(int number);

    IReadOnlyList<ScummV4Costume> ReadCostumes();

    ScummV4Costume? FindCostume(int id);

    /// <summary>Every stored pose of a costume that decodes, in the palette of the room that stores the costume.</summary>
    IReadOnlyList<(int Pose, RoomImage Image)> DecodeCostumePoses(ScummV4Costume costume);

    IReadOnlyList<ScummV4Sound> ReadSounds();
}

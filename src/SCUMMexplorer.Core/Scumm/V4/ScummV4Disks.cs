using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A v4 game whose rooms sit on several disks (<c>DISK01.LEC</c>, <c>DISK02.LEC</c>, …; Monkey Island has four VGA or eight EGA ones), each read as its own
/// <see cref="ScummV4Archive"/>. A resource of such a game says which disk holds it in <see cref="GameResource.ContainerFileNumber"/>.
/// Used for browsing only (rooms, costumes, sounds); the live LOOM CD session works on the single-disk <see cref="ScummV4Archive"/>.
/// </summary>
public sealed class ScummV4Disks
{
    private ScummV4Disks(IReadOnlyList<ScummV4Archive> disks) => Disks = disks;

    /// <summary>The disks in order: element 0 is <c>DISK01.LEC</c>.</summary>
    public IReadOnlyList<ScummV4Archive> Disks { get; }

    /// <summary>Loads every consecutive <c>DISKnn.LEC</c> that the source has, starting at 1; throws when there is no first disk.</summary>
    public static ScummV4Disks Load(IGameFileSource source)
    {
        var disks = new List<ScummV4Archive>();
        // One archive built from the plain per-room files (Indiana Jones 256 colours) counts as the single disk.
        if (!source.Exists(ScummV4Archive.DiskFileName(1)) && ScummV4Archive.IsRoomFileLayout(source))
            return new ScummV4Disks([ScummV4Archive.LoadRoomFiles(source)]);
        while (source.Exists(ScummV4Archive.DiskFileName(disks.Count + 1)))
            disks.Add(ScummV4Archive.Load(source, ScummV4Archive.DiskFileName(disks.Count + 1)));
        if (disks.Count == 0) throw new InvalidDataException("000.LFL and DISK01.LEC are both needed.");
        return new ScummV4Disks(disks);
    }

    /// <summary>The archive holding the resource (its disk number is the container file number), or null when the resource names no disk of this game.</summary>
    public ScummV4Archive? ArchiveFor(GameResource resource) =>
        resource.ContainerFileNumber is int disk && disk >= 1 && disk <= Disks.Count ? Disks[disk - 1] : null;
}

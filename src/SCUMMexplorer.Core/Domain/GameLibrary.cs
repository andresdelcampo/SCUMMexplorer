namespace SCUMMexplorer.Core.Domain;

public enum ScummVersion
{
    Unknown,
    V1,
    V2,
    V3,
    V4,
    V5,
    V6,
    V7,
    V8
}

public enum GameId
{
    Unknown,
    Loom,
    IndianaJones3,
    /// <summary>LOOM on CD (SCUMM v4, VGA): DISK01.LEC, read from a folder or a disc image.</summary>
    LoomVga
}

public enum ResourceKind
{
    Room,
    Character,
    Costume,
    Object,
    Hotspot,
    Dialogue,
    Script,
    Sound,
    Executable,
    RawResource,
    Other
}

public sealed record GameResource(
    string Name,
    ResourceKind Kind,
    string FilePath,
    int? ResourceId = null,
    string? Description = null,
    int? ContainerFileNumber = null,
    int? Offset = null,
    int? IndexDirectoryOffset = null,
    int? ContainerNumberOffset = null,
    int? ScriptRoom = null);

public sealed record GameLibrary(
    string Title,
    string FolderPath,
    ScummVersion Version,
    string Platform,
    IReadOnlyList<GameResource> Resources,
    string DetectionNote,
    GameId Id = GameId.Unknown,
    bool IsDemo = false,
    string Edition = "")
{
    /// <summary>The SCUMM v4 archive (DISK01.LEC; LOOM CD); null for every other game.</summary>
    public ScummV4Archive? V4Archive { get; init; }

    /// <summary>The disks of a v4 game that is only browsed (rooms, costumes, sounds; not LOOM CD); null for every other game.</summary>
    public ScummV4Disks? V4Disks { get; init; }

    /// <summary>Which classic (v1) game this is (it picks the colour table of its rooms); null for every other game.</summary>
    public ScummV1Game? V1Game { get; init; }

    /// <summary>The v4 archive that holds the resource: the CD game's single archive, or the disk of a browsed v4 game; null for the old-v3 games.</summary>
    public ScummV4Archive? V4ArchiveFor(GameResource resource) => V4Archive ?? V4Disks?.ArchiveFor(resource);

    public IEnumerable<GameResource> Rooms =>Resources.Where(r => r.Kind == ResourceKind.Room);
}

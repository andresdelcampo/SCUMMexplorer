namespace SCUMMexplorer.Core.Domain;

public enum ScummVersion
{
    Unknown,
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
    bool IsDemo = false)
{
    /// <summary>The SCUMM v4 archive (DISK01.LEC; LOOM CD); null for every other game.</summary>
    public ScummV4Archive? V4Archive { get; init; }

    public IEnumerable<GameResource> Rooms => Resources.Where(r => r.Kind == ResourceKind.Room);
}

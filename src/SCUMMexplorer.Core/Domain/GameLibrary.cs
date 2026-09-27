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
    IndianaJones3
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
    int? ContainerNumberOffset = null);

public sealed record GameLibrary(
    string Title,
    string FolderPath,
    ScummVersion Version,
    string Platform,
    IReadOnlyList<GameResource> Resources,
    string DetectionNote,
    GameId Id = GameId.Unknown)
{
    public IEnumerable<GameResource> Rooms => Resources.Where(r => r.Kind == ResourceKind.Room);
}

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

/// <summary>
/// Which game the data is, whatever its format or edition, told by its bytes alone (<see cref="Discovery.GameIdentifier"/>); <see cref="Unknown"/> when no known
/// data matches. What the explorer can do with it is <see cref="GameSupport.FamilyOf"/>, which also weighs the format.
/// </summary>
public enum KnownGame
{
    Unknown,
    Loom,
    IndianaJones3,
    IndianaJones4,
    MonkeyIsland,
    MonkeyIsland2,
    ManiacMansion,
    ZakMcKracken
}

/// <summary>
/// The game-specific code path a library takes: LOOM EGA, LOOM CD and Indiana Jones 3 EGA have readers and sessions of their own; every other game,
/// named (<see cref="GameLibrary.Game"/>) or not, is <see cref="Unknown"/> and runs on the shared engine. Set by the scanner from the game and its format.
/// </summary>
public enum GameId
{
    Unknown,
    Loom,
    IndianaJones3,
    /// <summary>LOOM on CD (SCUMM v4, VGA): DISK01.LEC, read from a folder or a disc image.</summary>
    LoomVga
}

/// <summary>Which game-specific code path a game takes in a format: the one place that maps (game, SCUMM version) onto a <see cref="GameId"/>.</summary>
public static class GameIds
{
    public static GameId For(KnownGame game, ScummVersion version) => (game, version) switch
    {
        (KnownGame.Loom, ScummVersion.V4) => GameId.LoomVga,
        (KnownGame.Loom, _) => GameId.Loom,
        (KnownGame.IndianaJones3, ScummVersion.V3) => GameId.IndianaJones3,
        _ => GameId.Unknown
    };
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
    /// <summary>
    /// Which game the data is (told by its bytes); <see cref="KnownGame.Unknown"/> when no known data matches. A library made without the scanner takes the
    /// game of its <see cref="Id"/>.
    /// </summary>
    public KnownGame Game { get; init; } = Id switch
    {
        GameId.Loom or GameId.LoomVga => KnownGame.Loom,
        GameId.IndianaJones3 => KnownGame.IndianaJones3,
        _ => KnownGame.Unknown
    };

    /// <summary>The SCUMM v4 archive (DISK01.LEC; LOOM CD); null for every other game.</summary>
    public ScummV4Archive? V4Archive { get; init; }

    /// <summary>Room-local scripts of an index-driven game (old v1 to v3), read from its rooms; the other games list theirs among <see cref="Resources"/>.</summary>
    public IReadOnlyList<GameResource> RoomLocalScripts { get; init; } = [];

    /// <summary>Every script the tree offers: the indexed ones and the room-local ones.</summary>
    public IEnumerable<GameResource> AllScripts => Resources.Where(r => r.Kind == ResourceKind.Script).Concat(RoomLocalScripts);

    /// <summary>The disks of a v4 game that is only browsed (rooms, costumes, sounds; not LOOM CD); null for every other game.</summary>
    public ScummV4Disks? V4Disks { get; init; }

    /// <summary>The disc image of a game read from a .cue/.bin, when it carries CD audio tracks that are listed as sounds (see <see cref="CdTrackIdBase"/>); null for every other game.</summary>
    public DiscImage? AudioDisc { get; init; }

    /// <summary>The spoken lines of a talkie game (<c>MONSTER.SOU</c>), when the game has them; null for every other game.</summary>
    public ScummSpeechBank? Speech { get; init; }

    /// <summary>
    /// 19 October 2026: the languages the game's data holds, when it holds more than one (Monkey Island 1 CD: one folder per language on the disc); empty otherwise.
    /// Detected from the data only; <see cref="Language"/> says which one this library reads.
    /// </summary>
    public IReadOnlyList<GameLanguage> Languages { get; init; } = [];

    /// <summary>The key of the language this library reads (one of <see cref="Languages"/>), or null when the game holds one language.</summary>
    public string? Language { get; init; }

    /// <summary>What identifies this data set: the folder, and the language when the game holds several. Caches and play sessions are keyed by it.</summary>
    public string Key => Language is null ? FolderPath : FolderPath + "|" + Language;

    /// <summary>The resource number of CD audio track <c>n</c> is <c>CdTrackIdBase + n</c>.</summary>
    public const int CdTrackIdBase = 2000;

    /// <summary>The resource number of speech sample <c>n</c> (counted from 1 in file order) of a talkie game's <c>MONSTER.SOU</c> is <c>SpeechIdBase + n</c>; its <c>Offset</c> is the sample's place in the file.</summary>
    public const int SpeechIdBase = 10000;

    /// <summary>Which classic (v1) game this is (it picks the colour table of its rooms); null for every other game.</summary>
    public ScummV1Game? V1Game { get; init; }

    /// <summary>The v4 archive that holds the resource: the CD game's single archive, or the disk of a browsed v4 game; null for the old-v3 games.</summary>
    public IBrowsableArchive? V4ArchiveFor(GameResource resource) => V4Archive ?? V4Disks?.ArchiveFor(resource);

    public IEnumerable<GameResource> Rooms =>Resources.Where(r => r.Kind == ResourceKind.Room);
}

/// <summary>
/// One language of a game whose data holds several (19 October 2026): <see cref="Key"/> names the data set to read (the disc folder, for Monkey Island 1 CD
/// <c>SPANISH</c>) and <see cref="Label"/> is how the data names it, for the user ("Spanish").
/// </summary>
public sealed record GameLanguage(string Key, string Label);

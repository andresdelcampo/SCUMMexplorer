using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>Directory sizes that tell a game in one format (<see cref="DirectoryShape"/>); a size left null is not compared.</summary>
public sealed record KnownShape(ScummVersion Version, int? Rooms = null, int? Costumes = null, int? Scripts = null, int? Sounds = null)
{
    public bool Matches(DirectoryShape shape) =>
        shape.Version == Version && (Rooms is null || Rooms == shape.Rooms) && (Costumes is null || Costumes == shape.Costumes)
        && (Scripts is null || Scripts == shape.Scripts) && (Sounds is null || Sounds == shape.Sounds);
}

/// <summary>
/// What the explorer knows of one game in every edition: its published title, the mark a format adds to it ("LOOM (CD)"), the data files that every local
/// edition shares in one of a few versions (they tell the game in a language whose edition is not in <see cref="KnownEditions"/>), and the directory sizes that
/// tell it when no known file matches.
/// </summary>
public sealed record KnownGameInfo(
    KnownGame Game,
    string Title,
    IReadOnlyDictionary<ScummVersion, string> FormatMarks,
    IReadOnlyDictionary<string, string[]> SharedFiles,
    IReadOnlyList<KnownShape> Shapes);

/// <summary>The per-game facts identification uses besides the edition catalog (<see cref="KnownEditions"/>). One row per <see cref="KnownGame"/>.</summary>
public static class KnownGames
{
    /// <summary>How many of a game's shared files must match for the folder to be taken as that game.</summary>
    public const int RequiredSharedMatches = 3;

    private static readonly IReadOnlyDictionary<ScummVersion, string> NoMarks = new Dictionary<ScummVersion, string>();
    private static readonly IReadOnlyDictionary<string, string[]> NoFiles = new Dictionary<string, string[]>();

    // The enhanced v2 release of a classic v1 game.
    private static readonly IReadOnlyDictionary<ScummVersion, string> Enhanced = new Dictionary<ScummVersion, string> { [ScummVersion.V2] = "Enhanced" };

    public static IReadOnlyList<KnownGameInfo> All { get; } =
    [
        new(KnownGame.Loom, "LOOM", new Dictionary<ScummVersion, string> { [ScummVersion.V4] = "CD" },
            // Graphics-only room containers that every local EGA copy shares in one of two versions (the Roland upgrade changes them).
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["27.LFL"] = ["40ceb165a65f9e48cd988937d157018132df1a0e2527ebe9e8bbc95d6854e10c", "90c4cfed55a34e2468a4041be0e9764128e81f5722f05685bea44723c703483d"],
                ["29.LFL"] = ["56435fb8bcd6f6d909862b2021722ec4fa933fa6a3f81f07f598087e70d6ffe5", "c75d329dd10ef8eb73cd568e6f1e672a95b23a43e640957e05054e67891bdd70"],
                ["39.LFL"] = ["0f819189a499fa2ecc57660260979d050d16cea3c612d92d1b536eb2d266967b", "fbb2b62e0d4705b1a823098f324a6351531e04104a220ec455564405cdaaddbe"],
                ["52.LFL"] = ["1c647b63e117c7723514c3d30080ea514327226c9ee4181ab3e18853e19c3570", "36a2ef6222c2b83a068ba2bec1f35f08a5214ec1a3f8002c3d186af84e7b70ea"],
                ["61.LFL"] = ["60e0b65db5f3f6ccb9d4e62386c86338242722f92fea3acc4abf7763dc17a6d2", "a15d3e4c959d621249a1803900ea4664966a307fe9dff424ed5eb014e8d4b926"],
                ["81.LFL"] = ["3a41b766cd2219b1df1af9cfbb2ddb8234625a52227cb5578394385ec1f98c4b", "fabdc2337e8db54426c36d05865ed8eeed17338f5a60835e5be5c2136e82e2ef"],
            },
            // Every local LOOM EGA edition lists 176 costumes and 117 scripts in its old-v3 index (checked 10 October 2026); the demos differ. Both local dumps of
            // the CD (the Kixx release included) list 78 rooms, 165 costumes, 637 scripts and 30 sounds.
            [new(ScummVersion.V3, Costumes: 176, Scripts: 117), new(ScummVersion.V4, 78, 165, 637, 30)]),
        new(KnownGame.IndianaJones3, "Indiana Jones and the Last Crusade", NoMarks,
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["42.LFL"] = ["242ac5f6757167331f313958b0c7d93c9e3327dd5914265dd5aed9aaaba08e49"],
                ["43.LFL"] = ["3f1251b4e4d6806b417d19acf9ec74f8e948f29e8f5aad7f1965faf2b81787f1"],
                ["44.LFL"] = ["eca90bf12b0cbfec4dd1e77e69966a4c2670b8c1015bb4a44710cdaa7395288c"],
                ["64.LFL"] = ["45e01a53c3056237f7b70d02c57208c2ca6d797bfb5dfbbc1ec34ac996f5a64d"],
                ["67.LFL"] = ["a37d7504548a5b4f2093284f923aea8322a633e09b97f6e98946aac2787ca780"],
            },
            // Both local EGA editions list 100 costumes and 130 scripts; the demo differs.
            [new(ScummVersion.V3, Costumes: 100, Scripts: 130)]),
        new(KnownGame.IndianaJones4, "Indiana Jones and the Fate of Atlantis", NoMarks, NoFiles, []),
        new(KnownGame.MonkeyIsland, "The Secret of Monkey Island", NoMarks, NoFiles, []),
        new(KnownGame.MonkeyIsland2, "Monkey Island 2: LeChuck's Revenge", NoMarks, NoFiles, []),
        // The classic v1 releases are told by their index layout (ScummV1Index: the directory sizes are fixed per game and not stored).
        new(KnownGame.ManiacMansion, "Maniac Mansion", Enhanced, NoFiles, []),
        new(KnownGame.ZakMcKracken, "Zak McKracken and the Alien Mindbenders", Enhanced, NoFiles, []),
    ];

    public static KnownGameInfo Info(KnownGame game) => All.First(info => info.Game == game);

    /// <summary>The title a library of <paramref name="game"/> is shown under: the published title, with the format's mark or "demo" (<c>LOOM (CD)</c>, <c>LOOM (demo)</c>).</summary>
    public static string TitleOf(KnownGame game, ScummVersion version, bool demo)
    {
        var info = Info(game);
        var mark = demo ? "demo" : info.FormatMarks.GetValueOrDefault(version);
        return mark is null ? info.Title : $"{info.Title} ({mark})";
    }
}

using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>Directory sizes that tell a game in one format (<see cref="DirectoryShape"/>); a size left null is not compared.</summary>
public sealed record KnownShape(ScummVersion Version, int? Rooms = null, int? Costumes = null, int? Scripts = null, int? Sounds = null)
{
    public bool Matches(DirectoryShape shape) =>
        shape.Version == Version && (Rooms is null || Rooms == shape.Rooms) && (Costumes is null || Costumes == shape.Costumes)
        && (Scripts is null || Scripts == shape.Scripts) && (Sounds is null || Sounds == shape.Sounds);
}

/// <summary>What a game's known editions share, made from them (<c>known-editions.json</c>): the data files that are one of a few versions in every edition, and the directory sizes that every edition of a format has.</summary>
internal sealed record KnownGameData(KnownGame Game, IReadOnlyDictionary<string, string[]> SharedFiles, IReadOnlyList<KnownShape> Shapes);

/// <summary>
/// What the explorer knows of one game in every edition: its published title, the mark a format adds to it ("LOOM (CD)"), and, made from the catalog of its
/// editions (<see cref="KnownEditions"/>) the same for every game, what tells it in an edition the catalog does not list (for example another language): the data
/// files its editions share in one of a few versions, then the directory sizes.
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
    /// <summary>How many of a game's shared files must match for the folder to be taken as that game (all of them when the game has fewer).</summary>
    public const int RequiredSharedMatches = 3;

    private static readonly IReadOnlyDictionary<ScummVersion, string> NoMarks = new Dictionary<ScummVersion, string>();

    // The enhanced v2 release of a classic v1 game.
    private static readonly IReadOnlyDictionary<ScummVersion, string> Enhanced = new Dictionary<ScummVersion, string> { [ScummVersion.V2] = "Enhanced" };

    private static readonly (KnownGame Game, string Title, IReadOnlyDictionary<ScummVersion, string> Marks)[] Titles =
    [
        (KnownGame.Loom, "LOOM", new Dictionary<ScummVersion, string> { [ScummVersion.V4] = "CD" }),
        (KnownGame.IndianaJones3, "Indiana Jones and the Last Crusade", NoMarks),
        (KnownGame.IndianaJones4, "Indiana Jones and the Fate of Atlantis", NoMarks),
        (KnownGame.MonkeyIsland, "The Secret of Monkey Island", NoMarks),
        (KnownGame.MonkeyIsland2, "Monkey Island 2: LeChuck's Revenge", NoMarks),
        (KnownGame.ManiacMansion, "Maniac Mansion", Enhanced),
        (KnownGame.ZakMcKracken, "Zak McKracken and the Alien Mindbenders", Enhanced),
    ];

    public static IReadOnlyList<KnownGameInfo> All { get; } = Titles.Select(t =>
    {
        var data = KnownEditions.Games.FirstOrDefault(g => g.Game == t.Game);
        return new KnownGameInfo(t.Game, t.Title, t.Marks, data?.SharedFiles ?? new Dictionary<string, string[]>(), data?.Shapes ?? []);
    }).ToList();

    public static KnownGameInfo Info(KnownGame game) => All.First(info => info.Game == game);

    /// <summary>The title a library of <paramref name="game"/> is shown under: the published title, with the format's mark or "demo" (<c>LOOM (CD)</c>, <c>LOOM (demo)</c>).</summary>
    public static string TitleOf(KnownGame game, ScummVersion version, bool demo)
    {
        var info = Info(game);
        var mark = demo ? "demo" : info.FormatMarks.GetValueOrDefault(version);
        return mark is null ? info.Title : $"{info.Title} ({mark})";
    }
}

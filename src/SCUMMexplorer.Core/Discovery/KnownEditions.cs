using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// An edition told by the bytes of its data files: the game, a label ("English, Roland upgrade"), whether it is a demo, how many data files it has and the
/// SHA-256 of up to 14 of them.
/// </summary>
public sealed record KnownEdition(KnownGame Game, string Label, bool Demo, int DataFiles, IReadOnlyDictionary<string, string> Files);

/// <summary>
/// The catalog of known editions of every game, in every format: <c>known-editions.json</c>, made by <c>.verification/known-editions/Make</c> from the local
/// copies (see <see cref="GameFingerprint"/> for which files count). Per edition it keeps the hashes of the files that tell it from the other editions of
/// its game. A game is taken as the edition whose hashes it matches best: at least 60 percent of them and three files (all of them when the edition lists
/// fewer, as a v5 game's one index does), so an installer-rewritten or missing file does not matter; when two match equally well, the one whose file count
/// is closest wins (the Roland upgrade of LOOM adds four sound files).
/// </summary>
public static class KnownEditions
{
    private const double RequiredRatio = 0.6;
    private const int RequiredMatches = 3;

    private static readonly Lazy<IReadOnlyList<KnownEdition>> Editions = new(Load);

    public static IReadOnlyList<KnownEdition> All => Editions.Value;

    private static IReadOnlyList<KnownEdition> Load()
    {
        using var stream = typeof(KnownEditions).Assembly.GetManifestResourceStream("SCUMMexplorer.Core.known-editions.json");
        if (stream is null) return [];
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return rows.Select(r => new KnownEdition(Enum.Parse<KnownGame>(r.Game), r.Label, r.Demo, r.DataFiles,
            new Dictionary<string, string>(r.Files, StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private sealed record Row(string Game, string Label, bool Demo, int DataFiles, Dictionary<string, string> Files);

    /// <summary>The edition whose hashes the data files match best, or null.</summary>
    public static KnownEdition? Identify(GameFingerprint data)
    {
        KnownEdition? best = null;
        var bestKey = (Ratio: 0.0, Matches: 0, Closeness: int.MinValue);
        foreach (var edition in All)
        {
            var matches = edition.Files.Count(f => string.Equals(data.Hash(f.Key), f.Value, StringComparison.OrdinalIgnoreCase));
            var ratio = (double)matches / edition.Files.Count;
            if (matches < Math.Min(RequiredMatches, edition.Files.Count) || ratio < RequiredRatio) continue;
            var key = (ratio, matches, -Math.Abs(edition.DataFiles - data.FileCount));
            if (key.CompareTo(bestKey) > 0) { best = edition; bestKey = key; }
        }
        return best;
    }
}

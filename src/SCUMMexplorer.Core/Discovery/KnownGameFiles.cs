using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// An edition told by the bytes of its files: the game, a label ("English, Roland upgrade"), whether it is a demo, and SHA-256 hashes of up to 14 of its LFL files.
/// </summary>
public sealed record KnownEdition(GameId Game, string Label, bool Demo, int LflFiles, IReadOnlyDictionary<string, string> Files);

/// <summary>
/// Known game data, told by the bytes of specific LFL files (not by names or sizes). <c>known-editions.json</c> is made by <c>.verification/known-editions/make.py</c> from the
/// local copies (LOOM EGA in eight editions and two demos, Indiana Jones and the Last Crusade EGA in two editions and a demo): per edition, the hashes of the files that tell it
/// from the others. A folder is taken as the edition whose hashes it matches best (at least 60 percent and 3 files, so an installer-rewritten or missing file does not matter);
/// when two match equally well, the one whose file count is closest wins (the Roland upgrade adds four sound files). Failing that, six graphics-only LOOM files and five
/// Indy3 files recognise the game in any language; <see cref="GameFolderScanner"/> has the weaker fallbacks after this.
/// </summary>
public static class KnownGameFiles
{
    /// <summary>How many of a game's language-free files must match for the folder to be taken as that game.</summary>
    private const int RequiredMatches = 3;

    private static readonly Lazy<IReadOnlyList<KnownEdition>> Editions = new(LoadEditions);

    // Graphics-only room containers that every local copy of the game shares in one of two versions (the Roland upgrade changes the LOOM ones).
    private static readonly Dictionary<GameId, Dictionary<string, string[]>> Games = new()
    {
        [GameId.Loom] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["27.LFL"] = ["40ceb165a65f9e48cd988937d157018132df1a0e2527ebe9e8bbc95d6854e10c", "90c4cfed55a34e2468a4041be0e9764128e81f5722f05685bea44723c703483d"],
            ["29.LFL"] = ["56435fb8bcd6f6d909862b2021722ec4fa933fa6a3f81f07f598087e70d6ffe5", "c75d329dd10ef8eb73cd568e6f1e672a95b23a43e640957e05054e67891bdd70"],
            ["39.LFL"] = ["0f819189a499fa2ecc57660260979d050d16cea3c612d92d1b536eb2d266967b", "fbb2b62e0d4705b1a823098f324a6351531e04104a220ec455564405cdaaddbe"],
            ["52.LFL"] = ["1c647b63e117c7723514c3d30080ea514327226c9ee4181ab3e18853e19c3570", "36a2ef6222c2b83a068ba2bec1f35f08a5214ec1a3f8002c3d186af84e7b70ea"],
            ["61.LFL"] = ["60e0b65db5f3f6ccb9d4e62386c86338242722f92fea3acc4abf7763dc17a6d2", "a15d3e4c959d621249a1803900ea4664966a307fe9dff424ed5eb014e8d4b926"],
            ["81.LFL"] = ["3a41b766cd2219b1df1af9cfbb2ddb8234625a52227cb5578394385ec1f98c4b", "fabdc2337e8db54426c36d05865ed8eeed17338f5a60835e5be5c2136e82e2ef"],
        },
        [GameId.IndianaJones3] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["42.LFL"] = ["242ac5f6757167331f313958b0c7d93c9e3327dd5914265dd5aed9aaaba08e49"],
            ["43.LFL"] = ["3f1251b4e4d6806b417d19acf9ec74f8e948f29e8f5aad7f1965faf2b81787f1"],
            ["44.LFL"] = ["eca90bf12b0cbfec4dd1e77e69966a4c2670b8c1015bb4a44710cdaa7395288c"],
            ["64.LFL"] = ["45e01a53c3056237f7b70d02c57208c2ca6d797bfb5dfbbc1ec34ac996f5a64d"],
            ["67.LFL"] = ["a37d7504548a5b4f2093284f923aea8322a633e09b97f6e98946aac2787ca780"],
        },
    };

    private static IReadOnlyList<KnownEdition> LoadEditions()
    {
        using var stream = typeof(KnownGameFiles).Assembly.GetManifestResourceStream("SCUMMexplorer.Core.known-editions.json");
        if (stream is null) return [];
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return rows.Select(r => new KnownEdition(Enum.Parse<GameId>(r.Game), r.Label, r.Demo, r.LflFiles, r.Files)).ToList();
    }

    private sealed record Row(string Game, string Label, bool Demo, int LflFiles, Dictionary<string, string> Files);

    /// <summary>The edition whose hashes the folder's LFL files match best, or null.</summary>
    public static KnownEdition? IdentifyEdition(IReadOnlyDictionary<string, string> lflPathsByFileName)
    {
        var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        string? Hash(string name)
        {
            if (cache.TryGetValue(name, out var known)) return known;
            string? hash = null;
            if (lflPathsByFileName.TryGetValue(name, out var path))
                try { hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))); } catch (IOException) { }
            return cache[name] = hash;
        }
        KnownEdition? best = null; var bestKey = (Ratio: 0.0, Matches: 0, Closeness: int.MinValue);
        foreach (var edition in Editions.Value)
        {
            var matches = edition.Files.Count(f => string.Equals(Hash(f.Key), f.Value, StringComparison.OrdinalIgnoreCase));
            var ratio = (double)matches / edition.Files.Count;
            if (matches < 3 || ratio < 0.6) continue;
            var key = (ratio, matches, -Math.Abs(edition.LflFiles - lflPathsByFileName.Count));
            if (key.CompareTo(bestKey) > 0) { best = edition; bestKey = key; }
        }
        return best;
    }

    /// <summary>The game whose language-free files match, or null (used when no edition matched well enough, for example another language).</summary>
    public static GameId? IdentifyGame(IReadOnlyDictionary<string, string> lflPathsByFileName)
    {
        foreach (var (game, files) in Games)
        {
            var matches = 0;
            foreach (var (name, hashes) in files)
            {
                if (!lflPathsByFileName.TryGetValue(name, out var path)) continue;
                try
                {
                    if (hashes.Contains(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.OrdinalIgnoreCase)) matches++;
                }
                catch (IOException) { }
            }
            if (matches >= RequiredMatches) return game;
        }
        return null;
    }
}

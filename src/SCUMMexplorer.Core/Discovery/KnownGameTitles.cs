namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// The published names of the SCUMM games the explorer can recognise by their resource-file stem or executable, so a game opens under its real title and not under the
/// name of its folder or file (MONKEY, MONKEY2). A game that is not recognised keeps the folder name.
/// </summary>
public static class KnownGameTitles
{
    public const string MonkeyIsland = "The Secret of Monkey Island";
    public const string MonkeyIsland2 = "Monkey Island 2: LeChuck's Revenge";
    public const string FateOfAtlantis = "Indiana Jones and the Fate of Atlantis";
    public const string Indy3 = "Indiana Jones and the Last Crusade";
    public const string ManiacMansion = "Maniac Mansion";
    public const string ZakMcKracken = "Zak McKracken and the Alien Mindbenders";

    /// <summary>The title of a v5 game by its file stem (<c>MONKEY2</c> of <c>MONKEY2.000</c>, with or without a folder), or null.</summary>
    public static string? ForV5Stem(string stem) => Path.GetFileName(stem).ToUpperInvariant() switch
    {
        "MONKEY" => MonkeyIsland,
        "MONKEY2" => MonkeyIsland2,
        "ATLANTIS" => FateOfAtlantis,
        _ => null
    };

    /// <summary>The title of a game by the names of the executables in its folder, or null. The first executable that is known decides.</summary>
    public static string? ForExecutables(IEnumerable<string> fileNames)
    {
        foreach (var name in fileNames)
        {
            var title = Path.GetFileName(name).ToUpperInvariant() switch
            {
                "MONKEY.EXE" => MonkeyIsland,
                "MONKEY2.EXE" => MonkeyIsland2,
                "ATLANTIS.EXE" => FateOfAtlantis,
                "INDY3.EXE" or "INDY256.EXE" or "INDY.EXE" => Indy3,
                "MANIAC.EXE" => ManiacMansion,
                "ZAK.EXE" => ZakMcKracken,
                _ => null
            };
            if (title is not null) return title;
        }
        return null;
    }
}

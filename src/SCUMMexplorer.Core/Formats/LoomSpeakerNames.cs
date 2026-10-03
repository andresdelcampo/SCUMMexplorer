using System.Globalization;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// What to call a speaker in the text views. The scripts print lines for an actor number; the names below come from the
/// lines themselves (what the others call them, who they are in the story), not from any table in the game data.
/// An actor whose identity the lines do not settle keeps its number, with the role when the lines show it.
/// </summary>
public static class LoomSpeakerNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "Bobbin",
        [2] = "Elder (actor 2)",    // the Guild's council: "Basta, Lachesis." / "Gran Consejo"
        [3] = "Elder (actor 3)",
        [4] = "Elder (actor 4)",
        [5] = "Hetchel",            // the accused before the council: "Mi destino está en vuestras manos, Ancianos."
        [6] = "Glassmaker",         // "No es cristal, Tejedor Threadbare": the glassmaker's shop
        [254] = "Game menu",
        [255] = "Game menu",
    };

    /// <summary>The display name for a speaker as the extractor labels it ("Actor 5", "Ego"); anything else is returned unchanged.</summary>
    public static string Display(string? speaker)
    {
        if (speaker is null) return "Unknown speaker";
        if (speaker == "Ego") return "Bobbin";
        if (speaker.StartsWith("Actor ", StringComparison.Ordinal) &&
            int.TryParse(speaker.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && Names.TryGetValue(number, out var name))
            return name;
        return speaker;
    }
}

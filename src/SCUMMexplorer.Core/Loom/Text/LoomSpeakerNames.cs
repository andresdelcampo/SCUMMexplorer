using System.Globalization;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// What to call a speaker in the text views. The scripts print lines for an actor number; the names below come from the
/// lines themselves (what the others call them, who they are in the story), not from any table in the game data.
/// An actor whose identity the lines do not settle keeps its number, with the role when the lines show it.
/// SCUMM v3/v4 data hold no actor names, so there is nothing to read them from. Checked against the English Roland edition on 10 October
/// (<c>.verification/cutscene-scan-ega speakers</c>): the scoped names (Mandible, Cob, Rusty, The Dead One, Mr. Stoke, Hetchel in Room 9) are
/// confirmed by the English lines; actors 2 to 5 and 255 elsewhere are reused for many characters, so their generic labels are a role at best.
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

    /// <summary>
    /// The display name for a line: <see cref="Display(string?)"/>, except where the scene settles who an actor number is
    /// (numbers are reused from scene to scene). Names come from the lines of the scene in both editions, as in the glossary.
    /// </summary>
    public static string Display(LoomTextEntry entry)
    {
        if (entry.Speaker is { } speaker && speaker.StartsWith("Actor ", StringComparison.Ordinal) &&
            int.TryParse(speaker.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            var scoped = (number, entry.ScriptId, entry.RoomId) switch
            {
                (2, 91 or 93, _) => "Mandible",
                (3, 91 or 93, _) => "Cob",
                (3, 98, _) => "Rusty",
                (4, 105, _) or (4, _, 9) => "The Dead One",
                (5, 86 or 87, _) => "Mr. Stoke",
                _ => null
            };
            if (scoped is not null) return scoped;
        }
        return Display(entry.Speaker);
    }

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

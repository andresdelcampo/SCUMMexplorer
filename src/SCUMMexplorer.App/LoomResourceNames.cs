using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

/// <summary>A resource's own label with the glossary's name after it ("Script 91 · cage scene (Room 43)"), where the glossary has evidence for one.</summary>
internal static class LoomResourceNames
{
    public static GlossaryEntry? Entry(LoomGlossary glossary, GameResource resource) => resource.ResourceId is not int id ? null : resource.Kind switch
    {
        ResourceKind.Script => glossary.Script(id),
        ResourceKind.Sound => null, // sounds stay plain "Sound NN": a name or a music/effect guess was not reliable enough (user, 4 October)
        ResourceKind.Room => glossary.Room(id),
        _ => null
    };

    public static string Label(LoomGlossary glossary, GameResource resource) =>
        Entry(glossary, resource) is { } entry ? $"{resource.Name} · {entry.Name}" : resource.Name;

    /// <summary>The tooltip that says how well the name is supported.</summary>
    public static string? Evidence(LoomGlossary glossary, GameResource resource) =>
        Entry(glossary, resource) is { } entry
            ? $"{entry.Name} ({entry.EvidenceLabel})" + (entry.Source.Length > 0 ? $": {entry.Source}" : "")
            : null;

    /// <summary>Adds the glossary name after each "script 028" in a line of the explorer's own text (the line itself stays the evidence in the tooltip).</summary>
    /// <summary>Adds the name after each "bit[0xD9D]" the glossary (or the catalog scene that sets it) names.</summary>
    public static string NameBits(LoomGlossary glossary, string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"bit\[0x([0-9A-Fa-f]{1,4})\](?! \()", match =>
            glossary.Bit(int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber)) is { } entry
                ? $"{match.Value} ({entry.Name})" : match.Value);

    public static string NameScripts(LoomGlossary glossary, string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\b([Ss]cript )(\d{1,3})\b(?! \()", match =>
            glossary.Script(int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) is { } entry
                ? $"{match.Value} ({entry.Name})" : match.Value);
}

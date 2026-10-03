using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

/// <summary>A resource's own label with the glossary's name after it ("Script 91 · cage scene (Room 43)"), where the glossary has evidence for one.</summary>
internal static class LoomResourceNames
{
    public static GlossaryEntry? Entry(LoomGlossary glossary, GameResource resource) => resource.ResourceId is not int id ? null : resource.Kind switch
    {
        ResourceKind.Script => glossary.Script(id),
        ResourceKind.Sound => glossary.Sound(id),
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
    public static string NameScripts(LoomGlossary glossary, string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"([Ss]cript )(\d{1,3})(?! \()", match =>
            glossary.Script(int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) is { } entry
                ? $"{match.Value} ({entry.Name})" : match.Value);
}

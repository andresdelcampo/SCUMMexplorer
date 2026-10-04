namespace SCUMMexplorer.App;

/// <summary>
/// What an object's verb number means, from the decoded verb roles (docs/loom/LOOM_SCRIPT_MAP.md "Object verbs"): 0x36 look (script 006 runs it after a
/// double click), 0x38 use or go (script 007, after a click), 0x37 what happens first on a click, 0x39 the close-up shown in the verb area,
/// 0x3A-0x55 the sixteen drafts (forward and reverse), 0x56 any other draft, which does nothing here.
/// </summary>
internal static class LoomVerbNames
{
    public static string Label(int verb) => verb switch
    {
        0x36 => "Look at",
        0x37 => "On click (before the walk)",
        0x38 => "Use / go",
        0x39 => "Close-up",
        0x56 => "Any other draft",
        _ => LoomDraftNames.ForVerb(verb) is { } draft ? "Draft: " + draft.Name : $"Verb 0x{verb:X2}"
    };

    /// <summary>The label of a known verb, or null for a verb outside the known ranges (which would show as "Verb 0x..").</summary>
    public static string? LabelOrNull(int verb) => Label(verb) is { } label && !label.StartsWith("Verb 0x", StringComparison.Ordinal) ? label : null;

    /// <summary>Display order: look, use, on click, close-up, then the drafts in the game's order, the catch-all draft last.</summary>
    public static int Order(int verb) => verb switch { 0x36 => 0, 0x38 => 1, 0x37 => 2, 0x39 => 3, 0x56 => 1000, _ => 100 + verb };
}

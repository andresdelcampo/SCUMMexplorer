using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

internal static class LoomDraftNames
{
    // Shared display associations already used by Game Status. They do not
    // supply spell prerequisites or change the source verb selected by a replay.
    private static readonly (int Forward, int Reverse, string Title)[] Names =
    [
        (0x3A, 0x3B, "Opening / Closing"),
        (0x3C, 0x3D, "Straw into Gold"),
        (0x3E, 0x3F, "Dyeing / Bleaching"),
        (0x40, 0, "Night Vision"),
        (0x41, 0x42, "Twisting"),
        (0x43, 0x44, "Invisibility"),
        (0x45, 0x46, "Emptying"),
        (0x47, 0x48, "Sleep"),
        (0x49, 0, "Terror"),
        (0x4A, 0x4B, "Sharpening"),
        (0x4C, 0, "Reflection"),   // swapped 3 Oct after play: the scene run by verb 0x4D heals the shepherds (Room 34 object 586)
        (0x4D, 0, "Healing"),
        (0x4E, 0x4F, "Silence"),
        (0x50, 0x51, "Shaping"),
        (0x52, 0x53, "Unmaking"),
        (0x54, 0, "Transcend")
    ];

    public static string Title(LoomDraftStatus draft) =>
        draft.Slot >= 0 && draft.Slot < Names.Length &&
        Names[draft.Slot].Forward == draft.ForwardVerb && Names[draft.Slot].Reverse == draft.ReverseVerb
            ? Names[draft.Slot].Title : $"Draft {draft.Slot + 1}";

    /// <summary>The draft a verb number casts and whether it is the reverse (0x3A "Opening", 0x3B "Closing"...), or null for a verb that is not a draft.</summary>
    public static (string Name, bool Reverse)? ForVerb(int verb)
    {
        foreach (var name in Names)
        {
            if (verb != name.Forward && verb != name.Reverse) continue;
            var reverse = verb == name.Reverse;
            var parts = name.Title.Split(" / ", StringSplitOptions.None);
            return (parts.Length == 2 ? parts[reverse ? 1 : 0] : name.Title + (reverse ? " (reversed)" : ""), reverse);
        }
        return null;
    }

    public static string ReplayTitle(string title, int verb)
    {
        if (verb == 0) return title;
        foreach (var name in Names)
        {
            if (verb != name.Forward && verb != name.Reverse) continue;
            bool reverse = verb == name.Reverse;
            var parts = name.Title.Split(" / ", StringSplitOptions.None);
            var label = parts.Length == 2 ? parts[reverse ? 1 : 0] : name.Title + (reverse ? " (reverse)" : "");
            return title.Replace($"Draft response {verb - 0x39} at ", $"{label} at ", StringComparison.Ordinal);
        }
        return title;
    }
}

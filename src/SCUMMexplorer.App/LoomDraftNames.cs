using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

internal static class LoomDraftNames
{
    // Shared display associations already used by Game Status. They do not supply spell prerequisites or change the
    // source verb selected by a replay. The slot order is the game's draft table; the names were checked on 3 October
    // against what each verb does to the objects that answer it (docs/LOOM_SCRIPT_MAP.md, "Draft verb names"). Evidence per entry:
    private static readonly (int Forward, int Reverse, string Title)[] Names =
    [
        // Room 02 "cielo" 0x3A starts script 049, 0x3B answers "Parece que se ha cerrado."; "agujero", "puerta", "jaula" open and close.
        (0x3A, 0x3B, "Opening / Closing"),
        // Room 38 "paja" 0x3C: "Ya se ha tejido toda la paja", 0x3D: "Ya se ha tejido todo el oro"; Room 28 "oro" and "paja" swap scripts.
        (0x3C, 0x3D, "Straw into Gold"),
        // Room 24 "ovejas" 0x3F: "No es posible que sean mas blancas" (so 0x3F whitens and 0x3E dyes); Room 06 wool, Room 08 tapestries.
        (0x3E, 0x3F, "Dyeing / Bleaching"),
        // Room 63 and 65 "oscuridad" (darkness); Room 02 "cielo": "Ya le he sacado suficiente luz".
        (0x40, 0, "Night Vision"),
        // Room 33 "escalones" 0x41: "Ya parecen suficientemente enredados", 0x42: "Dudo que pueda desenredarlos mas"; Room 13 "tifon".
        (0x41, 0x42, "Twisting"),
        // Swapped with Invisibility on 3 October: Room 34 "chico" 0x43: "Ya esta totalmente dormido"; Rusty 0x43: "Esta durmiendo. Para siempre.", 0x44: "No esta dormido. Esta muerto!"; Room 28 dragon 0x43/0x44, and Terror (0x49) on it needs it awake.
        (0x43, 0x44, "Sleep"),
        // Room 22 "caliz" 0x45: "Ya esta vacio", 0x46: "Esta lleno hasta arriba"; Room 31 "estanque" 0x46: "lo suficientemente lleno".
        (0x45, 0x46, "Emptying"),
        // Swapped with Sleep: Room 24 "durmiente" (a sleeper) answers 0x47-0x49 with "Si duerme, no serviria de nada"; Room 16 and 20 "obreros" 0x47/0x48 start the same script with 1 / 0 (on and off); sheep: "Supongo que no querran ni mirarme".
        (0x47, 0x48, "Invisibility"),
        // Room 24 "ovejas" 0x49: "Son demasiado listas para asustarse tan facilmente"; Room 23 "pastores" and Room 28 dragons start fear scripts.
        (0x49, 0, "Terror"),
        // Room 41 "espada" (sword): 0x4A / 0x4B (the only objects with them). Reading: no text says which direction is which.
        (0x4A, 0x4B, "Sharpening"),
        // Room 34 Rusty 0x4C: "Yech. Ahora no quiero parecerme a el."; "chico" 0x4C starts local 202, the only writer of the ego costume 0x67 (a disguise). Swapped with Healing on 3 October.
        (0x4C, 0, "Reflection"),
        // Room 26 "pastores" 0x4D starts script 202 (the shepherds healed, seen in play); Room 09 Hetchel: "Eso no ayudo. Debe estar bajo la influencia de algun hechizo."
        (0x4D, 0, "Healing"),
        // No object answers 0x4E; Room 09 Hetchel answers 0x4F. Reading only: the manual order.
        (0x4E, 0x4F, "Silence"),
        // Only Room 09 Hetchel answers 0x51 (0x50 has no object). Reading only: the manual order.
        (0x50, 0x51, "Shaping"),
        // Room 09 "telar" (the loom) 0x52 starts script 104.
        (0x52, 0x53, "Unmaking"),
        // Room 66 object 833 "Bobbin" 0x54 starts script 106 (the ending).
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

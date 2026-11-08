namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The titles of the Play Cutscene lists, in one place for every game (REQUIREMENTS §8.2): "Room 04 · Look at leaf", "Room 73 · Push light switch".
/// The verb's word comes from the game's <see cref="ScummCutsceneVerbs"/> (LOOM's draft names, a verb-based game's own verb names). The verb byte
/// is not part of the title (the App adds it in Advanced mode). Moved from <c>LoomCutsceneTitles</c> on 17 October 2026 (S7).
/// </summary>
public static class ScummCutsceneTitles
{
    /// <summary>The room prefix; a title that already has it is left alone, and the "Use response ·" lead of a room-change title is dropped.</summary>
    public static string Format(int room, string description)
    {
        description = System.Text.RegularExpressions.Regex.Replace(description,
            @"(?:Use|Look|Walk) response · (?=Reached Rooms? )", "");
        var prefix = $"Room {room:00} · ";
        return description.StartsWith(prefix, StringComparison.Ordinal) ? description : prefix + description;
    }

    /// <summary>
    /// The title as Normal mode shows it for a verb-based game (R-6.36): an unnamed object reads `Unnamed object` and a verb the game never names reads `Action`,
    /// so no title carries an id or a verb byte (Advanced adds them elsewhere). An unknown native word never becomes a made-up action.
    /// </summary>
    public static string Plain(ScummCutscene trigger) => Title(trigger with
    {
        ObjectName = trigger.ObjectName.Length > 0 ? trigger.ObjectName : "Unnamed object",
        Verbs = trigger.Roles with { Word = id => trigger.Roles.Word?.Invoke(id) is { Length: > 0 } word ? word : "Action" }
    });

    /// <summary>
    /// The title of a trigger found by the script scan: "Reached Room 03" when the verb leads to other rooms, otherwise the verb and the
    /// object ("Use sheep", "Opening at sky"). An object without a name reads as the LOOM EGA titles do: "Use response".
    /// </summary>
    public static string Title(ScummCutscene trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var roles = trigger.Roles;
        // Rooms the verb leads to by the doorway finder. An unnamed object's plain verb, which the LOOM EGA catalog titles by the rooms a run reached,
        // falls back to the rooms its span names in literal loads (20 of the catalog's 24 title differences with the scan were such a load the
        // doorway finder does not follow). Named objects and other verbs keep their names, as the catalog does.
        var plain = roles.IsPlain(trigger.VerbId);
        var reached = trigger.LeadsTo.Count > 0 ? trigger.LeadsTo
            : !trigger.IsRoomEntry && plain && trigger.ObjectName.Length == 0 ? (trigger.LoadTargets ?? []).Where(r => r != trigger.Room).ToArray()
            : [];
        if (reached.Count > 0)
            return Format(trigger.Room, "Reached " + (reached.Count == 1 ? "Room " : "Rooms ") +
                string.Join(", ", reached.Select(r => r.ToString("00"))));
        if (trigger.IsDepartureScript) return Format(trigger.Room, "Leaving");
        if (trigger.IsRoomEntry) return Format(trigger.Room, "Entry");
        var word = roles.WordFor(trigger.VerbId);
        if (trigger.ObjectName.Length == 0 && plain) return Format(trigger.Room, word.Split(' ')[0] + " response");
        var target = trigger.ObjectName.Length > 0 ? trigger.ObjectName : "unnamed object"; // R-6.36: Normal titles carry no raw ids (the id is in the note)
        return Format(trigger.Room, $"{word} {(roles.AimsAt?.Invoke(trigger.VerbId) == true ? "at " : "")}{target}");
    }
}

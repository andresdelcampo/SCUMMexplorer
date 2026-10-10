namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// The titles of the Play Cutscene lists, in one place for both editions: "Room 04 · Look at leaf". The verb byte is not part of
/// the title (the App adds it in Advanced mode).
/// </summary>
public static class LoomCutsceneTitles
{
    /// <summary>The room prefix; a title that already has it is left alone, and the "Use response ·" lead of a room-change title is dropped.</summary>
    public static string Format(int room, string description)
    {
        description = System.Text.RegularExpressions.Regex.Replace(description,
            @"(?:Use|Look|Walk) response · (?=Reached Rooms? )", "");
        var prefix = $"Room {room:00} · ";
        return description.StartsWith(prefix, StringComparison.Ordinal) ? description : prefix + description;
    }

    /// <summary>The verb as a word ("Look at", "Walk to", "Use", a draft's name, or "Other draft response" for a verb the table does not name).</summary>
    public static string VerbWord(int verbId) => verbId switch
    {
        0x36 => "Look at",
        0x37 => "Walk to",
        0x38 => "Use",
        var v => LoomDraftNames.ForVerb(v)?.Name ?? "Other draft response"
    };

    /// <summary>The rooms a verb's code leads to, in the form <see cref="LoomCutsceneIndex.Scan"/> takes (the doorway finder of the room hotspots).</summary>
    public static Func<GameResource, ScummScriptDisassembly, IReadOnlyList<int>> Doorways(GameLibrary game)
    {
        var finder = new LoomDoorwayFinder(game);
        return (room, code) => finder.Find(room, code).Select(t => t.RoomId).Distinct().ToArray();
    }

    /// <summary>
    /// The title of a trigger found by the script scan: "Reached Room 03" when the verb leads to other rooms, otherwise the verb and the
    /// object ("Use sheep", "Opening at sky"). An object without a name reads as the EGA titles do: "Use response".
    /// </summary>
    public static string Title(LoomCutscene trigger)
    {
        if (trigger.LeadsTo.Count > 0)
            return Format(trigger.Room, "Reached " + (trigger.LeadsTo.Count == 1 ? "Room " : "Rooms ") +
                string.Join(", ", trigger.LeadsTo.Select(r => r.ToString("00"))));
        if (trigger.IsRoomEntry) return Format(trigger.Room, "Entry");
        var plain = trigger.VerbId is 0x36 or 0x37 or 0x38;
        if (trigger.ObjectName.Length == 0 && plain) return Format(trigger.Room, VerbWord(trigger.VerbId).Split(' ')[0] + " response");
        var target = trigger.ObjectName.Length > 0 ? trigger.ObjectName : $"object {trigger.ObjectId}";
        return Format(trigger.Room, $"{VerbWord(trigger.VerbId)} {(plain ? "" : "at ")}{target}");
    }
}

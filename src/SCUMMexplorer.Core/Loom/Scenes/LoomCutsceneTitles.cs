namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// LOOM's part of the Play Cutscene titles: the verb words (the input verbs and the draft names). The format and the title of a scanned trigger are
/// the engine's (<see cref="ScummCutsceneTitles"/>, since 17 October 2026); <see cref="Format"/> and <see cref="Title"/> forward to it.
/// </summary>
public static class LoomCutsceneTitles
{
    /// <summary>The room prefix (<see cref="ScummCutsceneTitles.Format"/>).</summary>
    public static string Format(int room, string description) => ScummCutsceneTitles.Format(room, description);

    /// <summary>The verb as a word ("Look at", "Walk to", "Use", a draft's name, or "Other draft response" for a verb the table does not name).</summary>
    public static string VerbWord(int verbId) => verbId switch
    {
        0x36 => "Look at",
        0x37 => "Walk to",
        0x38 => "Use",
        var v => LoomDraftNames.ForVerb(v)?.Name ?? "Other draft response"
    };

    /// <summary>The rooms a verb's code leads to, in the form <see cref="ScummCutsceneIndex.Scan"/> takes (the doorway finder of the room hotspots).</summary>
    public static Func<GameResource, ScummScriptDisassembly, IReadOnlyList<int>> Doorways(GameLibrary game)
    {
        var finder = new LoomDoorwayFinder(game);
        return (room, code) => finder.Find(room, code).Select(t => t.RoomId).Distinct().ToArray();
    }

    /// <summary>The title of a scanned trigger (<see cref="ScummCutsceneTitles.Title"/>, with LOOM's verbs when the trigger carries none).</summary>
    public static string Title(ScummCutscene trigger) =>
        ScummCutsceneTitles.Title(trigger.Verbs is null ? trigger with { Verbs = LoomCutsceneVerbs.Roles } : trigger);
}

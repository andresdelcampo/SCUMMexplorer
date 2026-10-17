namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// LOOM's verbs as the cutscene list reads them (data for the engine's <see cref="ScummCutsceneIndex"/>, 17 October 2026): the input scripts'
/// look, walk-to and use verbs 0x36, 0x37 and 0x38 (scripts 005 to 007), and the draft verbs from 0x39 up, each its own entry and titled
/// "&lt;draft&gt; at &lt;object&gt;" (R-8.7, R-8.10). Both editions use the same numbers.
/// </summary>
public static class LoomCutsceneVerbs
{
    public static ScummCutsceneVerbs Roles { get; } = new(Look: 0x36, WalkTo: 0x37, Use: 0x38, SeparateFrom: 0x39,
        Word: LoomCutsceneTitles.VerbWord, AimsAt: verb => verb is not (0x36 or 0x37 or 0x38));

    /// <summary>The LOOM scan: <see cref="ScummCutsceneIndex.Scan"/> with LOOM's verbs.</summary>
    public static IReadOnlyList<ScummCutscene> Scan(IScummDataSource data, IEnumerable<int> rooms,
        Func<GameResource, ScummScriptDisassembly, IReadOnlyList<int>>? doorways = null, GameLibrary? game = null) =>
        ScummCutsceneIndex.Scan(data, rooms, doorways, game, Roles);

    /// <summary>A trigger with LOOM's verbs (for tests and recipes that name one directly).</summary>
    public static ScummCutscene Trigger(int Room, int ObjectId, string ObjectName, int VerbId, IReadOnlyList<string> Scripts,
        int Texts, int RoomLoads, int SpanInstructions, string FirstText, IReadOnlyList<int>? Destinations = null,
        IReadOnlyList<int>? LoadTargets = null, int LoadsAfter = 0) =>
        new(Room, ObjectId, ObjectName, VerbId, Scripts, Texts, RoomLoads, SpanInstructions, FirstText, Destinations, LoadTargets, LoadsAfter, Roles);
}

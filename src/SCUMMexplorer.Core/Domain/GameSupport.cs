namespace SCUMMexplorer.Core.Domain;

/// <summary>
/// What the explorer can do with a game, in one place (CLAUDE.md, "LOOM EGA is the reference; LOOM CD also has ...; Indy3 EGA is
/// supported only for backgrounds and texts"). The UI asks this instead of repeating game checks for its page texts and for the
/// buttons that only make sense for some games.
/// </summary>
public sealed record GameSupport(bool Rooms, bool LiveSession, bool Scripts, bool Cutscenes, bool Texts, bool Status, bool Audio,
    string Subtitle, string EmptyPreview)
{
    private const string Unsupported = "The folder index was read, but room and resource inspection is not supported for this game yet.";
    private const string UnsupportedPreview = "This game layout does not yet have a supported room background decoder.";

    public static GameSupport For(GameLibrary game) => (game.Id, game.Version) switch
    {
        (GameId.Loom, ScummVersion.V3) => new(true, true, true, true, true, true, true,
            "Explore rooms, objects, and sounds.", "Pick a room to explore the game from there."),
        (GameId.LoomVga, _) => new(true, true, true, true, false, true, true,
            "Explore rooms, objects, and sounds.", "Pick a room to explore the game from there."),
        (GameId.IndianaJones3, ScummVersion.V3) => new(true, false, false, false, true, false, false,
            "Room backgrounds and texts from the game.", "Select a room to see its background."),
        _ => new(false, false, false, false, false, false, false, Unsupported, UnsupportedPreview)
    };
}

using System.Text;
using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Core.Domain;

/// <summary>
/// The kinds of game the explorer tells apart for what it supports. One family is one row of the support matrix in the README and in
/// docs/REQUIREMENTS.md (section 3.1). Families are about what the explorer can do, not about the game's name: the three LOOM EGA
/// rows differ in how much of the replay catalog and edition review exists, the demos are kept apart on purpose.
/// </summary>
public enum GameFamily
{
    /// <summary>Not a supported layout: nothing is offered.</summary>
    Unsupported,
    LoomEgaReference,
    LoomEga,
    LoomEgaDemo,
    LoomCd,
    Indy3Ega,
    Indy3Vga,
    MonkeyFloppy,
    MonkeyCd,
    Monkey2,
    Indy4,
    ManiacEnhanced,
    ManiacClassic,
    ZakEnhanced,
    ZakClassic,
    /// <summary>Another old-v3 game whose <c>00.LFL</c> parsed (not in the README matrix).</summary>
    OtherV3,
    /// <summary>Another v1, v2, v4 or v5 game, read by the shared readers but not named by the explorer (not in the README matrix).</summary>
    OtherBrowsed
}

/// <summary>The features of the support matrix, in the order of its columns (docs/REQUIREMENTS.md, section 3.1 defines each).</summary>
public enum SupportFeature { Rooms, Objects, WalkBoxes, Costumes, Sound, Texts, Scripts, Play, Cutscenes, Status }

/// <summary>How far a feature works for a family: <see cref="Full"/> ✔, <see cref="Partial"/> ◐ (a note under the matrix says what is missing), <see cref="None"/> –.</summary>
public enum SupportLevel { None, Partial, Full }

/// <summary>One row of the matrix: a family, how it is named in the README, its level per feature and the texts of the page.</summary>
public sealed record FamilySupport(GameFamily Family, string Label, IReadOnlyList<SupportLevel> Levels, string Subtitle, string EmptyPreview, bool InMatrix = true)
{
    public SupportLevel this[SupportFeature feature] => Levels[(int)feature];
}

/// <summary>
/// What the explorer can do with a game, in one place. This registry is the single source of the support matrix: the README table and
/// the table in docs/REQUIREMENTS.md are generated from <see cref="Families"/> (<see cref="RenderMatrix"/>) and a test fails when they
/// differ. The UI asks it for its page texts and for the buttons that only make sense for some games. Raising a level means the
/// feature works for that family and its conformance check passes (tests/SCUMMexplorer.Tests/SupportConformanceTests.cs).
/// </summary>
public sealed record GameSupport(GameFamily Family, FamilySupport Row)
{
    private const string Unsupported = "The folder index was read, but room and resource inspection is not supported for this game yet.";
    private const string UnsupportedPreview = "This game layout does not yet have a supported room background decoder.";
    private const string Explore = "Explore rooms, objects, and sounds.";
    private const string ExplorePreview = "Pick a room to explore the game from there.";
    private const string BackgroundsAndTexts = "Room backgrounds and texts from the game.";
    private const string Browsed = "Room backgrounds, costumes, sounds, scripts and texts from the game.";
    private const string BrowsedPreview = "Select a room to see its background.";

    private const SupportLevel F = SupportLevel.Full, P = SupportLevel.Partial, N = SupportLevel.None;

    // Columns: Rooms, Objects, WalkBoxes, Costumes, Sound, Texts, Scripts, Play, Cutscenes, Status.
    private static FamilySupport Row_(GameFamily family, string label, SupportLevel[] levels, string subtitle, string preview, bool inMatrix = true) =>
        new(family, label, levels, subtitle, preview, inMatrix);

    public static IReadOnlyList<FamilySupport> Families { get; } =
    [
        Row_(GameFamily.Unsupported, "Unsupported layout", [N, N, N, N, N, N, N, N, N, N], Unsupported, UnsupportedPreview, false),
        Row_(GameFamily.LoomEgaReference, "**LOOM** EGA English (reference)", [F, F, F, F, F, F, F, F, F, F], Explore, ExplorePreview),
        Row_(GameFamily.LoomEga, "**LOOM** EGA other editions", [F, F, F, F, F, F, F, F, P, F], Explore, ExplorePreview),
        Row_(GameFamily.LoomEgaDemo, "**LOOM** demos", [F, F, F, F, F, F, F, F, P, F], Explore, ExplorePreview),
        Row_(GameFamily.LoomCd, "**LOOM** CD", [F, F, F, F, F, F, F, F, P, F], Explore, ExplorePreview),
        Row_(GameFamily.Indy3Ega, "**Indiana Jones 3** EGA", [F, P, F, F, F, F, F, N, N, N], BackgroundsAndTexts, BrowsedPreview),
        Row_(GameFamily.Indy3Vga, "**Indiana Jones 3** 256 colours", [F, P, F, F, F, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.MonkeyFloppy, "**Monkey Island 1** floppy", [F, P, F, F, F, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.MonkeyCd, "**Monkey Island 1** CD", [F, P, F, F, P, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.Monkey2, "**Monkey Island 2**", [F, P, F, F, P, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.Indy4, "**Indiana Jones 4**", [F, P, F, F, P, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.ManiacEnhanced, "**Maniac Mansion** enhanced", [F, P, F, F, F, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.ManiacClassic, "**Maniac Mansion** classic", [F, P, F, F, F, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.ZakEnhanced, "**Zak McKracken** enhanced", [F, P, F, F, F, F, F, N, N, N], Browsed, BrowsedPreview),
        Row_(GameFamily.ZakClassic, "**Zak McKracken** classic", [F, P, F, P, F, F, F, N, N, N], Browsed, BrowsedPreview),
        // Another old-v3 game (Zak McKracken EGA and the like): the room pictures and the texts need nothing game-specific. Limited on purpose.
        Row_(GameFamily.OtherV3, "Other old-v3 game", [F, N, N, N, N, F, N, N, N, N], BackgroundsAndTexts, BrowsedPreview, false),
        Row_(GameFamily.OtherBrowsed, "Other browsed game", [F, P, F, P, P, F, F, N, N, N], Browsed, BrowsedPreview, false)
    ];

    public static FamilySupport RowOf(GameFamily family) => Families.First(row => row.Family == family);

    public static GameSupport For(GameLibrary game) => For(FamilyOf(game));

    public static GameSupport For(GameFamily family) => new(family, RowOf(family));

    /// <summary>The level of one feature for this game's family.</summary>
    public SupportLevel this[SupportFeature feature] => Row[feature];

    public bool Rooms => this[SupportFeature.Rooms] != SupportLevel.None;
    public bool LiveSession => this[SupportFeature.Play] != SupportLevel.None;
    public bool Scripts => this[SupportFeature.Scripts] != SupportLevel.None;
    public bool Cutscenes => this[SupportFeature.Cutscenes] != SupportLevel.None;
    public bool Texts => this[SupportFeature.Texts] != SupportLevel.None;
    public bool Status => this[SupportFeature.Status] != SupportLevel.None;
    public bool Audio => this[SupportFeature.Sound] != SupportLevel.None;
    public string Subtitle => Row.Subtitle;
    public string EmptyPreview => Row.EmptyPreview;

    /// <summary>The edition label of the English LOOM EGA release with the Roland upgrade: the one whose replay catalog is hand-validated.</summary>
    public const string ReferenceEditionLabel = "English, Roland upgrade";

    /// <summary>The one place that maps a detected game onto its family. Version and title decide the browsed games, which are all <see cref="GameId.Unknown"/>.</summary>
    public static GameFamily FamilyOf(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        switch (game.Id)
        {
            case GameId.Loom:
                return game.IsDemo ? GameFamily.LoomEgaDemo : game.Edition == ReferenceEditionLabel ? GameFamily.LoomEgaReference : GameFamily.LoomEga;
            case GameId.LoomVga:
                return GameFamily.LoomCd;
            case GameId.IndianaJones3:
                return game.Version == ScummVersion.V3 ? GameFamily.Indy3Ega : GameFamily.Unsupported;
        }

        var title = game.Title;
        bool Is(string known) => title.StartsWith(known, StringComparison.Ordinal);
        return game.Version switch
        {
            ScummVersion.V3 => GameFamily.OtherV3,
            ScummVersion.V4 when Is(KnownGameTitles.MonkeyIsland) => GameFamily.MonkeyFloppy,
            ScummVersion.V4 when Is(KnownGameTitles.Indy3) => GameFamily.Indy3Vga,
            ScummVersion.V5 when Is(KnownGameTitles.MonkeyIsland2) => GameFamily.Monkey2,
            ScummVersion.V5 when Is(KnownGameTitles.MonkeyIsland) => GameFamily.MonkeyCd,
            ScummVersion.V5 when Is(KnownGameTitles.FateOfAtlantis) => GameFamily.Indy4,
            ScummVersion.V2 when Is(KnownGameTitles.ManiacMansion) => GameFamily.ManiacEnhanced,
            ScummVersion.V2 when Is(KnownGameTitles.ZakMcKracken) => GameFamily.ZakEnhanced,
            ScummVersion.V1 when game.V1Game == ScummV1Game.ManiacMansion => GameFamily.ManiacClassic,
            ScummVersion.V1 when game.V1Game == ScummV1Game.ZakMcKracken => GameFamily.ZakClassic,
            ScummVersion.V1 or ScummVersion.V2 or ScummVersion.V4 or ScummVersion.V5 => GameFamily.OtherBrowsed,
            _ => GameFamily.Unsupported
        };
    }

    private static string Name(SupportFeature feature) => feature == SupportFeature.WalkBoxes ? "Walk boxes" : feature.ToString();

    private static string Mark(SupportLevel level) => level switch { SupportLevel.Full => "✔", SupportLevel.Partial => "◐", _ => "–" };

    /// <summary>
    /// The support matrix as a Markdown table (the rows with <see cref="FamilySupport.InMatrix"/>). The README and docs/REQUIREMENTS.md hold this text between
    /// the markers <c>&lt;!-- support-matrix:start --&gt;</c> and <c>&lt;!-- support-matrix:end --&gt;</c>.
    /// </summary>
    public static string RenderMatrix()
    {
        var features = Enum.GetValues<SupportFeature>();
        var text = new StringBuilder();
        text.Append("| Game |").AppendJoin("", features.Select(f => $" {Name(f)} |")).Append('\n');
        text.Append("|---|").AppendJoin("", features.Select(_ => ":-:|")).Append('\n');
        foreach (var row in Families.Where(row => row.InMatrix))
            text.Append($"| {row.Label} |").AppendJoin("", features.Select(f => $" {Mark(row[f])} |")).Append('\n');
        return text.ToString();
    }
}

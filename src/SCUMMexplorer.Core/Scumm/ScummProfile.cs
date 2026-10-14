using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// How the engine must behave for one game: the one place that maps a detected game onto engine variants, so that engine code asks
/// the profile ("does this version walk the v4 way?") instead of testing which game it is running. A new SCUMM version or game
/// adds a case here and, where it really differs, a reader or data source in its <c>V3</c>/<c>V4</c>-style folder.
/// </summary>
public sealed record ScummProfile(ScummVersion Version, ScummScriptDialect Dialect)
{
    /// <summary>Actor walking follows the v4 routines (<c>Actor</c>) instead of the old-v3 ones (<c>Actor_v3</c>).</summary>
    public bool UsesV4Walking => Version >= ScummVersion.V4;

    /// <summary>The game's resources live in a v4 archive (<c>DISK01.LEC</c>) instead of old-v3 <c>*.LFL</c> files.</summary>
    public bool UsesV4Archive => Version >= ScummVersion.V4;

    /// <summary>The constants of the AdLib music driver (ScummVM <c>Player_AD</c>): LOOM's differ from every other game's.</summary>
    public ScummAdlibTiming AdlibTiming { get; init; } = ScummAdlibTiming.Standard;

    /// <summary>
    /// Variables the frame loop adds the frame's jiffies to besides <c>VAR_TMR_1</c> to <c>VAR_TMR_3</c> (ScummVM <c>scummLoop</c>, scumm.cpp: "Amiga/PC versions
    /// of Indy3 set three extra timers", variables 39 to 41). The 256-colour Indiana Jones and the Last Crusade has them too.
    /// </summary>
    public IReadOnlyList<string> ExtraTimerVariables { get; init; } = [];

    /// <summary>
    /// The room view and room cards of this game come from the first visit the generic session runs (<see cref="ScummRoomFirstVisit"/>, S5): a browsed game
    /// of the old bundle whose boot the session follows (Indiana Jones and the Last Crusade EGA). LOOM has its own session and scenes.
    /// </summary>
    public bool HasFirstVisitScenes { get; init; }

    /// <summary>
    /// The value the engine gives <c>VAR_VIDEOMODE</c> (variable 49) at start (<c>setVideoModeVarToCurrentConfig</c>, vars.cpp): 13 for an EGA game, 19 for VGA.
    /// Null before version 3, which has no such variable. A game's scripts test it to choose their palettes (Indiana Jones tests for 4, CGA).
    /// </summary>
    public int? VideoMode => Version < ScummVersion.V3 ? null : UsesV4Archive ? 19 : 13;

    /// <summary><c>VAR_CHARINC</c> (variable 37) at start: <c>resetScummVars</c> gives the delay between the characters of a message 4 in every version.</summary>
    public const int CharIncAtStart = 4;

    public static ScummProfile For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        // The 256-colour Indy3 has v4-style blocks (one file per room) but version 3 scripts, with Indy3's opcode differences.
        var roomFileGame = game.V4Disks?.Disks.OfType<ScummV4Archive>().Any(disk => disk.IsRoomFileGame) == true;
        var dialect = game.Version is ScummVersion.V1 or ScummVersion.V2 ? ScummScriptDialect.V2
            : roomFileGame ? ScummScriptDialect.V3Indy3
            : game.Version == ScummVersion.V5 ? ScummScriptDialect.V5
            : game.Version >= ScummVersion.V4 ? ScummScriptDialect.V4
            : game.Id == GameId.IndianaJones3 ? ScummScriptDialect.V3Indy3
            : ScummScriptDialect.V3;
        return new ScummProfile(game.Version, dialect)
        {
            AdlibTiming = game.Id is GameId.Loom or GameId.LoomVga ? ScummAdlibTiming.Loom : ScummAdlibTiming.Standard,
            HasFirstVisitScenes = game.Id == GameId.IndianaJones3 && game.Version == ScummVersion.V3,
            ExtraTimerVariables = game.Id == GameId.IndianaJones3 ? [ScummVars.ExtraTimer1, ScummVars.ExtraTimer2, ScummVars.ExtraTimer3] : []
        };
    }
}

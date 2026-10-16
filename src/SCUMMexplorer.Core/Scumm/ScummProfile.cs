using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// How the engine must behave for one game: the one place that maps a detected game onto engine variants, so that engine code asks
/// the profile ("does this version walk the v4 way?") instead of testing which game it is running. A new SCUMM version or game
/// adds a case here and, where it really differs, a reader or data source in its <c>V3</c>/<c>V4</c>-style folder.
/// </summary>
/// <summary>
/// The labelled assumed input of a boot that waits for the player (R-15.5). When nothing in the boot changes for a while, the session presses a key:
/// the digits of the value in <paramref name="AnswerVariable"/> when the game has put an expected answer there (read, never written: the game's
/// own script derived it), else <paramref name="ContinueKey"/> (Enter). Keys go through the game's own input script like a player's.
/// </summary>
public sealed record ScummBootInputPlan(string? AnswerVariable, int ContinueKey = 13);

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
    /// whose boot the session follows, decided by the game's format and dialect (old-bundle v3 with the Indy3 dialect, v4 on several disks; S5 Indy3Ega and
    /// MonkeyFloppy). LOOM has its own session and scenes.
    /// </summary>
    public bool HasFirstVisitScenes { get; init; }

    /// <summary>
    /// The value the engine gives <c>VAR_VIDEOMODE</c> (variable 49) at start (<c>setVideoModeVarToCurrentConfig</c>, vars.cpp): 13 for an EGA game, 19 for VGA.
    /// Null before version 3, which has no such variable. A game's scripts test it to choose their palettes (Indiana Jones tests for 4, CGA).
    /// </summary>
    public int? VideoMode => Version < ScummVersion.V3 ? null : UsesV4Archive ? 19 : 13;

    /// <summary><c>VAR_CHARINC</c> (variable 37) at start: <c>resetScummVars</c> gives the delay between the characters of a message 4 in every version.</summary>
    public const int CharIncAtStart = 4;

    /// <summary>
    /// Variables the engine sets to a value besides the ones every session sets (<c>resetScummVars</c>, vars.cpp, read 14 October 2026): for version 4 and later
    /// <c>VAR_HEAPSPACE</c> = 1400 and <c>VAR_FIXEDDISK</c> = 1 (the game is on a hard disk), and <c>VAR_DEBUGMODE</c> = 0 (version 4 and later, no debug mode).
    /// </summary>
    public IReadOnlyList<(string Variable, ushort Value)> EngineVariablesAtStart =>
        UsesV4Archive ? [(ScummVars.HeapSpace, 1400), (ScummVars.FixedDisk, 1), (ScummVars.DebugMode, 0)] : [];

    /// <summary>
    /// How many global variables the engine allocates, all zero at start (ScummVM <c>_numVariables</c>, 800 for the version 4 and 5 games); 0 where the
    /// session does not yet start every variable at 0 (the old-v3 games keep reading unwritten variables as unknown).
    /// </summary>
    public int GlobalVariableCount => UsesV4Archive ? 800 : 0;

    /// <summary>
    /// How a stalled boot is answered when the game waits for the player (a "press a key" screen, a copy-protection question); null when the
    /// boot of the game does not wait. Edition data: the answer variable is a slot of this game's own scripts.
    /// </summary>
    public ScummBootInputPlan? BootInput { get; init; }

    /// <summary>
    /// <c>actorFollowCamera</c> on an actor that is not in the current room starts that room's scene (ScummVM camera.cpp <c>setCameraFollows</c>, read 14 October 2026).
    /// Modelled for the generic session of the version 4 games (Monkey Island's boot ends script 1 that way); LOOM's sessions keep reporting it as not modelled.
    /// </summary>
    public bool CameraFollowStartsScene => UsesV4Archive;

    /// <summary>
    /// Bit variables (<c>bit[n]</c>) are stored apart from the word variables (ScummVM <c>readVar</c>/<c>writeVar</c>, script.cpp, read 15 October 2026: up to
    /// version 3 a bit is bit <c>n &amp; 15</c> of <c>_scummVars[n &gt;&gt; 4]</c>; from version 4 it lives in <c>_bitVars</c>). Without it a v4 game's bits
    /// 0x040 to 0x04F would overwrite <c>VAR_ROOM</c> (S5 MonkeyFloppy). LOOM CD sets the same storage in its own session.
    /// </summary>
    public bool SeparateBitVariables => Version >= ScummVersion.V4;

    /// <summary>The boot is over only when the player has control, not when script 1 has ended (a game whose script 1 hands over to an opening scene).</summary>
    public bool BootEndsWhenPlayerHasControl { get; init; }

    /// <summary>
    /// The game is played through its own verb interface (REQUIREMENTS §7.1, Proposed 15 October 2026): its verb area, sentence line and inventory are drawn
    /// from its verbs and the player's clicks and keys go to its input script (<see cref="ScummSession.ClickScreen"/>). True only for a family whose Play
    /// cell is raised (R-7.17: no clicks reach the scripts of the others). S6 Indy3Ega.
    /// </summary>
    public bool PlaysThroughVerbs { get; init; }

    /// <summary>The third argument of <c>runInputScript</c> for a left click: 0 up to version 3, 1 from version 4 (ScummVM <c>checkExecVerbs</c>).</summary>
    public int ClickInputMode => Version > ScummVersion.V3 ? 1 : 0;

    /// <summary>
    /// Keys not delivered to the game in play (R-7.11): a game's save/load key, whose script leaves the session's state (Indiana Jones EGA: F5 = 319,
    /// Script 24 chains to Script 9 when <c>bit[0x5E4]</c> is 0). Edition data.
    /// </summary>
    public IReadOnlyList<int> UndeliveredKeys { get; init; } = [];

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
            // R-6.12a: every game the generic session boots shows its rooms' first visit. The session reads the old-bundle v3 scripts of Indiana Jones and the
            // v4 scripts of a game on several disks; LOOM (EGA and CD) has its own session, and the 256-colour Indy3 (room files) has no boot yet (S4b).
            HasFirstVisitScenes = game.Id is not (GameId.Loom or GameId.LoomVga) && !roomFileGame
                && (dialect == ScummScriptDialect.V3Indy3 && game.Version == ScummVersion.V3 || dialect == ScummScriptDialect.V4 && game.V4Archive is null && game.V4Disks is not null),
            ExtraTimerVariables = game.Id == GameId.IndianaJones3 ? [ScummVars.ExtraTimer1, ScummVars.ExtraTimer2, ScummVars.ExtraTimer3] : [],
            // The Secret of Monkey Island (floppy): script 152 (room 90) asks the copy-protection question and keeps the right year in var[0x10A] (+0x00EE) before it waits.
            BootInput = GameSupport.FamilyOf(game) == GameFamily.MonkeyFloppy ? new ScummBootInputPlan("var[0x10A]") : null,
            BootEndsWhenPlayerHasControl = GameSupport.FamilyOf(game) == GameFamily.MonkeyFloppy,
            // S6 (15 October 2026): Indiana Jones EGA plays through its verbs; F5 is its save/load key (Script 24).
            PlaysThroughVerbs = GameSupport.FamilyOf(game) == GameFamily.Indy3Ega,
            UndeliveredKeys = GameSupport.FamilyOf(game) == GameFamily.Indy3Ega ? [0x13F] : []
        };
    }
}

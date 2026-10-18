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
/// <remarks>
/// <paramref name="ClickObject"/> (S4b Monkey2, 17 October 2026): the boot waits for a click on a screen of choices instead (Monkey Island 2's "Monkey Island 2"
/// or "Monkey 2 Lite"); the session clicks the middle of that object's box through the game's input script, as a player would. Edition data: the object of
/// the choice the explorer takes (§4.5: the full game).
/// </remarks>
public sealed record ScummBootInputPlan(string? AnswerVariable, int ContinueKey = 13, int? ClickObject = null);

public sealed record ScummProfile(ScummVersion Version, ScummScriptDialect Dialect)
{
    /// <summary>
    /// The version of the engine that runs the game's scripts, which can differ from the container format of <see cref="Version"/>: the 256-colour Indiana Jones and
    /// the Last Crusade stores v4-style blocks (one file per room) but is a version 3 game for ScummVM (<c>detection_tables.h</c>; S4b Indy3Vga, 15 October 2026).
    /// Engine behaviour (walking, variables, bit storage) follows this version; reading the data follows <see cref="Version"/>.
    /// </summary>
    public ScummVersion EngineVersion { get; init; } = Version;

    /// <summary>ScummVM scumm.cpp: Maniac Mansion allocates 25 actor slots; these other families allocate 13, including reserved slot zero.</summary>
    public int ActorCount { get; init; } = 13;
    /// <summary>Maniac v1 o2_endCutscene restores the saved camera mode and, unless following ego, the saved room.</summary>
    public bool RestoresClassicCutsceneScene { get; init; }
    /// <summary>Classic Maniac verb rows are eight pixels below the stored coordinate (o2_verbOps).</summary>
    public int V2VerbYOffset { get; init; }

    /// <summary>Actor walking follows the v4 routines (<c>Actor</c>) instead of the old-v3 ones (<c>Actor_v3</c>).</summary>
    public bool UsesV4Walking => EngineVersion >= ScummVersion.V4;

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
        EngineVersion >= ScummVersion.V5
            // Version 5 adds (vars.cpp, ScummVM master read 17 October 2026): VAR_INPUTMODE = 3 (mouse) and VAR_FADE_DELAY = 3 in resetScummVars,
            // VAR_V5_TALK_STRING_Y = -0x50 in ScummEngine_v5::resetScummVars, and the game's own GameVariablesAtStart below.
            ? [(ScummVars.HeapSpace, 1400), (ScummVars.FixedDisk, 1), (ScummVars.DebugMode, 0), (ScummVars.InputMode, 3), (ScummVars.FadeDelay, 3),
               (ScummVars.TalkStringY, unchecked((ushort)-0x50)), .. GameVariablesAtStart]
            : EngineVersion >= ScummVersion.V4 ? [(ScummVars.HeapSpace, 1400), (ScummVars.FixedDisk, 1), (ScummVars.DebugMode, 0)]
            : EngineVersion <= ScummVersion.V2 ? GameVariablesAtStart : [];

    /// <summary>
    /// Variables the engine sets for one game only (edition data): <c>ScummEngine_v5::resetScummVars</c> gives <c>GID_MONKEY</c> (The Secret of Monkey Island CD,
    /// version 5; the floppy release is <c>GID_MONKEY_VGA</c>) <c>_scummVars[74]</c> = 1225 (vars.cpp, ScummVM master read 17 October 2026; no comment in the source).
    /// </summary>
    public IReadOnlyList<(string Variable, ushort Value)> GameVariablesAtStart { get; init; } = [];

    /// <summary>
    /// How many global variables the engine allocates, all zero at start (ScummVM <c>_numVariables</c>, 800 for the version 4 and 5 games); 0 where the
    /// session does not yet start every variable at 0 (the old-v3 games keep reading unwritten variables as unknown).
    /// </summary>
    public int GlobalVariableCount => EngineVersion >= ScummVersion.V4 ? 800 : 0;

    /// <summary>
    /// How a stalled boot is answered when the game waits for the player (a "press a key" screen, a copy-protection question); null when the
    /// boot of the game does not wait. Edition data: the answer variable is a slot of this game's own scripts.
    /// </summary>
    public ScummBootInputPlan? BootInput { get; init; }

    /// <summary>
    /// <c>actorFollowCamera</c> on an actor that is not in the current room starts that room's scene (ScummVM camera.cpp <c>setCameraFollows</c>, read 14 October 2026).
    /// Modelled for the generic session of the version 4 games (Monkey Island's boot ends script 1 that way); LOOM's sessions keep reporting it as not modelled.
    /// </summary>
    public bool CameraFollowStartsScene => EngineVersion <= ScummVersion.V2 || EngineVersion >= ScummVersion.V4;

    /// <summary>
    /// Bit variables (<c>bit[n]</c>) are stored apart from the word variables (ScummVM <c>readVar</c>/<c>writeVar</c>, script.cpp, read 15 October 2026: up to
    /// version 3 a bit is bit <c>n &amp; 15</c> of <c>_scummVars[n &gt;&gt; 4]</c>; from version 4 it lives in <c>_bitVars</c>). Without it a v4 game's bits
    /// 0x040 to 0x04F would overwrite <c>VAR_ROOM</c> (S5 MonkeyFloppy). LOOM CD sets the same storage in its own session.
    /// </summary>
    public bool SeparateBitVariables => EngineVersion >= ScummVersion.V4;

    /// <summary>The boot is over only when the player has control, not when script 1 has ended (a game whose script 1 hands over to an opening scene).</summary>
    public bool BootEndsWhenPlayerHasControl { get; init; }

    /// <summary>
    /// The game is played through its own verb interface (REQUIREMENTS §7.1, Proposed 15 October 2026): its verb area, sentence line and inventory are drawn
    /// from its verbs and the player's clicks and keys go to its input script (<see cref="ScummSession.ClickScreen"/>). True only for a family whose Play
    /// cell is raised (R-7.17: no clicks reach the scripts of the others). S6 Indy3Ega; S6 Indy3Vga (16 October 2026) by the same data: a game whose
    /// first visit the generic session runs with the Indy3 script dialect, whatever its container (old-bundle LFL or one file per room).
    /// </summary>
    public bool PlaysThroughVerbs { get; init; }

    /// <summary><c>cursorCommand</c> copies the cursor state and user input into <c>VAR_CURSORSTATE</c>/<c>VAR_USERPUT</c> (from version 4, <c>o5_cursorCommand</c>).</summary>
    public bool PublishesCursorVariables => EngineVersion >= ScummVersion.V4;

    /// <summary>
    /// The class number of <c>kObjectClassUntouchable</c> as the game's data stores it (object.cpp <c>getClass</c>/<c>putClass</c>, ScummVM master read 17 October 2026):
    /// the small-header games (up to version 4) map it to 24; from version 5 it is 32 itself. <c>findObject</c> skips an object with it.
    /// </summary>
    public int UntouchableClass => Version >= ScummVersion.V5 ? 32 : ScummObjectClasses.Untouchable;

    /// <summary>
    /// <c>setVerbObject</c> (verbs.cpp, ScummVM master read 17 October 2026) reads an image verb's picture from the room the script names (<c>findObjectInRoom</c>)
    /// in the games without the small header (version 5); the small-header and old-bundle games take it only from a local object of the current room.
    /// Monkey Island CD's inventory script (Script 9) assigns the icons of Room 99 that way.
    /// </summary>
    public bool VerbImagesFromNamedRoom => Version >= ScummVersion.V5;

    /// <summary>The variable holding the engine's save/load menu key (<c>VAR_MAINMENU_KEY</c>, from version 4); the engine opens its menu and the scripts never see the key.</summary>
    public string? MainMenuKeyVariable => EngineVersion >= ScummVersion.V4 ? ScummVars.MainMenuKey : null;
    /// <summary>
    /// The third argument of <c>runInputScript</c> for a left click: 0 up to version 3, 1 from version 4 (ScummVM <c>checkExecVerbs</c>: <c>_game.version &gt; 3</c>).
    /// The engine version decides: the 256-colour Indiana Jones stores v4-style blocks but ScummVM runs it as version 3, so mode 0 (S6 Indy3Vga).
    /// </summary>
    public int ClickInputMode => EngineVersion > ScummVersion.V3 ? 1 : 0;

    /// <summary>
    /// R-7.16 (Rule, confirmed by the user 16 October 2026): in play, the right button goes to the game only where the game's own interface reads it,
    /// decided by what the engine version hands the scripts (ScummVM master <c>processInput</c> and <c>checkExecVerbs</c>, read 16 October 2026). From
    /// version 4 a right click is a click with mode 2 (<c>(_game.version &gt; 3) ? (left ? 1 : 2) : 0</c>), which the input script can tell from a left
    /// click (Monkey Island floppy Script 4 +0x0005 <c>isEqual local[0x002], 2</c>: the two-button default verb). Up to version 3 the scripts never see
    /// it: ScummVM turns it into the Esc key outside LOOM (the app's Skip already gives that), and LOOM's mode is 0 like a left click. Elsewhere the
    /// right button stays the app's.
    /// </summary>
    public bool RightClickReachesGame => PlaysThroughVerbs && EngineVersion > ScummVersion.V3;

    /// <summary>The third argument of <c>runInputScript</c> for a right click where <see cref="RightClickReachesGame"/> (ScummVM <c>checkExecVerbs</c>: 2 from version 4).</summary>
    public int RightClickInputMode => EngineVersion > ScummVersion.V3 ? 2 : 0;

    /// <summary>
    /// Keys not delivered to the game in play (R-7.11): a game's save/load key, whose script leaves the session's state (Indiana Jones EGA and 256 colours:
    /// F5 = 319, Script 24 chains to Script 9 when <c>bit[0x5E4]</c> is 0; the two releases' Script 24 are the same instructions). Edition data.
    /// </summary>
    public IReadOnlyList<int> UndeliveredKeys { get; init; } = [];

    /// <summary>
    /// What the engine stops when a music sound starts on the session's AdLib device (S6 sound, 16 October 2026; ScummVM master read that day): the
    /// AdLib player of LOOM and Indiana Jones (<c>Player_AD::startSound</c>: a resource whose third byte is 0x80 is music and <c>stopMusic()</c> ends the one
    /// playing; effects take one of three slots) stops the previous music; Monkey Island EGA/VGA, which runs through iMuse, stops every sound
    /// (<c>Sound::triggerSound</c>: "the music is never explicitly stopped", <c>stopAllSounds()</c> before any resource that is not an <c>ASFX</c>,
    /// and <c>convertADResource</c> makes an <c>ASFX</c> of an AD block whose third byte is not 0x80). Only the generic session applies it; LOOM's session is unchanged.
    /// </summary>
    public ScummMusicStart MusicStart { get; init; }

    public static ScummProfile For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        // The 256-colour Indy3 has v4-style blocks (one file per room) but version 3 scripts, with Indy3's opcode differences.
        var roomFileGame = game.V4Disks?.Disks.OfType<ScummV4Archive>().Any(disk => disk.IsRoomFileGame) == true;
        var dialect = game.Version == ScummVersion.V1 ? ScummScriptDialect.V1
            : game.Version == ScummVersion.V2 ? ScummScriptDialect.V2
            : roomFileGame ? ScummScriptDialect.V3Indy3
            : game.Version == ScummVersion.V5 ? ScummScriptDialect.V5
            : game.Version >= ScummVersion.V4 ? ScummScriptDialect.V4
            : game.Id == GameId.IndianaJones3 ? ScummScriptDialect.V3Indy3
            : ScummScriptDialect.V3;
        return new ScummProfile(game.Version, dialect)
        {
            EngineVersion = roomFileGame ? ScummVersion.V3 : game.Version,
            ActorCount = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced ? 25 : 13,
            RestoresClassicCutsceneScene = GameSupport.FamilyOf(game) == GameFamily.ManiacClassic,
            V2VerbYOffset = GameSupport.FamilyOf(game) == GameFamily.ManiacClassic ? 8 : 0,
            AdlibTiming = game.Id is GameId.Loom or GameId.LoomVga ? ScummAdlibTiming.Loom : ScummAdlibTiming.Standard,
            // R-6.12a: every game the generic session boots shows its rooms' first visit. The session reads the version 3 scripts of Indiana Jones (old-bundle
            // LFL files, and the 256-colour room files since S4b Indy3Vga) and the v4 scripts of a game on several disks; LOOM (EGA and CD) has its own session.
            HasFirstVisitScenes = game.Id is not (GameId.Loom or GameId.LoomVga)
                && (dialect == ScummScriptDialect.V3Indy3 && (game.Version == ScummVersion.V3 || roomFileGame) || dialect == ScummScriptDialect.V4 && game.V4Archive is null && game.V4Disks is not null
                    // S5 MonkeyCd (17 October 2026): the v5 format, for the first v5 family whose boot the session follows (S4b).
                    || dialect == ScummScriptDialect.V5 && game.V4Disks?.Disks.OfType<ScummV5Archive>().Any() == true && GameSupport.FamilyOf(game) == GameFamily.MonkeyCd
                    // S5 Indy4 (17 October 2026): Indiana Jones and the Fate of Atlantis, the second v5 family with an S4b boot; Monkey Island 2 (S5 Monkey2) the third.
                    || dialect == ScummScriptDialect.V5 && game.V4Disks?.Disks.OfType<ScummV5Archive>().Any() == true && GameSupport.FamilyOf(game) is GameFamily.Indy4 or GameFamily.Monkey2
                    || GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced or GameFamily.ZakClassic or GameFamily.ZakEnhanced),
            ExtraTimerVariables = GameSupport.FamilyOf(game) is GameFamily.Indy3Ega or GameFamily.Indy3Vga ? [ScummVars.ExtraTimer1, ScummVars.ExtraTimer2, ScummVars.ExtraTimer3] : [],
            // The Secret of Monkey Island (floppy): script 152 (room 90) asks the copy-protection question and keeps the right year in var[0x10A] (+0x00EE) before it waits.
            BootInput = GameSupport.FamilyOf(game) == GameFamily.MonkeyFloppy ? new ScummBootInputPlan("var[0x10A]")
                // Monkey Island 2 (S4b, 17 October 2026): Script 130 +0x034A waits for the choice between "Monkey Island 2" (object 1156, Script 131 +0x0067:
                // bit[0x237] = 0, "Regular Mode") and "Monkey 2 Lite" (object 1157); the explorer takes the full game (§4.5), labelled as assumed input.
                : GameSupport.FamilyOf(game) == GameFamily.Monkey2 ? new ScummBootInputPlan(null, ClickObject: 0x484)
                : null,
            // Monkey Island floppy and CD: script 1 ends by following the ego into the opening (Room 38), the player gets control later (S4b MonkeyCd: the CD boot too).
            BootEndsWhenPlayerHasControl = GameSupport.FamilyOf(game) is GameFamily.MonkeyFloppy or GameFamily.MonkeyCd
                or GameFamily.ZakClassic or GameFamily.ZakEnhanced
                // S4b Indy4 (17 October 2026): script 1 loads Barnett College (Room 4) with user input off; the playable prologue starts when local script 206 turns it on.
                || GameSupport.FamilyOf(game) is GameFamily.Indy4 or GameFamily.Monkey2,
            // vars.cpp ScummEngine_v5::resetScummVars: if (_game.id == GID_MONKEY) _scummVars[74] = 1225 (the CD release).
            GameVariablesAtStart = GameSupport.FamilyOf(game) switch
            {
                GameFamily.MonkeyCd => [("var[0x04A]", 1225)],
                // vars.cpp ScummEngine_v2::resetScummVars: Zak's engine speed enables the elaborate introduction.
                GameFamily.ZakClassic or GameFamily.ZakEnhanced => [(ScummVars.V2.MachineSpeed, 0x7FFF)],
                _ => []
            },
            // S6 (15 October 2026): Indiana Jones EGA plays through its verbs; F5 is its save/load key (Script 24). S6 Indy3Vga (16 October 2026): the
            // 256-colour release has the same sentence, inventory and key scripts (Script 4 differs by one branch), so the dialect decides, not the family. S6 MonkeyFloppy (16 October 2026): Monkey Island floppy.
            // S6 MonkeyCd and Maniac/Zak (17 October 2026): native version-specific input and presentation.
            // S6 Monkey2 (17 October 2026): Monkey Island 2 plays through its own verbs and inventory icons on version 5.
            PlaysThroughVerbs = PlaysIndy3Interface(game, dialect, roomFileGame) || GameSupport.FamilyOf(game) is GameFamily.MonkeyFloppy or GameFamily.MonkeyCd
                or GameFamily.ManiacClassic or GameFamily.ManiacEnhanced or GameFamily.ZakClassic or GameFamily.ZakEnhanced or GameFamily.Monkey2,
            UndeliveredKeys = PlaysIndy3Interface(game, dialect, roomFileGame) ? [0x13F] : [],
            // scumm.cpp setupMusic: DOS + AdLib + (LOOM or Indy3) → Player_AD; the other DOS games of version 3 and later → iMuse (Monkey's workaround in triggerSound).
            MusicStart = game.Id is GameId.Loom or GameId.LoomVga || GameSupport.FamilyOf(game) is GameFamily.Indy3Ega or GameFamily.Indy3Vga ? ScummMusicStart.StopsPreviousMusic
                : GameSupport.FamilyOf(game) == GameFamily.MonkeyFloppy ? ScummMusicStart.StopsAllSounds
                : ScummMusicStart.StopsNothing
        };
    }

    // The generic session runs the Indy3 dialect (old-bundle v3 or one file per room): Indiana Jones EGA and 256 colours. LOOM has its own session.
    private static bool PlaysIndy3Interface(GameLibrary game, ScummScriptDialect dialect, bool roomFileGame) =>
        dialect == ScummScriptDialect.V3Indy3 && (game.Version == ScummVersion.V3 || roomFileGame) && game.Id is not (GameId.Loom or GameId.LoomVga);
}

/// <summary>What the engine stops when a music sound starts (<see cref="ScummProfile.MusicStart"/>).</summary>
public enum ScummMusicStart
{
    /// <summary>Nothing is known to stop: sounds overlap until a script stops them.</summary>
    StopsNothing,
    /// <summary><c>Player_AD</c>: one music at a time; the new music ends the previous one, effects play on.</summary>
    StopsPreviousMusic,
    /// <summary>Monkey Island EGA/VGA through iMuse: any music start stops every sound first.</summary>
    StopsAllSounds
}

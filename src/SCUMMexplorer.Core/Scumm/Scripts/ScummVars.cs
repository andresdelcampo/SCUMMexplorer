namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The engine-defined global variables of the SCUMM v3-v5 family, as the keys the script runtime uses (ScummVM <c>VAR_*</c>).
/// Games set the rest; a later engine version with another numbering would get its own table chosen by <see cref="ScummProfile"/>.
/// These numbers are ScummVM's <c>setupScummVars</c> (vars.cpp, checked 14 October 2026) for version 3 (the old bundle: LOOM and Indiana Jones and
/// the Last Crusade EGA) and version 4, where the base table is the same; version 4 only adds 27, 39 and 50 to 53, version 5 adds 56 to 72.
/// Version 2 and version 1 number them differently (<c>ScummEngine_v2::setupScummVars</c>); their separate table is <see cref="V2"/>.
/// One engine tick the DOS v3 games add besides the timers of this table: <c>scummLoop</c> also adds the frame's jiffies to variables 39 to 41 of
/// Indiana Jones and the Last Crusade (every non-Macintosh Indy3 and the FM-Towns Zak), see <see cref="ScummProfile.ExtraTimerVariables"/>.
/// </summary>
public static class ScummVars
{
    /// <summary>SCUMM v1/v2 slots (ScummVM vars.cpp setupScummVars, read 17 October 2026). Absent v3 roles must not write game variables.</summary>
    public static class V2
    {
        public const string Ego = "var[0x000]";
        public const string MachineSpeed = "var[0x006]";
        public const string CharCount = "var[0x007]";
        public const string ActiveVerb = "var[0x008]";
        public const string ActiveObject1 = "var[0x009]";
        public const string ActiveObject2 = "var[0x00A]";
        public const string CurrentLights = "var[0x00C]";
        public const string MusicTimer = "var[0x011]";
        public const string VerbAllowed = "var[0x012]";
        public const string CursorState = "var[0x015]";
        public const string CameraMinX = "var[0x017]";
        public const string CameraMaxX = "var[0x018]";
        public const string TimerNext = "var[0x019]";
        public const string SentenceVerb = "var[0x01A]";
        public const string SentenceObject1 = "var[0x01B]";
        public const string SentenceObject2 = "var[0x01C]";
        public const string SentencePreposition = "var[0x01D]";
        public const string MouseX = "var[0x01E]";
        public const string MouseY = "var[0x01F]";
        public const string ClickArea = "var[0x020]";
        public const string ClickVerb = "var[0x021]";
        public const string ClickObject = "var[0x023]";
        public const string RoomResource = "var[0x024]";
        public const string LastSound = "var[0x025]";
        public const string Keypress = "var[0x027]";
        public const string CutsceneExitKey = "var[0x028]";
        public const string TalkActor = "var[0x029]";
        // Actor_v2::isPlayer, actor.cpp (17 October 2026); classic Maniac uses Ego instead.
        public const string FirstPlayerActor = "var[0x02A]";
        public const string LastPlayerActor = "var[0x02B]";
    }
    /// <summary><c>VAR_RESULT</c>: where <c>getActor*</c>-style opcodes leave their answer.</summary>
    public const string Result = "var[0x000]";
    /// <summary><c>VAR_EGO</c>: the actor the player controls.</summary>
    public const string Ego = "var[0x001]";
    /// <summary><c>VAR_CAMERA_POS_X</c>.</summary>
    public const string CameraPosX = "var[0x002]";
    /// <summary><c>VAR_HAVE_MSG</c>: nonzero while a message is on screen.</summary>
    public const string HaveMsg = "var[0x003]";
    /// <summary><c>VAR_CURRENT_LIGHTS</c> (vars.cpp: 9 for v3 to v5): bit 1 room lights on, bit 2 flashlight on, bit 3 actors use their own colours, bit 0 base palette.</summary>
    public const string CurrentLights = "var[0x009]";
    /// <summary><c>VAR_MUSIC_TIMER</c>.</summary>
    public const string MusicTimer = "var[0x00E]";
    /// <summary>Variables 39 to 41, the three extra timers of Indiana Jones and the Last Crusade (<see cref="ScummProfile.ExtraTimerVariables"/>).</summary>
    public const string ExtraTimer1 = "var[0x027]";
    public const string ExtraTimer2 = "var[0x028]";
    public const string ExtraTimer3 = "var[0x029]";
    /// <summary><c>VAR_CAMERA_MIN_X</c>.</summary>
    public const string CameraMinX = "var[0x011]";
    /// <summary><c>VAR_CAMERA_MAX_X</c>.</summary>
    public const string CameraMaxX = "var[0x012]";
    /// <summary><c>VAR_VIRT_MOUSE_X</c>.</summary>
    public const string MouseX = "var[0x014]";
    /// <summary><c>VAR_VIRT_MOUSE_Y</c>.</summary>
    public const string MouseY = "var[0x015]";
    /// <summary><c>VAR_CHARINC</c>: the delay between characters of a message.</summary>
    public const string CharInc = "var[0x025]";
    /// <summary><c>VAR_SOUNDCARD</c>: the sound card the game's scripts see (0 in a session by default).</summary>
    public const string SoundCard = "var[0x030]";
    /// <summary><c>VAR_VIDEOMODE</c> (variable 49): 13 EGA, 19 VGA, 4 CGA.</summary>
    public const string VideoMode = "var[0x031]";
    /// <summary>
    /// <c>VAR_WALKTO_OBJ</c> (variable 38, versions 3 to 5): while <c>o5_loadRoomWithEgo</c> enters a room, the object the hero comes in by (a door);
    /// 0 again once the room is entered.
    /// </summary>
    public const string WalkToObject = "var[0x026]";
    /// <summary><c>VAR_ROOM</c>: the room the game is in.</summary>
    public const string Room = "var[0x004]";
    /// <summary><c>VAR_ROOM_RESOURCE</c> (vars.cpp: 22 for v3 to v5): the resource number of the room shown.</summary>
    public const string RoomResource = "var[0x016]";
    /// <summary><c>VAR_OVERRIDE</c>: 1 after an aborted cutscene, 0 otherwise.</summary>
    public const string Override = "var[0x005]";
    /// <summary><c>VAR_TMR_1</c>..<c>VAR_TMR_3</c>: jiffies the frame loop adds every frame.</summary>
    public const string Timer1 = "var[0x00B]";
    public const string Timer2 = "var[0x00C]";
    public const string Timer3 = "var[0x00D]";
    /// <summary><c>VAR_TIMER_NEXT</c>: the jiffies the main loop waits per frame.</summary>
    public const string TimerNext = "var[0x013]";
    /// <summary><c>VAR_CUTSCENEEXIT_KEY</c>: the key that aborts a cutscene (0: none).</summary>
    public const string CutsceneExitKey = "var[0x018]";
    /// <summary><c>VAR_VERB_SCRIPT</c>: the script <c>runInputScript</c> runs for every click or key.</summary>
    public const string VerbScript = "var[0x020]";
    /// <summary><c>VAR_SENTENCE_SCRIPT</c>, <c>VAR_INVENTORY_SCRIPT</c>: the scripts the engine runs for a sentence and for any inventory change (<c>runInventoryScript</c>).</summary>
    public const string SentenceScript = "var[0x021]";
    public const string InventoryScript = "var[0x022]";
    /// <summary><c>VAR_ENTRY_SCRIPT</c>, <c>VAR_ENTRY_SCRIPT2</c>, <c>VAR_EXIT_SCRIPT</c>, <c>VAR_EXIT_SCRIPT2</c>: the global scripts run around every room entry and exit.</summary>
    public const string EntryScript = "var[0x01C]";
    public const string EntryScript2 = "var[0x01D]";
    public const string ExitScript = "var[0x01E]";
    public const string ExitScript2 = "var[0x01F]";
    /// <summary><c>VAR_CUTSCENE_START_SCRIPT</c> and <c>VAR_CUTSCENE_END_SCRIPT</c>.</summary>
    public const string CutsceneStartScript = "var[0x023]";
    public const string CutsceneEndScript = "var[0x024]";
    /// <summary>Version 4 and later: <c>VAR_DEBUGMODE</c> (39), <c>VAR_HEAPSPACE</c> (40) and <c>VAR_FIXEDDISK</c> (51) (vars.cpp <c>setupScummVars</c>).</summary>
    public const string DebugMode = "var[0x027]";
    public const string HeapSpace = "var[0x028]";
    public const string FixedDisk = "var[0x033]";
    /// <summary><c>VAR_RESTART_KEY</c> and <c>VAR_PAUSE_KEY</c>: keys the engine handles itself.</summary>
    public const string RestartKey = "var[0x02A]";
    public const string PauseKey = "var[0x02B]";
    /// <summary>Version 4 and later: <c>VAR_MAINMENU_KEY</c> (50), the key of the engine's save/load menu (vars.cpp <c>setupScummVars</c>; input.cpp <c>processKeyboard</c>).</summary>
    public const string MainMenuKey = "var[0x032]";
    /// <summary>Version 4 and later: <c>VAR_CURSORSTATE</c> (52) and <c>VAR_USERPUT</c> (53), copies of <c>_cursor.state</c> and <c>_userPut</c> after each <c>cursorCommand</c>.</summary>
    public const string CursorState = "var[0x034]";
    public const string UserPut = "var[0x035]";
    /// <summary>
    /// Version 4 and 5: <c>VAR_V5_TALK_STRING_Y</c> (54), the y of a talking actor's text; <c>ScummEngine_v5::resetScummVars</c> sets it to -0x50 (vars.cpp,
    /// ScummVM master read 17 October 2026). The generic session sets it for version 5 only (S4b MonkeyCd); the version 4 games are left as they were.
    /// </summary>
    public const string TalkStringY = "var[0x036]";
    /// <summary>
    /// Version 5 (vars.cpp <c>setupScummVars</c>, <c>_game.version &gt;= 5</c>, read 17 October 2026): <c>VAR_SOUNDRESULT</c> (56), <c>VAR_TALKSTOP_KEY</c> (57),
    /// <c>VAR_FADE_DELAY</c> (59, 3 at start), <c>VAR_NOSUBTITLES</c> (60), <c>VAR_SOUNDPARAM</c>..<c>VAR_SOUNDPARAM3</c> (64 to 66), <c>VAR_INPUTMODE</c> (67: 1 keyboard,
    /// 2 joystick, 3 mouse; 3 at start), <c>VAR_MEMORY_PERFORMANCE</c> (68), <c>VAR_VIDEO_PERFORMANCE</c> (69), <c>VAR_ROOM_FLAG</c> (70), <c>VAR_GAME_LOADED</c> (71),
    /// <c>VAR_NEW_ROOM</c> (72).
    /// </summary>
    public const string SoundResult = "var[0x038]";
    public const string TalkStopKey = "var[0x039]";
    public const string FadeDelay = "var[0x03B]";
    public const string NoSubtitles = "var[0x03C]";
    public const string SoundParam = "var[0x040]";
    public const string SoundParam2 = "var[0x041]";
    public const string SoundParam3 = "var[0x042]";
    public const string InputMode = "var[0x043]";
    public const string MemoryPerformance = "var[0x044]";
    public const string VideoPerformance = "var[0x045]";
    public const string RoomFlag = "var[0x046]";
    public const string GameLoaded = "var[0x047]";
    public const string NewRoom = "var[0x048]";
    /// <summary><c>VAR_MOUSE_X</c>/<c>VAR_MOUSE_Y</c>: the pointer's screen point (<see cref="MouseX"/> is the virtual one in the room).</summary>
    public const string ScreenMouseX = "var[0x02C]";
    public const string ScreenMouseY = "var[0x02D]";
    /// <summary><c>VAR_TIMER</c> (the jiffies of the last frame) and <c>VAR_TIMER_TOTAL</c> (jiffies since the start).</summary>
    public const string Timer = "var[0x02E]";
    public const string TimerTotal = "var[0x02F]";
}

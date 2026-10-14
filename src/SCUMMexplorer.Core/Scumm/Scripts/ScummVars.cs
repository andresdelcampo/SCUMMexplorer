namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The engine-defined global variables of the SCUMM v3-v5 family, as the keys the script runtime uses (ScummVM <c>VAR_*</c>).
/// Games set the rest; a later engine version with another numbering would get its own table chosen by <see cref="ScummProfile"/>.
/// </summary>
public static class ScummVars
{
    /// <summary><c>VAR_RESULT</c>: where <c>getActor*</c>-style opcodes leave their answer.</summary>
    public const string Result = "var[0x000]";
    /// <summary><c>VAR_EGO</c>: the actor the player controls.</summary>
    public const string Ego = "var[0x001]";
    /// <summary><c>VAR_CAMERA_POS_X</c>.</summary>
    public const string CameraPosX = "var[0x002]";
    /// <summary><c>VAR_HAVE_MSG</c>: nonzero while a message is on screen.</summary>
    public const string HaveMsg = "var[0x003]";
    /// <summary><c>VAR_MUSIC_TIMER</c>.</summary>
    public const string MusicTimer = "var[0x00E]";
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
    /// <summary><c>VAR_ROOM</c>: the room the game is in.</summary>
    public const string Room = "var[0x004]";
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
    /// <summary><c>VAR_ENTRY_SCRIPT</c>, <c>VAR_ENTRY_SCRIPT2</c>, <c>VAR_EXIT_SCRIPT</c>, <c>VAR_EXIT_SCRIPT2</c>: the global scripts run around every room entry and exit.</summary>
    public const string EntryScript = "var[0x01C]";
    public const string EntryScript2 = "var[0x01D]";
    public const string ExitScript = "var[0x01E]";
    public const string ExitScript2 = "var[0x01F]";
    /// <summary><c>VAR_CUTSCENE_START_SCRIPT</c> and <c>VAR_CUTSCENE_END_SCRIPT</c>.</summary>
    public const string CutsceneStartScript = "var[0x023]";
    public const string CutsceneEndScript = "var[0x024]";
    /// <summary><c>VAR_RESTART_KEY</c> and <c>VAR_PAUSE_KEY</c>: keys the engine handles itself.</summary>
    public const string RestartKey = "var[0x02A]";
    public const string PauseKey = "var[0x02B]";
    /// <summary><c>VAR_MOUSE_X</c>/<c>VAR_MOUSE_Y</c>: the pointer's screen point (<see cref="MouseX"/> is the virtual one in the room).</summary>
    public const string ScreenMouseX = "var[0x02C]";
    public const string ScreenMouseY = "var[0x02D]";
    /// <summary><c>VAR_TIMER</c> (the jiffies of the last frame) and <c>VAR_TIMER_TOTAL</c> (jiffies since the start).</summary>
    public const string Timer = "var[0x02E]";
    public const string TimerTotal = "var[0x02F]";
}

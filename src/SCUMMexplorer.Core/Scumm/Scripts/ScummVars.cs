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
}

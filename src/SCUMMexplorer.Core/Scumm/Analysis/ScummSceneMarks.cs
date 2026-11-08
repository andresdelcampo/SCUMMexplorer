using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Where a script's scene starts and ends, for every engine version (8 November 2026). <c>cutscene</c> and <c>endCutscene</c>; and in versions 1 and 2,
/// a <c>cursorCommand</c> whose user state hides the whole interface (ScummVM <c>ScummEngine_v2::setUserState</c>, scumm.h <c>USERSTATE_SET_IFACE</c>
/// with none of <c>USERSTATE_IFACE_SENTENCE</c>, <c>_INVENTORY</c>, <c>_VERBS</c>), and one that shows it again. Maniac Mansion takes the player's control
/// so for many of its scenes without a <c>cutscene</c> (Script 107, the Green Tentacle hearing a tape: <c>cursorCommand 0x0F02</c> … <c>0xF701</c>).
/// Only versions 1 and 2 write the command as a literal number; later versions name it (<c>userInputOff</c>), so this reads nothing there.
/// </summary>
public static class ScummSceneMarks
{
    private const int SetInterface = 0x04;
    private const int InterfaceParts = 0xE0;

    /// <summary>The user state a version 1 or 2 <c>cursorCommand</c> sets (its high byte), or null when it is not a literal.</summary>
    public static int? V2UserState(ScummScriptInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        if (instruction.Operation != "cursorCommand") return null;
        var operand = instruction.Operands.Split(',', 2)[0].Trim();
        return operand.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var command)
            ? command >> 8 : null;
    }

    /// <summary>A user state that hides the whole interface: the player's control is taken, as a cutscene does.</summary>
    public static bool HidesInterface(int state) => (state & SetInterface) != 0 && (state & InterfaceParts) == 0;

    /// <summary>A user state that shows the interface again.</summary>
    public static bool ShowsInterface(int state) => (state & SetInterface) != 0 && (state & InterfaceParts) != 0;

    /// <summary>The instruction starts a scene.</summary>
    public static bool Starts(ScummScriptInstruction instruction) =>
        instruction.Operation == "cutscene" || V2UserState(instruction) is int state && HidesInterface(state);

    /// <summary>The instruction ends a scene.</summary>
    public static bool Ends(ScummScriptInstruction instruction) =>
        instruction.Operation == "endCutscene" || V2UserState(instruction) is int state && ShowsInterface(state);
}

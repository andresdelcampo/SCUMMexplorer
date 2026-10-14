namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 (14 October 2026): the display operations the Indiana Jones and the Last Crusade EGA boot reported as "not applied" are applied from
/// ScummVM (script_v5.cpp <c>o5_roomOps</c> case 2, <c>o5_lights</c>, <c>o5_stringOps</c>; read 14 October 2026). Synthetic scripts, no game data.
/// </summary>
public sealed class ScummDisplayOperationTests
{
    private static int _next;
    private static ScummScriptInstruction I(string operation, string operands = "") => new(_next++, 0, operation, operands, "00");

    private static ScummScriptDisassembly Script(params ScummScriptInstruction[] instructions) =>
        new(1, 1, 0, 64, 32, instructions.Select((item, index) => item with { Offset = index }).ToArray(), null, null, [], []);

    private static (ScummBoundedScriptResult Run, ScummScriptRuntimeState Runtime) Run(params ScummScriptInstruction[] code)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>(), new Dictionary<int, byte>());
        runtime.CurrentRoom = 5;
        var run = new ScummBoundedScriptExecutor().Execute(Script(code), runtime.ObjectStates, [], runtime: runtime, onEvent: (_, _) => "");
        return (run, runtime);
    }

    [Fact]
    public void RoomColorLegacyDrawsAColourNumberAsAnotherAndRoomLoadResetsIt()
    {
        var (run, runtime) = Run(I("roomOps", "roomColorLegacy, 0x0000, 0x0006"), I("stopObjectCode"));

        Assert.Null(run.UnresolvedReason);
        Assert.Equal((byte)0, runtime.RoomColors[6]);
        Assert.Equal((byte)7, runtime.RoomColors[7]);
        runtime.ResetRoomColors();
        Assert.Equal((byte)6, runtime.RoomColors[6]);
    }

    [Fact]
    public void LightsSetTheCurrentLightsVariableOrTheFlashlight()
    {
        var (run, runtime) = Run(I("lights", "0x0B, 0x00, 0x00"), I("lights", "0x04, 0x03, 0x01"), I("stopObjectCode"));

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(11, runtime.GlobalVariables[ScummVars.CurrentLights]);
        Assert.Equal((4, 3), runtime.Flashlight);
    }

    [Fact]
    public void StringOperationsKeepAndChangeStringResources()
    {
        var (run, runtime) = Run(
            I("stringOps", "loadString, 0x09, \"@@@@@\""),
            I("stringOps", "setStringChar, 0x09, 0x00, 0x41"),
            I("stringOps", "getStringChar, var[0x035], 0x09, 0x00"),
            I("stringOps", "getStringChar, var[0x036], 0x09, 0x01"),
            I("stopObjectCode"));

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(0x41, runtime.GlobalVariables["var[0x035]"]);
        Assert.Equal((ushort)'@', runtime.GlobalVariables["var[0x036]"]);
    }

    [Fact]
    public void TransitionsResourceRoutinesAndShakeLeaveNothingUnapplied()
    {
        var (run, _) = Run(I("oldRoomEffect", "switchEffect, 0x0081"), I("resourceRoutines", "loadSound, 0x23"),
            I("roomOps", "shakeOn, 0x0000, 0x0000"), I("roomOps", "setScreen, 0x0010, 0x0098"), I("stopObjectCode"));

        Assert.Null(run.UnresolvedReason);
    }
}

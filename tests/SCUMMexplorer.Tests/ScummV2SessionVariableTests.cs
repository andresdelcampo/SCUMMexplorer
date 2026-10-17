using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2SessionVariableTests
{
    [Fact]
    public void V2FrameDoesNotIncrementV3TimersOrOverwriteItsOwnGameSlots()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = new ScummV2DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data, boot: false);
        session.SetGlobalWord(11, 25); // VAR_NUM_ACTOR, not v3 timer 1.
        session.SetGlobalWord(13, 7); // VAR_CURRENTDRIVE, not v3 timer 3.
        session.SetGlobalWord(25, 6); // v2 timer-next.
        session.SetGlobalWord(46, 123);
        session.SetGlobalWord(47, 456);
        session.AdvanceFrame();
        Assert.Equal(6, session.FrameJiffies);
        var variables = session.Snapshot.KnownGlobalVariables;
        Assert.Equal((ushort)25, variables["var[0x00B]"]);
        Assert.Equal((ushort)11, variables[ScummVars.V2.CurrentLights]);
        Assert.Equal((ushort)7, variables["var[0x00D]"]);
        Assert.Equal((ushort)123, variables[ScummVars.Timer]);
        Assert.Equal((ushort)456, variables[ScummVars.TimerTotal]);
        Assert.Equal((ushort)0, variables[ScummVars.SoundCard]);
        Assert.Equal((ushort)0, variables[ScummVars.CharInc]); // v2 LAST_SOUND.
        Assert.Equal((ushort)0, variables[ScummVars.CurrentLights]); // v2 ACTIVE_OBJECT1.
    }

    [Fact]
    public void V2CameraExposesPixelsWhileScriptsReadStripsAndCursorFlagsStaySeparate()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.WriteCameraPixels(320);
        Assert.Equal((ushort)40, runtime.GlobalVariables[ScummVars.CameraPosX]);
        Assert.Equal(320, runtime.ReadCameraPixels());
        Assert.Equal(ScummVars.V2.Ego, runtime.EgoVariable);
        var frozen = new List<bool>();
        runtime.V2FreezeScripts = frozen.Add;
        runtime.ApplyV2UserState(0xF7);
        Assert.Equal(0xE0, runtime.V2UserState);
        Assert.Equal(1, runtime.UserPut);
        runtime.ApplyV2UserState(0x0B);
        Assert.Equal(0xE0, runtime.V2UserState);
        Assert.Equal(0, runtime.UserPut);
        Assert.Equal(new[] { false, true }, frozen);
    }
}

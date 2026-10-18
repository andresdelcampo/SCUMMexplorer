using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2PlayerBoxTests
{
    [Fact]
    public void OldPlayerRangeIsLiveAndClassicManiacUsesEgo()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.GlobalVariables["var[0x02A]"] = 1;
        runtime.GlobalVariables["var[0x02B]"] = 4;
        Assert.True(runtime.IsPlayerActor(1));
        Assert.True(runtime.IsPlayerActor(4));
        Assert.False(runtime.IsPlayerActor(5));
        runtime.GlobalVariables["var[0x02A]"] = 2;
        Assert.False(runtime.IsPlayerActor(1));
        runtime.EngineVersion = ScummVersion.V1;
        runtime.IsClassicManiac = true;
        runtime.GlobalVariables[runtime.EgoVariable] = 3;
        Assert.True(runtime.IsPlayerActor(3));
        Assert.False(runtime.IsPlayerActor(2));
        runtime.EngineVersion = ScummVersion.V3;
        Assert.False(runtime.IsPlayerActor(3));
    }

    [Fact]
    public void ZakPlayerDoesNotEnterTheNpcCounterWithItsSelfLoopMatrix()
    {
        var path = Path.Combine(TestGames.Root, "zakE ega en", "07.LFL");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        for (var index = 0; index < bytes.Length; index++) bytes[index] ^= 0xFF;
        var boxes = ScummWalkBoxReader.ReadV2(bytes);
        Assert.Equal(0xA0, boxes.GetFlags(2));
        Assert.Equal(2, boxes.GetNextBox(2, 0));
        Assert.Equal(1, boxes.Adjust(184, 96, true, 0, 0).Box);
        Assert.Equal(2, boxes.Adjust(184, 96, false, 0, 0).Box);
    }
}

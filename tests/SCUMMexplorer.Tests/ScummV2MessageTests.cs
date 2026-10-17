using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2MessageTests
{
    [Fact]
    public void CharacterCountAccumulatesAcrossWaitChunksAndResetsForTheNextSpeech()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.SetGlobalVariable(ScummVars.V2.CharCount, 99);
        ScummPrintRequest Speech(byte[] text) => new(0xFF, [], text, "Counter regression", 0, 0, null);
        runtime.Messages.Print(Speech([(byte)'A', (byte)'B', 0xFF, 3, (byte)'C', (byte)'D', 0]), runtime);
        Assert.Equal((ushort)2, runtime.GlobalVariables[ScummVars.V2.CharCount]);
        for (var i = 0; i < 6; i++) runtime.Messages.StartFrame(15, runtime);
        Assert.Equal((ushort)4, runtime.GlobalVariables[ScummVars.V2.CharCount]);
        runtime.Messages.Print(Speech([(byte)'E', 0]), runtime);
        Assert.Equal((ushort)1, runtime.GlobalVariables[ScummVars.V2.CharCount]);
    }
}

namespace SCUMMexplorer.Tests;

public sealed class ScummV2PrepositionTests
{
    [Theory]
    [InlineData(ScummVersion.V1)]
    [InlineData(ScummVersion.V2)]
    public void NativeGetAndSetUseHeaderBitsAndPreserveMissingObjectSentinel(ScummVersion version)
    {
        var header = (byte)0x3B; // prep1, walk-Y27; synthetic header, no game-state preparation.
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = version };
        runtime.ObjectPreposition = id => id == 10 ? header >> 5 : 255;
        runtime.SetObjectPreposition = (id, prep) => { if (id == 10) header = (byte)((header & 31) | (prep << 5)); return true; };
        var result = Run(runtime, ("getObjPreposition", "var[0x050], 0x000A"),
            ("setObjPreposition", "0x000A, 0x0A"),
            ("getObjPreposition", "var[0x051], 0x000A"),
            ("setObjPreposition", "0x0063, 0x00"),
            ("getObjPreposition", "var[0x052], 0x0063"));
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(1, result.KnownVariables["var[0x050]"]);
        Assert.Equal(2, result.KnownVariables["var[0x051]"]);
        Assert.Equal(255, result.KnownVariables["var[0x052]"]);
        Assert.Equal(27, header & 31);
    }

    [Fact]
    public void ClassicSentenceResolvesObjectDependentVerbButEnhancedDoesNotSynthesizeIt()
    {
        foreach (var classic in new[] { true, false })
        {
            var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>())
                { EngineVersion = classic ? ScummVersion.V1 : ScummVersion.V2, IsClassicManiac = classic };
            runtime.ApplyV2UserState(0x24);
            runtime.Verbs.CreateV2(11, 0, 0, 0, 255, "Use");
            runtime.GlobalVariables[ScummVars.V2.SentenceVerb] = 11;
            runtime.GlobalVariables[ScummVars.V2.SentenceObject1] = 167;
            runtime.ObjectPreposition = _ => 1;
            var result = Run(runtime, ("drawSentence", ""));
            Assert.Null(result.UnresolvedReason);
            Assert.Equal(classic ? 1 : 0, result.KnownVariables[ScummVars.V2.SentencePreposition]);
            Assert.Equal(classic ? 1 : 0, runtime.V2DrawnSentence!.Value.Preposition);
        }
    }

    [Fact]
    public void UnreadableHeaderDoesNotGuessAPreposition()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2, ObjectPreposition = _ => null };
        Assert.Contains("getObjPreposition", Run(runtime, ("getObjPreposition", "var[0x050], 0x000A")).UnresolvedReason!);
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("mmansionE ega es")]
    public void NativeBatteryHeaderKeepsWalkYSeparateFromItsPreposition(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var record = data.ReadRoom(2).Objects.Single(o => o.Id == 167);
        Assert.Equal(1, record.Preposition);
        Assert.Equal(8, record.WalkY);
    }

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime, params (string Op, string Args)[] ops)
    {
        var instructions = ops.Select((op, i) => new ScummScriptInstruction(i, 0, op.Op, op.Args, "00"))
            .Append(new(ops.Length, 0, "stopObjectCode", "", "00")).ToArray();
        var script = new ScummScriptDisassembly(1, 1, 0, instructions.Length, instructions.Length, instructions, null, null, [], []);
        return new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);
    }
}

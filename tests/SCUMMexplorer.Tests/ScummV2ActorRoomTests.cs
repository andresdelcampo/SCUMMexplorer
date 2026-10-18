namespace SCUMMexplorer.Tests;

public sealed class ScummV2ActorRoomTests
{
    [Theory]
    [InlineData(ScummVersion.V1)]
    [InlineData(ScummVersion.V2)]
    public void NativeRoomSurvivesLeavingItsMappedResourceAndCameraFollowsTheNativeNumber(ScummVersion version)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>())
            { EngineVersion = version, CurrentRoom = 16, CameraFollowStartsScene = true };
        runtime.PseudoRooms[145] = 8;
        runtime.GlobalVariables[ScummVars.Room] = 16;
        var result = Run(runtime, ("putActorInRoom", "0x01, 0x91"),
            ("getActorRoom", "var[0x050], 0x01"), ("actorFollowCamera", "0x01"));
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(8, runtime.Actors[1].Room); // physical room for rendering/walking
        Assert.Equal(145, result.KnownVariables["var[0x050]"]);
        Assert.Contains(result.Events, e => e.Operation == "actorFollowCamera" && e.TargetId == 145);
    }

    [Fact]
    public void NativeRoomChangesBetweenAliasesOfTheSameResourceAndClearsForRoomZero()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.PseudoRooms[144] = runtime.PseudoRooms[145] = 8;
        var result = Run(runtime, ("putActorInRoom", "0x01, 0x91"), ("putActorInRoom", "0x01, 0x90"),
            ("getActorRoom", "var[0x050], 0x01"), ("putActorInRoom", "0x01, 0x00"),
            ("getActorRoom", "var[0x051], 0x01"));
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(144, result.KnownVariables["var[0x050]"]);
        Assert.Equal(0, result.KnownVariables["var[0x051]"]);
    }

    [Fact]
    public void AliasIsPerActorAndCoordinateOnlyPlacementPreservesIt()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.PseudoRooms[144] = runtime.PseudoRooms[145] = 8;
        var result = Run(runtime, ("putActorInRoom", "0x01, 0x91"), ("putActorInRoom", "0x02, 0x90"),
            ("putActor", "0x01, 0x05, 0x06"), ("getActorRoom", "var[0x050], 0x01"),
            ("getActorRoom", "var[0x051], 0x02"));
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(145, result.KnownVariables["var[0x050]"]);
        Assert.Equal(144, result.KnownVariables["var[0x051]"]);
    }

    [Fact]
    public void PhysicalRoomMutationInvalidatesAnOldAliasAndLaterVersionsKeepPriorBehavior()
    {
        foreach (var version in new[] { ScummVersion.V2, ScummVersion.V3 })
        {
            var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = version };
            runtime.PseudoRooms[145] = 8;
            Run(runtime, ("putActorInRoom", "0x01, 0x91"));
            Assert.Equal(version == ScummVersion.V2 ? 145 : 8, runtime.ActorRoomForScript(1));
            runtime.Actors.PutInRoom(1, 3, new(null, null, null, null, null), null);
            Assert.Equal(3, runtime.ActorRoomForScript(1));
        }
    }

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime, params (string Op, string Args)[] ops)
    {
        var instructions = ops.Select((op, i) => new ScummScriptInstruction(i, 0, op.Op, op.Args, "00"))
            .Append(new(ops.Length, 0, "stopObjectCode", "", "00")).ToArray();
        var script = new ScummScriptDisassembly(1, 1, 0, instructions.Length, instructions.Length, instructions, null, null, [], []);
        return new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime,
            onEvent: (_, _) => "");
    }
}

using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomScriptDisassemblerTests
{
    [Fact]
    public void JumpTraversesTargetAndSkipsUnreachableStop()
    {
        var result = LoomScriptDisassembler.Disassemble([0x18, 0x01, 0x00, 0x00, 0x20, 0x00]);

        Assert.Empty(result.Issues);
        Assert.Equal([0, 4, 5], result.Instructions.Select(item => item.Offset));
        Assert.Equal("stopMusic", result.Instructions[1].Operation);
    }

    [Fact]
    public void ConditionalTraversesBothEncodedPaths()
    {
        var result = LoomScriptDisassembler.Disassemble([0x28, 0x00, 0x00, 0x02, 0x00, 0x20, 0x00, 0x80, 0x00]);

        Assert.Empty(result.Issues);
        Assert.Equal([0, 5, 6, 7, 8], result.Instructions.Select(item => item.Offset));
        Assert.Equal("equalZero", result.Instructions[0].Operation);
    }

    [Fact]
    public void BackwardJumpVisitsInstructionOnce()
    {
        var result = LoomScriptDisassembler.Disassemble([0x18, 0xFD, 0xFF]);

        Assert.Empty(result.Issues);
        Assert.Single(result.Instructions);
    }

    [Fact]
    public void OutOfResourceTargetIsReported()
    {
        var result = LoomScriptDisassembler.Disassemble([0x18, 0x04, 0x00]);

        Assert.Single(result.Issues);
        Assert.Equal(7, result.Issues[0].Offset);
    }

    [Theory]
    [InlineData(new byte[] { 0x30, 0x02, 0x05, 0x00 }, "setBoxFlags", 3)]
    [InlineData(new byte[] { 0x5C, 0x03, 0x01, 0x00, 0x00 }, "oldRoomEffect", 4)]
    [InlineData(new byte[] { 0x27, 0x01, 0x04, 0x48, 0x69, 0x00, 0x00 }, "stringOps", 6)]
    [InlineData(new byte[] { 0x54, 0x31, 0x02, 0x48, 0x69, 0x00, 0x00 }, "setObjectName", 6)]
    [InlineData(new byte[] { 0x33, 0x0F, 0x00, 0x0F, 0x00, 0x04, 0x00 }, "roomOps", 6)]
    public void OldV3OperandsEndAtFollowingOpcode(byte[] code, string operation, int nextOffset)
    {
        var result = LoomScriptDisassembler.Disassemble(code);

        Assert.Empty(result.Issues);
        Assert.Equal([0, nextOffset], result.Instructions.Select(item => item.Offset));
        Assert.Equal(operation, result.Instructions[0].Operation);
    }

    [Fact]
    public void StateConditionalTracksTargetAndFallthrough()
    {
        var result = LoomScriptDisassembler.Disassemble([0x2F, 0x0A, 0x00, 0x01, 0x01, 0x00, 0x20, 0x00]);

        Assert.Empty(result.Issues);
        Assert.Equal([0, 6, 7], result.Instructions.Select(item => item.Offset));
        Assert.Equal("ifNotState", result.Instructions[0].Operation);
    }

    [Theory]
    [InlineData(new byte[] { 0x25, 0x0A, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00 }, "0x000A, at, 0x0002, var[0x003]")]
    [InlineData(new byte[] { 0x05, 0x0A, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00 }, "0x000A, at, 0x0002, 0x0003")]
    [InlineData(new byte[] { 0x45, 0x0A, 0x00, 0x04, 0x00, 0x03, 0x00, 0x00 }, "0x000A, at, var[0x004], 0x0003")]
    public void SmallHeaderDrawObjectUsesOpcodeWordOperands(byte[] code, string operands)
    {
        var result = LoomScriptDisassembler.Disassemble(code);

        Assert.Empty(result.Issues);
        Assert.Equal("drawObject", result.Instructions[0].Operation);
        Assert.Equal(operands, result.Instructions[0].Operands);
        Assert.Equal(code.Length - 1, result.Instructions[1].Offset);
    }


}

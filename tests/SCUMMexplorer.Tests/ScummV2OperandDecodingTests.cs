using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2OperandDecodingTests
{
    [Theory]
    [InlineData(ScummScriptDialect.V1)]
    [InlineData(ScummScriptDialect.V2)]
    public void OverrideDecodesNormalAndSkippedPaths(ScummScriptDialect dialect)
    {
        // Normal path stops at4. The embedded jump reaches6 only when the player skips.
        var result = ScummScriptDisassembler.Disassemble([0x58, 0x18, 2, 0, 0, 0xFF, 0x80, 0], dialect);
        Assert.Empty(result.Issues);
        Assert.Equal([0, 4, 6, 7], result.Instructions.Select(i => i.Offset));
        Assert.Equal("beginOverride", result.Instructions[0].Operation);
        Assert.Equal("breakHere", result.Instructions[2].Operation);
    }

    [Theory]
    [InlineData(ScummScriptDialect.V1)]
    [InlineData(ScummScriptDialect.V2)]
    public void ActorNameConsumesInlineTextBeforeNextInstruction(ScummScriptDialect dialect)
    {
        byte[] code = [0x13, 3, 0, 3, .. "Dave"u8.ToArray(), 0, 0x80, 0];
        var result = ScummScriptDisassembler.Disassemble(code, dialect);
        Assert.Empty(result.Issues);
        Assert.Equal([0, 9, 10], result.Instructions.Select(i => i.Offset));
        Assert.Equal("0x03, 0x00, sub=0x03, name=\"Dave\"", result.Instructions[0].Operands);
        Assert.Equal("breakHere", result.Instructions[1].Operation);
    }

    [Fact]
    public void EnhancedPaletteConsumesIndexButClassicLeavesNextOpcode()
    {
        var enhanced = ScummScriptDisassembler.Disassemble([0x13, 3, 14, 2, 5, 0x80, 0], ScummScriptDialect.V2);
        var classic = ScummScriptDisassembler.Disassemble([0x13, 3, 14, 2, 0x80, 0], ScummScriptDialect.V1);
        Assert.Empty(enhanced.Issues);
        Assert.Empty(classic.Issues);
        Assert.Equal([0, 5, 6], enhanced.Instructions.Select(i => i.Offset));
        Assert.Equal([0, 4, 5], classic.Instructions.Select(i => i.Offset));
        Assert.Equal("0x03, 0x0E, sub=0x02, palette=0x05", enhanced.Instructions[0].Operands);
        Assert.Equal("0x03, 0x0E, sub=0x02", classic.Instructions[0].Operands);
    }

    [Theory]
    [InlineData(0, 0, 0, "16777215")]
    [InlineData(255, 255, 255, "0")]
    [InlineData(254, 255, 255, "1")]
    [InlineData(0xA9, 0xCB, 0xED, "1193046")]
    public void DelayDecodesComplementOfStored24BitValue(byte a, byte b, byte c, string expected)
    {
        foreach (var dialect in new[] { ScummScriptDialect.V1, ScummScriptDialect.V2 })
        {
            var result = ScummScriptDisassembler.Disassemble([0x2E, a, b, c, 0], dialect);
            Assert.Empty(result.Issues);
            Assert.Equal(expected, result.Instructions[0].Operands);
            Assert.Equal(4, result.Instructions[1].Offset);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x13, 3, 0, 3, 68 })]
    [InlineData(new byte[] { 0x13, 3, 14, 2 })]
    public void MissingInlineOperandRemainsExplicitlyTruncated(byte[] code)
    {
        var result = ScummScriptDisassembler.Disassemble(code, ScummScriptDialect.V2);
        Assert.Contains(result.Issues, i => i.Offset == 0 && i.Reason.Contains("truncated"));
    }

    [Theory]
    [InlineData("mmansion ega en", ScummScriptDialect.V1, 174, 995)]
    [InlineData("mmansionE ega es", ScummScriptDialect.V2, 221, 1207)]
    public void ManiacBootContinuesPastActorNames(string folder, ScummScriptDialect dialect, int instructions, int bytes)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        Assert.Equal(dialect, ScummProfile.For(game).Dialect);
        var resource = game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == 1);
        var result = new ScummScriptReader(dialect).Decode(resource);
        Assert.Contains(result.Instructions, i => i.Operation == "actorOps" && i.Operands.Contains("name="));
        Assert.Empty(result.Issues!);
        Assert.Equal(instructions, result.Instructions.Count);
        Assert.Equal(bytes, result.DecodedBytes);
        Assert.Equal(result.CodeLength, result.DecodedBytes);
    }
}

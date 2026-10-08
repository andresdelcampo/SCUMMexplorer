using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomScriptPresenterTests
{
    [Fact]
    public void SaveLoadVariableCommandsKeepTheirFilenameAndRangesInReadableOutput()
    {
        var text = Present([0xA7, 0x02, 0x03, .. "savegame.___"u8.ToArray(), 0x00, 0x02, 0x09, 0x14, 0xFF, 0xA0]);

        Assert.Contains("load variables (openFile, \"savegame.___\"; strings, 0x09, 0x14; closeFile)", text);
        Assert.DoesNotContain("unresolved", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string Present(byte[] code)
    {
        var decoded = LoomScriptDisassembler.Disassemble(code);
        Assert.Empty(decoded.Issues);
        var script = new LoomScriptDisassembly(1, 1, 0, code.Length, code.Length, decoded.Instructions, null, null, [], []);
        return new LoomScriptPresenter(LoomGlossary.Default).Present(script);
    }

    [Fact]
    public void ForwardBranchWithAJumpBecomesIfElse()
    {
        // notEqualZero bit[0xD61] jumps when the bit is clear, so the body runs while the staff is held.
        var text = Present([0xA8, 0x61, 0x8D, 0x04, 0x00, 0x80, 0x18, 0x01, 0x00, 0x80, 0x00]);
        var lines = text.Split(Environment.NewLine);
        Assert.Contains(lines, l => l.StartsWith("0000") && l.EndsWith("if the distaff (staff) is held"));
        Assert.Contains(lines, l => l.TrimEnd() == "          else" || l.Trim() == "else");
        Assert.Contains(lines, l => l.Trim() == "end if");
        Assert.Contains("[decoded]", text);
    }

    [Fact]
    public void BackwardJumpToTheConditionBecomesWhile()
    {
        var text = Present([0xA8, 0x61, 0x8D, 0x04, 0x00, 0x80, 0x18, 0xF7, 0xFF, 0x00]);
        Assert.Contains("while the distaff (staff) is held", text);
        Assert.Contains("end while", text);
    }

    [Fact]
    public void ConditionsAreReadWithScummVmsJumpIfFalseSemantics()
    {
        // isGreater var[0x002], 464: ScummVM jumps unless (464 > var), so the body runs while camera x < 464.
        var text = Present([0x78, 0x02, 0x00, 0xD0, 0x01, 0x02, 0x00, 0x80, 0x80, 0x00]);
        Assert.Contains("if camera x < 464", text);
    }

    [Fact]
    public void UnknownThingsKeepTheirRawForm()
    {
        // move var[0x0D1] = 5 has no name; bit[0xD96] = 1 is shown raw too.
        var text = Present([0x1A, 0xD1, 0x00, 0x05, 0x00, 0x1A, 0x96, 0x8D, 0x01, 0x00, 0x00]);
        Assert.Contains("var[0x0D1] = 5", text);
        Assert.Contains("set: bit[0xD96]", text);
    }

    [Fact]
    public void NamedArraysUseTheirEstablishedBaseAndLeaveUnknownOffsetsRaw()
    {
        var instructions = new[]
        {
            new LoomScriptInstruction(0, 0, "move", "var[0x032+local[0x000]] = 0x0001", ""),
            new LoomScriptInstruction(1, 0, "move", "var[0x033+local[0x000]] = 0x0001", ""),
            new LoomScriptInstruction(2, 0, "move", "var[0x0D1+var[0x002]] = 0x0001", "")
        };
        var script = new LoomScriptDisassembly(1, 1, 0, 3, 3, instructions, null, null, [], []);
        var text = new LoomScriptPresenter(LoomGlossary.Default).Present(script);
        Assert.Contains("draft table (pattern words)[local 0] = 1", text);
        Assert.Contains("var[0x033+local[0x000]] = 1", text);
        Assert.Contains("var[0x0D1+var[0x002]] = 1", text);
    }

    [Fact]
    public void PrintNamesTheSpeakerAndStartScriptNamesTheScript()
    {
        // print actor 1 [text "Hi"], startScript 17 [0]
        byte[] code = [0x14, 0x01, 0x0F, (byte)'H', (byte)'i', 0x00, 0x0A, 0x11, 0x00, 0x00, 0x00, 0xFF, 0x00];
        var text = Present(code);
        Assert.Contains("Bobbin says: “Hi”", text);
        Assert.Contains("start Script 17 · give the ego a costume (by size)", text);
    }

    // Golden outputs for a few real scripts; written when missing or when SCUMM_UPDATE_GOLDEN=1, then reviewed by hand.
    [Theory]
    [InlineData(91)]
    [InlineData(17)]
    [InlineData(6)]
    public void RealScriptsMatchTheirGoldenOutput(int scriptId)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var game = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative)));
        var glossary = LoomGlossary.Load(game);
        var script = new LoomScriptReader().Decode(game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == scriptId));
        var text = new LoomScriptPresenter(glossary).Present(script);
        // One golden per edition: the text, the offsets and the bit numbers differ between the releases.
        var edition = Path.GetFileName(game.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var path = Path.Combine(root, ".verification", "readable-scripts", edition, $"script-{scriptId:000}.txt");
        if (!File.Exists(path) || Environment.GetEnvironmentVariable("SCUMM_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        Assert.Equal(File.ReadAllText(path), text);
    }

    [Theory]
    [InlineData("roomOps", "roomScroll, 0x0098, 0x00A0", "camera limits: x from 152 to 160")]
    [InlineData("roomOps", "setScreen, 0x0000, 0x0090", "set the screen to rows 0 to 144")]
    [InlineData("roomOps", "shakeOn, 0x0000, 0x0000", "the screen starts shaking")]
    [InlineData("stringOps", "loadString, 0x04, \"Pausa.\"", "string 4 = “Pausa.”")]
    [InlineData("stringOps", "getStringChar, local[0x007], local[0x005], local[0x006]", "local 7 = character local 6 of string local 5")]
    [InlineData("doSentence", "0xFE, 0x0000, 0x0000", "cancel the pending sentence")]
    [InlineData("matrixOps", "setBoxScale, 0x02, 0x80", "walk box 2: scale 128")]
    [InlineData("matrixOps", "createBoxMatrix", "rebuild the walk-box connections")]
    [InlineData("oldRoomEffect", "switchEffect, 0x81", "room transition effect 129")]
    [InlineData("lights", "0x0B, 0x00, 0x00", "set lighting (11, 0, 0)")]
    [InlineData("systemOps", "restart", "restart the game")]
    [InlineData("setObjectName", "0x9A, \"leaf\"", "rename object 154 to “leaf”")]
    [InlineData("print", "0x0B, [sayVoice, 0x1BD4, 0x0014]", "play CD audio from disc second 712.4 for 2 s")]
    public void RoomStringAndSentenceOperationsGetPlainPhrases(string operation, string operands, string expected)
    {
        var instructions = new[] { new LoomScriptInstruction(0, 0, operation, operands, "") };
        var script = new LoomScriptDisassembly(1, 1, 0, 1, 1, instructions, null, null, [], []);
        var text = new LoomScriptPresenter(LoomGlossary.Default).Present(script);
        Assert.Contains(expected, text);
        Assert.DoesNotContain("[raw]", text);
    }
}

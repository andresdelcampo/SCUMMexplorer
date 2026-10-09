using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomTextExtractorTests
{
    [Fact]
    public void GroupsNestedCutsceneLinesInSourceOrderAndKeepsSpeakers()
    {
        var extraction = Extract([
            0x40, 0xFF,
            0xD8, 0x0F, (byte)'A', (byte)'1', 0,
            0x40, 0xFF,
            0x14, 0x02, 0x0F, (byte)'B', (byte)'2', 0,
            0xC0,
            0xD8, 0x0F, (byte)'C', (byte)'3', 0,
            0xC0,
            0x00
        ]);

        var lines = extraction.Entries.Where(entry => entry.ConversationId is not null)
            .OrderBy(entry => entry.ConversationLine).ToArray();

        Assert.Equal(3, lines.Length);
        Assert.All(lines, entry => Assert.Equal(lines[0].ConversationId, entry.ConversationId));
        Assert.Equal(new int?[] { 1, 2, 3 }, lines.Select(entry => entry.ConversationLine));
        Assert.Equal(["Ego", "Actor 2", "Ego"], lines.Select(entry => entry.Speaker));
        Assert.Equal(["A1", "B2", "C3"], lines.Select(entry => entry.Text));
    }

    [Fact]
    public void GroupsUnmarkedDialogueSeparatelyBeforeAndAfterCutsceneMarkers()
    {
        var code = new List<byte>();
        AddEgoLine(code, "Before one");
        AddEgoLine(code, "Before two");
        code.AddRange([0x40, 0xFF]);
        AddEgoLine(code, "Marked");
        code.Add(0xC0);
        AddEgoLine(code, "After one");
        AddEgoLine(code, "After two");
        code.Add(0x00);

        var entries = Extract(code.ToArray()).Entries.Where(entry => entry.Kind == "Spoken line").ToArray();
        var before = entries.Where(entry => entry.Text.StartsWith("Before", StringComparison.Ordinal)).ToArray();
        var marked = Assert.Single(entries, entry => entry.Text == "Marked");
        var after = entries.Where(entry => entry.Text.StartsWith("After", StringComparison.Ordinal)).ToArray();

        Assert.Equal(2, before.Length);
        Assert.All(before, entry => Assert.Equal("Script dialogue", entry.ConversationKind));
        Assert.Single(before.Select(entry => entry.ConversationId).Distinct());
        Assert.Equal("Cutscene", marked.ConversationKind);
        Assert.NotEqual(before[0].ConversationId, after[0].ConversationId);
        Assert.All(after, entry => Assert.Equal("Script dialogue", entry.ConversationKind));
    }

    [Fact]
    public void LeavesSingleUnmarkedDialogueLineUngrouped()
    {
        var code = new List<byte>();
        AddEgoLine(code, "One line");
        code.Add(0x00);

        var line = Assert.Single(Extract(code.ToArray()).Entries, entry => entry.Text == "One line");

        Assert.Null(line.ConversationId);
        Assert.Null(line.ConversationKind);
    }

    [Fact]
    public void ExcludesBlankSingleCharacterAndControlOnlyStringsAndKeepsObjectTargetsExplicit()
    {
        var code = new List<byte>
        {
            0x54, 0xA6, 0x00, (byte)'T', (byte)'a', (byte)'r', (byte)'g', (byte)'e', (byte)'t', 0,
            0xD4, 0x01, 0x00, (byte)'D', (byte)'y', (byte)'n', (byte)'a', (byte)'m', (byte)'i', (byte)'c', 0,
            0xD8, 0x0F, 0,
            0xD8, 0x0F, (byte)' ', 0,
            0xD8, 0x0F, (byte)'!', 0,
            0xD8, 0x0F, (byte)'I', 0,
            0xD8, 0x0F, 0xFF, 0x03, 0,
            0x00
        };

        var entries = Extract(code.ToArray()).Entries;
        var literalTarget = Assert.Single(entries, entry => entry.Text == "Target");
        var dynamicTarget = Assert.Single(entries, entry => entry.Text == "Dynamic");

        Assert.Equal(166, literalTarget.ObjectId);
        Assert.Null(dynamicTarget.ObjectId);
        Assert.DoesNotContain(entries, entry => entry.Text is "" or " " or "!" or "I" or "[pause]");
    }

    [Fact]
    public void ExtractsLiteralVerbMenuNamesAsChoiceOptionsOutsideSpokenTranscripts()
    {
        var code = new List<byte> { 0x7A, 0x01, 0x02 };
        code.AddRange(System.Text.Encoding.ASCII.GetBytes("Ask about Marcus"));
        code.AddRange([0x00, 0x06, 0xFF, 0x00]);

        var choice = Assert.Single(Extract(code.ToArray()).Entries);

        Assert.Equal("Ask about Marcus", choice.Text);
        Assert.Equal("Verb label", choice.Kind);
        Assert.Null(choice.ConversationId);
        Assert.Null(choice.ConversationKind);
    }

    [Fact]
    public void ExtractsKnownIndyEgaChoiceStringsFromTheSuppliedGameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        var entries = new LoomTextExtractor().Extract(game).Entries;
        var choices = entries.Where(entry => entry.Kind == "Verb label").ToArray();

        // The supplied fixture may be the English or Spanish EGA edition.
        // Keep exact paired strings so a decoder regression cannot pass merely
        // by finding arbitrary menu labels in either language.
        var spanish = choices.Any(entry => entry.Text == "Perdon, pero tengo que irme.");
        var excuseText = spanish ? "Perdon, pero tengo que irme." : "Excuse me, but I've got to be going.";
        var kidsText = spanish ? "¡ Apartaros de mi camino, chicos !" : "Out of my way, kids!";
        Assert.Contains(choices, entry => entry.Text == excuseText);
        Assert.Contains(choices, entry => entry.Text == kidsText);
        var excuse = choices.First(entry => entry.Text == excuseText);
        Assert.Equal(LoomTextExtractor.ChoiceConversationKind, excuse.ConversationKind);
        Assert.True(excuse.IsPlayerChoice);

        // Indy3 wait/waitForActor/getActorX/Y operand rules keep dialogue
        // scripts aligned, so most choice sets and their replies are reached.
        var playerChoices = entries.Where(entry => entry.IsPlayerChoice).Select(entry => entry.Text).Distinct().Count();
        Assert.True(playerChoices > 150, $"Only {playerChoices} player choices were extracted.");
        Assert.Contains(entries, entry => entry.ConversationKind == LoomTextExtractor.ChoiceConversationKind &&
                                          entry.Kind == "Spoken line" && entry.ChoiceVerbId.HasValue);
    }

    [Fact]
    public void GroupsDialogueChoicesWithTheRepliesInTheirSelectionBranches()
    {
        var code = new List<byte>();
        void Label(byte verb, string text)
        {
            code.AddRange([0x7A, verb, 0x02]);
            code.AddRange(System.Text.Encoding.ASCII.GetBytes(text));
            code.AddRange([0x00, 0xFF]);
        }
        Label(0x78, "Hello there");
        Label(0x79, "Goodbye now");
        var reply = new List<byte> { 0x14, 0x0A, 0x0F };
        reply.AddRange(System.Text.Encoding.ASCII.GetBytes("Nice to meet you"));
        reply.Add(0x00);
        // isEqual var[0x072], 0x0078: fall through into the reply when equal.
        code.AddRange([0x48, 0x72, 0x00, 0x78, 0x00, (byte)reply.Count, 0x00]);
        code.AddRange(reply);
        code.Add(0x00);

        var entries = Extract(code.ToArray()).Entries;
        var hello = Assert.Single(entries, entry => entry.Text == "Hello there");
        var goodbye = Assert.Single(entries, entry => entry.Text == "Goodbye now");
        var answer = Assert.Single(entries, entry => entry.Text == "Nice to meet you");

        Assert.True(hello.IsPlayerChoice);
        Assert.Equal(0x78, hello.ChoiceVerbId);
        Assert.Equal(hello.ConversationId, goodbye.ConversationId);
        Assert.Equal(hello.ConversationId, answer.ConversationId);
        Assert.Equal(0x78, answer.ChoiceVerbId);
        Assert.False(answer.IsPlayerChoice);
        Assert.True(answer.ConversationLine > hello.ConversationLine);
        Assert.True(answer.ConversationLine < goodbye.ConversationLine);
    }

    private static LoomTextExtraction Extract(byte[] code)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-text-extractor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "00.LFL");
            var plain = new byte[4 + code.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(plain, (ushort)plain.Length);
            code.CopyTo(plain, 4);
            File.WriteAllBytes(path, plain.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var script = new GameResource("Script 01", ResourceKind.Script, path, 1,
                ContainerFileNumber: 1, Offset: 0);
            var game = new GameLibrary("LOOM", directory, ScummVersion.V3, "DOS", [script], "test");
            return new LoomTextExtractor().Extract(game);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void AddEgoLine(List<byte> code, string text)
    {
        code.AddRange([0xD8, 0x0F]);
        code.AddRange(System.Text.Encoding.ASCII.GetBytes(text));
        code.Add(0);
    }
}

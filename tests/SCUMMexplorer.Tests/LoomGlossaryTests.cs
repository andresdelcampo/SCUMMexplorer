using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomGlossaryTests
{
    [Fact]
    public void NamesCarryTheirEvidenceAndRangesAreCovered()
    {
        var glossary = LoomGlossary.Default;
        var staff = glossary.Bit(0xD61);
        Assert.NotNull(staff);
        Assert.Equal(GlossaryEvidence.Decoded, staff!.Evidence);
        Assert.Contains("staff", staff.Name);
        Assert.Equal("engine", glossary.Variable(0x002)!.EvidenceLabel);
        Assert.Equal("draft table (pattern words)", glossary.Variable(0x040)!.Name);
        Assert.Equal("ego size", glossary.Variable(0x07C)!.Name);
        Assert.Equal(GlossaryEvidence.Reading, glossary.Bit(0xDA5)!.Evidence);
    }

    [Fact]
    public void BitNamesFollowTheEditionsBitNumbering()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        int shift = LoomStoryEvents.FirstBitFor(game) - LoomStoryEvents.FirstBit;
        var glossary = LoomGlossary.Load(game);
        // The staff bit is below the shifted range; "a cutscene is running" is the Spanish release's 0xD6E.
        Assert.Contains("staff", glossary.Bit(0xD61)!.Name);
        var cutscene = glossary.Bit(0xD6E + shift);
        Assert.Equal("a cutscene is running", cutscene!.Name);
        if (shift != 0) Assert.Equal(GlossaryEvidence.Reading, cutscene.Evidence);
    }

    [Fact]
    public void UnknownThingsStayUnnamed()
    {
        var glossary = LoomGlossary.Default;
        Assert.Null(glossary.Variable(0x0D1));
        Assert.Null(glossary.Bit(0xD96));
        Assert.Null(glossary.Script(77));
        Assert.Null(glossary.Object(2, 154));
    }

    [Fact]
    public void StoryBitLabelsFillOnlyMissingBitsAndAreMarkedAsReading()
    {
        var glossary = LoomGlossary.Default;
        var custom = LoomGlossary.CreateNew();
        custom.StoryBitLabel = bit => bit == 0xD96 ? "cage scene happened" : null;
        Assert.Equal("cage scene happened", custom.Bit(0xD96)!.Name);
        Assert.Equal(GlossaryEvidence.Reading, custom.Bit(0xD96)!.Evidence);
        Assert.Contains("staff", custom.Bit(0xD61)!.Name);
        Assert.Null(custom.Bit(0xD97));
        Assert.Null(glossary.Bit(0xD96));
    }

    [Fact]
    public void ObjectNamesComeFromTheGameData()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var glossary = LoomGlossary.Load(new GameFolderScanner().Scan(folder));
        Assert.Equal("hoja", glossary.Object(2, 154)!.Name);
        Assert.Null(glossary.Object(9, 154));
        Assert.Equal(2, glossary.ObjectRoom(154));
        Assert.Contains("cage", glossary.Script(91)!.Name);
    }

    [Fact]
    public void ActorNamesAreScopedBecauseNumbersAreReused()
    {
        var glossary = LoomGlossary.Default;
        Assert.Equal("Elder (actor 2)", glossary.Actor(2)!.Name);
        Assert.Equal("Elder (actor 2)", glossary.Actor(2, "script:91")!.Name);
        Assert.Equal("flying actor of Room 03", glossary.Actor(2, "script:42")!.Name);
        Assert.Equal("fairy", glossary.Actor(3, "script:44")!.Name);
    }
}

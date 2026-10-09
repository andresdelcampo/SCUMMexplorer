using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomEgaScriptReviewTests
{
    [Theory]
    [InlineData("en", 203, 21)]
    [InlineData("en", 204, 72)]
    [InlineData("es", 203, 21)]
    [InlineData("es", 204, 72)]
    public void Room70SaveNameHelpersDecodeThroughTheirRealEnd(string language, int id, int length)
    {
        var data = ScummDataSources.For(Game(language));
        var script = Assert.IsType<ScummScriptDisassembly>(data.RoomLocalScript(70, id));

        Assert.Equal(length, script.CodeLength);
        Assert.True(script.HasCompleteControlFlow);
        Assert.Equal(length, script.DecodedBytes);
        Assert.Equal(length - 1, script.Instructions[^1].Offset);
        Assert.Equal("stopObjectCode", script.Instructions[^1].Operation);
        var command = Assert.Single(script.Instructions, i => i.Operation == "saveLoadVars");
        Assert.Contains("\"savegame.___\"", command.Operands);
        Assert.Contains("strings, 0x09, 0x14; closeFile", command.Operands);
    }

    private static SCUMMexplorer.Core.Domain.GameLibrary Game(string language)
    {
        var games = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games"));
        var folder = Path.Combine(games, "loom ega mt32 " + language);
        if (!Directory.Exists(folder)) folder = Path.Combine(games, "ALTERNATIVE LOOMS", "loom ega mt32 " + language);
        return new GameFolderScanner().Scan(folder);
    }

    [Theory]
    [InlineData("en", 115)]
    [InlineData("es", 113)]
    public void EveryStandaloneCompleteEgaGlobalHasItsOwnReviewedTitle(string language, int expected)
    {
        var game = Game(language); var data = ScummDataSources.For(game); var glossary = LoomGlossary.Load(game);
        var valid = new List<int>();
        var chain = new LflResourceChain();
        foreach (var id in data.GlobalScriptIds())
        {
            var resource = game.Resources.Single(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && r.ResourceId == id);
            if (chain.FindLeftover(game, resource) is not null) { Assert.Null(glossary.Script(id)); continue; }
            ScummScriptDisassembly? code;
            try { code = data.GlobalScript(id); }
            catch (InvalidDataException) { Assert.Null(glossary.Script(id)); continue; }
            if (code is not { HasCompleteControlFlow: true }) { Assert.Null(glossary.Script(id)); continue; }
            valid.Add(id);
            var title = Assert.IsType<GlossaryEntry>(glossary.Script(id));
            Assert.Contains($"EGA Script {id:00}", title.Source);
            Assert.Contains(language == "en" ? "English EGA" : "Spanish EGA", title.Source);
            Assert.Contains(title.Name, new LoomScriptPresenter(glossary).Present(code));
        }
        Assert.Equal(expected, valid.Count);
        Assert.Null(glossary.Script(0));
        Assert.Null(glossary.Script(200));
        Assert.Equal("check whether draft N is learned", glossary.Script(24)!.Name);
        Assert.Equal("restore an actor's palette", glossary.Script(33)!.Name);
    }

    [Fact]
    public void AnInteriorIndexEntryGetsNoReviewedTitleEvenIfItsBytesDecodeCompletely()
    {
        var game = Game("en");
        var resource = game.Resources.Single(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && r.ResourceId == 116);
        var leftover = Assert.IsType<LflLeftoverIndexEntry>(new LflResourceChain().FindLeftover(game, resource));

        Assert.Equal(114, leftover.Owner!.ResourceId);
        Assert.Equal(48, resource.Offset!.Value - leftover.Container.Start);
        Assert.True(ScummDataSources.For(game).GlobalScript(116)!.HasCompleteControlFlow);
        Assert.Null(LoomGlossary.Load(game).Script(116));
    }

    [Theory]
    [InlineData("en", 0xD68, 0xD66)]
    [InlineData("es", 0xD69, 0xD67)]
    public void DraftFlagsFollowActualEditionOperands(string language, int learned, int playing)
    {
        var game = Game(language); var data = ScummDataSources.For(game); var glossary = LoomGlossary.Load(game);
        string Show(int id) => new LoomScriptPresenter(glossary).Present(data.GlobalScript(id)!);
        Assert.Equal("the current draft is learned", glossary.Bit(learned)!.Name);
        Assert.Equal(GlossaryEvidence.Decoded, glossary.Bit(learned)!.Evidence);
        Assert.Contains("mask 0x2000", glossary.Bit(learned)!.Source);
        Assert.Contains("the current draft is learned", Show(24));
        Assert.Equal("a note is playing", glossary.Bit(playing)!.Name);
        Assert.Equal("the draft response was refused or consumed", glossary.Bit(playing + 1)!.Name);
        Assert.Contains("the draft response was refused or consumed", Show(26));
        Assert.Contains("a note is playing", Show(31));
        Assert.Contains("current draft response word", Show(26));
        Assert.Contains("sound currently playing", Show(38));
        Assert.Contains("pointer x", Show(6));
        Assert.Contains("ego talk-stop animation", Show(17));
        Assert.Contains("flying actor x positions[local 2]", Show(42));
        Assert.Equal("flying actor x positions", glossary.Variable(0x0B7)!.Name);
        Assert.Null(glossary.Variable(0x0B8)); // Only five positions were initialized.
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void SceneRolesAndEgaSpecificHelpersKeepTheirOwnMeanings(string language)
    {
        var game = Game(language); var data = ScummDataSources.For(game); var glossary = LoomGlossary.Load(game);
        foreach (var actor in Enumerable.Range(2, 5)) Assert.Null(glossary.Actor(actor));
        Assert.Null(glossary.Actor(12));
        Assert.Equal("learning glow", glossary.Actor(12, "script:29")!.Name);
        Assert.Equal("Fleece", glossary.Actor(2, "script:69")!.Name);
        Assert.Equal("dragon", glossary.Actor(2, "script:75")!.Name);
        Assert.Equal("Mandible", glossary.Actor(2, "script:90")!.Name);
        Assert.Equal("Master Goodmold", glossary.Actor(6, "script:67")!.Name);
        Assert.Equal("Glassmaker", glossary.Actor(6, "script:108")!.Name);
        Assert.Equal("Mr. Stoke", glossary.Actor(5, "script:87")!.Name);
        Assert.Equal("Hetchel", glossary.Actor(11, "script:88")!.Name);
        Assert.Equal("Cygna (Bobbin's mother)", glossary.Actor(3, "script:100")!.Name);
        Assert.Contains("save screen", glossary.Script(4)!.Name);
        Assert.Contains("music", glossary.Script(39)!.Name);
        Assert.Equal("save-screen input hook", glossary.Script(115)!.Name);
        Assert.Contains("Mandible's introduction", glossary.Script(90)!.Name);
        Assert.Contains("Goodmold's aftermath", glossary.Script(99)!.Name);
        Assert.Contains("spell visual", glossary.Script(111)!.Name);
        var seven = new LoomScriptPresenter(glossary).Present(data.GlobalScript(7)!);
        Assert.Contains(language == "en" ? "if local 2 < 2" : "if local 2 < 3", seven); // Spanish EGA and CD use 3; English EGA uses 2.
        if (language == "es") { Assert.Null(glossary.Script(51)); Assert.Null(glossary.Script(55)); Assert.Null(glossary.Script(116)); }
    }

    [Fact]
    public void AChangedEgaInstructionStreamDoesNotReceiveAnImportedTitle()
    {
        var game = Game("en"); var data = ScummDataSources.For(game);
        var original = game.Resources.Single(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && r.ResourceId == 24);
        var script = data.GlobalScript(24)!;
        var bytes = File.ReadAllBytes(original.FilePath);
        var and = script.Instructions.Single(i => i.Operation == "and");
        bytes[(script.CodeFileOffset ?? script.ResourceOffset + 4) + and.Offset + 3] ^= 1; // Change the 0x2000 mask.
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".LFL");
        try
        {
            File.WriteAllBytes(path, bytes);
            var altered = game with { Resources = game.Resources.Select(r => r == original ? r with { FilePath = path } : r).ToArray() };
            Assert.Null(LoomGlossary.Load(altered).Script(24));
            Assert.NotNull(LoomGlossary.Load(game).Script(24));
        }
        finally { File.Delete(path); }
    }
}

using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomCdScriptReviewTests
{
    private static SCUMMexplorer.Core.Domain.GameLibrary Game() => new GameFolderScanner().Scan(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom vga en")));

    [Fact]
    public void AllCdGlobalsHaveAnIndividualReviewAndTheirOwnEvidence()
    {
        var game = Game(); var data = ScummDataSources.For(game); var glossary = LoomGlossary.Load(game);
        using var stream = typeof(LoomGlossary).Assembly.GetManifestResourceStream("SCUMMexplorer.Core.loom-cd-glossary.json")!;
        using var review = JsonDocument.Parse(stream);
        Assert.Equal(data.GlobalScriptIds().Order(), review.RootElement.EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Order());
        foreach (var id in data.GlobalScriptIds())
        {
            var name = Assert.IsType<GlossaryEntry>(glossary.Script(id));
            Assert.Contains($"CD Script {id:00}", name.Source);
            Assert.Contains(name.Name, new LoomScriptPresenter(glossary).Present(data.GlobalScript(id)!));
        }
        Assert.Equal("input script", glossary.Script(5)!.Name);
        Assert.Equal("run a sentence (verb on an object)", glossary.Script(7)!.Name);
        Assert.Contains("stub", glossary.Script(4)!.Name);
        Assert.Contains("stub", glossary.Script(39)!.Name);
        Assert.Contains("CD effect", glossary.Script(40)!.Name);
        Assert.Contains("Mandible", glossary.Script(90)!.Name);
        Assert.Null(glossary.Script(51)); Assert.Null(glossary.Script(116)); Assert.Null(glossary.Script(200));
    }

    [Fact]
    public void ScreenshotScriptsHaveReadableOperandsAndSceneActorsStayScoped()
    {
        var game = Game(); var data = ScummDataSources.For(game); var glossary = LoomGlossary.Load(game);
        string Show(int id) => new LoomScriptPresenter(glossary).Present(data.GlobalScript(id)!);
        Assert.Contains("object under the pointer or clicked", Show(7));
        Assert.DoesNotContain("var[0x099]", Show(7).Split("Names used")[0]);
        Assert.Contains("Bobbin: set text layout center, at 160, text line y", Show(16));
        Assert.DoesNotContain("0x00A0", Show(16).Split("Names used")[0]);
        Assert.Contains("a note is playing", Show(20));
        Assert.Contains("a cutscene is running", Show(2));
        Assert.Equal("flying actor of Room 03", glossary.Actor(2, "script:42")!.Name);
        Assert.Equal("Mandible", glossary.Actor(2, "script:91")!.Name);
        Assert.Null(glossary.Actor(2)); // The same slot cannot be an Elder or Mandible globally.
    }

    [Fact]
    public void CdUsedByIncludesGlobalsAndKeepsRepeatedLocalIdsInTheirOwnRooms()
    {
        var game = Game(); var index = LoomUsageIndex.Build(game);
        Assert.NotEmpty(index.UsedBy(LoomUsageTarget.Script, 16));
        Assert.Contains(index.UsedBy(LoomUsageTarget.Script, 8), u => u.Where == "Script 007");
        Assert.Empty(index.UsedBy(LoomUsageTarget.Script, 200));
        var locals = game.Rooms.Select(r => (Room: r.ResourceId!.Value,
            Uses: index.UsedBy(LoomUsageTarget.Script, 200, r.ResourceId))).Where(r => r.Uses.Count > 0).ToArray();
        Assert.True(locals.Length > 1);
        foreach (var (room, uses) in locals) Assert.All(uses, u => Assert.Equal(room, u.ScriptRoom));
    }

    [Fact]
    public void CdExploreLinksUsesDiscSourcesAndRoomScopedScriptResources()
    {
        var game = Game(); var index = new LoomRelationshipIndexer().Build(game);
        var seven = game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == 7 && r.ScriptRoom is null);
        Assert.Contains(index.ForResource(seven), link => link.SourceResource == seven && link.TargetResource is { ResourceId: 8, ScriptRoom: null });
        Assert.All(index.Relationships, link => Assert.Contains("DISK01.LEC", link.Evidence));
        var locals = index.Relationships.Where(link => link.TargetResource is { Kind: ResourceKind.Script, ScriptRoom: not null }).ToArray();
        Assert.NotEmpty(locals);
        foreach (var link in locals)
        {
            Assert.Contains($"Room {link.TargetResource!.ScriptRoom:00}", link.TargetLabel);
            Assert.Equal(LoomRelationshipStatus.StaticCandidate, link.Status);
        }
        Assert.Contains(index.Relationships, link => link.TargetResource is null && link.TargetLabel.Contains("current room (unresolved)"));
    }
}

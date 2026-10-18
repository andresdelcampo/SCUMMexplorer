using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicUseWithTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(TestGames.Root, "mmansion ega en");

    [Theory]
    [InlineData(84, true)]
    [InlineData(142, false)]
    public void NativeHeldInventorySentenceDispatchesAndOnlyFlashlightConsumesBatteries(int target, bool consumes)
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var observed = new List<ScummScriptObservation>();
        var session = ManiacClassicUseWithRoute.Prepare(Folder, new(observed.Add,
            site => site.Script is 2 or 4 || site.Context?.Contains("167") == true,
            key => key is "var[0x008]" or "var[0x009]" or "var[0x00A]" or "var[0x032]"));
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Assert.Equal(3, session.Snapshot.ObjectOwners[167]);
        Assert.Equal(3, session.Snapshot.ObjectOwners[84]);
        Assert.Equal(1, ManiacClassicUseWithRoute.Verb(session, 11).Mode);
        var first = ManiacClassicUseWithRoute.Inventory(session, 167);
        Assert.Equal((3, 167, 0), (first.Area, first.Value, first.Mode));
        var composed = ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs());
        output.WriteLine("First item: " + composed);
        Assert.Contains("batteries", composed);
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentencePreposition]);
        var second = ManiacClassicUseWithRoute.Inventory(session, target);
        Assert.Equal((3, target, 0), (second.Area, second.Value, second.Mode));
        Assert.Equal(167, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject1]);
        Assert.Equal(target, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject2]);
        output.WriteLine("Two items: " + ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
        // A repeated click on the selected second item executes the game's own sentence.
        ManiacClassicUseWithRoute.Inventory(session, target);
        ManiacClassicUseWithRoute.Frames(session, 40);
        Assert.Contains(observed, observation => observation.Site is { Script: 2, Operation: "doSentence", Offset: 0x19F } &&
            observation.Values.GetValueOrDefault("var[0x009]") == 167 && observation.Values.GetValueOrDefault("var[0x00A]") == target);
        Assert.Equal(consumes ? 2 : 0, session.Snapshot.KnownGlobalVariables["var[0x032]"]);
        Assert.Equal(consumes ? 0 : 3, session.Snapshot.ObjectOwners[167]);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("unsupported v2"));
        if (!consumes) return;
        Assert.Contains(observed, observation => observation.Site is { Offset: 0x22, Operation: "move" } && observation.Site.Context?.Contains("167") == true);
        Assert.Contains(observed, observation => observation.Site is { Offset: 0x26, Operation: "setOwnerOf" } && observation.Site.Context?.Contains("167") == true);
        ManiacClassicUseWithRoute.Verb(session, 4);
        ManiacClassicUseWithRoute.Inventory(session, 84);
        ManiacClassicUseWithRoute.Inventory(session, 84);
        ManiacClassicUseWithRoute.Frames(session, 20);
        var game = new GameFolderScanner().Scan(Folder);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 5));
        ManiacClassicUseWithRoute.Frames(session, 20);
        var capture = session.CaptureRoom();
        output.WriteLine($"Source Switch on → dark Room5: lights={capture.Lights}, strips={capture.Flashlight}");
        Assert.Equal(0, capture.Lights & 2);
        Assert.NotEqual(0, capture.Lights & 4);
    }
}

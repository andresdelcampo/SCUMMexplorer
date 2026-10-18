using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>One continuous Spanish enhanced route from native boot, without catalog visits or story writes.</summary>
public sealed class ScummManiacEnhancedPlayTests(ITestOutputHelper output)
{
    [Fact]
    public void PartyChoiceSkipSentenceWalkingAndPickupUseTheNativeInterface()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        var elapsed = 0;
        void Frames(int count) { for (var i = 0; i < count; i++) { session.AdvanceFrame(); elapsed++; } }
        void Until(Func<bool> ready, int limit = 2500)
        {
            for (var i = 0; i < limit && !ready(); i++) Frames(1);
            Assert.True(ready(), $"ego={session.Actors.Single(a => a.Number == 3)}\n" + string.Join("\n", session.SessionTrace.Where(line => line.Contains("Script 02") || line.Contains("startObject") || line.Contains("input:")).TakeLast(20)));
        }
        void Click(int x, int y)
        {
            var input = session.ClickScreen(x, y, out var reason);
            Assert.NotNull(input); Assert.Null(reason); Frames(1);
            output.WriteLine($"frame {elapsed}: {input}; room={session.CurrentRoomId}, sentence={ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}");
        }
        void Key(int key)
        {
            Assert.NotNull(session.PressPlayKey(key, out var reason)); Assert.Null(reason); Frames(1);
        }
        Assert.Equal(45, session.CurrentRoomId);
        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Click(x, y + session.MainScreen.Top); Frames(100); }
        Until(() => session.CurrentRoomId == 44 && session.CutsceneSkippable);
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables["var[0x030]"]);
        Assert.Equal(5, session.Snapshot.KnownGlobalVariables["var[0x031]"]);
        session.PressKey(27);
        Until(() => session.PlayerHasControl);
        foreach (var line in session.SessionTrace.Where(line => line.Contains("abortCutscene:"))) output.WriteLine(line);
        Assert.Contains(session.SessionTrace, line => line.Contains("abortCutscene: Script 131") && line.Contains("+0x0008"));
        Assert.Equal(15, session.Verbs.Count(v => v.Mode == 1));
        Click(8, 120 + session.MainScreen.Top);
        Until(() => session.CurrentRoomId == 1);
        Frames(120);
        foreach (var obj in ScummDataSources.For(game).ReadRoom(1).Objects.Where(obj => obj.Id is 54 or 55)) output.WriteLine(obj.ToString());
        for (var step = 0; step < 2; step++) { Click(8, 120 + session.MainScreen.Top); Frames(200); }
        output.WriteLine($"entrance: frame={elapsed}, viewLeft={session.ViewLeft}, ego={session.Actors.Single(a => a.Number == 3).X}");
        Assert.Equal(352, session.ViewLeft);
        Key('d');
        Click(376 - session.ViewLeft, 84 + session.MainScreen.Top);
        Assert.Equal(14, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceVerb]);
        Assert.Equal(54, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceObject1]);
        Assert.NotEmpty(session.PlaySentenceGlyphs());
        Assert.Contains((byte)13, session.PlayVerbArea().Pixels.Take(320 * 8));
        // The game's script executes the composed sentence on a second object click.
        Click(376 - session.ViewLeft, 84 + session.MainScreen.Top);
        Until(() => (session.Snapshot.ObjectStates[54] & 8) != 0);
        Key('d');
        Click(416 - session.ViewLeft, 84 + session.MainScreen.Top);
        Click(416 - session.ViewLeft, 84 + session.MainScreen.Top);
        Until(() => session.Snapshot.ObjectOwners[55] == 3);
        Assert.Contains(session.SessionTrace, line => line.Contains("startObject runs object 54 verb 0x0E"));
        Assert.Contains(session.SessionTrace, line => line.Contains("startObject runs object 55 verb 0x0E"));
        var picture = new ScummPlayScreen(game, session, indexed.States).Compose(elapsed);
        Assert.True(Enumerable.Range(32, 8).Sum(y => picture.Verbs.Pixels.Skip(y * 320).Take(144).Count(p => p != 0)) > 0);
        Click(8, session.MainScreen.Bottom + 36);
        Assert.Equal(3, session.DeliveredInput.Last().Area);
        Assert.Equal(55, session.DeliveredInput.Last().Value);
        Assert.Contains(session.PlaySentenceGlyphs(), glyph => glyph != 32);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("unsupported v2"));
        output.WriteLine($"complete: frames={elapsed}; inputs={session.DeliveredInput.Count}; keyOwner={session.Snapshot.ObjectOwners[55]}; sentence={ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}");
        foreach (var line in session.SessionTrace.Where(line => line.Contains("startObject runs") || line.Contains("abortCutscene:"))) output.WriteLine(line);
        var data = ScummDataSources.For(game);
        foreach (var id in new[] { 2, 4, 131 })
            foreach (var instruction in data.GlobalScript(id)!.Instructions.Where(i => i.Operation is "doSentence" or "drawSentence" or "beginOverride" or "endCutscene"))
                output.WriteLine($"Script {id} +{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}");
        foreach (var (room, obj, verb) in new[] { (45, 260, 13), (45, 263, 13), (45, 403, 13), (44, 258, 13), (1, 54, 14), (1, 55, 14) })
        {
            var code = data.ReadObjectVerb(room, data.ReadRoom(room).Objects.Last(o => o.Id == obj), verb, out var entry);
            output.WriteLine($"Room {room} object {obj} verb {verb}: {entry}");
            foreach (var instruction in code.Instructions.SkipWhile(i => i.Offset < code.EntryOffset).Take(10))
                output.WriteLine($"+{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}");
        }
    }
}

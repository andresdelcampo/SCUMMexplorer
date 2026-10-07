using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCdReadableTests
{
    [Fact]
    public void EntireCdScriptLibraryUsesReadablePresentation()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var game = new GameFolderScanner().Scan(Path.Combine(root, "Games/loom vga en"));
        var data = ScummDataSources.For(game);
        var glossary = LoomGlossary.Load(game);
        var counts = new Dictionary<string, int>();
        var raw = new Dictionary<string, int>();
        void Check(string kind, LoomScriptDisassembly? script)
        {
            if (script is null) return;
            var output = new LoomScriptPresenter(glossary).Present(script);
            Assert.Contains("Readable view:", output);
            Assert.Contains("DISK01.LEC", output);
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
            foreach (var line in output.Split('\n').Where(l => l.Contains("[raw] ")))
            {
                var operation = line.Split("[raw] ")[1].Split(' ')[0];
                raw[operation] = raw.GetValueOrDefault(operation) + 1;
            }
        }
        foreach (var id in data.GlobalScriptIds()) Check("global", data.GlobalScript(id));
        foreach (var room in game.Rooms)
        {
            int id = room.ResourceId!.Value;
            Check("entry", data.RoomEntryScript(id));
            Check("exit", data.RoomExitScript(id));
            foreach (var local in data.RoomLocalScriptIds(id)) Check("local", data.RoomLocalScript(id, local));
            var objects = data.ReadRoom(id).Objects;
            foreach (var (objectId, verbs) in data.ReadObjectVerbs(id, objects))
                foreach (var verb in verbs) Check("object verb", data.ReadObjectVerb(id, objects.Single(o => o.Id == objectId), verb.VerbId, out _));
        }
        Assert.All(new[] { "global", "entry", "exit", "local", "object verb" }, kind => Assert.True(counts.GetValueOrDefault(kind) > 0));
        File.WriteAllLines(Path.Combine(root, ".verification/loom-parity/readable-library.txt"),
            counts.Select(c => $"{c.Key}: {c.Value}").Concat(raw.Select(r => $"raw fallback {r.Key}: {r.Value}")));
        Assert.Empty(raw); // Every decoded opcode family in this supplied CD edition has a readable phrase.
    }

    [Fact]
    public void CdReadableHelpersNameTheirOwnVariablesAndKeepRawAddressesAvailable()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom vga en"));
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var glossary = LoomGlossary.Load(game);
        var script = data.GlobalScript(28)!;
        var readable = new LoomScriptPresenter(glossary).Present(script);
        Assert.Contains("clear the draft buffer", readable);
        Assert.Contains("if distaff in use (2 when held) == 2", readable);
        Assert.Contains("if difficulty (0 practice, 1 standard, 2 expert) != 2", readable);
        Assert.Contains("wrong tries = 0", readable);
        Assert.Contains("fill draft buffer with 4 values", readable);
        Assert.Contains("Names used", readable);
        Assert.Contains("var[0x096]", script.Format(false));
        Assert.Contains("var[0x0AD]", script.Format(false));
        Assert.Null(glossary.Variable(0x063)); // EGA's address must not acquire the CD hint-counter role.
        Assert.Null(glossary.Bit(0xD61));
        Assert.Null(glossary.Script(91)); // No unverified EGA story title by matching ID.

        var note = new LoomScriptPresenter(glossary).Present(data.GlobalScript(10)!);
        Assert.Contains("notes the player has played", note);
        Assert.Contains("number of notes played so far", note);
        var look = new LoomScriptPresenter(glossary).Present(data.GlobalScript(17)!);
        Assert.Contains("ego costume for size 0 (large)", look);
        Assert.Contains("ego costume for size 2 (small)", look);
        Assert.Contains("ego standing animation", look);
        var input = new LoomScriptPresenter(glossary).Present(data.GlobalScript(6)!);
        Assert.Contains("last click x", input);
        Assert.Contains("pointer x", input);
        Assert.Contains("walk target x", input);
        var comparison = script with { Instructions = [new(0, 0, "isEqual", "var[0x0A2], 0x0002, branch +0x0000 -> 0x0007", "")], CodeLength = 7 };
        Assert.Contains("previous room == Room 02", new LoomScriptPresenter(glossary).Present(comparison));
    }
}

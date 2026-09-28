using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A click in the verb area sent as assumed input: findVerbAtPos over the verb
/// slots, then runInputScript(1, verb, 0) runs VAR_VERB_SCRIPT (script 005),
/// whose area 1 turns a distaff note into script 010.
/// </summary>
public sealed class LoomVerbClickTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static (GameLibrary Game, LoomExplorationSession Session)? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return (game, new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"))));
    }

    private static ushort? Var(LoomExplorationSession session, int number) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue($"var[0x{number:X3}]", out var value) ? value : null;

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(250, 1, 0, 32, 16, instructions, null, null, [], [], "Synthetic test script");

    // Script 018 [2], as script 003 starts it in play: var[0x064] = 2 and the distaff notes on.
    private static void TurnOnDistaff(LoomExplorationSession session) =>
        session.RunTopLevelScript(Script(
            new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"),
            new(5, 0, "stopObjectCode", "", "00")));

    private static bool Traced(LoomExplorationSession session, int from, string text) =>
        session.SessionTrace.Skip(from).Any(line => line.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void ANoteWithNothingAimedAtReachesScript013()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        TurnOnDistaff(session);
        var note = session.VerbArea().Items.Single(item => item.VerbId == 0x03);
        var from = session.SessionTrace.Count;
        var click = session.ClickVerbArea(note.X + 1, note.Y + 1);
        Assert.True(Traced(session, from, "verb 0x03 (slot 3), runs script 5 with [1, 3, 0]"));
        // Script 010: var[0x064] = 2 but var[0x067] = 0, so it chains to 013 ("aim at something first").
        Assert.True(Traced(session, from, "Script 10 chains to script 13"));
        Assert.Equal((ushort?)(note.X + 1), Var(session, 0x02C));
        Assert.Equal((ushort?)(note.Y + 1), Var(session, 0x02D));
        Assert.Contains(click.Uncertainties, line => line.Contains("runInputScript(1, 0x03, 0)", StringComparison.Ordinal));
        Assert.Null(session.Pointer);
    }

    [Fact]
    public void ANoteAfterAimingAtAnObjectIsRecorded()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        TurnOnDistaff(session);
        // Room 02 object 156 (class 1): script 006 sets var[0x067] to it and walks the ego there.
        session.Click(700, 40);
        for (var frame = 0; frame < 4; frame++) session.AdvanceFrame();
        Assert.Equal((ushort?)156, Var(session, 0x067));
        var note = session.VerbArea().Items.Single(item => item.VerbId == 0x03);
        var from = session.SessionTrace.Count;
        session.ClickVerbArea(note.X + 1, note.Y + 1);
        // Script 010 stores the note in var[0x056 + var[0x062]] and chains to 027.
        Assert.True(Traced(session, from, "Script 10 chains to script 27"));
        Assert.Equal((ushort?)3, Var(session, 0x056));
        Assert.Equal((ushort?)1, Var(session, 0x062));
    }

    [Fact]
    public void AClickOffEveryVerbSendsVerbZeroAndRefusalsAreLabelled()
    {
        if (Start() is not { } started) return;
        var (game, session) = started;
        // After the boot every verb is off: findVerbAtPos finds nothing, so the verb is 0.
        var from = session.SessionTrace.Count;
        session.ClickVerbArea(20, 150);
        Assert.True(Traced(session, from, "on no verb, runs script 5 with [1, 0, 0]"));

        var outside = session.ClickVerbArea(20, 100);
        Assert.Contains(outside.Uncertainties, line => line.Contains("not sent, because the point is outside the verb area", StringComparison.Ordinal));

        // Script 002 (the cutscene start hook) turns user input off.
        session.RunTopLevelScript(new LoomScriptReader().Decode(game.Resources.Single(item => item.Kind == ResourceKind.Script && item.ResourceId == 2)));
        var ignored = session.ClickVerbArea(20, 150);
        Assert.Contains(ignored.Uncertainties, line => line.Contains("user input is off", StringComparison.Ordinal));
    }
}

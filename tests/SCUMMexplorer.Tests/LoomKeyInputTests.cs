using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A key sent as assumed input: runInputScript(4, key, 1) runs VAR_VERB_SCRIPT
/// (script 005), whose area 4 maps note letters to script 010, handles Escape,
/// the text speed, F5–F7 and Ctrl+V. The engine's own keys are not sent.
/// </summary>
public sealed class LoomKeyInputTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static LoomExplorationSession? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
    }

    private static ushort? Var(LoomExplorationSession session, string name) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue(name, out var value) ? value : null;

    private static void Frames(LoomExplorationSession session, int count)
    {
        for (var frame = 0; frame < count; frame++) session.AdvanceFrame();
    }

    private static bool Traced(LoomExplorationSession session, int from, string text) =>
        session.SessionTrace.Skip(from).Any(line => line.Contains(text, StringComparison.Ordinal));

    // Script 018 [2] turns the distaff on; a room click on Room 02 object 156 (class 1) aims at it.
    private static void DistaffAimedAt156(LoomExplorationSession session)
    {
        session.RunTopLevelScript(new LoomScriptDisassembly(250, 1, 0, 32, 16,
            [new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"), new(5, 0, "stopObjectCode", "", "00")],
            null, null, [], [], "Synthetic test script"));
        session.Click(700, 40);
        // The ego walks to object 156's walk point (5 frames); script 006 holds bit[0xD63] until it arrives.
        Frames(session, 10);
    }

    [Fact]
    public void NoteLettersAreNotesAndEscapeCancelsTheDraftBeingPlayed()
    {
        if (Start() is not { } session) return;
        DistaffAimedAt156(session);
        Assert.Equal((ushort?)156, Var(session, "var[0x067]"));
        var from = session.SessionTrace.Count;
        // Script 005 +0x00D2: 'e' is note 3 (the q-w-e-r-t-y-u-i row); script 010 records it.
        var press = session.PressKey('e');
        Assert.True(Traced(session, from, "key: 'e' runs script 5 with [4, 0x65, 1]"));
        Assert.Contains(press.Uncertainties, line => line.Contains("runInputScript(4, 0x65, 1)", StringComparison.Ordinal));
        Assert.Equal((ushort?)3, Var(session, "var[0x056]"));
        // +0x012C: 'c' is note 1 (the c-d-e-f-g-a-b-C row): the second note of the draft.
        session.PressKey('c');
        Assert.Equal((ushort?)1, Var(session, "var[0x057]"));
        Assert.Equal((ushort?)2, Var(session, "var[0x062]"));
        // Script 020 (started by the first note) sets bit[0xD66] while it plays the draft back.
        Assert.Equal((ushort?)1, Var(session, "bit[0xD66]"));

        // Escape (+0x01A4) outside a cutscene: the close-up goes off, and with bit[0xD66] set
        // a cutscene runs scripts 028 and 021, which stop the playback and clear the draft.
        from = session.SessionTrace.Count;
        session.PressKey(0x1B);
        Frames(session, 6);
        Assert.True(Traced(session, from, "cutscene at Script 05 +0x01C0"));
        Assert.Equal((ushort?)0, Var(session, "var[0x067]"));
        Assert.Equal((ushort?)0, Var(session, "var[0x062]"));
        Assert.Equal((ushort?)0, Var(session, "bit[0xD66]"));
    }

    [Fact]
    public void TextSpeedAndMusicKeysChangeTheirVariables()
    {
        if (Start() is not { } session) return;
        // '>' (+0x01F6): var[0x0A5] + 1, then VAR_CHARINC (var[0x025]) = 9 - var[0x0A5].
        session.PressKey('>');
        Assert.Equal(((ushort?)1, (ushort?)8), (Var(session, "var[0x0A5]"), Var(session, "var[0x025]")));
        session.PressKey('<');
        Assert.Equal(((ushort?)0, (ushort?)9), (Var(session, "var[0x0A5]"), Var(session, "var[0x025]")));
        // F6 (0x140, +0x024F) cycles var[0x0A2]: music off (1), music and effects off (2), both on (0).
        var cycle = new List<ushort?>();
        for (var press = 0; press < 3; press++)
        {
            session.PressKey(0x140);
            Frames(session, 1);
            cycle.Add(Var(session, "var[0x0A2]"));
        }
        Assert.Equal([1, 2, 0], cycle);
    }

    [Fact]
    public void F5RunsTheSaveScreenScript()
    {
        if (Start() is not { } session) return;
        // F5 (0x13F, +0x0237): bit[0xD6F] is 0, so script 004 runs; it loads Room 70 in a cutscene.
        var from = session.SessionTrace.Count;
        session.PressKey(0x13F);
        Frames(session, 12);
        Assert.True(Traced(session, from, "cutscene at Script 04 +0x0000"));
        Assert.Equal(70, session.Snapshot.RoomId);
    }

    [Fact]
    public void TheEnginesOwnKeysAndKeysWithInputOffAreNotSent()
    {
        if (Start() is not { } session) return;
        var from = session.SessionTrace.Count;
        Assert.Contains(session.PressKey(0x142).Uncertainties, line => line.Contains("not sent, because it is the restart key (var[0x02A]", StringComparison.Ordinal));
        Assert.Contains(session.PressKey(' ').Uncertainties, line => line.Contains("not sent, because it is the pause key (var[0x02B]", StringComparison.Ordinal));
        Assert.Contains(session.PressKey('.').Uncertainties, line => line.Contains("talk-stop key", StringComparison.Ordinal));
        Assert.False(Traced(session, from, "runs script 5"));
        Assert.Equal("F8", LoomExplorationSession.DescribeKey(0x142));
        Assert.Equal("Ctrl+V", LoomExplorationSession.DescribeKey(0x16));

        // Script 002 (the cutscene start hook) turns user input off.
        var game = new GameFolderScanner().Scan(Folder);
        session.RunTopLevelScript(new LoomScriptReader().Decode(game.Resources.Single(item =>
            item.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && item.ResourceId == 2)));
        Assert.Contains(session.PressKey('e').Uncertainties, line => line.Contains("user input is off", StringComparison.Ordinal));
    }

    [Fact]
    public void EscapeSkipsScript052ToItsOverrideJump()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 10));
        // Script 052 (Room 10 object 295's cutscene), started as that verb would.
        session.RunTopLevelScript(new LoomScriptDisassembly(250, 1, 0, 32, 16,
            [new(0, 0x0A, "startScript", "0x34, []", "0A 34 FF"), new(3, 0, "stopObjectCode", "", "00")],
            null, null, [], [], "Synthetic test script"));
        Assert.True(session.InCutscene);

        // Before +0x00C6 beginOverride, abortCutscene has nothing to jump to.
        Assert.False(session.CutsceneSkippable);
        var early = session.PressKey(0x1B);
        Assert.Contains(early.Uncertainties, line => line.Contains("the open cutscene has no override set", StringComparison.Ordinal));
        Assert.Contains(early.Uncertainties, line => line.Contains("did not reach the input script after abortCutscene, because user input is off", StringComparison.Ordinal));
        Assert.Equal((ushort)0, Var(session, "var[0x005]"));

        // In Room 11, past +0x00C6: the skip jump (+0x00C8) goes to +0x0157, which reads VAR_OVERRIDE.
        for (var frame = 0; frame < 40 && session.Snapshot.RoomId != 11; frame++) session.AdvanceFrame();
        Frames(session, 20);
        Assert.Equal(11, session.Snapshot.RoomId);
        Assert.True(session.CutsceneSkippable);
        var from = session.SessionTrace.Count;
        session.PressKey(0x1B);
        Assert.False(session.CutsceneSkippable);
        Assert.True(Traced(session, from, "abortCutscene: Script 52 continues at its skip jump +0x00C8, var[0x005] = 1"));
        Assert.Equal((ushort)1, Var(session, "var[0x005]"));

        for (var frame = 0; frame < 20 && session.Snapshot.RoomId != 13; frame++) session.AdvanceFrame();
        Assert.Equal(13, session.Snapshot.RoomId);
        Assert.True(Traced(session, from, "stopScript 210 at Script 52 +0x015C"));
        Assert.True(Traced(session, from, "endCutscene at Script 52 +0x017C runs script 3"));
        Assert.Equal((ushort)0, Var(session, "var[0x005]"));
    }
}

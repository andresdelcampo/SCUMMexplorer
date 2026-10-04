using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomGameStatusTests
{
    private static LoomExplorationSession Start()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        for (int i = 0; i < 3000 && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        return session;
    }
    [Fact]
    public void LoadedScriptsEstablishPossessionPolarityAvailableNotesAndPackedDrafts()
    {
        var status = Start().GameStatus;
        Assert.True(status.Supported, status.Evidence);
        Assert.True(status.CanEdit);
        Assert.False(status.StaffHeld);
        Assert.Equal(3, status.AvailableNoteCount);
        Assert.Equal(16, status.Drafts.Count);
        Assert.Equal([8, 4, 5, 1], status.Drafts[15].Notes);
        Assert.Equal(0x54, status.Drafts[15].ForwardVerb);
        Assert.Equal([3, 1, 3, 2], status.Drafts[0].Notes);
        Assert.True(status.Drafts[0].Learned);
    }
    [Fact]
    public void ExplicitSequenceEditPreservesFlagsAndResponseTargetsAndRejectsInvalidNotes()
    {
        var session = Start(); var original = session.GameStatus.Drafts[0];
        session.UpdateDraftSequence(0, [8, 7, 6, 5]);
        var edited = session.GameStatus.Drafts[0];
        Assert.Equal(original.Packed!.Value & 0xF000, edited.Packed!.Value & 0xF000);
        Assert.Equal(original.ForwardVerb, edited.ForwardVerb); Assert.Equal(original.ReverseVerb, edited.ReverseVerb);
        Assert.Equal([8, 7, 6, 5], edited.Notes); Assert.Contains("Explicit user", edited.Origin);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateDraftSequence(0, [1, 2, 3, 9]));
        Assert.Equal(edited.Packed, session.GameStatus.Drafts[0].Packed);
    }
    [Fact]
    public void StaffEditsPreserveOtherBitsAndRefreshTheSourceNoteControls()
    {
        var session = Start(); var oldWord = session.Snapshot.KnownGlobalVariables["var[0x0D6]"];
        session.UpdateStaffPossession(true);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.Equal(oldWord & ~2, session.Snapshot.KnownGlobalVariables["var[0x0D6]"] & ~2);
        Assert.Equal(2, session.Snapshot.KnownGlobalVariables["var[0x064]"]);
        session.UpdateAvailableNotes(8); Assert.Equal(8, session.GameStatus.AvailableNoteCount);
        session.UpdateStaffPossession(false); Assert.False(session.GameStatus.StaffHeld);
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["var[0x064]"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateAvailableNotes(9));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void NativeInputAcceptsTheDisplayedLimitAndRefusesTheNextNote(int limit)
    {
        var session = Start(); session.UpdateStaffPossession(true); session.UpdateAvailableNotes(limit);
        Assert.Equal(limit, session.Snapshot.KnownGlobalVariables["var[0x07A]"]);
        session.Click(700, 40);
        for (int i = 0; i < 100 && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        var from = session.SessionTrace.Count;
        var note = session.VerbArea().Items.Single(i => i.VerbId == limit);
        session.ClickVerbArea(note.X + 1, note.Y + 1);
        Assert.Equal(limit, session.Snapshot.KnownGlobalVariables["var[0x056]"]);
        Assert.DoesNotContain(session.SessionTrace.Skip(from), l => l.Contains("Script 10 chains to script 11"));
        if (limit == 8) return;
        note = session.VerbArea().Items.Single(i => i.VerbId == limit + 1);
        session.ClickVerbArea(note.X + 1, note.Y + 1);
        Assert.Contains(session.SessionTrace.Skip(from), l => l.Contains("Script 10 chains to script 11"));
    }

    [Fact]
    public void NativeMatcherIncludesTheLoadedTranscendSlot()
    {
        var session = Start(); session.UpdateStaffPossession(true); session.UpdateAvailableNotes(8);
        session.Click(700, 40);
        for (int i = 0; i < 32; i++) session.AdvanceFrame();
        var from = session.SessionTrace.Count;
        foreach (int id in session.GameStatus.Drafts[15].Notes)
        {
            var note = session.VerbArea().Items.Single(i => i.VerbId == id);
            session.ClickVerbArea(note.X + 1, note.Y + 1);
        }
        for (int i = 0; i < 1500; i++) session.AdvanceFrame();
        Assert.Contains(session.SessionTrace.Skip(from), l => l.Contains("Script 22 chains to script 26"));
        Assert.Equal(0x54, session.Snapshot.KnownGlobalVariables["var[0x065]"]);
    }

    [Fact]
    public void BrowsingRaisesHeldStaffToInitialLevelAndKeepsHigherExplicitChoices()
    {
        var session = Start(); var game = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative)));
        session.UpdateAvailableNotes(1);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 26), preserveStaffLevel: true);
        for (int i = 0; i < 1000 && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        Assert.True(session.GameStatus.StaffHeld);
        Assert.Equal(3, session.GameStatus.AvailableNoteCount);
        Assert.Contains("browsing adjustment", session.GameStatus.NotesOrigin);
        session.UpdateAvailableNotes(8);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 13), preserveStaffLevel: true);
        for (int i = 0; i < 1000 && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        Assert.Equal(8, session.GameStatus.AvailableNoteCount);
        session.UpdateAvailableNotes(4);
        for (int i = 0; i < 32; i++) session.AdvanceFrame();
        Assert.Equal(4, session.GameStatus.AvailableNoteCount);
    }
    [Fact]
    public void StatusCannotRewritePrerequisitesDuringAStartedScene()
    {
        var session = Start(); session.Click(377, 9);
        Assert.Throws<InvalidOperationException>(() => session.UpdateStaffPossession(true));
    }

    [Fact]
    public void LearnedEditPreservesSequenceReverseKnowledgeAndResponses()
    {
        var session = Start(); var original = session.GameStatus.Drafts[0];
        session.UpdateDraftLearned(0, false);
        var edited = session.GameStatus.Drafts[0];
        Assert.False(edited.Learned); Assert.Equal(original.Packed!.Value & ~0x2000, edited.Packed!.Value);
        Assert.Equal(original.Reversible, edited.Reversible); Assert.Equal(original.ForwardVerb, edited.ForwardVerb); Assert.Equal(original.ReverseVerb, edited.ReverseVerb);
        session.UpdateDraftSequence(0, [1, 2, 1, 2]);
        Assert.False(session.GameStatus.Drafts[0].Learned);
        session.UpdateDraftLearned(0, true); Assert.True(session.GameStatus.Drafts[0].Learned);
        Assert.Equal([1, 2, 1, 2], session.GameStatus.Drafts[0].Notes);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateDraftLearned(16, true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCastingRespectsExplicitLearnedChoice(bool learned)
    {
        var session = Start(); session.UpdateStaffPossession(true); session.UpdateAvailableNotes(8);
        session.UpdateDraftLearned(0, learned);
        session.Click(700, 40);
        for (int frame = 0; frame < 100 && !session.GameStatus.CanEdit; frame++) session.AdvanceFrame();
        Assert.Equal((ushort)156, session.Snapshot.KnownGlobalVariables["var[0x067]"]);
        var from = session.SessionTrace.Count;
        foreach (int id in session.GameStatus.Drafts[0].Notes)
        {
            var note = session.VerbArea().Items.Single(i => i.VerbId == id);
            session.ClickVerbArea(note.X + 1, note.Y + 1);
        }
        int idle = 0;
        for (int frame = 0; frame < 3000 && idle < 32; frame++) { session.AdvanceFrame(); idle = session.GameStatus.CanEdit ? idle + 1 : 0; }
        var trace = session.SessionTrace.Skip(from).ToArray();
        Assert.Contains(trace, line => line.Contains("Script 22 chains to script 26"));
        Assert.Equal(learned, trace.Any(line => line.Contains("startObject runs object 156 verb 0x3A")));
        Assert.Equal(learned, trace.Any(line => line.Contains("cutscene at Script 49")));
        if (!learned) Assert.True(session.GameStatus.CanEdit);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, false)]
    public void NativeReverseMatchingRespectsSeparateCapability(int slot, bool reversible)
    {
        var session = Start(); Assert.Equal(reversible, session.GameStatus.Drafts[slot].Reversible);
        session.UpdateStaffPossession(true); session.UpdateAvailableNotes(8);
        session.UpdateDraftSequence(slot, [8, 7, 1, 2]); session.UpdateDraftLearned(slot, true);
        session.Click(700, 40);
        for (int i = 0; i < 32; i++) session.AdvanceFrame();
        var from = session.SessionTrace.Count;
        foreach (int id in session.GameStatus.Drafts[slot].Notes.Reverse())
        {
            var note = session.VerbArea().Items.Single(i => i.VerbId == id);
            session.ClickVerbArea(note.X + 1, note.Y + 1);
        }
        for (int i = 0; i < 500; i++) session.AdvanceFrame();
        Assert.Equal(reversible, session.SessionTrace.Skip(from).Any(l => l.Contains("Script 22 chains to script 26")));
        Assert.Equal(reversible, session.GameStatus.Drafts[slot].Reversible);
    }
}

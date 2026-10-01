using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomGameStatusTests
{
    private static LoomExplorationSession Start()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
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
        Assert.Equal(2, status.AvailableNoteCount);
        Assert.Equal(15, status.Drafts.Count);
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
    [Fact]
    public void StatusCannotRewritePrerequisitesDuringAStartedScene()
    {
        var session = Start(); session.Click(377, 9);
        Assert.Throws<InvalidOperationException>(() => session.UpdateStaffPossession(true));
    }
}

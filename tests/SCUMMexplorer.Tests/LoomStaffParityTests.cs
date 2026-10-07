using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomStaffParityTests
{
    [Theory]
    [InlineData("loom ega mt32 en")]
    [InlineData("loom vga en")]
    public void CatalogVisitsKeepStaffAppearanceNotesAndMenuSuppressionTogether(string edition)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", edition));
        var game = new GameFolderScanner().Scan(folder);
        var indexed = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
        var session = new LoomExplorationSession(game, indexed);
        void Visit(int room)
        {
            session.VisitRoomById(room);
            for (var f = 0; f < 600 && (!session.GameStatus.CanEdit || f < 40); f++) session.AdvanceFrame();
        }
        bool NoteText() => session.Messages.OnScreen.Any(m => m.Y >= 144 && m.Source.StartsWith("Script 18 +", StringComparison.Ordinal));
        Visit(3);
        session.UpdateStaffPossession(true);
        Visit(5);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.Equal(8, session.VerbArea().Items.Count(v => v.VerbId is >= 1 and <= 8));
        Assert.True(NoteText(), string.Join("; ", session.Messages.OnScreen.Select(m => $"{m.Source} ({m.X},{m.Y})")));
        var held = session.Actors.Single(a => a.Number == 1).Look;
        session.UpdateStaffPossession(false);
        var without = session.Actors.Single(a => a.Number == 1).Look;
        if (game.Id == GameId.LoomVga) { Assert.Equal(172, held.Costume); Assert.Equal(171, without.Costume); }
        else Assert.NotEqual(held.StandFrame, without.StandFrame);
        Visit(3);
        Assert.False(session.GameStatus.StaffHeld);
        Assert.DoesNotContain(session.VerbArea().Items, v => v.VerbId is >= 1 and <= 8);
        session.UpdateStaffPossession(true);
        Visit(69);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.DoesNotContain(session.VerbArea().Items, v => v.VerbId is >= 1 and <= 8 or >= 0x31 and <= 0x35 or >= 0x5A and <= 0x61);
        Assert.False(NoteText());
        Visit(3);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.True(NoteText());
        Assert.Equal(8, session.VerbArea().Items.Count(v => v.VerbId is >= 1 and <= 8));
    }

}

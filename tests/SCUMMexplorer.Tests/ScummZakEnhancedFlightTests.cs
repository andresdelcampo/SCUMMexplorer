using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Explicit room/tool exploration preparation, then the native reservation and flight interface.</summary>
public sealed class ScummZakEnhancedFlightTests(ITestOutputHelper output)
{
    private sealed class Route(ScummGameSession session, GameLibrary game)
    {
        public ScummGameSession Session { get; } = session;
        public HashSet<string> Messages { get; } = [];
        public void Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            { foreach (var message in Session.Messages.OnScreen) Messages.Add(message.Text); Session.AdvanceFrame(); }
        }
        public void Visit(int room) => Session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        public void SkipReachedAlien()
        {
            for (var frame = 0; frame < 1200 && !Session.CutsceneSkippable; frame++) Frames(1);
            Assert.True(Session.CutsceneSkippable);
            Session.PressKey(27);
            for (var frame = 0; frame < 1200 && Session.InCutscene; frame++) Frames(1);
            Assert.False(Session.InCutscene);
            Assert.True(Session.UserInputOn);
        }
        public void Click(int x, int y)
        { Assert.NotNull(Session.ClickScreen(x, y, out var refusal)); Assert.Null(refusal); Frames(2); }
        public void RoomClick(int x, int y) => Click(x - Session.ViewLeft, y + Session.MainScreen.Top);
        public void Verb(int id)
        { var v = Session.PlayVerbArea().Items.Single(v => v.VerbId == id && v.Mode == 1); Click(v.X + 4, v.Y + 3); }
        public void Inventory(int id)
        {
            var inventory = Session.CaptureInventoryStatus();
            var items = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
            var index = Array.FindIndex(items, i => i.ObjectId == id);
            Assert.InRange(index, 0, 3);
            Click((index % 2) * 176 + 8, Session.MainScreen.Bottom + 36 + (index / 2) * 8);
        }
        public void UseHeldOnCard(int tool)
        { Verb(11); Inventory(tool); RoomClick(248, 90); RoomClick(248, 90); Frames(180); }
        public void Airport()
        { Visit(8); Frames(100); SkipReachedAlien(); Frames(100); Assert.Equal(144, Word(4)); }
        public void Terminal()
        { Verb(11); RoomClick(144, 80); RoomClick(144, 80); Frames(180); }
        public int Word(int number) => Session.Snapshot.KnownGlobalVariables[$"var[0x{number:X3}]"];
    }

    private static Route? Prepare(bool cashCard)
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!Directory.Exists(folder)) return null;
        var game = new GameFolderScanner().Scan(folder);
        var route = new Route(ScummGameSession.BootForPlay(game, out _), game);
        route.Frames(150);
        route.Visit(2); route.Frames(300); route.SkipReachedAlien(); route.Frames(100);
        route.Session.ActivateObjectVerb(26, 14); route.Frames(100); // disclosed exploration pickup
        route.Visit(1); route.Frames(100);
        if (cashCard) route.UseHeldOnCard(26);
        return route;
    }

    [Fact]
    public void RetainedKnifeClassRetrievesCashCardButUnsuitableHeldTicketDoesNot()
    {
        if (Prepare(false) is not { } r) return;
        r.UseHeldOnCard(82);
        Assert.Equal(15, r.Session.Snapshot.ObjectOwners[18]);
        Assert.Contains(r.Messages, text => text.Contains("I can't reach it with that."));
        r.UseHeldOnCard(26);
        Assert.Equal(1, r.Session.Snapshot.ObjectOwners[18]);
        Assert.Equal(12250, r.Word(62));
        Assert.Equal(1, r.Session.Snapshot.ObjectOwners[26]);
        Assert.Equal(1, r.Session.Snapshot.ObjectOwners[82]);
        Assert.DoesNotContain(r.Session.Snapshot.Uncertainties, u => u.Contains("unsupported v2"));
        output.WriteLine("Room 1 helper93 +0013/+001E reads held tool native byte6; +0029 picks up18 only for knife26.");
    }

    [Fact]
    public void MissingCashCardRefusesReservationsWithoutChangingExistingTicketOrMoney()
    {
        if (Prepare(false) is not { } r) return;
        r.Airport(); r.Terminal();
        Assert.Equal(8, r.Session.CurrentRoomId);
        Assert.Contains(r.Messages, text => text.Contains("I don't have my CashCard here."));
        Assert.Equal(1, r.Session.Snapshot.ObjectOwners[82]);
        Assert.Equal(145, r.Word(155));
        Assert.Equal(662, r.Word(79));
        Assert.Equal(260, r.Word(82));
        Assert.Equal(12250, r.Word(62));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeReservationChoiceCancelsOrBuysAndBoardsWithActualMealService(bool buy)
    {
        if (Prepare(true) is not { } r) return;
        Assert.Equal(1, r.Session.Snapshot.ObjectOwners[18]);
        r.Airport(); r.Terminal();
        Assert.Equal(45, r.Session.CurrentRoomId);
        Assert.Equal(12510, r.Word(62)); // source refunds the old $260 ticket when opening
        Assert.Equal(14, r.Session.Snapshot.ObjectOwners[82]);
        r.RoomClick(80, 40); r.Frames(30); // rendered second destination, source object663
        Assert.Equal(663, r.Word(79));
        r.RoomClick(buy ? 80 : 240, 108); r.Frames(200);
        Assert.Equal(8, r.Session.CurrentRoomId);
        Assert.Equal(144, r.Word(4));
        Assert.Equal(buy ? 12250 : 12510, r.Word(62));
        Assert.Equal(buy ? 1 : 14, r.Session.Snapshot.ObjectOwners[82]);
        Assert.Equal(buy ? 144 : 0, r.Word(155));
        Assert.Equal(buy ? 260 : 0, r.Word(82));
        r.Verb(13); r.RoomClick(20, 60); r.Frames(buy ? 2500 : 180);
        if (buy)
        {
            Assert.Equal(11, r.Session.CurrentRoomId);
            Assert.Equal(14, r.Session.Snapshot.ObjectOwners[82]);
            Assert.Equal(0, r.Word(155));
            Assert.Equal(1, r.Session.Snapshot.ObjectOwners[51]);
            Assert.Contains(r.Session.CaptureInventoryStatus().Items, item => item.ObjectId == 51 && item.RetainedRoomId == 11);
            Assert.False(r.Session.InCutscene);
            Assert.True(r.Session.UserInputOn);
            Assert.DoesNotContain(r.Session.PendingScriptSummary, line => line.StartsWith("Script 104 "));
            Assert.Contains(r.Session.SessionTrace, line => line.Contains("Script 116") && line.Contains("delay"));
            Assert.Contains(r.Messages, text => text.Contains("Here's your meal."));
        }
        else
        {
            Assert.Equal(8, r.Session.CurrentRoomId);
            Assert.Contains(r.Messages, text => text.Contains("I need a ticket first."));
            Assert.Equal(15, r.Session.Snapshot.ObjectOwners[51]);
        }
        Assert.DoesNotContain(r.Session.Snapshot.Uncertainties, u => u.Contains("unsupported v2"));
        output.WriteLine($"buy={buy}, cash={r.Word(62)}, departure={r.Word(155)}, owner82={r.Session.Snapshot.ObjectOwners[82]}, peanuts51={r.Session.Snapshot.ObjectOwners[51]}, room={r.Session.CurrentRoomId}.");
    }
}

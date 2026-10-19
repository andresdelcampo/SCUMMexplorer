using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>Native bus segment after explicitly disclosed knife/card and street previews.</summary>
public sealed class ScummZakBusTests
{
    [Theory]
    [InlineData("zak ega en", false)]
    [InlineData("zakE ega en", false)]
    [InlineData("zak ega en", true)]
    [InlineData("zakE ega en", true)]
    public void NativeWakeAndReaderEitherChargeTheFareOrRefuseAnUnrelatedTool(string folder, bool wrongTool)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var data = ScummDataSources.For(game);
        var session = ScummGameSession.BootForPlay(game, out _);
        var messages = new HashSet<string>();
        void Run(int count)
        {
            for (var n = 0; n < count; n++)
            {
                foreach (var message in session.Messages.OnScreen) messages.Add(message.Text);
                session.AdvanceFrame();
            }
        }
        void Until(Func<bool> ready, int bound)
        { for (var n = 0; n < bound && !ready(); n++) Run(1); Assert.True(ready()); }
        void Click(int x, int y)
        { Assert.NotNull(session.ClickScreen(x, y, out var refusal)); Assert.Null(refusal); Run(5); }
        void Verb(int id)
        {
            var verb = Assert.Single(session.PlayVerbArea().Items, v => v.VerbId == id && v.Mode == 1);
            Click(verb.X + Math.Min(4, verb.Width / 2), verb.Y + 3);
        }
        void Item(int id)
        {
            var inventory = session.CaptureInventoryStatus();
            var items = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
            var index = Array.FindIndex(items, i => i.ObjectId == id);
            Assert.InRange(index, 0, 3);
            Click(index % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + 8 * (index / 2));
        }
        void Object(int id)
        {
            var record = Assert.Single(data.ReadRoom(session.CurrentRoomId!.Value).Objects, o => o.Id == id);
            var x = record.X + record.Width / 2 - session.ViewLeft;
            Assert.InRange(x, 0, 319);
            Click(x, record.Y + Math.Max(1, record.Height / 2) + session.MainScreen.Top);
        }
        void Use(int item, int target)
        {
            Verb(13); // Native Walk clears any previous two-object sentence.
            Verb(11); Item(item); Object(target);
            Click(8, session.MainScreen.Bottom + 4);
        }
        // These are explicit previews/direct pickup, not an earned whole-game route.
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2));
        Until(() => session.CutsceneSkippable, 1200);
        session.PressKey(27); Until(() => session.PlayerHasControl, 1200);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2));
        session.ActivateObjectVerb(26, 14); Run(120);
        Assert.Equal(1, session.Snapshot.ObjectOwners.GetValueOrDefault(26));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 1));
        Use(26, 106); Run(180);
        Assert.Equal(1, session.Snapshot.ObjectOwners.GetValueOrDefault(18));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 3));
        Verb(13);
        for (var n = 0; n < 8 && session.ViewLeft < 560; n++)
        { Click(300, session.MainScreen.Bottom - 4); Run(180); }
        Assert.True(session.ViewLeft >= 560);
        Use(26, 142);
        Until(() => (session.Snapshot.ObjectStates.GetValueOrDefault(144) & 8) == 0, 1200);
        Run(300);
        Assert.Contains(messages, text => text.Contains("Well, climb aboard.", StringComparison.Ordinal));
        var cash = session.Snapshot.KnownGlobalVariables["var[0x03E]"];
        var ticketOwner = session.Snapshot.ObjectOwners.GetValueOrDefault(82);
        Use(wrongTool ? 26 : 18, 150);
        if (wrongTool)
        {
            Run(180);
            Assert.Equal(cash, session.Snapshot.KnownGlobalVariables["var[0x03E]"]);
            Assert.Equal(3, session.CurrentRoomId);
            Assert.Equal(3, session.Snapshot.KnownGlobalVariables[ScummVars.Room]);
        }
        else
        {
            Until(() => session.Snapshot.KnownGlobalVariables["var[0x03E]"] == cash - 8, 1800);
            Until(() => session.Snapshot.KnownGlobalVariables[ScummVars.Room] == 145, 3000);
            Until(() => session.PlayerHasControl, 1200);
            Assert.Equal(8, session.CurrentRoomId);
            Assert.Equal(145, session.Snapshot.KnownGlobalVariables[ScummVars.Room]);
            Assert.Equal(cash - 8, session.Snapshot.KnownGlobalVariables["var[0x03E]"]);
            Assert.Contains(messages, text => text.Contains("All right, let's go!", StringComparison.Ordinal));
        }
        Assert.Equal(ticketOwner, session.Snapshot.ObjectOwners.GetValueOrDefault(82));
        Assert.Equal(1, session.Snapshot.ObjectOwners.GetValueOrDefault(18));
        Assert.Equal(1, session.Snapshot.ObjectOwners.GetValueOrDefault(26));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, text => text.Contains("unsupported v2", StringComparison.OrdinalIgnoreCase));
    }
}

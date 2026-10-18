using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Own-edition source and bounded native inputs after explicitly disclosed room previews.</summary>
public sealed class ScummZakClassicFlightTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(TestGames.Root, "zak ega en");
    private static void Run(ScummGameSession s, int frames)
    { for (var frame = 0; frame < frames; frame++) s.AdvanceFrame(); }
    private static void Until(ScummGameSession s, Func<bool> ready, int bound)
    { for (var frame = 0; frame < bound && !ready(); frame++) s.AdvanceFrame(); Assert.True(ready()); }
    private static void Click(ScummGameSession s, int x, int y)
    { Assert.NotNull(s.ClickScreen(x, y, out var refusal)); Assert.Null(refusal); Run(s, 5); }
    private static void Verb(ScummGameSession s, int verb)
    { var v = Assert.Single(s.PlayVerbArea().Items, v => v.VerbId == verb && v.Mode == 1); Click(s, v.X + Math.Min(4, v.Width / 2), v.Y + 3); }
    private static void Inventory(ScummGameSession s, int obj)
    {
        var inventory = s.CaptureInventoryStatus();
        var items = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
        var slot = Array.FindIndex(items, item => item.ObjectId == obj);
        Assert.InRange(slot, 0, 3);
        Click(s, slot % 2 == 0 ? 8 : 184, s.MainScreen.Bottom + 36 + 8 * (slot / 2));
    }
    private static void UseCard(ScummGameSession s, IScummDataSource data, int tool)
    {
        Verb(s, 11); Inventory(s, tool);
        var card = Assert.Single(data.ReadRoom(1).Objects, o => o.Id == 106);
        Click(s, card.X + 4 - s.ViewLeft, card.Y + 4 + s.MainScreen.Top);
        Assert.Equal(tool == 26 ? "Use butter knife with plastic card" : "Use ticket in plastic card",
            ScummTextDecoder.DecodeGlyphs(s.PlaySentenceGlyphs()));
        Click(s, 8, s.MainScreen.Bottom + 4); Run(s, 180);
    }
    private static (GameLibrary Game, IScummDataSource Data, ScummGameSession Session) PreparedKnife(ScummScriptObserver? observer = null)
    {
        var game = new GameFolderScanner().Scan(Folder); var data = ScummDataSources.For(game);
        var s = new ScummGameSession(game, ((ScummV2DataSource)data).ReadIndexedObjects(), observer: observer, data: data); Run(s, 150);
        s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2));
        Until(s, () => s.CutsceneSkippable, 1200); s.PressKey(27); Until(s, () => s.PlayerHasControl, 600);
        s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2)); s.ActivateObjectVerb(26, 14); Run(s, 120);
        Assert.Equal(1, s.Snapshot.ObjectOwners[26]); s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 1));
        return (game, data, s);
    }

    private static ScummGameSession PreparedTerminal(ScummScriptObserver? observer = null)
    {
        var (game, data, s) = PreparedKnife(observer); UseCard(s, data, 26);
        Assert.Equal(1, s.Snapshot.ObjectOwners[18]);
        // Explicit airport preview: native logical144 is backed by physical Room8.
        // It is not an earned bus journey; the original departure145 is deliberately wrong here.
        s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 8));
        Assert.Equal(144, s.Snapshot.KnownGlobalVariables["var[0x004]"]);
        Until(s, () => s.CutsceneSkippable, 1200); s.PressKey(27); Until(s, () => s.PlayerHasControl, 1200);
        Assert.Equal(144, s.Snapshot.KnownGlobalVariables["var[0x004]"]);
        Assert.Equal(8, Assert.Single(s.Actors, actor => actor.Number == 1).Room); // Rendering snapshot uses the mapped room.
        Verb(s, 11); Click(s, 148, 92 + s.MainScreen.Top); Click(s, 148, 92 + s.MainScreen.Top);
        Until(s, () => s.CurrentRoomId == 45, 180); Run(s, 40);
        Assert.Equal(12510, s.Snapshot.KnownGlobalVariables["var[0x03E]"]); // Native refund of initial $260 ticket.
        Assert.Equal(14, s.Snapshot.ObjectOwners[82]);
        Assert.Equal(0, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Click(s, 80, 40 + s.MainScreen.Top); Run(s, 30); // Rendered San Francisco choice663.
        Assert.Equal(663, s.Snapshot.KnownGlobalVariables["var[0x04F]"]);
        return s;
    }

    [Fact]
    public void FlightWaitReadsPeanutsWhileBootSuppliesTheSeparateInitialTicket()
    {
        if (!Directory.Exists(Folder)) return;
        var game = new GameFolderScanner().Scan(Folder); var data = ScummDataSources.For(game);
        Assert.Equal("peanuts", ScummTextDecoder.DecodeGlyphs(data.ReadRoom(11).ObjectNames[51]));
        Assert.Equal("ticket", ScummTextDecoder.DecodeGlyphs(data.ReadRoom(46).ObjectNames[82]));
        Assert.Contains(data.GlobalScript(1)!.Instructions, i => i.Offset == 0x60 && i.Operation == "pickupObject" && i.Operands == "0x0052");
        Assert.Contains(data.GlobalScript(104)!.Instructions, i => i.Offset == 0x1C5 && i.Operation == "getObjectOwner" && i.Operands.Contains("0x0033"));
        Assert.Contains(data.GlobalScript(116)!.Instructions, i => i.Offset == 0x25 && i.Operation == "pickupObject" && i.Operands == "0x0033");
        var s = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(1, s.Snapshot.ObjectOwners[82]); Assert.Equal(15, s.Snapshot.ObjectOwners[51]);
        Assert.Equal(145, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Assert.Equal(662, s.Snapshot.KnownGlobalVariables["var[0x04F]"]);
    }

    [Theory]
    [InlineData(26, true)]
    [InlineData(82, false)]
    public void NativeCashCardRecoveryHonorsTheHeldToolsOwnHeaderMask(int tool, bool recovers)
    {
        if (!Directory.Exists(Folder)) return;
        var masks = new List<ScummScriptObservation>();
        var (_, data, s) = PreparedKnife(new ScummScriptObserver(masks.Add,
            site => site.Operation == "ifClassOfIs" && site.Context?.Contains("Object 93") == true,
            key => key == "var[0x00A]"));
        var cash = s.Snapshot.KnownGlobalVariables["var[0x03E]"];
        UseCard(s, data, tool);
        Assert.Equal(recovers ? 1 : 15, s.Snapshot.ObjectOwners[18]);
        Assert.Equal(1, s.Snapshot.ObjectOwners[82]); Assert.Equal(1, s.Snapshot.ObjectOwners[26]);
        Assert.Equal(cash, s.Snapshot.KnownGlobalVariables["var[0x03E]"]);
        Assert.Contains(masks, observed => observed.Site is { Stage: "instruction", Offset: 0x26 } &&
            observed.Values.GetValueOrDefault("var[0x00A]") == tool);
        Assert.DoesNotContain(s.Snapshot.Uncertainties, line => line.Contains("unsupported v2 ifClassOfIs"));
        output.WriteLine($"tool {tool}, card recovered {recovers}, cash {cash}, ticket retained");
    }

    [Fact]
    public void InitialTicketFromAnotherAirportIsRefusedWithoutConsumption()
    {
        if (!Directory.Exists(Folder)) return;
        var game = new GameFolderScanner().Scan(Folder); var s = ScummGameSession.BootForPlay(game, out _); Run(s, 150);
        s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 8));
        Assert.Equal(144, s.Snapshot.KnownGlobalVariables["var[0x004]"]);
        s.ActivateObjectVerb(190, 13);
        var lines = new HashSet<string>();
        for (var frame = 0; frame < 60; frame++)
        { foreach (var m in s.Messages.OnScreen) lines.Add(m.Text); s.AdvanceFrame(); }
        Assert.Contains(lines, line => line.Contains("ticket's from a different"));
        Assert.Equal(1, s.Snapshot.ObjectOwners[82]);
        Assert.Equal(145, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Assert.Equal(662, s.Snapshot.KnownGlobalVariables["var[0x04F]"]);
        output.WriteLine("Explicit Room8 seeded airport144 rejects initial departure145; ticket/route preserved.");
    }

    [Fact]
    public void NativePurchaseBoardsAndMealServiceReleasesTheFlightWait()
    {
        if (!Directory.Exists(Folder)) return;
        var observed = new List<ScummScriptObservation>();
        var s = PreparedTerminal(new ScummScriptObserver(observed.Add,
            site => site.Script is 104 or 115 or 116, key => key == "var[0x001]"));
        Click(s, 80, 108 + s.MainScreen.Top); Run(s, 240); // Native Buy657; no supplied cash/route state.
        Assert.Equal(8, s.CurrentRoomId); Assert.Equal(1, s.Snapshot.ObjectOwners[82]);
        Assert.Equal(144, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Assert.Equal(663, s.Snapshot.KnownGlobalVariables["var[0x04F]"]);
        Assert.Equal(260, s.Snapshot.KnownGlobalVariables["var[0x052]"]);
        Assert.Equal(12250, s.Snapshot.KnownGlobalVariables["var[0x03E]"]);
        Verb(s, 13); Click(s, 20, 60 + s.MainScreen.Top); Run(s, 2500);
        Assert.Equal(11, s.CurrentRoomId); Assert.True(s.PlayerHasControl); Assert.False(s.InCutscene);
        Assert.Equal(14, s.Snapshot.ObjectOwners[82]); Assert.Equal(0, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Assert.Equal(1, s.Snapshot.ObjectOwners[51]);
        Assert.Contains(observed, o => o.Site is { Script: 115, Stage: "instruction", Offset: 0x7E, Operation: "setOwnerOf" });
        Assert.Contains(observed, o => o.Site is { Script: 116, Stage: "instruction", Offset: 0x25, Operation: "pickupObject" });
        Assert.Contains(observed, o => o.Site is { Script: 104, Stage: "instruction", Offset: 0x1CF });
        Assert.DoesNotContain(s.PendingScriptSummary, line => line.StartsWith("Script 104 "));
        Assert.DoesNotContain(s.Snapshot.Uncertainties, line => line.Contains("unsupported v2"));
        output.WriteLine("Native Buy/purchase/departure144; 2500-frame gate bound reaches Room11/control, consumed ticket, served peanuts and passed Script104 guard.");
    }

    [Fact]
    public void NativeCancellationRefundsTheOldTicketAndRejectsBoarding()
    {
        if (!Directory.Exists(Folder)) return;
        var s = PreparedTerminal();
        Click(s, 240, 108 + s.MainScreen.Top); Run(s, 240); // Native Cancel658 after choosing a candidate.
        Assert.Equal(8, s.CurrentRoomId); Assert.Equal(144, s.Snapshot.KnownGlobalVariables["var[0x004]"]);
        Assert.Equal(12510, s.Snapshot.KnownGlobalVariables["var[0x03E]"]);
        Assert.Equal(14, s.Snapshot.ObjectOwners[82]); Assert.Equal(0, s.Snapshot.KnownGlobalVariables["var[0x09B]"]);
        Assert.Equal(0, s.Snapshot.KnownGlobalVariables["var[0x052]"]);
        Assert.Equal(663, s.Snapshot.KnownGlobalVariables["var[0x04F]"]); // Native candidate remains; it is not a valid ticket.
        Verb(s, 13); Click(s, 20, 60 + s.MainScreen.Top);
        var lines = new HashSet<string>();
        for (var frame = 0; frame < 120; frame++)
        { foreach (var m in s.Messages.OnScreen) lines.Add(m.Text); s.AdvanceFrame(); }
        Assert.Contains(lines, line => line.Contains("I need a ticket first."));
        Assert.Equal(8, s.CurrentRoomId); Assert.False(s.InCutscene);
        Assert.Equal(15, s.Snapshot.ObjectOwners[51]);
        Assert.Equal(12510, s.Snapshot.KnownGlobalVariables["var[0x03E]"]);
        output.WriteLine("Cancel leaves native candidate663 but no departure/ticket; initial $260 refunded, no fare charged, boarding refused.");
    }
}

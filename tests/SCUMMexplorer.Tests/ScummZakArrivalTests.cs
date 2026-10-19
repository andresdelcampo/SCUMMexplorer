using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Disclosed exploration preparation, then native purchase, flight timer and automatic arrival.</summary>
public sealed class ScummZakArrivalTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void NativeCruisingTimerMustExpireBeforeAutomaticDisembarkAtThePurchasedAirport(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var timerObject = data.ReadRoom(11).Objects.Last(o => o.Id == 241);
        var timer = data.ReadObjectVerb(11, timerObject, 253, out _);
        Assert.Equal("28800", Assert.Single(timer.Instructions, i => i.Operation == "delay").Operands);
        var observed = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(observed.Add,
            site => site.Script == 79 || site.Context?.Contains("Object 241", StringComparison.Ordinal) == true ||
                site.Context?.Contains("Object 256", StringComparison.Ordinal) == true,
            key => key is "var[0x02C]" or "var[0x02D]" or "var[0x035]");
        var s = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        void Frames(int count) { for (var f = 0; f < count; f++) s.AdvanceFrame(); }
        void Visit(int room) => s.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        void Click(int x, int y)
        { Assert.NotNull(s.ClickScreen(x, y, out var why)); Assert.Null(why); Frames(2); }
        void RoomClick(int x, int y) => Click(x - s.ViewLeft, y + s.MainScreen.Top);
        void Verb(int id)
        { var v = s.PlayVerbArea().Items.Single(v => v.VerbId == id && v.Mode == 1); Click(v.X + 4, v.Y + 3); }
        void SkipReachedAlien()
        {
            for (var f = 0; f < 1200 && !s.CutsceneSkippable; f++) Frames(1);
            Assert.True(s.CutsceneSkippable); s.PressKey(27);
            for (var f = 0; f < 1200 && s.InCutscene; f++) Frames(1);
            Assert.False(s.InCutscene);
        }
        int Word(int id) => s.Snapshot.KnownGlobalVariables[$"var[0x{id:X3}]"];

        // Same disclosed room/tool preparation as the accepted 18 October flight route.
        Frames(150); Visit(2); Frames(300); SkipReachedAlien(); Visit(2); Frames(100);
        Assert.NotNull(s.ActivateObjectVerb(26, 14)); Frames(100);
        Visit(1); Frames(100); Verb(11);
        var inventory = s.CaptureInventoryStatus();
        var owned = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
        var knife = Array.FindIndex(owned, i => i.ObjectId == 26);
        Assert.InRange(knife, 0, 3);
        Click((knife % 2) * 176 + 8, s.MainScreen.Bottom + 36 + (knife / 2) * 8);
        RoomClick(248, 90); RoomClick(248, 90); Frames(180);
        Assert.Equal(1, s.Snapshot.ObjectOwners[18]);
        Visit(8); Frames(100); SkipReachedAlien(); Frames(100);
        Assert.Equal(144, Word(4));
        Verb(11); RoomClick(144, 80); RoomClick(144, 80); Frames(180);
        Assert.Equal(45, s.CurrentRoomId);
        RoomClick(80, 40); Frames(30); Assert.Equal(663, Word(79));
        RoomClick(80, 108); Frames(200);
        Assert.Equal(144, Word(155)); Assert.Equal(1, s.Snapshot.ObjectOwners[82]);
        Verb(13); RoomClick(20, 60); Frames(2500);
        Assert.Equal(11, s.CurrentRoomId); Assert.True(s.PlayerHasControl);
        Assert.Equal(14, s.Snapshot.ObjectOwners[82]); Assert.Equal(0, Word(155));
        Assert.Equal(1, s.Snapshot.ObjectOwners[51]);

        int RemainingDelay()
        {
            var pending = Assert.Single(s.PendingScriptSummary, p => p.StartsWith("Room 11 / Object 241 /", StringComparison.Ordinal));
            var delay = Regex.Match(pending, @"\(delay (\d+)\)");
            Assert.True(delay.Success, pending);
            return int.Parse(delay.Groups[1].Value);
        }
        var remaining = RemainingDelay();
        Assert.InRange(remaining, 1, 28800);
        var delta = Math.Min(s.FrameJiffies, 15);
        var beforeExpiry = remaining / delta;
        Frames(beforeExpiry);
        Assert.Equal(11, s.CurrentRoomId); Assert.Equal(11, Word(4));
        Assert.Equal(remaining % delta, RemainingDelay());
        Assert.DoesNotContain(observed, o => o.Site is { Script: 79, Operation: "cutscene" });
        Assert.Equal(0, Word(53));

        // No further input, Skip, room visit or timer edit: native delay expiry starts arrival.
        Frames(1);
        for (var f = 0; f < 2000 && (s.CurrentRoomId == 11 || !s.PlayerHasControl); f++) Frames(1);
        Assert.Equal(8, s.CurrentRoomId);
        Assert.Equal(145, Word(4)); // Script120 maps purchased destination663 to native airport145.
        Assert.True(s.PlayerHasControl); Assert.False(s.InCutscene);
        Assert.DoesNotContain(s.PendingScriptSummary, p => p.StartsWith("Room 11 / Object 241 /", StringComparison.Ordinal) ||
            p.StartsWith("Script 79 ", StringComparison.Ordinal));
        Assert.Contains(observed, o => o.Site is { Operation: "increment", Operands: "var[0x035]" } &&
            o.Site.Context?.Contains("Object 241", StringComparison.Ordinal) == true);
        Assert.Contains(observed, o => o.Site is { Operation: "loadRoomWithEgo" } &&
            o.Values.GetValueOrDefault("var[0x02C]") == 145 && o.Values.GetValueOrDefault("var[0x02D]") == 190);
        Assert.Equal(14, s.Snapshot.ObjectOwners[82]); Assert.Equal(0, Word(155));
        Assert.Equal(1, s.Snapshot.ObjectOwners[51]); Assert.Equal(12250, Word(62));
        Assert.Contains(s.CaptureInventoryStatus().Items, i => i.ObjectId == 51 && i.RetainedRoomId == 11);
        Assert.DoesNotContain(s.Snapshot.Uncertainties, u => u.Contains("unsupported v2", StringComparison.Ordinal));
        output.WriteLine($"{fixture}: native 28800-jiffy timer, observed remainder {remaining}, early boundary after {beforeExpiry} frames stays cabin; next tick/landing reaches logical 145/physical 8 with control, consumed ticket and retained peanuts.");
    }
}

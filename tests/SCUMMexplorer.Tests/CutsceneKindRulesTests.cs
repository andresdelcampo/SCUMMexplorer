namespace SCUMMexplorer.Tests;

/// <summary>R-8.10 (user, 9 November 2026): what makes a run a cutscene, and the game's stock responses that do not.</summary>
public sealed class CutsceneKindRulesTests
{
    private static readonly Func<string, bool> Stock = place => place == "Script 41 +0x004C";

    [Fact]
    public void AnAnimationOnlyFromTheStockResponseIsAReaction()
    {
        // LOOM's Other draft: Script 41's sparkle at the object, then "that draft won't work".
        var stockOnly = new ScummCutsceneRunResult(true, [], 90, Lines: 1, Animation: "Script 41 +0x004C animateActor", Animations: ["Script 41 +0x004C"]);
        Assert.False(stockOnly.ShowsBeyond(Stock, widened: false));
        // The room's own response as well (LOOM Room 06's wool dyed by local script 200): a cutscene.
        var own = stockOnly with { Animations = ["Script 41 +0x004C", "Room 06 local script 200 +0x0036"] };
        Assert.True(own.ShowsBeyond(Stock, widened: false));
    }

    [Fact]
    public void AnotherRoomIsACutsceneWhateverAnimatesAndAWidenedOneNeedsItsOwnView()
    {
        var goes = new ScummCutsceneRunResult(true, [65], 200, ChangesRoom: true, Animations: ["Script 41 +0x004C"]);
        Assert.True(goes.ShowsBeyond(Stock, widened: false));
        // A widened candidate (no control taken): the hero walking through a door to another room is navigation; its own view of a room counts.
        Assert.False(goes.ShowsBeyond(Stock, widened: true));
        Assert.True((goes with { ChangesView = true }).ShowsBeyond(Stock, widened: true));
    }

    [Fact]
    public void ARunThatDidNotBeginOrOnlyPrintsIsNotACutscene()
    {
        Assert.False(new ScummCutsceneRunResult(false, [3], 600, ChangesRoom: true).ShowsBeyond(Stock, widened: false));
        Assert.False(new ScummCutsceneRunResult(true, [], 80, Lines: 4, Animations: []).ShowsBeyond(Stock, widened: false));
        // A run kept from an older cache, without its places: its first animation counts.
        Assert.True(new ScummCutsceneRunResult(true, [], 80, Animation: "Room 28 local script 218 +0x0047 putActor").ShowsBeyond(Stock, widened: false));
    }
}

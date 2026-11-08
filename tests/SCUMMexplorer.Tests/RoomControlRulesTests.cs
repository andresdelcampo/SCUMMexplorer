using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>The shared room-control rules answered by the user on 24 October 2026 (R-6.32, R-6.36, R-6.39, R-7.16).</summary>
public sealed class RoomControlRulesTests(ITestOutputHelper output)
{
    [Fact]
    public void RepeatedHeadingsAreNumberedFromTheSecondAndNothingElseChanges()
    {
        Assert.Equal(["Door", "Lamp", "Door 2", "door 3", "Unnamed object", "Unnamed object 2"],
            RoomInteractionRules.NumberRepeats(["Door", "Lamp", "Door", "door", "Unnamed object", "Unnamed object"]));
        Assert.Empty(RoomInteractionRules.NumberRepeats([]));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(24, true)]
    [InlineData(25, false)]
    [InlineData(83, false)]
    public void TheGroupsOfASmallOverviewStartOpenAndThoseOfALargeOneCollapsed(int actions, bool open) =>
        Assert.Equal(open, RoomInteractionRules.GroupsStartOpen(actions));

    [Fact]
    public void TheOverviewCountsEveryRowOfEveryObject()
    {
        static RoomInteractionChoice Row(string label) => new(label, null, () => { });
        var overview = new RoomInteractionOverview([new("Door", false, [Row("Open"), Row("Look at")]), new("Lamp", true, [Row("Pick up")])]);
        Assert.Equal(3, overview.Actions);
    }

    [Theory]
    [InlineData("loom ega mt32 en", false)]
    [InlineData("mmansion ega en", false)]
    [InlineData("zak ega en", false)]
    [InlineData("indy3 ega en", false)]
    [InlineData("indy3 vga en", false)]
    public void WhereTheGamesOwnInputNeverReadsTheRightButtonItIsTheAppsShortcut(string folder, bool reaches)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        // Indiana Jones 3 on 256 colours stores v4-style blocks but ScummVM runs it as engine version 3: its right press is not the game's.
        Assert.Equal(reaches, ScummProfile.For(new GameFolderScanner().Scan(path)).RightClickReachesGame);
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("zak ega en")]
    public void NormalTitlesOfTheRoomListsCarryNoTechnicalInformation(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(path));
        // Whole words: Maniac Mansion's manuscript is a game object, not evidence (8 November 2026).
        var technical = new Regex(@"0x|\bverb\b|\bscript\b|\bobject \d|\d{3}", RegexOptions.IgnoreCase);
        var count = 0;
        foreach (var group in planner.Groups)
        {
            var title = ScummCutsceneTitles.Plain(group.First);
            count++;
            // A title may name a game object ("Room 01 · Open front door"); it never carries ids, hex or the words of the evidence.
            Assert.False(technical.IsMatch(title.Replace("Room ", "")), title);
        }
        output.WriteLine($"{folder}: {count} titles without technical information");
        Assert.True(count > 0);
    }
}

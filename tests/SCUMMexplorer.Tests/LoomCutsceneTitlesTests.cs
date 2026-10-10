using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.Tests;

public class LoomCutsceneTitlesTests
{
    [Fact]
    public void FormatAddsOneRoomPrefixAndDropsTheResponseLeadOfRoomChanges()
    {
        Assert.Equal("Room 02 · Reached Room 03", LoomCutsceneTitles.Format(2, "Room 02 · Use response · Reached Room 03"));
        Assert.Equal("Room 02 · Look at leaf", LoomCutsceneTitles.Format(2, "Look at leaf"));
        Assert.Equal("Room 02 · Look at leaf", LoomCutsceneTitles.Format(2, "Room 02 · Look at leaf"));
    }

    [Fact]
    public void TitlesNameTheVerbAndObjectLikeTheEgaCatalog()
    {
        static LoomCutscene T(int obj, string name, int verb, params int[] to) => new(5, obj, name, verb, ["s"], 0, 0, 1, "", to);
        Assert.Equal("Room 05 · Look at leaf", LoomCutsceneTitles.Title(T(1, "leaf", 0x36)));
        Assert.Equal("Room 05 · Use response", LoomCutsceneTitles.Title(T(1, "", 0x38)));
        Assert.Equal("Room 05 · Opening at sky", LoomCutsceneTitles.Title(T(1, "sky", 0x3A)));
        Assert.Equal("Room 05 · Closing at sky", LoomCutsceneTitles.Title(T(1, "sky", 0x3B)));
        Assert.Equal("Room 05 · Reached Rooms 03, 07", LoomCutsceneTitles.Title(T(1, "door", 0x38, 3, 7)));
        Assert.Equal("Room 05 · Entry", LoomCutsceneTitles.Title(T(0, "", 0)));
    }

    [Fact]
    public void ARoomChangeMakesAScene()
    {
        var withDoor = new LoomCutscene(5, 1, "door", 0x38, ["s"], 1, 0, 9, "", [3]);
        var talk = new LoomCutscene(5, 2, "elder", 0x36, ["s"], 14, 0, 78, "");
        Assert.True(new LoomCutsceneGroup(withDoor, []).IsScene);
        Assert.False(new LoomCutsceneGroup(talk, []).IsScene);
    }
}

public class LoomReferenceCatalogTitleTests
{
    private static string CatalogPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SCUMMexplorer.slnx")))
                return Path.Combine(dir.FullName, "src", "SCUMMexplorer.App", "loom-replays.json");
        throw new FileNotFoundException("Repository root not found.");
    }

    [Fact]
    public void EveryCatalogTitleHasExactlyOneMatchingRoomPrefix()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(CatalogPath()));
        var replays = document.RootElement.GetProperty("replays").EnumerateArray().ToArray();
        Assert.NotEmpty(replays);
        foreach (var replay in replays)
        {
            var room = replay.GetProperty("room").GetInt32();
            var verb = replay.GetProperty("verb").GetInt32();
            var shown = LoomCutsceneTitles.Format(room, LoomDraftNames.ReplayTitle(replay.GetProperty("title").GetString()!, verb));
            var prefix = $"Room {room:00} · ";
            Assert.StartsWith(prefix, shown);
            Assert.DoesNotContain(" · Room ", shown[prefix.Length..]); // no second "Room NN ·" prefix inside
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(shown, @"^Room \d\d · "));
            Assert.Equal(shown, LoomCutsceneTitles.Format(room, shown)); // formatting twice must not double the prefix
            Assert.DoesNotContain("Draft response", shown);
        }
    }
}

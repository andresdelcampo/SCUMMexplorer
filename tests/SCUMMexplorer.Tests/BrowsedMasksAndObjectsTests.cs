using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The depth masks and stored objects of the browsed games (generic SCUMM parsing, no per-game data). The same absolute-folder variables as <see cref="OtherV4GameTests"/>;
/// a check is skipped without its variable. Counts are what the local copies give (11 October 2026); the EGA and VGA Monkey Island agree.
/// </summary>
public class BrowsedMasksAndObjectsTests
{
    private static GameLibrary? Open(string variable)
    {
        var folder = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(folder) ? null : new GameFolderScanner().Scan(folder);
    }

    private static (int Rooms, int Masked, int Objects, int Images, int Named) Survey(GameLibrary game)
    {
        int rooms = 0, masked = 0, objects = 0, images = 0, named = 0;
        foreach (var resource in game.Rooms)
        {
            var archive = game.V4ArchiveFor(resource)!;
            var room = archive.DecodeRoom(resource.ResourceId!.Value);
            Assert.True(room.IsValid);
            rooms++;
            if (room.DepthMask is not null) masked++;
            var stored = archive.ReadBrowsedObjects(resource.ResourceId!.Value);
            objects += stored.Records.Count;
            images += stored.Images.Count;
            named += stored.Names.Count;
            // Every image has a unique entry id and belongs to a stored object.
            Assert.Equal(stored.Images.Count, stored.Images.Select(i => i.Id).Distinct().Count());
            Assert.All(stored.Images, image => Assert.Contains(stored.Records, r => r.Id == BrowsedObjects.BaseId(image.Id)));
        }
        return (rooms, masked, objects, images, named);
    }

    [Fact]
    public void ReadsMasksAndObjectsOfTheSixteenAndTheTwoFiftySixColourMonkeyIsland()
    {
        foreach (var variable in new[] { "SCUMM_MONKEY1_VGA_TEST_ROOT", "SCUMM_MONKEY1_EGA_TEST_ROOT" })
        {
            if (Open(variable) is not { } game) continue;
            var (rooms, masked, objects, images, named) = Survey(game);
            Assert.Equal(81, rooms);
            Assert.Equal(57, masked);   // rooms with a first depth-mask plane: the same in EGA and VGA
            Assert.Equal(606, images);  // the EGA object bitmaps decode like the VGA ones
            Assert.InRange(objects, 938, 939);   // the 256-colour copy lists one more object
            Assert.Equal(458, named);
        }
    }

    [Fact]
    public void ReadsMasksAndObjectsOfTheTwoFiftySixColourIndianaJones()
    {
        if (Open("SCUMM_INDY3_VGA_TEST_ROOT") is not { } game) return;
        var (rooms, masked, _, images, named) = Survey(game);
        Assert.Equal(87, rooms);
        Assert.Equal(62, masked);
        Assert.Equal(624, images);
        Assert.Equal(348, named);
    }

    [Fact]
    public void ReadsMasksOfTheOlderGamesWhenConfigured()
    {
        // v2 enhanced and v1 classic: the masks follow the picture (v2) or are tile characters (v1); every room carries a plane and some are not empty.
        foreach (var variable in new[] { "SCUMM_V2_ENHANCED_TEST_ROOT", "SCUMM_V1_CLASSIC_TEST_ROOT" })
        {
            if (Open(variable) is not { } game) continue;
            var bytes = game.Rooms.Select(r => File.ReadAllBytes(r.FilePath)).ToArray();
            var rooms = bytes.Select(b => game.Version == ScummVersion.V1 ? new ScummV1RoomDecoder(game.V1Game!.Value).Decode(b) : new ScummV2RoomDecoder().Decode(b)).ToArray();
            Assert.All(rooms, room => Assert.True(room.IsValid));
            Assert.All(rooms, room => Assert.NotNull(room.DepthMask));
            Assert.Contains(rooms, room => room.DepthMask!.Pixels.Span.IndexOfAnyExcept((byte)0) >= 0);
        }
    }

    [Fact]
    public void ReadsMasksAndObjectsOfAV5GameWhenConfigured()
    {
        if (Open("SCUMM_V5_TEST_ROOT") is not { } game) return;
        var (rooms, masked, objects, images, named) = Survey(game);
        Assert.True(rooms > 50);
        Assert.True(masked > 30);
        Assert.True(objects > 500 && images > 400 && named > 200);
    }
}

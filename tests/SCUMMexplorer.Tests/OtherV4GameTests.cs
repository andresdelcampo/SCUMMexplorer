using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A v4 game that is not LOOM CD is browsed from all its disks. Set <c>SCUMM_MONKEY1_VGA_TEST_ROOT</c> (and, for the 16-colour copy,
/// <c>SCUMM_MONKEY1_EGA_TEST_ROOT</c>; for the 256-colour Indiana Jones, <c>SCUMM_INDY3_VGA_TEST_ROOT</c>) to the absolute folders (<c>Games/FUTURE/monkey1/VGA</c>, <c>…/EGA</c>); the checks are skipped without them.
/// </summary>
public class OtherV4GameTests
{
    [Fact]
    public void BrowsesEveryDiskOfAnotherV4GameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_MONKEY1_VGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        Assert.Null(game.V4Archive);
        Assert.Equal(4, game.V4Disks.Disks.Count);
        // Every room is on exactly one disk, and the resource says which: its decoded background is a valid picture.
        var rooms = game.Rooms.ToArray();
        Assert.Equal(rooms.Length, rooms.Select(r => r.ResourceId).Distinct().Count());
        Assert.All(rooms.Where(r => r.ResourceId is 10 or 33 or 45 or 1), room =>
        {
            var archive = game.V4ArchiveFor(room);
            Assert.NotNull(archive);
            Assert.True(archive.DecodeRoom(room.ResourceId!.Value).IsValid);
        });
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        // Every sound is an SO block with both an AdLib and a PC speaker part, across all four disks.
        var sounds = game.V4Disks.Disks.SelectMany(d => d.ReadSounds()).ToArray();
        Assert.Equal(90, sounds.Length);
        Assert.All(sounds, sound => { Assert.NotNull(sound.Adlib); Assert.NotNull(sound.Speaker); });
    }

    [Fact]
    public void BrowsesTheRoomFilesOfTheSuppliedIndianaJones256ColourGameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_VGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        var disk = Assert.Single(game.V4Disks.Disks);
        // The 87 room files whose picture decodes, through the four older 256-colour strip codes (2, 3, 4 and 7) as well as raw and bit-coded strips.
        var rooms = game.Rooms.ToArray();
        Assert.Equal(87, rooms.Length);
        Assert.All(rooms, room => Assert.True(disk.DecodeRoom(room.ResourceId!.Value).IsValid));
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        Assert.DoesNotContain(rooms, room => room.ResourceId is 98 or 99);
        // The sounds sit in a second SO block inside the first; the parts are found through it.
        var sounds = disk.ReadSounds();
        Assert.Equal(73, sounds.Count);
        // The limb commands of these costumes are bytes (LOOM CD stores words): read as words the side-walking poses of the guard (costume 11) drew a second head.
        var guard = disk.FindCostume(11);
        if (guard is not null) Assert.True(disk.DecodeCostumePoses(guard).Count >= 30);
        Assert.All(sounds, sound => Assert.NotNull(sound.Adlib));
    }

    [Fact]
    public void BrowsesTheSixteenColourVersionWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_MONKEY1_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        Assert.Equal(8, game.V4Disks.Disks.Count);
        // The rooms have no PA block: the fixed EGA palette (16 colours) and the old-v3 strip code.
        var rooms = game.Rooms.ToArray();
        Assert.NotEmpty(rooms);
        Assert.All(rooms.Where(r => r.ResourceId is 10 or 33 or 34), room =>
        {
            var image = game.V4ArchiveFor(room)!.DecodeRoom(room.ResourceId!.Value);
            Assert.True(image.IsValid);
            Assert.All(image.Pixels.ToArray(), pixel => Assert.True(pixel < 16));
        });
    }

    [Fact]
    public void BrowsesAnEnhancedV2GameWhenConfigured()
    {
        // Maniac Mansion Enhanced or Zak McKracken Enhanced: the index gives one byte per object, the room picture is one run-length stream.
        var folder = Environment.GetEnvironmentVariable("SCUMM_V2_ENHANCED_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V2, game.Version);
        var rooms = game.Rooms.ToArray();
        Assert.NotEmpty(rooms);
        Assert.DoesNotContain(rooms, room => room.ResourceId == 0);
        var decoder = new ScummV2RoomDecoder();
        Assert.All(rooms, room =>
        {
            var image = decoder.Decode(File.ReadAllBytes(room.FilePath));
            Assert.True(image.IsValid);
            Assert.All(image.Pixels.ToArray(), pixel => Assert.True(pixel < 16));
        });
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Script);
    }

    [Fact]
    public void BrowsesAClassicV1GameWhenConfigured()
    {
        // Maniac Mansion or Zak McKracken: the fixed-size index, the tile rooms and the 0x57 costumes.
        var folder = Environment.GetEnvironmentVariable("SCUMM_V1_CLASSIC_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V1, game.Version);
        Assert.NotNull(game.V1Game);
        var rooms = game.Rooms.ToArray();
        Assert.NotEmpty(rooms);
        var decoder = new ScummV1RoomDecoder(game.V1Game.Value);
        Assert.All(rooms, room =>
        {
            var image = decoder.Decode(File.ReadAllBytes(room.FilePath));
            Assert.True(image.IsValid);
            Assert.All(image.Pixels.ToArray(), pixel => Assert.True(pixel < 16));
        });
        // The classic costumes (format 0x57) are drawn in the colours of the actor the explorer assumes; every offered costume has a picture.
        var costumes = game.Resources.Where(r => r.Kind == ResourceKind.Costume).ToArray();
        Assert.NotEmpty(costumes);
        var reader = new ScummV3CostumeReader();
        var renderer = new ScummCostumeRenderer();
        Assert.All(costumes, costume =>
        {
            var container = LflEncoding.DecodeXorFF(File.ReadAllBytes(costume.FilePath));
            var slots = reader.Decode(container, costume.Offset!.Value).AnimationSlots.Where(s => s.HasData);
            Assert.Contains(slots, slot => renderer.DecodePose(container, costume.Offset.Value, slot.Index, game.V1Game, costume.ResourceId ?? 0) is not null);
        });
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Script);
    }

    [Fact]
    public void BrowsesAV5GameFromFilesOrADiscWhenConfigured()
    {
        // Indy 4, Monkey Island 1 CD or 2: NAME.000 and NAME.001 (on a disc, in a language or game folder; the game with the most rooms is taken).
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V5, game.Version);
        var archive = Assert.Single(game.V4Disks!.Disks);
        var rooms = game.Rooms.ToArray();
        Assert.True(rooms.Length >= 50);
        // The pictures use zigzag, major-minor and raw strip codes; costumes of 16 or 32 colours.
        Assert.All(rooms, room => Assert.True(archive.DecodeRoom(room.ResourceId!.Value).IsValid));
        var costumes = game.Resources.Where(r => r.Kind == ResourceKind.Costume).ToArray();
        Assert.True(costumes.Length >= 100);
        Assert.NotEmpty(archive.DecodeCostumePoses(archive.FindCostume(costumes[0].ResourceId!.Value)!));
        Assert.DoesNotContain(game.Resources, r => r.Kind is ResourceKind.Sound or ResourceKind.Script);
    }

    [Fact]
    public void ExtractsTextsOfTheIndy256ColourGameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_VGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var result = new LoomTextExtractor().Extract(game);
        Console.WriteLine($"Indy3 256: {result.Entries.Count} texts; {result.Coverage}");
        foreach (var e in result.Entries.Where(e => e.Kind == "Spoken line").Take(8)) Console.WriteLine($"  [{e.Kind}] {e.Text}");
        Assert.True(result.Entries.Count > 500);
    }

    [Theory]
    [InlineData("SCUMM_V2_ENHANCED_TEST_ROOT", 1000)]
    [InlineData("SCUMM_V1_CLASSIC_TEST_ROOT", 1000)]
    public void ReadsTheScriptsOfTheOlderGamesWhenConfigured(string variable, int minimumTexts)
    {
        var folder = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
        var scripts = game.Resources.Where(r => r.Kind == ResourceKind.Script).ToArray();
        Assert.True(scripts.Length > 100);
        // A script header of the wrong size decodes one instruction per script; the real thing decodes thousands, almost all with full coverage.
        var decoded = scripts.Select(reader.Decode).ToArray();
        Assert.True(decoded.Sum(d => d.Instructions.Count) > 3000);
        Assert.True(decoded.Count(d => d.Issues is { Count: 0 } && d.DecodedBytes >= d.CodeLength - 1) * 10 >= scripts.Length * 8);
        var texts = new SCUMMexplorer.Core.Loom.LoomTextExtractor().Extract(game);
        Assert.True(texts.Entries.Count >= minimumTexts, $"Only {texts.Entries.Count} texts.");
        Assert.Contains(texts.Entries, e => e.Kind == "Spoken line");
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

/// <summary>
/// R-6.23 / R-6.41 (user, 24 Oct 2026: "MM Doorways not visible until Play Game Intro is used"): a static preview (no session) finds the doorways of a
/// Maniac Mansion or Zak room from the room's own verb scripts with the data source the game's version selects, exactly as
/// <c>MainWindow.FindBrowsedDoorways</c> does; before the fix that browse path was limited to engine versions 3 and 4.
/// </summary>
public class ScummStaticDoorwayTests
{
    public static IEnumerable<object[]> Folders() =>
    [
        ["mmansion ega en"], ["mmansionE ega es"], ["zak ega en"], ["zakE ega en"]
    ];

    private static int DoorwaysOf(GameLibrary game, GameResource room)
    {
        var data = ScummDataSources.For(game);
        var records = data.ReadRoom(room.ResourceId!.Value).Objects;
        var verbs = data.ReadObjectVerbs(room.ResourceId!.Value, records);
        var finder = new ScummDoorwayFinder(game, data);
        var found = 0;
        foreach (var record in records)
            foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
                try { found += finder.Find(room, data.ReadObjectVerb(room.ResourceId!.Value, record, verb.VerbId, out _)).Count; }
                catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException) { }
        return found;
    }

    [Theory]
    [MemberData(nameof(Folders))]
    public void AStaticPreviewFindsTheRoomsOwnDoorways(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return; // no local copy of this game
        var game = new GameFolderScanner().Scan(path);
        Assert.True(game.Version is ScummVersion.V1 or ScummVersion.V2, "these are the version 1 and 2 games the browse path used to skip");
        var perRoom = game.Rooms.Where(room => room.ResourceId is not null).ToDictionary(room => room.ResourceId!.Value, room => DoorwaysOf(game, room));
        // A mansion or a spaceport has many rooms that lead on; a handful at least must show their doorways.
        Assert.True(perRoom.Values.Count(count => count > 0) >= 5, string.Join(", ", perRoom.Where(p => p.Value > 0).Select(p => $"{p.Key}:{p.Value}")));
    }
}

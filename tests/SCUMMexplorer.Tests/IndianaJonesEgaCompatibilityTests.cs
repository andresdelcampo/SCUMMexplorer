using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class IndianaJonesEgaCompatibilityTests
{
    [Fact]
    public void DecodesSuppliedIndianaJonesEgaRoomBackgroundsWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.IndianaJones3, game.Id);
        Assert.Equal(ScummVersion.V3, game.Version);
        var rooms = game.Rooms.ToArray();
        Assert.NotEmpty(rooms);

        var decoder = new ScummV3EgaRoomDecoder();
        foreach (var room in rooms)
        {
            var image = decoder.Decode(File.ReadAllBytes(room.FilePath));
            Assert.True(image.IsValid, $"Room {room.ResourceId} did not produce a valid image.");
        }
    }
}

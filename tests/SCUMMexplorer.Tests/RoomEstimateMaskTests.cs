using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class RoomEstimateMaskTests
{
    /// <summary>
    /// Room 02's object 157 (the stalactite's tip, 544..560 x 88..120, a solid mask) is state 0 in the game, but the artwork
    /// estimate shows its image to close a gap. The estimate must not change the depth mask: with it, Bobbin was cut along the
    /// image's vertical edge at x = 560, earlier than ScummVM, which cuts him only at the room mask's own edge (about x 548).
    /// </summary>
    [Fact]
    public void ArtworkEstimateDoesNotPaintItsMaskOverTheRoomMask()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var path = Path.Combine(folder, "02.LFL");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        var room = new OldV3EgaRoomDecoder().Decode(bytes);
        var records = new LoomRoomObjectDirectory().Decode(bytes);
        var images = new LoomRoomObjects().Decode(bytes);
        if (images.All(image => image.Id != 157)) return; // another edition's numbering
        var states = images.ToDictionary(image => image.Id, _ => (byte)0);
        var selection = new RoomObjectSelection(images, states, null, records, room);
        var plain = selection.ComposeDepthMask(room)!;
        selection.ShowUnresolvedArtwork();
        var estimated = selection.ComposeDepthMask(room)!;
        Assert.True(images.Any(selection.IsActive), "the estimate shows no image here, so the check proves nothing");
        Assert.Equal(plain, estimated);
    }
}

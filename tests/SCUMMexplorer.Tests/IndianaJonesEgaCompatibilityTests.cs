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

    [Fact]
    public void DecodesSuppliedIndianaJonesEgaCostumePosesWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        var costumes = game.Resources.Where(r => r.Kind == ResourceKind.Costume).ToArray();
        Assert.NotEmpty(costumes);

        var reader = new ScummV3CostumeReader();
        var renderer = new ScummCostumeRenderer();
        int withPose = 0, poses = 0;
        foreach (var costume in costumes)
        {
            var container = LflEncoding.DecodeXorFF(File.ReadAllBytes(costume.FilePath));
            var found = 0;
            foreach (var slot in reader.Decode(container, costume.Offset!.Value).AnimationSlots.Where(s => s.HasData))
            {
                try { if (renderer.DecodePose(container, costume.Offset.Value, slot.Index) is not null) found++; }
                catch (InvalidDataException) { }
            }
            if (found > 0) withPose++;
            poses += found;
        }
        Console.WriteLine($"Indy3 EGA: {costumes.Length} costumes, {withPose} with a pose, {poses} poses");
        Assert.True(withPose * 10 >= costumes.Length * 9, $"Only {withPose} of {costumes.Length} costumes decode a pose.");
    }

    [Fact]
    public void RendersSuppliedIndianaJonesEgaSoundsWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        var sounds = game.Resources.Where(r => r.Kind == ResourceKind.Sound).ToArray();
        Assert.NotEmpty(sounds);

        int speaker = 0, adlib = 0;
        foreach (var sound in sounds)
        {
            try { if (new ScummPcSpeakerAudio().RenderWave(sound, CancellationToken.None).Length > 44) speaker++; }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException) { }
            try { if (new ScummAdlibAudio().RenderWave(sound, CancellationToken.None).Length > 44) adlib++; }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException) { }
        }
        Console.WriteLine($"Indy3 EGA: {sounds.Length} sounds listed, {speaker} render on the PC speaker, {adlib} on AdLib");
        Assert.True(adlib > 0 && speaker > 0);
    }
}

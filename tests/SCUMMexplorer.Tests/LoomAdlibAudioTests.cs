using System.Buffers.Binary;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomAdlibAudioTests
{
    [Fact]
    public void TempoMetaEventUsesTheFirstTwoPayloadBytes()
    {
        // LOOM's tempo word is 09 D7 here; reading D7 88 stretches one note
        // from about a second to half a minute.
        var wave = RenderFixture([
            0xFF, 0x51, 3, 0x09, 0xD7, 0x88, 0,
            0x90, 60, 64, 0x87, 0x40,
            0x80, 60, 64, 0,
            0xFF, 0x2F
        ]);
        var duration = (wave.Length - 44) / (4.0 * 49716);
        Assert.InRange(duration, 1.0, 3.0);
        Assert.Contains(wave.AsSpan(44).ToArray(), value => value != 0);
    }

    [Fact]
    public void EndOfTrackRendersKeyedOffRelease()
    {
        var wave = RenderFixture([
            0xFF, 0x51, 3, 0x09, 0xD7, 0x88, 0,
            0x90, 60, 64, 0x87, 0x40,
            0xFF, 0x2F
        ]);
        // The 40-tick startup delay and 240-tick note end at about 1.39 s.
        // There must be room after that for the OPL envelope to release.
        var duration = (wave.Length - 44) / (4.0 * 49716);
        Assert.InRange(duration, 1.55, 3.5);
    }

    private static byte[] RenderFixture(byte[] events)
    {
        var track = new byte[0x93 + events.Length];
        track[2] = 0x80;
        track[3] = 0xC0;
        track[4] = 1; // stop at the end marker
        track[10] = 1;
        track[11] = 1;
        var instrument = new byte[]
        {
            0, 0, 1,
            1, 0x3F, 0xF0, 0x77, 0,
            1, 0, 0xF0, 0x77, 0,
            0, 0, 0
        };
        instrument.CopyTo(track, 19);
        events.CopyTo(track, 0x93);

        var encodedFile = new byte[8 + track.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(encodedFile, 4);
        encodedFile[2] = (byte)'W';
        encodedFile[3] = (byte)'A';
        BinaryPrimitives.WriteUInt16LittleEndian(encodedFile.AsSpan(4), checked((ushort)(4 + track.Length)));
        encodedFile[6] = (byte)'A';
        encodedFile[7] = (byte)'D';
        track.CopyTo(encodedFile, 8);
        for (var i = 0; i < encodedFile.Length; i++) encodedFile[i] ^= 0xFF;

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, encodedFile);
            var resource = new GameResource("Tempo fixture", ResourceKind.Sound, path,
                ContainerFileNumber: 1, Offset: 0);
            return new LoomAdlibAudio().RenderWave(resource);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RendersSuppliedAdlibMusicWhenAvailable()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var sounds = new GameFolderScanner().Scan(folder).Resources
            .Where(resource => resource.Kind == ResourceKind.Sound && resource.ResourceId.HasValue)
            .ToDictionary(resource => resource.ResourceId!.Value);
        var renderer = new LoomAdlibAudio();
        Assert.False(renderer.HasMusic(sounds[1]));
        foreach (var id in Enumerable.Range(13, 20).Where(id => id != 24))
        {
            Assert.True(renderer.HasMusic(sounds[id]), $"Sound {id} lacks AdLib music.");
            byte[] wave;
            try { wave = renderer.RenderWave(sounds[id]); }
            catch (Exception ex) { throw new Xunit.Sdk.XunitException($"Sound {id}: {ex}"); }
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
            Assert.True(wave.Length > 44 + 49716 * 4, $"Sound {id} is too short.");
            if (id is 13 or 16) Assert.True(wave.Length < 44 + 49716 * 4 * 5, $"Sound {id} played too slowly.");
            Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(22, 2)));
            Assert.Contains(wave.AsSpan(44).ToArray(), value => value != 0);
        }
    }
}

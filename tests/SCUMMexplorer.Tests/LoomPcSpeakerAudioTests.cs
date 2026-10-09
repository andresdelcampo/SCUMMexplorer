using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomPcSpeakerAudioTests
{
    [Fact]
    public void RendersRepresentativeSuppliedLoomTracksWhenAvailable()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var sounds = new GameFolderScanner().Scan(folder).Resources
            .Where(resource => resource.Kind == ResourceKind.Sound)
            .Where(resource => resource.ResourceId.HasValue)
            .ToDictionary(resource => resource.ResourceId!.Value);
        foreach (var id in Enumerable.Range(1, 32).Where(id => id != 24))
        {
            byte[] wave;
            try { wave = new ScummPcSpeakerAudio().RenderWave(sounds[id]); }
            catch (Exception ex) { throw new Xunit.Sdk.XunitException($"Sound {id}: {ex}"); }
            Assert.True(wave.Length > 44, $"Sound {id} did not render.");
            if (id >= 25)
                Assert.True(wave.Length > 22050 * 2 * 5, $"Musical track {id} ended after {(wave.Length - 44) / 44100.0:F1} seconds.");
        }
    }

    [Fact]
    public void RendersIndexedSpeakerTrackAsAudibleWave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scumm-sound-{Guid.NewGuid():N}.LFL");
        try
        {
            var data = new byte[44];
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 22);
            var commands = new byte[]
            {
                0xFF, 0x04, 0xE8, 0x03, // Speaker divisor 1000.
                0xFF, 0x0A, 0x01, 0x00, // Audible volume.
                0xFF, 0x00, 0x14, 0x00, // Play for 20 ticks.
                0xFF, 0x00, 0x00, 0x00  // End.
            };
            commands.CopyTo(data, 22);
            File.WriteAllBytes(path, data.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var resource = new GameResource("Sound 01", ResourceKind.Sound, path, 1, null, 1, 0);

            var wave = new ScummPcSpeakerAudio().RenderWave(resource);

            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
            Assert.True(wave.Length > 44);
            Assert.Contains(wave.AsSpan(44).ToArray(), value => value != 0);
            var samples = Enumerable.Range(0, (wave.Length - 44) / 2)
                .Select(i => BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(44 + i * 2, 2))).ToArray();
            var crossings = Enumerable.Range(1, samples.Length - 1)
                .Count(i => samples[i - 1] < 0 && samples[i] >= 0);
            Assert.InRange(crossings, 85, 110); // 1193 Hz for roughly 20/236 seconds.
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SineModulationChangesPitchDuringAnIndexedNote()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scumm-modulated-{Guid.NewGuid():N}.LFL");
        try
        {
            var commands = new byte[]
            {
                0xF9, 0x00,             // V2 sine modulation; also sets its 4096-step period.
                0xFF, 0x04, 0xE8, 0x03, // Base speaker divisor 1000.
                0xFF, 0x20, 0x00, 0x01, // Advance modulation by 256 per tick.
                0xFF, 0x22, 0xF4, 0x01, // Modulation depth 500.
                0xFF, 0x0A, 0x01, 0x00, // Audible volume.
                0xFF, 0x00, 0x30, 0x00, // Sustain for 48 ticks.
                0xFA
            };
            var data = new byte[22 + commands.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 22);
            commands.CopyTo(data, 22);
            File.WriteAllBytes(path, data.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var resource = new GameResource("Modulated sound", ResourceKind.Sound, path, 1, null, 1, 0);

            var wave = new ScummPcSpeakerAudio().RenderWave(resource);
            var samples = Enumerable.Range(0, (wave.Length - 44) / 2)
                .Select(i => BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(44 + i * 2, 2))).ToArray();
            var lowPitch = CountPositiveCrossings(samples, 4, 7);
            var highPitch = CountPositiveCrossings(samples, 12, 15);
            Assert.True(highPitch >= lowPitch + 4,
                $"Expected audible pitch modulation; crossing counts were {lowPitch} and {highPitch}.");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void IntegratedSpeakerPulseSuppressesFoldedThirdHarmonic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scumm-speaker-spectrum-{Guid.NewGuid():N}.LFL");
        try
        {
            var commands = new byte[]
            {
                0xFF, 0x04, 0xC8, 0x00, // Divisor 200: 5965 Hz fundamental.
                0xFF, 0x0A, 0x01, 0x00,
                0xFF, 0x00, 0xEC, 0x00, // One second of steady tone.
                0xFA
            };
            var data = new byte[22 + commands.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 22);
            commands.CopyTo(data, 22);
            File.WriteAllBytes(path, data.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var resource = new GameResource("High speaker note", ResourceKind.Sound, path, 1, null, 1, 0);

            var wave = new ScummPcSpeakerAudio().RenderWave(resource);
            var samples = Enumerable.Range(0, (wave.Length - 44) / 2)
                .Select(i => BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(44 + i * 2, 2))).ToArray();
            var fundamental = SpectralMagnitude(samples, 5965);
            // The square wave's 17,895 Hz third harmonic folds to 4,155 Hz at 22,050 Hz.
            var foldedThird = SpectralMagnitude(samples, 4155);
            Assert.True(foldedThird < fundamental * 0.25,
                $"Folded third harmonic was {foldedThird / fundamental:P1} of the fundamental.");
        }
        finally { File.Delete(path); }
    }

    private static double SpectralMagnitude(short[] samples, double frequency)
    {
        const int sampleRate = 22050;
        var start = sampleRate / 10;
        var count = sampleRate / 2;
        double real = 0, imaginary = 0;
        for (var i = 0; i < count; i++)
        {
            var window = 0.5 - 0.5 * Math.Cos(Math.Tau * i / (count - 1));
            var angle = Math.Tau * frequency * i / sampleRate;
            real += samples[start + i] * window * Math.Cos(angle);
            imaginary += samples[start + i] * window * Math.Sin(angle);
        }
        return Math.Sqrt(real * real + imaginary * imaginary);
    }

    private static int CountPositiveCrossings(short[] samples, int fromTick, int toTick)
    {
        var start = fromTick * 22050 / 236;
        var end = toTick * 22050 / 236;
        return Enumerable.Range(start + 1, end - start - 1)
            .Count(i => samples[i - 1] < 0 && samples[i] >= 0);
    }

    [Fact]
    public void RejectsUnsupportedEncoding()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scumm-sound-{Guid.NewGuid():N}.LFL");
        try
        {
            var data = new byte[32];
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
            File.WriteAllBytes(path, data.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var resource = new GameResource("Sound 33", ResourceKind.Sound, path, 33, null, 1, 0);
            Assert.Throws<NotSupportedException>(() => new ScummPcSpeakerAudio().RenderWave(resource));
        }
        finally { File.Delete(path); }
    }
}

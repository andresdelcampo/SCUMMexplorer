using System.Buffers.Binary;
using Mt32emu;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>The MT-32 (or CM-32L) ROM images found in a folder: the control ROM and the PCM ROM of one model.</summary>
public sealed record Mt32RomSet(string ControlPath, string PcmPath, string Model)
{
    /// <summary>
    /// Looks in <paramref name="folder"/> for <c>MT32_CONTROL.ROM</c> + <c>MT32_PCM.ROM</c>, then for <c>CM32L_CONTROL.ROM</c> + <c>CM32L_PCM.ROM</c>
    /// (the LOOM upgrade is written for the MT-32 and the LAPC-1, whose sound is the CM-32L's). The ROMs are the user's own files; nothing here ships them.
    /// </summary>
    public static Mt32RomSet? Find(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
        foreach (var (prefix, model) in new[] { ("MT32", "MT-32"), ("CM32L", "CM-32L") })
        {
            var control = Path.Combine(folder, prefix + "_CONTROL.ROM");
            var pcm = Path.Combine(folder, prefix + "_PCM.ROM");
            if (File.Exists(control) && File.Exists(pcm)) return new Mt32RomSet(control, pcm, model);
        }
        return null;
    }
}

/// <summary>
/// Renders the Roland sounds of the LOOM upgrade (<see cref="ScummRolandSong"/>) to a stereo wave through Munt's MT-32 emulator
/// (<c>mt32emu</c>, LGPL; it needs the user's own ROM images). The game's Roland driver passes the song's events to the MIDI port unchanged:
/// the song uses MIDI channels 1 to 8 and 9 (the MT-32's parts 1 to 8 on its default channels 2 to 9) and 9 as the rhythm channel, and it picks
/// instruments only by program number, so the emulator needs no set-up data.
/// </summary>
public sealed class ScummMt32Audio
{
    /// <summary>A song's tail after its last event, so the final notes can release.</summary>
    private const double ReleaseSeconds = 3;
    private const double MaxSeconds = 420;

    private readonly Mt32RomSet _roms;

    public ScummMt32Audio(Mt32RomSet roms) => _roms = roms;

    public byte[] RenderWave(GameResource resource, CancellationToken cancellationToken = default) =>
        RenderWave(ScummRolandSong.Read(resource), cancellationToken);

    public byte[] RenderWave(ScummRolandSong song, CancellationToken cancellationToken = default)
    {
        using var synth = new Mt32Context(null!);
        synth.AnalogOutputMode = AnalogOutputMode.Accurate;
        synth.AddRom(_roms.ControlPath);
        synth.AddRom(_roms.PcmPath);
        synth.OpenSynth();
        var rate = (int)Math.Round(synth.ActualStereoOutputSampleRate);
        var endSeconds = Math.Min(MaxSeconds, song.Ticks * ScummRolandSong.SecondsPerTick + ReleaseSeconds);
        var totalFrames = (long)Math.Ceiling(endSeconds * rate);
        var pcm = new short[totalFrames * 2];
        long frame = 0;

        void RenderUntil(long target)
        {
            target = Math.Min(target, totalFrames);
            const int chunk = 4096;
            while (frame < target)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(chunk, target - frame);
                synth.Render(pcm.AsSpan((int)(frame * 2), count * 2));
                frame += count;
            }
        }

        foreach (var item in song.Events)
        {
            RenderUntil((long)Math.Round(item.Tick * ScummRolandSong.SecondsPerTick * rate));
            if (frame >= totalFrames) break;
            var message = (uint)(item.Status | item.Data1 << 8 | item.Data2 << 16);
            if (item.Command == 0x9 && item.Data2 == 0) message = (uint)((0x80 | item.Channel) | item.Data1 << 8);
            synth.PlayMessage(message);
        }
        RenderUntil(totalFrames);
        return ToWave(pcm, rate);
    }

    private static byte[] ToWave(short[] pcm, int rate)
    {
        var bytes = pcm.Length * 2;
        var wave = new byte[44 + bytes];
        "RIFF"u8.CopyTo(wave);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 36 + bytes);
        "WAVEfmt "u8.CopyTo(wave.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22), 2);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28), rate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32), 4);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34), 16);
        "data"u8.CopyTo(wave.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40), bytes);
        Buffer.BlockCopy(pcm, 0, wave, 44, bytes);
        return wave;
    }
}

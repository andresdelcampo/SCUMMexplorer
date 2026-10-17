using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The sounds of a v5 game that play a track of its disc (ScummVM <c>GF_AUDIOTRACKS</c>; The Secret of Monkey Island CD), for the live session (S4b MonkeyCd,
/// 17 October 2026). Decided by the game's data: its resource file's <c>SOUN</c> blocks that name a track (<see cref="ScummV5Archive.ReadCdTrackSounds"/>) and the
/// disc image that holds the tracks. ScummVM soundcd.cpp (master, read 17 October 2026): <c>playCDTrackFromSoundID</c> plays the track from the sound's start
/// frame to its end frame (the end of the track when the end is not after the start), 0xFF loops for ever; <c>playCDTrack</c> sets <c>VAR_MUSIC_TIMER</c> and
/// the CD timer to 0 and installs the timer, which adds one every fourth tick of the engine's 240 Hz timer (one per jiffy); <c>Sound::updateMusicTimer</c>
/// copies it into <c>VAR_MUSIC_TIMER</c> every frame (sound.cpp). The drive plays one track at a time. A sound's track byte counts the disc's audio tracks
/// from 1 (the 14 October S3 decision of the MonkeyCd tracker).
/// </summary>
public sealed class ScummCdAudioTracks
{
    private readonly Dictionary<int, (ScummCdTrackSound Sound, int? TrackFrames)> _sounds = [];
    private DiscImage? _disc;

    private ScummCdAudioTracks() { }

    /// <summary>The sound's track repeats until a script stops it (loop count 0xFF).</summary>
    public bool LoopsForever(int soundId) => _sounds.TryGetValue(soundId, out var item) && item.Sound.Loops == 0xFF;

    /// <summary>
    /// What the drive plays for the sound, as a WAV file's bytes (S6 MonkeyCd play, 17 October 2026; <c>playCDTrackFromSoundID</c>): the sound's stretch of its
    /// track (start frame to end frame, or to the end of the track), repeated for a finite loop count above 1; one pass for a track that loops for ever (the player
    /// repeats it, <see cref="LoopsForever"/>). Empty when the sound plays no track of the image.
    /// </summary>
    public byte[] ReadWave(int soundId)
    {
        if (_disc is not { } disc || !_sounds.TryGetValue(soundId, out var item) || item.Sound.Track < 1 || item.Sound.Track > disc.AudioTracks.Count) return [];
        var track = disc.AudioTracks[item.Sound.Track - 1];
        var end = item.Sound.EndFrame > item.Sound.StartFrame ? Math.Min(item.Sound.EndFrame, track.Frames) : track.Frames;
        var frames = end - item.Sound.StartFrame;
        if (frames <= 0) return [];
        var wave = disc.ReadSectorsWave(track.StartSector + item.Sound.StartFrame, frames);
        var repeats = item.Sound.Loops is > 1 and < 0xFF ? (int)item.Sound.Loops : 1;
        if (repeats == 1 || wave.Length <= 44) return wave;
        // A finite loop count: the same samples again after the first pass, under one header (the data size is patched).
        var pcm = wave.Length - 44;
        var looped = new byte[44 + pcm * repeats];
        Array.Copy(wave, looped, wave.Length);
        for (var i = 1; i < repeats; i++) Array.Copy(wave, 44, looped, 44 + pcm * i, pcm);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(looped.AsSpan(4), 36 + pcm * repeats);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(looped.AsSpan(40), pcm * repeats);
        return looped;
    }

    /// <summary>The game's CD-track sounds, or null when the game plays no disc track (no v5 archive with track sounds, or no disc image with audio).</summary>
    public static ScummCdAudioTracks? For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.AudioDisc is not { } disc || disc.AudioTracks.Count == 0 || game.V4Disks is null) return null;
        var tracks = new ScummCdAudioTracks { _disc = disc };
        foreach (var archive in game.V4Disks.Disks.OfType<ScummV5Archive>())
            foreach (var sound in archive.ReadCdTrackSounds())
                tracks._sounds.TryAdd(sound.SoundId, (sound, sound.Track >= 1 && sound.Track <= disc.AudioTracks.Count ? disc.AudioTracks[sound.Track - 1].Frames : null));
        return tracks._sounds.Count > 0 ? tracks : null;
    }

    /// <summary>The sound starts a disc track.</summary>
    public bool Plays(int soundId) => _sounds.ContainsKey(soundId);

    /// <summary>
    /// How long the sound's track plays, on the session's sound clock (472 Hz callbacks; a CD frame is 1/75 s): from its start frame to its end frame, or to the
    /// end of the disc track; a track that loops for ever never completes. Null for a sound that plays no track.
    /// </summary>
    public ScummSoundLifetime? Lifetime(int soundId)
    {
        if (!_sounds.TryGetValue(soundId, out var item)) return null;
        var (sound, trackFrames) = item;
        if (sound.Loops == 0xFF) return new(ScummSoundLifetimeKind.Looping, null, true, $"CD track {sound.Track} loops until a script stops it");
        var end = sound.EndFrame > sound.StartFrame ? sound.EndFrame : trackFrames;
        if (end is not int last || last <= sound.StartFrame)
            return new(ScummSoundLifetimeKind.Unknown, null, true, $"CD track {sound.Track} is not on the disc image");
        var frames = (long)(last - sound.StartFrame) * Math.Max(1, (int)sound.Loops);
        return new(ScummSoundLifetimeKind.Finite, (int)Math.Min(int.MaxValue, frames * 472 / 75), true);
    }

    /// <summary>The track a sound plays, as the trace names it.</summary>
    public string Describe(int soundId) =>
        _sounds.TryGetValue(soundId, out var item) ? $"sound {soundId} plays CD track {item.Sound.Track} from frame {item.Sound.StartFrame}" : $"sound {soundId}";
}

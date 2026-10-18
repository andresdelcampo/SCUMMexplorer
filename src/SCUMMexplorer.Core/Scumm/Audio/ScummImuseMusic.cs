using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>The sound card whose part of a v5 sound iMUSE plays (a <c>SOUN</c> block holds one part per card: <c>ROL </c>, <c>ADL </c>, <c>SPK </c>).</summary>
public enum ScummImuseDevice { Speaker, Adlib, Roland }

/// <summary>Where a looping song goes on: from <see cref="RestartSeconds"/> of its rendered wave once the wave reaches <see cref="EndSeconds"/>.</summary>
public sealed record ScummImuseLoop(double RestartSeconds, double EndSeconds);

/// <summary>
/// The iMUSE songs of a v5 game (Monkey Island 2, Fate of Atlantis; S6 Monkey2, 17 October 2026) as play needs them: which part a device plays and whether
/// it loops. The parts are rendered by the S3 players (<see cref="ScummImuseMt32"/>, <see cref="ScummImuseAdlib"/>, <see cref="ScummImuseSpeaker"/>), which
/// unroll a looping song to a few minutes (<see cref="ScummImuseTimeline"/>); in the game such a song loops until a script stops it, so play repeats the
/// rendered pass from <see cref="ScummImuseTimeline.RestartSeconds"/>. Decided by each sound's own data. Markers, hooks with an id, and the player commands of
/// <c>soundKludge</c> that steer a playing song are not modelled: the loop point is an estimate (ScummVM <c>imuse/imuse_player.cpp</c>, master read 17 October 2026).
/// </summary>
public sealed class ScummImuseMusic
{
    private readonly Dictionary<int, ScummV4Sound> _sounds = [];
    private readonly Dictionary<(int, ScummImuseDevice), ScummImuseLoop?> _loops = [];

    private ScummImuseMusic() { }

    /// <summary>The game's iMUSE sounds, or null when it has none (no v5 archive with <c>ROL </c>/<c>ADL </c>/<c>SPK </c> parts).</summary>
    public static ScummImuseMusic? For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.V4Disks is null) return null;
        var music = new ScummImuseMusic();
        foreach (var archive in game.V4Disks.Disks.OfType<ScummV5Archive>())
            foreach (var sound in archive.ReadSounds())
                if (sound.ImuseRoland is not null || sound.ImuseAdlib is not null || sound.ImuseSpeaker is not null) music._sounds.TryAdd(sound.Id, sound);
        return music._sounds.Count > 0 ? music : null;
    }

    /// <summary>The part the device plays, or null when the sound has none for it.</summary>
    public byte[]? Part(int soundId, ScummImuseDevice device) => _sounds.TryGetValue(soundId, out var sound)
        ? device switch { ScummImuseDevice.Roland => sound.ImuseRoland, ScummImuseDevice.Adlib => sound.ImuseAdlib, _ => sound.ImuseSpeaker }
        : null;

    /// <summary>Where the device's rendition of the sound loops, or null for a sound that ends (or that has no part for the device).</summary>
    public ScummImuseLoop? Loop(int soundId, ScummImuseDevice device)
    {
        lock (_loops)
        {
            if (_loops.TryGetValue((soundId, device), out var known)) return known;
            ScummImuseLoop? loop = null;
            if (Part(soundId, device) is { } part)
            {
                try
                {
                    var timeline = ScummImuseTimeline.Build(ScummImuseSong.Parse(part));
                    if (timeline is { EndedOnLoop: true, RestartSeconds: double restart } && timeline.EndSeconds > restart) loop = new(restart, timeline.EndSeconds);
                }
                catch (InvalidDataException) { }
            }
            _loops[(soundId, device)] = loop;
            return loop;
        }
    }
}

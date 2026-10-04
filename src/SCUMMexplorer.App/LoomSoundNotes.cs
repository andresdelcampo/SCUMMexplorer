using System.IO;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.App;

/// <summary>What the game's own data says about one sound: music or effect, and where decoded code starts it.</summary>
internal sealed record LoomSoundNote(bool Music, string Short, string Evidence);

/// <summary>
/// A description for every sound, from the data alone: music or effect (does the resource hold music data) and the rooms and
/// scripts whose decoded code starts it: a literal number (<see cref="LoomUsageIndex"/>) or a number a room's code hands down
/// to the scripts it starts (<see cref="LoomRoomSoundCandidates"/>). Starts through other variables are not found. The
/// glossary's own names ("opening music") stay in front where it has them.
/// </summary>
internal static class LoomSoundNotes
{
    public static Dictionary<int, LoomSoundNote> Build(GameLibrary game, LoomUsageIndex usage, LoomGlossary glossary)
    {
        var notes = new Dictionary<int, LoomSoundNote>();
        var decoder = new LoomAdlibAudio();
        var roomsBySound = new Dictionary<int, SortedSet<int>>();
        var pathsBySound = new Dictionary<int, List<string>>();
        var directory = new LoomRoomObjectDirectory();
        var candidates = new LoomRoomSoundCandidates();
        foreach (var room in game.Resources.Where(item => item.Kind == ResourceKind.Room && item.ResourceId.HasValue))
        {
            try
            {
                var records = directory.Decode(File.ReadAllBytes(room.FilePath));
                foreach (var cue in candidates.Analyze(game, room, records).Where(item => item.SoundId.HasValue))
                {
                    var sound = cue.SoundId!.Value;
                    if (!roomsBySound.TryGetValue(sound, out var rooms)) roomsBySound[sound] = rooms = [];
                    rooms.Add(room.ResourceId!.Value);
                    if (!pathsBySound.TryGetValue(sound, out var paths)) pathsBySound[sound] = paths = [];
                    paths.Add(cue.Evidence);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        }
        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Sound && item.ResourceId.HasValue))
        {
            var id = resource.ResourceId!.Value;
            var music = false;
            try { music = decoder.HasMusic(resource); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { }
            var kind = music ? "music" : "effect";

            var uses = usage.UsedBy(LoomUsageTarget.Sound, id);
            var scripts = uses.Where(use => use.Where.StartsWith("Script ", StringComparison.Ordinal) &&
                    int.TryParse(use.Where.AsSpan(7, 3), out _))
                .Select(use => int.Parse(use.Where.AsSpan(7, 3))).Distinct().Order().ToList();
            var rooms = uses.Where(use => use.Room > 0 && !use.Where.StartsWith("Script ", StringComparison.Ordinal))
                .Select(use => use.Room).Concat(roomsBySound.TryGetValue(id, out var found) ? found : [])
                .Distinct().Order().ToList();
            var paths = pathsBySound.TryGetValue(id, out var cuePaths) ? cuePaths : [];
            var parts = new List<string>();
            if (rooms.Count > 0) parts.Add(List("Room", "Rooms", rooms));
            if (scripts.Count > 0) parts.Add(List("script", "scripts", scripts));
            var where = parts.Count > 0 ? string.Join(", ", parts) : "no start found";

            var lines = uses.Select(use => LoomUsageIndex.Describe(use, glossary)).Concat(paths).Distinct().ToList();
            var evidence = $"{kind} (from the sound's own data). " + (lines.Count == 0
                ? "No decoded instruction starts it with a number the scan can follow (starts through other variables are not found)."
                : "Started by: " + string.Join("; ", lines.Take(6)) + (lines.Count > 6 ? $"; and {lines.Count - 6} more." : "."));
            notes[id] = new LoomSoundNote(music, $"{kind} · {where}", evidence);
        }
        return notes;
    }

    private static string List(string one, string many, List<int> ids) =>
        ids.Count == 1 ? $"{one} {ids[0]:00}"
        : ids.Count <= 3 ? $"{many} {string.Join(", ", ids.Select(item => item.ToString("00")))}"
        : $"{ids.Count} {many.ToLowerInvariant()}";
}

using System.Globalization;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// A game in container files (SCUMM v4 and v5), from a folder or a disc image: v4 on one or several disks (<c>000.LFL</c> + <c>DISKnn.LEC</c>) or one plain
/// <c>NN.LFL</c> per room, v5 as <c>NAME.000</c> + <c>NAME.001</c> (perhaps one copy per language folder of a disc). The game is identified from its data
/// (<see cref="GameIdentifier"/>); LOOM CD is listed for its own session, every other game is browsed through the shared readers.
/// </summary>
internal static class ArchiveLayout
{
    /// <summary>The source at <paramref name="path"/> when it holds container files: a disc image, a folder holding one, or a folder with v4/v5 files; null for anything else.</summary>
    public static IGameFileSource? Open(string path)
    {
        if (File.Exists(path)) return DiscImage.IsImageFile(path) ? new DiscFileSource(path) : null;
        if (!Directory.Exists(path)) return null;
        var folder = new FolderFileSource(path);
        if (ScummV4Archive.IsVga(folder) || ScummV4Archive.IsRoomFileLayout(folder) || ScummV5Archive.FindGame(folder) is not null) return folder;
        if (folder.FileNames.Any(n => n.Equals("00.LFL", StringComparison.OrdinalIgnoreCase)) || DiscImage.FindImageIn(path) is null) return null;
        try { return new DiscFileSource(path); }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { return null; }
    }

    /// <summary>The v5 game to read: the one in <paramref name="discFolder"/> when the source has it, else the default (<see cref="ScummV5Archive.FindGame"/>); null for a v4 game.</summary>
    private static (ScummV5Game? Game, bool Chosen) FindV5(IGameFileSource source, string? discFolder)
    {
        if (ScummV4Archive.IsVga(source)) return (null, false);
        var chosen = discFolder is null ? null : ScummV5Archive.FindGame(source, discFolder);
        return (chosen ?? ScummV5Archive.FindGame(source), chosen is not null);
    }

    public static GameFingerprint Fingerprint(IGameFileSource source, string? discFolder)
    {
        if (FindV5(source, discFolder).Game is { } v5)
            return GameFingerprint.Of(source, ScummV5Archive.FolderOf(v5), () => ShapeOf(ScummV4Disks.FromArchives(ScummV5Archive.Load(source, v5)), ScummVersion.V5));
        return GameFingerprint.Of(source, null, () => ShapeOf(ScummV4Disks.Load(source), ScummVersion.V4));
    }

    public static GameLibrary Scan(IGameFileSource source, string? discFolder)
    {
        var (v5, chosen) = FindV5(source, discFolder);
        if (v5 is null)
        {
            var disks = ScummV4Disks.Load(source);
            var identity = GameIdentifier.Identify(GameFingerprint.Of(source, null, () => ShapeOf(disks, ScummVersion.V4)));
            // LOOM CD has its own session, which reads the single archive of the disc.
            if (identity.Game == KnownGame.Loom && ScummV4Archive.IsVga(source) && disks.Disks is [ScummV4Archive archive])
                return ListLoomCd(source, archive, identity);
            return ListBrowsed(source, disks, null, identity);
        }

        var v5Disks = ScummV4Disks.FromArchives(ScummV5Archive.Load(source, v5));
        var v5Identity = GameIdentifier.Identify(GameFingerprint.Of(source, ScummV5Archive.FolderOf(v5), () => ShapeOf(v5Disks, ScummVersion.V5)));
        var library = ListBrowsed(source, v5Disks, v5, v5Identity);
        var languages = LanguagesOf(source);
        if (languages.Count == 0) return library;
        // A folder that is not on the disc (a choice remembered for another copy) falls back to the default and says so.
        var read = ScummV5Archive.FolderOf(v5);
        var label = languages.FirstOrDefault(l => string.Equals(l.Key, read, StringComparison.OrdinalIgnoreCase))?.Label ?? read;
        var how = chosen ? "chosen" : discFolder is not null ? $"the default: \"{discFolder}\" is not on this disc" : "the default: the game with the most rooms, an ENGLISH folder winning a tie";
        return library with
        {
            Languages = languages, Language = read,
            DetectionNote = library.DetectionNote + $" The data holds {languages.Count} languages, one folder each ({string.Join(", ", languages.Select(l => l.Key))}); {label} is read ({how})."
        };
    }

    /// <summary>The languages of a source with one copy of the same v5 game per folder (R-3.16), labelled by the folder's own name; empty for one language.</summary>
    public static IReadOnlyList<GameLanguage> LanguagesOf(IGameFileSource source) =>
        ScummV4Archive.IsVga(source) ? [] : ScummV5Archive.FindGameFolders(source).Select(folder => new GameLanguage(folder, LanguageLabel(folder))).ToList();

    /// <summary>The disc names the folder ("SPANISH", "ENGLISH"): the label is that name in title case ("Spanish"); the name is data of the disc, so nothing is guessed.</summary>
    private static string LanguageLabel(string folder)
    {
        var name = folder[(folder.LastIndexOf('/') + 1)..];
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    /// <summary>The sizes of the directories of every disk: rooms, costumes, scripts (global and room-local) and sounds; null when they do not read.</summary>
    private static DirectoryShape? ShapeOf(ScummV4Disks disks, ScummVersion version)
    {
        try
        {
            int Scripts(IBrowsableArchive disk) => disk switch
            {
                ScummV5Archive v5 => v5.ReadGlobalScripts().Count + v5.Rooms.Sum(room => v5.ReadRoomScripts(room.Number).Count(s => s.Kind == "LSCR")),
                ScummV4Archive v4 => v4.GlobalScriptNumbers().Count + v4.Rooms.Sum(room => v4.ReadScripts(room.Number).Count(s => s.Kind == "LS")),
                _ => 0
            };
            return new DirectoryShape(version, disks.Disks.Sum(d => d.Rooms.Count), disks.Disks.Sum(d => d.ReadCostumes().Count), disks.Disks.Sum(Scripts),
                disks.Disks.Sum(d => d.ReadSounds().Count));
        }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or EndOfStreamException) { return null; }
    }

    /// <summary>LOOM CD (game-specific): the single archive of the disc, listed for the LOOM session (rooms, costumes, sounds, global and room-local scripts).</summary>
    private static GameLibrary ListLoomCd(IGameFileSource source, ScummV4Archive archive, GameIdentity identity)
    {
        var resources = archive.Rooms.Select(room => new GameResource($"Room {room.Number:00}", ResourceKind.Room, source.Location,
            room.Number, string.IsNullOrEmpty(room.Name) ? "LOOM CD room (DISK01.LEC)." : $"LOOM CD room \"{room.Name}\" (DISK01.LEC).",
            1, room.Offset)).ToList();
        resources.AddRange(archive.ReadCostumes().OrderBy(c => c.Id).Select(c => new GameResource($"Costume {c.Id:00}", ResourceKind.Costume, source.Location,
            c.Id, $"LOOM CD costume, stored in room {c.Room:00} (DISK01.LEC).", 1, null)));
        resources.AddRange(archive.ReadSounds().Select(s => new GameResource($"Sound {s.Id:00}", ResourceKind.Sound, source.Location,
            s.Id, $"LOOM CD {(s.IsMusic ? "music" : "sound effect")}, stored in room {s.Room:00} (DISK01.LEC): a PC speaker and an AdLib rendition.", 1, null)));
        resources.AddRange(archive.GlobalScriptNumbers().Select(id => new GameResource($"Script {id:00}", ResourceKind.Script,
            source.Location, id, "LOOM CD global script (DISK01.LEC).", 1, archive.ReadGlobalScript(id)?.BlockOffset)));
        foreach (var room in archive.Rooms)
            resources.AddRange(archive.ReadScripts(room.Number).Where(s => s.Kind == "LS").Select(s => new GameResource(
                $"Room {room.Number:00} local script {s.Number}", ResourceKind.Script, source.Location, s.Number,
                "LOOM CD room-local script (DISK01.LEC).", 1, s.BlockOffset, ScriptRoom: room.Number)));
        var note = "LOOM CD (SCUMM v4, VGA) read from its disc image or files: 000.LFL and DISK01.LEC. Rooms, scripts, objects, actors, texts, the sounds in DISK01.LEC " +
            "(AdLib and PC speaker) and the voices on the disc's audio track are read. " + identity.Note;
        return new GameLibrary(KnownGames.TitleOf(KnownGame.Loom, ScummVersion.V4, identity.IsDemo), source.Location, ScummVersion.V4, "CD (VGA)", resources, note,
            GameId.LoomVga, identity.IsDemo, identity.EditionLabel) { Game = KnownGame.Loom, V4Archive = archive };
    }

    /// <summary>
    /// A v4 or v5 game browsed from every disk it has: its rooms, costumes, sounds and scripts, and on a disc its audio tracks; a talkie's spoken lines. If no room
    /// of any disk decodes (a format whose rooms this explorer does not read), nothing is offered rather than rooms that cannot be shown.
    /// </summary>
    private static GameLibrary ListBrowsed(IGameFileSource source, ScummV4Disks disks, ScummV5Game? v5, GameIdentity identity)
    {
        var version = v5 is null ? ScummVersion.V4 : ScummVersion.V5;
        var perRoomFiles = v5 is null && !source.Exists(ScummV4Archive.DiskFileName(1));
        var resources = new List<GameResource>();
        var withoutPicture = 0;
        for (var i = 0; i < disks.Disks.Count; i++)
        {
            var disk = i + 1;
            var archive = disks.Disks[i];
            var diskName = v5 is not null ? $"{Path.GetFileName(v5.Stem)}.001" : perRoomFiles ? "its room files" : ScummV4Archive.DiskFileName(disk);
            // A room is offered only if its picture decodes: a resource bank (rooms 98 and 99 of the 256-colour Indiana Jones) has none.
            foreach (var room in archive.Rooms)
            {
                try { if (!archive.DecodeRoom(room.Number).IsValid) throw new InvalidDataException(); }
                catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException) { withoutPicture++; continue; }
                resources.Add(new GameResource($"Room {room.Number:00}", ResourceKind.Room, source.Location, room.Number,
                    string.IsNullOrEmpty(room.Name) ? $"Room on {diskName}." : $"Room \"{room.Name}\" on {diskName}.", disk, room.Offset));
            }
            foreach (var costume in archive.ReadCostumes())
                resources.Add(new GameResource($"Costume {costume.Id:00}", ResourceKind.Costume, source.Location, costume.Id,
                    $"Costume stored in room {costume.Room:00} on {diskName}.", disk, null));
            foreach (var sound in archive.ReadSounds())
                resources.Add(new GameResource($"Sound {sound.Id:00}", ResourceKind.Sound, source.Location, sound.Id,
                    $"{(sound.ImuseRoland is not null || sound.ImuseAdlib is not null || sound.ImuseSpeaker is not null || sound.ImuseDigital is not null ? "Sound" : sound.IsMusic ? "Music" : "Sound effect")} stored in room {sound.Room:00} on {diskName}.", disk, null));
            // Scripts of the v5 archive: the global SCRP blocks and each room's local LSCR blocks.
            if (archive is ScummV5Archive v5Scripts)
            {
                foreach (var script in v5Scripts.ReadGlobalScripts())
                    resources.Add(new GameResource($"Script {script.Number:00}", ResourceKind.Script, source.Location, script.Number,
                        $"Global script stored in room {script.Room:00}.", disk, script.Position));
                foreach (var room in v5Scripts.Rooms)
                    foreach (var local in v5Scripts.ReadRoomScripts(room.Number).Where(s => s.Kind == "LSCR"))
                        resources.Add(new GameResource($"Room {room.Number:00} local script {local.Number}", ResourceKind.Script, source.Location, local.Number,
                            $"Room-local script of room {room.Number:00}.", disk, local.Position, ScriptRoom: room.Number));
            }
            // Scripts of the v4 archives: global ones and each room's local ones, for the raw and readable views.
            if (archive is ScummV4Archive scriptArchive)
            {
                foreach (var id in scriptArchive.GlobalScriptNumbers())
                    resources.Add(new GameResource($"Script {id:00}", ResourceKind.Script, source.Location, id,
                        $"Global script stored on {diskName}.", disk, scriptArchive.ReadGlobalScript(id)?.BlockOffset));
                foreach (var room in scriptArchive.Rooms)
                    foreach (var local in scriptArchive.ReadScripts(room.Number).Where(s => s.Kind == "LS"))
                        resources.Add(new GameResource($"Room {room.Number:00} local script {local.Number}", ResourceKind.Script, source.Location, local.Number,
                            $"Room-local script of room {room.Number:00} on {diskName}.", disk, local.BlockOffset, ScriptRoom: room.Number));
            }
        }
        var disc = source is DiscFileSource discSource && discSource.Image.AudioTracks.Count > 0 ? discSource.Image : null;
        if (disc is not null) resources.AddRange(ListCdTracks(source, disks, disc));
        // A talkie game: every spoken line of MONSTER.SOU is a sound of its own (device-independent digital audio, like the disc's tracks).
        var speech = v5 is null ? null : ScummSpeechBank.Find(source);
        if (speech is not null)
        {
            var number = 0;
            foreach (var (offset, length) in speech.ListSamples())
            {
                number++;
                resources.Add(new GameResource($"Speech {number:0000}", ResourceKind.Sound, source.Location, GameLibrary.SpeechIdBase + number,
                    $"Spoken line {number} of {speech.FileName}. Its text is in Game Texts.", null, offset, ContainerNumberOffset: length));
            }
        }

        var title = identity.Game == KnownGame.Unknown ? UnknownTitle(source, v5) : KnownGames.TitleOf(identity.Game, version, identity.IsDemo);
        var platform = $"DOS (SCUMM v{(int)version})";
        var layout = v5 is not null ? $"a SCUMM v5 index and resource file ({v5.Stem}.000 and .001)" : perRoomFiles ? "one file per room (00.LFL and NN.LFL)"
            : $"{disks.Disks.Count} disks (000.LFL and DISK01.LEC to DISK{disks.Disks.Count:00}.LEC)";
        if (!resources.Any(r => r.Kind == ResourceKind.Room))
            return new GameLibrary(title, source.Location, version, platform, [],
                $"The folder holds a SCUMM v{(int)version} game ({layout}). Its rooms are not in a format this explorer reads yet, so it is not supported yet. {identity.Note}",
                IsDemo: identity.IsDemo, Edition: identity.EditionLabel) { Game = identity.Game };
        var ordered = resources.OrderBy(r => r.Kind).ThenBy(r => r.ResourceId).ToList();
        var note = $"A SCUMM v{(int)version} game read from {layout}." + (withoutPicture > 0 ? $" {withoutPicture} room entries without a picture are left out." : "") + " " + identity.Note;
        return new GameLibrary(title, source.Location, version, platform, ordered, note, IsDemo: identity.IsDemo, Edition: identity.EditionLabel)
        {
            Game = identity.Game, V4Disks = disks, AudioDisc = disc, Speech = speech
        };
    }

    /// <summary>The title of a game the data does not name: the v5 file stem, else the folder (a folder named after its graphics, "EGA" or "VGA", with the folder around it).</summary>
    private static string UnknownTitle(IGameFileSource source, ScummV5Game? v5)
    {
        if (v5 is not null) return Path.GetFileNameWithoutExtension(v5.Stem);
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source.Location));
        var parentName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source.Location)) ?? ""));
        return folderName.Equals("EGA", StringComparison.OrdinalIgnoreCase) || folderName.Equals("VGA", StringComparison.OrdinalIgnoreCase)
            ? $"{parentName} {folderName}".Trim() : folderName;
    }

    /// <summary>
    /// The red-book audio tracks of a disc image (Monkey Island's music), played by the scripts through the CD drive. The game's own sounds say which track plays
    /// where: the sound is stored in the room whose music it is.
    /// </summary>
    private static IEnumerable<GameResource> ListCdTracks(IGameFileSource source, ScummV4Disks disks, DiscImage disc)
    {
        var users = disks.Disks.OfType<ScummV5Archive>().SelectMany(a => a.ReadCdTrackSounds().Select(s => (Sound: s, Room: a.Rooms.FirstOrDefault(r => r.Number == s.Room))))
            .GroupBy(item => item.Sound.Track).ToDictionary(g => g.Key, g => g.ToList());
        // The sound's track byte counts the disc's audio tracks from 1 (the data track is not counted; ScummVM plays it with AudioCDManager::play(track) and the
        // MI1 CD has 24 audio tracks, disc tracks 2 to 25, and sounds naming tracks 1 to 24). So disc track N is the game's track N - 1.
        var gameTrack = 0;
        foreach (var track in disc.AudioTracks)
        {
            gameTrack++;
            var played = users.TryGetValue(gameTrack, out var list) ? list : [];
            var rooms = played.Select(item => string.IsNullOrEmpty(item.Room?.Name) ? $"room {item.Sound.Room:00}" : $"room {item.Sound.Room:00} \"{item.Room!.Name}\"").Distinct().ToList();
            // "CD track 03 · 2:41 · room 34 "Name"": the track, its length, and the rooms whose music it is (nothing when no sound of the game starts it).
            var name = $"CD track {track.Number:00} · {(int)track.Seconds / 60}:{(int)track.Seconds % 60:00}" + (rooms.Count > 0 ? " · " + string.Join(", ", rooms.Take(2)) + (rooms.Count > 2 ? ", ..." : "") : " · not started by a sound");
            var plays = played.Count == 0 ? "" : " Started by " + string.Join(", ", played.Select(item =>
                $"sound {item.Sound.SoundId}{(item.Sound.Loops == 0xFF ? " (repeats)" : "")}{(item.Sound.EndFrame > item.Sound.StartFrame ? $" ({item.Sound.StartFrame / 4500}:{item.Sound.StartFrame / 75 % 60:00} to {item.Sound.EndFrame / 4500}:{item.Sound.EndFrame / 75 % 60:00})" : "")}")) + ".";
            yield return new GameResource(name, ResourceKind.Sound, source.Location, GameLibrary.CdTrackIdBase + track.Number,
                $"Track {track.Number} of the disc (track {gameTrack} of the game's audio), {(int)track.Seconds / 60}:{(int)track.Seconds % 60:00}.{plays}", null, null);
        }
    }
}

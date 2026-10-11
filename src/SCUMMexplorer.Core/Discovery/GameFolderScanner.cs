using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// Identifies known game-folder layouts and exposes user-facing resources.
/// Format-specific directory parsing belongs in a separate provider.
/// </summary>
public sealed class GameFolderScanner
{
    /// <summary>
    /// The game an old-v3 index belongs to when the folder has no executable (a SCUMMVM-style install may keep only the data): how many costumes and scripts the
    /// index lists. Checked on 10 October on every local copy: all LOOM EGA editions list 176 costumes and 117 scripts, Indiana Jones and the Last Crusade EGA
    /// 100 and 130. The demos and other games differ and stay unknown without their executable.
    /// </summary>
    private static GameId IdentifyByIndex(string indexPath)
    {
        try
        {
            var entries = new LflIndexReader().Read(indexPath);
            var costumes = entries.Count(e => e.Kind == ResourceKind.Costume);
            var scripts = entries.Count(e => e.Kind == ResourceKind.Script);
            return (costumes, scripts) switch { (176, 117) => GameId.Loom, (100, 130) => GameId.IndianaJones3, _ => GameId.Unknown };
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException) { return GameId.Unknown; }
    }

    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".SOU", ".MIDI", ".MID", ".IMS" };

    public GameLibrary Scan(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullPath = Path.GetFullPath(folderPath);
        if (TryScanCd(fullPath) is { } cd) return cd;
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Game folder not found: {fullPath}");

        var files = Directory.EnumerateFiles(fullPath).ToArray();
        var lflFiles = files.Select(path => (Path: path, Id: ParseLflId(path)))
            .Where(item => item.Id.HasValue)
            .Select(item => (item.Path, Id: item.Id.GetValueOrDefault()))
            .OrderBy(item => item.Id)
            .ToArray();
        var executableNames = files.Select(path => Path.GetFileName(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The data decides first: the edition whose known hashes the LFL files match, then the game by its language-free files; only then the executable's name (the demos)
        // and, last, the shape of the index. So a copy without its executable (as a SCUMMVM-style install keeps it) or with a renamed one is still told.
        var lflByName = lflFiles.GroupBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Path, StringComparer.OrdinalIgnoreCase);
        var edition = KnownGameFiles.IdentifyEdition(lflByName);
        var gameId = edition?.Game ?? KnownGameFiles.IdentifyGame(lflByName) ?? GameId.Unknown;
        var recognizedBy = gameId == GameId.Unknown ? "" : "known data";
        // The self-running LOOM previews ship as LOOMDEMO.EXE with a subset of the game's rooms, same resource layout.
        var isDemo = executableNames.Contains("LOOMDEMO.EXE") || edition?.Demo == true;
        if (gameId == GameId.Unknown)
        {
            gameId = executableNames.Contains("LOOM.EXE") || isDemo ? GameId.Loom
                : executableNames.Contains("INDY3.EXE") ? GameId.IndianaJones3
                : GameId.Unknown;
            recognizedBy = gameId == GameId.Unknown ? "" : "executable name";
        }
        var hasIndex = lflFiles.Any(item => item.Id == 0);
        if (gameId == GameId.Unknown && hasIndex && IdentifyByIndex(lflFiles.First(item => item.Id == 0).Path) is var byShape && byShape != GameId.Unknown)
        {
            gameId = byShape;
            recognizedBy = "index shape only (inferred)";
        }
        var hasRoomFiles = lflFiles.Length > 1;

        var resources = new List<GameResource>();
        var chains = new Dictionary<string, IReadOnlyList<LflChainResource>?>(StringComparer.OrdinalIgnoreCase);
        var omitted = 0;
        var indexWasParsed = false;
        foreach (var item in lflFiles)
        {
            if (item.Id == 0)
            {
                resources.Add(new GameResource("Game index", ResourceKind.Other, item.Path, 0,
                    "00.LFL is an index resource, not a room."));
                if (hasIndex && hasRoomFiles)
                {
                    try
                    {
                        foreach (var indexed in new LflIndexReader().Read(item.Path))
                        {
                            var fileName = $"{indexed.ContainerFileNumber:00}.LFL";
                            var resourcePath = Path.Combine(fullPath, fileName);
                            if (indexed.Kind == ResourceKind.Room && !File.Exists(resourcePath)) continue;
                            // A demo's index is not a subset of the full game's: it keeps entries for resources its
                            // folder lacks (no container file) or whose offset is not where a resource starts.
                            if (isDemo && indexed.Kind != ResourceKind.Room && !StartsResource(chains, resourcePath, indexed.Offset))
                            {
                                omitted++;
                                continue;
                            }
                            var label = indexed.Kind switch
                            {
                                ResourceKind.Room => $"Room {indexed.ResourceId:00}",
                                ResourceKind.Costume => $"Costume {indexed.ResourceId:00}",
                                ResourceKind.Script => $"Script {indexed.ResourceId:00}",
                                ResourceKind.Sound => $"Sound {indexed.ResourceId:00}",
                                _ => $"{indexed.Kind} {indexed.ResourceId:00}"
                            };
                            var description = gameId == GameId.Loom && indexed.Kind == ResourceKind.Script
                                ? "Indexed by 00.LFL; DOS LOOM old-v3 bytecode has a partial disassembly view."
                                : "Indexed by 00.LFL; resource-specific inspection depends on the supported format readers.";
                            resources.Add(new GameResource(label, indexed.Kind, resourcePath,
                                indexed.ResourceId, description,
                                indexed.ContainerFileNumber, indexed.Offset,
                                indexed.DirectoryEntryOffset, indexed.ContainerNumberOffset));
                        }
                        indexWasParsed = true;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
                    {
                        // Keep the physical-file fallback useful for incomplete or
                        // unsupported LFL layouts instead of claiming a parse.
                    }
                }
                continue;
            }

            if (!indexWasParsed)
                resources.Add(new GameResource($"Room file {item.Id:00}", ResourceKind.Other, item.Path, item.Id,
                    "Physical LFL file; no supported index was parsed."));
        }

        if (isDemo && indexWasParsed) omitted += DropUnreadableDemoResources(fullPath, resources);

        foreach (var path in files.Where(path => SoundExtensions.Contains(Path.GetExtension(path))))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Sound, path));

        foreach (var path in files.Where(path => Path.GetExtension(path).Equals(".EXE", StringComparison.OrdinalIgnoreCase)))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Executable, path));

        var title = gameId switch
        {
            GameId.Loom => isDemo ? "LOOM (demo)" : "LOOM",
            GameId.IndianaJones3 => "Indiana Jones and the Last Crusade",
            _ => Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        };
        var version = indexWasParsed ? ScummVersion.V3 : ScummVersion.Unknown;
        var note = gameId switch
        {
            GameId.Loom when indexWasParsed && isDemo =>
                $"LOOM self-running preview (LOOMDEMO.EXE): a subset of the game's rooms. Its 00.LFL index also lists {omitted} scripts, costumes and sounds that this folder does not contain; they are left out.",
            GameId.Loom when indexWasParsed =>
                "DOS LOOM profile inferred from its files; the 00.LFL SCUMM v3 signature and resource directories were read.",
            GameId.Loom =>
                "Likely DOS LOOM with the individual-LFL layout. The SCUMM version is not confirmed because the index could not be parsed.",
            GameId.IndianaJones3 when indexWasParsed =>
                "DOS EGA profile inferred from INDY3.EXE and the old-v3 00.LFL resource directory. Room backgrounds are supported; object and script behavior is not yet verified.",
            GameId.IndianaJones3 =>
                "Indiana Jones and the Last Crusade files found. The SCUMM version is not confirmed because no supported old-v3 index was parsed.",
            _ when indexWasParsed =>
                "The old-v3 SCUMM index in 00.LFL was parsed; game identity and resource-specific support are not confirmed.",
            _ => "Folder contents found. Game and SCUMM version are not confirmed yet."
        };

        if (recognizedBy.Length > 0 && gameId != GameId.Unknown)
            note += edition is not null ? $" Recognised by known data: {edition.Label}." : $" Recognised by: {recognizedBy}.";
        return new GameLibrary(title, fullPath, version, "DOS (inferred)", resources, note, gameId, isDemo, edition?.Label ?? "");
    }

    /// <summary>
    /// The LOOM CD game: a .cue, .iso or .bin file, a folder holding one, or a folder with the files copied off the disc
    /// (000.LFL and DISK01.LEC). Null when the path is none of these, so the folder scan goes on.
    /// </summary>
    /// <summary>The SHA-256 fingerprint of the LOOM CD data (both local dumps, the Kixx release included; see <c>ScummV4Archive.Fingerprint</c>).</summary>
    private const string LoomCdFingerprint = "8A23DBBAFC432CE3F428DF15AFA47B6ED4F38117BF9490ED7DDB0F6F23E6318B-A13D9B5D5EBDA838014293E5A9BF131D4BF7DC792DC3A0AE193838B8B3395A05";

    /// <summary>
    /// Whether a v4 archive is LOOM CD: its known fingerprint, or (another release of the disc) 78 rooms, 165 costumes, 637 scripts and 30 sounds, the counts of both
    /// local dumps.
    /// </summary>
    public static bool IsLoomCd(string fingerprint, int rooms, int costumes, int scripts, int sounds) =>
        string.Equals(fingerprint, LoomCdFingerprint, StringComparison.OrdinalIgnoreCase) || (rooms, costumes, scripts, sounds) == (78, 165, 637, 30);

    private static GameLibrary? TryScanCd(string path)
    {
        IGameFileSource source;
        if (File.Exists(path))
        {
            if (!DiscImage.IsImageFile(path)) return null;
            source = new DiscFileSource(path);
        }
        else if (Directory.Exists(path))
        {
            var folder = new FolderFileSource(path);
            if (ScummV4Archive.IsVga(folder)) source = folder;
            else if (folder.FileNames.Any(n => n.Equals("00.LFL", StringComparison.OrdinalIgnoreCase)) || DiscImage.FindImageIn(path) is null) return null;
            else
            {
                try { source = new DiscFileSource(path); }
                catch (Exception ex) when (ex is InvalidDataException or IOException) { return null; }
            }
        }
        else return null;
        if (!ScummV4Archive.IsVga(source)) return null;

        var archive = ScummV4Archive.Load(source);
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
        // 000.LFL + DISK01.LEC is the container of every game of this SCUMM generation, not only LOOM CD: claim LOOM only for the known data (the fingerprint of the
        // two dumps), or, for another release of the disc, by the size of its resource directories; anything else is reported as an unsupported v4 game.
        if (!IsLoomCd(archive.Fingerprint, archive.Rooms.Count, resources.Count(r => r.Kind == ResourceKind.Costume), resources.Count(r => r.Kind == ResourceKind.Script), resources.Count(r => r.Kind == ResourceKind.Sound)))
            return ScanOtherV4(source);
        const string note = "LOOM CD (SCUMM v4, VGA) read from its disc image or files: 000.LFL and DISK01.LEC. Rooms, scripts, objects, actors, texts, the sounds in DISK01.LEC (AdLib and PC speaker) and the voices on the disc's audio track are read.";
        return new GameLibrary("LOOM (CD)", source.Location, ScummVersion.V4, "CD (VGA)", resources, note, GameId.LoomVga) { V4Archive = archive };
    }

    /// <summary>
    /// A v4 game that is not LOOM CD (Monkey Island, for one), browsed from every disk it has: its rooms, costumes and sounds, with no session. The game is not named
    /// (<see cref="GameId.Unknown"/>), so no game-specific feature switches on. If no room of any disk decodes (the 16-colour EGA rooms are stored in another image format),
    /// nothing is offered rather than rooms that cannot be shown.
    /// </summary>
    private static GameLibrary ScanOtherV4(IGameFileSource source)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source.Location));
        var parentName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source.Location)) ?? ""));
        // A folder named after its graphics ("EGA", "VGA") is told by the game folder around it.
        var title = folderName.Equals("EGA", StringComparison.OrdinalIgnoreCase) || folderName.Equals("VGA", StringComparison.OrdinalIgnoreCase)
            ? $"{parentName} {folderName}".Trim() : folderName;
        var disks = ScummV4Disks.Load(source);
        var resources = new List<GameResource>();
        var decodable = false;
        for (var i = 0; i < disks.Disks.Count; i++)
        {
            var disk = i + 1;
            var archive = disks.Disks[i];
            var diskName = ScummV4Archive.DiskFileName(disk);
            foreach (var room in archive.Rooms)
                resources.Add(new GameResource($"Room {room.Number:00}", ResourceKind.Room, source.Location, room.Number,
                    string.IsNullOrEmpty(room.Name) ? $"Room on {diskName}." : $"Room \"{room.Name}\" on {diskName}.", disk, room.Offset));
            foreach (var costume in archive.ReadCostumes())
                resources.Add(new GameResource($"Costume {costume.Id:00}", ResourceKind.Costume, source.Location, costume.Id,
                    $"Costume stored in room {costume.Room:00} on {diskName}.", disk, null));
            foreach (var sound in archive.ReadSounds())
                resources.Add(new GameResource($"Sound {sound.Id:00}", ResourceKind.Sound, source.Location, sound.Id,
                    $"{(sound.IsMusic ? "Music" : "Sound effect")} stored in room {sound.Room:00} on {diskName}.", disk, null));
            if (!decodable && archive.Rooms.Count > 0)
            {
                try { decodable = archive.DecodeRoom(archive.Rooms[0].Number).IsValid; }
                catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException) { }
            }
        }
        if (!decodable)
            return new GameLibrary(title, source.Location, ScummVersion.V4, "DOS (SCUMM v4)", [],
                $"The folder holds a SCUMM v4 game ({disks.Disks.Count} disks) that is not LOOM CD. Its rooms are not in a format this explorer reads yet (the 16-colour EGA version), so it is not supported yet.");
        var ordered = resources.OrderBy(r => r.Kind).ThenBy(r => r.ResourceId).ToList();
        return new GameLibrary(title, source.Location, ScummVersion.V4, "DOS (SCUMM v4)", ordered,
            $"A SCUMM v4 game that is not LOOM CD, read from its {disks.Disks.Count} disks (000.LFL and DISK01.LEC to DISK{disks.Disks.Count:00}.LEC). Room backgrounds and costumes are shown; " +
            "objects, scripts, sounds and interaction are not read for this game yet.") { V4Disks = disks };
    }

    /// <summary>
    /// Some stale entries of a demo's index start a real resource of another kind (two entries claim one start) or a
    /// resource that is not what the entry says. An entry stays only if its resource reads as its kind: a script
    /// decodes without issues, a costume loads (sounds are only checked for where they start); where a script and
    /// a costume or sound claim the same start, the costume or sound wins when it reads. Returns how many were dropped.
    /// </summary>
    private static int DropUnreadableDemoResources(string folder, List<GameResource> resources)
    {
        var provisional = new GameLibrary("LOOM (demo)", folder, ScummVersion.V3, "DOS (inferred)", resources, "", GameId.Loom, true);
        var scripts = new ScummScriptReader(ScummScriptDialect.V3);
        bool Reads(GameResource resource)
        {
            try
            {
                return resource.Kind switch
                {
                    ResourceKind.Script => scripts.Decode(resource) is { Issues: not { Count: > 0 }, DecodeNote: null },
                    ResourceKind.Costume => resource.ResourceId is int id && ScummCostume.Load(provisional, id) is not null,
                    // Sounds are only checked for where they start: half of the full game's sounds have no AdLib or PC speaker bundle either.
                    _ => true
                };
            }
            catch (Exception) { return false; }
        }
        var indexed = resources.Where(r => r.Kind is ResourceKind.Script or ResourceKind.Costume or ResourceKind.Sound).ToArray();
        var reads = indexed.ToDictionary(r => r, Reads);
        var drop = indexed.Where(r => !reads[r]).ToHashSet();
        foreach (var shared in indexed.GroupBy(r => (r.ContainerFileNumber, r.Offset)).Where(g => g.Select(r => r.Kind).Distinct().Count() > 1))
            if (shared.Any(r => r.Kind != ResourceKind.Script && reads[r]))
                foreach (var script in shared.Where(r => r.Kind == ResourceKind.Script)) drop.Add(script);
        resources.RemoveAll(drop.Contains);
        return drop.Count;
    }

    private static bool StartsResource(Dictionary<string, IReadOnlyList<LflChainResource>?> chains, string path, int offset)
    {
        if (!chains.TryGetValue(path, out var chain))
        {
            try { chain = File.Exists(path) ? LflResourceChain.Read(File.ReadAllBytes(path)) : null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { chain = null; }
            chains[path] = chain;
        }
        return chain is not null && chain.Any(item => item.Start == offset);
    }

    private static int? ParseLflId(string path)
    {
        if (!Path.GetExtension(path).Equals(".LFL", StringComparison.OrdinalIgnoreCase)) return null;
        var stem = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id is >= 0 and <= 999
            ? id : null;
    }
}

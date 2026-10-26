using System.Globalization;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// A game of one file per room with an index in <c>00.LFL</c> (SCUMM v1 to v3: Maniac Mansion and Zak McKracken, LOOM and Indiana Jones 3 EGA): the index is
/// read in the format it is in (classic v1, enhanced v2 or old v3), the game is identified from its files and index (<see cref="GameIdentifier"/>), and the
/// resources the index lists are offered when their containers hold them.
/// </summary>
internal static class LflFolderLayout
{
    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase) { ".SOU", ".MIDI", ".MID", ".IMS" };

    private const string Platform = "DOS (inferred)";

    /// <summary>The index of the folder in the format it is in: its version, the game a classic v1 index names, and its entries.</summary>
    private sealed record OldIndex(ScummVersion Version, ScummV1Game? V1Game, IReadOnlyList<IndexedResource> Entries)
    {
        public DirectoryShape Shape => new(Version, Count(ResourceKind.Room), Count(ResourceKind.Costume), Count(ResourceKind.Script), Count(ResourceKind.Sound));

        private int Count(ResourceKind kind) => Entries.Count(e => e.Kind == kind);

        /// <summary>v1 and v2 games: entries are offered only when their resource reads (the directories also list unused entries).</summary>
        public bool IsOlder => Version is ScummVersion.V1 or ScummVersion.V2;
    }

    /// <summary>The folder's files: every file, the numbered LFL files in order, the index and what the game is told by.</summary>
    private sealed record FolderData(string[] Files, (string Path, int Id)[] LflFiles, string? IndexPath, OldIndex? Index, GameFingerprint Fingerprint);

    private static FolderData Read(string folder)
    {
        var files = Directory.EnumerateFiles(folder).ToArray();
        var lflFiles = files.Select(path => (Path: path, Id: ParseLflId(path))).Where(item => item.Id.HasValue)
            .Select(item => (item.Path, Id: item.Id.GetValueOrDefault())).OrderBy(item => item.Id).ToArray();
        var indexPath = lflFiles.Where(item => item.Id == 0).Select(item => item.Path).FirstOrDefault();
        var index = indexPath is null ? null : ReadIndex(indexPath);
        var lflByName = lflFiles.GroupBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (Func<byte[]>)(() => File.ReadAllBytes(g.First().Path)), StringComparer.OrdinalIgnoreCase);
        return new FolderData(files, lflFiles, indexPath, index, new GameFingerprint(lflByName, () => index?.Shape));
    }

    public static GameFingerprint Fingerprint(string folder) => Read(folder).Fingerprint;

    public static GameLibrary Scan(string folder)
    {
        var (files, lflFiles, indexPath, index, fingerprint) = Read(folder);
        var hasRoomFiles = lflFiles.Length > 1;
        var identity = GameIdentifier.Identify(fingerprint, index?.V1Game switch
        {
            ScummV1Game.ManiacMansion => KnownGame.ManiacMansion,
            ScummV1Game.ZakMcKracken => KnownGame.ZakMcKracken,
            _ => KnownGame.Unknown
        });

        var resources = new List<GameResource>();
        if (indexPath is not null)
            resources.Add(new GameResource("Game index", ResourceKind.Other, indexPath, 0, "00.LFL is an index resource, not a room."));
        var omitted = 0;
        var listed = index is not null && hasRoomFiles ? ListIndexed(folder, index, identity, ref omitted) : null;
        if (listed is not null)
        {
            resources.AddRange(listed);
            if (identity.IsDemo) omitted += DropUnreadableDemoResources(folder, resources, identity, index!.Version);
        }
        else
            foreach (var item in lflFiles.Where(item => item.Id != 0))
                resources.Add(new GameResource($"Room file {item.Id:00}", ResourceKind.Other, item.Path, item.Id, "Physical LFL file; no supported index was parsed."));

        foreach (var path in files.Where(path => SoundExtensions.Contains(Path.GetExtension(path))))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Sound, path));
        foreach (var path in files.Where(path => Path.GetExtension(path).Equals(".EXE", StringComparison.OrdinalIgnoreCase)))
            resources.Add(new GameResource(Path.GetFileName(path), ResourceKind.Executable, path));

        var parsed = listed is not null;
        var version = parsed ? index!.Version : ScummVersion.Unknown;
        var title = identity.Game == KnownGame.Unknown ? Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)) : KnownGames.TitleOf(identity.Game, version, identity.IsDemo);
        var note = LayoutNote(parsed ? index : null, omitted) + " " + identity.Note;
        var id = GameIds.For(identity.Game, version);
        var library = new GameLibrary(title, folder, version, Platform, resources, note, id, identity.IsDemo, identity.EditionLabel)
        {
            Game = identity.Game, V1Game = index?.V1Game
        };
        return parsed ? WithRoomLocalScripts(library) : library;
    }

    /// <summary>
    /// R-5.9, R-6.51: a room's local scripts are tree entries (<c>Room NN local script N</c>) in every game, as in LOOM CD, so the Advanced panel's
    /// event buttons can open them. The old-v3 index lists only global scripts; the local ones are read from the rooms through the game's data source
    /// and kept apart from <see cref="GameLibrary.Resources"/> (whose scripts all readers treat as the index's global ones).
    /// </summary>
    private static GameLibrary WithRoomLocalScripts(GameLibrary library)
    {
        try
        {
            var data = ScummDataSources.For(library);
            var local = new List<GameResource>();
            foreach (var room in library.Rooms)
            {
                if (room.ResourceId is not int number) continue;
                try
                {
                    foreach (var id in data.RoomLocalScriptIds(number))
                        local.Add(new GameResource($"Room {number:00} local script {id}", ResourceKind.Script, room.FilePath, id,
                            $"Room-local script of room {number:00}.", room.ContainerFileNumber, null, ScriptRoom: number));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or EndOfStreamException or ArgumentException) { }
            }
            return local.Count == 0 ? library : library with { RoomLocalScripts = local };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or EndOfStreamException or ArgumentException)
        {
            return library;
        }
    }

    private static string LayoutNote(OldIndex? index, int omitted) => (index?.Version switch
    {
        ScummVersion.V1 => "A classic SCUMM v1 game: the fixed-size 00.LFL index and its room, costume, script and sound resource directories were read.",
        ScummVersion.V2 => "An enhanced SCUMM v2 game: the 00.LFL index (one byte per object) and its room, costume, script and sound resource directories were read.",
        ScummVersion.V3 => "An old-v3 SCUMM game: the 00.LFL index and its room, costume, script and sound resource directories were read.",
        _ => "Folder contents found. The SCUMM version is not confirmed because no supported 00.LFL index was parsed."
    }) + (omitted > 0 ? $" The index also lists {omitted} scripts, costumes and sounds that this folder does not contain; they are left out." : "");

    /// <summary>
    /// The index in its format: a classic v1 index (its fixed per-game directory sizes add up to the file), an enhanced v2 one (one byte per global object) or
    /// an old-v3 one (four bytes per object); null when it reads as none of them.
    /// </summary>
    private static OldIndex? ReadIndex(string path)
    {
        try
        {
            var reader = new LflIndexReader();
            var objectEntryBytes = reader.DetectObjectEntryBytes(path);
            if (objectEntryBytes == 0 && ScummV1Index.TryRead(path) is { } classic) return new OldIndex(ScummVersion.V1, classic.Game, classic.Resources);
            return objectEntryBytes == 1 ? new OldIndex(ScummVersion.V2, null, reader.Read(path, 1)) : new OldIndex(ScummVersion.V3, null, reader.Read(path, 4));
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException) { return null; }
    }

    /// <summary>The resources the index lists whose container holds them; <paramref name="omitted"/> counts a demo's entries that point at nothing.</summary>
    private static List<GameResource> ListIndexed(string folder, OldIndex index, GameIdentity identity, ref int omitted)
    {
        var resources = new List<GameResource>();
        var chains = new Dictionary<string, IReadOnlyList<LflChainResource>?>(StringComparer.OrdinalIgnoreCase);
        // Indy3 EGA's costume directory also lists empty entries (Costume 00), as the v1 and v2 directories do; LOOM's lists none.
        var checksCostumes = index.IsOlder || identity.Game == KnownGame.IndianaJones3;
        foreach (var indexed in index.Entries)
        {
            var path = Path.Combine(folder, $"{indexed.ContainerFileNumber:00}.LFL");
            if (indexed.Kind == ResourceKind.Room && !File.Exists(path)) continue;
            // An enhanced v2 game's room directory also points at room 0, which is the index file itself: only a room whose picture decodes is offered.
            if (index.IsOlder && indexed.Kind == ResourceKind.Room && !DecodesAsOlderRoom(path, index.V1Game)) continue;
            if (checksCostumes && indexed.Kind == ResourceKind.Costume && !ReadsAsOlderCostume(path, indexed.Offset)) continue;
            if (index.IsOlder && indexed.Kind == ResourceKind.Sound && !ReadsAsOlderSound(path, indexed.Offset)) continue;
            if (index.IsOlder && indexed.Kind == ResourceKind.Script && !ReadsAsOlderScript(path, indexed.Offset)) continue;
            // A demo's index is not a subset of the full game's: it keeps entries for resources its folder lacks (no container file) or whose offset is not
            // where a resource starts.
            if (identity.IsDemo && indexed.Kind != ResourceKind.Room && !StartsResource(chains, path, indexed.Offset))
            {
                omitted++;
                continue;
            }
            resources.Add(new GameResource($"{indexed.Kind} {indexed.ResourceId:00}", indexed.Kind, path, indexed.ResourceId,
                "Indexed by 00.LFL; resource-specific inspection depends on the supported format readers.",
                indexed.ContainerFileNumber, indexed.Offset, indexed.DirectoryEntryOffset, indexed.ContainerNumberOffset));
        }
        return resources;
    }

    /// <summary>
    /// Some stale entries of a demo's index start a real resource of another kind (two entries claim one start) or a resource that is not what the entry says. An
    /// entry stays only if its resource reads as its kind: a script decodes without issues, a costume loads (sounds are only checked for where they start); where
    /// a script and a costume or sound claim the same start, the costume or sound wins when it reads. Returns how many were dropped.
    /// </summary>
    private static int DropUnreadableDemoResources(string folder, List<GameResource> resources, GameIdentity identity, ScummVersion version)
    {
        var id = GameIds.For(identity.Game, version);
        var provisional = new GameLibrary("", folder, version, Platform, resources, "", id, true) { Game = identity.Game };
        var scripts = new ScummScriptReader(ScummProfile.For(provisional).Dialect);
        bool Reads(GameResource resource)
        {
            try
            {
                return resource.Kind switch
                {
                    ResourceKind.Script => scripts.Decode(resource) is { Issues: not { Count: > 0 }, DecodeNote: null },
                    ResourceKind.Costume => resource.ResourceId is int costume && ScummCostume.Load(provisional, costume) is not null,
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

    /// <summary>A v1/v2 script entry whose size word fits inside its room file (the directory also lists unused entries).</summary>
    private static bool ReadsAsOlderScript(string path, int offset)
    {
        try { return File.Exists(path) && ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(path)), offset) is not null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool ReadsAsOlderCostume(string path, int offset)
    {
        try { return new ScummV3CostumeReader().Decode(LflEncoding.DecodeXorFF(File.ReadAllBytes(path)), offset).AnimationSlots.Any(slot => slot.HasData); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    /// <summary>
    /// Whether the entry is a PC speaker sound: of the v2 player's format (the one LOOM's old bundle uses; a length word, the priority and restart bytes, then the channel
    /// pointers, the first at 22 and the other three empty), which Zak McKracken classic and the enhanced games have, or of classic Maniac Mansion's chunk format.
    /// </summary>
    private static bool ReadsAsOlderSound(string path, int offset)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(path));
            if (offset < 0 || offset + 12 > data.Length) return false;
            var size = data[offset] | data[offset + 1] << 8;
            if (size < 12 || offset + size > data.Length) return false;
            var sound = data.AsSpan(offset, size).ToArray();
            if (ScummPcSpeakerV1Audio.IsV2Format(sound)) return size >= 22;
            // Classic Maniac Mansion's own chunk format: zero bytes after the priority, a known first chunk, and at least one note.
            return ScummPcSpeakerV1Audio.LooksLikeV1(sound) && ScummPcSpeakerV1Audio.HasNotes(sound);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool DecodesAsOlderRoom(string path, ScummV1Game? v1Game)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return (v1Game is { } game ? new ScummV1RoomDecoder(game).Decode(bytes) : new ScummV2RoomDecoder().Decode(bytes)).IsValid;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException) { return false; }
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
        return int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id is >= 0 and <= 999 ? id : null;
    }
}

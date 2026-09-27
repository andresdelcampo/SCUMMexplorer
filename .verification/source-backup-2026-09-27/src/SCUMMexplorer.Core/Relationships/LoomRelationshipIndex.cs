using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Relationships;

public enum LoomRelationshipStatus
{
    Established,
    StaticCandidate,
    Unresolved
}

/// <summary>A source-backed link between parsed LOOM game elements.</summary>
public sealed record LoomRelationship(
    GameResource SourceResource,
    string Relationship,
    GameResource? TargetResource,
    string TargetLabel,
    string Evidence,
    LoomRelationshipStatus Status);

/// <summary>An immutable index of relationships established by the supported LOOM v3 readers.</summary>
public sealed class LoomRelationshipIndex
{
    public static LoomRelationshipIndex Empty { get; } = new([]);

    public LoomRelationshipIndex(IEnumerable<LoomRelationship> relationships)
    {
        Relationships = relationships.ToArray();
    }

    public IReadOnlyList<LoomRelationship> Relationships { get; }

    public int UnresolvedCount => Relationships.Count(item => item.Status == LoomRelationshipStatus.Unresolved);

    public IReadOnlyList<LoomRelationship> ForResource(GameResource resource) =>
        Relationships.Where(item => item.SourceResource == resource || item.TargetResource == resource).ToArray();
}

/// <summary>
/// Connects indexed LOOM resources, room-local object records, embedded entry-script pointers,
/// and literal targets from the currently recognized script instructions.
/// </summary>
public sealed class LoomRelationshipIndexer
{
    private const int EntryScriptPointerOffset = 0x1B;
    private static readonly Regex LiteralNumber = new(@"^0x(?<value>[0-9A-Fa-f]+)(?:$|[\s,])", RegexOptions.CultureInvariant);
    private static readonly Regex CostumeOperation = new(@"(?:\[|;\s*)costume,\s*(?<value>0x[0-9A-Fa-f]+)", RegexOptions.CultureInvariant);
    private readonly LoomScriptReader _scriptReader = new();
    private readonly LoomObjectVerbReader _verbReader = new();
    private readonly LoomRoomObjectDirectory _objectDirectory = new();

    public LoomRelationshipIndex Build(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != ScummVersion.V3) return LoomRelationshipIndex.Empty;

        var relationships = new List<LoomRelationship>();
        var roomResources = game.Resources
            .Where(resource => resource.Kind == ResourceKind.Room && resource.ResourceId.HasValue)
            .GroupBy(resource => resource.ResourceId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var resourcesByKey = game.Resources
            .Where(resource => resource.ResourceId.HasValue)
            .GroupBy(resource => (resource.Kind, Id: resource.ResourceId!.Value))
            .ToDictionary(group => group.Key, group => group.First());
        var roomContexts = new Dictionary<int, RoomContext>();

        foreach (var room in roomResources.Values.OrderBy(resource => resource.ResourceId))
            ReadRoomRelationships(room, roomContexts, relationships);

        AddObjectVerbRelationships(roomResources, roomContexts, relationships);
        AddContainerRelationships(game, roomResources, relationships);

        var objectsById = roomContexts.Values
            .SelectMany(context => context.Objects.Select(item => new ObjectLocation(context.Room, item)))
            .GroupBy(location => location.Record.Id)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var script in game.Resources.Where(resource => resource.Kind == ResourceKind.Script && resource.ResourceId.HasValue))
            ReadScriptRelationships(script, resourcesByKey, roomResources, roomContexts, objectsById, relationships);

        return new LoomRelationshipIndex(relationships);
    }

    private void AddObjectVerbRelationships(
        IReadOnlyDictionary<int, GameResource> roomResources,
        IReadOnlyDictionary<int, RoomContext> roomContexts,
        ICollection<LoomRelationship> relationships)
    {
        foreach (var context in roomContexts.Values)
        {
            IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> tables;
            try { tables = _verbReader.ReadAll(File.ReadAllBytes(context.Room.FilePath), context.Objects); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
            foreach (var record in context.Objects)
            {
                if (!tables.TryGetValue(record.Id, out var verbs)) continue;
                foreach (var verb in verbs)
                {
                    LoomScriptDisassembly script;
                    try { script = _verbReader.Decode(context.Room, record, verb.VerbId); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
                    foreach (var instruction in script.Instructions)
                    {
                        var roomLoad = LoomScriptEvent.FromInstruction(script, instruction,
                            LoomScriptEventCertainty.StaticPotential);
                        if (roomLoad is not { Kind: LoomScriptEventKind.RoomLoaded, TargetId: int targetId } ||
                            !roomResources.TryGetValue(targetId, out var target)) continue;
                        relationships.Add(new LoomRelationship(context.Room, "object verb may load room",
                            target, $"Object {record.Id} · {verb.Label} · possible Room {targetId:00}",
                            $"{Path.GetFileName(context.Room.FilePath)}+0x{verb.ScriptOffset + instruction.Offset:X4}: " +
                            $"object {record.Id} {verb.Label} contains {instruction.Operation}; execution path is unverified.",
                            LoomRelationshipStatus.StaticCandidate));
                    }
                }
            }
        }
    }

    private void ReadRoomRelationships(
        GameResource room,
        IDictionary<int, RoomContext> roomContexts,
        ICollection<LoomRelationship> relationships)
    {
        var roomId = room.ResourceId!.Value;
        try
        {
            var encoded = File.ReadAllBytes(room.FilePath);
            var data = LflEncoding.DecodeXorFF(encoded);
            var objects = _objectDirectory.Decode(encoded);
            var context = new RoomContext(room, objects);
            roomContexts[roomId] = context;

            foreach (var obj in objects)
            {
                var imageText = obj.ImageOffset is int imageOffset
                    ? $"image at file offset 0x{imageOffset:X4}"
                    : "no separate image record";
                relationships.Add(new LoomRelationship(
                    room,
                    "contains local object record",
                    null,
                    $"Object ID {obj.Id} · local record {obj.LocalIndex} · code at 0x{obj.CodeOffset:X4} · {imageText}",
                    $"{Path.GetFileName(room.FilePath)}+0x{obj.CodeOffset:X4}: room object code record for ID {obj.Id}.",
                    LoomRelationshipStatus.Established));
            }

            if (data.Length <= EntryScriptPointerOffset + 1)
            {
                relationships.Add(new LoomRelationship(
                    room,
                    "entry-script pointer",
                    null,
                    "Room header is too short to read the pointer",
                    $"{Path.GetFileName(room.FilePath)}: bytes 0x001B–0x001C are unavailable.",
                    LoomRelationshipStatus.Unresolved));
                return;
            }

            var roomLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
            var entryOffset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(EntryScriptPointerOffset, 2));
            if (entryOffset == 0)
            {
                relationships.Add(new LoomRelationship(
                    room,
                    "entry-script pointer",
                    null,
                    "No room entry-script offset is stored",
                    $"{Path.GetFileName(room.FilePath)}+0x{EntryScriptPointerOffset:X4} contains 0x0000.",
                    LoomRelationshipStatus.Established));
                return;
            }

            var validOffset = entryOffset >= 0x1D && entryOffset < roomLength && entryOffset < data.Length;
            relationships.Add(new LoomRelationship(
                room,
                "entry-script pointer",
                null,
                validOffset
                    ? $"Embedded room entry script at file offset 0x{entryOffset:X4} · body not disassembled"
                    : $"Entry-script offset 0x{entryOffset:X4} is outside the declared room data",
                $"{Path.GetFileName(room.FilePath)}+0x{EntryScriptPointerOffset:X4} stores 0x{entryOffset:X4}.",
                validOffset ? LoomRelationshipStatus.Established : LoomRelationshipStatus.Unresolved));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            relationships.Add(new LoomRelationship(
                room,
                "room data",
                null,
                "Object directory or entry-script pointer could not be read",
                $"{Path.GetFileName(room.FilePath)}: {exception.Message}",
                LoomRelationshipStatus.Unresolved));
        }
    }

    private static void AddContainerRelationships(
        GameLibrary game,
        IReadOnlyDictionary<int, GameResource> roomResources,
        ICollection<LoomRelationship> relationships)
    {
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        foreach (var resource in game.Resources.Where(item =>
                     item.Kind is ResourceKind.Costume or ResourceKind.Script or ResourceKind.Sound &&
                     item.ResourceId.HasValue && item.ContainerFileNumber.HasValue))
        {
            var containerNumber = resource.ContainerFileNumber!.Value;
            var indexEvidence = resource.IndexDirectoryOffset is int entryOffset
                ? $"00.LFL+0x{entryOffset:X4}"
                : "00.LFL resource directory";
            if (resource.ContainerNumberOffset is int containerOffset)
                indexEvidence += $"; container byte at +0x{containerOffset:X4}";

            if (roomResources.TryGetValue(containerNumber, out var room))
            {
                relationships.Add(new LoomRelationship(
                    resource,
                    "is stored in room container",
                    room,
                    room.Name,
                    $"{indexEvidence} identifies container room {containerNumber:00} for {resource.Kind} {resource.ResourceId:00}.",
                    LoomRelationshipStatus.Established));
            }
            else
            {
                relationships.Add(new LoomRelationship(
                    resource,
                    "is stored in room container",
                    null,
                    $"Room {containerNumber:00} is not present in the indexed room list",
                    $"{indexPath}: {indexEvidence} identifies container room {containerNumber:00}.",
                    LoomRelationshipStatus.Unresolved));
            }
        }
    }

    private void ReadScriptRelationships(
        GameResource script,
        IReadOnlyDictionary<(ResourceKind Kind, int Id), GameResource> resourcesByKey,
        IReadOnlyDictionary<int, GameResource> roomResources,
        IReadOnlyDictionary<int, RoomContext> roomContexts,
        IReadOnlyDictionary<int, ObjectLocation[]> objectsById,
        ICollection<LoomRelationship> relationships)
    {
        LoomScriptDisassembly disassembly;
        try
        {
            disassembly = _scriptReader.Decode(script);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
        {
            relationships.Add(new LoomRelationship(
                script,
                "script decode",
                null,
                "Script resource could not be decoded",
                $"{Path.GetFileName(script.FilePath)}+0x{script.Offset.GetValueOrDefault():X4}: {exception.Message}",
                LoomRelationshipStatus.Unresolved));
            return;
        }

        foreach (var instruction in disassembly.Instructions)
        {
            var absoluteOffset = disassembly.ResourceOffset + 4 + instruction.Offset;
            var operationEvidence = string.IsNullOrEmpty(instruction.Operands)
                ? instruction.Operation
                : $"{instruction.Operation} {instruction.Operands}";
            var evidence = $"{Path.GetFileName(script.FilePath)}+0x{absoluteOffset:X4} ({instruction.Bytes}) · {operationEvidence}";
            foreach (var candidate in ExtractCandidates(instruction))
            {
                if (candidate.TargetId is not int targetId)
                {
                    relationships.Add(new LoomRelationship(
                        script,
                        candidate.Relationship,
                        null,
                        $"Dynamic {candidate.TargetKind} target: {candidate.Operand}",
                        evidence,
                        LoomRelationshipStatus.Unresolved));
                    continue;
                }

                if (candidate.TargetKind == ResourceKind.Object)
                {
                    AddObjectRelationship(script, candidate, targetId, roomResources, roomContexts, objectsById,
                        evidence, relationships);
                    continue;
                }

                if (resourcesByKey.TryGetValue((candidate.TargetKind, targetId), out var target))
                {
                    relationships.Add(new LoomRelationship(
                        script,
                        candidate.Relationship,
                        target,
                        target.Name,
                        evidence,
                        LoomRelationshipStatus.StaticCandidate));
                }
                else
                {
                    relationships.Add(new LoomRelationship(
                        script,
                        candidate.Relationship,
                        null,
                        $"{candidate.TargetKind} {targetId:00} is not present in the parsed index",
                        evidence,
                        LoomRelationshipStatus.Unresolved));
                }
            }
        }

        foreach (var issue in disassembly.Issues ?? [])
        {
            var codeOffset = issue.Offset;
            relationships.Add(new LoomRelationship(
                script,
                "unresolved bytecode path",
                null,
                $"Bytecode at +0x{codeOffset:X4} is unresolved: {issue.Reason}",
                $"{Path.GetFileName(script.FilePath)}+0x{disassembly.ResourceOffset + 4 + codeOffset:X4} is the unresolved path.",
                LoomRelationshipStatus.Unresolved));
        }
    }

    private static void AddObjectRelationship(
        GameResource script,
        ScriptReferenceCandidate candidate,
        int objectId,
        IReadOnlyDictionary<int, GameResource> roomResources,
        IReadOnlyDictionary<int, RoomContext> roomContexts,
        IReadOnlyDictionary<int, ObjectLocation[]> objectsById,
        string evidence,
        ICollection<LoomRelationship> relationships)
    {
        ObjectLocation[] matches = [];
        if (script.ContainerFileNumber is int containerRoom &&
            roomContexts.TryGetValue(containerRoom, out var context))
        {
            matches = context.Objects.Where(item => item.Id == objectId)
                .Select(item => new ObjectLocation(context.Room, item)).ToArray();
        }

        if (matches.Length == 0 && objectsById.TryGetValue(objectId, out var allMatches))
            matches = allMatches;

        if (matches.Length == 1)
        {
            var match = matches[0];
            var label = $"Object ID {objectId} · {match.Room.Name} · local record {match.Record.LocalIndex} · code +0x{match.Record.CodeOffset:X4}";
            relationships.Add(new LoomRelationship(script, candidate.Relationship,
                roomResources.GetValueOrDefault(match.Room.ResourceId!.Value), label, evidence,
                roomResources.ContainsKey(match.Room.ResourceId!.Value)
                    ? LoomRelationshipStatus.StaticCandidate
                    : LoomRelationshipStatus.Unresolved));
            return;
        }

        var detail = matches.Length > 1
            ? $"appears in multiple rooms ({string.Join(", ", matches.Select(item => item.Room.Name).Distinct())})"
            : "has no matching parsed local room record";
        relationships.Add(new LoomRelationship(script, candidate.Relationship, null,
            $"Object ID {objectId} {detail}", evidence, LoomRelationshipStatus.Unresolved));
    }

    private static IEnumerable<ScriptReferenceCandidate> ExtractCandidates(LoomScriptInstruction instruction)
    {
        var operands = instruction.Operands.Trim();
        var first = FirstArgument(operands);

        switch (instruction.Operation)
        {
            case "startScript":
                yield return ResourceCandidate("starts script", ResourceKind.Script, first);
                break;
            case "chainScript":
                yield return ResourceCandidate("chains to script", ResourceKind.Script, first);
                break;
            case "loadRoom":
                yield return ResourceCandidate("loads room", ResourceKind.Room, first);
                break;
            case "loadRoomWithEgo":
                yield return ResourceCandidate("loads room with actor", ResourceKind.Room, SecondArgument(operands));
                break;
            case "startMusic":
                yield return ResourceCandidate("starts music", ResourceKind.Sound, first);
                break;
            case "startSound":
                yield return ResourceCandidate("starts sound", ResourceKind.Sound, first);
                break;
            case "stopSound":
                yield return ResourceCandidate("stops sound", ResourceKind.Sound, first);
                break;
            case "isSoundRunning":
                yield return ResourceCandidate("checks sound state", ResourceKind.Sound, LastArgument(operands));
                break;
            case "setState":
                yield return ObjectCandidate("sets object state", first);
                break;
            case "drawObject":
                yield return ObjectCandidate("draws object", first);
                break;
            case "getObjectState":
                yield return ObjectCandidate("reads object state", LastArgument(operands));
                break;
            case "getObjectOwner":
                yield return ObjectCandidate("reads object owner", LastArgument(operands));
                break;
            case "setOwnerOf":
            case "pickupObject":
                yield return ObjectCandidate("changes object ownership", first);
                break;
            case "startObject":
                yield return ObjectCandidate("starts object verb", first);
                break;
            case "stopObjectScript":
                yield return ObjectCandidate("stops object script", first);
                break;
            case "ifClassOfIs":
                yield return ObjectCandidate("checks object class", first);
                break;
            case "actorOps":
                foreach (Match match in CostumeOperation.Matches(operands))
                    yield return ResourceCandidate("uses costume", ResourceKind.Costume, match.Groups["value"].Value);
                break;
            case "resourceRoutines":
                var parts = operands.Split(',', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && TryResourceRoutine(parts[0], out var targetKind, out var relation))
                    yield return ResourceCandidate(relation, targetKind, parts[1]);
                break;
        }
    }

    private static bool TryResourceRoutine(string operation, out ResourceKind targetKind, out string relationship)
    {
        var verb = operation.StartsWith("load", StringComparison.Ordinal) ? "loads"
            : operation.StartsWith("nuke", StringComparison.Ordinal) ? "unloads"
            : operation.StartsWith("lock", StringComparison.Ordinal) ? "locks"
            : operation.StartsWith("unlock", StringComparison.Ordinal) ? "unlocks"
            : "";
        var suffix = operation.StartsWith("load", StringComparison.Ordinal) ? operation[4..]
            : operation.StartsWith("nuke", StringComparison.Ordinal) ? operation[4..]
            : operation.StartsWith("lock", StringComparison.Ordinal) ? operation[4..]
            : operation.StartsWith("unlock", StringComparison.Ordinal) ? operation[6..]
            : "";
        targetKind = suffix switch
        {
            "Script" => ResourceKind.Script,
            "Sound" => ResourceKind.Sound,
            "Costume" => ResourceKind.Costume,
            "Room" => ResourceKind.Room,
            _ => ResourceKind.Other
        };
        relationship = targetKind == ResourceKind.Other ? "" : $"{verb} {suffix.ToLowerInvariant()} resource";
        return targetKind != ResourceKind.Other;
    }

    private static ScriptReferenceCandidate ResourceCandidate(string relationship, ResourceKind kind, string operand) =>
        new(relationship, kind, ReadLiteral(operand), operand);

    private static ScriptReferenceCandidate ObjectCandidate(string relationship, string operand) =>
        new(relationship, ResourceKind.Object, ReadLiteral(operand), operand);

    private static int? ReadLiteral(string operand)
    {
        var match = LiteralNumber.Match(operand.Trim());
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string FirstArgument(string value) => value.Split(',', 2, StringSplitOptions.TrimEntries)[0];

    private static string LastArgument(string value)
    {
        var comma = value.LastIndexOf(',');
        return comma < 0 ? value : value[(comma + 1)..].Trim();
    }

    private static string SecondArgument(string value)
    {
        var arguments = value.Split(',', StringSplitOptions.TrimEntries);
        return arguments.Length >= 2 ? arguments[1] : "";
    }

    private sealed record RoomContext(GameResource Room, IReadOnlyList<LoomRoomObjectRecord> Objects);
    private sealed record ObjectLocation(GameResource Room, LoomRoomObjectRecord Record);
    private sealed record ScriptReferenceCandidate(string Relationship, ResourceKind TargetKind, int? TargetId, string Operand);
}

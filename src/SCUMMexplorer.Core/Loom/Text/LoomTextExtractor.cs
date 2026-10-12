using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public sealed record LoomTextEntry(string Text, string Kind, string Source, int? RoomId,
    int? ObjectId, int? VerbId, int? ScriptId, int CodeOffset,
    string? ObjectName = null, string? ConversationId = null, int? ConversationLine = null, string? ConversationKind = null, string? Speaker = null,
    int? SourceObjectId = null, string? SourceObjectName = null, int? ChoiceVerbId = null)
{
    /// <summary>True for a player-selectable dialogue label in a choice set.</summary>
    public bool IsPlayerChoice => ChoiceVerbId.HasValue && Kind == "Verb label";
}

public sealed record LoomTextExtraction(IReadOnlyList<LoomTextEntry> Entries,
    int DecodedBytes, int CandidateBytes, int UnresolvedPaths, int ResourcesVisited,
    int ResourcesSkipped, IReadOnlyList<string> Diagnostics,
    IReadOnlyList<LflLeftoverIndexEntry>? LeftoverIndexEntries = null)
{
    /// <summary>
    /// Indexed scripts whose 00.LFL offset points inside another resource.
    /// They are not text sources and are not counted as skipped ranges, but
    /// they stay listed so coverage claims can name them.
    /// </summary>
    public IReadOnlyList<LflLeftoverIndexEntry> LeftoverEntries => LeftoverIndexEntries ?? [];

    public string Coverage => CandidateBytes == 0 ? "No script bytes were visited." :
        $"{DecodedBytes:N0} instruction bytes decoded across {ResourcesVisited:N0} decode ranges; ranges can overlap when verbs share stored code.";
}

/// <summary>Finds literal text in decoded DOS LOOM v3 script instructions.</summary>
public sealed class LoomTextExtractor
{
    public const string ChoiceConversationKind = "Dialogue choices";
    private static readonly Regex SpeechCodePattern = new(@"\[text control 0A: [0-9A-F]{2} [0-9A-F]{2}\]", RegexOptions.Compiled);
    private readonly ScummRoomObjectDirectory _objects = new();
    public LoomTextExtraction Extract(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var dialect = ScummProfile.For(game).Dialect;
        var isV5 = game.Version == ScummVersion.V5;
        var scripts = new ScummScriptReader(dialect);
        var verbs = new ScummObjectVerbReader(dialect);
        var entries = new List<LoomTextEntry>();
        var decodedBytes = 0;
        var candidateBytes = 0;
        var unresolved = 0;
        var visited = 0;
        var skipped = 0;
        var diagnostics = new List<string>();
        var leftovers = new List<LflLeftoverIndexEntry>();
        var chains = new LflResourceChain();

        void Skip(string source, Exception ex)
        {
            skipped++;
            diagnostics.Add($"{source}: {ex.Message}");
        }

        void Add(ScummScriptDisassembly? script, int? roomId, int? objectId = null, int? verbId = null,
            string? sourceOverride = null, string? objectName = null,
            IReadOnlyDictionary<int, string>? roomObjectNames = null)
        {
            if (script is null) return;
            visited++;
            decodedBytes += script.DecodedBytes;
            candidateBytes += script.CodeLength;
            unresolved += script.Issues?.Count ?? (script.DecodeNote is null ? 0 : 1);
            var source = sourceOverride ?? script.Context ??
                $"Indexed script {script.ScriptId:00} / {script.ContainerFileNumber:00}.LFL";
            if (script.Issues is { Count: > 0 })
                diagnostics.AddRange(script.Issues.Select(issue => $"{source} +0x{issue.Offset:X4}: {issue.Reason}"));
            else if (script.DecodeNote is not null)
                diagnostics.Add($"{source} +0x{script.UndecodedOffset.GetValueOrDefault():X4}: {script.DecodeNote}");
            var cutsceneDepth = 0;
            var cutsceneNumber = 0;
            var conversationLine = 0;
            var scriptDialogueLine = 0;
            var scriptDialogueSegment = 0;
            var unmarkedLineIndexes = new Dictionary<int, List<int>>();
            string? conversationId = null;
            var scriptDialogueId = $"{roomId?.ToString("00", CultureInfo.InvariantCulture) ?? "script"}:" +
                $"{script.ContainerFileNumber:00}:{script.ResourceOffset:X4}:dialogue";
            var firstEntry = entries.Count;
            foreach (var instruction in script.Instructions)
            {
                if (instruction.Operation == "cutscene")
                {
                    if (cutsceneDepth++ == 0)
                    {
                        scriptDialogueSegment++;
                        scriptDialogueLine = 0;
                        cutsceneNumber++;
                        conversationLine = 0;
                        conversationId = $"{roomId?.ToString("00", CultureInfo.InvariantCulture) ?? "script"}:" +
                            $"{script.ContainerFileNumber:00}:{script.ResourceOffset:X4}:cutscene:{cutsceneNumber}";
                    }
                }
                else if (instruction.Operation == "endCutscene" && cutsceneDepth > 0 && --cutsceneDepth == 0)
                {
                    conversationId = null;
                    scriptDialogueSegment++;
                    scriptDialogueLine = 0;
                }
                if (!IsTextInstruction(instruction)) continue;
                var entryObjectId = objectId;
                var entryObjectName = objectName;
                if (instruction.Operation == "setObjectName")
                {
                    if (TryReadLiteralObjectId(instruction.Operands, out var targetId))
                    {
                        entryObjectId = targetId;
                        entryObjectName = roomObjectNames is not null && roomObjectNames.TryGetValue(targetId, out var targetName)
                            ? targetName : null;
                    }
                    else
                    {
                        entryObjectId = null;
                        entryObjectName = null;
                    }
                }
                foreach (var rawText in ScummTextDecoder.DecodeLiterals(instruction.Operands))
                {
                    // The talkie games (Monkey Island 2, Indiana Jones 4) put four "play this speech sample" codes (0xFF 0x0A and two bytes each) before a spoken line.
                    var text = isV5 ? SpeechCodePattern.Replace(rawText, "") : rawText;
                    if (!HasUsefulText(text)) continue;
                    var kind = instruction.Operation switch
                    {
                        "print" or "printEgo" => "Spoken line",
                        "setObjectName" => "Object name",
                        "stringOps" => "Stored line",
                        "verbOps" => "Verb label",
                        _ => "Object label"
                    };
                    var inMarkedCutscene = kind == "Spoken line" && cutsceneDepth > 0;
                    var line = kind != "Spoken line" ? (int?)null
                        : inMarkedCutscene ? ++conversationLine : ++scriptDialogueLine;
                    var groupId = kind != "Spoken line" ? null
                        : inMarkedCutscene ? conversationId : $"{scriptDialogueId}:{scriptDialogueSegment}";
                    var groupKind = kind != "Spoken line" ? null
                        : inMarkedCutscene ? "Cutscene" : "Script dialogue";
                    entries.Add(new LoomTextEntry(text, kind, source, roomId, entryObjectId,
                        verbId, roomId is null ? script.ScriptId : null, instruction.Offset,
                        entryObjectName, groupId, line, groupKind,
                        kind == "Spoken line" ? GetSpeaker(instruction) : null,
                        instruction.Operation == "setObjectName" ? objectId : null,
                        instruction.Operation == "setObjectName" ? objectName : null));
                    if (kind == "Spoken line" && !inMarkedCutscene)
                    {
                        if (!unmarkedLineIndexes.TryGetValue(scriptDialogueSegment, out var segmentLines))
                            unmarkedLineIndexes[scriptDialogueSegment] = segmentLines = [];
                        segmentLines.Add(entries.Count - 1);
                    }
                }
            }
            foreach (var segmentLines in unmarkedLineIndexes.Values)
                if (segmentLines.Select(index => (entries[index].CodeOffset, entries[index].Text, entries[index].Speaker)).Distinct().Count() < 2)
                    foreach (var index in segmentLines)
                        entries[index] = entries[index] with { ConversationId = null, ConversationLine = null, ConversationKind = null };
            ApplyChoiceSets(script, entries, firstEntry,
                $"{roomId?.ToString("00", CultureInfo.InvariantCulture) ?? "script"}:" +
                $"{script.ContainerFileNumber:00}:{script.ResourceOffset:X4}:choices");
        }

        // The CD game keeps its scripts in tagged blocks of DISK01.LEC, a browsed v4 game (Monkey Island) in the blocks of each of its disks: every block is one
        // script (conversation ids use the block's offset, and the disk number as the container).
        var archives = game.V4Archive is { } cd ? [cd] : game.V4Disks?.Disks.OfType<ScummV4Archive>().ToList() ?? [];
        for (var disk = 0; disk < archives.Count; disk++)
        {
            var vga = archives[disk];
            var container = disk + 1;
            var diskLabel = archives.Count > 1 ? $"Disk {container} / " : "";
            foreach (var room in vga.Rooms)
                foreach (var script in vga.ReadScripts(room.Number))
                {
                    var source = $"{diskLabel}Room {room.Number:00} / {script.Label}";
                    try { Add(scripts.DecodeCode(script.Code, script.Number, container, script.BlockOffset) with { Context = source }, room.Number, sourceOverride: source); }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source, ex); }
                }
            foreach (var room in vga.Rooms)
            {
                var objects = vga.ReadObjects(room.Number);
                var names = objects.Where(o => o.Name.Length > 0).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Name);
                foreach (var obj in objects)
                {
                    if (HasUsefulText(obj.Name))
                        entries.Add(new LoomTextEntry(obj.Name, "Object name", $"{diskLabel}Room {room.Number:00} / Object {obj.Id}", room.Number, obj.Id, null, null,
                            obj.BlockOffset, obj.Name));
                    foreach (var verb in obj.Verbs)
                    {
                        var label = verb.VerbId == 0xFF ? "Fallback verb" : $"Verb 0x{verb.VerbId:X2}";
                        var source = $"{diskLabel}Room {room.Number:00} / Object {obj.Id} / {label}";
                        try
                        {
                            Add(scripts.DecodeCode(obj.Block[verb.Offset..], obj.Id, container, obj.BlockOffset + verb.Offset) with { Context = source }, room.Number, obj.Id,
                                verb.VerbId, source, obj.Name.Length > 0 ? obj.Name : null, names);
                        }
                        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source, ex); }
                    }
                }
            }
        }
        // The v5 games (Monkey Island 1 CD, Monkey Island 2, Indiana Jones 4): the SCRP, ENCD, EXCD and LSCR blocks and the VERB tables of the objects.
        var v5Archives = game.V4Disks?.Disks.OfType<ScummV5Archive>().ToList() ?? [];
        foreach (var v5 in v5Archives)
        {
            foreach (var script in v5.ReadGlobalScripts())
            {
                var source = $"Script {script.Number:00}";
                try { Add(scripts.DecodeCode(script.Code, script.Number, 1, script.Position) with { Context = source }, null, sourceOverride: source); }
                catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source, ex); }
            }
            foreach (var room in v5.Rooms)
            {
                foreach (var script in v5.ReadRoomScripts(room.Number))
                {
                    var source = script.Kind switch { "ENCD" => $"Room {room.Number:00} / Entry script", "EXCD" => $"Room {room.Number:00} / Exit script", _ => $"Room {room.Number:00} / Local script {script.Number}" };
                    try { Add(scripts.DecodeCode(script.Code, script.Number, 1, script.Position) with { Context = source }, room.Number, sourceOverride: source); }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source, ex); }
                }
                var labels = v5.ReadObjectLabels(room.Number);
                var names = labels.GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Name);
                foreach (var label in labels)
                    if (HasUsefulText(label.Name))
                        entries.Add(new LoomTextEntry(label.Name, "Object name", $"Room {room.Number:00} / Object {label.Id}", room.Number, label.Id, null, null, 0, label.Name));
                foreach (var verb in v5.ReadObjectVerbs(room.Number))
                {
                    var verbLabel = verb.VerbId == 0xFF ? "Fallback verb" : $"Verb 0x{verb.VerbId:X2}";
                    var source = $"Room {room.Number:00} / Object {verb.ObjectId} / {verbLabel}";
                    try
                    {
                        Add(scripts.DecodeCode(verb.Block, verb.ObjectId, 1, verb.Position, verb.StartOffset) with { Context = source }, room.Number, verb.ObjectId, verb.VerbId, source,
                            verb.ObjectName is { Length: > 0 } ? verb.ObjectName : null, names);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source, ex); }
                }
            }
        }
        if (archives.Count > 0 || v5Archives.Count > 0) return new LoomTextExtraction(entries, decodedBytes, candidateBytes, unresolved, visited, skipped, diagnostics, leftovers);

        if (game.Version is ScummVersion.V1 or ScummVersion.V2)
        {
            // Maniac Mansion and Zak McKracken: the global scripts, then each room's entry and exit scripts and the verb scripts of its objects.
            foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue && item.Offset.HasValue))
            {
                try
                {
                    var code = ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath)), resource.Offset!.Value) ?? throw new InvalidDataException("size");
                    Add(scripts.DecodeCode(code, resource.ResourceId!.Value, resource.ContainerFileNumber ?? 0, resource.Offset.Value) with { Context = resource.Name }, null, sourceOverride: resource.Name);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip(resource.Name, ex); }
            }
            foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
            {
                var roomId = room.ResourceId!.Value;
                var plain = LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath));
                var roomObjects = ScummOldRoomObjects.Read(plain, null);
                var names = roomObjects.Where(o => o.Name.Length > 0).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Name);
                foreach (var o in roomObjects)
                    if (HasUsefulText(o.Name))
                        entries.Add(new LoomTextEntry(o.Name, "Object name", $"Room {roomId:00} / Object {o.Id}", roomId, o.Id, null, null, 0, o.Name));
                foreach (var source in ScummOldScripts.ReadRoomScripts(plain, roomId))
                {
                    try { Add(scripts.DecodeCode(source.Code, source.ObjectId ?? roomId, source.Container, source.Offset, source.StartOffset) with { Context = source.Label }, roomId, source.ObjectId, source.VerbId, source.Label, source.ObjectName, names); }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { Skip(source.Label, ex); }
                }
            }
            return new LoomTextExtraction(entries, decodedBytes, candidateBytes, unresolved, visited, skipped, diagnostics, leftovers);
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            if (chains.FindLeftover(game, resource) is { } leftover)
            {
                leftovers.Add(leftover);
                continue;
            }
            try { Add(scripts.Decode(resource), null); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip(resource.Name, ex); }
        }

        var indexedScriptCount = game.Resources.Count(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue);
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            var roomId = room.ResourceId!.Value;
            try { Add(scripts.DecodeRoomEntry(room), roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} entry", ex); }
            try { Add(scripts.DecodeRoomExit(room), roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} exit", ex); }
            try
            {
                var encoded = File.ReadAllBytes(room.FilePath);
                var plain = LflEncoding.DecodeXorFF(encoded);
                var records = _objects.Decode(encoded);
                var tables = verbs.ReadAll(encoded, records);
                var roomObjectNames = ReadObjectNames(plain, records);
                foreach (var record in records)
                {
                    if (TryReadObjectName(plain, record.CodeOffset, out var objectName, out var nameOffset))
                    {
                        if (HasUsefulText(objectName))
                            entries.Add(new LoomTextEntry(objectName, "Object name",
                                $"Room {roomId:00} / Object {record.Id}", roomId, record.Id, null, null, nameOffset,
                                objectName));
                    }
                    if (!tables.TryGetValue(record.Id, out var table)) continue;
                    foreach (var verb in table)
                    {
                        try { Add(verbs.Decode(room, record, verb.VerbId), roomId, record.Id, verb.VerbId,
                            $"Room {roomId:00} / Object {record.Id} / {verb.Label}",
                            roomObjectNames.TryGetValue(record.Id, out var ownerName) ? ownerName : null, roomObjectNames); }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} object {record.Id} {verb.Label}", ex); }
                    }
                }

                if (!TryReadLocalScriptIds(encoded, indexedScriptCount, out var localIds))
                    diagnostics.Add($"Room {roomId:00}: local-script directory was malformed or had no terminator.");
                foreach (var localId in localIds)
                {
                    try { Add(scripts.DecodeRoomLocalScript(room, localId, indexedScriptCount), roomId,
                        sourceOverride: $"Room {roomId:00} / Local script {localId:00}"); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} local script {localId:00}", ex); }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} objects and local scripts", ex); }
        }

        return new LoomTextExtraction(entries, decodedBytes, candidateBytes, unresolved, visited, skipped, diagnostics,
            leftovers);
    }

    /// <summary>
    /// Groups player dialogue choices: a run of consecutive <c>verbOps</c>
    /// instructions that give at least two verbs literal labels. When a later
    /// <c>isEqual</c> compares one variable with those verb IDs, spoken lines
    /// in each equality branch are attached to that choice as replies. This is
    /// static structure; conditions can hide options or change replies.
    /// </summary>
    private static void ApplyChoiceSets(ScummScriptDisassembly script, List<LoomTextEntry> entries,
        int firstEntry, string groupPrefix)
    {
        var instructions = script.Instructions;
        var runs = new List<(int Start, int End, List<(int Verb, int Offset)> Labels, HashSet<int> Verbs)>();
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Operation != "verbOps") continue;
            var start = i;
            var labels = new List<(int Verb, int Offset)>();
            var verbs = new HashSet<int>();
            while (i < instructions.Count && instructions[i].Operation == "verbOps")
            {
                if (TryReadLiteralObjectId(instructions[i].Operands, out var verb))
                {
                    verbs.Add(verb);
                    if (instructions[i].Operands.Contains("name, \"", StringComparison.Ordinal))
                        labels.Add((verb, instructions[i].Offset));
                }
                i++;
            }
            i--;
            if (labels.Select(label => label.Verb).Distinct().Count() >= 2)
                runs.Add((start, i, labels, verbs));
        }
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            var groupId = $"{groupPrefix}:{runIndex + 1}";
            var labelOrder = run.Labels.Select((label, order) => (label.Verb, label.Offset, order))
                .ToDictionary(label => label.Offset, label => label);
            var verbOrder = new Dictionary<int, int>();
            foreach (var label in run.Labels) verbOrder.TryAdd(label.Verb, verbOrder.Count);
            for (var index = firstEntry; index < entries.Count; index++)
            {
                if (entries[index].Kind != "Verb label" ||
                    !labelOrder.TryGetValue(entries[index].CodeOffset, out var label)) continue;
                entries[index] = entries[index] with
                {
                    ConversationId = groupId, ConversationKind = ChoiceConversationKind,
                    ConversationLine = verbOrder[label.Verb] * 1000, ChoiceVerbId = label.Verb,
                    Speaker = "Player choice"
                };
            }

            // Find the selection variable: the first equality test after the
            // run against one of its verbs, before the next choice set.
            var limit = runIndex + 1 < runs.Count ? instructions[runs[runIndex + 1].Start].Offset : int.MaxValue;
            string? selection = null;
            var branches = new List<(int Verb, int From, int To)>();
            for (var i = run.End + 1; i < instructions.Count && instructions[i].Offset < limit; i++)
            {
                var instruction = instructions[i];
                if (instruction.Operation != "isEqual") continue;
                var match = Regex.Match(instruction.Operands,
                    @"^(?<var>[a-z]+\[[^\]]+\]),\s*0x(?<value>[0-9A-Fa-f]+),.*->\s*0x(?<target>[0-9A-Fa-f]+)");
                if (!match.Success) continue;
                var value = int.Parse(match.Groups["value"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!run.Verbs.Contains(value)) continue;
                selection ??= match.Groups["var"].Value;
                if (selection != match.Groups["var"].Value) continue;
                var from = i + 1 < instructions.Count ? instructions[i + 1].Offset : instruction.Offset;
                var to = int.Parse(match.Groups["target"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (to > from) branches.Add((value, from, Math.Min(to, limit)));
            }
            var replyCounts = new Dictionary<int, int>();
            for (var index = firstEntry; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.Kind != "Spoken line") continue;
                var branch = branches.FirstOrDefault(item => entry.CodeOffset >= item.From && entry.CodeOffset < item.To);
                if (branch.To == 0) continue;
                var order = verbOrder.TryGetValue(branch.Verb, out var known) ? known : verbOrder.Count + branch.Verb;
                replyCounts[branch.Verb] = replyCounts.GetValueOrDefault(branch.Verb) + 1;
                entries[index] = entry with
                {
                    ConversationId = groupId, ConversationKind = ChoiceConversationKind,
                    ConversationLine = order * 1000 + replyCounts[branch.Verb], ChoiceVerbId = branch.Verb
                };
            }
        }
    }

    private static bool IsTextInstruction(ScummScriptInstruction instruction) => instruction.Operation is
        "print" or "printEgo" or "setObjectName" ||
        instruction.Operation == "stringOps" && instruction.Operands.Contains("loadString", StringComparison.Ordinal) ||
        instruction.Operation == "verbOps" && instruction.Operands.Contains("name, \"", StringComparison.Ordinal);

    private static bool TryReadLiteralObjectId(string operands, out int objectId)
    {
        var match = Regex.Match(operands, @"^\s*(?:0x(?<hex>[0-9A-Fa-f]+)|(?<decimal>[0-9]+))\b");
        if (match.Groups["hex"].Success)
            return int.TryParse(match.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out objectId);
        return int.TryParse(match.Groups["decimal"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out objectId);
    }

    private static string? GetSpeaker(ScummScriptInstruction instruction)
    {
        if (instruction.Operation == "printEgo") return "Ego";
        if (instruction.Operation != "print") return null;
        var match = Regex.Match(instruction.Operands, @"^\s*(?:0x(?<hex>[0-9A-Fa-f]+)|(?<decimal>[0-9]+))\s*,");
        if (!match.Success) return null;
        var value = match.Groups["hex"].Success
            ? int.Parse(match.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(match.Groups["decimal"].Value, CultureInfo.InvariantCulture);
        return $"Actor {value}";
    }

    private static bool HasUsefulText(string text)
    {
        var withoutControls = Regex.Replace(text, @"\[(?:pause|continue|text control [^\]]*)\]", "", RegexOptions.IgnoreCase);
        var visible = withoutControls.EnumerateRunes().Where(Rune.IsLetterOrDigit).ToArray();
        return visible.Length > 1;
    }

    private static bool TryReadLocalScriptIds(byte[] encoded, int globalScriptCount, out int[] ids)
    {
        ids = [];
        var data = LflEncoding.DecodeXorFF(encoded);
        if (data.Length < 29) return false;
        var roomEnd = data[0] | data[1] << 8;
        var objectCount = data[20];
        var soundCount = data[23];
        var localCount = data[24];
        var cursor = 29 + objectCount * 4 + soundCount + localCount;
        if (roomEnd > data.Length || cursor >= roomEnd) return false;
        var found = new List<int>();
        while (cursor < roomEnd && data[cursor] != 0)
        {
            if (cursor > roomEnd - 3) return false;
            var id = data[cursor];
            var offset = data[cursor + 1] | data[cursor + 2] << 8;
            if (id >= globalScriptCount)
            {
                if (offset <= cursor || offset >= roomEnd) return false;
                found.Add(id);
            }
            cursor += 3;
        }
        if (cursor >= roomEnd || found.Distinct().Count() != found.Count) return false;
        ids = found.ToArray();
        return true;
    }

    /// <summary>
    /// Stored object names of one room (the name in each object's code header),
    /// keyed by object id. Names that are blank or only symbols are left out.
    /// Scripts can rename objects at run time (<c>setObjectName</c>); these are
    /// the names as stored.
    /// </summary>
    public static IReadOnlyDictionary<int, string> ReadObjectNames(byte[] plainRoom,
        IEnumerable<ScummRoomObjectRecord> records)
    {
        var names = new Dictionary<int, string>();
        foreach (var record in records)
            if (TryReadObjectName(plainRoom, record.CodeOffset, out var name, out _) && HasUsefulText(name))
                names[record.Id] = name;
        return names;
    }

    private static bool TryReadObjectName(byte[] room, int codeOffset, out string name, out int nameOffset)
    {
        name = "";
        nameOffset = 0;
        if (!ScummRoomObjectDirectory.TryReadObjectNameBytes(room, codeOffset, out var start, out var length)) return false;
        nameOffset = start;
        name = ScummTextDecoder.DecodeRawString(room.AsSpan(start, length));
        return !string.IsNullOrWhiteSpace(name);
    }
}

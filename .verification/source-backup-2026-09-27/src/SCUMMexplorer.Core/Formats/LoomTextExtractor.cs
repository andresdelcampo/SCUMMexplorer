using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomTextEntry(string Text, string Kind, string Source, int? RoomId,
    int? ObjectId, int? VerbId, int? ScriptId, int CodeOffset,
    string? ObjectName = null, string? ConversationId = null, int? ConversationLine = null, string? ConversationKind = null, string? Speaker = null,
    int? SourceObjectId = null, string? SourceObjectName = null);

public sealed record LoomTextExtraction(IReadOnlyList<LoomTextEntry> Entries,
    int DecodedBytes, int CandidateBytes, int UnresolvedPaths, int ResourcesVisited,
    int ResourcesSkipped, IReadOnlyList<string> Diagnostics)
{
    public string Coverage => CandidateBytes == 0 ? "No script bytes were visited." :
        $"{DecodedBytes:N0} instruction bytes decoded across {ResourcesVisited:N0} decode ranges; ranges can overlap when verbs share stored code.";
}

/// <summary>Finds literal text in decoded DOS LOOM v3 script instructions.</summary>
public sealed class LoomTextExtractor
{
    private readonly LoomScriptReader _scripts = new();
    private readonly LoomObjectVerbReader _verbs = new();
    private readonly LoomRoomObjectDirectory _objects = new();
    public LoomTextExtraction Extract(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var entries = new List<LoomTextEntry>();
        var decodedBytes = 0;
        var candidateBytes = 0;
        var unresolved = 0;
        var visited = 0;
        var skipped = 0;
        var diagnostics = new List<string>();

        void Skip(string source, Exception ex)
        {
            skipped++;
            diagnostics.Add($"{source}: {ex.Message}");
        }

        void Add(LoomScriptDisassembly? script, int? roomId, int? objectId = null, int? verbId = null,
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
                foreach (var text in LoomScummTextDecoder.DecodeLiterals(instruction.Operands))
                {
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
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            try { Add(_scripts.Decode(resource), null); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip(resource.Name, ex); }
        }

        var indexedScriptCount = game.Resources.Count(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue);
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            var roomId = room.ResourceId!.Value;
            try { Add(_scripts.DecodeRoomEntry(room), roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} entry", ex); }
            try { Add(_scripts.DecodeRoomExit(room), roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} exit", ex); }
            try
            {
                var encoded = File.ReadAllBytes(room.FilePath);
                var plain = LflEncoding.DecodeXorFF(encoded);
                var records = _objects.Decode(encoded);
                var tables = _verbs.ReadAll(encoded, records);
                var roomObjectNames = new Dictionary<int, string>();
                foreach (var record in records)
                    if (TryReadObjectName(plain, record.CodeOffset, out var name, out _) && HasUsefulText(name))
                        roomObjectNames[record.Id] = name;
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
                        try { Add(_verbs.Decode(room, record, verb.VerbId), roomId, record.Id, verb.VerbId,
                            $"Room {roomId:00} / Object {record.Id} / {verb.Label}",
                            roomObjectNames.TryGetValue(record.Id, out var ownerName) ? ownerName : null, roomObjectNames); }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} object {record.Id} {verb.Label}", ex); }
                    }
                }

                if (!TryReadLocalScriptIds(encoded, indexedScriptCount, out var localIds))
                    diagnostics.Add($"Room {roomId:00}: local-script directory was malformed or had no terminator.");
                foreach (var localId in localIds)
                {
                    try { Add(_scripts.DecodeRoomLocalScript(room, localId, indexedScriptCount), roomId,
                        sourceOverride: $"Room {roomId:00} / Local script {localId:00}"); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} local script {localId:00}", ex); }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { Skip($"Room {roomId:00} objects and local scripts", ex); }
        }

        return new LoomTextExtraction(entries, decodedBytes, candidateBytes, unresolved, visited, skipped, diagnostics);
    }

    private static bool IsTextInstruction(LoomScriptInstruction instruction) => instruction.Operation is
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

    private static string? GetSpeaker(LoomScriptInstruction instruction)
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

    private static bool TryReadObjectName(byte[] room, int codeOffset, out string name, out int nameOffset)
    {
        name = "";
        nameOffset = 0;
        if (codeOffset < 0 || codeOffset > room.Length - 17) return false;
        var length = room[codeOffset] | room[codeOffset + 1] << 8;
        var end = codeOffset + length;
        var relative = room[codeOffset + 16];
        var start = codeOffset + relative;
        if (length < 18 || end > room.Length || relative < 17 || start >= end) return false;
        var bytes = room.AsSpan(start, end - start);
        var terminator = FindStringTerminator(bytes);
        if (terminator <= 0) return false;
        nameOffset = start;
        name = LoomScummTextDecoder.DecodeRawString(bytes[..terminator]);
        return !string.IsNullOrWhiteSpace(name);
    }

    private static int FindStringTerminator(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0) return i;
            if (bytes[i] != 0xFF || i + 1 >= bytes.Length) continue;
            var control = bytes[++i];
            var parameterCount = control is 1 or 2 or 3 or 8 ? 0 : 2;
            i = Math.Min(bytes.Length - 1, i + parameterCount);
        }
        return -1;
    }
}

/// <summary>Turns the disassembler's escaped SCUMM byte strings into DOS CP437 text.</summary>
public static class LoomScummTextDecoder
{
    private static readonly Encoding Cp437 = CreateCp437();
    private static readonly Regex LiteralPattern = new("\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.Compiled);

    public static IReadOnlyList<string> DecodeLiterals(string operands)
    {
        var results = new List<string>();
        foreach (Match match in LiteralPattern.Matches(operands))
        {
            var bytes = ParseBytes(match.Groups[1].Value);
            results.Add(DecodeBytes(bytes));
        }
        return results;
    }

    public static string DecodeRawString(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder();
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            if (value == 0xFF && i + 1 < bytes.Length)
            {
                var control = bytes[++i];
                var rawBytes = new byte[bytes.Length];
                bytes.CopyTo(rawBytes);
                AppendControl(output, control, rawBytes, ref i);
            }
            else output.Append(DecodeByte(value));
        }
        return output.ToString();
    }

    private static List<byte> ParseBytes(string value)
    {
        var result = new List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                if (value[i + 1] == 'x' && i + 3 < value.Length &&
                    byte.TryParse(value.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                {
                    result.Add(hex);
                    i += 3;
                    continue;
                }
                result.Add((byte)value[++i]);
                continue;
            }
            result.Add((byte)value[i]);
        }
        return result;
    }

    private static string DecodeBytes(IReadOnlyList<byte> bytes)
    {
        var output = new StringBuilder();
        for (var i = 0; i < bytes.Count; i++)
        {
            var value = bytes[i];
            if (value == 0xFF && i + 1 < bytes.Count)
            {
                var control = bytes[++i];
                AppendControl(output, control, bytes, ref i);
            }
            else output.Append(DecodeByte(value));
        }
        return output.ToString();
    }

    private static string DecodeByte(byte value) => value < 0x80 ? ((char)value).ToString() : Cp437.GetString([value]);

    private static void AppendControl(StringBuilder output, byte control, IReadOnlyList<byte> bytes, ref int index)
    {
        if (control == 1) { output.AppendLine(); return; }
        if (control == 2) { output.Append("[continue]"); return; }
        if (control == 3) { output.Append("[pause]"); return; }
        output.Append($"[text control {control:X2}");
        if (control is not (1 or 2 or 3 or 8))
        {
            for (var parameter = 0; parameter < 2 && index + 1 < bytes.Count; parameter++)
                output.Append(parameter == 0 ? ": " : " ").Append(bytes[++index].ToString("X2", CultureInfo.InvariantCulture));
        }
        output.Append(']');
    }

    private static Encoding CreateCp437()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(437);
    }
}

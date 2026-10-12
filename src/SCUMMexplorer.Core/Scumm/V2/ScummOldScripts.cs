using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One script of a v1 or v2 game, cut out of its room file: where it came from and its bytecode.</summary>
public sealed record OldScriptSource(string Label, int? Room, int? ObjectId, int? VerbId, string? ObjectName, int Container, int Offset, byte[] Code, int StartOffset = 0);

/// <summary>
/// The scripts of Maniac Mansion and Zak McKracken (classic v1 and enhanced v2; ScummVM <c>setupRoomSubBlocks</c> for version 2 and below, 12 October 2026).
/// A global script is a size word, two header bytes and its code; a room's exit script starts at the word at byte 0x18 and its entry script at the word at 0x1A (it runs to the
/// room's end); an object's code starts with a 15-byte header (the name offset is byte 14) followed by (verb, offset) byte pairs ending in 0, offsets counted from
/// the start of the code.
/// </summary>
public static class ScummOldScripts
{
    /// <summary>A global script resource is a size word and two more header bytes (as in old-v3); the code follows. A shorter header decodes only the first byte.</summary>
    public const int ResourceHeaderLength = 4;

    /// <summary>The bytecode of a global script at this offset of its (XOR-decoded) room file, or null when the size is not plausible.</summary>
    public static byte[]? ReadGlobalScript(byte[] decoded, int offset)
    {
        if (offset < 0 || offset + ResourceHeaderLength > decoded.Length) return null;
        var size = decoded[offset] | decoded[offset + 1] << 8;
        if (size <= ResourceHeaderLength || offset + size > decoded.Length) return null;
        return decoded.AsSpan(offset + ResourceHeaderLength, size - ResourceHeaderLength).ToArray();
    }

    /// <summary>The room's entry and exit scripts and the verb scripts of its objects (<paramref name="decoded"/> is the room file after XOR 0xFF).</summary>
    public static IEnumerable<OldScriptSource> ReadRoomScripts(byte[] decoded, int room)
    {
        if (decoded.Length < 30) yield break;
        var roomEnd = Math.Min(Word(decoded, 0), decoded.Length);
        var exit = Word(decoded, 0x18);
        var entry = Word(decoded, 0x1A);
        if (entry > 0 && entry < roomEnd)
            yield return new OldScriptSource($"Room {room:00} / Entry script", room, null, null, null, room, entry, decoded[entry..roomEnd]);
        if (exit > 0 && exit < roomEnd)
        {
            var exitEnd = entry > exit && entry < roomEnd ? entry : roomEnd;
            yield return new OldScriptSource($"Room {room:00} / Exit script", room, null, null, null, room, exit, decoded[exit..exitEnd]);
        }
        var count = decoded[20];
        if (count < 2 || 28 + 4 * count > decoded.Length) yield break;
        var codes = Enumerable.Range(0, count).Select(i => Word(decoded, 28 + 2 * count + 2 * i)).Where(c => c > 0 && c + 15 <= decoded.Length).OrderBy(c => c).ToArray();
        foreach (var code in codes)
        {
            var next = codes.Where(c => c > code).DefaultIfEmpty(roomEnd).Min();
            if (next > decoded.Length) next = decoded.Length;
            var id = Word(decoded, code + 4);
            var nameStart = code + decoded[code + 14];
            string? name = null;
            if (nameStart < decoded.Length)
            {
                var end = Array.IndexOf(decoded, (byte)0, nameStart);
                if (end > nameStart) name = System.Text.Encoding.Latin1.GetString(decoded, nameStart, Math.Min(end - nameStart, 40));
            }
            for (var cursor = code + 15; cursor + 1 < next && decoded[cursor] != 0; cursor += 2)
            {
                var verb = decoded[cursor];
                var start = code + decoded[cursor + 1];
                if (start <= code || start >= next) continue;
                yield return new OldScriptSource($"Room {room:00} / Object {id} / {(verb == 0xFF ? "Fallback verb" : $"Verb 0x{verb:X2}")}", room, id, verb, name,
                    room, code, decoded[code..next], start - code);
            }
        }
    }

    private static int Word(byte[] data, int offset) => data[offset] | data[offset + 1] << 8;
}

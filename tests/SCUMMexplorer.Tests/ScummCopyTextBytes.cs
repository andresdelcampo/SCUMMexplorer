using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S9 accents (18 October 2026), shared by the Indy4 and MonkeyCd edition tests: every byte from 0x80 up that a copy's texts hold (string literals of every
/// global, entry, exit, local and object-verb script, and the object names; text controls led by 0xFF or 0xFE and their operands skipped), with its count.
/// Copied from <c>ScummMonkeyFloppyPlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets</c> (17 October 2026), which keeps its own copy.
/// </summary>
internal static class ScummCopyTextBytes
{
    public static Dictionary<byte, int> Collect(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        var found = new Dictionary<byte, int>();
        void Text(IReadOnlyList<byte> bytes)
        {
            for (var i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] == 0) break;
                if (bytes[i] is 0xFF or 0xFE) { i += i + 1 < bytes.Count && bytes[i + 1] is 1 or 2 or 3 or 8 ? 1 : 3; continue; }
                if (bytes[i] >= 0x80) found[bytes[i]] = found.GetValueOrDefault(bytes[i]) + 1;
            }
        }
        var literal = new System.Text.RegularExpressions.Regex("\"((?:\\\\.|[^\"\\\\])*)\"");
        void Script(ScummScriptDisassembly? script)
        {
            if (script is null) return;
            foreach (var instruction in script.Instructions)
                foreach (System.Text.RegularExpressions.Match match in literal.Matches(instruction.Operands))
                    Text(ScummTextDecoder.ParseEscaped(match.Groups[1].Value));
        }
        foreach (var id in data.GlobalScriptIds()) Script(data.GlobalScript(id));
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            ScummRoomData roomData;
            try { roomData = data.ReadRoom(room); } catch (Exception ex) when (ex is IOException or InvalidDataException) { continue; }
            Script(data.RoomEntryScript(room));
            Script(data.RoomExitScript(room));
            foreach (var id in data.RoomLocalScriptIds(room)) Script(data.RoomLocalScript(room, id));
            foreach (var name in roomData.ObjectNames.Values) Text(name);
            foreach (var (_, entries) in data.ReadObjectVerbs(room, roomData.Objects))
                foreach (var entry in entries.Where(e => !e.IsFallback))
                    Script(data.ReadObjectVerb(room, roomData.Objects.First(o => o.Id == entry.ObjectId), entry.VerbId, out _));
        }
        return found;
    }

    public static string Describe(Dictionary<byte, int> found) =>
        string.Join(" ", found.OrderBy(p => p.Key).Select(p => $"{p.Key:X2}x{p.Value} '{ScummTextDecoder.DecodeGlyphs([p.Key])}'"));
}

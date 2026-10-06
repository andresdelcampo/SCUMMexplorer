using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Formats;

/// <summary>One stretch of the disc's audio track that a script asks for with <c>print ... sayVoice</c>: the cue, the line's text if it has one, and the first script that asks for it.</summary>
public sealed record LoomCdCue(LoomVoiceCue Cue, string Text, string Source)
{
    /// <summary>A cue without a spoken line, or one that runs 20 seconds or more, is music or narration rather than a spoken line.</summary>
    public bool IsMusicOrNarration => string.IsNullOrWhiteSpace(Text) || Cue.Length >= 200;
}

/// <summary>Finds every distinct voice cue in the CD game's scripts (global, entry, exit, local, and object verb code), in disc order.</summary>
public static partial class LoomCdCueIndex
{
    [GeneratedRegex(@"sayVoice, 0x([0-9A-F]+), 0x([0-9A-F]+)(?:; text, ""(.*?)""\])?")]
    private static partial Regex CuePattern();

    /// <summary>The cues the scripts of one room ask for (its entry, exit, local scripts and object verbs; not the global scripts they start), in disc order.</summary>
    public static IReadOnlyList<LoomCdCue> ScanRoom(IScummDataSource data, int room) => Scan(data, [room], includeGlobal: false);

    public static IReadOnlyList<LoomCdCue> Scan(IScummDataSource data, IEnumerable<int> rooms, bool includeGlobal = true)
    {
        var found = new Dictionary<LoomVoiceCue, LoomCdCue>();
        void Look(string source, LoomScriptDisassembly? script)
        {
            if (script is null) return;
            foreach (var ins in script.Instructions)
            {
                if (ins.Operation != "print") continue;
                var m = CuePattern().Match(ins.Operands);
                if (!m.Success) continue;
                var cue = new LoomVoiceCue(int.Parse(m.Groups[1].Value, NumberStyles.HexNumber), int.Parse(m.Groups[2].Value, NumberStyles.HexNumber));
                if (cue.IsStop || found.ContainsKey(cue)) continue;
                var text = m.Groups[3].Success ? Regex.Replace(m.Groups[3].Value, @"\\x[0-9A-F]{2}", " ").Trim() : "";
                found[cue] = new LoomCdCue(cue, text, source);
            }
        }
        if (includeGlobal) foreach (var id in data.GlobalScriptIds()) Look($"global script {id}", Try(() => data.GlobalScript(id)));
        foreach (var room in rooms)
        {
            Look($"room {room} entry", Try(() => data.RoomEntryScript(room)));
            Look($"room {room} exit", Try(() => data.RoomExitScript(room)));
            foreach (var id in data.RoomLocalScriptIds(room)) Look($"room {room} local script {id}", Try(() => data.RoomLocalScript(room, id)));
            ScummRoomData? roomData = Try(() => data.ReadRoom(room));
            if (roomData is null) continue;
            foreach (var (objectId, verbs) in Try(() => data.ReadObjectVerbs(room, roomData.Objects)) ?? new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>())
            {
                var record = roomData.Objects.First(o => o.Id == objectId);
                foreach (var verb in verbs)
                    Look($"room {room} object {objectId} verb 0x{verb.VerbId:X2}", Try(() => data.ReadObjectVerb(room, record, verb.VerbId, out _)));
            }
        }
        return found.Values.OrderBy(c => c.Cue.Offset).ThenBy(c => c.Cue.Length).ToArray();
    }

    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return null; }
    }
}

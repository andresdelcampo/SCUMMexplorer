using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public enum LoomUsageTarget { Script, Object, Room, Sound }

/// <summary>One place in the game's code that names a script, object, room or sound with a literal number.</summary>
public sealed record LoomUsage(string Where, int Room, int? ObjectId, int Offset, string Operation, LoomUsageTarget Target, int TargetId, int? TargetVerb)
{
    public int? ScriptRoom { get; init; }
}

/// <summary>
/// "Used by": which code starts a script, runs an object's verb, loads a room or plays a sound with a literal number (the
/// start of script 91 from Room 43 object 643's verb 0x3A, for example). Static references from the decoded code, not proof of
/// execution; calls through a variable are not listed, and neither are the player's clicks (script 006 starts verbs by variable).
/// </summary>
public sealed class LoomUsageIndex
{
    private readonly Dictionary<(LoomUsageTarget, int, int?), List<LoomUsage>> _uses;

    private LoomUsageIndex(Dictionary<(LoomUsageTarget, int, int?), List<LoomUsage>> uses) => _uses = uses;

    public static LoomUsageIndex Empty { get; } = new([]);

    public static LoomUsageIndex Build(GameLibrary game)
    {
        var uses = new Dictionary<(LoomUsageTarget, int, int?), List<LoomUsage>>();
        var data = ScummDataSources.For(game);
        void Visit(ScummScriptDisassembly? script, int room, int? objectId, int? _, string label)
        {
            if (script is null) return;
            foreach (var ins in script.Instructions)
            {
                var args = Split(ins.Operands);
                switch (ins.Operation)
                {
                    case "startScript" or "chainScript" when Literal(args, 0) is int id:
                        // A literal local ID belongs to the caller's room. A global with no known room cannot
                        // establish which local 200 it starts, so it must not appear as a use of every room's 200.
                        if (id < data.GlobalScriptCount || room > 0)
                            Add(uses, new(label, room, objectId, ins.Offset, ins.Operation, LoomUsageTarget.Script, id, null)
                                { ScriptRoom = id >= data.GlobalScriptCount ? room : null });
                        break;
                    case "startObject" when Literal(args, 0) is int obj:
                        Add(uses, new(label, room, objectId, ins.Offset, ins.Operation, LoomUsageTarget.Object, obj, Literal(args, 1)));
                        break;
                    case "loadRoom" when Literal(args, 0) is int target:
                        Add(uses, new(label, room, objectId, ins.Offset, ins.Operation, LoomUsageTarget.Room, target, null));
                        break;
                    case "loadRoomWithEgo" when Literal(args, 1) is int target:
                        Add(uses, new(label, room, objectId, ins.Offset, ins.Operation, LoomUsageTarget.Room, target, null));
                        break;
                    case "startSound" or "startMusic" when Literal(args, 0) is int sound:
                        Add(uses, new(label, room, objectId, ins.Offset, ins.Operation, LoomUsageTarget.Sound, sound, null));
                        break;
                }
            }
        }
        new LoomSceneStateScanner(game).VisitCodeUnits(Visit);
        foreach (var resource in game.Rooms.Where(r => r.ResourceId.HasValue))
        {
            int room = resource.ResourceId!.Value;
            Visit(data.RoomExitScript(room), room, null, null, $"Room {room:00} exit");
        }
        return new LoomUsageIndex(uses);
    }

    public IReadOnlyList<LoomUsage> UsedBy(LoomUsageTarget target, int id, int? scriptRoom = null) =>
        _uses.TryGetValue((target, id, target == LoomUsageTarget.Script ? scriptRoom : null), out var list) ? list : [];

    /// <summary>One line per use, in plain language, with names from the glossary.</summary>
    public static string Describe(LoomUsage use, LoomGlossary glossary, Func<int, string?>? verbLabel = null)
    {
        var where = use.Where;
        if (use.ObjectId is int objectId && glossary.Object(0, objectId) is { } named)
            where = where.Replace($"object {objectId}", $"object {objectId} “{named.Name}”", StringComparison.Ordinal);
        else if (where.StartsWith("Script ", StringComparison.Ordinal) && int.TryParse(where.AsSpan(7, 3), out var scriptId) && glossary.Script(scriptId) is { } script)
            where += $" · {script.Name}";
        var verb = use.Operation switch
        {
            "startScript" => "starts it",
            "chainScript" => "continues in it",
            "startObject" => use.TargetVerb is int v ? $"runs its {(verbLabel?.Invoke(v) is { } label ? $"\"{label}\" verb" : $"verb 0x{v:X2}")}" : "runs one of its verbs",
            "startSound" or "startMusic" => "plays it",
            _ => "goes there"
        };
        return $"{where} +0x{use.Offset:X4} {verb}";
    }

    private static void Add(Dictionary<(LoomUsageTarget, int, int?), List<LoomUsage>> uses, LoomUsage use)
    {
        var key = (use.Target, use.TargetId, use.ScriptRoom);
        if (!uses.TryGetValue(key, out var list)) uses[key] = list = [];
        list.Add(use);
    }

    private static List<string> Split(string operands)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < operands.Length; i++)
        {
            if (operands[i] is '[' or '(') depth++;
            else if (operands[i] is ']' or ')') depth--;
            else if (operands[i] == ',' && depth == 0)
            {
                parts.Add(operands[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(operands[start..].Trim());
        return parts;
    }

    private static int? Literal(List<string> args, int index) =>
        index < args.Count && args[index].StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(args[index].AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
}

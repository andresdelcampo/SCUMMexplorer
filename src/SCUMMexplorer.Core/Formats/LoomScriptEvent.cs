using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public enum LoomScriptEventKind
{
    ScriptStarted,
    ScriptChained,
    ScriptStopped,
    ObjectScriptStarted,
    MusicStarted,
    MusicStopped,
    SoundStarted,
    SoundStopped,
    CutsceneStarted,
    CutsceneEnded,
    ScriptsFrozen,
    Dialogue,
    RoomLoaded,
    ObjectStateChanged,
    CharacterAction
}

public enum LoomScriptEventCertainty
{
    Reached,
    Conditional,
    StaticPotential
}

/// <summary>A source-linked event read from LOOM bytecode, with execution certainty kept explicit.</summary>
public sealed record LoomScriptEvent(
    int SourceScriptId,
    string SourceLabel,
    int Offset,
    LoomScriptEventKind Kind,
    string Operation,
    string Operands,
    ResourceKind? TargetKind,
    int? TargetId,
    LoomScriptEventCertainty Certainty,
    bool FreezeResistant = false,
    IReadOnlyList<int?>? ResolvedArguments = null,
    bool Recursive = false,
    IReadOnlyList<int>? EstimatedArgumentIndexes = null)
{
    /// <summary>The executor visited this instruction on its chosen route. Certainty still records estimates;
    /// false also covers commands collected from branches that were not executed.</summary>
    public bool Executed { get; init; }

    public string Format()
    {
        var certainty = Certainty switch
        {
            LoomScriptEventCertainty.Conditional => "Possible",
            LoomScriptEventCertainty.StaticPotential => "Potential",
            _ => ""
        };
        var title = Kind switch
        {
            LoomScriptEventKind.ScriptStarted => "starts script",
            LoomScriptEventKind.ScriptChained => "chains to script",
            LoomScriptEventKind.ScriptStopped => "stops script",
            LoomScriptEventKind.ObjectScriptStarted => "starts object script",
            LoomScriptEventKind.MusicStarted => "starts music",
            LoomScriptEventKind.MusicStopped => "stops music",
            LoomScriptEventKind.SoundStarted => "starts sound",
            LoomScriptEventKind.SoundStopped => "stops sound",
            LoomScriptEventKind.CutsceneStarted => "begins cutscene",
            LoomScriptEventKind.CutsceneEnded => "ends cutscene",
            LoomScriptEventKind.ScriptsFrozen => Operands == "0x00" ? "unfreezes scripts" : "freezes scripts",
            LoomScriptEventKind.Dialogue => "dialogue",
            LoomScriptEventKind.RoomLoaded => "loads room",
            LoomScriptEventKind.ObjectStateChanged => "changes object state",
            LoomScriptEventKind.CharacterAction => "character action",
            _ => Operation
        };
        var target = TargetId is int id
            ? $" {TargetKind switch { ResourceKind.Sound => "Sound", ResourceKind.Script => "Script", ResourceKind.Room => "Room", ResourceKind.Object => "Object", _ => "resource" }} {id:00}"
            : string.IsNullOrWhiteSpace(Operands) ? "" : $" ({Operands})";
        var prefix = certainty.Length == 0 ? "" : certainty + ": ";
        return $"{prefix}{SourceLabel} +0x{Offset:X4} · {title}{target}";
    }

    public static LoomScriptEvent? FromInstruction(
        LoomScriptDisassembly script,
        LoomScriptInstruction instruction,
        LoomScriptEventCertainty certainty)
    {
        var operation = instruction.Operation;
        var first = FirstArgument(instruction.Operands);
        var (kind, targetKind, targetId) = operation switch
        {
            "startScript" => (LoomScriptEventKind.ScriptStarted, ResourceKind.Script, ParseLiteral(first)),
            "chainScript" => (LoomScriptEventKind.ScriptChained, ResourceKind.Script, ParseLiteral(first)),
            // stopScript 0 ends the current script (o5_stopScript) and names no other script.
            "stopScript" when ParseLiteral(first) != 0 =>
                (LoomScriptEventKind.ScriptStopped, ResourceKind.Script, ParseLiteral(first)),
            "startObject" => (LoomScriptEventKind.ObjectScriptStarted, ResourceKind.Object, ParseLiteral(first)),
            "startMusic" => (LoomScriptEventKind.MusicStarted, ResourceKind.Sound, ParseLiteral(first)),
            "stopMusic" => (LoomScriptEventKind.MusicStopped, (ResourceKind?)null, (int?)null),
            "startSound" => (LoomScriptEventKind.SoundStarted, ResourceKind.Sound, ParseLiteral(first)),
            "stopSound" => (LoomScriptEventKind.SoundStopped, ResourceKind.Sound, ParseLiteral(first)),
            "cutscene" => (LoomScriptEventKind.CutsceneStarted, (ResourceKind?)null, (int?)null),
            "endCutscene" => (LoomScriptEventKind.CutsceneEnded, (ResourceKind?)null, (int?)null),
            "freezeScripts" => (LoomScriptEventKind.ScriptsFrozen, (ResourceKind?)null, (int?)null),
            "print" or "printEgo" => (LoomScriptEventKind.Dialogue, (ResourceKind?)null, (int?)null),
            "loadRoom" => (LoomScriptEventKind.RoomLoaded, ResourceKind.Room, ParseLiteral(first)),
            "loadRoomWithEgo" => (LoomScriptEventKind.RoomLoaded, ResourceKind.Room, ParseLiteral(SecondArgument(instruction.Operands))),
            "setState" or "drawObject" => (LoomScriptEventKind.ObjectStateChanged, ResourceKind.Object, ParseLiteral(first)),
            "actorOps" or "setClass" or "animateActor" or "putActor" or "putActorInRoom" or
                "faceActor" or "walkActorTo" or "walkActorToActor" or "walkActorToObject" or
                "putActorAtObject" or "actorFollowCamera" => (LoomScriptEventKind.CharacterAction, (ResourceKind?)null, (int?)null),
            _ => ((LoomScriptEventKind?)null, (ResourceKind?)null, (int?)null)
        };

        if (kind is null) return null;
        // o5_startScript: opcode bit 0x20 makes the started script freeze-resistant;
        // bit 0x40 makes it recursive (runScript then does not stop a running copy).
        return new LoomScriptEvent(script.ScriptId, script.Context ?? $"Script {script.ScriptId:00}",
            instruction.Offset, kind.Value, operation, instruction.Operands, targetKind, targetId, certainty,
            operation == "startScript" && (instruction.Opcode & 0x20) != 0,
            Recursive: operation == "startScript" && (instruction.Opcode & 0x40) != 0);
    }

    private static string FirstArgument(string operands)
    {
        var value = operands.Trim();
        var comma = value.IndexOf(',');
        if (comma >= 0) value = value[..comma];
        return value.Trim();
    }

    private static string SecondArgument(string operands)
    {
        var values = operands.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return values.Length > 1 ? values[1] : "";
    }

    private static int? ParseLiteral(string operand)
    {
        if (!operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return null;
        return value;
    }
}

using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Relationships;

public sealed partial class LoomRelationshipIndexer
{
    private static LoomRelationshipIndex BuildCd(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        var relationships = new List<LoomRelationship>();
        var resources = game.Resources.Where(r => r.ResourceId.HasValue)
            .ToDictionary(r => (r.Kind, Id: r.ResourceId!.Value, r.ScriptRoom));
        GameResource? Target(ResourceKind kind, int id, int room) => kind == ResourceKind.Script && id >= data.GlobalScriptCount
            ? room > 0 ? resources.GetValueOrDefault((kind, id, room)) : null
            : resources.GetValueOrDefault((kind, id, null));
        void Visit(LoomScriptDisassembly? script, int room, int? obj, int? verb, string label)
        {
            if (script is null) return;
            var source = script.Context is null ? Target(ResourceKind.Script, script.ScriptId, 0)
                : script.Context.Contains("local", StringComparison.OrdinalIgnoreCase) ? Target(ResourceKind.Script, script.ScriptId, room)
                : Target(ResourceKind.Room, room, 0);
            if (source is null) return;
            foreach (var issue in script.Issues ?? [])
                relationships.Add(new(source, "decode issue", null, $"Unresolved path +0x{issue.Offset:X4}",
                    $"{label} in {script.ContainerName}: {issue.Reason}", LoomRelationshipStatus.Unresolved));
            foreach (var instruction in script.Instructions)
            {
                var evidence = $"{label} in {script.ContainerName}+0x{(script.CodeFileOffset ?? script.ResourceOffset) + instruction.Offset:X}: " +
                    $"{instruction.Operation} {instruction.Operands}; static decoded instruction, execution path is unverified.";
                var ev = LoomScriptEvent.FromInstruction(script, instruction, LoomScriptEventCertainty.StaticPotential);
                if (ev?.TargetKind is { } kind)
                {
                    var target = ev.TargetId is int id ? Target(kind, id, room) : null;
                    var targetLabel = ev.TargetId is int number ? $"{kind} {number:00}" : $"dynamic {kind} target";
                    if (kind == ResourceKind.Script && ev.TargetId >= data.GlobalScriptCount)
                        targetLabel += room > 0 ? $" in Room {room:00}" : " in the current room (unresolved)";
                    relationships.Add(new(source, ev.Kind switch
                    {
                        LoomScriptEventKind.ScriptStarted => "may start script",
                        LoomScriptEventKind.ScriptChained => "may continue in script",
                        LoomScriptEventKind.ScriptStopped => "may stop script",
                        LoomScriptEventKind.RoomLoaded => "may load room",
                        LoomScriptEventKind.ObjectScriptStarted => "may run object verb",
                        _ => "may use resource"
                    }, target, targetLabel, evidence, target is null ? LoomRelationshipStatus.Unresolved : LoomRelationshipStatus.StaticCandidate));
                }
                if (instruction.Operation == "actorOps")
                    foreach (System.Text.RegularExpressions.Match match in CostumeOperation.Matches(instruction.Operands))
                    {
                        int id = Convert.ToInt32(match.Groups["value"].Value[2..], 16);
                        var target = Target(ResourceKind.Costume, id, 0);
                        relationships.Add(new(source, "may give actor a costume", target, $"Costume {id:00}", evidence,
                            target is null ? LoomRelationshipStatus.Unresolved : LoomRelationshipStatus.StaticCandidate));
                    }
            }
        }
        new LoomSceneStateScanner(game).VisitCodeUnits(Visit);
        foreach (var room in game.Rooms)
            Visit(data.RoomExitScript(room.ResourceId!.Value), room.ResourceId.Value, null, null, $"Room {room.ResourceId:00} exit");
        return new(relationships);
    }
}

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    private Dictionary<int, (int Costume, string Evidence)>? _inventoryCharacterCostumes;

    /// <summary>
    /// A detached full-body standing picture. Off-room actors need native showActor
    /// animation frames decoded even when their live costume is already known.
    /// A zero costume can use a unique literal source assignment for this actor;
    /// this is display evidence, never an actor or story edit.
    /// </summary>
    public ScummActorSnapshot? ReadInventoryCharacterAppearance(int actorId, out string? evidence)
    {
        evidence = null;
        if (!InventoryCharacters().Any(c => c.ActorId == actorId)) return null;
        var actor = _runtime.Actors[actorId];
        var costumeId = actor.Look.Costume;
        if (costumeId is > 0)
            evidence = $"Actor {actorId}: current native costume {costumeId}; detached standing animation";
        else
        {
            _inventoryCharacterCostumes ??= InventoryCharacterCostumes();
            if (!_inventoryCharacterCostumes.TryGetValue(actorId, out var source)) return null;
            costumeId = source.Costume;
            evidence = $"Actor {actorId}: display-only costume from {source.Evidence}; live actor remains unchanged";
        }
        if (ReadCostume(costumeId.Value) is not { } costume) return null;
        var look = actor.Look;
        var animation = new ScummCostumeAnimationState();
        var direction = ScummCostume.NewDirToOldDir(look.Facing ?? 180);
        // Actor_v2::showActor starts stand, init, then talk-stop. Decode into a
        // private limb state, including actors never yet shown in the live room.
        foreach (var frame in new[] { look.StandFrame, look.InitFrame, look.TalkStopFrame })
            costume.DecodeAnimation(animation, frame, direction);
        return actor with
        {
            Look = look with { Costume = costumeId, Frame = look.TalkStopFrame, Animation = animation,
                Palette = look.Palette.ToArray() }
        };
    }

    private Dictionary<int, (int Costume, string Evidence)> InventoryCharacterCostumes()
    {
        var assignments = new List<(int Actor, int Costume, string Evidence)>();
        void Read(ScummScriptDisassembly? script)
        {
            foreach (var instruction in script?.Instructions ?? [])
            {
                if (instruction.Operation != "actorOps") continue;
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 3 || parts[2] != "sub=0x04" ||
                    InventoryLiteral(parts[0]) is not int actor || InventoryLiteral(parts[1]) is not int costume || costume == 0) continue;
                assignments.Add((actor, costume, $"{script!.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operands}"));
            }
        }
        foreach (var id in _data.GlobalScriptIds()) Read(_data.GlobalScript(id));
        foreach (var room in _game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
        {
            Read(_data.RoomEntryScript(room)); Read(_data.RoomExitScript(room));
            foreach (var id in _data.RoomLocalScriptIds(room)) Read(_data.RoomLocalScript(room, id));
            var records = _data.ReadRoom(room).Objects;
            var verbs = _data.ReadObjectVerbs(room, records);
            foreach (var obj in records.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? []) Read(_data.ReadObjectVerb(room, obj, verb.VerbId, out _));
        }
        // Multiple possible looks require actual session state. Never pick an
        // arbitrary first costume or an actor-ID-specific fallback table.
        return assignments.GroupBy(x => x.Actor).Where(g => g.Select(x => x.Costume).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => (g.First().Costume, g.First().Evidence));
    }
}

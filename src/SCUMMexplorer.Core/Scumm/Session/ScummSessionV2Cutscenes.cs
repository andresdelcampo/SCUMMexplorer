using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    // ScummVM script_v2.cpp o2_cutscene/o2_endCutscene (1464–1503): classic Maniac
    // restores camera mode, then either follows the current ego or returns to the saved room.
    // Camera coordinates, destination and followed actor are not part of cutSceneData.
    // Null means the caller must execute the normal actorFollowCamera path.
    private string? RestoreClassicCutsceneScene(int room, int cameraMode, ScummScriptEvent scriptEvent, int depth)
    {
        _runtime.Camera.Mode = cameraMode;
        if (cameraMode == ScummCamera.FollowActor) return null;
        if (room == (_runtime.CurrentRoom ?? NullRoom)) return "";
        return HandleEvent(scriptEvent with
        {
            Kind = ScummScriptEventKind.RoomLoaded,
            Operation = "loadRoom",
            Operands = $"0x{room:X2} (classic endCutscene saved room)",
            TargetKind = ResourceKind.Room,
            TargetId = room,
            ResolvedArguments = [room]
        }, depth) ?? "";
    }
}

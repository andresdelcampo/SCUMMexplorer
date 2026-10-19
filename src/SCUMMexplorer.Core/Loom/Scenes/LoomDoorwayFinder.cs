using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>A possible room destination reached statically from an object verb.</summary>
public sealed record LoomDoorwayTarget(int RoomId, int InstructionOffset, string Path);

/// <summary>LOOM's existing doorway API over the shared script-derived finder.</summary>
public sealed class LoomDoorwayFinder
{
    private readonly ScummDoorwayFinder _finder;
    public LoomDoorwayFinder(GameLibrary game) => _finder = new(game);
    public LoomDoorwayFinder(GameLibrary game, IScummDataSource data) => _finder = new(game, data);
    public IReadOnlyList<LoomDoorwayTarget> Find(GameResource room, ScummScriptDisassembly verbScript) =>
        _finder.Find(room, verbScript).Select(target => new LoomDoorwayTarget(target.RoomId, target.InstructionOffset, target.Path)).ToArray();
}

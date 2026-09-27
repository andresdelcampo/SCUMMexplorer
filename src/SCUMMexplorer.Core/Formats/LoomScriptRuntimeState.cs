using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>Known state shared by bounded script runs in one exploration route.</summary>
public sealed class LoomScriptRuntimeState
{
    public LoomScriptRuntimeState(IReadOnlyDictionary<int, byte> initialStates,
        IReadOnlyDictionary<int, byte>? initialOwners = null)
    {
        ObjectStates = new Dictionary<int, byte>(initialStates);
        ObjectOwners = initialOwners is null ? [] : new Dictionary<int, byte>(initialOwners);
        // Old-v3 bit variables address one of 256 global words. SCUMM starts
        // those words at zero; later scripts update the same backing words.
        for (var index = 0; index < 256; index++)
            GlobalVariables[$"var[0x{index:X3}]"] = 0;
    }

    public Dictionary<int, byte> ObjectStates { get; }
    public Dictionary<int, byte> ObjectOwners { get; }
    public Dictionary<int, RoomObjectPosition> ObjectPositions { get; } = [];
    public Dictionary<string, ushort> GlobalVariables { get; } = new(StringComparer.Ordinal);
    public HashSet<int> Inventory { get; } = [];
    public HashSet<int> ActiveSounds { get; } = [];
    /// <summary>drawObject calls since the last room entry or setState, in call order.</summary>
    public List<int> DrawQueue { get; } = [];
}

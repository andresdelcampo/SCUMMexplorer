using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>Known state shared by bounded script runs in one exploration route.</summary>
public sealed class LoomScriptRuntimeState
{
    public LoomScriptRuntimeState(IReadOnlyDictionary<int, byte> initialStates,
        IReadOnlyDictionary<int, byte>? initialOwners = null,
        IReadOnlyDictionary<int, uint>? initialClasses = null)
    {
        ObjectStates = new Dictionary<int, byte>(initialStates);
        ObjectOwners = initialOwners is null ? [] : new Dictionary<int, byte>(initialOwners);
        ObjectClasses = initialClasses is null ? [] : new Dictionary<int, uint>(initialClasses);
        // Old-v3 bit variables address one of 256 global words. SCUMM starts
        // those words at zero; later scripts update the same backing words.
        for (var index = 0; index < 256; index++)
            GlobalVariables[$"var[0x{index:X3}]"] = 0;
    }

    public Dictionary<int, byte> ObjectStates { get; }
    public Dictionary<int, byte> ObjectOwners { get; }
    /// <summary>Class bits per object or actor number (ScummVM <c>_classData</c>).</summary>
    public Dictionary<int, uint> ObjectClasses { get; }
    public Dictionary<int, RoomObjectPosition> ObjectPositions { get; } = [];
    public Dictionary<string, ushort> GlobalVariables { get; } = new(StringComparer.Ordinal);
    public HashSet<int> Inventory { get; } = [];
    public HashSet<int> ActiveSounds { get; } = [];
    /// <summary>drawObject calls since the last room entry or setState, in call order.</summary>
    public List<int> DrawQueue { get; } = [];
    /// <summary>Objects whose state reached <c>setState</c> calls set since the last room entry.</summary>
    public HashSet<int> StatesSetInRoom { get; } = [];
    /// <summary>Stand-in for the engine's random source; every value it gives is a labelled pick.</summary>
    public LoomSeededRandom Random { get; set; } = new(0);
    /// <summary>Global variables whose value came from a seeded pick, directly or through arithmetic or a branch on one.</summary>
    public HashSet<string> EstimatedVariables { get; } = new(StringComparer.Ordinal);
    /// <summary>Objects whose state, class or draw was set by code that ran only because of a seeded pick.</summary>
    public HashSet<int> EstimatedStates { get; } = [];
}

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
    /// <summary>
    /// Answers <c>isScriptRunning</c> from the session's running and pending
    /// scripts; <c>Estimate</c> says why the answer may differ from the game.
    /// Null outside a session.
    /// </summary>
    public Func<int, (bool Running, string? Estimate)>? ScriptRunning { get; set; }
    /// <summary>
    /// The camera's x limits in the current room, or null when no room is loaded:
    /// <c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c> (<c>var[0x011]</c>/<c>var[0x012]</c>), which
    /// room entry sets to half a screen from each edge and <c>roomOps roomScroll</c>
    /// narrows. Camera commands keep <c>var[0x002]</c> (<c>VAR_CAMERA_POS_X</c>) inside them.
    /// </summary>
    public (int Min, int Max)? CameraRange { get; set; }
    /// <summary>Half a screen from each edge of the current room: the widest camera limits (<c>cameraMoved</c>).</summary>
    public (int Min, int Max)? RoomCameraBounds { get; set; }
    /// <summary>
    /// The virtual mouse the session put in <c>var[0x014]</c>/<c>var[0x015]</c>
    /// (ScummVM <c>VAR_VIRT_MOUSE_X/Y</c>) this frame, and the input assumption
    /// behind it; null outside a session room.
    /// </summary>
    public (int X, int Y)? VirtualMouse { get; set; }
    public string? VirtualMouseAssumption { get; set; }
    /// <summary>The current room's walk boxes (flags as <c>setBoxFlags</c> left them), or null outside a room.</summary>
    public LoomWalkBoxSet? WalkBoxes { get; set; }
    /// <summary>Why the current room's box flags are unknown (a <c>setBoxFlags</c> with an unknown box), or null.</summary>
    public string? WalkBoxFlagsUnknown { get; set; }
    /// <summary>Actor rooms, positions and walk boxes as reached code left them.</summary>
    public LoomActorTable Actors { get; } = new();
    /// <summary>The current room's number, or null outside a room.</summary>
    public int? CurrentRoom { get; set; }
}

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
        Actors.NativeMutation = NotifyNativeMutation;
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
    internal LoomScriptObserver? MutationObserver { get; set; }

    /// <summary>Native writes/removals notify even for identical or absent values; estimates are untouched.</summary>
    public void SetObjectState(int subject, byte value)
    { ObjectStates[subject] = value; NotifyNativeMutation("object-state", subject); }
    public bool RemoveObjectState(int subject)
    { var removed = ObjectStates.Remove(subject); NotifyNativeMutation("object-state", subject); return removed; }
    public void SetObjectClasses(int subject, uint value)
    { ObjectClasses[subject] = value; NotifyNativeMutation("object-classes", subject); }
    public bool RemoveObjectClasses(int subject)
    { var removed = ObjectClasses.Remove(subject); NotifyNativeMutation("object-classes", subject); return removed; }
    public void SetObjectOwner(int subject, byte value)
    { ObjectOwners[subject] = value; NotifyNativeMutation("object-owner", subject); }
    public bool RemoveObjectOwner(int subject)
    { var removed = ObjectOwners.Remove(subject); NotifyNativeMutation("object-owner", subject); return removed; }

    /// <summary>
    /// Report a runtime/session global write even when its number is unchanged. This does not
    /// change alias or estimate policy. Raw dictionary writes outside these APIs still require
    /// explicit relation invalidation; the dictionary is retained for API compatibility.
    /// </summary>
    public void SetGlobalVariable(string key, ushort value)
    {
        GlobalVariables[key] = value;
        NotifyGlobalMutation(key);
    }

    public bool RemoveGlobalVariable(string key)
    {
        var removed = GlobalVariables.Remove(key);
        NotifyGlobalMutation(key);
        return removed;
    }

    /// <summary>
    /// Notify the scoped/session observer after a raw global dictionary mutation, including
    /// same-value writes or removals. This does not write storage, synchronize aliases, change
    /// estimates or detect unnotified mutations. Unscoped locals use tracker.NotifyExternal.
    /// </summary>
    public void NotifyGlobalMutation(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        MutationObserver?.GlobalMutation(key);
    }

    /// <summary>
    /// Notify an optional independent native dependency tracker of raw native writes/removals or
    /// estimate changes, including same-value writes. Supported domains are object-state/classes,
    /// actor query fields (or actor), and actor-palette; unknown domains retire all native reads.
    /// No storage, estimates or numeric relations are changed; unnotified raw writes remain limits.
    /// </summary>
    public void NotifyNativeMutation(string domain, int? subject = null)
    {
        ArgumentNullException.ThrowIfNull(domain);
        MutationObserver?.NativeMutation(domain, subject);
    }

    /// <summary>Scope mutation notification to the supplied observer, restoring it on disposal.</summary>
    public IDisposable ObserveGlobalMutations(LoomScriptObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return new MutationScope(this, observer);
    }

    private sealed class MutationScope : IDisposable
    {
        private LoomScriptRuntimeState? state;
        private readonly LoomScriptObserver? previous;
        public MutationScope(LoomScriptRuntimeState state, LoomScriptObserver observer)
        {
            this.state = state;
            previous = state.MutationObserver;
            state.MutationObserver = observer;
        }
        public void Dispose()
        {
            if (state is null) return;
            state.MutationObserver = previous;
            state = null;
        }
    }
    public HashSet<int> Inventory { get; } = [];
    public HashSet<int> ActiveSounds { get; } = [];
    /// <summary>Native resource-clock status, with unresolved/seeded route certainty; null outside a session.</summary>
    public Func<int, (bool Running, string? Estimate)>? SoundRunning { get; set; }
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
    /// Told the offset of the skip jump when the running script reaches
    /// <c>beginOverride</c>, and null at <c>endOverride</c> (ScummVM records
    /// the point in <c>vm.cutScenePtr</c> for <c>abortCutscene</c>). Null outside a session.
    /// </summary>
    public Action<int?>? OverrideChanged { get; set; }
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
    /// <summary>The camera's mode, followed actor and destination (its x is <c>var[0x002]</c>).</summary>
    public LoomCamera Camera { get; } = new();
    /// <summary>The stored name of an object of the current room (text code 6), or null; set by the session at each room entry.</summary>
    public Func<int, byte[]?>? ObjectName { get; set; }
    /// <summary>Messages as reached <c>print</c>s left them: text slots, the talk message and its delay, drawn strings.</summary>
    public LoomMessageTable Messages { get; } = new();
    /// <summary>Verb slots as reached <c>verbOps</c> left them (the verb bar and the verb-area close-up).</summary>
    public LoomVerbTable Verbs { get; } = new();
    /// <summary>The current room's number, or null outside a room.</summary>
    public int? CurrentRoom { get; set; }
    /// <summary>
    /// ScummVM <c>_userPut</c>: <c>cursorCommand userInputOn</c>/<c>Off</c> set it to
    /// 1/0 and the soft forms add or take one; clicks are read only while it is
    /// above 0 (<c>checkExecVerbs</c>). It starts at 0.
    /// </summary>
    public int UserPut { get; set; }
    /// <summary>Why <see cref="UserPut"/> is an estimate (it was set on a path behind a seeded pick), or null.</summary>
    public string? UserPutEstimate { get; set; }
}

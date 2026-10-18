
namespace SCUMMexplorer.Core.Scumm;

/// <summary>Known state shared by bounded script runs in one exploration route.</summary>
public sealed class ScummScriptRuntimeState
{
    public SCUMMexplorer.Core.Domain.ScummVersion EngineVersion { get; set; } = SCUMMexplorer.Core.Domain.ScummVersion.V3;
    public bool IsV2 => EngineVersion is SCUMMexplorer.Core.Domain.ScummVersion.V1 or SCUMMexplorer.Core.Domain.ScummVersion.V2;
    public string EgoVariable => IsV2 ? ScummVars.V2.Ego : ScummVars.Ego;
    public string CurrentLightsVariable => IsV2 ? ScummVars.V2.CurrentLights : ScummVars.CurrentLights;
    public string CameraMinVariable => IsV2 ? ScummVars.V2.CameraMinX : ScummVars.CameraMinX;
    public string CameraMaxVariable => IsV2 ? ScummVars.V2.CameraMaxX : ScummVars.CameraMaxX;
    public string CameraPosVariable => ScummVars.CameraPosX;
    public string MusicTimerVariable => IsV2 ? ScummVars.V2.MusicTimer : ScummVars.MusicTimer;
    public string MouseXVariable => IsV2 ? ScummVars.V2.MouseX : ScummVars.MouseX;
    public string MouseYVariable => IsV2 ? ScummVars.V2.MouseY : ScummVars.MouseY;
    public int? ReadCameraPixels() => GlobalVariables.TryGetValue(CameraPosVariable, out var value) ? value * (IsV2 ? 8 : 1) : null;
    public void WriteCameraPixels(int pixels) => SetGlobalVariable(CameraPosVariable, (ushort)(pixels / (IsV2 ? 8 : 1)));
    public int V2DefaultTalkColor { get; set; } = 15;
    /// <summary>The source-defined classic Maniac verb screen offset; other v1/v2 games use zero.</summary>
    public int V2VerbYOffset { get; set; }
    public bool IsClassicManiac { get; set; }
    /// <summary>Names assigned by reached v1/v2 actorOps, retained as source text.</summary>
    public Dictionary<int, string> ActorNames { get; } = [];
    /// <summary>Actor_v2::isPlayer uses the current ego in Maniac v1, otherwise globals 42 and 43 (actor.cpp).</summary>
    public bool IsPlayerActor(int number) => IsV2
        ? IsClassicManiac ? GlobalVariables.GetValueOrDefault(EgoVariable) == number
            : GlobalVariables.GetValueOrDefault(ScummVars.V2.FirstPlayerActor) <= number && number <= GlobalVariables.GetValueOrDefault(ScummVars.V2.LastPlayerActor)
        : ObjectClasses.TryGetValue(number, out var bits) && ScummObjectClasses.Has(bits, ScummActorTable.PlayerClass);
    public int V2TextSpeed { get; set; } = 6; // scumm.cpp resetScumm: _defaultTextSpeed.
    public int V2UserState { get; private set; }
    public (int Verb, int Object1, int Object2, int Preposition)? V2DrawnSentence { get; set; }
    public Action<bool>? V2FreezeScripts { get; set; }
    /// <summary>script_v2.cpp setUserState, scumm.h USERSTATE flags (17 October 2026).</summary>
    public void ApplyV2UserState(int state)
    {
        if ((state & 4) != 0) V2UserState = state & 0xE0;
        if ((state & 1) != 0) V2FreezeScripts?.Invoke((state & 8) != 0);
        if ((state & 2) != 0) UserPut = CursorState = (state & 0x10) != 0 ? 1 : 0;
    }
    public ScummScriptRuntimeState(IReadOnlyDictionary<int, byte> initialStates,
        IReadOnlyDictionary<int, byte>? initialOwners = null,
        IReadOnlyDictionary<int, uint>? initialClasses = null, bool useV4Walking = false, int actorCount = ScummActorTable.Count)
    {
        ObjectStates = new Dictionary<int, byte>(initialStates);
        ObjectOwners = initialOwners is null ? [] : new Dictionary<int, byte>(initialOwners);
        ObjectClasses = initialClasses is null ? [] : new Dictionary<int, uint>(initialClasses);
        Actors = new ScummActorTable(useV4Walking, actorCount);
        Actors.NativeMutation = NotifyNativeMutation;
        Messages.NativeMutation = () => NotifyNativeMutation("message");
        Camera.NativeMutation = () => NotifyNativeMutation("camera");
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
    /// <summary>Notified placement mutations; raw dictionary writes still require notification.</summary>
    public void SetObjectPosition(int subject, RoomObjectPosition value)
    { ObjectPositions[subject] = value; NotifyNativeMutation("object-position", subject); }
    public bool TryAddObjectPosition(int subject, RoomObjectPosition value)
    { var added = ObjectPositions.TryAdd(subject, value); NotifyNativeMutation("object-position", subject); return added; }
    public bool RemoveObjectPosition(int subject)
    { var removed = ObjectPositions.Remove(subject); NotifyNativeMutation("object-position", subject); return removed; }
    public Dictionary<string, ushort> GlobalVariables { get; } = new(StringComparer.Ordinal);
    internal ScummScriptObserver? MutationObserver { get; set; }

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
    public IDisposable ObserveGlobalMutations(ScummScriptObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return new MutationScope(this, observer);
    }

    private sealed class MutationScope : IDisposable
    {
        private ScummScriptRuntimeState? state;
        private readonly ScummScriptObserver? previous;
        public MutationScope(ScummScriptRuntimeState state, ScummScriptObserver observer)
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

    /// <summary>
    /// ScummVM's <c>_inventory</c> (80 slots for these versions): <c>addObjectToInventory</c> (pickupObject) takes the first free slot and
    /// <c>clearOwnerOf</c> (<c>setOwnerOf</c> with owner 0) frees it. <c>findInventory</c> and <c>getInventoryCount</c> read this array, not the owner table:
    /// an object owned by an actor through <c>setOwnerOf</c> alone is not in it.
    /// </summary>
    public int[] InventorySlots { get; } = new int[80];
    /// <summary>The room an inventory object was taken from (<c>addObjectToInventory</c> copies its code from there).</summary>
    public Dictionary<int, int> InventoryRooms { get; } = [];
    /// <summary>
    /// ScummVM <c>getVerbEntrypoint</c>: where the object's code for the verb starts (0 when the object is not in the room or inventory, or has no entry
    /// for the verb or a fallback). Answered by the session, which can read the room data; null outside a session.
    /// </summary>
    public Func<int, int, int?>? VerbEntrypoint { get; set; }
    /// <summary>
    /// ScummVM <c>getActorFromPos(x, y)</c>: the lowest-numbered touchable actor drawn on the point's strip whose drawn top and bottom enclose y, or 0;
    /// null where the session does not model it (the instruction then stays unresolved).
    /// </summary>
    public Func<int, int, int>? ActorFromPosition { get; set; }
    /// <summary>ScummVM <c>convertVerbMessage</c> (text code 5): the name of the verb in use with that number; null where the session does not model it.</summary>
    public Func<int, byte[]?>? VerbName { get; set; }
    public bool AddToInventorySlots(int subject)
    {
        var free = Array.IndexOf(InventorySlots, 0);
        if (free < 0) return false;
        InventorySlots[free] = subject;
        return true;
    }
    public void RemoveFromInventorySlots(int subject)
    {
        var index = Array.IndexOf(InventorySlots, subject);
        if (index >= 0) InventorySlots[index] = 0;
    }

    /// <summary>
    /// ScummVM's <c>_sentence</c> stack: <c>doSentence</c> pushes a verb with two objects, the engine pops the last one into the sentence script
    /// (<c>VAR_SENTENCE_SCRIPT</c>) when that script is not running.
    /// </summary>
    public List<(int Verb, int ObjectA, int ObjectB)> Sentences { get; } = [];
    /// <summary>Executed membership attempts notify even when unchanged; raw set edits still need notification.</summary>
    public bool AddInventoryObject(int subject)
    { var added = Inventory.Add(subject); NotifyNativeMutation("inventory-membership", subject); return added; }
    public bool RemoveInventoryObject(int subject)
    { var removed = Inventory.Remove(subject); NotifyNativeMutation("inventory-membership", subject); return removed; }
    public HashSet<int> ActiveSounds { get; } = [];
    /// <summary>Native resource-clock status, with unresolved/seeded route certainty; null outside a session.</summary>
    public Func<int, (bool Running, string? Estimate)>? SoundRunning { get; set; }

    /// <summary>
    /// A script armed a hook of a playing song (<c>soundKludge</c> player command 12 or 20: sound, class, value; S6 Monkey2). Returns what the session's sound
    /// clock now follows, or null when it does not follow it.
    /// </summary>
    public Func<int, int, int, string?>? SoundHook { get; set; }
    /// <summary>drawObject calls since the last room entry or setState, in call order.</summary>
    public List<int> DrawQueue { get; } = [];
    /// <summary>Objects whose state reached <c>setState</c> calls set since the last room entry.</summary>
    public HashSet<int> StatesSetInRoom { get; } = [];
    /// <summary>Stand-in for the engine's random source; every value it gives is a labelled pick.</summary>
    public ScummSeededRandom Random { get; set; } = new(0);
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

    /// <summary>
    /// <c>actorFollowCamera</c> on an actor outside the current room starts that room's scene, as ScummVM's <c>setCameraFollows</c> does
    /// (<see cref="ScummProfile.CameraFollowStartsScene"/>); off, the preview only reports that it did not model the room load.
    /// </summary>
    public bool CameraFollowStartsScene { get; set; }
    /// <summary>Half a screen from each edge of the current room: the widest camera limits (<c>cameraMoved</c>).</summary>
    public (int Min, int Max)? RoomCameraBounds { get; set; }

    /// <summary>
    /// The shadow palette (ScummVM <c>_shadowPalette</c>, set by <c>roomOps roomPalette a, b</c> in old-v3 games): colour slot b is shown as
    /// colour a. Identity at room entry. The opening's title fades by stepping one slot through the EGA colours.
    /// </summary>
    public byte[] ShadowPalette { get; } = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    /// <summary>
    /// ScummVM <c>_roomPalette</c> (<c>roomOps roomColorLegacy a, b</c>: <c>_roomPalette[b] = a</c>): the colour each colour number of the room picture and its objects is
    /// drawn as. Identity at every room load (room.cpp <c>startScene</c>). Actors are not drawn through it.
    /// </summary>
    public byte[] RoomColors { get; } = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    public void ResetRoomColors()
    {
        for (var i = 0; i < RoomColors.Length; i++) RoomColors[i] = (byte)i;
    }

    /// <summary>The flashlight's size in strips set by <c>lights</c> (ScummVM <c>_flashlight.xStrips/yStrips</c>), (0, 0) when none.</summary>
    public (int X, int Y) Flashlight { get; set; }

    /// <summary>The game's string resources (<c>stringOps</c>), by string number, with the zero terminator.</summary>
    public Dictionary<int, byte[]> Strings { get; } = [];

    public void ResetShadowPalette()
    {
        for (var i = 0; i < ShadowPalette.Length; i++) ShadowPalette[i] = (byte)i;
    }
    /// <summary>
    /// The virtual mouse the session put in <c>var[0x014]</c>/<c>var[0x015]</c>
    /// (ScummVM <c>VAR_VIRT_MOUSE_X/Y</c>) this frame, and the input assumption
    /// behind it; null outside a session room.
    /// </summary>
    public (int X, int Y)? VirtualMouse { get; set; }
    public string? VirtualMouseAssumption { get; set; }
    /// <summary>The current room's walk boxes (flags as <c>setBoxFlags</c> left them), or null outside a room.</summary>
    public ScummWalkBoxSet? WalkBoxes { get; set; }
    /// <summary>Why the current room's box flags are unknown (a <c>setBoxFlags</c> with an unknown box), or null.</summary>
    public string? WalkBoxFlagsUnknown { get; set; }
    /// <summary>Actor rooms, positions and walk boxes as reached code left them.</summary>
    public ScummActorTable Actors { get; }
    /// <summary>The camera's mode, followed actor and destination (its x is <c>var[0x002]</c>).</summary>
    public ScummCamera Camera { get; } = new();
    /// <summary>The stored name of an object of the current room (text code 6), or null; set by the session at each room entry.</summary>
    public Func<int, byte[]?>? ObjectName { get; set; }
    /// <summary>Names changed by reached old-engine setObjectName operations, retained with the session's resource state.</summary>
    public Dictionary<int, byte[]> ObjectNameOverrides { get; } = [];
    /// <summary>Messages as reached <c>print</c>s left them: text slots, the talk message and its delay, drawn strings.</summary>
    public ScummMessageTable Messages { get; } = new();
    /// <summary>Verb slots as reached <c>verbOps</c> left them (the verb bar and the verb-area close-up).</summary>
    public ScummVerbTable Verbs { get; } = new();
    /// <summary>
    /// ScummVM <c>_resourceMapper</c>, as <c>o5_pseudoRoom</c> fills it: a room number from 0x80 up (a pseudo room) names the real room whose
    /// resource it loads; the same real room can serve several numbers (the game's scripts tell them apart by <c>VAR_ROOM</c>).
    /// </summary>
    public Dictionary<int, int> PseudoRooms { get; } = [];
    /// <summary>The current room's number, or null outside a room.</summary>
    public int? CurrentRoom { get; set; }
    /// <summary>
    /// ScummVM <c>_userPut</c>: <c>cursorCommand userInputOn</c>/<c>Off</c> set it to
    /// 1/0 and the soft forms add or take one; clicks are read only while it is
    /// above 0 (<c>checkExecVerbs</c>). It starts at 0.
    /// </summary>
    private int userPut;
    /// <summary>
    /// The charset <c>cursorCommand charsetSet</c> last chose (ScummVM <c>_string[0].charset</c>), or null before any did.
    /// A message keeps the charset that was set when its <c>print</c> ran (<see cref="ScummMessage.Charset"/>).
    /// </summary>
    public int? Charset { get; set; }
    /// <summary>ScummVM <c>_cursor.state</c>: <c>cursorCommand cursorOn</c>/<c>Off</c> set it to 1/0, the soft forms add or take one; the cursor shows while it is above 0.</summary>
    public int CursorState { get; set; }
    /// <summary>
    /// From version 4 every <c>cursorCommand</c> copies <c>_cursor.state</c> and <c>_userPut</c> into <c>VAR_CURSORSTATE</c> (52) and <c>VAR_USERPUT</c> (53)
    /// (script_v5.cpp <c>o5_cursorCommand</c>, read 16 October 2026); Monkey Island's interface loop (Script 23) reads the first. Off for older versions.
    /// </summary>
    public bool PublishesCursorVariables { get; set; }
    /// <summary>
    /// The class number <c>findObject</c> skips as untouchable (<see cref="ScummProfile.UntouchableClass"/>): 24 for the small-header games, 32 from version 5
    /// (object.cpp <c>getClass</c>, ScummVM master read 17 October 2026).
    /// </summary>
    public int UntouchableClass { get; set; } = ScummObjectClasses.Untouchable;
    /// <summary>
    /// <c>cursorCommand charsetColors</c> per charset: colour-map entries (0 to 15) a script replaced (ScummVM <c>_charsetData[charset]</c>); a text drawn with
    /// that charset takes them in place of the charset file's own map.
    /// </summary>
    public Dictionary<int, Dictionary<int, byte>> CharsetColors { get; } = [];
    /// <summary>
    /// The room (main) screen's rows from <c>roomOps setScreen</c> (ScummVM <c>initScreens(top, bottom)</c>): the text screen is above <c>Top</c>, the verb
    /// screen from <c>Bottom</c> down. ScummVM's start value for these versions is (16, 144) (<c>resetScumm</c>), until a script sets it.
    /// </summary>
    public (int Top, int Bottom) MainScreen { get; set; } = (16, 144);
    public int UserPut
    {
        get => userPut;
        set { userPut = value; NotifyNativeMutation("user-input", 0); }
    }
    /// <summary>Why <see cref="UserPut"/> is an estimate (it was set on a path behind a seeded pick), or null.</summary>
    private string? userPutEstimate;
    public string? UserPutEstimate
    {
        get => userPutEstimate;
        set { userPutEstimate = value; NotifyNativeMutation("user-input", 0); }
    }
}

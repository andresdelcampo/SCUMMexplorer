using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What a scene's own scripts change on screen while it plays (REQUIREMENTS R-8.10, user, 9 November 2026: a cutscene is anything that changes room, even
/// coming back, or animates characters or objects; not anything that only shows lines of text). Read from the instructions those scripts run: a room
/// load; an actor walked, put somewhere or animated (not turned to face a way: ScummVM <c>animateActor</c> 248 to 251 is <c>turnToDirection</c>); the
/// camera moved; an object's picture changed twice or more (a sequence: one change, a door opening, is the interaction's result, not an animation).
/// Talking is not an instruction of the scene's scripts, nor the hero's walk to a picked object (the engine's), so neither counts.
/// </summary>
public sealed class ScummSceneActivity
{
    private readonly Dictionary<string, int> _objectChanges = new(StringComparer.Ordinal);

    /// <summary>A room was loaded by the scene's scripts.</summary>
    public bool ChangesRoom { get; private set; }

    /// <summary>The first instruction that animated something (for Advanced), or null.</summary>
    public string? Animation { get; private set; }

    public bool Animates => Animation is not null;

    /// <summary>The scene showed another room by itself (<c>loadRoom</c>), not the hero walking through a door (<c>loadRoomWithEgo</c>).</summary>
    public bool ChangesView { get; private set; }

    /// <param name="hero">Whether an actor operand names the hero (the ego variable, or the ego's number): the hero's own walks and placements are the
    /// approach to what was picked, written in the script (LOOM's Look at leaf walks Bobbin there), not the scene's animation; their animations count.</param>
    /// <param name="scriptKey">The key of a script the watched code starts by its number (<c>global 107</c>, <c>local 35/211</c>): it is the scene's too.</param>
    public void Record(string label, IEnumerable<ScummScriptInstruction> executed, Func<string, bool>? hero = null, Func<int, string>? scriptKey = null)
    {
        ArgumentNullException.ThrowIfNull(executed);
        foreach (var ins in executed)
        {
            var first = ins.Operands.Split(',', 2)[0].Trim();
            switch (ins.Operation)
            {
                case "startScript" or "chainScript" when scriptKey is not null && Number(first) is int started:
                    _children.Add(scriptKey(started));
                    break;
                case "loadRoom" or "loadRoomWithEgo":
                    ChangesRoom = true;
                    if (ins.Operation == "loadRoom") ChangesView = true;
                    break;
                // The hero's own gestures (reaching, picking up, turning, LOOM's distaff) are the interaction's, not a scene's (user, 9 November 2026).
                case "animateActor" when hero?.Invoke(first) == true:
                    break;
                // The hero's approach: a walk to a spot, or to (or onto) the picked object itself. Moved to another object (LOOM Room 30's Use: Bobbin put at
                // object 516, through the passage) or set down elsewhere is the scene's.
                case "walkActorTo" when hero?.Invoke(first) == true:
                    break;
                case "walkActorToObject" or "putActorAtObject" when hero?.Invoke(first) == true && PickedObject is int picked &&
                                                                     Number(ins.Operands.Split(',').ElementAtOrDefault(1)) == picked:
                    break;
                // The lights changed once (a lamp switched on) is a single change, as a door opening.
                case "lights":
                    break;
                // An actor named by a script variable that is not the hero's (Monkey Island 1's exits: Script 34 puts local[0x000]) cannot be told
                // from the hero walking out: not counted alone.
                case "walkActorTo" or "walkActorToObject" or "walkActorToActor" or "putActor" or "putActorAtObject" when !first.StartsWith("0x", StringComparison.Ordinal):
                    break;
                case "walkActorTo" or "walkActorToObject" or "walkActorToActor" or "putActor" or "putActorAtObject" or "panCameraTo" or "setCameraAt":
                    Animation ??= $"{label} +0x{ins.Offset:X4} {ins.Operation}";
                    break;
                case "animateActor" when !IsPose(ins.Operands, OldVersion):
                    Animation ??= $"{label} +0x{ins.Offset:X4} {ins.Operation}";
                    break;
                // Only a change of the object's picture: in versions 1 and 2 the IntrinsicOn bit (Untouchable, Locked and Pickupable are not drawn; Maniac
                // Mansion's Pick up writes IntrinsicOff then Untouchable, one picture change).
                case "setState" or "drawObject" when ChangesPicture(ins):
                    if ((_objectChanges[first] = _objectChanges.GetValueOrDefault(first) + 1) >= 2)
                        Animation ??= $"{label} +0x{ins.Offset:X4} {ins.Operation} (object {first} changes picture again)";
                    break;
            }
        }
    }

    private readonly HashSet<string> _children = new(StringComparer.Ordinal);

    /// <summary>The keys of the scripts the scene's own scripts started (they are the scene's too: LOOM Room 35's Use hands over to local scripts 211 and 202).</summary>
    public IReadOnlySet<string> Children => _children;

    /// <summary>The picked object (its own approach does not count), or null.</summary>
    public int? PickedObject { get; init; }

    private static int? Number(string? operand)
    {
        var text = operand?.Trim() ?? "";
        return text.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>A <c>setState</c> or <c>drawObject</c> that changes what the object shows (versions 1 and 2 name the state bit: only IntrinsicOn/Off is drawn).</summary>
    internal static bool ChangesPicture(ScummScriptInstruction ins) =>
        ins.Operation == "drawObject" || ins.Operands.Split(',').ElementAtOrDefault(1)?.Trim() is not ("Untouchable" or "Touchable" or "Locked" or "Unlocked" or "Pickupable" or "Unpickupable");

    /// <summary>Versions 1 and 2 (only their facing values are poses).</summary>
    public bool OldVersion { get; init; }

    /// <summary>
    /// An <c>animateActor</c> that is a pose, not a gesture: in versions 1 and 2 the facing values 248 to 251; from version 3 (ScummVM
    /// <c>Actor::animateActor</c>) 244 and up (turn, change direction, stand) and a costume's basic animations 1 to 5 (init, walk, stand, talk start,
    /// talk stop: Monkey Island 2's conversations). A value read from a variable counts as a gesture.
    /// </summary>
    internal static bool IsPose(string operands, bool oldVersion)
    {
        var value = operands.Split(',').ElementAtOrDefault(1)?.Trim() ?? "";
        if (!value.StartsWith("0x", StringComparison.Ordinal) || !int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        return oldVersion ? v is >= 248 and <= 251 : v >= 244 || v is >= 1 and <= 5;
    }
}

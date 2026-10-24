namespace SCUMMexplorer.Core.Scumm;

// R-16.9: the room controls (playback bar, Play Cutscene, Interact, Sounds) are one shared UI in the App with one
// behaviour. A game, a SCUMM version or the static preview only fills them through the types below.

/// <summary>One entry of the room's `Play Cutscene` list: titles already carry the Normal/Advanced wording (R-8.2).</summary>
public sealed record RoomCutsceneEntry(string Title, string Category, string Note, Action? Start, string Status = "");

/// <summary>One row of an object's Interact menu: the verb's own word, its source (Advanced tooltip) and the exploration action.</summary>
public sealed record RoomInteractionChoice(string Label, string? Source, Action Run);

/// <summary>The Interact menu of the selected object: the object's name (any case) and the rows.</summary>
public sealed record RoomInteractionMenu(string Heading, IReadOnlyList<RoomInteractionChoice> Choices);

/// <summary>One row of the Sounds menu (label already formatted, R-6.24).</summary>
public sealed record RoomSoundChoice(string Label, string? Tip, Action Play);

/// <summary>The room's sounds; <paramref name="Stop"/> is offered (as `■ Stop`) only while <paramref name="Playing"/>.</summary>
public sealed record RoomSoundMenu(IReadOnlyList<RoomSoundChoice> Choices, bool Playing, Action Stop);

/// <summary>What the transport buttons show now; cheap to read on every frame.</summary>
public sealed record RoomTransportState(bool PauseEnabled, bool Paused, bool StepEnabled, bool InCutscene, bool CutsceneSkippable, bool SkipLineEnabled)
{
    public static RoomTransportState Idle { get; } = new(false, false, false, false, false, false);
}

/// <summary>The Interact button's state (R-6.22): shown, usable now, and the name of the selected object.</summary>
public sealed record RoomInteractState(bool Visible, bool Enabled, string? TargetName);

/// <summary>Everything the bar shows now (read by the one shared presenter; no game test belongs in the App).</summary>
public sealed record RoomControlState(
    RoomTransportState Transport,
    bool BarVisible,
    bool GlobalCutsceneEnabled,
    bool CanStartCutscene,
    IReadOnlyList<RoomCutsceneEntry> RoomCutscenes,
    bool RoomCutsceneEnabled,
    bool CutsceneListing,
    bool ScanComputing,
    bool ScanNoteVisible,
    bool ScanProgressVisible)
{
    public static RoomControlState Hidden { get; } = new(RoomTransportState.Idle, false, false, false, [], false, false, false, false, false);
}

/// <summary>The one interface a game (or the static preview) implements to fill the shared room controls.</summary>
public interface IRoomControlProvider
{
    RoomTransportState ReadTransport();

    /// <param name="room">The room shown, if any.</param>
    /// <param name="advanced">Advanced wording of titles and notes (R-1.13).</param>
    RoomControlState ReadState(int? room, bool advanced);

    RoomInteractState ReadInteract();

    void TogglePause();
    void Step();
    void Skip();
    void SkipLine();

    /// <summary>The menu of the selected object, or null when there is none to offer now.</summary>
    RoomInteractionMenu? InteractionMenu(bool advanced);

    /// <summary>The room's sounds, or null when the menu cannot open now (device locked, nothing to offer).</summary>
    RoomSoundMenu? SoundMenu(bool advanced);
}

/// <summary>The wording of the shared controls (LOOM's, R-6.21), identical for every game.</summary>
public static class RoomControlText
{
    public const string PauseTip = "Pause or play the scene (space). In Advanced mode, Step ▸ then plays one frame while paused.";
    public const string StepTip = "Advance the scene by one frame (for stepping while paused).";
    public const string SkipLineTip = "Skip the current line, as with the '.' key.";
    public const string GlobalCutsceneTip = "Choose a cutscene from anywhere in the game.";
    public const string GlobalCutsceneBusyTip = "Playback is active. Finish or skip it before starting another scene.";
    public const string RoomCutsceneListingTip = "Looking for the scenes that happen in this room… (the first launch takes a while; the result is kept for the next one).";
    public const string InteractTip = "Click an object to select it, then choose what to do with it. Right-clicking an object opens the same menu.";

    public static string PauseCaption(bool paused) => paused ? "▶ Play" : "⏸ Pause";

    public static string SkipTip(bool inCutscene, bool skippable) =>
        !inCutscene ? "No cutscene is playing."
        : skippable ? "Skip the cutscene (Escape): ScummVM's abortCutscene sends the script that set the override to its skip jump. Assumed input."
        : "The cutscene has not set its skip point (beginOverride) yet, so Escape would do nothing now, as in the game.";

    public static string RoomCutsceneCaption(int entries, bool listing) => entries == 1 || listing ? "Play Cutscene" : "Play Cutscene ▴";

    public static string RoomCutsceneTip(bool listing, bool canStart, IReadOnlyList<RoomCutsceneEntry> entries) =>
        listing ? RoomCutsceneListingTip
        : !canStart ? GlobalCutsceneBusyTip
        : entries.Count == 1 ? $"Play {entries[0].Title}" : "Choose a cutscene that happens in this room.";

    public static string InteractCaption(string? targetName) => string.IsNullOrEmpty(targetName) ? "Interact ▴" : $"Interact · {targetName} ▴";
}

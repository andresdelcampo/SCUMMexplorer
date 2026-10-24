namespace SCUMMexplorer.Core.Scumm;

// R-16.9: the room controls (playback bar, Play Cutscene, Interact, Sounds) are one shared UI in the App with one
// behaviour. A game, a SCUMM version or the static preview only fills them through the types below.

/// <summary>One entry of the room's `Play Cutscene` list: titles already carry the Normal/Advanced wording (R-8.2).</summary>
public sealed record RoomCutsceneEntry(string Title, string Category, string Note, Action? Start, string Status = "");

/// <summary>The global `Play Cutscene` picker: every scene the game can start from anywhere, and the line above the list.</summary>
public sealed record RoomCutscenePicker(IReadOnlyList<RoomCutsceneEntry> Entries, string Summary);

/// <summary>The `Game Status` button (R-6.25.3): whether the game has a status to show now, its tooltip and what opens it. One click applies and closes.</summary>
public sealed record RoomGameStatusEntry(bool Available, string Tip, Action Open)
{
    public static RoomGameStatusEntry None { get; } = new(false, "", () => { });
}

/// <summary>One entry of an Advanced inspection section: a script or an interaction, with the game's own description and its raw code.</summary>
public sealed record RoomInspectionItem(string Header, string? Detail, ScummScriptDisassembly? Code);

/// <summary>A headed group of inspection entries (room entry/exit and local scripts, interactions).</summary>
public sealed record RoomInspectionSection(string Heading, IReadOnlyList<RoomInspectionItem> Items);

/// <summary>What the Advanced right panel lists for the room: the sections and the relationships (resource links) decoded from their code.</summary>
public sealed record RoomInspection(IReadOnlyList<RoomInspectionSection> Sections, IReadOnlyList<ScummScriptEvent> Relationships);

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

    /// <summary>The Advanced inspection of the room, or null when the game shows its own (LOOM's `Room entry` trace carries readable text the other versions lack, R-6.25.4).</summary>
    RoomInspection? Inspection();

    /// <summary>The Game Status entry (the button and its flow are shared; the content is the game's).</summary>
    RoomGameStatusEntry GameStatus();

    /// <summary>Whether the background scan has listed the scenes of every room (it shapes the scan note, not the controls).</summary>
    bool RoomListsDone { get; }

    /// <summary>The global picker behind the header `Play Cutscene` button (the game's own list; the dialog is shared).</summary>
    RoomCutscenePicker GlobalCutscenes(bool advanced);

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

    /// <summary>The hotspot row's caption (LOOM's pattern): the stored name, else `Hotspot N`; a name used twice carries the index.</summary>
    public static string HotspotLabel(string? name, int localIndex, bool repeated)
    {
        var label = name ?? $"Hotspot {localIndex}";
        return repeated ? $"{label} · {localIndex}" : label;
    }

    /// <summary>The hotspot row's tooltip (LOOM's pattern).</summary>
    public static string HotspotTip(int localIndex, int objectId, string? name, int x, int y, int width, int height) =>
        $"Hotspot {localIndex} · object {objectId}" + (name is null ? "" : $" · stored name \"{name}\"") +
        $" · {x}, {y} · {width} × {height}. Choose a decoded interaction. Use Enter or Space to activate this hotspot.";

    public static string InteractCaption(string? targetName) => string.IsNullOrEmpty(targetName) ? "Interact ▴" : $"Interact · {targetName} ▴";
}

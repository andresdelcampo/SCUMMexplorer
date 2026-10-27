using System.Windows.Controls;
﻿using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// What a game adds to the main window beyond the shared behaviour: the state it keeps while a game is open, the work it starts in the background and the
/// cutscenes Game Texts can play. The window asks <see cref="MainWindow.CurrentModule"/> and has no game test of its own for these; a game's module lives in its
/// own folder (<c>Loom/</c>) as a nested class of <see cref="MainWindow"/>, so it can reach the window's state the way the room-control providers do.
/// </summary>
internal interface IGameModule
{
    /// <summary>The object states the game's live session starts from, read from its data (null for a game with no dedicated session).</summary>
    ScummIndexedObjectSnapshot? ReadObjectSnapshot(GameLibrary game);

    /// <summary>Starts the game's live session (and its sound or voice) and loads the charset it draws with; leaves the window's session null for a game with none.</summary>
    void StartSession(GameLibrary game, ScummIndexedObjectSnapshot? snapshot);

    /// <summary>A group of the game's own resources for the Advanced tree beyond the shared ones (LOOM CD's audio cues); null for none.</summary>
    TreeViewItem? ExtraResourceGroup(GameLibrary game);

    /// <summary>Forgets what the previous game left (called before another game opens).</summary>
    void Reset();

    /// <summary>The game is open and <see cref="MainWindow"/> holds it: loads the game's own data.</summary>
    void Opened(GameLibrary game);

    /// <summary>Starts the game's background derivations once the room cards have their artwork.</summary>
    void StartBackgroundDerivation(GameLibrary game);

    /// <summary>The cutscenes Game Texts offers, by the indexed script that prints their texts.</summary>
    Dictionary<int, GalleryCutscene> GalleryCutscenes();

    /// <summary>The game's own spoken or disc audio beside the scene sounds (<see cref="NoVoice"/> for a game with none).</summary>
    IGameVoice Voice { get; }

    /// <summary>The option "play the game's voices" (kept even while another game is open).</summary>
    bool VoicesEnabled { get; set; }

    /// <summary>Rebuilds the room's Sounds list when the game has its own kind (true: handled, the generic list is not built); false leaves it to the generic code.</summary>
    bool UpdateRoomSounds(GameResource? room);

    /// <summary>The notes of the speakers a direct verb needs placed in the room before it runs (empty for a game that does not place them).</summary>
    List<string> SpeakersForDirectVerb(GameResource room, int objectId, int verbId);

    /// <summary>The names the game gives its resources, verbs and hero (what every tree, title and readable script shows).</summary>
    GameNames CreateNames(GameLibrary game);

    /// <summary>The fixed wording of a script page's subtitle when the game has one (without the name part); null for the generic wording.</summary>
    string? ScriptKind(GameResource resource);

    /// <summary>True when the game runs in a live session of its own (LOOM's); false for the generic session or none.</summary>
    bool HasDedicatedSession { get; }

    /// <summary>The note a script page shows while it reads the bytecode; null for the generic wording.</summary>
    string? ScriptLoadingNote { get; }

    /// <summary>The note that says an entry is leftover bytes and not a standalone script, or null when it is a script.</summary>
    string? ScriptBoundaryNote(GameResource resource);

    /// <summary>Decodes a script from the game's own source when it has one (null: the generic decoders read it).</summary>
    ScummScriptDisassembly? DecodeScript(GameResource resource);

    /// <summary>What a script page's Play button does for the script, when the game has a catalog that knows it as a scene; null hides the button.</summary>
    Action? ScriptPlayback(int? scriptId);
}

/// <summary>The voice or disc-audio player of a game, driven by the window's session and playback lifecycle.</summary>
internal interface IGameVoice
{
    void Stop();
    void Pause();
    void Resume();
    void Detach();
    /// <summary>Plays the cues the session issued since the last call.</summary>
    void Update();
    /// <summary>Drops the cues a room visit issued without playing them.</summary>
    void SkipPending(LoomExplorationSession? session);
    /// <summary>True when the voice already follows the session.</summary>
    bool Follows(LoomExplorationSession? session);
    /// <summary>Starts following the session; false when the game has no voice.</summary>
    bool Attach(LoomExplorationSession session);
    /// <summary>True when this game's sound comes from the voice and not from the scene sound player.</summary>
    bool ReplacesSceneSound { get; }
}

internal sealed class NoVoice : IGameVoice
{
    public static NoVoice Instance { get; } = new();
    public void Stop() { }
    public void Pause() { }
    public void Resume() { }
    public void Detach() { }
    public void Update() { }
    public void SkipPending(LoomExplorationSession? session) { }
    public bool Follows(LoomExplorationSession? session) => false;
    public bool Attach(LoomExplorationSession session) => false;
    public bool ReplacesSceneSound => false;
}

/// <summary>
/// What the window shows as the names of a game's things. Every game gets the engine's variable names; a game's module adds its own resource names (the glossary),
/// verb names and the name of its hero. The glossary type is still LOOM's (<c>Core/Loom/Text</c>) and reads only the engine rows for any other game.
/// </summary>
internal sealed class GameNames(LoomGlossary glossary, Func<int, string?>? verbName, string hero)
{
    public LoomGlossary Glossary { get; } = glossary;

    /// <summary>The name of a verb, or null where the game gives none (the script then shows the number).</summary>
    public Func<int, string?>? VerbName { get; } = verbName;

    /// <summary>What readable scripts call the hero.</summary>
    public string Hero { get; } = hero;

    public string Label(GameResource resource) => LoomResourceNames.Label(Glossary, resource);
    public string? Evidence(GameResource resource) => LoomResourceNames.Evidence(Glossary, resource);

    /// <summary>The evidence word of the resource's name ("decoded", "reading"...), or null when it has none.</summary>
    public string? EvidenceLabel(GameResource resource) => LoomResourceNames.Entry(Glossary, resource)?.EvidenceLabel;

    public string NameScripts(string text) => LoomResourceNames.NameScripts(Glossary, text);
    public string NameBits(string text) => LoomResourceNames.NameBits(Glossary, text);
}

public partial class MainWindow
{
    private IGameModule? _loomModuleInstance;

    /// <summary>The one place that picks the module of the open game (composition, like <see cref="CurrentRoomControls"/>).</summary>
    private IGameModule CurrentModule => IsLoomGame ? LoomModule : NoGameModule.Instance;

    private IGameModule LoomModule => _loomModuleInstance ??= new LoomGameModule(this);

    /// <summary>A game with no module of its own.</summary>
    private sealed class NoGameModule : IGameModule
    {
        public static NoGameModule Instance { get; } = new();
        public void Reset() { }
        public void Opened(GameLibrary game) { }
        public void StartBackgroundDerivation(GameLibrary game) { }
        public Dictionary<int, GalleryCutscene> GalleryCutscenes() => [];
        public List<string> SpeakersForDirectVerb(GameResource room, int objectId, int verbId) => [];
        public IGameVoice Voice => NoVoice.Instance;
        public bool VoicesEnabled { get; set; } = true;
        public bool UpdateRoomSounds(GameResource? room) => false;
        public GameNames CreateNames(GameLibrary game) => new(LoomGlossary.Load(game), null, "the hero");
        public string? ScriptKind(GameResource resource) => null;
        public Action? ScriptPlayback(int? scriptId) => null;
        public bool HasDedicatedSession => false;
        public TreeViewItem? ExtraResourceGroup(GameLibrary game) => null;
        public ScummIndexedObjectSnapshot? ReadObjectSnapshot(GameLibrary game) => null;
        public void StartSession(GameLibrary game, ScummIndexedObjectSnapshot? snapshot) { }
        public string? ScriptLoadingNote => null;
        public string? ScriptBoundaryNote(GameResource resource) => null;
        public ScummScriptDisassembly? DecodeScript(GameResource resource) => null;
    }
}

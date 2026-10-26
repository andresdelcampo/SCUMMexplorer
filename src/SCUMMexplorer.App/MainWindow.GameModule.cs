using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// What a game adds to the main window beyond the shared behaviour: the state it keeps while a game is open, the work it starts in the background and the
/// cutscenes Game Texts can play. The window asks <see cref="MainWindow.CurrentModule"/> and has no game test of its own for these; a game's module lives in its
/// own folder (<c>Loom/</c>) as a nested class of <see cref="MainWindow"/>, so it can reach the window's state the way the room-control providers do.
/// </summary>
internal interface IGameModule
{
    /// <summary>Forgets what the previous game left (called before another game opens).</summary>
    void Reset();

    /// <summary>The game is open and <see cref="MainWindow"/> holds it: loads the game's own data.</summary>
    void Opened(GameLibrary game);

    /// <summary>Starts the game's background derivations once the room cards have their artwork.</summary>
    void StartBackgroundDerivation(GameLibrary game);

    /// <summary>The cutscenes Game Texts offers, by the indexed script that prints their texts.</summary>
    Dictionary<int, GalleryCutscene> GalleryCutscenes();
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
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// The open game's inspection copy (30 October 2026): the room actions, the doorway overlay and the room sounds read a room's code through a reader of
/// their own, because an archive or disk reader keeps a seek position that live play and the background derivation must not share. Each of them used to
/// rescan the whole game (<see cref="GameFolderScanner.Reopen"/>) on every room change, on the UI thread for two of them: Indy 4's first launch held the
/// window for 85 of its 87 seconds (<c>.verification/indy4-rooms-20261030</c>). The copy is now made once, in the background, when the game opens, and the
/// readers take turns on it.
/// </summary>
public partial class MainWindow
{
    private GameLibrary? _inspectionFor;
    private Task<GameLibrary>? _inspectionCopy;
    private readonly object _inspectionLock = new();

    /// <summary>Starts the inspection copy of a newly opened game. A folder game (no shared archive reader) inspects itself.</summary>
    private void StartInspectionCopy(GameLibrary game)
    {
        _inspectionFor = game;
        _inspectionCopy = game.V4Archive is null && game.V4Disks is null ? Task.FromResult(game) : Task.Run(() => GameFolderScanner.Reopen(game));
    }

    /// <summary>
    /// Takes a turn on the inspection copy of <paramref name="game"/> (any thread): <paramref name="source"/> is the copy to read through until the turn
    /// is disposed. A game that is not the open one gets a copy of its own.
    /// </summary>
    private InspectionTurn UseInspectionCopy(GameLibrary game, out GameLibrary source)
    {
        var copy = ReferenceEquals(_inspectionFor, game) && _inspectionCopy is { } made ? made
            : game.V4Archive is null && game.V4Disks is null ? Task.FromResult(game) : Task.FromResult(GameFolderScanner.Reopen(game));
        source = copy.GetAwaiter().GetResult();
        Monitor.Enter(_inspectionLock);
        return new InspectionTurn(_inspectionLock);
    }

    private readonly struct InspectionTurn(object gate) : IDisposable
    {
        public void Dispose() => Monitor.Exit(gate);
    }
}

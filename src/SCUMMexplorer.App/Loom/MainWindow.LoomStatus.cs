using System.IO;
using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.App;

/// <summary>LOOM's Game Status window and the speaker placement of a direct verb, which both read the exploration session's status.</summary>
public partial class MainWindow
{
    private bool CanEditGameStatus => _session?.GameStatus.CanEdit == true && _replaySequence is null &&
        !_playbackPaused && (_playback is null || !PlaybackHasActivity);

    private GameStatusWindow BuildGameStatusWindow() => new(() => _session, () => CanEditGameStatus, () =>
    {
        if (_currentRoomResource is { } room && _currentRoom is not null && room.ResourceId == _session?.Snapshot.RoomId)
        {
            ApplyRoomState(room, _currentRoomObjectRecords, _objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview(); UpdateActorLayer(); UpdateVerbArea(); UpdatePlaybackControls();
        }
    }, _game, () => IsAdvancedMode, _game?.Id == GameId.LoomVga ? null : LoomEgaStateFlags.Create(() => _replayCatalog?.Replays ?? [])) { Owner = this, Resources = Resources };

    /// <summary>
    /// The derived speaker placements (<see cref="LoomSceneSetupPlanner"/>, the "actor" steps only) for an object verb that prints lines for
    /// an actor who is not in the room. Returns the labelled notes; nothing happens outside LOOM or while the session cannot be edited.
    /// </summary>
    internal List<string> PlaceSpeakersForDirectVerb(GameResource room, int objectId, int verbId)
    {
        var notes = new List<string>();
        if (_game is null || _session is null || _game.Id is not (GameId.Loom or GameId.LoomVga) || room.ResourceId is not int roomId || !_session.GameStatus.CanEdit) return notes;
        _setupPlanner ??= new LoomSceneSetupPlanner(_game);
        var setup = _setupPlanner.DeriveActors(roomId, objectId, verbId);
        foreach (var step in setup.Steps.Where(item => item.Kind == "actor"))
        {
            try { _session.PlaceActorInRoom(step.ObjectId, step.X, step.Y, step.Verb, step.EndBoundary ?? "the game's code"); }
            catch (InvalidOperationException) { continue; }
        }
        notes.AddRange(setup.Notes.Where(note => note.Contains("an actor outside the room is not shown", StringComparison.Ordinal)));
        return notes;
    }
}

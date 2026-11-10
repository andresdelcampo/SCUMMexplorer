using System.IO;
using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.App;

/// <summary>LOOM's Game Status window and the speaker placement of a direct verb, which both read the exploration session's status.</summary>
public partial class MainWindow
{
    private bool CanEditGameStatus => _ctx.Session?.GameStatus.CanEdit == true && _ctx.ReplaySequence is null &&
        !_ctx.PlaybackPaused && (_ctx.Playback is null || !PlaybackHasActivity);

    private GameStatusWindow BuildGameStatusWindow() => new(() => _ctx.Session, () => CanEditGameStatus, () =>
    {
        if (_ctx.CurrentRoomResource is { } room && _ctx.CurrentRoom is not null && room.ResourceId == _ctx.Session?.Snapshot.RoomId)
        {
            ApplyRoomState(room, _ctx.CurrentRoomObjectRecords, _ctx.Objects, visit: false);
            UpdateRoomNavigationActions(room);
            RefreshRoomPreview(); UpdateActorLayer(); UpdateVerbArea(); UpdatePlaybackControls();
        }
    }, _ctx.Game, () => IsAdvancedMode, _ctx.Game?.Id == GameId.LoomVga ? null : LoomEgaStateFlags.Create(() => _ctx.ReplayCatalog?.Replays ?? [])) { Owner = this, Resources = Resources };

    /// <summary>
    /// The derived speaker placements (<see cref="LoomSceneSetupPlanner"/>, the "actor" steps only) for an object verb that prints lines for
    /// an actor who is not in the room. Returns the labelled notes; nothing happens outside LOOM or while the session cannot be edited.
    /// </summary>
    internal List<string> PlaceSpeakersForDirectVerb(GameResource room, int objectId, int verbId)
    {
        var notes = new List<string>();
        if (_ctx.Game is null || _ctx.Session is null || _ctx.Game.Id is not (GameId.Loom or GameId.LoomVga) || room.ResourceId is not int roomId || !_ctx.Session.GameStatus.CanEdit) return notes;
        _ctx.SetupPlanner ??= new LoomSceneSetupPlanner(_ctx.Game);
        var setup = _ctx.SetupPlanner.DeriveActors(roomId, objectId, verbId);
        foreach (var step in setup.Steps.Where(item => item.Kind == "actor"))
        {
            try { _ctx.Session.PlaceActorInRoom(step.ObjectId, step.X, step.Y, step.Verb, step.EndBoundary ?? "the game's code"); }
            catch (InvalidOperationException) { continue; }
        }
        notes.AddRange(setup.Notes.Where(note => note.Contains("an actor outside the room is not shown", StringComparison.Ordinal)));
        return notes;
    }
}

using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private enum RoomCardPreviewStage { Background, InitialState }
    private enum RoomCardLoadPhase { Backgrounds, InitialStates, Completed, Cancelled }
    private readonly Dictionary<GameResource, RoomCardPreviewStage> _roomCardStages = [];
    private Task? _roomCardLoadTask;
    private RoomCardLoadPhase _roomCardLoadPhase;
    private bool _roomCardCacheCommitted;
    private TaskCompletionSource<bool> _roomCardBackgroundsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task StartRoomBackgroundFollowupsAsync(GameLibrary game, Task<bool> backgroundsReady,
        bool useCache, int generation, CancellationToken token)
    {
        if (!await backgroundsReady || !IsCurrentRoomCardLoad(game, generation, token) || Dispatcher.HasShutdownStarted) return;
        // Existing scene scans can use several workers. Queue them after the cheap artwork pass.
        var previous = _roomCardsMayUseCache;
        _roomCardsMayUseCache = useCache;
        try
        {
            CurrentModule.StartBackgroundDerivation(game);
            StartPlayCutsceneDerivation(game);
            UpdatePlaybackControls();
        }
        finally { _roomCardsMayUseCache = previous; }
    }

    private bool IsCurrentRoomCardLoad(GameLibrary game, int generation, CancellationToken token) =>
        !token.IsCancellationRequested && generation == _roomCardGeneration && ReferenceEquals(_game, game);

    private static bool UsesInitializedRoomCards(GameLibrary game) =>
        game is { Id: GameId.Loom, Version: ScummVersion.V3 };

    private async Task PrepareRoomArtworkAsync(GameLibrary game, CancellationToken token)
    {
        if (game is not { Id: GameId.Loom, Version: ScummVersion.V3 } || _sceneBatches is not null) return;
        ScummSceneDrawBatches batches;
        try { batches = await DrawBatchesAsync(game).WaitAsync(token); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { batches = ScummSceneDrawBatches.Empty; }
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_game, game)) throw new OperationCanceledException();
        _sceneBatches = batches;
    }

    private async Task<bool> PublishRoomCardAsync(GameLibrary game, GameResource resource,
        BitmapSource? bitmap, RoomCardPreviewStage stage, string? failure, int generation, CancellationToken token)
    {
        var current = false;
        if (Dispatcher.HasShutdownStarted || token.IsCancellationRequested) return false;
        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsCurrentRoomCardLoad(game, generation, token)) return;
            current = true;
            // Only LOOM EGA needs native detail filling in the selector. Other games keep
            // their plain backgrounds while the same native work/cache prepares room opening.
            if (stage == RoomCardPreviewStage.InitialState && !UsesInitializedRoomCards(game)) return;
            if (bitmap is not null) SetRoomCardThumbnail(resource, bitmap, stage);
            else SetRoomCardFailure(resource, failure ?? "Room preview unavailable.");
        }, DispatcherPriority.Background, token);
        return current;
    }

    /// <summary>Decode stored artwork only: no object visibility, room entry or actor execution.</summary>
    private static BitmapSource RenderRoomCardBackground(GameLibrary game, GameResource resource, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RoomImage room;
        if (game.V4ArchiveFor(resource) is { } archive)
            room = archive.DecodeRoom(resource.ResourceId!.Value);
        else
        {
            var bytes = File.ReadAllBytes(resource.FilePath);
            room = game.Version switch
            {
                ScummVersion.V1 when game.V1Game is { } variant => new ScummV1RoomDecoder(variant).Decode(bytes),
                ScummVersion.V2 => new ScummV2RoomDecoder().Decode(bytes),
                _ => new ScummV3EgaRoomDecoder().Decode(bytes)
            };
        }
        token.ThrowIfCancellationRequested();
        return CreateRoomCardBitmap(room, room.Pixels.ToArray(), 0, room.Palette);
    }

    private async Task LoadRoomCardThumbnailsAsync(GameLibrary game, IReadOnlyList<GameResource> rooms,
        ScummIndexedObjectSnapshot? indexedSnapshot, IReadOnlyDictionary<int, byte> indexedStates,
        int generation, CancellationToken cancellationToken)
    {
        // OpenGameFolder's cache flag is scoped to its synchronous call; capture it before yielding.
        var useCache = _roomCardsMayUseCache;
        if (!IsCurrentRoomCardLoad(game, generation, cancellationToken)) return;
        var backgroundsReady = _roomCardBackgroundsReady;
        using var cancelled = cancellationToken.Register(() => backgroundsReady.TrySetResult(false));
        _roomCardLoadPhase = RoomCardLoadPhase.Backgrounds;
        _roomCardCacheCommitted = false;
        try
        {
            // Every room gets its cheap background before the first native initialization starts.
            foreach (var resource in rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BitmapSource? bitmap = null;
                string? failure = null;
                try { bitmap = await Task.Run(() => RenderRoomCardBackground(game, resource, cancellationToken), cancellationToken); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failure = ex.Message; }
                if (!await PublishRoomCardAsync(game, resource, bitmap, RoomCardPreviewStage.Background,
                        failure, generation, cancellationToken)) return;
            }

            if (!IsCurrentRoomCardLoad(game, generation, cancellationToken)) return;
            _roomCardLoadPhase = RoomCardLoadPhase.InitialStates;
            backgroundsReady.TrySetResult(true);
            var cached = useCache
                ? await Task.Run(() => RoomThumbnailCache.TryLoad(game.FolderPath,
                    rooms.Select(r => r.ResourceId ?? -1), game.Language), cancellationToken)
                : null;
            if (cached is not null)
            {
                foreach (var resource in rooms)
                    if (resource.ResourceId is int id && cached.TryGetValue(id, out var bitmap) &&
                        !await PublishRoomCardAsync(game, resource, bitmap, RoomCardPreviewStage.InitialState,
                            null, generation, cancellationToken)) return;
                if (!IsCurrentRoomCardLoad(game, generation, cancellationToken)) return;
                _roomCardCacheCommitted = true;
            }

            // Cache pixels accelerate display; native states are still prepared for opening rooms.
            var batches = ScummSceneDrawBatches.Empty;
            if (game is { Id: GameId.Loom, Version: ScummVersion.V3 } && indexedSnapshot is not null)
            {
                try { batches = await DrawBatchesAsync(game).WaitAsync(cancellationToken); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { batches = ScummSceneDrawBatches.Empty; }
            }
            var completed = new Dictionary<int, BitmapSource>();
            var failed = false;
            foreach (var resource in rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BitmapSource? bitmap = null;
                string? failure = null;
                try
                {
                    bitmap = await Task.Run(() => RenderRoomCardThumbnail(game, resource,
                        indexedSnapshot, indexedStates, batches, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failure = ex.Message; }
                if (!await PublishRoomCardAsync(game, resource, bitmap, RoomCardPreviewStage.InitialState,
                        failure, generation, cancellationToken)) return;
                if (bitmap is not null && resource.ResourceId is int id) completed[id] = bitmap;
                else failed = true;
            }

            if (!IsCurrentRoomCardLoad(game, generation, cancellationToken)) return;
            if (_roomCardLoadPhase == RoomCardLoadPhase.InitialStates && !failed &&
                completed.Count == rooms.Count && completed.Count > 0 && cached is null)
            {
                var saved = await Task.Run(() => RoomThumbnailCache.SaveCompleted(game.FolderPath,
                    completed, game.Language, cancellationToken), cancellationToken);
                if (!IsCurrentRoomCardLoad(game, generation, cancellationToken)) return;
                _roomCardCacheCommitted = saved;
            }
            _roomCardLoadPhase = RoomCardLoadPhase.Completed;
        }
        catch (OperationCanceledException) { }
        catch (Exception) when (Dispatcher.HasShutdownStarted) { }
        finally
        {
            if (generation == _roomCardGeneration && ReferenceEquals(_game, game) && cancellationToken.IsCancellationRequested)
            {
                _roomCardLoadPhase = RoomCardLoadPhase.Cancelled;
                backgroundsReady.TrySetResult(false);
            }
        }
    }
}

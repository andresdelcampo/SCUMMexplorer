using System.IO;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>Where a room preview's data came from; the preview's later steps (objects, note, doorways) differ by it.</summary>
internal enum RoomSourceKind { CdArchive, BrowsedV4, OlderV1V2, OldV3Lfl }

/// <summary>What a <see cref="RoomSource"/> read for one room: the background and the data the preview builds the room's objects from.</summary>
internal sealed record RoomLoad(RoomSourceKind Kind, RoomImage Room, byte[] Bytes, ScummWalkBoxSet? WalkBoxes, LoomCdScene? CdScene,
    ScummRoomScene? BrowsedScene, BrowsedObjects? BrowsedObjects, ScummRoomData? CdRoom, IBrowsableArchive? BrowseArchive);

public partial class MainWindow
{
    /// <summary>
    /// How one format hands over a room for the preview. The sources are tried in order (<see cref="RoomSourceList"/>); a game or format with another way to read its
    /// rooms adds its own source, and the preview itself (<see cref="RoomLoad"/> onwards) does not test for the game.
    /// </summary>
    private abstract class RoomSource
    {
        public abstract RoomSourceKind Kind { get; }

        /// <summary>True when this source reads the game's room.</summary>
        public abstract bool Reads(MainWindow w, GameLibrary game, GameResource resource);

        /// <summary>Reads the room; null when <paramref name="cancelled"/> turned true meanwhile (the preview then stops without showing anything).</summary>
        public abstract Task<RoomLoad?> LoadAsync(MainWindow w, GameLibrary game, GameResource resource, Func<bool> cancelled, CancellationToken token);

        /// <summary>First visit of a browsed old-bundle game (R-5.3): the entry hooks and scripts run in a fresh generic session; null where that cannot run.</summary>
        protected static async Task<ScummRoomScene?> FirstVisitAsync(GameLibrary game, GameResource resource, CancellationToken token) =>
            ScummProfile.For(game).HasFirstVisitScenes && resource.ResourceId is int roomId
                ? await Task.Run(() => ReadFirstVisit(game, roomId), token) : null;
    }

    /// <summary>The sources in the order they claim a room; the first that <see cref="RoomSource.Reads"/> it loads it.</summary>
    private static readonly RoomSource[] RoomSourceList = [new CdRoomSource(), new BrowsedV4RoomSource(), new OlderRoomSource(), new OldV3RoomSource()];

    private static RoomSource? RoomSourceFor(MainWindow w, GameLibrary game, GameResource resource) =>
        RoomSourceList.FirstOrDefault(source => source.Reads(w, game, resource));

    /// <summary>A browsed v4 game: the room comes out of the disk that holds it (the plain background, stored objects, or a first visit).</summary>
    private sealed class BrowsedV4RoomSource : RoomSource
    {
        public override RoomSourceKind Kind => RoomSourceKind.BrowsedV4;
        public override bool Reads(MainWindow w, GameLibrary game, GameResource resource) => game.V4Disks is not null && game.V4ArchiveFor(resource) is not null;

        public override async Task<RoomLoad?> LoadAsync(MainWindow w, GameLibrary game, GameResource resource, Func<bool> cancelled, CancellationToken token)
        {
            var archive = game.V4ArchiveFor(resource)!;
            await w.PrepareRoomArtworkAsync(game, token);
            if (cancelled()) return null;
            var browsedScene = await FirstVisitAsync(game, resource, token);
            if (cancelled()) return null;
            w._browsedScene = browsedScene;
            var room = browsedScene?.Room ?? await Task.Run(() => archive.DecodeRoom(resource.ResourceId!.Value), token);
            BrowsedObjects? browsedObjects = null;
            if (browsedScene is null)
            {
                try { browsedObjects = await Task.Run(() => archive.ReadBrowsedObjects(resource.ResourceId!.Value), token); }
                catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or ArgumentOutOfRangeException or IndexOutOfRangeException) { }
            }
            if (cancelled()) return null;
            return new(Kind, room, [], archive.ReadWalkBoxes(resource.ResourceId!.Value), null, browsedScene, browsedObjects, null, archive);
        }
    }

    /// <summary>The v1 and v2 games (Maniac Mansion, Zak McKracken): the XOR-encoded room file read with the older decoder.</summary>
    private sealed class OlderRoomSource : RoomSource
    {
        public override RoomSourceKind Kind => RoomSourceKind.OlderV1V2;
        public override bool Reads(MainWindow w, GameLibrary game, GameResource resource) => game.Version is ScummVersion.V1 or ScummVersion.V2;

        public override async Task<RoomLoad?> LoadAsync(MainWindow w, GameLibrary game, GameResource resource, Func<bool> cancelled, CancellationToken token)
        {
            var bytes = await File.ReadAllBytesAsync(resource.FilePath, token);
            await w.PrepareRoomArtworkAsync(game, token);
            if (cancelled()) return null;
            var browsedScene = await FirstVisitAsync(game, resource, token);
            if (cancelled()) return null;
            w._browsedScene = browsedScene;
            var room = browsedScene?.Room ?? DecodeOlderRoom(game, bytes);
            if (cancelled()) return null;
            return new(Kind, room, bytes, TryReadOlderWalkBoxes(bytes), null, browsedScene, null, null, null);
        }
    }

    /// <summary>A v3 game with one LFL file per room (LOOM EGA, Indy 3 EGA, other browsed games).</summary>
    private sealed class OldV3RoomSource : RoomSource
    {
        public override RoomSourceKind Kind => RoomSourceKind.OldV3Lfl;

        public override bool Reads(MainWindow w, GameLibrary game, GameResource resource) =>
            game is { Version: ScummVersion.V3, Id: GameId.Loom or GameId.IndianaJones3 or GameId.Unknown } && w._roomDecoder.CanDecode(resource.FilePath, 3);

        public override async Task<RoomLoad?> LoadAsync(MainWindow w, GameLibrary game, GameResource resource, Func<bool> cancelled, CancellationToken token)
        {
            var bytes = await File.ReadAllBytesAsync(resource.FilePath, token);
            await w.PrepareRoomArtworkAsync(game, token);
            if (cancelled()) return null;
            var browsedScene = await FirstVisitAsync(game, resource, token);
            if (cancelled()) return null;
            w._browsedScene = browsedScene;
            var room = browsedScene?.Room ?? DecodeBrowsedOldV3Room(game, w._roomDecoder.Decode(bytes), bytes);
            if (cancelled()) return null;
            return new(Kind, room, bytes, TryReadWalkBoxes(bytes), null, browsedScene, null, null, null);
        }
    }
}

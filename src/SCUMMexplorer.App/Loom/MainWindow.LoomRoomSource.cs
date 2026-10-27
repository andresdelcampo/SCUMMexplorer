using System.IO;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    /// <summary>LOOM CD: the rooms come out of DISK01.LEC (no per-room file); with no live session the room is shown as its entry script leaves it (first visit).</summary>
    private sealed class CdRoomSource : RoomSource
    {
        public override RoomSourceKind Kind => RoomSourceKind.CdArchive;
        public override bool Reads(MainWindow w, GameLibrary game, GameResource resource) => game is { Id: GameId.LoomVga, V4Archive: not null };

        public override async Task<RoomLoad?> LoadAsync(MainWindow w, GameLibrary game, GameResource resource, Func<bool> cancelled, CancellationToken token)
        {
            await w.PrepareRoomArtworkAsync(game, token);
            if (cancelled()) return null;
            LoomCdScene? cdScene = null;
            if (w._session is null)
            {
                try { cdScene = await Task.Run(() => LoomCdRoomScene.FirstVisit(game, resource.ResourceId!.Value), token); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException) { }
            }
            var browsedScene = await FirstVisitAsync(game, resource, token);
            if (cancelled()) return null;
            w._browsedScene = browsedScene;
            var room = browsedScene?.Room ?? cdScene?.Room ?? await Task.Run(() => game.V4Archive!.DecodeRoom(resource.ResourceId!.Value), token);
            var cdRoom = w._session is not null && w._cdData is not null ? await Task.Run(() => w._cdData.ReadRoom(resource.ResourceId!.Value), token) : null;
            if (cancelled()) return null;
            w._cdScene = cdScene;
            return new(Kind, room, [], cdScene?.WalkBoxes ?? cdRoom?.WalkBoxes, cdScene, browsedScene, null, cdRoom, null);
        }
    }
}

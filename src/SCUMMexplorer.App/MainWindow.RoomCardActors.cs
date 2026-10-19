using System.IO;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    // Room cards use detached first-visit state. The selected room's live actor overlay is
    // separate from its background control and must never replace this complete image.
    private static BitmapSource RenderLoomEgaRoomCard(GameLibrary game, GameResource resource,
        RoomImage room, byte[] bytes, ScummIndexedObjectSnapshot indexedSnapshot,
        IReadOnlyDictionary<int, byte> indexedStates, ScummSceneDrawBatches sceneBatches,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = new ScummRoomObjectDirectory().Decode(bytes);
        var objects = new ScummRoomObjects().Decode(bytes);
        var session = new LoomExplorationSession(game, indexedSnapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = session.VisitRoom(resource);
        var initialized = CreateSessionRoomSelection(resource, room, records, objects,
            snapshot, indexedStates, sceneBatches);
        var pixels = initialized.Selection.Compose(room);
        AddRoomCardActors(pixels, room, resource.ResourceId!.Value, session.Actors,
            initialized.Selection, session.CurrentWalkBoxes, session.ObjectClasses,
            session.ReadCostume, cancellationToken);
        return CreateRoomCardBitmap(room, pixels, session.CameraX ?? 0,
            DisplayPalette(room, session.ShadowPalette));
    }

    private static BitmapSource RenderLoomCdRoomCard(GameLibrary game, int roomId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var scene = LoomCdRoomScene.FirstVisit(game, roomId);
            var pixels = scene.Compose();
            AddRoomCardActors(pixels, scene.Room, roomId, scene.Actors, scene.Selection,
                scene.WalkBoxes, scene.Snapshot.ObjectClasses, scene.Costumes, cancellationToken);
            return CreateRoomCardBitmap(scene.Room, pixels, scene.CameraX,
                scene.ShadowPalette is { } shadow ? DisplayPalette(scene.Room, shadow) : scene.Room.Palette);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException)
        {
            // Keep the existing CD first-visit failure policy: its own plain room picture.
            cancellationToken.ThrowIfCancellationRequested();
            var room = game.V4Archive!.DecodeRoom(roomId);
            return CreateRoomCardBitmap(room, room.Pixels.ToArray(), 0);
        }
    }

    private static void AddRoomCardActors(byte[] pixels, RoomImage room, int roomId,
        IReadOnlyList<ScummActorSnapshot> actors, RoomObjectSelection selection,
        ScummWalkBoxSet? boxes, IReadOnlyDictionary<int, uint> classes,
        Func<int, ScummCostume?> costumes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScummCostume? ReadCostume(int id)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return costumes(id); }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException) { return null; }
        }
        var layer = ScummActorRenderer.Render(actors, roomId, room.Width, room.Height, ReadCostume,
            selection.ComposeDepthMask(room), boxes, classes,
            room.ExtraDepthMasks.Select(plane => plane.Pixels.ToArray()).ToArray());
        for (var i = 0; i < pixels.Length; i++)
        {
            if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            // Palette index 255 is an opaque native VGA colour as well as the old sentinel.
            if (layer.IsDrawn(i)) pixels[i] = layer.Pixels[i];
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}

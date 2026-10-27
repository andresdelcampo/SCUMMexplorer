using System.IO;
using System.Windows.Controls;
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

    private static bool UsesInitializedRoomCards(GameLibrary game) =>
        game is { Id: GameId.Loom, Version: ScummVersion.V3 };

    /// <summary>LOOM's room cards: the CD game's rendered scene, and the EGA game's initial state with its actors.</summary>
    private static BitmapSource? TryRenderLoomRoomCard(GameLibrary game, GameResource roomResource, ScummIndexedObjectSnapshot? indexedSnapshot,
        IReadOnlyDictionary<int, byte> indexedStates, ScummSceneDrawBatches sceneBatches, CancellationToken cancellationToken)
    {
        if (game is { Id: GameId.LoomVga, V4Archive: not null })
            return RenderLoomCdRoomCard(game, roomResource.ResourceId!.Value, cancellationToken);
        if (!UsesInitializedRoomCards(game) || indexedSnapshot is null) return null;
        var bytes = File.ReadAllBytes(roomResource.FilePath);
        var room = new ScummV3EgaRoomDecoder().Decode(bytes);
        return RenderLoomEgaRoomCard(game, roomResource, room, bytes, indexedSnapshot, indexedStates, sceneBatches, cancellationToken);
    }

    /// <summary>LOOM CD: the disc's audio track as a tree group, every stretch a script asks for with sayVoice.</summary>
    private TreeViewItem? CdCueTreeGroup(GameLibrary game)
    {
        if (game.Id != GameId.LoomVga || game.V4Archive is not { Disc: not null }) return null;
        // The disc's audio track: every stretch a script asks for with sayVoice. Long ones and those without a line are the music and narration.
        _cdCues = LoomCdCueIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value));
        var cueNode = new TreeViewItem { Header = $"CD audio  ({_cdCues.Count})", IsExpanded = false };
        for (var i = 0; i < _cdCues.Count; i++)
        {
            var cue = _cdCues[i];
            var kind = cue.IsMusicOrNarration ? "music / narration" : "voice";
            var resource = new GameResource($"CD audio {i + 1:000}", ResourceKind.Sound, game.FolderPath, CdCueIdBase + i,
                $"{kind}, {cue.Cue.Seconds:0.#} s at disc second {cue.Cue.DiscSeconds:0.#}; first asked for by {cue.Source}.", 1, null);
            _soundLines[resource] = cue.Text;
            var snippet = cue.Text.Length > 40 ? cue.Text[..40] + "…" : cue.Text;
            cueNode.Items.Add(new TreeViewItem
            {
                Header = $"{(cue.IsMusicOrNarration ? "♪ " : "")}{resource.Name} · {cue.Cue.Seconds:0.#} s{(snippet.Length > 0 ? " · " + snippet : "")}",
                ToolTip = resource.Description, Tag = resource
            });
        }
        return cueNode;
    }
}

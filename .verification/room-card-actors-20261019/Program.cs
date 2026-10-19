using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    private const BindingFlags Methods = BindingFlags.NonPublic | BindingFlags.Static;
    private static object Call(string name, params object?[] args) =>
        typeof(MainWindow).GetMethod(name, Methods)!.Invoke(null, args)!;
    private static void Require(bool value, string text)
    { if (!value) throw new InvalidOperationException(text); }

    [STAThread]
    private static void Main()
    {
        var output = Path.GetFullPath(".verification/room-card-actors-20261019/out");
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "cache"));
        var ega = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\loom ega mt32 en");
        var indexed = new LflObjectStateReader().ReadSnapshot(Path.Combine(ega.FolderPath, "00.LFL"));
        var originalStates = indexed.States.ToDictionary(p => p.Key, p => p.Value);
        var originalOwners = indexed.Owners.ToDictionary(p => p.Key, p => p.Value);
        var batches = ScummSceneDrawBatches.Build(ega);
        foreach (var id in new[] { 2, 4 })
        {
            var resource = ega.Rooms.Single(r => r.ResourceId == id);
            var bytes = File.ReadAllBytes(resource.FilePath);
            var room = new ScummV3EgaRoomDecoder().Decode(bytes);
            var baseline = new LoomExplorationSession(ega, indexed);
            var visited = baseline.VisitRoom(resource);
            var records = new ScummRoomObjectDirectory().Decode(bytes);
            var images = new ScummRoomObjects().Decode(bytes);
            var initialized = Call("CreateSessionRoomSelection", resource, room, records, images,
                visited, indexed.States, batches, true);
            var selection = (RoomObjectSelection)initialized.GetType().GetField("Item1")!.GetValue(initialized)!;
            var layer = ScummActorRenderer.Render(baseline.Actors, id, room.Width, room.Height, baseline.ReadCostume,
                selection.ComposeDepthMask(room), baseline.CurrentWalkBoxes, baseline.ObjectClasses,
                room.ExtraDepthMasks.Select(p => p.Pixels.ToArray()).ToArray());
            var picture = (BitmapSource)Call("RenderLoomEgaRoomCard", ega, resource, room, bytes,
                indexed, indexed.States, batches, CancellationToken.None);
            Check($"ega-{id:00}", picture, room, selection.Compose(room), layer, baseline.CameraX ?? 0,
                (IReadOnlyList<uint>)Call("DisplayPalette", room, baseline.ShadowPalette), output);
        }
        Require(indexed.States.OrderBy(p => p.Key).SequenceEqual(originalStates.OrderBy(p => p.Key)), "EGA source states changed");
        Require(indexed.Owners.OrderBy(p => p.Key).SequenceEqual(originalOwners.OrderBy(p => p.Key)), "EGA source owners changed");

        var cd = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\loom vga en");
        foreach (var id in new[] { 2, 4 })
        {
            var scene = LoomCdRoomScene.FirstVisit(cd, id);
            var layer = ScummActorRenderer.Render(scene.Actors, id, scene.Room.Width, scene.Room.Height, scene.Costumes,
                scene.Selection.ComposeDepthMask(scene.Room), scene.WalkBoxes, scene.Snapshot.ObjectClasses,
                scene.Room.ExtraDepthMasks.Select(p => p.Pixels.ToArray()).ToArray());
            var picture = (BitmapSource)Call("RenderLoomCdRoomCard", cd, id, CancellationToken.None);
            Check($"cd-{id:00}", picture, scene.Room, scene.Compose(), layer, scene.CameraX,
                scene.ShadowPalette is { } shadow ? (IReadOnlyList<uint>)Call("DisplayPalette", scene.Room, shadow) : scene.Room.Palette, output);
        }
        // Synthetic missing scene metadata: FirstVisit refuses, but the supplied archive's own plain picture is still readable.
        var noScene = cd with { Resources = cd.Resources.Where(r => r.Kind != ResourceKind.Room || r.ResourceId != 4).ToArray() };
        var fallback = (BitmapSource)Call("RenderLoomCdRoomCard", noScene, 4, CancellationToken.None);
        var plain = cd.V4Archive!.DecodeRoom(4);
        var expected = (BitmapSource)Call("CreateRoomCardBitmap", plain, plain.Pixels.ToArray(), 0, null);
        Require(Pixels(fallback).SequenceEqual(Pixels(expected)), "CD failure fallback changed");
        Require(fallback.Palette.Colors.SequenceEqual(expected.Palette.Colors), "CD fallback palette changed");
        foreach (var name in new[] { "RenderLoomEgaRoomCard", "RenderLoomCdRoomCard" })
        {
            try
            {
                if (name.EndsWith("EgaRoomCard"))
                {
                    var resource = ega.Rooms.Single(r => r.ResourceId == 2);
                    var bytes = File.ReadAllBytes(resource.FilePath);
                    Call(name, ega, resource, new ScummV3EgaRoomDecoder().Decode(bytes), bytes,
                        indexed, indexed.States, batches, new CancellationToken(true));
                }
                else Call(name, cd, 2, new CancellationToken(true));
                throw new InvalidOperationException("Canceled helper continued");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException) { }
        }
        Console.WriteLine("PASS four native actor compositions, source-state preservation, CD plain fallback and both cancellation guards; no window constructed");
    }

    private static byte[] Pixels(BitmapSource picture)
    {
        var result = new byte[picture.PixelWidth * picture.PixelHeight];
        picture.CopyPixels(result, picture.PixelWidth, 0);
        return result;
    }

    private static void Check(string name, BitmapSource picture, RoomImage room, byte[] background,
        ScummActorLayer layer, int cameraX, IReadOnlyList<uint> palette, string output)
    {
        var width = Math.Min(320, room.Width);
        var left = Math.Clamp(cameraX - width / 2, 0, room.Width - width);
        Require(picture.IsFrozen && picture.Format == PixelFormats.Indexed8, $"{name} bitmap is not frozen Indexed8");
        Require(picture.PixelWidth == width && picture.PixelHeight == room.Height, $"{name} crop dimensions changed");
        Require(picture.Palette.Colors.SequenceEqual(palette.Select(c => Color.FromArgb((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c))), $"{name} palette changed");
        var pixels = Pixels(picture);
        var actorPixels = 0; var changes = 0; var opaque255 = 0; var scenery = 0;
        for (var y = 0; y < room.Height; y++)
        for (var x = 0; x < width; x++)
        {
            var full = y * room.Width + left + x;
            var expected = layer.IsDrawn(full) ? layer.Pixels[full] : background[full];
            Require(pixels[y * width + x] == expected, $"{name} unexpected pixel at {x},{y}");
            if (layer.IsDrawn(full))
            { actorPixels++; if (expected != background[full]) changes++; if (expected == 255) opaque255++; }
            else scenery++;
        }
        Require(actorPixels > 0 && changes > 0, $"{name} did not establish visible native actors in the crop");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(picture));
        using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        Console.WriteLine($"PASS {name}: actorPixels={actorPixels} changed={changes} opaque255={opaque255} untouchedScenery={scenery} cameraLeft={left} size={width}x{room.Height}");
    }
}

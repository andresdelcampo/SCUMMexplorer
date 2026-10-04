using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var game = new GameFolderScanner().Scan(args[0]);
        var resource = game.Rooms.Single(r => r.ResourceId == 20);
        var bytes = File.ReadAllBytes(resource.FilePath);
        var room = new LoomEgaRoomDecoder().Decode(bytes);
        var records = new LoomRoomObjectDirectory().Decode(bytes);
        var images = new LoomRoomObjects().Decode(bytes);
        void WriteImage(string name, int width, int height, byte[] pixels)
        {
            var rgb = new byte[pixels.Length*4];
            for(int i=0;i<pixels.Length;i++) {uint c=room.Palette[Math.Min(pixels[i],(byte)15)]; rgb[i*4]=(byte)c;rgb[i*4+1]=(byte)(c>>8);rgb[i*4+2]=(byte)(c>>16);rgb[i*4+3]=255;}
            var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,rgb,width*4);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(args[1],name+".png"));encoder.Save(stream);
        }
        WriteImage("background",room.Width,room.Height,room.Pixels.ToArray());
        foreach(var picture in images) {WriteImage($"object{picture.Id}-{picture.X}-{picture.Y}",picture.Width,picture.Height,picture.Pixels); Console.WriteLine($"object{picture.Id} {picture.X},{picture.Y} {picture.Width}x{picture.Height}");}
        var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
        var batches = LoomSceneDrawBatches.Build(game);
        foreach (int id in new[] {4,20,43,86})
        {
            var raw=File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==id).FilePath);
            var bg=new LoomEgaRoomDecoder().Decode(raw);
            foreach(var obj in new LoomRoomObjects().Decode(raw))
            {
                var before=new HashSet<byte>();
                for(int y=0;y<obj.Height;y++) for(int x=0;x<obj.Width;x++) before.Add(bg.Pixels.Span[(obj.Y+y)*bg.Width+obj.X+x]);
                var after=obj.Pixels.ToHashSet();
                Console.WriteLine($"METRIC room{id} obj{obj.Id} before={string.Join(',',before.Order())} after={string.Join(',',after.Order())}");
            }
        }
        var session = new LoomExplorationSession(game, index);
        session.VisitRoom(resource);
        var costumes = new Dictionary<int, LoomCostume?>();
        LoomCostume? Costume(int id)
        {
            if (!costumes.TryGetValue(id, out var costume)) costumes[id] = costume = LoomCostume.Load(game, id);
            return costume;
        }
        var frames = new List<object>();
        for (int frame = 0; frame <= 200; frame++)
        {
            if (frame is 0 or 1 or 10 or 50 or 200)
            {
                var snapshot = session.Snapshot;
                var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, records, room, index.States, snapshot.DrawOrder);
                if (!snapshot.IsFaithful)
                {
                    selection.ExcludeInterfaceImages(batches.InterfaceImages.Keys);
                    selection.FillGaps(batches.ForRoom(20), seed:20);
                    if (!images.Any(selection.IsActive)) selection.ShowUnresolvedArtwork(batches.ForRoom(20), seed:20, snapshot.StatesSetByCode, batches.EntryStatesForRoom(20));
                }
                var layer = LoomActorRenderer.Render(session.Actors, 20, room.Width, room.Height, Costume,
                    selection.ComposeDepthMask(room), session.CurrentWalkBoxes, session.ObjectClasses);
                frames.Add(new {frame, sourceActors=session.Actors, layer.Actors, layer.Skipped, snapshot.ObjectStates, snapshot.ObjectClasses});
                var pixels = selection.Compose(room);
                for(int i=0;i<pixels.Length;i++) if(layer.Pixels[i] != LoomActorRenderer.Transparent) pixels[i]=layer.Pixels[i];
                var rgb = new byte[pixels.Length*4];
                for(int i=0;i<pixels.Length;i++) {uint c=room.Palette[Math.Min(pixels[i],(byte)15)]; rgb[i*4]=(byte)c;rgb[i*4+1]=(byte)(c>>8);rgb[i*4+2]=(byte)(c>>16);rgb[i*4+3]=255;}
                var bitmap=BitmapSource.Create(room.Width,room.Height,96,96,PixelFormats.Bgra32,null,rgb,room.Width*4);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(args[1],$"room20-frame{frame:000}.png"));encoder.Save(stream);
                Console.WriteLine($"frame {frame}: {string.Join(';',layer.Actors.Select(a=>$"actor{a.Number} costume{a.Costume} pixels{a.Pixels} bounds{a.Bounds}"))}");
            }
            session.AdvanceFrame();
        }
        File.WriteAllText(Path.Combine(args[1],"evidence.json"),JsonSerializer.Serialize(new {rooms=game.Rooms.Select(r=>r.ResourceId),records,frames,trace=session.SessionTrace,uncertainties=session.Snapshot.Uncertainties},new JsonSerializerOptions{WriteIndented=true}));
    }
}

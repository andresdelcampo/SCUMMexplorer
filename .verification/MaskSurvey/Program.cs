// Throwaway survey: per game folder, the scan hash and, per room, a hash of the decoded pixels + depth masks. Used to prove existing games did not change
// and to count which rooms of the browsed games carry masks. Usage: MaskSurvey <games root> [...]
using System.Security.Cryptography;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

static string Hash(byte[] b) => Convert.ToHexString(SHA1.HashData(b))[..10];
static RoomImage Decode(GameLibrary game, GameResource r)
{
    if (game.V4ArchiveFor(r) is { } a) return a.DecodeRoom(r.ResourceId!.Value);
    var bytes = File.ReadAllBytes(r.FilePath);
    return game.Version switch
    {
        ScummVersion.V1 when game.V1Game is { } v1 => new ScummV1RoomDecoder(v1).Decode(bytes),
        ScummVersion.V2 => new ScummV2RoomDecoder().Decode(bytes),
        _ => new ScummV3EgaRoomDecoder().Decode(bytes)
    };
}

static void Png(string path, int w, int h, byte[] rgb)
{
    using var ms = new MemoryStream();
    var raw = new byte[(w * 3 + 1) * h];
    for (var y = 0; y < h; y++) Array.Copy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);
    using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) z.Write(raw);
    using var f = File.Create(path);
    void Chunk(string t, byte[] d)
    {
        var len = BitConverter.GetBytes(d.Length); Array.Reverse(len); f.Write(len);
        var td = System.Text.Encoding.ASCII.GetBytes(t).Concat(d).ToArray(); f.Write(td);
        var crc = System.IO.Hashing.Crc32.Hash(td); Array.Reverse(crc); f.Write(crc);
    }
    f.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    var hd = new byte[13]; 
    BitConverter.GetBytes(w).Reverse().ToArray().CopyTo(hd, 0); BitConverter.GetBytes(h).Reverse().ToArray().CopyTo(hd, 4); hd[8] = 8; hd[9] = 2;
    Chunk("IHDR", hd); Chunk("IDAT", ms.ToArray()); Chunk("IEND", []);
}

if (args.Length >= 2 && args[0] == "--objects")
{
    // --objects <game folder> [room]: per room the v4 object records and decoded images (counts, a few names).
    var g = new GameFolderScanner().Scan(args[1]);
    int tot = 0, totImg = 0, roomsWith = 0;
    foreach (var r in g.Rooms)
    {
        if (args.Length > 2 && r.ResourceId != int.Parse(args[2])) continue;
        if (g.V4ArchiveFor(r) is not { } a) continue;
        try
        {
            var b = a.ReadBrowsedObjects(r.ResourceId!.Value); var o = b.Records; var im = b.Images;
            tot += o.Count; totImg += im.Count; if (o.Count > 0) roomsWith++;
            if (args.Length > 2) foreach (var x in o) Console.WriteLine($"  obj {x.Id} {x.X},{x.Y} {x.Width}x{x.Height} '{b.Names.GetValueOrDefault(x.Id)}' images={im.Count(i => i.Id == x.Id)}");
        }
        catch (Exception ex) { Console.WriteLine($"room {r.ResourceId}: {ex.GetType().Name} {ex.Message}"); }
    }
    Console.WriteLine($"objects={tot} images={totImg} roomsWithObjects={roomsWith}");
    return;
}

if (args.Length == 4 && args[0] == "--png")
{
    // --png <game folder> <room> <out.png>: the room with the first depth mask tinted red.
    var g = new GameFolderScanner().Scan(args[1]);
    var res = g.Rooms.First(r => r.ResourceId == int.Parse(args[2]));
    var img = Decode(g, res);
    var rgb = new byte[img.Width * img.Height * 3];
    for (var i = 0; i < img.Width * img.Height; i++)
    {
        var c = img.Palette[img.Pixels.Span[i]];
        byte r8 = (byte)(c >> 16), g8 = (byte)(c >> 8), b8 = (byte)c;
        if (img.DepthMask is { } m && m.Pixels.Span[i] != 0) { r8 = (byte)((r8 + 255) / 2); g8 /= 2; b8 = (byte)((b8 + 255) / 2); }
        rgb[i * 3] = r8; rgb[i * 3 + 1] = g8; rgb[i * 3 + 2] = b8;
    }
    Png(args[3], img.Width, img.Height, rgb);
    Console.WriteLine($"{img.Width}x{img.Height} mask={(img.DepthMask is null ? "none" : "yes")} extra={img.ExtraDepthMasks.Count}");
    return;
}

foreach (var root in args)
foreach (var dir in Directory.EnumerateDirectories(root))
{
    GameLibrary game;
    try { game = new GameFolderScanner().Scan(dir); } catch (Exception ex) { Console.WriteLine($"{Path.GetFileName(dir)}: scan failed ({ex.GetType().Name})"); continue; }
    var scan = string.Join("|", game.Resources.Select(r => $"{r.Name}/{r.Kind}/{r.ResourceId}/{r.ContainerFileNumber}/{r.Offset}"));
    int rooms = 0, withMask = 0, extra = 0, failed = 0, nonEmpty = 0;
    var all = new System.Text.StringBuilder();
    var heights = new SortedDictionary<int, int>();
    foreach (var r in game.Rooms)
    {
        try
        {
            RoomImage image;
            if (game.V4ArchiveFor(r) is { } a) image = a.DecodeRoom(r.ResourceId!.Value);
            else
            {
                var bytes = File.ReadAllBytes(r.FilePath);
                image = game.Version switch
                {
                    ScummVersion.V1 when game.V1Game is { } v1 => new ScummV1RoomDecoder(v1).Decode(bytes),
                    ScummVersion.V2 => new ScummV2RoomDecoder().Decode(bytes),
                    _ => new ScummV3EgaRoomDecoder().Decode(bytes)
                };
            }
            rooms++;
            heights[image.Height] = heights.GetValueOrDefault(image.Height) + 1;
            if (image.DepthMask is { } m) { withMask++; if (m.Pixels.Span.IndexOfAnyExcept((byte)0) >= 0) nonEmpty++; extra += image.ExtraDepthMasks.Count; all.Append(Hash(m.Pixels.ToArray())); }
            all.Append(Hash(image.Pixels.ToArray()));
        }
        catch (Exception) { failed++; }
    }
    Console.WriteLine($"{Path.GetFileName(dir),-32} v{(int)game.Version} id={game.Id} scan={Hash(System.Text.Encoding.UTF8.GetBytes(game.Title + game.Version + game.Id + scan))} rooms={rooms} failed={failed} masked={withMask} nonEmpty={nonEmpty} extraPlanes={extra} heights={string.Join(',', heights.Select(h => $"{h.Key}x{h.Value}"))} pix={Hash(System.Text.Encoding.UTF8.GetBytes(all.ToString()))}");
}

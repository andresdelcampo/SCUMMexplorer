using System.IO.Compression;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Rendering;

// Steps costume animations with LoomCostume (ScummVM ClassicCostumeLoader) and writes one PNG strip per
// (costume, frame, direction): step 0 is the frame just started, each later cell one increaseAnims call.
// A cross marks the actor's point. Usage: CostumeAnimRender <game folder> <output folder> <costume> <steps> <frame>...
var folder = args[0];
var output = args[1];
var id = int.Parse(args[2]);
var steps = int.Parse(args[3]);
var frames = args.Skip(4).Select(int.Parse).ToArray();
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var costume = LoomCostume.Load(game, id) ?? throw new InvalidOperationException($"Costume {id} is not indexed.");
var log = new List<string> { $"costume {id}: animations 0..{costume.AnimationLimit}, mirror {costume.Mirror}, palette colour {costume.PaletteColor}" };
string[] names = ["left", "right", "down", "up"];
const int CellW = 96, CellH = 112, OriginX = 48, OriginY = 104, Scale = 3;
foreach (var frame in frames)
for (var direction = 0; direction < 4; direction++)
{
    var state = new LoomCostumeAnimationState();
    if (!costume.DecodeAnimation(state, frame, direction))
    {
        log.Add($"frame {frame} {names[direction]}: animation {direction + frame * 4} is empty or beyond the costume");
        continue;
    }
    var canvas = new byte[CellW * steps * CellH];
    Array.Fill(canvas, (byte)255);
    for (var step = 0; step < steps; step++)
    {
        if (step > 0) costume.IncreaseAnimations(state);
        var cels = costume.Draw(state, direction);
        var cell = new byte[CellW * CellH];
        Array.Fill(cell, (byte)255);
        costume.Compose(cels, cell, CellW, CellH, OriginX, OriginY);
        for (var d = -2; d <= 2; d++) { cell[OriginY * CellW + OriginX + d] = 12; cell[(OriginY + d) * CellW + OriginX] = 12; }
        for (var y = 0; y < CellH; y++) Array.Copy(cell, y * CellW, canvas, y * CellW * steps + step * CellW, CellW);
        log.Add($"frame {frame} {names[direction]} step {step}: positions [{string.Join(",", state.Positions.Select((p, l) => p < 0 ? "-" : $"{l}:{p}").Where(s => s != "-"))}] " +
                $"stopped 0x{state.Stopped:X4} cels [{string.Join(" ", cels.Select(c => $"L{c.Limb}({c.Left},{c.Top} {c.Width}x{c.Height})"))}]");
    }
    WritePng(Path.Combine(output, $"costume{id:000}-frame{frame}-{names[direction]}.png"), canvas, CellW * steps, CellH, Scale);
}
File.WriteAllLines(Path.Combine(output, $"costume{id:000}.txt"), log);
Console.WriteLine(string.Join(Environment.NewLine, log.Take(3)));

static void WritePng(string path, byte[] indexed, int width, int height, int scale)
{
    uint[] ega = [0x000000, 0x0000AA, 0x00AA00, 0x00AAAA, 0xAA0000, 0xAA00AA, 0xAA5500, 0xAAAAAA,
                  0x555555, 0x5555FF, 0x55FF55, 0x55FFFF, 0xFF5555, 0xFF55FF, 0xFFFF55, 0xFFFFFF];
    int w = width * scale, h = height * scale;
    var raw = new byte[h * (w * 3 + 1)];
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        var index = indexed[y / scale * width + x / scale];
        var rgb = index == 255 ? 0x6E7F8Eu : ega[index & 15];
        var o = y * (w * 3 + 1) + 1 + x * 3;
        raw[o] = (byte)(rgb >> 16); raw[o + 1] = (byte)(rgb >> 8); raw[o + 2] = (byte)rgb;
    }
    using var file = File.Create(path);
    file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    var header = new byte[13];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, w);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), h);
    header[8] = 8; header[9] = 2;
    Chunk(file, "IHDR", header);
    using var compressed = new MemoryStream();
    using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true)) z.Write(raw);
    Chunk(file, "IDAT", compressed.ToArray());
    Chunk(file, "IEND", []);
}

static void Chunk(Stream stream, string type, byte[] data)
{
    var length = new byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
    stream.Write(length);
    var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
    stream.Write(body);
    var crc = new byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(body));
    stream.Write(crc);
}

static uint Crc(byte[] data)
{
    var crc = 0xFFFFFFFFu;
    foreach (var b in data)
    {
        crc ^= b;
        for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
    }
    return ~crc;
}

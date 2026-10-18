using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var root = args.ElementAtOrDefault(0) ?? @"D:\Juegos\Lucas";
var output = args.ElementAtOrDefault(1) ?? ".verification/maniacenhanced-fidelity-20261018/output";
Directory.CreateDirectory(output);
var report = new StringBuilder("# Native-lighting check\n\nSource-derived state previews, not earned routes or original-game comparison.\n\n");
foreach (var fixture in new[] { "mmansionE ega es", "mmansion ega en", "zak ega en", "zakE ega en" })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, fixture));
    var data = ScummDataSources.For(game);
    if (args.Contains("torch"))
    {
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        if (fixture.StartsWith("mmansion")) ManiacFirstVisit.Prepare(game, session);
        var torchRoom = fixture.StartsWith("mmansion") ? 7 : 28;
        var torchId = fixture.StartsWith("mmansion") ? 84 : 69;
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == torchRoom));
        session.Settle();
        session.ActivateObjectVerb(torchId, 14); // Actual pickup code; disclosed direct exploration action.
        session.ActivateObjectVerb(torchId, 4); // Actual Switch on code; does not bypass battery/state tests.
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == (fixture.StartsWith("mmansion") ? 5 : 36)));
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        session.PointAtScreen((179, 79));
        var screen = new ScummPlayScreen(game, session, indexed.States);
        var nativeScene = screen.Scene()!;
        var capture = session.CaptureRoom();
        report.AppendLine($"{fixture}: torch owner {session.Snapshot.ObjectOwners.GetValueOrDefault(torchId)}, lights {capture.Lights}, strips {capture.Flashlight}, pointer {capture.RoomPointer}, view {capture.ViewLeft}; shown {nativeScene.Compose().Count(p => p != 0)} pixels.");
        Save(Path.Combine(output, fixture + "-torch.bmp"), nativeScene.Room.Width, nativeScene.Room.Height, nativeScene.ComposeWithActors(), nativeScene.DisplayPalette());
        continue;
    }
    if (args.Contains("inspect"))
    {
        var torchRoom = fixture.StartsWith("mmansion") ? 7 : 28;
        var torchId = fixture.StartsWith("mmansion") ? 84 : 69;
        var record = data.ReadRoom(torchRoom).Objects.First(r => r.Id == torchId);
        Console.WriteLine(fixture);
        foreach (var verb in data.ReadObjectVerbs(torchRoom, [record])[torchId].DistinctBy(v => v.ScriptOffset))
            Console.WriteLine(data.ReadObjectVerb(torchRoom, record, verb.VerbId, out _).Format());
        continue;
    }
    report.AppendLine($"## {fixture}");
    report.AppendLine(data.GlobalScript(fixture.StartsWith("mmansion") ? 55 : 6)?.Format());
    foreach (var resource in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue))
        foreach (var op in data.GlobalScript(resource.ResourceId!.Value)?.Instructions.Where(i => i.Operation == "lights") ?? [])
            report.AppendLine($"- Script {resource.ResourceId} +{op.Offset:X4}: {op.Operation} {op.Operands}");
    foreach (var resource in game.Rooms.Where(r => r.ResourceId.HasValue))
    {
        var id = resource.ResourceId!.Value;
        var entry = data.RoomEntryScript(id);
        foreach (var op in entry?.Instructions.Where(i => i.Operation == "lights") ?? [])
            report.AppendLine($"- Room {id} entry +{op.Offset:X4}: {op.Operation} {op.Operands}");
        var records = data.ReadRoom(id).Objects;
        foreach (var name in data.ReadRoom(id).ObjectNames)
            if (Encoding.ASCII.GetString(name.Value).Contains("flash", StringComparison.OrdinalIgnoreCase) || Encoding.ASCII.GetString(name.Value).Contains("linterna", StringComparison.OrdinalIgnoreCase))
                report.AppendLine($"FLASHLIGHT SOURCE Room {id} object {name.Key}: {Encoding.ASCII.GetString(name.Value)}");
        var verbs = data.ReadObjectVerbs(id, records);
        foreach (var record in records.DistinctBy(r => r.Id))
            if (verbs.TryGetValue(record.Id, out var entries))
                foreach (var verb in entries.DistinctBy(v => v.ScriptOffset))
                    foreach (var op in data.ReadObjectVerb(id, record, verb.VerbId, out _).Instructions.Where(i => i.Operation == "lights"))
                        report.AppendLine($"- Room {id} object {record.Id} verb {verb.VerbId} +{op.Offset:X4}: {op.Operation} {op.Operands}");
    }
    var roomId = fixture.StartsWith("mmansion") ? 5 : 36;
    var scene = fixture.StartsWith("mmansion") ? ManiacFirstVisit.Run(game, roomId) : ScummRoomFirstVisit.Run(game, roomId);
    var raw = scene.Selection.Compose(scene.Room);
    var native = scene.Compose();
    var actors = scene.ActorLayer();
    report.AppendLine($"CHECK Room {roomId}, lights {scene.State.Lights}, strips {scene.State.Flashlight}, raw nonzero {raw.Count(v => v != 0)}, shown nonzero {native.Count(v => v != 0)}, actors {actors.Actors.Count}, colors {string.Join(",", Enumerable.Range(0, actors.Pixels.Length).Where(actors.IsDrawn).Select(i => actors.Pixels[i]).Distinct().Order())}.");
    Save(Path.Combine(output, fixture + "-source.bmp"), scene.Room.Width, scene.Room.Height, raw, scene.Room.Palette);
    Save(Path.Combine(output, fixture + "-native.bmp"), scene.Room.Width, scene.Room.Height, scene.ComposeWithActors(), scene.DisplayPalette());
}
File.WriteAllText(Path.Combine(output, "lighting.md"), report.ToString());
Console.WriteLine(report);

static void Save(string path, int width, int height, byte[] pixels, IReadOnlyList<uint> palette)
{
    using var writer = new BinaryWriter(File.Create(path));
    var pitch = (width * 3 + 3) & ~3;
    writer.Write((ushort)0x4D42); writer.Write(54 + pitch * height); writer.Write(0); writer.Write(54);
    writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((ushort)1); writer.Write((ushort)24);
    writer.Write(0); writer.Write(pitch * height); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
    for (var y = height - 1; y >= 0; y--)
    {
        for (var x = 0; x < width; x++)
        {
            var color = palette[pixels[y * width + x] % palette.Count];
            writer.Write((byte)color); writer.Write((byte)(color >> 8)); writer.Write((byte)(color >> 16));
        }
        for (var i = width * 3; i < pitch; i++) writer.Write((byte)0);
    }
}

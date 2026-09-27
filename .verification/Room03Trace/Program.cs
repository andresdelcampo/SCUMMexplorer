using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

var gameRoot = Path.GetFullPath(args.Length > 0 ? args[0] : "Games/loom");
var files = Directory.GetFiles(gameRoot, "*.LFL");
string RoomPath(int id) => Path.Combine(gameRoot, $"{id:00}.LFL");
var roomReader = new LoomRoomObjectDirectory();
var verbReader = new LoomObjectVerbReader();
var scriptReader = new LoomScriptReader();
var index = new LflIndexReader().Read(RoomPath(0));
var globalScriptCount = new LflIndexReader().ReadDeclaredScriptCount(RoomPath(0));
Console.WriteLine($"GLOBAL_SCRIPT_COUNT {globalScriptCount}");
var scripts = index.Where(item => item.Kind == ResourceKind.Script).Select(item =>
    new GameResource($"Script {item.ResourceId:00}", ResourceKind.Script,
        Path.Combine(gameRoot, $"{item.ContainerFileNumber:00}.LFL"), item.ResourceId,
        ContainerFileNumber: item.ContainerFileNumber, Offset: item.Offset))
    .ToDictionary(resource => resource.ResourceId!.Value);

foreach (var roomId in new[] { 3, 17 })
{
    var path = RoomPath(roomId);
    var bytes = File.ReadAllBytes(path);
    var records = roomReader.Decode(bytes);
    Console.WriteLine($"ROOM {roomId:00} file={Path.GetFileName(path)} records={records.Count}");
    foreach (var record in records)
        Console.WriteLine($"OBJ id={record.Id} local={record.LocalIndex} rect={record.X},{record.Y},{record.Width},{record.Height} parentLocal={record.ParentLocalIndex} req={record.ParentRequiredState} walk={record.WalkX},{record.WalkY} dir={record.Direction} code=0x{record.CodeOffset:X4} image={(record.ImageOffset is int img ? $"0x{img:X4}" : "none")}");

    if (roomId == 17)
    {
        var byLocal = records.ToDictionary(item => item.LocalIndex);
        foreach (var image in records.Where(item => item.ImageOffset.HasValue && item.ParentLocalIndex != 0))
        {
            var parent = byLocal.GetValueOrDefault(image.ParentLocalIndex);
            if (parent is not null && parent.ImageOffset is null)
                Console.WriteLine($"IMAGE_WITH_CODE_ONLY_PARENT child={image.Id} parent={parent.Id} parentLocal={parent.LocalIndex} req={image.ParentRequiredState}");
        }
    }

    if (roomId == 17) continue;
    var room = new GameResource("Room 03", ResourceKind.Room, path, roomId,
        ContainerFileNumber: 3);
    var entry = scriptReader.DecodeRoomEntry(room);
    var exit = scriptReader.DecodeRoomExit(room);
    foreach (var script in new[] { entry, exit }.Where(item => item is not null))
    {
        Console.WriteLine($"ROOM_SCRIPT {(script!.Context ?? "unknown")} offset=0x{script.ResourceOffset:X4}");
        foreach (var op in script.Instructions)
            Console.WriteLine($"  +0x{op.Offset:X4} {op.Bytes} {op.Operation} {op.Operands}");
    }

    var allVerbs = verbReader.ReadAll(bytes, records);
    foreach (var record in records)
    {
        foreach (var verb in allVerbs[record.Id])
        {
            var decoded = verbReader.Decode(room, record, verb.VerbId);
            Console.WriteLine($"VERB object={record.Id} rect={record.X},{record.Y},{record.Width},{record.Height} verb=0x{verb.VerbId:X2} offset=0x{verb.ScriptOffset:X4} fallback={verb.IsFallback}");
            foreach (var op in decoded.Instructions)
                Console.WriteLine($"  +0x{op.Offset:X4} {op.Bytes} {op.Operation} {op.Operands}");
            foreach (var op in decoded.Instructions.Where(op => op.Operation == "startScript"))
            {
                var token = op.Operands.Split(',', StringSplitOptions.TrimEntries)[0];
                if (!token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(token.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var childId))
                    continue;
                if (!scripts.TryGetValue(childId, out var child))
                {
                    var local = scriptReader.DecodeRoomLocalScript(room, childId, globalScriptCount);
                    if (local is null) continue;
                    Console.WriteLine($"  LOCAL script={childId:00} resource=room+0x{local.ResourceOffset:X4} bytes={local.CodeLength} issues={local.Issues?.Count ?? 0}");
                    foreach (var localOp in local.Instructions)
                        Console.WriteLine($"    +0x{localOp.Offset:X4} {localOp.Bytes} {localOp.Operation} {localOp.Operands}");
                    continue;
                }
                var childCode = scriptReader.Decode(child);
                foreach (var childOp in childCode.Instructions.Where(item => item.Operation == "loadRoom"))
                    Console.WriteLine($"  CHILD script={childId:00} resource=0x{child.Offset:X4} +0x{childOp.Offset:X4} {childOp.Bytes} loadRoom {childOp.Operands}");
            }
        }
    }
}

foreach (var resource in scripts.Values.OrderBy(item => item.ResourceId))
{
    try
    {
        var script = scriptReader.Decode(resource);
        if (resource.ResourceId == 42)
        {
            foreach (var instruction in script.Instructions)
                Console.WriteLine($"SCRIPT42 +0x{instruction.Offset:X4} {instruction.Operation} {instruction.Operands}");
            Console.WriteLine("SCRIPT42_GATE evidence: bit[0xD7A] set at +0x0058; cleared at +0x0091; first operation beyond the bounded executor after the +0x0021 condition is getRandomNr at +0x0026.");
        }
        foreach (var instruction in script.Instructions.Where(item =>
                     item.Operands.Contains("bit[0xD7A]", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"BIT_D7A script={resource.ResourceId:00} +0x{instruction.Offset:X4} {instruction.Operation} {instruction.Operands}");
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
}

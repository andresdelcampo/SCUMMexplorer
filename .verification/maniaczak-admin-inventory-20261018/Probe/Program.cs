using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

foreach (var folder in new[] { "mmansion ega en", "mmansionE ega es", "zak ega en", "zakE ega en" })
{
    var game = new GameFolderScanner().Scan(Path.Combine(@"D:\Juegos\Lucas", folder));
    var data = ScummDataSources.For(game);
    var session = ScummGameSession.BootForPlay(game, out _);
    if (folder.StartsWith("mmansion")) ManiacCutsceneSetup.PrepareIfNeeded(game, session);
    for (var frame = 0; frame < 2500 && !session.CaptureInventoryStatus().CanEdit; frame++) session.AdvanceFrame();
    var view = session.CaptureInventoryStatus();
    Console.WriteLine($"CATALOG {folder}: {view.Catalog.Count} items, {view.Catalog.Count(x => !string.IsNullOrWhiteSpace(x.Name))} named; actors {string.Join(',', view.Characters.Select(x => x.ActorId))}");
    foreach (var item in view.Catalog) Console.WriteLine($"ITEM {item.ObjectId} room {item.SourceRoomId}: {item.Name}: {item.Evidence}");
    foreach (var note in session.InventoryCatalogNotes) Console.WriteLine("AUDIT " + note);
    if (!folder.StartsWith("mmansion")) continue;
    var menu = data.ReadRoom(45);
    var tables = data.ReadObjectVerbs(45, menu.Objects);
    foreach (var item in menu.Objects)
        foreach (var verb in tables.GetValueOrDefault(item.Id) ?? [])
            foreach (var instruction in data.ReadObjectVerb(45, item, verb.VerbId, out _).Instructions)
                if (instruction.Operation is "move" or "startScript" or "chainScript")
                    Console.WriteLine($"MENU {item.Id} +{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}");
    if (data.GlobalScript(114) is {} helper)
        foreach (var instruction in helper.Instructions)
            Console.WriteLine($"114 +{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}");
}

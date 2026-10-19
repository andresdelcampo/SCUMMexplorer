using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\zak ega en");
foreach (var id in new[] { 13, 32 })
{
    var resource = game.Resources.Single(r => r.Kind == ResourceKind.Costume && r.ResourceId == id);
    var bytes = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
    var offset = resource.Offset!.Value;
    var summary = new ScummV3CostumeReader().Decode(bytes, offset);
    var renderer = new ScummCostumeRenderer();
    var original = ScummV1ActorColors.For(game)!.ColorOf(id);
    var alternate = original with { Suit = 0 };
    var count = 0; var pixels = 0; var changed = 0;
    foreach (var slot in summary.AnimationSlots.Where(s => s.HasData))
    {
        var first = renderer.DecodePose(bytes, offset, slot.Index, game.V1Game, original);
        var second = renderer.DecodePose(bytes, offset, slot.Index, game.V1Game, alternate);
        if (first is null || second is null) continue;
        count++; var a = first.Pixels.ToArray(); var b = second.Pixels.ToArray(); pixels += a.Length;
        for (var n = 0; n < a.Length; n++) if (first.Palette[a[n]] != second.Palette[b[n]]) changed++;
    }
    Console.WriteLine($"costume{id}: {count} native poses, {pixels} stored pixels, {changed} changed expanded pixels for suit{original.Suit}->0");
}

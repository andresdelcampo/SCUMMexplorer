using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args.FirstOrDefault() ?? @"D:\Juegos\Lucas\zak ega en");
var watch = Stopwatch.StartNew();
var colors = ScummV1ActorColors.Read(game);
Console.WriteLine($"READ milliseconds={watch.ElapsedMilliseconds} settings={colors.Settings.Count}");
var requested = args.Skip(1).Select(int.Parse).ToArray();
var ids = requested.Length == 0 ? new[] { 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32 } : requested;
var all = new List<ScummV1CostumeColorContext>();
foreach (var costume in ids)
{
    watch.Restart();
    var contexts = colors.ContextsOf(costume);
    var cold = watch.ElapsedMilliseconds;
    watch.Restart();
    var warm = colors.ContextsOf(costume);
    if (!contexts.SequenceEqual(warm)) throw new InvalidOperationException("Warm contexts changed");
    Console.WriteLine($"COSTUME {costume} contexts={contexts.Count} cold-ms={cold} warm-ms={watch.ElapsedMilliseconds} global={colors.ColorOf(costume).Basis}");
    foreach (var context in contexts)
        Console.WriteLine($"CONTEXT costume={context.Costume} suit={context.Suit} actor={context.Actor} basis={context.Basis} estimate={context.IsEstimate} preview={context.PreviewRoom}: {context.Evidence}");
    all.AddRange(contexts);
}
Console.WriteLine($"TOTAL contexts={all.Count} distinct-pairs={all.DistinctBy(c => (c.Costume, c.Skin, c.Suit)).Count()} global-estimates={Enumerable.Range(1, 35).Count(i => colors.ColorOf(i).Basis == ScummV1ColorBasis.Estimate)}");

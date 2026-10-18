using System.Security.Cryptography;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var scanner = new GameFolderScanner();
var matches = new List<GameLibrary>();
foreach (var directory in Directory.EnumerateDirectories(args[0]).Order())
{
    if (!File.Exists(Path.Combine(directory, "00.LFL"))) continue;
    var game = scanner.Scan(directory);
    if (GameSupport.FamilyOf(game) is not (GameFamily.ManiacClassic or GameFamily.ManiacEnhanced)) continue;
    Console.WriteLine($"INVENTORY {Path.GetFileName(directory)}: {GameSupport.FamilyOf(game)}, {game.Version}, {game.Edition}, rooms={game.Rooms.Count()}");
    if (GameSupport.FamilyOf(game) == GameFamily.ManiacEnhanced) matches.Add(game);
}
Console.WriteLine($"Enhanced editions available={matches.Count}; classic is a different family.");
var fixture = matches.Single(g => Path.GetFullPath(g.FolderPath) == Path.GetFullPath(args[1]));
var data = ScummDataSources.For(fixture);
Console.WriteLine($"DATA globals={data.GlobalScriptIds().Count}; room45 fingerprint={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.Rooms.Single(r=>r.ResourceId==45).FilePath)))}");
var executable = File.ReadAllBytes(Path.Combine(fixture.FolderPath,"MANIAC.EXE"));
var native = ScummV2NativeFont.TryLoad(fixture.FolderPath) ?? throw new InvalidDataException("Native font did not validate.");
Console.WriteLine($"EXE {Convert.ToHexString(SHA256.HashData(executable))}; font {Convert.ToHexString(SHA256.HashData(native))}");
var session = ScummGameSession.BootForPlay(fixture,out _);
var charset = session.PlayCharset()!;
foreach (var (label,font) in new (string,IScummTextCharset)[] { ("previous built-in",new ScummV2Charset(2,"es")),("current session",charset) })
{
    var mismatches = new List<int>();
    for (var glyph=0;glyph<128;glyph++)
    {
        var pixels=new byte[64];
        font.DrawPlainLine(pixels,8,0,[(byte)glyph],0,0,1);
        var expected=Enumerable.Range(0,64).Select(p=>(byte)(glyph!='@' && (native[glyph*8+p/8]&(128>>(p%8)))!=0?1:0)).ToArray();
        if (!pixels.SequenceEqual(expected)) mismatches.Add(glyph);
    }
    Console.WriteLine($"RENDER {label}: native text parity={128-mismatches.Count}/128 (slot64 marker suppressed); differing slots=[{string.Join(',',mismatches)}]");
}
var readable=ManiacPreviewText.For(fixture);
Console.WriteLine($"TEXT readable label: {readable?.Invoke("Qu] es el tel]fono")} (raw script bytes retained)");
var planner=ManiacCutsceneSetup.Planner(fixture);
Console.WriteLine($"PREVIEW own-data scan={planner.Triggers.Count}/{planner.Groups.Count}; word15={planner.Verbs.WordFor(15)}; no whole-census rerun");
Console.WriteLine("Alternate enhanced edition comparisons: BLOCKED, no second local enhanced copy.");

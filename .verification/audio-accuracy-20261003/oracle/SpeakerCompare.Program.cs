using System.Buffers.Binary;
using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
var oracle = args[1]; var tmp = args[2];
var psi = new ProcessStartInfo(oracle, "tables x") { RedirectStandardOutput = true };
var lines = Process.Start(psi)!.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
Console.WriteLine($"table: oracle {lines.Length} ours {LoomPcSpeakerModulationTable.Values.Length} equal={lines.SequenceEqual(LoomPcSpeakerModulationTable.Values.Select(v => (int)v))}");
var sounds = new GameFolderScanner().Scan(args[0]).Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue).OrderBy(r => r.ResourceId).ToList();
foreach (var r in sounds)
{
    byte[] wave;
    try { wave = new LoomPcSpeakerAudio().RenderWave(r); } catch { continue; }
    var file = File.ReadAllBytes(r.FilePath); for (int i = 0; i < file.Length; i++) file[i] ^= 0xFF;
    int off = r.Offset!.Value; int size = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(off));
    var inPath = Path.Combine(tmp, "in.bin"); var outPath = Path.Combine(tmp, "out.raw");
    File.WriteAllBytes(inPath, file.AsSpan(off, size).ToArray());
    var p = Process.Start(new ProcessStartInfo(oracle, $"\"{inPath}\" \"{outPath}\"") { UseShellExecute = false })!; p.WaitForExit();
    var o = File.ReadAllBytes(outPath);
    int ours = (wave.Length - 44) / 2, theirs = o.Length / 2, n = Math.Min(ours, theirs), bad = 0, first = -1, max = 0;
    for (int i = 0; i < n; i++) { int x = BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(44 + 2 * i)); int y = BinaryPrimitives.ReadInt16LittleEndian(o.AsSpan(2 * i)); if (x != y) { bad++; if (first < 0) first = i; max = Math.Max(max, Math.Abs(x - y)); } }
    // Oracle tail beyond our end must be (near) silent.
    int tail = 0; for (int i = n; i < theirs; i++) tail = Math.Max(tail, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(o.AsSpan(2 * i))));
    Console.WriteLine($"{r.ResourceId}: ours={ours} oracle={theirs} mismatches={bad} first={first} maxdiff={max} oracleTailPeak={tail}");
}

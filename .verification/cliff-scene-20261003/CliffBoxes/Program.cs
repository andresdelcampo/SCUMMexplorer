using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var res = game.Rooms.Single(r => r.ResourceId == 34);
var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(res.FilePath));
Console.WriteLine($"boxes {boxes.Count}, matrix entries {boxes.Matrix.Count}, truncated {boxes.MatrixTruncated}, table offset {boxes.TableOffset}");
Console.WriteLine("next box matrix (row = from, column = to; 255 = none):");
Console.WriteLine("     " + string.Join(" ", Enumerable.Range(0, boxes.Count).Select(i => $"{i,3}")));
for (var from = 0; from < boxes.Count; from++)
    Console.WriteLine($"{from,3}: " + string.Join(" ", Enumerable.Range(0, boxes.Count).Select(to => $"{boxes.GetNextBox(from, to),3}")));
foreach (var b in boxes.Boxes)
    Console.WriteLine($"box {b.Index}: x {Math.Min(Math.Min(b.UpperLeft.X, b.UpperRight.X), Math.Min(b.LowerLeft.X, b.LowerRight.X))}..{Math.Max(Math.Max(b.UpperLeft.X, b.UpperRight.X), Math.Max(b.LowerLeft.X, b.LowerRight.X))} y {Math.Min(Math.Min(b.UpperLeft.Y, b.UpperRight.Y), Math.Min(b.LowerLeft.Y, b.LowerRight.Y))}..{Math.Max(Math.Max(b.UpperLeft.Y, b.UpperRight.Y), Math.Max(b.LowerLeft.Y, b.LowerRight.Y))} flags 0x{b.Flags:X2} mask {b.Mask}");

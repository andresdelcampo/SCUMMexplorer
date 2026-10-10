using SCUMMexplorer.Core.Scumm;

// Usage: CdExtract <Loom.cue> <output folder>: writes the data files of the disc image so another program (ScummVM) can read them as plain files.
var disc = DiscImage.Open(args[0]);
Directory.CreateDirectory(args[1]);
foreach (var file in disc.ListFiles())
{
    File.WriteAllBytes(Path.Combine(args[1], file.Name), disc.Read(file));
    Console.WriteLine($"{file.Name} {file.Length}");
}

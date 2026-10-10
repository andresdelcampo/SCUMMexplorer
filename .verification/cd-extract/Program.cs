using SCUMMexplorer.Core.Scumm;

// Usage: CdExtract <Loom.cue> <output folder>: writes the data files of the disc image so another program (ScummVM) can read them as plain files.
var disc = DiscImage.Open(args[0]);
Directory.CreateDirectory(args[1]);
foreach (var file in disc.ListFiles())
{
    File.WriteAllBytes(Path.Combine(args[1], file.Name), disc.Read(file));
    Console.WriteLine($"{file.Name} {file.Length}");
}

// ScummVM plays a CD game's audio from ripped tracks beside the data: the first audio track is track 2 (LOOM CD's scenes also wait on the CD music timer, which only runs while a track plays).
var wave = disc.ReadAudioWave(0, int.MaxValue / DiscImage.AudioSectorBytes);
if (wave.Length > 0) { File.WriteAllBytes(Path.Combine(args[1], "track2.wav"), wave); Console.WriteLine($"track2.wav {wave.Length}"); }

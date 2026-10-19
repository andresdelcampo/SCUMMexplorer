using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args.FirstOrDefault() ?? ".verification/room-preview-cache-20261019/run-private");
        if (args.Length > 1 && args[1] == "read")
        {
            Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(root, "cache"));
            var persisted = RoomThumbnailCache.TryLoad(Path.Combine(root, "source-a"), [1, 2]);
            var pixels = new byte[16];
            persisted?[1].CopyPixels(pixels, 8, 0);
            if (persisted is not { Count: 2 } || pixels[0] != 12 || persisted.Values.Any(b => !b.IsFrozen))
                throw new InvalidOperationException("Cross-process complete batch did not survive");
            Console.WriteLine("PASS complete batch persisted across a separate headless STA process");
            return;
        }
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException("Use a fresh private output directory; no existing data is removed.");
        Directory.CreateDirectory(root);
        var cache = Path.Combine(root, "cache");
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", cache);
        var source = Source(root, "source-a");
        var other = Source(root, "source-b");
        var first = new Dictionary<int, BitmapSource> { [1] = Image(12), [2] = Image(34) };
        var second = new Dictionary<int, BitmapSource> { [1] = Image(56), [2] = Image(78) };
        var checks = 0;
        void Check(bool accepted, string name)
        {
            if (!accepted) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS " + name);
            checks++;
        }
        byte Pixel(IReadOnlyDictionary<int, BitmapSource>? images, int room = 1)
        {
            if (images is null || !images.ContainsKey(room)) return 255;
            var output = new byte[16];
            images[room].CopyPixels(output, 8, 0);
            return output[0];
        }
        string Archive(string folder, string? language = null) =>
            (string)typeof(RoomThumbnailCache).GetMethod("CacheFile", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [folder, language])!;
        void Resave() => Check(RoomThumbnailCache.SaveCompleted(source, first), "refresh complete private cache");

        Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, "headless harness runs in STA");
        Check(RoomThumbnailCache.TryLoad(source, [1, 2]) is null, "cache miss before complete batch");
        Check(RoomThumbnailCache.SaveCompleted(source, first), "complete batch published");
        var loaded = RoomThumbnailCache.TryLoad(source, [2, 1]);
        Check(loaded is { Count: 2 } && loaded.Values.All(b => b.IsFrozen) && Pixel(loaded) == 12 && Pixel(loaded, 2) == 34,
            "completed roundtrip uses sorted complete set and frozen pixels");
        var previous = File.ReadAllBytes(Archive(source));
        Check(!RoomThumbnailCache.SaveCompleted(source, second, cancellationToken: new CancellationToken(true)) &&
            previous.SequenceEqual(File.ReadAllBytes(Archive(source))) && Pixel(RoomThumbnailCache.TryLoad(source, [1, 2])) == 12,
            "pre-cancel preserves prior complete archive byte for byte");
        Check(!RoomThumbnailCache.SaveCompleted(source, new Dictionary<int, BitmapSource>()) &&
            !RoomThumbnailCache.SaveCompleted(source, new Dictionary<int, BitmapSource> { [1] = null! }) &&
            previous.SequenceEqual(File.ReadAllBytes(Archive(source))), "empty and missing bitmap batches preserve prior complete cache");
        Check(RoomThumbnailCache.TryLoad(source, [1, 2, 3]) is null && RoomThumbnailCache.TryLoad(source, [1]) is null,
            "incomplete or different requested room sets refused");

        using (var cancel = new CancellationTokenSource())
        using (var observed = new ManualResetEventSlim())
        using (var watcher = new FileSystemWatcher(cache, "*.tmp") { EnableRaisingEvents = true })
        {
            watcher.Created += (_, _) => { cancel.Cancel(); observed.Set(); };
            var noisy = NoisyImage();
            var replacement = Enumerable.Range(1, 64).ToDictionary(i => i, _ => noisy);
            var saved = RoomThumbnailCache.SaveCompleted(source, replacement, cancellationToken: cancel.Token);
            Check(observed.Wait(TimeSpan.FromSeconds(5)) && !saved && previous.SequenceEqual(File.ReadAllBytes(Archive(source))),
                "cancellation after staging begins preserves prior complete archive");
        }
        Check(!Directory.EnumerateFiles(cache, "*.tmp").Any(), "owned staging file cleaned without directory deletion");
        bool committedWhileLocked;
        using (var blocked = new FileStream(Archive(source), FileMode.Open, FileAccess.Read, FileShare.None))
            committedWhileLocked = RoomThumbnailCache.SaveCompleted(source, second);
        Check(!committedWhileLocked && previous.SequenceEqual(File.ReadAllBytes(Archive(source))) &&
            Pixel(RoomThumbnailCache.TryLoad(source, [1, 2])) == 12, "failed atomic replacement preserves prior valid cache");

        Check(RoomThumbnailCache.TryLoad(source, [1, 2], "ENGLISH") is null &&
            RoomThumbnailCache.SaveCompleted(source, second, "ENGLISH") &&
            Pixel(RoomThumbnailCache.TryLoad(source, [1, 2], "english")) == 56 &&
            Pixel(RoomThumbnailCache.TryLoad(source, [1, 2])) == 12 &&
            RoomThumbnailCache.TryLoad(source, [1, 2], "SPANISH") is null, "selected languages are isolated and case-normalized");
        Check(RoomThumbnailCache.TryLoad(other, [1, 2]) is null && RoomThumbnailCache.SaveCompleted(other, second) &&
            Pixel(RoomThumbnailCache.TryLoad(other, [1, 2])) == 56 && Pixel(RoomThumbnailCache.TryLoad(source, [1, 2])) == 12,
            "source folders with identical metadata remain isolated");

        using (var archive = ZipFile.Open(Archive(source), ZipArchiveMode.Update)) archive.GetEntry("room-2.png")!.Delete();
        Check(RoomThumbnailCache.TryLoad(source, [1, 2]) is null, "missing PNG refuses incomplete stored batch");
        Resave();
        using (var archive = ZipFile.Open(Archive(source), ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("manifest.json")!;
            JsonNode manifest;
            using (var read = entry.Open()) manifest = JsonNode.Parse(read)!;
            entry.Delete();
            manifest["Stamp"] = "old-build-stamp";
            using var write = new StreamWriter(archive.CreateEntry("manifest.json").Open());
            write.Write(manifest.ToJsonString());
        }
        Check(RoomThumbnailCache.TryLoad(source, [1, 2]) is null, "stale build/source stamp refused");
        Resave();
        foreach (var relative in new[] { "01.LFL", "DISK01.LEC", "MONKEY.000", "MONKEY.001", "SPANISH/DISK02.LEC" })
        {
            File.AppendAllText(Path.Combine(source, relative), "changed");
            Check(RoomThumbnailCache.TryLoad(source, [1, 2]) is null, "metadata invalidates " + relative);
            Resave();
        }
        using (var stopReaders = new CancellationTokenSource())
        using (var readerStarted = new ManualResetEventSlim())
        {
            Exception? readerFailure = null;
            var reads = 0;
            var reader = new Thread(() =>
            {
                try
                {
                    while (!stopReaders.IsCancellationRequested)
                    {
                        var batch = RoomThumbnailCache.TryLoad(source, [1, 2]);
                        var pair = (Pixel(batch), Pixel(batch, 2));
                        if (pair is not ((12, 34) or (56, 78))) throw new InvalidOperationException("Reader saw missing or mixed batch");
                        reads++;
                        readerStarted.Set();
                    }
                }
                catch (Exception ex) { readerFailure = ex; readerStarted.Set(); }
            }) { IsBackground = true };
            reader.SetApartmentState(ApartmentState.STA);
            reader.Start();
            Check(readerStarted.Wait(TimeSpan.FromSeconds(5)) && readerFailure is null, "concurrent reader starts on valid complete batch");
            var writes = true;
            for (var write = 0; write < 12; write++) writes &= RoomThumbnailCache.SaveCompleted(source, write % 2 == 0 ? second : first);
            stopReaders.Cancel();
            Check(reader.Join(TimeSpan.FromSeconds(5)) && readerFailure is null && reads > 0 && writes,
                "concurrent reads observe only old or new complete batches");
        }
        var disc = Source(root, "source-disc");
        var cue = Path.Combine(disc, "game.cue");
        File.WriteAllText(cue, "FILE game.bin BINARY");
        File.WriteAllText(Path.Combine(disc, "game.bin"), "private disc fixture");
        Check(RoomThumbnailCache.SaveCompleted(cue, first, "ENGLISH"), "disc path batch published");
        File.AppendAllText(Path.Combine(disc, "game.bin"), "changed");
        Check(RoomThumbnailCache.TryLoad(cue, [1, 2], "ENGLISH") is null, "disc companion data invalidates cue-path cache");
        var outsideImage = Path.Combine(root, "outside-disc.bin");
        File.WriteAllText(outsideImage, "private external disc fixture");
        File.WriteAllText(cue, "FILE \"../outside-disc.bin\" BINARY");
        Check(RoomThumbnailCache.SaveCompleted(cue, first, "ENGLISH"), "shared cue resolver includes external data-image metadata");
        File.AppendAllText(outsideImage, "changed");
        Check(RoomThumbnailCache.TryLoad(cue, [1, 2], "ENGLISH") is null, "external cue data image invalidates cache");
        Console.WriteLine($"PASS total={checks}; private output={root}; no game data, profile or window used");
    }

    private static string Source(string root, string name)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(folder, "SPANISH"));
        foreach (var relative in new[] { "00.LFL", "01.LFL", "DISK01.LEC", "MONKEY.000", "MONKEY.001", "SPANISH/DISK02.LEC" })
            File.WriteAllText(Path.Combine(folder, relative), "private metadata fixture");
        return folder;
    }

    private static BitmapSource Image(byte value)
    {
        var bytes = Enumerable.Repeat(value, 16).ToArray();
        var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, bytes, 8);
        image.Freeze();
        return image;
    }

    private static BitmapSource NoisyImage()
    {
        var bytes = new byte[256 * 256 * 4];
        new Random(13).NextBytes(bytes);
        var image = BitmapSource.Create(256, 256, 96, 96, PixelFormats.Bgra32, null, bytes, 256 * 4);
        image.Freeze();
        return image;
    }
}

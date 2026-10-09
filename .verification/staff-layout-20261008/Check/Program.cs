using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        if (args.Length > 2 && args[2] == "room17")
        {
            var game = new GameFolderScanner().Scan(Path.Combine(args[0], "loom ega mt32 en"));
            var data = ScummDataSources.For(game);
            var lines = new List<string>();
            void Dump(string name, ScummScriptDisassembly? script)
            {
                lines.Add(name);
                foreach (var i in script?.Instructions ?? []) lines.Add($"+{i.Offset:X4} {i.Operation} {i.Operands}");
            }
            Dump("English Roland EGA Room 17 entry", data.RoomEntryScript(17));
            Dump("Room 17 local 201", data.RoomLocalScript(17, 201));
            Dump("Global 17 (ego look/costume)", data.GlobalScript(17));
            File.WriteAllLines(Path.Combine(args[1], "room17-source.txt"), lines);
            foreach (var line in lines) Console.WriteLine(line);
            return 0;
        }
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.InitializeComponent();
        var report = new List<string>();
        var failures = 0;
        void Check(bool ok, string text) { report.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; }
        foreach (var edition in new[] { "loom ega mt32 en", "loom vga en" })
        {
            var game = new GameFolderScanner().Scan(Path.Combine(args[0], edition));
            var cd = game.Id == GameId.LoomVga;
            var prefix = cd ? "vga" : "ega";
            var data = ScummDataSources.For(game);
            var indexed = game.V4Archive?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
            var session = new LoomExplorationSession(game, indexed);
            session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 3), preserveStaffLevel: true);
            for (int f = 0; f < 600 && (!session.GameStatus.CanEdit || f < 40); f++) session.AdvanceFrame();
            session.UpdateStaffPossession(true);
            session.UpdateAvailableNotes(8);
            for (int f = 0; f < 40; f++) session.AdvanceFrame();
            var images = data.ReadObjectImages(1);
            RoomObjectImage? Image(int room, int id) => room == 1 ? images.FirstOrDefault(p => p.Id == id) : null;
            // ScummVM 2.2.0 o5_verbOps first adjusts LOOM v4's eight note verbs.
            // drawVerbBitmap then passes those adjusted y coordinates unchanged.
            int[] sourceY = [175, 175, 174, 172, 171, 169, 168, 173];
            int[] nativeOffsets = [7, 7, 6, 4, 3, 1, 0, 5];
            var referenceVerbs = session.Verbs.Select(v => cd && v.VerbId is >= 0x5A and <= 0x61
                ? v with { Y = v.Y - nativeOffsets[v.VerbId - 0x5A] } : v).ToArray();
            var reference = ScummVerbArea.Compose(referenceVerbs, Image);
            var actual = session.VerbArea();
            Check(actual.Pixels.SequenceEqual(reference.Pixels), prefix + " live staff equals the complete native coordinate path");
            if (cd)
            {
                for (int i = 0; i < sourceY.Length; i++)
                {
                    var item = actual.Items.Single(v => v.VerbId == 0x5A + i);
                    var expected = sourceY[i] - nativeOffsets[i];
                    Check(item.Y == expected, $"VGA verb {item.VerbId:X2}: Script 18 y={sourceY[i]}, native offset={nativeOffsets[i]}, click rectangle y={item.Y} (expected {expected})");
                }
            }
            var palette = cd ? game.V4Archive!.ReadPalette(1) : new ScummV3EgaRoomDecoder().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 1).FilePath)).Palette;
            var messages = session.Messages.OnScreen.Where(m => m.Source.StartsWith("Script 18 +", StringComparison.Ordinal)).ToArray();
            var letters = cd ? ScummV4MessageRenderer.Render(messages, game.V4Archive!.ReadCharset(2)!)
                : ScummV3MessageRenderer.Render(messages, ScummV3Charset.TryLoad(game.FolderPath, 0, out _)!);
            byte[] Crop(ScummVerbAreaPicture area)
            {
                var pixels = new byte[224 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 224; x++)
                {
                    var p = letters.Pixels[(168 + y) * 320 + 16 + x];
                    pixels[y * 224 + x] = p != ScummV3MessageRenderer.Transparent ? p : area.Pixels[(24 + y) * 320 + 16 + x];
                }
                return pixels;
            }
            var sourcePixels = Crop(reference);
            Save(Bitmap(sourcePixels, 224, 32, palette), Path.Combine(args[1], prefix + "-source-score.png"));
            Save(Bitmap(Crop(actual), 224, 32, palette), Path.Combine(args[1], prefix + "-live-score.png"));
            var artworkType = typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomStatusArtwork")!;
            object?[] loadArgs = [game, null];
            var artwork = artworkType.GetMethod("TryLoad")!.Invoke(null, loadArgs)!;
            var notes = (BitmapSource[])artworkType.GetProperty("Notes")!.GetValue(artwork)!;
            var notePixels = new byte[224 * 32 * 4];
            for (int i = 0; i < notes.Length; i++)
            {
                var pixels = new byte[notes[i].PixelWidth * notes[i].PixelHeight * 4];
                notes[i].CopyPixels(pixels, notes[i].PixelWidth * 4, 0);
                for (int y = 0; y < notes[i].PixelHeight; y++)
                    Array.Copy(pixels, y * 24 * 4, notePixels, (y * 224 + 8 + i * 24) * 4, 24 * 4);
            }
            Save(BitmapSource.Create(224, 32, 96, 96, PixelFormats.Bgra32, null, notePixels, 224 * 4), Path.Combine(args[1], prefix + "-status-score.png"));
            if (cd)
            {
                for (int i = 0; i < sourceY.Length; i++)
                {
                    var slot = session.Verbs.Single(v => v.VerbId == 0x5A + i);
                    var head = Image(1, slot.ImageObject!.Value)!;
                    int left = slot.X!.Value / 8 * 8 - 16;
                    int matching = 0, compared = 0;
                    for (int y = 0; y < head.Height / 8 * 8; y++) for (int x = 0; x < head.Width / 8 * 8; x++)
                    {
                        int at = (sourceY[i] - nativeOffsets[i] - 168 + y) * 224 + left + x;
                        // Game Status deliberately presents palette zero as transparent; compare the visible colored sprite pixels.
                        if (at < 0 || at >= sourcePixels.Length || head.Pixels[y * head.Width + x] == 0 || sourcePixels[at] == 0) continue;
                        var color = palette[sourcePixels[at]];
                        int b = at * 4;
                        compared++;
                        if (notePixels[b] == (byte)color && notePixels[b + 1] == (byte)(color >> 8) && notePixels[b + 2] == (byte)(color >> 16) && notePixels[b + 3] == 255) matching++;
                    }
                    Check(compared > 0 && matching == compared, $"VGA Game Status note {i + 1} retains {matching}/{compared} source-positioned opaque pixels");
                }
            }
            report.Add(prefix + " live score SHA256 " + Convert.ToHexString(SHA256.HashData(Crop(actual))));
            report.Add(prefix + " status score SHA256 " + Convert.ToHexString(SHA256.HashData(notePixels)));
            var status = new GameStatusWindow(() => session, () => session.GameStatus.CanEdit, () => { }, game)
                { ShowActivated = false, Left = -2000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual };
            status.Show(); status.Measure(new Size(900, 780)); status.Arrange(new Rect(0, 0, 900, 780)); status.UpdateLayout();
            var shot = new RenderTargetBitmap(900, 780, 96, 96, PixelFormats.Pbgra32);
            shot.Render(status); Save(shot, Path.Combine(args[1], prefix + "-status-window.png"));
            status.Close();
        }
        report.Add($"{report.Count(l => l.StartsWith("PASS "))} passed, {failures} failed; detached sessions, no native game inputs or saved profile edits.");
        File.WriteAllLines(Path.Combine(args[1], "report.txt"), report);
        foreach (var line in report) Console.WriteLine(line);
        return failures == 0 ? 0 : 1;
    }

    static BitmapSource Bitmap(byte[] pixels, int width, int height, IReadOnlyList<uint> palette)
    {
        var bgra = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            uint c = palette[pixels[i]];
            bgra[i * 4] = (byte)c; bgra[i * 4 + 1] = (byte)(c >> 8); bgra[i * 4 + 2] = (byte)(c >> 16); bgra[i * 4 + 3] = 255;
        }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
    }
    static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}

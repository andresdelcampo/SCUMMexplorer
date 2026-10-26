using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class GameFolderScannerTests
{
    [Theory]
    [InlineData("loom ega mt32 en", "English, Roland upgrade", false)]
    [InlineData("loom ega en", "English", false)]
    [InlineData("loom ega it", "Italian", false)]
    public void NamesTheEditionFromTheFilesAlone(string folderName, string edition, bool demo)
    {
        var folder = Path.GetFullPath(Path.Combine(TestGames.Root, folderName));
        if (!File.Exists(Path.Combine(folder, "00.LFL")) && !File.Exists(Path.Combine(folder, "00.lfl"))) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Loom, game.Id);
        Assert.Equal(edition, game.Edition);
        Assert.Equal(demo, game.IsDemo);
    }

    [Theory]
    [InlineData("loom ega en", KnownGame.Loom, GameEvidence.EditionFiles)]
    [InlineData("loom vga en", KnownGame.Loom, GameEvidence.EditionFiles)]
    [InlineData("loom vga en kixx", KnownGame.Loom, GameEvidence.EditionFiles)]
    [InlineData("indy3 ega es", KnownGame.IndianaJones3, GameEvidence.EditionFiles)]
    [InlineData("indy3 vga en", KnownGame.IndianaJones3, GameEvidence.EditionFiles)]
    [InlineData("indy4 vga en", KnownGame.IndianaJones4, GameEvidence.EditionFiles)]
    [InlineData("indy4 talkie vga es", KnownGame.IndianaJones4, GameEvidence.EditionFiles)]
    [InlineData("monkey1 ega en", KnownGame.MonkeyIsland, GameEvidence.EditionFiles)]
    [InlineData("monkey1 vga en", KnownGame.MonkeyIsland, GameEvidence.EditionFiles)]
    [InlineData("monkey1 vga es", KnownGame.MonkeyIsland, GameEvidence.EditionFiles)]
    [InlineData("mi2 sb16 en", KnownGame.MonkeyIsland2, GameEvidence.EditionFiles)]
    [InlineData("mi2 talkie es", KnownGame.MonkeyIsland2, GameEvidence.EditionFiles)]
    [InlineData("mmansion ega en", KnownGame.ManiacMansion, GameEvidence.EditionFiles)]
    [InlineData("zakE ega en", KnownGame.ZakMcKracken, GameEvidence.EditionFiles)]
    public void EveryFormatIsIdentifiedFromItsDataAlone(string folderName, KnownGame expected, GameEvidence evidence)
    {
        var folder = Path.GetFullPath(Path.Combine(TestGames.Root, folderName));
        if (!Directory.Exists(folder)) return;
        var identity = GameIdentifier.Identify(GameFolderScanner.Fingerprint(folder)!);
        Assert.Equal(expected, identity.Game);
        Assert.Equal(evidence, identity.Evidence);
        Assert.Equal(expected, new GameFolderScanner().Scan(folder).Game);
    }

    [Fact]
    public void OnlyLoomCdDataIsClaimedAsLoomCd()
    {
        // The directory sizes of both local dumps of the disc name LOOM; another game on the same archive format does not match.
        Assert.Equal(KnownGame.Loom, GameIdentifier.Identify(new GameFingerprint(new Dictionary<string, Func<byte[]>>(), () => new DirectoryShape(ScummVersion.V4, 78, 165, 637, 30))).Game);
        Assert.Equal(KnownGame.Unknown, GameIdentifier.Identify(new GameFingerprint(new Dictionary<string, Func<byte[]>>(), () => new DirectoryShape(ScummVersion.V4, 85, 120, 400, 60))).Game);
    }

    [Fact]
    public void RecognizesTheLoomDemosWithoutTheirExecutable()
    {
        var source = Path.GetFullPath(Path.Combine(TestGames.Root, "loom demo short ega en"));
        if (!File.Exists(Path.Combine(source, "00.LFL"))) return;
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*.LFL")) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            var game = new GameFolderScanner().Scan(folder);
            Assert.Equal(GameId.Loom, game.Id);
            Assert.True(game.IsDemo);
            Assert.Equal("English, short demo", game.Edition);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void RecognizesLoomWithoutItsExecutableFromTheIndexAndStillSaysNothingAboutAnUnknownIndex()
    {
        // An install without LOOM.EXE (a SCUMMVM-style copy, or the Italian release as found) is told by the data alone.
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(source, "00.LFL"))) return;
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*.LFL")) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            var game = new GameFolderScanner().Scan(folder);
            Assert.Equal(GameId.Loom, game.Id);
            Assert.Equal("LOOM", game.Title);
            Assert.False(game.IsDemo);
            Assert.True(game.Rooms.Count() > 60);
            Assert.Contains("known data", game.DetectionNote);
            Assert.False(string.IsNullOrEmpty(game.Edition));
            // A few of the listed files modified or gone: still the same game by the rest.
            foreach (var name in new[] { "27.LFL", "29.LFL" }) File.WriteAllBytes(Path.Combine(folder, name), [1, 2, 3]);
            File.Delete(Path.Combine(folder, "39.LFL"));
            Assert.Contains("known data", new GameFolderScanner().Scan(folder).DetectionNote);
            // Almost all gone: the known hashes no longer vouch for it, and the shape of the index is the labelled last resort.
            foreach (var file in Directory.EnumerateFiles(folder, "*.LFL").Where(f => !new[] { "00.LFL", "01.LFL", "02.LFL" }.Contains(Path.GetFileName(f)))) File.Delete(file);
            var inferred = new GameFolderScanner().Scan(folder);
            Assert.Equal(GameId.Loom, inferred.Id);
            Assert.Contains("sizes of its resource directories only (inferred)", inferred.DetectionNote);
        }
        finally { Directory.Delete(folder, recursive: true); }
        var tiny = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tiny);
        try
        {
            WriteOldV3Index(Path.Combine(tiny, "00.LFL"));
            File.WriteAllBytes(Path.Combine(tiny, "01.LFL"), []);
            Assert.Equal(GameId.Unknown, new GameFolderScanner().Scan(tiny).Id);
        }
        finally { Directory.Delete(tiny, recursive: true); }
    }

    [Fact]
    public void ListsAnOldV3IndexWithoutNamingTheGameFromAnExecutable()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            // Executables carry no data the explorer reads: a LOOM.EXE beside an unknown index does not name the game.
            File.WriteAllBytes(Path.Combine(folder, "LOOM.EXE"), []);
            WriteOldV3Index(Path.Combine(folder, "00.LFL"));
            File.WriteAllBytes(Path.Combine(folder, "01.LFL"), []);
            File.WriteAllBytes(Path.Combine(folder, "02.LFL"), []);

            var game = new GameFolderScanner().Scan(folder);

            Assert.Equal(KnownGame.Unknown, game.Game);
            Assert.Equal(GameId.Unknown, game.Id);
            Assert.Equal(ScummVersion.V3, game.Version);
            Assert.Equal(new int?[] { 1, 2 }, game.Rooms.Select(room => room.ResourceId));
            Assert.Contains(game.Resources, resource => resource.Name == "Game index" && resource.Kind == ResourceKind.Other);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Costume && resource.ResourceId == 0);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Script && resource.ResourceId == 0);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Sound && resource.ResourceId == 0);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LoomDemoKeepsOnlyResourcesItsFolderContains()
    {
        var folder = Path.GetFullPath(Path.Combine(TestGames.Root, "loom demo short ega en"));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal("LOOM (demo)", game.Title);
        Assert.True(game.IsDemo);
        // The demo's index also lists entries for files it lacks: none of them is offered.
        Assert.All(game.Resources.Where(r => r.Kind is ResourceKind.Costume or ResourceKind.Script or ResourceKind.Sound && r.ContainerFileNumber is not null),
            resource => Assert.True(File.Exists(resource.FilePath)));
        Assert.Contains("index also lists", game.DetectionNote);
    }

    [Fact]
    public void RecognizesIndianaJonesEgaFromItsData()
    {
        var folder = Path.GetFullPath(Path.Combine(TestGames.Root, "indy3 ega en"));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal("Indiana Jones and the Last Crusade", game.Title);
        Assert.Equal(GameId.IndianaJones3, game.Id);
        Assert.Equal(KnownGame.IndianaJones3, game.Game);
        Assert.Equal(ScummVersion.V3, game.Version);
    }

    [Fact]
    public void LeavesVersionUnknownWhenLoomEvidenceIsIncomplete()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "01.LFL"), []);
            var game = new GameFolderScanner().Scan(folder);
            Assert.Equal(ScummVersion.Unknown, game.Version);
            Assert.Contains("not confirmed", game.DetectionNote);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void WriteOldV3Index(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x0100); // old v3 00.LFL signature
        writer.Write((ushort)0); // global object count
        writer.Write((byte)3); // rooms
        writer.Write(new byte[] { 0, 0, 0 }); // room table
        writer.Write((ushort)0xFFFF); // no room zero
        writer.Write((ushort)0x0020);
        writer.Write((ushort)0x0040);
        writer.Write((byte)1); // costumes
        writer.Write((byte)1); // container file number
        writer.Write((ushort)0x0010);
        writer.Write((byte)1); // scripts
        writer.Write((byte)1);
        writer.Write((ushort)0x0030);
        writer.Write((byte)1); // sounds
        writer.Write((byte)2);
        writer.Write((ushort)0x0040);
    }
}

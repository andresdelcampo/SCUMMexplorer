using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>Charset 0 (99.LFL) and messages drawn with it, as ScummVM's CharsetRendererV3 draws them.</summary>
public sealed class LoomCharsetTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void CharsetZeroIsReadFrom99Lfl()
    {
        if (!File.Exists(Path.Combine(Folder, "99.LFL"))) return;
        var charset = LoomCharset.TryLoad(Folder, 0, out var error);
        Assert.Null(error);
        Assert.NotNull(charset);
        // +4: 128 glyphs (the English release; the Spanish one has 240); +5: font height 8.
        Assert.Equal(128, charset.GlyphCount);
        Assert.Equal(8, charset.FontHeight);
        Assert.Equal(7, charset.CharWidth('A'));
        Assert.Equal(4, charset.CharWidth(' '));
    }

    [Fact]
    public void AMissingCharsetIsReportedNotThrown()
    {
        var charset = LoomCharset.TryLoad(Path.Combine(Path.GetTempPath(), "no-such-loom-folder"), 0, out var error);
        Assert.Null(charset);
        Assert.Contains("99.LFL", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSkysDescriptionIsDrawnCentredAtItsSlotPosition()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        var charset = LoomCharset.TryLoad(Folder, 0, out _)!;
        session.ActivateObjectVerb(156, 0x36);
        var message = session.Messages.Talk!;
        Assert.Equal("Dawn.", message.Text);

        var layer = LoomMessageRenderer.Render(message, charset);
        var width = charset.StringWidth(message.LineGlyphs[0]);
        Assert.NotNull(layer.Bounds);
        var (left, top, right, bottom) = layer.Bounds.Value;
        // displayDialog: centred lines start at x - width / 2; glyphs are 8 rows high.
        var start = Math.Max(0, message.X - width / 2);
        Assert.InRange(left, start - 1, start + 1);
        Assert.InRange(right, start + width - 2, start + width + 2);
        Assert.InRange(top, message.Y - 1, message.Y + 1);
        Assert.True(bottom <= message.Y + LoomCharset.GlyphRows + 2);
        // The text's own colour is the actor's talk colour (low 4 bits).
        Assert.Contains(layer.Pixels, pixel => pixel == (message.Color & 0x0F));
    }

    [Fact]
    public void RoomAndVerbScreensClipTextAtTheirBottom()
    {
        if (!File.Exists(Path.Combine(Folder, "99.LFL"))) return;
        var charset = LoomCharset.TryLoad(Folder, 0, out _)!;
        // The boot's initScreens(0, 144): the room screen is rows 0–143, with no text screen above it, so a line
        // at y 12 (Room 41's two-line messages) is drawn whole into the room's top rows.
        var glyphs = "AAAA"u8.ToArray();
        LoomMessage At(int y) => new(LoomMessageKind.Drawn, LoomMessageTable.NoActor, 1, [glyphs],
            10, y, false, 0, LoomMessageTable.DefaultRight, 15, null, "test", 0, 0, null);
        var inText = LoomMessageRenderer.Render(At(12), charset).Bounds!.Value;
        Assert.Equal(12, inText.Top);
        Assert.Equal(0, LoomMessageRenderer.RoomScreenTop);
        Assert.True(inText.Bottom > 16);
        var inRoom = LoomMessageRenderer.Render(At(140), charset).Bounds!.Value;
        Assert.Equal(LoomMessageRenderer.VerbScreenTop, inRoom.Bottom);
    }
}

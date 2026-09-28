using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>Charset 0 (99.LFL) and messages drawn with it, as ScummVM's CharsetRendererV3 draws them.</summary>
public sealed class LoomCharsetTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void CharsetZeroIsReadFrom99Lfl()
    {
        if (!File.Exists(Path.Combine(Folder, "99.LFL"))) return;
        var charset = LoomCharset.TryLoad(Folder, 0, out var error);
        Assert.Null(error);
        Assert.NotNull(charset);
        // +4: 240 glyphs; +5: font height 8.
        Assert.Equal(240, charset.GlyphCount);
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
        Assert.Equal("Amanecer.", message.Text);

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
    public void EachVirtualScreenClipsTextAtItsOwnBottom()
    {
        if (!File.Exists(Path.Combine(Folder, "99.LFL"))) return;
        var charset = LoomCharset.TryLoad(Folder, 0, out _)!;
        // initScreens(16, 144): the text screen is rows 0–15 and the room screen rows 16–143.
        // A line whose top is in the text screen stops at row 15 (printChar clips at vs->h).
        var glyphs = "AAAA"u8.ToArray();
        LoomMessage At(int y) => new(LoomMessageKind.Drawn, LoomMessageTable.NoActor, 1, [glyphs],
            10, y, false, 0, LoomMessageTable.DefaultRight, 15, null, "test", 0, 0, null);
        var inText = LoomMessageRenderer.Render(At(12), charset).Bounds!.Value;
        Assert.Equal(12, inText.Top);
        Assert.Equal(LoomMessageRenderer.RoomScreenTop, inText.Bottom);
        var inRoom = LoomMessageRenderer.Render(At(140), charset).Bounds!.Value;
        Assert.Equal(LoomMessageRenderer.VerbScreenTop, inRoom.Bottom);
    }
}

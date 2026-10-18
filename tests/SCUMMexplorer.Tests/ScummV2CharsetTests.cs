namespace SCUMMexplorer.Tests;

public sealed class ScummV2CharsetTests
{
    [Fact]
    public void BuiltInArrowsAndTextHaveNativeEightPixelCells()
    {
        var font = new ScummV2Charset();
        Assert.Equal(8, font.FontHeight);
        Assert.Equal(56, font.StringWidth("Walk to"u8.ToArray()));
        var pixels = new byte[32 * 8];
        Assert.Equal(32, font.DrawPlainLine(pixels,32,0,[1,2,3,4],0,0,14));
        // First native arrow row is 0x01, second is 0x80; bit7 is leftmost.
        Assert.Equal(14,pixels[7]);
        Assert.Equal(14,pixels[8]);
        Assert.Equal(0,pixels[0]);
        Assert.All(Enumerable.Range(0,4), cell => Assert.Contains((byte)14,
            Enumerable.Range(0,8).SelectMany(y=>pixels.Skip(y*32+cell*8).Take(8))));
    }

    [Fact]
    public void ClassicApostropheAndSpanishSlotsUseNativeReplacementTables()
    {
        static byte[] Draw(ScummV2Charset font,byte glyph) {
            var pixels=new byte[64];
            font.DrawPlainLine(pixels,8,0,[glyph],0,0,7);
            return pixels;
        }
        var classic = Draw(new(1),39);
        Assert.NotEqual(Draw(new(2),39),classic);
        Assert.Equal(Draw(new(2),39),Draw(new(1,demo:true),39));
        Assert.Equal(classic,Draw(new(2,"es"),39));
        Assert.NotEqual(Draw(new(2),91),Draw(new(2,"es"),91));
        // Language substitutions do not alter the native inventory arrows.
        Assert.Equal(Draw(new(1),1),Draw(new(2,"es"),1));
    }
}


namespace SCUMMexplorer.Tests;

public sealed class LoomScummTextDecoderTests
{
    [Fact]
    public void DecodesDosAccentsAndPreservesControlParameters()
    {
        var slash = ((char)92).ToString();
        var operands = "\"L" + slash + "x82m " + slash + "xA0\"; \"A" + slash +
                       "xFF" + slash + "x04" + slash + "x00" + slash + "x02B\"";

        var texts = ScummTextDecoder.DecodeLiterals(operands);

        Assert.Equal(["L\u00E9m \u00E1", "A[text control 04: 00 02]B"], texts);
    }

    [Fact]
    public void DecodesStoredObjectNameAsDosCp437()
    {
        var text = ScummTextDecoder.DecodeRawString([0x43, 0x61, 0x6E, 0x63, 0x69, 0xA2, 0x6E]);

        Assert.Equal("Canci\u00F3n", text);
    }
}

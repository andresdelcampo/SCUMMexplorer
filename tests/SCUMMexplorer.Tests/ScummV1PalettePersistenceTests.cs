namespace SCUMMexplorer.Tests;

public sealed class ScummV1PalettePersistenceTests
{
    [Fact]
    public void ClassicCostumeChangesPreserveEveryActorPaletteSlot()
    {
        var actors = new ScummActorTable();
        actors.InitializeV2Game(classic: true);
        for (var i = 0; i < 16; i++) actors.SetPalette(1, i, (i + 7) % 16);
        var before = actors.Snapshot().Single(a => a.Number == 1).Look.Palette.ToArray();
        foreach (var costume in new[] { 5, 0, 30 })
        {
            actors.CostumeChanged(1, costume, new(1, null, null, null));
            Assert.Equal(costume, actors.Snapshot().Single(a => a.Number == 1).Look.Costume);
            Assert.Equal(before, actors.Snapshot().Single(a => a.Number == 1).Look.Palette);
            Assert.All(actors.Snapshot().Single(a => a.Number == 2).Look.Palette, color => Assert.Equal(0, color));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnhancedAndGenericCostumeChangesRetainTheirIdentityReset(bool enhanced)
    {
        var actors = new ScummActorTable();
        if (enhanced) actors.InitializeV2Game(classic: false);
        actors.SetPalette(1, 1, 13);
        actors.CostumeChanged(1, 5, new(1, null, null, null));
        Assert.Equal(Enumerable.Range(0, 16).Select(i => (byte)i), actors.Snapshot().Single(a => a.Number == 1).Look.Palette);
    }
}

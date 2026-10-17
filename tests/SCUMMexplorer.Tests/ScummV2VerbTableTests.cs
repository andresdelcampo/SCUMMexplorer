namespace SCUMMexplorer.Tests;

public sealed class ScummV2VerbTableTests
{
    [Fact]
    public void ExplicitSlotsCanShareAnIdAndDeletionDoesNotLookUpThatId()
    {
        var verbs = new ScummVerbTable();
        verbs.CreateV2(9, 4, 1, 2, 0, "first");
        verbs.CreateV2(9, 1, 2, 3, 0, "second");
        Assert.Equal(new[] { 2, 5 }, verbs.Snapshot().Select(verb => verb.Slot));
        Assert.Equal(2, verbs.Find(9)!.Slot);
        verbs.SetV2Mode(9, 2);
        Assert.Equal(2, verbs.Snapshot()[0].Mode);
        Assert.Equal(1, verbs.Snapshot()[1].Mode);
        verbs.DeleteV2(1);
        Assert.Equal(5, Assert.Single(verbs.Snapshot()).Slot);
        Assert.Equal(5, verbs.Find(9)!.Slot);
        verbs.DeleteV2(8); // raw slot 8 addresses slot 9, not verb ID 9.
        Assert.Single(verbs.Snapshot());
    }

    [Fact]
    public void CreationReplacesTheSpecifiedSlotAndResetsItsAttributes()
    {
        var verbs = new ScummVerbTable();
        verbs.CreateV2(8, 4, 3, 19, 2, "old", charset: 3, highlightColor: 7, color: 13);
        verbs.SetV2Mode(8, 0);
        verbs.CreateV2(12, 4, 6, 20, 1, "new", charset: 0, yOffset: 8);
        Assert.Null(verbs.Find(8));
        var slot = Assert.Single(verbs.Snapshot());
        Assert.Equal(12, slot.VerbId);
        Assert.Equal(5, slot.Slot);
        Assert.Equal(48, slot.X);
        Assert.Equal(168, slot.Y);
        Assert.Equal(1, slot.Mode);
        Assert.Equal(1, slot.Preposition);
        Assert.Equal("new", slot.Name);
        Assert.Equal(0, slot.Charset);
        Assert.Equal(2, slot.Color);
        Assert.Equal(14, slot.HighlightColor);
        Assert.Equal(8, slot.DimColor);
        Assert.Equal((int)'t', slot.Key);
        Assert.False(slot.Centered);
        Assert.Null(slot.ImageObject);
        Assert.True(slot.DrawOrder > 0);
    }

    [Fact]
    public void SlotKeyboardAndScratchModeFollowSourceTable()
    {
        var verbs = new ScummVerbTable();
        const string keys = "qwertasdfgzxcvb";
        for (var index = 0; index < 16; index++)
            verbs.CreateV2(index + 1, index, 0, 0, 0, "verb");
        Assert.Equal(keys.Select(key => (int)key).Append(0), verbs.Snapshot().Select(verb => verb.Key));
        var before = verbs.Snapshot();
        verbs.SetV2Mode(88, 1);
        Assert.Null(verbs.Find(88));
        Assert.Equal(before, verbs.Snapshot());
        Assert.Throws<InvalidDataException>(() => verbs.DeleteV2(-1));
        Assert.Throws<InvalidDataException>(() => verbs.CreateV2(5, ScummVerbTable.Count - 1, 0, 0, 0, "invalid"));
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Own-copy source/context proofs. Decoded Mars branches are deliberately separate from the
/// reached opening: these tests never assign an actor, costume, palette, item owner or story bit.
/// </summary>
public sealed class ScummZakClassicMarsPaletteTests
{
    private static GameLibrary? Open()
    {
        var path = Path.Combine(TestGames.Root, "zak ega en");
        return Directory.Exists(path) ? new GameFolderScanner().Scan(path) : null;
    }

    private static ScummScriptInstruction At(ScummScriptDisassembly source, int offset, string operation, string operands)
    {
        var instruction = Assert.Single(source.Instructions, i => i.Offset == offset);
        Assert.Equal(operation, instruction.Operation);
        Assert.Equal(operands, instruction.Operands);
        return instruction;
    }

    [Fact]
    public void ReachedOpeningSeparatesLeslieStoredClothingColorFromHerCurrentCostumePalette()
    {
        if (Open() is not { } game) return;
        var data = new ScummV1DataSource(game);
        var observations = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(observations.Add, site => site.Operation == "actorOps");
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data, observer: observer);
        Assert.False(session.BootReport!.HitFrameLimit);
        var bootColor = Assert.Single(observations, o => o.Site is { Stage: "instruction", Script: 108, Offset: 0xFC });
        Assert.Equal((ushort)7, bootColor.Values["var[0x0C4]"]);
        var clothingRule = observations.Last(o => o.Site is { Stage: "instruction", Script: 5, Offset: 0x1C });
        Assert.True(bootColor.Sequence < clothingRule.Sequence);
        Assert.Equal((ushort)7, clothingRule.Values["var[0x0C4]"]);
        Assert.Equal((ushort)7, session.Snapshot.KnownGlobalVariables["var[0x0C4]"]);
        var melissa = session.Actors.Single(a => a.Number == 3);
        var leslie = session.Actors.Single(a => a.Number == 4);
        Assert.Equal((0, (byte)3, 26), (melissa.Look.Costume, melissa.Look.Palette[3], melissa.Room));
        Assert.Equal((0, (byte)8, 26), (leslie.Look.Costume, leslie.Look.Palette[4], leslie.Room));
        // No inference that one sampled body/clothing colour is universal for costume 7.
        Assert.Equal(ScummV1ColorBasis.Estimate, ScummV1ActorColors.Read(game).ColorOf(7).Basis);
    }

    [Fact]
    public void DecodedMarsIntroductionSharesCostumeSevenWithoutAssigningEitherActorPalette()
    {
        if (Open() is not { } game) return;
        var source = ScummDataSources.For(game).GlobalScript(81)!;
        At(source, 0x6, "pickupObject", "0x0042");
        At(source, 0x9, "pickupObject", "0x0041");
        At(source, 0xC, "setOwnerOf", "0x0041, 0x03");
        At(source, 0x10, "setOwnerOf", "0x0042, 0x04");
        At(source, 0x5A, "actorOps", "0x03, 0x07, sub=0x04");
        At(source, 0x5E, "actorOps", "0x04, 0x07, sub=0x04");
        Assert.DoesNotContain(source.Instructions, i => i.Operation == "actorOps" && i.Operands.EndsWith("sub=0x02"));
        Assert.Equal(new[] { 3, 4 }, ScummV1ActorColors.Read(game).ActorsOf(7));
    }

    [Fact]
    public void DecodedClothingRuleUsesStoredColorOnlyForLesliesOwnCostumeFour()
    {
        if (Open() is not { } game) return;
        var source = ScummDataSources.For(game).GlobalScript(5)!;
        At(source, 0xF, "actorOps", "0x04, var[0x0C4], sub=0x02");
        At(source, 0x13, "getActorCostume", "var[0x001], 0x04");
        // Native jumpRelative jumps on false. A costume other than 4 falls through to colour 8.
        At(source, 0x16, "isNotEqual", "var[0x001], 0x0004, +4  ->  0x0020");
        At(source, 0x1C, "actorOps", "0x04, 0x08, sub=0x02");
    }

    [Fact]
    public void DecodedLeslieHelmetColorLoopRejectsEightAndRetainsTheVariableOperand()
    {
        if (Open() is not { } game) return;
        var data = ScummDataSources.For(game);
        var record = data.ReadRoom(26).Objects.Single(o => o.Id == 66);
        var source = data.ReadObjectVerb(26, record, 0xFF, out _);
        At(source, 0x21, "isEqual", "var[0x0AA], 0x0020, +25  ->  0x0040");
        At(source, 0x27, "getActorCostume", "var[0x001], 0x04");
        At(source, 0x2A, "isEqual", "var[0x001], 0x0004, +16  ->  0x0040");
        At(source, 0x30, "getRandomNr", "var[0x0C4], 0x09");
        At(source, 0x33, "isEqual", "var[0x0C4], 0x0008, +3  ->  0x003C");
        At(source, 0x39, "jumpRelative", "-12  ->  0x0030");
        At(source, 0x3C, "actorOps", "0x04, var[0x0C4], sub=0x02");
        Assert.Null(Assert.Single(ScummV1ActorColors.Read(game).Settings,
            s => s.Source == "Room 26 / Object 66 / Fallback verb" && s.Offset == 0x3C).Value);
        // 0..9 except 8 is a decoded conditional domain, never a reached random colour or route.
    }

    [Fact]
    public void DecodedIndirectPaletteWriterTargetsShamanActorsRatherThanMarsCompanions()
    {
        if (Open() is not { } game) return;
        var source = ScummDataSources.For(game).RoomEntryScript(38)!;
        At(source, 0xA, "move", "var[0x042], 0x000A");
        At(source, 0x12, "actorOps", "var[0x042], 0x09, sub=0x02");
        At(source, 0x1F, "increment", "var[0x042]");
        At(source, 0x21, "isLess", "var[0x042], 0x000C, -25  ->  0x000E");
        var indirectWrites = ScummV1ActorColors.Read(game).Settings.Where(s => !s.IsCostume && s.Actor is null).ToArray();
        Assert.Single(indirectWrites);
        Assert.Equal("Room 38 / Entry script", indirectWrites[0].Source);
    }
}

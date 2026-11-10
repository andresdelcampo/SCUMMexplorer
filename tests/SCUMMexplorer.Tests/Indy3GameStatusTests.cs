using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Indy 3's Game Status (user, 9 November 2026): inventory, health, outfit, IQ points and story flags, found by their roles in its scripts.</summary>
public sealed class Indy3GameStatusTests(ITestOutputHelper output)
{
    private static GameLibrary? Indy3()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    [Fact]
    public void TheSchemaFindsHealthOutfitIqAndStoryFlagsByTheirRoles()
    {
        if (Indy3() is not { } game) return;
        var schema = ScummStatusSchema.Read(game);
        output.WriteLine($"health {schema.HealthVariable} max {schema.HealthMax} ({schema.HealthWhere})");
        output.WriteLine($"outfit {schema.OutfitVariable} actor {schema.OutfitActor}: {string.Join(", ", schema.Outfits.Select(o => $"{o.ObjectId} {o.Name} costume {o.Costume}"))}");
        output.WriteLine($"awards string {schema.AwardString} total {schema.AwardTotal}: {schema.Awards.Count}, {schema.Awards.Sum(a => a.Points)} points");
        foreach (var award in schema.Awards.Take(5)) output.WriteLine($"  {award.Slot} {award.Points} {award.Title} ({award.Where})");
        output.WriteLine($"flags {schema.Flags.Count}");
        foreach (var flag in schema.Flags.Take(8)) output.WriteLine($"  {flag.Bit}: {string.Join(" / ", flag.Titles)}");
        Assert.Equal("var[0x0C1]", schema.HealthVariable);
        Assert.Equal(1000, schema.HealthMax);
        Assert.Equal("var[0x0B4]", schema.OutfitVariable);
        Assert.Equal([403, 405, 406], schema.Outfits.Select(o => o.ObjectId).Order());
        Assert.Equal(7, schema.AwardString);
        Assert.Equal("var[0x0F4]", schema.AwardTotal);
        Assert.True(schema.Awards.Count > 50, $"{schema.Awards.Count} awards");
        Assert.True(schema.Flags.Count > 30, $"{schema.Flags.Count} flags");
    }

    [Theory]
    [InlineData("indy3 vga es")]
    [InlineData("indy3 ega en")]
    public void ItemsCarryTheirPickupNamesAndTheStartsFlagsAreUnnamed(string folder)
    {
        // 10 November 2026 (user report on the Spanish 256-colour copy: English item names, "uniform", "A scene" checked): where each name comes from.
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        ScummCutscenePlanner.IntoControl(session);
        var data = ScummDataSources.For(game);
        var names = game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct()
            .SelectMany(r => data.ReadRoom(r).ObjectNames.Select(n => (Room: r, n.Key, Name: ScummTextDecoder.DecodeGlyphs(n.Value).TrimEnd('\0', '@', ' ')))).ToList();
        foreach (var item in session.CaptureInventoryStatus().Catalog)
            output.WriteLine($"item {item.ObjectId} \"{item.Name}\" room {item.SourceRoomId}: {item.Evidence}; names {string.Join(", ", names.Where(n => n.Key == item.ObjectId).Select(n => $"{n.Room}:{n.Name}"))}");
        var schema = ScummStatusSchema.Read(game, ScummCutscenePlanner.VerbsOf(session));
        foreach (var flag in schema.Flags.Where(f => f.Titles.Any(t => t.EndsWith("An unnamed scene", StringComparison.Ordinal))))
            output.WriteLine($"flag {flag.Bit} set {session.StatusBit(flag.Bit)} unnamed {flag.Unnamed}: {string.Join(" / ", flag.Titles)} at {string.Join("; ", flag.Where)}");
        // The books of Room 17 carry the names their pickup gives them (the Spanish copy's "tomo": "libro mapas", "manual", "Mein Kampf").
        var books = session.CaptureInventoryStatus().Catalog.Where(c => c.ObjectId is 239 or 240 or 241).ToList();
        Assert.Equal(3, books.Count);
        Assert.All(books, book => Assert.True(book.Renamed, book.Name));
        Assert.Equal(3, books.Select(b => b.Name).Distinct().Count());
        // The game's start (Script 118) sets bits 5D5 to 5D7: no scene names them.
        Assert.All(schema.Flags.Where(f => f.Bit is "bit[0x5D5]" or "bit[0x5D6]" or "bit[0x5D7]"), flag => Assert.True(flag.Unnamed, flag.Bit));
    }

    [Fact]
    public void IndyIsTheOnlyRecipientAndTheCatalogAndChoicesApply()
    {
        if (Indy3() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        ScummCutscenePlanner.IntoControl(session);
        var status = session.CaptureInventoryStatus();
        output.WriteLine($"characters {string.Join(", ", status.Characters.Select(c => $"{c.ActorId} {c.Name}"))}; current {status.CurrentActorId}; held {string.Join(", ", status.Items.Select(i => $"{i.ObjectId} {i.Name} owner {i.OwnerId}"))}; catalog {status.Catalog.Count}; refusal {status.Refusal}");
        Assert.Single(status.Characters);
        Assert.True(status.Catalog.Count > 20, $"{status.Catalog.Count} items");
        Assert.Null(status.Refusal);
        // A grant of an item not held.
        var item = status.Catalog.First(c => status.Items.All(i => i.ObjectId != c.ObjectId) && !string.IsNullOrWhiteSpace(c.Name));
        var result = session.ApplyInventoryEdits(status, [new ScummInventoryTransfer(item.ObjectId, status.CurrentActorId)]);
        Assert.True(result.Applied, result.Refusal);
        Assert.Contains(session.CaptureInventoryStatus().Items, i => i.ObjectId == item.ObjectId);
        // Health, an IQ award and a story flag.
        var schema = ScummStatusSchema.Read(game);
        var award = schema.Awards[0];
        var flag = schema.Flags.First(f => !session.StatusBit(f.Bit));
        var before = session.StatusVariable(schema.AwardTotal!) ?? 0;
        Assert.Null(session.ApplyStatusChoices(schema, new ScummStatusChoices(Health: 400, Awards: new Dictionary<int, bool> { [award.Slot] = true },
            Flags: new Dictionary<string, bool> { [flag.Bit] = true })));
        Assert.Equal(400, session.StatusVariable(schema.HealthVariable!));
        Assert.Equal(award.Points, session.StatusAward(schema, award.Slot));
        Assert.Equal(before + award.Points, session.StatusVariable(schema.AwardTotal!));
        Assert.True(session.StatusBit(flag.Bit));
        // An outfit granted, then worn: the dressing script's variable and costume (the grey uniform, 406: costume 3), and it leaves the hero's hands.
        var uniform = schema.Outfits.First(o => o.ObjectId == 406);
        var granted = session.CaptureInventoryStatus();
        Assert.True(session.ApplyInventoryEdits(granted, [new ScummInventoryTransfer(uniform.ObjectId, granted.CurrentActorId)]).Applied);
        Assert.Null(session.ApplyStatusChoices(schema, new ScummStatusChoices(Outfit: uniform.ObjectId)));
        Assert.Equal(uniform.ObjectId, session.StatusVariable(schema.OutfitVariable!));
        Assert.Equal(uniform.Costume, session.Actors.First(a => a.Number == schema.OutfitActor).Look.Costume);
        Assert.Equal(14, session.CaptureInventoryStatus().Items.First(i => i.ObjectId == uniform.ObjectId).OwnerId);
    }
}

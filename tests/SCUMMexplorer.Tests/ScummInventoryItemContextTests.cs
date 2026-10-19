using System.Reflection;
using System.Text.Json;

namespace SCUMMexplorer.Tests;

public sealed class ScummInventoryItemContextTests
{
    private static ScummScriptDisassembly Script(int id, params (string Op, string Operands)[] operations) =>
        new(id, 0, 0, operations.Length, operations.Length,
            operations.Select((op, index) => new ScummScriptInstruction(index, 0, op.Op, op.Operands, "00")).ToArray(),
            null, null, [], [], Issues: []);

    private static IReadOnlyDictionary<int, ScummInventoryItemContext> Infer(ScummScriptDisassembly script,
        int first = 101, int second = 102, int actorA = 1, int actorB = 2) =>
        ScummInventoryItemContexts.Infer([new(first, 1, "same item", ""), new(second, 2, "same item", "")],
            [(script, null, null)], new Dictionary<int, ScummScriptDisassembly?>(), (_, _, _) => null, new HashSet<int> { actorA, actorB });

    [Fact]
    public void LiteralNativeRecipientsGiveACompleteDistinctGroupContext()
    {
        var found = Infer(Script(90, ("setOwnerOf", "0x0065, 0x01"), ("setOwnerOf", "0x0066, 0x02"), ("stopObjectCode", "")));
        Assert.Equal(1, found[101].ActorId); Assert.Equal(2, found[102].ActorId);
        Assert.Null(found[101].Text); // Names come from the session's own edition, not this analyzer.
        Assert.Contains("Script 90 +0x0000", found[101].Evidence);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("conflicting actor")]
    [InlineData("same actor")]
    [InlineData("unrelated object")]
    [InlineData("unresolved actor")]
    [InlineData("nonplayer")]
    [InlineData("incomplete")]
    public void AmbiguousOrPartialOwnerWritesDoNotInventAQualifier(string scenario)
    {
        var operations = new List<(string, string)> { ("setOwnerOf", "0x0065, 0x01") };
        operations.Add(("setOwnerOf", scenario switch
        {
            "partial" => "0x0065, 0x01",
            "conflicting actor" => "0x0065, 0x02",
            "same actor" => "0x0066, 0x01",
            "unrelated object" => "0x0067, 0x02",
            "unresolved actor" => "0x0066, var[0x071]",
            "nonplayer" => "0x0066, 0x0F",
            _ => "0x0066, 0x02"
        }));
        if (scenario == "conflicting actor") operations.Add(("setOwnerOf", "0x0066, 0x02"));
        operations.Add(("stopObjectCode", ""));
        var script = Script(90, operations.ToArray());
        if (scenario == "incomplete") script = script with { DecodeNote = "unresolved source bytes" };
        Assert.Empty(Infer(script));
    }

    [Theory]
    [InlineData(101, 102, 1, 2)]
    [InlineData(701, 702, 5, 6)]
    public void ConditionalRecipientSelectsNativeRecordsWithoutItemOrActorTables(int first, int second, int actorA, int actorB)
    {
        var script = Script(90,
            ("isEqual", $"var[0x071], 0x{actorA:X4}, +1 -> 0x0002"), ("move", $"var[0x070], 0x{first:X4}"),
            ("isEqual", $"var[0x071], 0x{actorB:X4}, +1 -> 0x0004"), ("move", $"var[0x070], 0x{second:X4}"),
            ("setOwnerOf", "var[0x070], var[0x071]"), ("stopObjectCode", ""));
        var found = Infer(script, first, second, actorA, actorB);
        Assert.Equal(actorA, found[first].ActorId); Assert.Equal(actorB, found[second].ActorId);
        Assert.Contains("native recipient argument", found[first].Evidence);
    }

    [Theory]
    [InlineData("unknown owner write")]
    [InlineData("unknown item write")]
    [InlineData("range owner write")]
    [InlineData("same record")]
    public void ConditionalRecordMappingRejectsLostArgumentsOrInterchangeableTargets(string scenario)
    {
        var operations = new List<(string, string)>
        {
            ("isEqual", "var[0x071], 0x0001, +1 -> 0x0002"), ("move", "var[0x070], 0x0065"),
            ("isEqual", "var[0x071], 0x0002, +1 -> 0x0004"), ("move", scenario == "same record" ? "var[0x070], 0x0065" : "var[0x070], 0x0066")
        };
        if (scenario != "same record") operations.Add(scenario switch
        {
            "unknown owner write" => ("move", "var[0x071], var[0x099]"),
            "unknown item write" => ("move", "var[0x070], var[0x099]"),
            _ => ("setVarRange", "var[0x070], [0x00, 0x00]")
        });
        operations.Add(("setOwnerOf", "var[0x070], var[0x071]")); operations.Add(("stopObjectCode", ""));
        Assert.Empty(Infer(Script(90, operations.ToArray())));
    }

    [Fact]
    public void OptionalOwnerModePreservesTheOriginalInventoryEffectScan()
    {
        var script = Script(90, ("move", "var[0x070], 0x0065"), ("pickupObject", "var[0x070]"),
            ("setOwnerOf", "var[0x070], 0x02"), ("pickupObject", "0x0066"), ("stopObjectCode", ""));
        var oldEffects = new List<int?>(); var newEffects = new List<int?>(); var owners = new List<(int?, int?)>();
        new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?>(), (_, _, _) => null,
            (_, _, id, _, _) => oldEffects.Add(id), _ => { }).Scan(script);
        new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?>(), (_, _, _) => null,
            (_, _, id, _, _) => newEffects.Add(id), _ => { }, ownerWrite: (_, _, id, actor) => owners.Add((id, actor))).Scan(script);
        Assert.Equal(new int?[] { 101, 102 }, oldEffects); Assert.Equal(oldEffects, newEffects);
        Assert.Equal(new[] { ((int?)101, (int?)2) }, owners);
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("null resource", false)]
    [InlineData("incomplete prefix", false)]
    [InlineData("recursive", false)]
    [InlineData("nested missing", false)]
    [InlineData("dynamic", false)]
    [InlineData("missing object", false)]
    [InlineData("incomplete object", false)]
    [InlineData("unknown other", false)]
    [InlineData("deep owner", false)]
    [InlineData("complete", true)]
    public void ConditionalContextRequiresACompleteResolvedCalledClosure(string scenario, bool expected)
    {
        var globals = new Dictionary<int, ScummScriptDisassembly?>();
        var helper = Script(91, ("move", "var[0x099], 0x0001"), ("stopObjectCode", ""));
        if (scenario is "incomplete prefix" or "incomplete object")
            helper = Script(91, ("setOwnerOf", "0x0065, 0x01"), ("stopObjectCode", "")) with { DecodeNote = "unknown tail after a partial owner write" };
        if (scenario == "recursive") helper = Script(91, ("startScript", "0x5B"), ("stopObjectCode", ""));
        if (scenario == "nested missing") helper = Script(91, ("startScript", "0x5C"), ("stopObjectCode", ""));
        if (scenario == "deep owner")
        {
            for(var id=92;id<111;id++)globals[id]=Script(id,("startScript",$"0x{id+1:X2}"),("stopObjectCode",""));
            globals[111]=Script(111,("setOwnerOf","0x0065, 0x01"),("stopObjectCode",""));
            helper=Script(91,("startScript","0x5C"),("stopObjectCode",""));
        }
        if (scenario != "missing") globals[91] = scenario == "null resource" ? null : helper;
        var call = scenario switch
        {
            "dynamic" => ("startScript", "var[0x099]"),
            "missing object" or "incomplete object" => ("doSentence", "0xFD, 0x03E8, 0x0000, sub=0x01"),
            "unknown other" => ("doSentence", "0x0B, 0x03E8, var[0x099], sub=0x01"),
            _ => ("startScript", "0x5B")
        };
        var script = Script(90,
            ("isEqual", "var[0x071], 0x0001, +1 -> 0x0002"), ("move", "var[0x070], 0x0065"),
            ("isEqual", "var[0x071], 0x0002, +1 -> 0x0004"), ("move", "var[0x070], 0x0066"),
            call, ("setOwnerOf", "var[0x070], var[0x071]"), ("stopObjectCode", ""));
        var found = ScummInventoryItemContexts.Infer([new(101, 1, "record", ""), new(102, 2, "record", "")],
            [(script, null, 1)], globals, (_, _, _) => scenario is "incomplete object" or "unknown other" ? helper : null, new HashSet<int> { 1, 2 });
        Assert.Equal(expected ? 2 : 0, found.Count);
    }

    [Fact]
    public void AFixedLiteralRecipientCannotUseASymbolicItemAfterAnUnresolvedHelper()
    {
        var script = Script(90, ("move", "var[0x070], 0x0065"), ("startScript", "0x5B"),
            ("setOwnerOf", "var[0x070], 0x01"), ("move", "var[0x070], 0x0066"),
            ("setOwnerOf", "var[0x070], 0x02"), ("stopObjectCode", ""));
        Assert.Empty(Infer(script));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete prefix")]
    [InlineData("recursive")]
    [InlineData("nested missing")]
    public void OwnerClosureBarriersLeaveCallbackNullInventoryScansUnchanged(string scenario)
    {
        var globals = new Dictionary<int, ScummScriptDisassembly?>();
        if (scenario == "incomplete prefix") globals[91] = Script(91, ("setOwnerOf", "0x0065, 0x01"), ("stopObjectCode", "")) with { DecodeNote = "unknown tail" };
        if (scenario == "recursive") globals[91] = Script(91, ("startScript", "0x5B"), ("stopObjectCode", ""));
        if (scenario == "nested missing") globals[91] = Script(91, ("startScript", "0x5C"), ("stopObjectCode", ""));
        var script = Script(90, ("move", "var[0x070], 0x0065"), ("startScript", "0x5B"),
            ("setOwnerOf", "var[0x070], 0x01"), ("pickupObject", "var[0x070]"), ("stopObjectCode", ""));
        var defaults = new List<int?>(); var defaultNotes = new List<string>();
        new ScummInventoryCatalogAnalysis(globals, (_, _, _) => null, (_, _, id, _, _) => defaults.Add(id), defaultNotes.Add).Scan(script);
        Assert.Equal(new int?[] { 101 }, defaults);
        Assert.DoesNotContain(defaultNotes, x => x.StartsWith("Unresolved owner-flow:"));
        var ownerEvents = new List<int?>(); var observedPickups = new List<int?>(); var notes = new List<string>();
        new ScummInventoryCatalogAnalysis(globals, (_, _, _) => null, (_, _, id, _, _) => observedPickups.Add(id), notes.Add,
            ownerWrite: (_, _, id, _) => ownerEvents.Add(id)).Scan(script);
        Assert.Empty(ownerEvents); Assert.Empty(observedPickups);
        Assert.Contains(notes, x => x.StartsWith("Unresolved owner-flow:"));
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void OwnEditionSuppliesTenMeaningfulQualifiersAndLeavesShardsNeutral(string folder)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var session = Ready(folder); var runtime = Runtime(session); var before = State(runtime);
        var status = session.CaptureInventoryStatus();
        Assert.Equal(70, status.Catalog.Count);
        var expected = new Dictionary<int, int>
        {
            [18] = 1,
            [32] = 2,
            [75] = 4,
            [65] = 3,
            [66] = 4,
            [82] = 1,
            [83] = 2,
            [61] = 1,
            [62] = 3,
            [63] = 4
        };
        Assert.Equal(expected.Keys.Order(), status.Catalog.Where(x => x.Context is not null).Select(x => x.ObjectId).Order());
        foreach (var (item, actor) in expected)
        {
            var context = Assert.IsType<ScummInventoryItemContext>(status.Catalog.Single(x => x.ObjectId == item).Context);
            Assert.Equal(actor, context.ActorId);
            Assert.Equal(status.Characters.Single(x => x.ActorId == actor).Name, context.Text);
            Assert.False(string.IsNullOrWhiteSpace(context.Text)); Assert.Contains("setOwnerOf", context.Evidence);
        }
        foreach (var shard in new[] { 33, 56 }) Assert.Null(status.Catalog.Single(x => x.ObjectId == shard).Context);
        Assert.Equal(before, State(runtime));
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void NativeSourceAssociationsSurviveAdminTransfersAndExactUndo(string folder)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var session = Ready(folder); var runtime = Runtime(session); var beforeState = State(runtime);
        var before = session.CaptureInventoryStatus();
        var contexts = before.Catalog.Where(x => x.Context is not null).ToDictionary(x => x.ObjectId, x => x.Context!);
        var changes = contexts.Select(x => new ScummInventoryTransfer(x.Key, x.Value.ActorId % before.Characters.Count + 1)).ToArray();
        var result = session.ApplyInventoryEdits(before, changes); Assert.True(result.Applied, result.Refusal);
        var after = session.CaptureInventoryStatus();
        foreach (var (item, context) in contexts)
        {
            Assert.Equal(context, after.Catalog.Single(x => x.ObjectId == item).Context);
            Assert.NotEqual(context.ActorId, after.Items.Single(x => x.ObjectId == item).OwnerId);
        }
        var undone = session.UndoInventoryTransfers(result.Undo!); Assert.True(undone.Applied, undone.Refusal);
        Assert.Equal(beforeState, State(runtime));
        foreach (var (item, context) in contexts) Assert.Equal(context, session.CaptureInventoryStatus().Catalog.Single(x => x.ObjectId == item).Context);
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void SnapshotReadsNativeNamesWithoutFreezingMissingOrInventingEnglishNames(string folder)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var session = Ready(folder); var runtime = Runtime(session);
        var original = runtime.ActorNames[2]; runtime.ActorNames.Remove(2);
        Assert.Null(session.CaptureInventoryStatus().Catalog.Single(x => x.ObjectId == 32).Context!.Text);
        runtime.ActorNames[2] = "Native édition name";
        Assert.Equal("Native édition name", session.CaptureInventoryStatus().Catalog.Single(x => x.ObjectId == 32).Context!.Text);
        runtime.ActorNames[2] = original;
    }

    private static ScummGameSession Ready(string folder)
    {
        var session = ScummGameSession.BootForPlay(new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder)), out _);
        for (var frame = 0; frame < 2500 && !session.CaptureInventoryStatus().CanEdit; frame++) session.AdvanceFrame();
        Assert.True(session.CaptureInventoryStatus().CanEdit); return session;
    }
    private static ScummScriptRuntimeState Runtime(ScummSession session) => (ScummScriptRuntimeState)
        typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    private static string State(ScummScriptRuntimeState runtime) => JsonSerializer.Serialize(new
    {
        Slots = runtime.InventorySlots,
        Rooms = runtime.InventoryRooms.OrderBy(x => x.Key),
        Owners = runtime.ObjectOwners.OrderBy(x => x.Key),
        States = runtime.ObjectStates.OrderBy(x => x.Key),
        Members = runtime.Inventory.Order(),
        Variables = runtime.GlobalVariables.OrderBy(x => x.Key),
        Actors = runtime.Actors.Snapshot(),
        Classes = runtime.ObjectClasses.OrderBy(x => x.Key),
        Names = runtime.ActorNames.OrderBy(x => x.Key)
    });
}

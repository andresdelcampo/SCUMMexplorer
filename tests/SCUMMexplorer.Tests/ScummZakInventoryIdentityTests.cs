using System.Reflection;

namespace SCUMMexplorer.Tests;

public sealed class ScummZakInventoryIdentityTests
{
    [Theory]
    [InlineData("zak ega en", 55, 14)]
    [InlineData("zakE ega en", 55, 14)]
    [InlineData("zak ega en", 73, 30)]
    [InlineData("zakE ega en", 73, 30)]
    public void CanonicalCrystalAndFuseHaveNativeInventoryAllocationInstructions(string folder, int canonical, int roomId)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder));
        var data = (ScummV2DataSource)ScummDataSources.For(game); var indexed = data.ReadIndexedObjects();
        var room = data.ReadRoom(roomId);
        var script = canonical == 55 ? data.GlobalScript(119)! : data.ReadObjectVerb(30, room.Objects.Last(x => x.Id == 491), 33, out _);
        var instruction = Assert.Single(script.Instructions, x => x.Operation == "pickupObject");
        Assert.Equal(canonical == 55 ? ScummVars.V2.ActiveObject1 : "0x0049", instruction.Operands);
        var runtime = new ScummScriptRuntimeState(indexed.States, indexed.Owners) { EngineVersion = ScummProfile.For(game).EngineVersion, CurrentRoom = roomId };
        runtime.SetGlobalVariable(ScummVars.V2.Ego, 1); runtime.SetGlobalVariable(ScummVars.V2.ActiveObject1, (ushort)canonical);
        // Narrow prepared execution of the actual allocation instruction, not an earned pickup or story route.
        new ScummBoundedScriptExecutor().Execute(script, indexed.States, room.Objects, instructionLimit: 1, runtime: runtime, startOffset: instruction.Offset);
        Assert.Contains(canonical, runtime.InventorySlots); Assert.Equal(roomId, runtime.InventoryRooms[canonical]); Assert.Equal(1, runtime.ObjectOwners[canonical]);
    }

    [Theory]
    [InlineData("zak ega en", 25, 431, 431, 14, 55)]
    [InlineData("zak ega en", 25, 429, 429, 14, 81)]
    [InlineData("zak ega en", 28, 471, 471, 14, 73)]
    [InlineData("zak ega en", 33, 535, 537, 253, 70)]
    [InlineData("zak ega en", 33, 536, 537, 253, 70)]
    [InlineData("zak ega en", 33, 537, 537, 253, 70)]
    [InlineData("zakE ega en", 25, 431, 431, 14, 55)]
    [InlineData("zakE ega en", 25, 429, 429, 14, 81)]
    [InlineData("zakE ega en", 28, 471, 471, 14, 73)]
    [InlineData("zakE ega en", 33, 535, 537, 253, 70)]
    [InlineData("zakE ega en", 33, 536, 537, 253, 70)]
    [InlineData("zakE ega en", 33, 537, 537, 253, 70)]
    public void NativeScenePickupReturnsTheCanonicalOwnerWithoutAcquiringTheSceneHeader(string folder, int roomId, int scene, int handler, int verb, int canonical)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder));
        var data = (ScummV2DataSource)ScummDataSources.For(game); var indexed = data.ReadIndexedObjects();
        var runtime = new ScummScriptRuntimeState(indexed.States, indexed.Owners) { EngineVersion = ScummProfile.For(game).Version, CurrentRoom = roomId };
        runtime.SetGlobalVariable(ScummVars.V2.Ego, 1); runtime.SetGlobalVariable(ScummVars.V2.ActiveObject1, (ushort)scene);
        runtime.ObjectStates[canonical] = 1; // Narrow prepared commit: canonical ladder unlocked.
        var retainedRoom = canonical switch { 55 => 14, 81 => 44, 73 => 30, _ => 28 };
        runtime.InventorySlots[0] = canonical; runtime.InventoryRooms[canonical] = retainedRoom; runtime.Inventory.Add(canonical); runtime.SetObjectOwner(canonical, 14);
        var room = data.ReadRoom(roomId);
        if (scene != handler)
        {
            var pickup = data.ReadObjectVerb(roomId, room.Objects.Last(x => x.Id == scene), 14, out _);
            Assert.Contains(pickup.Instructions, x => x.Operation == "doSentence" && x.Operands == $"0xFD, 0x{handler:X4}, 0x0000, sub=0x01");
        }
        var script = data.ReadObjectVerb(roomId, room.Objects.Last(x => x.Id == handler), verb, out _);
        var result = new ScummBoundedScriptExecutor().Execute(script, indexed.States, room.Objects, runtime: runtime, startOffset: script.EntryOffset);
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(1, runtime.ObjectOwners[canonical]); Assert.Equal(15, runtime.ObjectOwners[scene]);
        Assert.Equal(canonical, runtime.InventorySlots[0]); Assert.Equal(retainedRoom, runtime.InventoryRooms[canonical]);
        Assert.DoesNotContain(scene, runtime.InventorySlots);
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void CompleteSourceEffectsAndPickupClosuresSeparateSixSceneAliases(string folder)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder));
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var indexed = data.ReadIndexedObjects();
        var session = Ready(game);
        var liveCatalog = session.CaptureInventoryStatus().Catalog;
        var rooms = game.Rooms.Where(x => x.ResourceId.HasValue).Select(x => x.ResourceId!.Value).Distinct()
            .ToDictionary(x => x, data.ReadRoom);
        // Rebuild the provisional index candidates independently of the final filtered UI catalog.
        // This continues to test all six false identities after the parent wires the filter.
        var indexedCode = rooms.SelectMany(r => r.Value.Objects.Select(o => (Room:r.Key,Id:o.Id)))
            .GroupBy(x=>x.Id).Where(g=>g.Select(x=>x.Room).Distinct().Count()==1&&(indexed.States.GetValueOrDefault(g.Key)&1)!=0)
            .Select(g=>g.Last()).Select(x=>new ScummInventoryCatalogItem(x.Id,x.Room,
                rooms[x.Room].ObjectNames.TryGetValue(x.Id,out var text)?ScummTextDecoder.DecodeGlyphs(text).TrimEnd('\0','@',' '):null,"index candidate"));
        var catalog=liveCatalog.Concat(indexedCode).DistinctBy(x=>x.ObjectId).ToArray();
        Assert.Equal(76,catalog.Length);
        var globals = data.GlobalScriptIds().ToDictionary(x => x, data.GlobalScript);
        var roots = globals.Values.Select(s => (Script: s, Object: (int?)null, Room: (int?)null)).ToList();
        foreach (var (roomId, room) in rooms)
        {
            roots.Add((data.RoomEntryScript(roomId), null, roomId));
            roots.Add((data.RoomExitScript(roomId), null, roomId));
            foreach (var local in data.RoomLocalScriptIds(roomId)) roots.Add((data.RoomLocalScript(roomId, local), null, roomId));
            var tables = data.ReadObjectVerbs(roomId, room.Objects);
            foreach (var obj in room.Objects.GroupBy(x => x.Id).Select(x => x.Last()))
                foreach (var verb in tables.GetValueOrDefault(obj.Id) ?? [])
                    roots.Add((data.ReadObjectVerb(roomId, obj, verb.VerbId, out _), verb.VerbId is 250 or 253 or 254 ? null : obj.Id, roomId));
        }
        var cache = new Dictionary<(int, int, int), ScummScriptDisassembly?>();
        ScummScriptDisassembly? ObjectScript(int roomId, int objectId, int verb)
        {
            var key = (roomId, objectId, verb);
            if (cache.TryGetValue(key, out var cached)) return cached;
            return cache[key] = Resolve(roomId, objectId, verb);
        }
        ScummScriptDisassembly? Resolve(int roomId, int objectId, int verb)
        {
            var room = rooms.GetValueOrDefault(roomId);
            var obj = room?.Objects.LastOrDefault(x => x.Id == objectId);
            if (obj is null)
            {
                var found = rooms.Where(x => x.Value.Objects.Any(o => o.Id == objectId)).ToArray();
                if (found.Length != 1) return null;
                roomId = found[0].Key; room = found[0].Value; obj = room.Objects.Last(x => x.Id == objectId);
            }
            var verbs = data.ReadObjectVerbs(roomId, room!.Objects).GetValueOrDefault(objectId) ?? [];
            return verbs.Any(x => x.VerbId == verb || x.VerbId == 255) ? data.ReadObjectVerb(roomId, obj, verb, out _) : null;
        }
        var mutable = new HashSet<int>();
        var writers = new ScummInventoryCatalogAnalysis(globals, ObjectScript, (_, _, id, _, state) => { if (state && id is int n) mutable.Add(n); }, _ => { });
        foreach (var root in roots) writers.Scan(root.Script, root.Object, root.Room);
        Assert.Equal(new[] { 71 }, mutable.Order());
        var effects = new HashSet<int>();
        var analysis = new ScummInventoryCatalogAnalysis(globals, ObjectScript, (_, instruction, id, _, state) =>
        {
            if (id is int n && !(state && instruction.Operands.EndsWith(", Unpickupable"))) effects.Add(n);
        }, _ => { }, indexed.States.ToDictionary(x => x.Key, x => mutable.Contains(x.Key) ? (bool?)null : (x.Value & 1) != 0));
        foreach (var root in roots) analysis.Scan(root.Script, root.Object, root.Room);
        bool KnownEmpty(int roomId, int id, int verb)
        {
            var found = rooms.Where(x => (x.Key == roomId || !rooms.GetValueOrDefault(roomId)?.Objects.Any(o => o.Id == id) == true) && x.Value.Objects.Any(o => o.Id == id)).ToArray();
            if (found.Length != 1) return false;
            var entries = data.ReadObjectVerbs(found[0].Key, found[0].Value.Objects).GetValueOrDefault(id);
            return entries is not null && !entries.Any(x => x.VerbId == verb || x.VerbId == 255);
        }
        var diagnostics = new List<string>();
        bool RetainedCode(int id)
        {
            var item = catalog.Single(x => x.ObjectId == id); var room = rooms[item.SourceRoomId];
            return (data.ReadObjectVerbs(item.SourceRoomId, room.Objects).GetValueOrDefault(id) ?? [])
                .Any(v => v.VerbId is > 0 and < 250 && v.VerbId != 14 && ObjectScript(item.SourceRoomId, id, v.VerbId) is { HasCompleteControlFlow: true, DecodeNote: null });
        }
        var aliases = ScummInventoryCatalogIdentity.FindSceneAliases(catalog, effects, globals, ObjectScript, KnownEmpty, RetainedCode, diagnostics.Add);
        Assert.True(new[] { 429, 431, 471, 535, 536, 537 }.SequenceEqual(aliases.Keys.Order()), string.Join(Environment.NewLine, diagnostics));
        Assert.Equal(81, aliases[429]); Assert.Equal(55, aliases[431]); Assert.Equal(73, aliases[471]);
        foreach (var id in new[] { 535, 536, 537 }) Assert.Equal(70, aliases[id]);
        foreach (var id in aliases.Keys) Assert.DoesNotContain(id, effects);
        foreach (var id in aliases.Values) Assert.NotNull(ObjectScript(catalog.Single(x => x.ObjectId == id).SourceRoomId, id, 11));
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void SameNamedRealInventoryItemsRemainIndependentAndUndoExactly(string folder)
    {
        if (!Directory.Exists(Path.Combine(TestGames.Root, folder))) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder));
        var session = Ready(game);
        var runtime = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        var beforeSlots = runtime.InventorySlots.ToArray();
        var beforeOwners = runtime.ObjectOwners.ToDictionary();
        var edits = new ScummInventoryTransfer[] { new(18, 1), new(32, 2), new(75, 4), new(61, 1), new(62, 3), new(63, 4), new(82, 1), new(83, 2), new(65, 3), new(66, 4), new(33, 1), new(56, 2) };
        var result = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), edits);
        Assert.True(result.Applied, result.Refusal);
        foreach (var edit in edits)
        {
            Assert.Single(runtime.InventorySlots, x => x == edit.ObjectId);
            Assert.Equal(edit.TargetActorId, runtime.ObjectOwners[edit.ObjectId]);
        }
        var undone = session.UndoInventoryTransfers(result.Undo!);
        Assert.True(undone.Applied, undone.Refusal);
        Assert.Equal(beforeSlots, runtime.InventorySlots);
        Assert.Equal(beforeOwners.OrderBy(x => x.Key), runtime.ObjectOwners.OrderBy(x => x.Key));
    }

    private static ScummGameSession Ready(SCUMMexplorer.Core.Domain.GameLibrary game)
    {
        var session = ScummGameSession.BootForPlay(game, out _);
        for (var f = 0; f < 2500 && !session.CaptureInventoryStatus().CanEdit; f++) session.AdvanceFrame();
        Assert.True(session.CaptureInventoryStatus().CanEdit); return session;
    }
}

public sealed class ScummInventoryIdentityClosureTests
{
    private static ScummScriptDisassembly Script(int id, params (string Op, string Operands)[] operations) =>
        new(id, 0, 0, operations.Length, operations.Length,
            operations.Select((op, index) => new ScummScriptInstruction(index, 0, op.Op, op.Operands, "00")).ToArray(),
            null, null, [], [], Issues: []);

    [Theory]
    [InlineData("known empty", true)]
    [InlineData("unresolved", false)]
    [InlineData("missing dispatcher", false)]
    [InlineData("unknown queued verb", false)]
    [InlineData("source pickup", false)]
    [InlineData("dynamic owner", false)]
    [InlineData("alternate owner", false)]
    [InlineData("different name", false)]
    [InlineData("owner removed", false)]
    [InlineData("queued stale caller", false)]
    public void QueuedSentenceUsesKnownContextAndUnprovedIdentityAlwaysKeepsTheCandidate(string scenario, bool expected)
    {
        var initial = Script(90, ("setOwnerOf", scenario == "dynamic owner" ? "var[0x077], var[0x000]" : scenario == "owner removed" ? "0x0014, 0x000F" : "0x0014, var[0x000]"),
            ("doSentence", scenario == "unknown queued verb" ? "var[0x055], 0x03E9, 0x0000, sub=0x00" : "0x0D, 0x03E9, 0x0000, sub=0x00"), ("stopObjectCode", ""));
        if (scenario == "queued stale caller") initial = Script(90, ("move", "var[0x099], 0x0000"), ("setOwnerOf", "0x0014, var[0x000]"), ("doSentence", "0x0D, 0x03E9, 0x0000, sub=0x00"), ("stopObjectCode", ""));
        var dispatch = scenario is "alternate owner" or "queued stale caller"
            ? Script(2, ("equalZero", "var[0x099], +2 -> 0x0003"), ("doSentence", "0x0D, 0x03E9, 0x0000, sub=0x01"), ("stopObjectCode", ""), ("setOwnerOf", "0x001E, 0x0001"), ("stopObjectCode", ""))
            : Script(2, ("isEqual", "var[0x008], 0x000D, +2 -> 0x0003"), ("doSentence", "0x0D, 0x03E9, 0x0000, sub=0x01"), ("stopObjectCode", ""), ("setOwnerOf", "0x001E, 0x0001"), ("stopObjectCode", ""));
        var globals = new Dictionary<int, ScummScriptDisassembly?>();
        if (scenario != "missing dispatcher") globals[2] = dispatch;
        var catalog = new[] { new ScummInventoryCatalogItem(10, 1, "fuse", ""), new ScummInventoryCatalogItem(20, 2, scenario == "different name" ? "ladder" : "fuse", ""), new ScummInventoryCatalogItem(30, 3, "fuse", "") };
        var acquired = scenario == "source pickup" ? new HashSet<int> { 10 } : [];
        var aliases = ScummInventoryCatalogIdentity.FindSceneAliases(catalog, acquired, globals,
            (_, id, verb) => id == 10 && verb == 14 ? initial : id == 20 && verb == 11 ? Script(91, ("stopObjectCode", "")) : null,
            (_, id, verb) => scenario != "unresolved" && id == 1001 && verb == 13,
            id => id == 20);
        Assert.Equal(expected, aliases.ContainsKey(10));
        if (expected) Assert.Equal(20, aliases[10]);
    }

    [Theory]
    [InlineData("unknown move")]
    [InlineData("indirect read")]
    [InlineData("range tail")]
    [InlineData("literal bit write")]
    [InlineData("sound result")]
    public void UnknownWritesCannotKeepAConstantThatHidesAnotherOwner(string scenario)
    {
        var mutation = scenario switch
        {
            "unknown move" => ("move", "var[0x077], var[0x099]"),
            "indirect read" => ("move", "var[0x077], var[0x00E]"),
            "range tail" => ("setVarRange", "var[0x076], [0x00, 0x01]"),
            "literal bit write" => ("setBitVar", "0x0610, 0x00, 0x01"),
            _ => ("isSoundRunning", "var[0x077], 0x01")
        };
        var script = Script(90, ("setOwnerOf", "0x0014, var[0x000]"), ("move", "var[0x077], 0x0000"), mutation,
            ("equalZero", "var[0x077], +2 -> 0x0006"), ("stopObjectCode", ""), ("stopObjectCode", ""),
            ("setOwnerOf", "0x001E, var[0x000]"), ("stopObjectCode", ""));
        Assert.Empty(Find(script, new Dictionary<int, ScummScriptDisassembly?>()));
    }

    [Theory]
    [InlineData("loadRoom", "0x02")]
    [InlineData("loadRoomWithEgo", "0x02, 0x0014")]
    [InlineData("restart", "")]
    [InlineData("jumpRelative", "+20 -> 0x0020")]
    public void MissingOrChangedSourceContextsCannotProveAliases(string op, string operands)
    {
        var script = Script(90, ("setOwnerOf", "0x0014, var[0x000]"), (op, operands), ("stopObjectCode", ""));
        Assert.Empty(Find(script, new Dictionary<int, ScummScriptDisassembly?>()));
    }

    [Fact]
    public void RecursiveCallsCannotManufactureACompletedPickupReturn()
    {
        var cycle = Script(99, ("startScript", "0x63"), ("setOwnerOf", "0x0014, var[0x000]"), ("stopObjectCode", ""));
        Assert.Empty(Find(Script(90, ("startScript", "0x63"), ("stopObjectCode", "")), new Dictionary<int, ScummScriptDisassembly?> { [99] = cycle }));
    }

    [Fact]
    public void MutualSceneRepresentationsCannotBecomeEachOthersRetainedIdentity()
    {
        var catalog=new[]{new ScummInventoryCatalogItem(10,1,"fuse",""),new ScummInventoryCatalogItem(20,2,"fuse","")};
        var aliases=ScummInventoryCatalogIdentity.FindSceneAliases(catalog,new HashSet<int>(),new Dictionary<int,ScummScriptDisassembly?>(),
            (_,id,verb)=>verb==14?Script(id,("setOwnerOf",$"0x{(id==10?20:10):X4}, var[0x000]"),("stopObjectCode","")):null,
            retainedItemCode:_=>true);
        Assert.Empty(aliases);
    }

    [Theory]
    [InlineData("sub=0x01", true)]
    [InlineData("sub=0x00", false)]
    public void StopOnlySentenceDoesNotRunAnInventedHandler(string mode, bool expected)
    {
        var script = Script(90, ("setOwnerOf", "0x0014, var[0x000]"), ("doSentence", $"0xFE, 0x03E9, 0x0000, {mode}"), ("stopObjectCode", ""));
        Assert.Equal(expected, Find(script, new Dictionary<int, ScummScriptDisassembly?>()).ContainsKey(10));
    }

    private static IReadOnlyDictionary<int, int> Find(ScummScriptDisassembly script, IReadOnlyDictionary<int, ScummScriptDisassembly?> globals)
    {
        var catalog = new[] { new ScummInventoryCatalogItem(10, 1, "fuse", ""), new ScummInventoryCatalogItem(20, 2, "fuse", ""), new ScummInventoryCatalogItem(30, 3, "fuse", "") };
        return ScummInventoryCatalogIdentity.FindSceneAliases(catalog, new HashSet<int>(), globals,
            (_, id, verb) => id == 10 && verb == 14 ? script : null, retainedItemCode: id => id == 20);
    }
}

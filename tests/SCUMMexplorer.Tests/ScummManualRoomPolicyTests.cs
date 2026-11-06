using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummManualRoomPolicyTests
{
    [Theory]
    [InlineData("monkey1 vga en")]
    [InlineData("monkey1 ega en")]
    public void MonkeyRoom51WithConditionalSceneWatchersDoesNotPublishAnUnfinishedEntry(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        var before = session.CurrentRoomId;
        var result = session.PrepareManualRoom(game.Rooms.Single(r => r.ResourceId == 51));
        Assert.Equal(before, session.CurrentRoomId);
        Assert.False(result.Succeeded);
        Assert.Equal(2400, result.Frames);
        // Its watchers no longer count as initialization (decision 7, 6 November 2026), but the room still does not come to rest.
        Assert.Contains("within the bound", result.Reason);
    }

    [Fact]
    public void LoomManualRoomWithConditionalSceneWatchersLeavesTheOriginalAlone()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(path, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(path);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(path, "00.LFL")));
        var before = session.Snapshot;
        var result = session.PrepareManualRoom(game.Rooms.Single(r => r.ResourceId == 2));
        Assert.Equal(before.RoomId, session.CurrentRoomId);
        Assert.Equal(before.PendingScripts, session.Snapshot.PendingScripts);
        Assert.False(result.Succeeded);
        Assert.Equal(2400, result.Frames);
        Assert.Contains("pending scene initialization", result.Reason);
    }

    [Fact]
    public void EnhancedManiacCommittedPartySurvivesCancelingDoorbellForManualRoom()
    {
        var path = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        SCUMMexplorer.Core.Maniac.ManiacFirstVisit.Prepare(game, session);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 1));
        session.ActivateObjectVerb(394, 9);
        for (var frame = 0; frame < 400 && !session.InCutscene; frame++) session.AdvanceFrame();
        Assert.True(session.InCutscene);
        var party = session.CaptureInventoryStatus().Characters.Select(c => c.ActorId).ToArray();
        var result = session.PrepareManualRoom(game.Rooms.Single(r => r.ResourceId == 2));
        Assert.True(result.Succeeded, $"{result.Reason}; frames={result.Frames}");
        Assert.True(session.InCutscene);
        var copy = Assert.IsType<ScummGameSession>(result.Session);
        Assert.Equal(party, copy.CaptureInventoryStatus().Characters.Select(c => c.ActorId));
        var scenes = copy.CutscenesBegun;
        for (var frame = 0; frame < 600; frame++) copy.AdvanceFrame();
        Assert.Equal(2, copy.CurrentRoomId);
        Assert.Equal(scenes, copy.CutscenesBegun);
        Assert.False(copy.InCutscene);
    }

    private static ScummScriptDisassembly Code(int number, string? context, params (string Op, string Args)[] operations) =>
        new(number, 1, number * 100, operations.Length, operations.Length,
            operations.Select((op, offset) => new ScummScriptInstruction(offset, 0, op.Op, op.Args, "00")).ToArray(),
            null, null, [], [], context, []);

    private sealed class Data : IScummDataSource
    {
        public Dictionary<int, ScummScriptDisassembly> Globals = [];
        public Dictionary<int, ScummScriptDisassembly> Entries = [];
        public int GlobalScriptCount => 100;
        public IReadOnlyList<int> GlobalScriptIds() => Globals.Keys.ToArray();
        public ScummScriptDisassembly? GlobalScript(int number) => Globals.GetValueOrDefault(number);
        public ScummScriptDisassembly? RoomEntryScript(int room) => Entries.GetValueOrDefault(room);
        public ScummScriptDisassembly? RoomExitScript(int room) => null;
        public IReadOnlyList<int> RoomLocalScriptIds(int room) => [];
        public ScummScriptDisassembly? RoomLocalScript(int room, int number) => null;
        public ScummRoomData ReadRoom(int room) => new([], new Dictionary<int, byte[]>(), null, null, 320, 144);
        public ScummCostume? ReadCostume(int id) => null;
        public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => [];
        public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records) =>
            new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
        public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry) =>
            throw new InvalidDataException("No synthetic object verb");
    }

    private sealed class Session : ScummSession
    {
        public Session(Data data) : base(new("Synthetic", "synthetic", ScummVersion.V3, "DOS",
            Enumerable.Range(1, 3).Select(i => new GameResource($"Room {i}", ResourceKind.Room, "unused", i)).ToArray(), "test"),
            new(new Dictionary<int, byte> { [17] = 0 }, new Dictionary<int, byte>()), 0, null, data,
            _ => new(ScummSoundLifetimeKind.Unknown, null, false, "test"))
        {
            foreach (var key in new[] { ScummVars.EntryScript, ScummVars.EntryScript2, ScummVars.ExitScript, ScummVars.ExitScript2,
                ScummVars.CutsceneStartScript, ScummVars.CutsceneEndScript }) _runtime.SetGlobalVariable(key, 0);
            _runtime.UserPut = 1;
            EnterRoom(Room(1), "synthetic initial state");
        }
        public GameResource Room(int id) => _game.Rooms.Single(r => r.ResourceId == id);
        public void Start(int id) => RunStartedScript(id, 0, null, 0);
        protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
            EnterRoom(target, "native synthetic load", action.ReachedRoomLoad);
    }

    [Fact]
    public void ManualDepartureCancelsAnUnskippableSceneAndItsDelayedChildWithoutRunningItsTail()
    {
        var data = new Data();
        data.Globals[20] = Code(20, null, ("cutscene", "[]"), ("startScript", "0x15, []"), ("breakHere", ""),
            ("move", "var[0x0D0] = 0x0001"), ("loadRoom", "0x03"), ("endCutscene", ""), ("stopObjectCode", ""));
        data.Globals[21] = Code(21, null, ("delay", "200"), ("loadRoom", "0x03"), ("stopObjectCode", ""));
        data.Globals[30] = Code(30, null, ("increment", "var[0x0D1]"), ("breakHere", ""), ("jumpRelative", "-3 -> 0x0000"));
        var session = new Session(data);
        session.Start(30);
        session.Start(20);
        Assert.True(session.InCutscene);
        Assert.False(session.CutsceneSkippable);
        var before = session.Snapshot;
        var result = session.PrepareManualRoom(session.Room(2), frameLimit: 400, quietFrames: 3);
        Assert.True(result.Succeeded, result.Reason);
        var copy = Assert.IsType<Session>(result.Session);
        Assert.True(session.InCutscene); // The source session was not published/mutated.
        Assert.Equal(before.RoomId, session.CurrentRoomId);
        for (var frame = 0; frame < 300; frame++) copy.AdvanceFrame();
        Assert.Equal(2, copy.CurrentRoomId);
        Assert.False(copy.InCutscene);
        Assert.Equal(0, copy.Snapshot.KnownGlobalVariables["var[0x0D0]"]);
        Assert.True(copy.Snapshot.KnownGlobalVariables["var[0x0D1]"] > before.KnownGlobalVariables["var[0x0D1]"]);
    }

    [Fact]
    public void DelayedEntryChildSceneFinishesOffscreenAndDoesNotAutoplayAfterPublication()
    {
        var data = new Data();
        data.Entries[2] = Code(2, "Room 02 entry", ("startScript", "0x0A, []"), ("stopObjectCode", ""));
        data.Globals[10] = Code(10, null, ("delay", "300"), ("cutscene", "[]"), ("delay", "10"),
            ("setState", "0x0011, 0x01"), ("endCutscene", ""), ("stopObjectCode", ""));
        var session = new Session(data);
        var result = session.PrepareManualRoom(session.Room(2), frameLimit: 500, quietFrames: 3);
        Assert.True(result.Succeeded, result.Reason);
        Assert.True(result.Frames > 300);
        Assert.Equal(1, result.Session!.Snapshot.ObjectStates[17]);
        Assert.Equal(0, session.Snapshot.ObjectStates[17]);
        var count = result.Session.CutscenesBegun;
        for (var frame = 0; frame < 400; frame++) result.Session.AdvanceFrame();
        Assert.Equal(count, result.Session.CutscenesBegun);
        Assert.False(result.Session.InCutscene);
    }

    [Fact]
    public void FrameBoundAndCancellationPublishNoPartialLiveSession()
    {
        var data = new Data();
        data.Entries[2] = Code(2, "Room 02 entry", ("delay", "300"), ("cutscene", "[]"), ("endCutscene", ""), ("stopObjectCode", ""));
        var session = new Session(data);
        Assert.False(session.PrepareManualRoom(session.Room(2), frameLimit: 2, quietFrames: 1).Succeeded);
        Assert.True(session.PrepareManualRoom(session.Room(2), cancellationToken: new CancellationToken(true)).Cancelled);
        Assert.Equal(1, session.CurrentRoomId);
    }

    [Fact]
    public void NativeEntryRoomLoadRefusesTheWrongRoomInsteadOfPlayingIt()
    {
        var data = new Data();
        data.Entries[2] = Code(2, "Room 02 entry", ("breakHere", ""), ("loadRoom", "0x03"), ("stopObjectCode", ""));
        var session = new Session(data);
        var result = session.PrepareManualRoom(session.Room(2), frameLimit: 10, quietFrames: 2);
        Assert.False(result.Succeeded);
        Assert.Equal(1, session.CurrentRoomId);
    }

    [Theory]
    [InlineData("mmansionE ega es")]
    [InlineData("zak ega en")]
    [InlineData("monkey1 ega en")]
    public void ExplicitIntroStartsBeforeTheBootLoopAndAssumesNoPlayerInput(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var session = ScummGameSession.StartIntro(new GameFolderScanner().Scan(path), out _);
        Assert.True(session.BootRunning);
        Assert.Null(session.BootReport);
        Assert.Equal(0, session.BootFrames);
        Assert.Empty(session.DeliveredInput);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.StartsWith("Player input assumed:", StringComparison.Ordinal));
    }

    [Fact]
    public void UnchosenNativePartyIsNotSilentlyPrepared()
    {
        var path = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        var result = session.PrepareManualRoom(game.Rooms.Single(r => r.ResourceId == 1));
        Assert.False(result.Succeeded);
        Assert.Contains("character-selection", result.Reason);
        Assert.False(session.InventoryCharactersAvailable);
    }
}


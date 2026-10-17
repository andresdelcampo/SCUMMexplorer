namespace SCUMMexplorer.Tests;

public sealed class ScummClassicCutsceneRestoreTests
{
    [Theory]
    [InlineData(ScummCamera.Normal)]
    [InlineData(ScummCamera.Panning)]
    public void ClassicNonFollowingCutsceneReturnsToSavedRoom(int mode)
    {
        if (Create("mmansion ega en") is not { } session) return;
        session.Begin(44, mode);
        session.MoveScene(45);
        session.End();
        Assert.Equal(mode, session.Mode);
        Assert.Equal(44, session.RequestedRoom);
        Assert.Equal("loadRoom", session.RequestedOperation);
        Assert.Equal(3, session.Cursor);
        Assert.Equal(1, session.Input);
    }

    [Fact]
    public void ClassicSameRoomRestoresModeWithoutInventingCameraPositionRestoration()
    {
        if (Create("mmansion ega en") is not { } session) return;
        session.Begin(44, ScummCamera.Panning);
        session.MoveScene(44);
        session.End();
        Assert.Equal(ScummCamera.Panning, session.Mode);
        Assert.Equal(240, session.Destination);
        Assert.Null(session.RequestedRoom);
    }

    [Theory]
    [InlineData("mmansion ega en", ScummCamera.FollowActor)]
    [InlineData("mmansionE ega es", ScummCamera.Normal)]
    [InlineData("zak ega en", ScummCamera.Normal)]
    public void ClassicFollowAndOtherOldFamiliesFollowTheCurrentEgo(string fixture, int savedMode)
    {
        if (Create(fixture) is not { } session) return;
        session.Begin(44, savedMode);
        session.MoveScene(45);
        session.End();
        Assert.Equal(ScummCamera.FollowActor, session.Mode);
        Assert.Equal(1, session.RequestedRoom);
        Assert.Equal("actorFollowCamera", session.RequestedOperation);
    }

    [Fact]
    public void ClassicCanRestoreNullRoom()
    {
        if (Create("mmansion ega en") is not { } session) return;
        session.Begin(0, ScummCamera.Normal);
        session.MoveScene(45);
        session.End();
        Assert.Equal(0, session.RequestedRoom);
    }

    private static Probe? Create(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!Directory.Exists(folder)) return null;
        var game = new GameFolderScanner().Scan(folder);
        return new Probe(game, (ScummV2DataSource)ScummDataSources.For(game));
    }

    private sealed class Probe(GameLibrary game, ScummV2DataSource data)
        : ScummSession(game, data.ReadIndexedObjects(), 0, null, data,
            _ => new(ScummSoundLifetimeKind.Unknown, null, false, "cutscene restore test"))
    {
        private static readonly ScummScriptEvent Event = new(1, "cutscene test", 0,
            ScummScriptEventKind.CutsceneEnded, "endCutscene", "", null, null, ScummScriptEventCertainty.Reached) { Executed = true };
        public int Mode => _runtime.Camera.Mode;
        public int Destination => _runtime.Camera.DestX;
        public int? RequestedRoom => _requestedLoad?.Event.TargetId;
        public string? RequestedOperation => _requestedLoad?.Event.Operation;
        public ushort Cursor => _runtime.GlobalVariables[ScummVars.V2.CursorState];
        public int Input => _runtime.UserPut;
        public void Begin(int room, int mode)
        {
            _runtime.CurrentRoom = room;
            _runtime.Camera.Mode = mode;
            _runtime.Camera.DestX = 160;
            _runtime.SetGlobalVariable(_runtime.EgoVariable, 1);
            _runtime.SetGlobalVariable(ScummVars.V2.CursorState, 3);
            _runtime.ApplyV2UserState(0xF7);
            _runtime.Actors.PutInRoom(1, 1, CurrentPlacement(), 1);
            _executing.Add(new ScriptFrame { Number = 1, Global = true });
            _dispatchScope = 1;
            BeginCutscene(Event, 0);
        }
        public void MoveScene(int room)
        {
            _runtime.CurrentRoom = room;
            _runtime.Camera.Mode = ScummCamera.Normal;
            _runtime.Camera.DestX = 240;
        }
        public void End() => EndCutscene(Event, 0);
        protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
            EnterRoom(target, "cutscene restore test", action.ReachedRoomLoad);
    }
}

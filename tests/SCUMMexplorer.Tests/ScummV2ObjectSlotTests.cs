namespace SCUMMexplorer.Tests;

public sealed class ScummV2ObjectSlotTests
{
    private static SlotSession? Session()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(folder);
        var data = new ScummV2DataSource(game);
        return new SlotSession(game, data);
    }

    [Fact]
    public void RestartReplacesOnlyMatchingObjectAndBothFlags()
    {
        if (Session() is not { } session) return;
        var ordinary = session.Start(671, false, false);
        var special = session.Start(671, false, true);
        var background = session.Start(671, true, true);
        var other = session.Start(672, false, true);
        var restarted = session.Start(671, false, true);
        Assert.True(session.Stopped(special));
        Assert.False(session.Stopped(ordinary));
        Assert.False(session.Stopped(background));
        Assert.False(session.Stopped(other));
        Assert.False(session.Stopped(restarted));
        Assert.Equal(session.Order(special), session.Order(restarted));
        Assert.Equal((false, true), session.Flags(restarted));
        Assert.Equal((true, true), session.Flags(background));
    }

    [Fact]
    public void StopObjectCancelsEveryFlagVariantButNotNumberedScripts()
    {
        if (Session() is not { } session) return;
        var ordinary = session.Start(671, false, false);
        var special = session.Start(671, false, true);
        var background = session.Start(671, true, true);
        var other = session.Start(672, false, true);
        var global = session.Global(671);
        Assert.Equal(3, session.Stop(671));
        Assert.True(session.Stopped(ordinary));
        Assert.True(session.Stopped(special));
        Assert.True(session.Stopped(background));
        Assert.False(session.Stopped(other));
        Assert.False(session.Stopped(global));
        Assert.Equal(0, session.Stop(671));
        Assert.Equal(0, session.Stop(0));
        var replacement = session.Start(671, false, true);
        Assert.False(session.Stopped(replacement));
    }

    private sealed class SlotSession(GameLibrary game, ScummV2DataSource data)
        : ScummSession(game, data.ReadIndexedObjects(), 0, null, data,
            _ => new(ScummSoundLifetimeKind.Unknown, null, false, "slot test"))
    {
        public object Start(int id, bool background, bool special)
        {
            var frame = ReplaceV2ObjectSlot(id, background, special);
            _executing.Add(frame);
            return frame;
        }
        public object Global(int id)
        {
            var frame = new ScriptFrame { Number = id, Global = true };
            _executing.Add(frame);
            return frame;
        }
        public bool Stopped(object frame) => ((ScriptFrame)frame).Stopped;
        public long Order(object frame) => ((ScriptFrame)frame).ObjectSlotOrder;
        public (bool, bool) Flags(object frame) => (((ScriptFrame)frame).FreezeResistant, ((ScriptFrame)frame).Recursive);
        public int Stop(int id) => StopV2ObjectScripts(id);
        protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
            EnterRoom(target, "slot test", action.ReachedRoomLoad);
    }
}

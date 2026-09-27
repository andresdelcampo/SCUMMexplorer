using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomSoundCueTests
{
    [Fact]
    public void ReachedStartSoundResolvesKnownScriptLocal()
    {
        var script = new LoomScriptDisassembly(39, 1, 0, 3, 3,
            [new LoomScriptInstruction(0, 0x9C, "startSound", "local[0x000]", "9C 00"),
                new LoomScriptInstruction(2, 0, "stopObjectCode", "", "00")],
            null, null, [], []);
        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte>(), [],
            initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 31 });

        Assert.Equal(31, Assert.Single(run.Events).TargetId);
    }

    [Fact]
    public void ReachedStopRemovesInheritedCue()
    {
        var start = Event(LoomScriptEventKind.SoundStarted, 31);
        var effect = Event(LoomScriptEventKind.SoundStarted, 3);
        var stop = Event(LoomScriptEventKind.SoundStopped, 31);

        Assert.Equal(31, LoomSoundCueResolver.Resolve([start, effect], id => id == 31).Start?.TargetId);
        Assert.Null(LoomSoundCueResolver.Resolve([start, effect, stop], id => id == 31).Start);
        Assert.Null(LoomSoundCueResolver.Resolve([start, Event(LoomScriptEventKind.MusicStopped, null)],
            id => id == 31).Start);
    }

    [Fact]
    public void UnknownStopPreventsClaimingCurrentCue()
    {
        var resolved = LoomSoundCueResolver.Resolve(
            [Event(LoomScriptEventKind.SoundStarted, 31), Event(LoomScriptEventKind.SoundStopped, null)],
            id => id == 31);

        Assert.Null(resolved.Start);
        Assert.True(resolved.HasUnknownSoundTarget);
    }

    private static LoomScriptEvent Event(LoomScriptEventKind kind, int? id) =>
        new(39, "Script 39", 0, kind, kind.ToString(), "", null, id,
            LoomScriptEventCertainty.Reached);
}

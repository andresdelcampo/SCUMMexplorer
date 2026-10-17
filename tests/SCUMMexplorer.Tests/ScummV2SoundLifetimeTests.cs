namespace SCUMMexplorer.Tests;

public sealed class ScummV2SoundLifetimeTests
{
    [Fact]
    public void ZakOpeningSoundsFinishAndItsDreamMusicPublishesScriptTimerValues()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var player = new ScummPcSpeakerAudio();
        ScummSoundLifetime Read(int id) => player.ReadV2Lifetime(game.Resources.Single(r => r.Kind == ResourceKind.Sound && r.ResourceId == id));
        Assert.Equal((ScummSoundLifetimeKind.Finite, 5714), (Read(81).Kind, Read(81).Callbacks));
        var dream = Read(95);
        Assert.Equal(ScummSoundLifetimeKind.Finite, dream.Kind);
        Assert.Equal(41090, dream.Callbacks);
        Assert.Contains(dream.TimerValues!, p => p.Value == 4);
        Assert.Equal(42, dream.TimerValues![^1].Value);
    }

    private static byte[] Resource(params byte[] commands)
    {
        var bytes = new byte[22 + commands.Length];
        bytes[6] = 22;
        commands.CopyTo(bytes, 22);
        return bytes;
    }

    [Fact]
    public void SoundScriptTimerValuesCanDecreaseAndCompletionExcludesPcmRelease()
    {
        // FF parameter 46 is the channel's music_timer, FF parameter zero ends the control slice.
        var result = new ScummPcSpeakerAudio().ReadV2Lifetime(Resource(0xFF, 46, 7, 0, 0xFF, 0, 2, 0, 0xFF, 46, 1, 0, 0xFF, 0, 0, 0));
        Assert.Equal(ScummSoundLifetimeKind.Finite, result.Kind);
        Assert.Equal(6, result.Callbacks);
        Assert.Equal(new[] { new ScummSoundTimerPoint(0, 0), new(2, 7), new(6, 1) }, result.TimerValues);
        var clock = new ScummSoundClock(_ => result);
        clock.Start(1, false);
        clock.Advance(1);
        Assert.False(clock.Status(1).Running);
        Assert.Equal(1, clock.MusicTimer());
    }

    [Fact]
    public void ExactControlStateRecurrenceProvesALoopButTruncatedCodeDoesNot()
    {
        var player = new ScummPcSpeakerAudio();
        var loop = player.ReadV2Lifetime(Resource(0xFF, 46, 1, 0, 0xFF, 0, 1, 0, 0xFE, 38, 0xF4, 0xFF));
        Assert.Equal(ScummSoundLifetimeKind.Looping, loop.Kind);
        Assert.Equal(2, loop.LoopStartCallback);
        Assert.Equal(4, loop.LoopEndCallback);
        Assert.Equal(ScummSoundLifetimeKind.Unknown, player.ReadV2Lifetime(Resource(0xFF)).Kind);
    }
}

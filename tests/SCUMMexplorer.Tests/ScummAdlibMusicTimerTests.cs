namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b (14 October 2026): the AdLib player's music timer (ScummVM <c>player_ad.cpp</c>, <c>sound.cpp</c> <c>updateMusicTimer</c>), which the
/// Indiana Jones and the Last Crusade EGA opening waits on (script 001 +0x01EE: <c>var[0x00E]</c> reaches 22, Room 79's local script 200 up to 105).
/// </summary>
public sealed class ScummAdlibMusicTimerTests
{
    [Fact]
    public void TheAdlibMusicTimerHoldsItsLastValueAfterTheSongEndsUntilTheNextSongStarts()
    {
        // player_ad.cpp: _engineMusicTimer is reset by startMusic only; stopMusic leaves it. Roland (iMuse) songs report 0 when none plays.
        var song = new ScummSoundLifetime(ScummSoundLifetimeKind.Finite, 472, true, MarkerTicks: [60, 120, 180], KeepsMusicTimer: true);
        var roland = new ScummSoundLifetime(ScummSoundLifetimeKind.Finite, 472, true, MarkerTicks: [60, 120, 180]);
        var clock = new ScummSoundClock(id => id == 1 ? song : roland);

        clock.Start(1, false);
        clock.Advance(60);
        Assert.Equal(3, clock.MusicTimer());
        clock.Advance(60);
        Assert.False(clock.Status(1).Running);
        Assert.Equal(3, clock.MusicTimer());

        clock.Start(1, false);
        Assert.Equal(0, clock.MusicTimer());

        var other = new ScummSoundClock(_ => roland);
        other.Start(2, false);
        other.Advance(120);
        Assert.Equal(0, other.MusicTimer());
    }

    [Fact]
    public void TheAdlibTimingConstantsFollowPlayerAd()
    {
        // player_ad.cpp startMusic: LOOM uses limit 473, tempo*2 and delays >> 2; every other game 256, tempo*1, >> 1.
        Assert.Equal(new ScummAdlibTiming(473, 2, 2), ScummAdlibTiming.Loom);
        Assert.Equal(new ScummAdlibTiming(256, 1, 1), ScummAdlibTiming.Standard);
    }
}

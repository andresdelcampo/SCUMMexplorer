using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 sound during play (16 October 2026): what the engine stops when music starts comes from the profile (ScummVM <c>setupMusic</c>: Player_AD for
/// Indiana Jones, iMuse with the Monkey Island workaround of <c>Sound::triggerSound</c>). The routes' sound ids are pinned in
/// <see cref="ScummIndy3PlayTests"/> and <see cref="ScummMonkeyFloppyPlayTests"/>. Record: <c>.verification/monkey1-play-20261016/README.md</c> (addendum).
/// </summary>
public sealed class ScummPlaySoundTests
{
    [Theory]
    [InlineData("indy3 ega en", ScummMusicStart.StopsPreviousMusic)]
    [InlineData("indy3 vga en", ScummMusicStart.StopsPreviousMusic)]
    [InlineData("monkey1 ega en", ScummMusicStart.StopsAllSounds)]
    [InlineData("loom ega mt32 en", ScummMusicStart.StopsPreviousMusic)]
    public void WhatAMusicStartStopsComesFromTheProfile(string folderName, ScummMusicStart expected)
    {
        var folder = Path.Combine(TestGames.Root, folderName);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(expected, ScummProfile.For(game).MusicStart);
    }

    [Fact]
    public void TheBarMusicIsNotStartedAgainWhileItPlays()
    {
        // Room 28's entry asks isSoundRunning var[0x04B] (+0x0080) and starts the bar music only when it is silent (+0x008A): a second entry while the
        // music (101, a looping song on the session's AdLib clock) still plays starts nothing.
        var folder = Path.Combine(TestGames.Root, "monkey1 ega en");
        if (!File.Exists(Path.Combine(folder, "DISK01.LEC"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV4DiskSetDataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
        var bar = game.Resources.First(r => r.Kind == ResourceKind.Room && r.ResourceId == 28);
        session.VisitRoom(bar);
        for (var i = 0; i < 60; i++) session.AdvanceFrame();
        Assert.Contains(101, session.Snapshot.ActiveSounds);
        // The dock (Room 33) is visited without the bar door (object 322 verb 0x0A stops the music, +0x0006), then the bar again.
        session.VisitRoom(game.Resources.First(r => r.Kind == ResourceKind.Room && r.ResourceId == 33));
        for (var i = 0; i < 30; i++) session.AdvanceFrame();
        session.VisitRoom(bar);
        for (var i = 0; i < 60; i++) session.AdvanceFrame();
        Assert.Single(session.SoundEvents, item => item.Event is { Kind: ScummScriptEventKind.SoundStarted, TargetId: 101 });
        Assert.Contains(101, session.Snapshot.ActiveSounds);
    }
}

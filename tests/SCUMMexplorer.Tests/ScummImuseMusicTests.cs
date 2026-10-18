using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Shared play addition of S6 Monkey2 (17 October 2026): iMUSE music in play. The songs a v5 game's scripts start play through the S3 player of the chosen
/// device (<see cref="ScummImuseMusic.Part"/>), and a song that jumps back keeps looping from its loop point (<see cref="ScummImuseMusic.Loop"/>) until a
/// script stops it. Skips without <c>mi2 talkie en</c>.
/// </summary>
public sealed class ScummImuseMusicTests(ITestOutputHelper output)
{
    [Fact]
    public void MonkeyIsland2sSongsHaveAPartPerDeviceAndTheLoopingOnesALoopPoint()
    {
        var folder = Path.Combine(TestGames.Root, "mi2 talkie en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var music = ScummImuseMusic.For(game);
        Assert.NotNull(music);
        // Sound 154 is the opening's song (Script 52 +0x0017); every card has its part.
        foreach (var device in Enum.GetValues<ScummImuseDevice>()) Assert.NotNull(music!.Part(154, device));
        var looping = 0;
        foreach (var id in Enumerable.Range(1, 200))
            if (music!.Loop(id, ScummImuseDevice.Adlib) is { } loop)
            {
                looping++;
                Assert.InRange(loop.RestartSeconds, 0, loop.EndSeconds - 0.001);
                Assert.True(loop.EndSeconds >= ScummImuseTimeline.MinLoopSeconds);
            }
        output.WriteLine($"{looping} looping songs of 200 ids (AdLib)");
        Assert.True(looping > 0);
        // A sound the game does not have, or a sampled effect without card parts, has no loop.
        Assert.Null(music!.Loop(9999, ScummImuseDevice.Roland));
    }

    [Fact]
    public void TheSongsTheBootStartsAreExecutedSoundEventsForThePlayer()
    {
        var folder = Path.Combine(TestGames.Root, "mi2 talkie en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        // The scene sound player plays executed starts: the opening's song through Script 52 +0x0017 is one.
        Assert.Contains(session.SoundEvents, item => item.Event is { Kind: ScummScriptEventKind.SoundStarted, TargetId: 154, Executed: true, Certainty: ScummScriptEventCertainty.Reached });
    }
}

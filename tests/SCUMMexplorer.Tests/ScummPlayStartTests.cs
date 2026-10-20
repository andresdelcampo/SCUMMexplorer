using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Play start in seconds (20 October 2026, MI2 play-start regression): what the window does when the user opens a generic-session game and picks a room
/// (<c>MainWindow.StartPlayCoreAsync</c>: <see cref="ScummGameSession.BootForPlay"/> off the UI thread, then the catalog preparation
/// <see cref="ScummSession.PrepareManualRoom"/>; R-5.3a, R-8.5b) finishes in seconds and leaves a live room with the game's input script, for the boot's
/// room or, when that room's native entry is not quiet (MI2's campfire goes on to Part One and Woodtick by itself), for the first live room in index order.
/// MI2's Room 4 is a static preview since 273cc399 (19 October, R-8.5b): a harness that waits for play there waits for ever. Record:
/// <c>.verification/mi2-play-start-20261020/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummPlayStartTests(ITestOutputHelper output)
{
    // Measured 0.3–2.2 s per game (boot plus preparations) on 20 October; the bound leaves room for a loaded machine and parallel tests.
    private const double BoundSeconds = 20;

    public static TheoryData<string> Copies => new() { "mi2 talkie en", "mi2 mt32 en", "indy4 vga en", "monkey1 vga en", "indy3 ega en" };

    private static string? Folder(string copy)
    {
        var path = Path.Combine(TestGames.Root, copy);
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any() ? path : null;
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void PlayReachesALiveRoomWithinSeconds(string copy)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var watch = Stopwatch.StartNew();
        var boot = ScummGameSession.BootForPlay(game, out _);
        var booted = watch.Elapsed.TotalSeconds;
        Assert.True(boot.PlayInputScript != 0, "the boot did not establish the game's input script");
        ScummGameSession? live = null;
        foreach (var room in game.Rooms.OrderBy(r => r.ResourceId == boot.CurrentRoomId ? 0 : 1).Take(12))
        {
            var prepared = boot.PrepareManualRoom(room);
            output.WriteLine($"Room {room.ResourceId}: {prepared.Frames} frames, {(prepared.Succeeded ? "live" : prepared.Reason)}");
            if (prepared.Session is ScummGameSession { PlayInputScript: not 0 } ready) { live = ready; break; }
        }
        Assert.NotNull(live);
        var room0 = live.CurrentRoomId;
        for (var frame = 0; frame < 30; frame++) live.AdvanceFrame();
        var total = watch.Elapsed.TotalSeconds;
        output.WriteLine($"{copy}: boot {booted:0.00} s (Room {boot.CurrentRoomId}), live Room {room0}, with 30 frames {total:0.00} s");
        Assert.True(total < BoundSeconds, $"{copy}: play took {total:0.0} s to reach a live room (bound {BoundSeconds} s)");
    }

    [Fact]
    public void Monkey2CampfireIsAStaticPreviewAndWoodtickIsLive()
    {
        if (Folder("mi2 talkie en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var boot = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(4, boot.CurrentRoomId);
        // Room 4's entry tells the campfire story and leaves for Room 1 (Part One) and Woodtick: no quiet room, so the window keeps a static preview (R-8.5b).
        var campfire = boot.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == 4));
        Assert.False(campfire.Succeeded);
        Assert.StartsWith("Native entry left the selected room", campfire.Reason, StringComparison.Ordinal);
        var woodtick = boot.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == 7));
        Assert.True(woodtick.Succeeded, woodtick.Reason);
        Assert.Equal(7, woodtick.Session!.CurrentRoomId);
    }
}

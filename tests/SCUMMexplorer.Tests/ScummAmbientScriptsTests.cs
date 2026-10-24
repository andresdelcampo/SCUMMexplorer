using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// R-8.5b ambient scripts (20 October 2026): which scripts a room's entry starts count as the room's permanent animation, decided from the game's own
/// code (<see cref="ScummAmbientScripts"/>). Record: <c>.verification/static-preview-controls-20261020/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummAmbientScriptsTests(ITestOutputHelper output)
{
    private static string? Folder(string copy)
    {
        var path = Path.Combine(TestGames.Root, copy);
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any() ? path : null;
    }

    [Theory]
    // MI2's campfire: script 200 (through 205) loops the pirates' and fire's animation; 204 and 206 open cutscenes (the story).
    [InlineData("mi2 talkie en", 4, new[] { 200 }, new[] { 204, 206 })]
    // LOOM's Room 02: script 042 is the swan (LOOM_SCRIPT_MAP), 039 the looping sound player.
    [InlineData("loom ega mt32 en", 2, new[] { 42, 39 }, new int[0])]
    // Indy3's Room 04: script 200 animates an actor and its sounds in a loop.
    [InlineData("indy3 ega en", 4, new[] { 200 }, new int[0])]
    public void ClassifiesFromTheRoomsCode(string copy, int room, int[] ambient, int[] story)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var result = ScummAmbientScripts.Classify(ScummDataSources.For(game), room);
        foreach (var item in result.Ambient) output.WriteLine($"+ {item.Evidence}");
        foreach (var item in result.Rejected) output.WriteLine($"- {item.ScriptId}: {item.Reason}");
        foreach (var id in ambient) Assert.True(result.IsAmbient(id), $"script {id} should be ambient");
        foreach (var id in story) Assert.Contains(result.Rejected, r => r.ScriptId == id && r.Reason.Contains("cutscene", StringComparison.Ordinal));
    }

    [Fact]
    public void Monkey2CampfirePreviewRunsOnlyItsAnimation()
    {
        if (Folder("mi2 talkie en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var boot = ScummGameSession.BootForPlay(game, out var objects);
        var room = game.Rooms.First(r => r.ResourceId == 4);
        Assert.False(boot.PrepareManualRoom(room).Succeeded); // R-8.5b: the campfire's entry tells the story and leaves.
        var preview = boot.PrepareAmbientRoom(room);
        var session = Assert.IsType<ScummGameSession>(preview.Session);
        var screen = new ScummPlayScreen(game, session, objects.States);
        var pictures = new HashSet<int>();
        for (var frame = 0; frame < 300; frame++)
        {
            session.AdvanceFrame();
            Assert.Equal(4, session.CurrentRoomId);
            Assert.False(session.InCutscene);
            Assert.Equal(0, session.Messages.HaveMsg);
            if (frame % 10 == 0) pictures.Add(screen.Compose(frame).Pixels.Aggregate(17, (h, b) => unchecked(h * 31 + (int)b)));
        }
        output.WriteLine($"{pictures.Count} distinct pictures in 30 samples; {string.Join(" | ", session.Snapshot.Uncertainties.TakeLast(2))}");
        Assert.True(pictures.Count > 1, "the campfire's animation should move");
        Assert.Equal(4, boot.CurrentRoomId); // the live session is not modified
    }

    [Theory]
    [InlineData("mi2 talkie en")]
    [InlineData("loom ega mt32 en")]
    [InlineData("indy3 ega en")]
    [InlineData("monkey1 vga en")]
    [InlineData("indy4 vga en")]
    public void Survey(string copy)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var rooms = 0;
        foreach (var room in game.Rooms)
        {
            var result = ScummAmbientScripts.Classify(data, room.ResourceId!.Value);
            if (result.Ambient.Count == 0 && result.Rejected.Count == 0) continue;
            if (result.Ambient.Count > 0) rooms++;
            output.WriteLine($"Room {room.ResourceId}: ambient [{string.Join(", ", result.Ambient.Select(a => a.ScriptId))}]" +
                             string.Concat(result.Ambient.Select(a => $"\n    + {a.Evidence}")) +
                             string.Concat(result.Rejected.Select(r => $"\n    - {r.ScriptId}: {r.Reason}")));
        }
        output.WriteLine($"{copy}: {rooms} rooms with ambient scripts");
    }
}

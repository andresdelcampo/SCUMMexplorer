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

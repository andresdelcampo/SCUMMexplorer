using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public class SpeechSoundsTests
{
    /// <summary>Every talkie game found under Games/FUTURE lists each sample of its speech file as a sound, and the first one decodes.</summary>
    [Fact]
    public void TalkieGamesListTheirSpeechAsSounds()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Games", "FUTURE"));
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.GetDirectories(root).Where(d => Directory.GetFiles(d, "*.sou").Length > 0))
        {
            var game = new GameFolderScanner().Scan(folder);
            Assert.NotNull(game.Speech);
            var speech = game.Resources.Where(r => r.ResourceId >= GameLibrary.SpeechIdBase).ToList();
            Assert.True(speech.Count > 1000, folder);
            var first = speech[0];
            var wave = game.Speech!.ReadWave(first.Offset!.Value, first.ContainerNumberOffset!.Value);
            Assert.True(wave.Length > 44);
        }
    }
}

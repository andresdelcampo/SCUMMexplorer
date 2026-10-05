using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Storage;

namespace SCUMMexplorer.Tests;

public sealed class CdIntroExperiment
{
    [Fact]
    public void Run()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_INTRO_OUT");
        if (outPath is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var cd = game.Vga!.ReadIndexedObjects();
        var session = new LoomExplorationSession(game, cd, stopAtOpening: Environment.GetEnvironmentVariable("CD_STOP") == "1");
        var log = new List<string>();
        int? room = null; var seenMsg = new HashSet<string>(); var voices = 0; var sounds = 0;
        for (var frame = 0; frame < 6000; frame++)
        {
            var snap = session.Snapshot;
            if (snap.RoomId != room) { room = snap.RoomId; log.Add($"frame {frame}: room {room} pending {snap.PendingScripts}"); }
            foreach (var m in session.Messages.OnScreen) { var key = $"{m.Charset} {m.X},{m.Y} {string.Join("|", m.Lines)}"; if (seenMsg.Add(key)) log.Add($"frame {frame}: message {key}"); }
            while (voices < session.Messages.VoiceEvents.Count) log.Add($"frame {frame}: voice {session.Messages.VoiceEvents[voices++]}");
            while (sounds < session.SoundEvents.Count) log.Add($"frame {frame}: sound {session.SoundEvents[sounds++].Event.Operation} {session.SoundEvents[sounds - 1].Event.Operands}");
            if (snap.PendingScripts == 0 && frame > 5) { log.Add($"frame {frame}: no pending scripts"); break; }
            session.AdvanceFrame();
        }
        var pend = (System.Collections.IEnumerable)typeof(LoomExplorationSession).GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        foreach (var p in pend) log.Add("P " + string.Join(", ", p.GetType().GetProperties().Where(pr => pr.Name != "Script").Select(pr => { try { return pr.Name + "=" + pr.GetValue(p); } catch { return pr.Name; } })));
        log.AddRange(session.Snapshot.KnownGlobalVariables.Where(kv => kv.Key.Contains("0x00B") || kv.Key.Contains("0x00E")).Select(kv => "V " + kv.Key + "=" + kv.Value));
        log.AddRange(session.SessionTrace.Select(u => "T " + u));
        log.AddRange(session.Snapshot.Uncertainties.Where(u => !u.StartsWith("Script 124")).Select(u => "U " + u));
        File.WriteAllLines(outPath, log);
    }
}

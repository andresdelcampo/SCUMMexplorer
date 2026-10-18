using System.Globalization;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S7 Cutscenes for Monkey Island 2: LeChuck's Revenge (SCUMM v5, fixture <c>mi2 talkie en</c>; 18 October 2026): the engine's scan (<see cref="ScummCutsceneIndex"/>)
/// lists the game's cutscene triggers from its resource file, titled with the verb names its verb script gives after the boot (2 Open … 8 Look at, 9 Pick up,
/// 0x0A Talk to, 0x0B Walk to), and three scenes begin from a fresh start in a headless run of the generic session (<see cref="ScummCutsceneRun"/>): in Woodtick,
/// Talk to barkeeper in the Bloody Lip (Room 9 object 73 verb 0x0A, "Yeah, boy?") and the entry of Largo's room at the inn (Room 13, "Who let you into my
/// room?!!", which goes on to Room 12), and in Part 3 Pick up voodoo key to jail (Room 74 object 755 verb 0x09, global script 76: LeChuck comes in). Script 76
/// starts its iMUSE song (sound 176) and waits for the song's beat 9 (<c>soundKludge</c> player command 0, <c>getParam</c> 7) before LeChuck walks in; the
/// session answers the beat from the song's AdLib timeline (an estimate). The talkie's speech codes (four <c>\xFF\x0A</c> controls) leave no letters in the first
/// texts. Registry: Cutscenes ◐. Record: <c>.verification/monkey2-cutscenes-20261018/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummMonkey2CutsceneTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "mi2 talkie en");
        return File.Exists(Path.Combine(path, "MONKEY2.000")) ? path : null;
    }

    private const int PickUp = 0x09, TalkTo = 0x0A;

    private static readonly Lazy<ScummCutscenePlanner?> Planner =
        new(() => Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null);

    private static ScummCutsceneGroup GroupOf(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        Assert.Single(planner.Groups, g => g.First.Room == room && (g.First.ObjectId == objectId && g.First.VerbId == verb ||
                                                                   g.Others.Any(o => o.ObjectId == objectId && o.VerbId == verb)));

    [Fact]
    public void TheScanTitlesWithTheGamesOwnVerbNames()
    {
        if (Planner.Value is not { } planner) return;
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes");
        Assert.Equal(GameFamily.Monkey2, GameSupport.FamilyOf(new GameFolderScanner().Scan(Folder()!)));
        Assert.Equal("Look at", planner.Verbs.WordFor(0x08));
        Assert.Equal("Pick up", planner.Verbs.WordFor(PickUp));
        Assert.Equal("Talk to", planner.Verbs.WordFor(TalkTo));
        Assert.Equal("Walk to", planner.Verbs.WordFor(0x0B));
        Assert.True(planner.Groups.Count >= 250, $"only {planner.Groups.Count} groups");

        var barkeeper = GroupOf(planner, 9, 73, TalkTo);
        Assert.Equal("Room 09 · Talk to barkeeper", ScummCutsceneTitles.Title(barkeeper.First));
        Assert.Equal("Yeah, boy?", barkeeper.First.FirstText);
        var largo = GroupOf(planner, 13, 0, 0);
        Assert.Equal("Room 13 · Entry", ScummCutsceneTitles.Title(largo.First));
        Assert.True(largo.IsScene);
        // The line's four speech controls (\xFF\x0A and two bytes each, often letters) are not part of the text.
        Assert.Equal("Who let you into my room?!!", largo.First.FirstText);
        var key = GroupOf(planner, 74, 755, PickUp);
        Assert.Equal("Room 74 · Pick up voodoo key to jail", ScummCutsceneTitles.Title(key.First));
        Assert.Contains("global script 76", key.First.Scripts);
        // A verb the game's verb script does not name keeps its number (the code an inventory object reaches when given to someone).
        Assert.Equal("Room 22 · verb 0x50 Captain Dread", ScummCutsceneTitles.Title(GroupOf(planner, 22, 239, 0x50).First));
    }

    [Fact]
    public void ThreeScenesBeginFromAFreshStart()
    {
        if (Planner.Value is not { } planner) return;
        foreach (var (room, objectId, verb) in new[] { (9, 73, TalkTo), (13, 0, 0), (74, 755, PickUp) })
        {
            var group = GroupOf(planner, room, objectId, verb);
            var run = planner.Run(group.First);
            output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames, rooms [{string.Join(", ", run.RoomsEntered)}] {run.Failure}");
            Assert.True(run.Begun, $"Room {room} object {objectId} verb 0x{verb:X2}: {run.Failure}");
            Assert.StartsWith("A fresh start begins its cutscene", planner.StartNote(group.First), StringComparison.Ordinal);
        }
        // Largo's room: the scene goes on in the inn's front room.
        Assert.Contains(12, planner.Run(GroupOf(planner, 13, 0, 0).First).RoomsEntered);
    }

    [Fact]
    public void LeChucksSceneStartsItsSongAndFollowsItsBeat()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 74));
        var at = session.SoundEvents.Count;
        session.ActivateObjectVerb(755, PickUp);
        // Script 76 +0x0019 starts sound 176 (0xB0), an iMUSE song of the resource file.
        Assert.Contains(session.SoundEvents.Skip(at), item => item.Event.Kind == ScummScriptEventKind.SoundStarted && item.Event.TargetId == 176 &&
                                                              item.Event.SourceLabel == "Script 76" && item.Event.Offset == 0x0019 && item.Event.Executed);
        // Script 76 waits at +0x0090..+0x00DF for beat 9 of the song; once the session answers the beat, LeChuck walks in and the script goes on.
        int Offset() => session.PendingScriptSummary.Where(s => s.StartsWith("Script 76 +0x", StringComparison.Ordinal))
            .Select(s => int.Parse(s.AsSpan("Script 76 +0x".Length, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).DefaultIfEmpty(-1).First();
        var frames = 0;
        while (frames < 3000 && Offset() is >= 0 and <= 0x00E6)
        {
            session.AdvanceFrame();
            frames++;
        }
        output.WriteLine($"Script 76 at +0x{Offset():X4} after {frames} frames; {string.Join(" | ", session.PendingScriptSummary)}");
        Assert.True(Offset() > 0x00E6, "Script 76 never got past the song's beat");
        Assert.True(session.InCutscene);
    }
}

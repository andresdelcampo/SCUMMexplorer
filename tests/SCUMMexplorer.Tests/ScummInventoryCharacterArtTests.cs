using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummInventoryCharacterArtTests
{
    [Theory]
    [InlineData("mmansion ega en", 3)]
    [InlineData("mmansionE ega es", 3)]
    [InlineData("zak ega en", 4)]
    [InlineData("zakE ega en", 4)]
    public void EveryEligibleCharacterHasDetachedNativeFullBodyWithoutChangingState(string folder, int count)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        if (folder.StartsWith("mmansion")) ManiacCutsceneSetup.PrepareIfNeeded(game, session);
        var runtime = Runtime(session);
        string State() => JsonSerializer.Serialize(new { Actors = session.Actors, Variables = runtime.GlobalVariables,
            Owners = runtime.ObjectOwners, States = runtime.ObjectStates, Slots = runtime.InventorySlots, Trace = session.SessionTrace });
        var before = State();
        var characters = session.CaptureInventoryStatus().Characters;
        Assert.Equal(count, characters.Count);
        foreach (var character in characters)
        {
            var actor = Assert.IsType<ScummActorSnapshot>(session.ReadInventoryCharacterAppearance(character.ActorId, out var evidence));
            Assert.False(string.IsNullOrWhiteSpace(evidence));
            var display = actor with { Room = 1, X = 96, Y = 224, Visible = true, WalkBox = null,
                Look = actor.Look with { Elevation = 0, Scale = 255 } };
            var layer = ScummActorRenderer.Render([display], 1, 256, 256, session.ReadCostume, engineVersion: (int)game.Version);
            var pixels = Enumerable.Range(0, layer.Pixels.Length).Where(layer.IsDrawn).ToArray();
            Assert.NotEmpty(pixels);
            Assert.True(pixels.Max(i => i / 256) - pixels.Min(i => i / 256) >= 20, $"Actor {character.ActorId} must show native body art.");
            actor.Look.Animation.Reset(); // Caller-owned limb state cannot damage live animation.
        }
        Assert.Null(session.ReadInventoryCharacterAppearance(9, out _));
        Assert.Equal(before, State());
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("mmansionE ega es")]
    public void PartyRecipientsWaitForNativeStartAndExcludeUnselectedChildren(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.False(session.InventoryCharactersAvailable);
        Assert.Empty(session.CaptureInventoryStatus().Characters);
        foreach (var point in new[] { (76, 104), (196, 104) })
        {
            session.Click(point.Item1, point.Item2);
            for (int i = 0; i < 100; i++) session.AdvanceFrame();
        }
        Assert.False(session.InventoryCharactersAvailable); // All three slots are filled, but Start was not chosen.
        Assert.Empty(session.CaptureInventoryStatus().Characters);
        session.Click(292, 60);
        for (int i = 0; i < 100; i++) session.AdvanceFrame();
        for (int i = 0; i < 5000 && (!session.PlayerHasControl || !session.InventoryCharactersAvailable); i++) session.AdvanceFrame();
        Assert.True(session.InventoryCharactersAvailable, $"room={session.CurrentRoomId}; control={session.PlayerHasControl}; {session.CaptureInventoryStatus().Refusal}");
        for (int i = 0; i < 2500 && !session.CaptureInventoryStatus().CanEdit; i++) session.AdvanceFrame();
        var view = session.CaptureInventoryStatus();
        Assert.Equal(new[] { 1, 3, 5 }, view.Characters.Select(c => c.ActorId));
        Assert.False(session.ApplyInventoryEdits(view, [new(55, 2)]).Applied);
        var grant = session.ApplyInventoryEdits(view, [new(55, 5)]);
        Assert.True(grant.Applied, grant.Refusal);
    }

    private static ScummScriptRuntimeState Runtime(ScummSession session) =>
        (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
}

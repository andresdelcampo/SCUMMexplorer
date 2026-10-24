using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// R-1.5 / R-6.25.2 (user, 24 Oct 2026): a double click looks at the object where the game's own input leaves the double click free
/// (<see cref="ScummProfile.DoubleClickLookVerb"/>). Evidence of each game's input chain: <c>.verification/doubleclick-20261024/README.md</c>.
/// </summary>
public sealed class ScummDoubleClickLookTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("indy3 ega en", null)]
    [InlineData("indy3 vga en", null)]
    [InlineData("mmansion ega en", null)]
    [InlineData("mmansionE ega es", null)]
    [InlineData("zak ega en", null)]
    [InlineData("zakE ega en", null)]
    [InlineData("loom ega mt32 en", null)]
    [InlineData("monkey1 ega en", 0x09)]
    [InlineData("monkey1 vga en", 0x08)]
    [InlineData("mi2 mt32 en", 0x08)]
    [InlineData("indy4 vga en", 0x09)]
    public void OnlyTheGamesWhoseInputLeavesTheDoubleClickFreeRunTheirLookVerb(string copy, int? verb)
    {
        var folder = Path.Combine(TestGames.Root, copy);
        if (!Directory.Exists(folder)) return;
        Assert.Equal(verb, ScummProfile.For(new GameFolderScanner().Scan(folder)).DoubleClickLookVerb);
    }

    [Fact]
    public void ADoubleClickOnTheMarqueeRunsTheGamesOwnLookAtSentenceInIndy4()
    {
        if (ScummIndy4PlayTests.Folder("indy4 vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        void Run(int frames) { for (var i = 0; i < frames; i++) session.AdvanceFrame(); }
        bool RunUntil(Func<bool> condition, int limit)
        {
            for (var i = 0; i < limit && !condition(); i++) session.AdvanceFrame();
            return condition();
        }
        bool Idle() => session.UserInputOn && !session.InCutscene;
        // The prologue's statue leads on to Room 5; Esc takes its override and New York (Room 10) has the full interface.
        session.PointAtScreen((80, 92));
        Run(2);
        Assert.NotNull(session.ClickScreen(80, 92 + session.MainScreen.Top, out _));
        Assert.True(RunUntil(() => session.CurrentRoomId == 5 && Idle(), 1500));
        session.PressKey(0x1B);
        Assert.True(RunUntil(() => Idle() && session.CurrentRoomId == 10 && session.Verbs.Any(v => v.VerbId == 0x03 && v.Mode == 1), 3000));

        // A double click on the marquee (object 142 at room (116, 16)): the second press looks at it, as clicking Look at and then the marquee would.
        var screenX = 116 - session.ViewLeft;
        var y = 16 + session.MainScreen.Top;
        Assert.NotNull(session.ClickScreen(screenX, y, out _));
        Run(3);
        output.WriteLine("first press: " + string.Join(" | ", session.SessionTrace.TakeLast(3)));
        var input = session.DoubleClickLook(screenX, y, out var refused);
        Assert.True(input is not null, refused);
        var looked = RunUntil(() => session.SessionTrace.Any(line => line.StartsWith("sentence script 2 runs verb 0x09 with objects 142", StringComparison.Ordinal)), 900);
        foreach (var delivered in session.DeliveredInput.TakeLast(6)) output.WriteLine("delivered: " + delivered.Description);
        foreach (var line in session.SessionTrace.Where(line => line.Contains("sentence", StringComparison.OrdinalIgnoreCase) || line.Contains("doSentence", StringComparison.Ordinal)).TakeLast(10)) output.WriteLine("trace: " + line);
        Assert.True(looked, "the Look at sentence did not run");
    }

    [Fact]
    public void AGameWhoseInputUsesTheSecondClickRefusesTheLookAndKeepsTwoClicks()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 ega en");
        if (!Directory.Exists(folder)) return;
        var session = ScummGameSession.BootForPlay(new GameFolderScanner().Scan(folder), out _);
        Assert.Null(session.DoubleClickLook(100, 60, out var refused));
        Assert.Contains("second click", refused);
    }
}

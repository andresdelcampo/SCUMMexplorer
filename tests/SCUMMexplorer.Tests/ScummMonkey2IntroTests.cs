using System.Text;
using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The explicit intro of the Monkey Island 2 floppy releases (30 October 2026, user report of 28 October: "the copy protection blocks the opening and its
/// screen is not shown"). Play Game Intro answers nothing for the player, so the player must read the recipe to answer it. Two engine gaps hid it:
/// a string that names a string (code 7 inside string 31: the recipe's name in string 43) was left out, and talk lines were not wrapped from version 4 on
/// (ScummVM <c>displayDialog</c>/<c>addLinebreaks</c>). Record: <c>.verification/mi2-intro-protection-20261030/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummMonkey2IntroTests
{
    private static string? Folder(string copy)
    {
        var path = Path.Combine(TestGames.Root, copy);
        return File.Exists(Path.Combine(path, "monkey2.000")) || File.Exists(Path.Combine(path, "MONKEY2.000")) ? path : null;
    }

    private static IReadOnlyList<string> Texts(ScummGameSession session) => session.Messages.OnScreen.SelectMany(message => message.Lines).ToArray();

    private static void Run(ScummGameSession session, Func<bool> until, int frames = 600)
    {
        for (var i = 0; i < frames && !until(); i++) session.AdvanceFrame();
    }

    [Fact]
    public void AStringThatNamesAStringIsConvertedToo()
    {
        // ScummVM convertStringMessage hands the stored string to convertMessageToString, so its own codes resolve.
        var strings = new Dictionary<int, byte[]>
        {
            [31] = [.. Encoding.ASCII.GetBytes("RECIPE FOR "), 0xFF, 0x07, 0x2B, 0x00, (byte)':'],
            [43] = Encoding.ASCII.GetBytes("GOUT"),
            [50] = [0xFF, 0x07, 50, 0x00, (byte)'!']
        };
        byte[]? Stored(int number) => strings.TryGetValue(number, out var text) ? text : null;
        var recipe = ScummTextDecoder.ConvertMessage([0xFF, 0x07, 0x1F, 0x00], new Dictionary<string, ushort>(), null, null, Stored, out _);
        Assert.Equal("RECIPE FOR GOUT:", Encoding.ASCII.GetString(recipe.ToArray()));
        // A string that names itself ends after a bounded depth instead of looping.
        var self = ScummTextDecoder.ConvertMessage([0xFF, 0x07, 50, 0x00], new Dictionary<string, ushort>(), null, null, Stored, out _);
        Assert.All(self, item => Assert.Equal((byte)'!', item));
    }

    [Theory]
    [InlineData("mi2 mt32 en")]
    [InlineData("mi2 classic es")]
    public void TheFloppyIntroShowsItsWholeRecipeAndTakesTheAnswer(string copy)
    {
        if (Folder(copy) is not { } folder) return;
        var session = ScummGameSession.StartIntro(new GameFolderScanner().Scan(folder), out _);
        Run(session, () => session.UserInputOn && Texts(session).Count > 0);
        // Script 128 +0x0022 centres the prompt at x 160 in charset 7, wider than the screen: the game wraps it (two lines, each inside 320 pixels).
        var prompt = session.Messages.OnScreen.Single();
        Assert.True(prompt.LineGlyphs.Count >= 2, $"one line: {string.Join(" / ", prompt.Lines)}");
        var charset = session.PlayCharset(prompt.Charset)!;
        Assert.All(prompt.LineGlyphs, line => Assert.True(charset.StringWidth(line) <= 316, $"too wide: {ScummTextDecoder.DecodeGlyphs(line)}"));

        Assert.True(session.PressPlayKey(13, out var refused) is not null, refused);
        Run(session, () => Texts(session).Count >= 3);
        var recipe = Texts(session);
        // Script 130 +0x00F7 prints string 31, which names string 43 (one of strings 34 to 40, the recipe's name); +0x016E prints string 45, which names
        // strings 41 and 46 (the answer's two digit fields, '_' each) and 32, 33 (the ingredients).
        Assert.Matches(@"\S+:\s*$", recipe[0]);
        Assert.DoesNotMatch(@"\s:\s*$", recipe[0]);
        Assert.Contains("__", recipe[1]);
        Assert.Matches(@"__ \S", recipe[1]);

        // The answer the game keeps (Script 130 +0x0134, read only) typed as a player would. Accepted, the input field (Script 136) ends and Script 130 goes on
        // to the full/lite choice; a wrong answer would start Script 128 again ("PRESS ENTER TO TRY AGAIN").
        var answer = session.Snapshot.KnownGlobalVariables["var[0x206]"];
        foreach (var digit in answer.ToString("0000")) { session.PressPlayKey(digit, out _); Run(session, () => false, 8); }
        Run(session, () => false, 600);
        Assert.Contains(session.PendingScriptSummary, item => item.StartsWith("Script 130 ", StringComparison.Ordinal));
        Assert.DoesNotContain(session.PendingScriptSummary, item => item.StartsWith("Script 128 ", StringComparison.Ordinal) || item.StartsWith("Script 136 ", StringComparison.Ordinal));
    }
}

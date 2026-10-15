namespace SCUMMexplorer.Tests;

/// <summary>
/// Where the local game data lives. Since 15 October 2026 it is kept outside the repository (a worktree clean-up deleted the
/// old <c>Games</c> folder through a junction): <c>SCUMM_GAMES_ROOT</c> if set, else a <c>Games</c> folder in the checkout
/// if one exists, else <see cref="DefaultRoot"/>. Every test that needs a game folder takes it from <see cref="Root"/>.
/// The LOOM tests run against the English DOS EGA release <c>loom ega mt32 en</c> (since 4 October; the Spanish release
/// <c>loom ega mt32 es</c>, once called <c>loom</c>, was the reference before). Set <c>SCUMM_LOOM_TEST_ROOT</c> to a full
/// folder path to run them against another edition; assertions that name edition-specific text (object names, dialogue)
/// then show what differs.
/// </summary>
internal static class TestGames
{
    /// <summary>The user's game folder outside the repository.</summary>
    public const string DefaultRoot = @"D:\Juegos\Lucas";

    /// <summary>The folder that holds one sub-folder per game (<c>loom ega mt32 en</c>, <c>indy3 ega en</c>, …).</summary>
    public static readonly string Root = ResolveRoot();

    /// <summary>The LOOM folder as an absolute path (callers combine it with the base directory, which keeps it as it is).</summary>
    public static readonly string LoomRelative = Resolve();

    private static string ResolveRoot()
    {
        if (Environment.GetEnvironmentVariable("SCUMM_GAMES_ROOT") is { Length: > 0 } overridden) return overridden;
        var inCheckout = Path.Combine(SupportMatrixTests.RepositoryRoot(), "Games");
        return Directory.Exists(inCheckout) && Directory.EnumerateFileSystemEntries(inCheckout).Any() ? inCheckout : DefaultRoot;
    }

    private static string Resolve()
    {
        if (Environment.GetEnvironmentVariable("SCUMM_LOOM_TEST_ROOT") is { Length: > 0 } overridden) return overridden;
        foreach (var name in new[] { "loom ega mt32 en", "loom ega mt32 es", "loom" })
            if (File.Exists(Path.Combine(Root, name, "00.LFL"))) return Path.Combine(Root, name);
        return Path.Combine(Root, "loom");
    }
}

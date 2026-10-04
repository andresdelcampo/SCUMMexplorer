namespace SCUMMexplorer.Tests;

/// <summary>
/// Where the local LOOM data lives. The tests run against the English DOS EGA release in <c>Games/loom ega en</c> (since 4 October;
/// the Spanish release in <c>Games/loom ega es</c>, once called <c>Games/loom</c>, was the reference before). Set
/// <c>SCUMM_LOOM_TEST_ROOT</c> to a full folder path to run them against another edition; assertions that name edition-specific
/// text (object names, dialogue) then show what differs.
/// </summary>
internal static class TestGames
{
    /// <summary>The folder as a path relative to a test binary's base directory, or the override's absolute path.</summary>
    public static readonly string LoomRelative = Resolve();

    private static string Resolve()
    {
        if (Environment.GetEnvironmentVariable("SCUMM_LOOM_TEST_ROOT") is { Length: > 0 } overridden) return overridden;
        const string up = "../../../../Games/";
        foreach (var name in new[] { "loom ega en", "loom ega es", "loom" })
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, up + name, "00.LFL"))) return up + name;
        return up + "loom";
    }
}

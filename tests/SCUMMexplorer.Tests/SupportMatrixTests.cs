using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The support matrix has one source, <see cref="GameSupport.Families"/>. The README and docs/REQUIREMENTS.md hold a generated copy between
/// <c>&lt;!-- support-matrix:start --&gt;</c> and <c>&lt;!-- support-matrix:end --&gt;</c>. Run the tests with <c>SCUMM_UPDATE_SUPPORT_MATRIX=1</c> to rewrite both.
/// </summary>
public class SupportMatrixTests
{
    private const string Start = "<!-- support-matrix:start -->";
    private const string End = "<!-- support-matrix:end -->";

    internal static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SCUMMexplorer.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("SCUMMexplorer.slnx not found above the test binaries.");
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/REQUIREMENTS.md")]
    public void GeneratedMatrixIsInStepWithTheRegistry(string file)
    {
        var path = Path.Combine(RepositoryRoot(), file);
        var text = File.ReadAllText(path);
        var start = text.IndexOf(Start, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{file} lacks the {Start} / {End} markers.");
        var expected = GameSupport.RenderMatrix();
        var current = text[(start + Start.Length)..end].Replace("\r\n", "\n").Trim('\n');

        if (Environment.GetEnvironmentVariable("SCUMM_UPDATE_SUPPORT_MATRIX") == "1" && current != expected.TrimEnd('\n'))
        {
            var newline = text.Contains("\r\n") ? "\r\n" : "\n";
            File.WriteAllText(path, text[..(start + Start.Length)] + newline + expected.TrimEnd('\n').Replace("\n", newline) + newline + text[end..]);
            return;
        }

        Assert.Equal(expected.TrimEnd('\n'), current);
    }

    [Fact]
    public void EveryFamilyHasOneRowWithAllColumns()
    {
        var features = Enum.GetValues<SupportFeature>().Length;
        Assert.All(GameSupport.Families, row => Assert.Equal(features, row.Levels.Count));
        Assert.Equal(Enum.GetValues<GameFamily>().Length, GameSupport.Families.Select(row => row.Family).Distinct().Count());
    }
}

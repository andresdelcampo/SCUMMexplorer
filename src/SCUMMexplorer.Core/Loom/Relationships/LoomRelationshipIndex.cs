using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public enum LoomRelationshipStatus
{
    Established,
    StaticCandidate,
    Unresolved
}

/// <summary>A source-backed link between parsed LOOM game elements.</summary>
public sealed record LoomRelationship(
    GameResource SourceResource,
    string Relationship,
    GameResource? TargetResource,
    string TargetLabel,
    string Evidence,
    LoomRelationshipStatus Status);

/// <summary>An immutable index of source relationships established by the supported LOOM readers.</summary>
public sealed class LoomRelationshipIndex
{
    public static LoomRelationshipIndex Empty { get; } = new([]);

    public LoomRelationshipIndex(IEnumerable<LoomRelationship> relationships)
    {
        Relationships = relationships.ToArray();
    }

    public IReadOnlyList<LoomRelationship> Relationships { get; }

    public int UnresolvedCount => Relationships.Count(item => item.Status == LoomRelationshipStatus.Unresolved);

    public IReadOnlyList<LoomRelationship> ForResource(GameResource resource) =>
        Relationships.Where(item => item.SourceResource == resource || item.TargetResource == resource).ToArray();
}

/// <summary>
/// Connects indexed LOOM resources, room-local object records, embedded entry-script pointers,
/// and literal targets from the currently recognized script instructions.
/// </summary>
public sealed partial class LoomRelationshipIndexer
{
    private static readonly Regex CostumeOperation = new(@"(?:\[|;\s*)costume,\s*(?<value>0x[0-9A-Fa-f]+)", RegexOptions.CultureInvariant);

    /// <summary>The relationships the game's code and containers show: the LFL index and room files for LOOM EGA (<c>Ega/</c>), the decoded scripts of any other game (<c>Cd/</c>, first written for the CD game).</summary>
    public LoomRelationshipIndex Build(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        // R-6.50: every game has Connections. LOOM EGA keeps its container-level reader; any other game's links come from its decoded code.
        return game.Id == GameId.Loom && game.Version == ScummVersion.V3 ? BuildEga(game) : BuildFromCode(game);
    }
}

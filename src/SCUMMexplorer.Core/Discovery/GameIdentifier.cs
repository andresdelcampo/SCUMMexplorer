using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>What told the game, strongest first.</summary>
public enum GameEvidence
{
    /// <summary>Nothing: the game is not recognised.</summary>
    None,
    /// <summary>The hashes of a known edition (<see cref="KnownEditions"/>).</summary>
    EditionFiles,
    /// <summary>The hashes of files every edition of the game shares (<see cref="KnownGameInfo.SharedFiles"/>); the edition is not known.</summary>
    SharedFiles,
    /// <summary>The format's own index layout, which is fixed per game (the classic v1 index).</summary>
    IndexLayout,
    /// <summary>Only the sizes of the resource directories (<see cref="KnownGameInfo.Shapes"/>): an inference, labelled as one.</summary>
    DirectoryShape
}

/// <summary>Which game and edition a data set is, and what told it.</summary>
public sealed record GameIdentity(KnownGame Game, GameEvidence Evidence, KnownEdition? Edition = null)
{
    public static GameIdentity Unknown { get; } = new(KnownGame.Unknown, GameEvidence.None);

    public bool IsDemo => Edition?.Demo == true;

    public string EditionLabel => Edition?.Label ?? "";

    /// <summary>The sentence of the detection note that says what the game is and how it was told.</summary>
    public string Note => Game == KnownGame.Unknown ? "The game is not recognised: no known data matches it." : Evidence switch
    {
        GameEvidence.EditionFiles => $"Recognised as {KnownGames.Info(Game).Title} by known data: {EditionLabel}.",
        GameEvidence.SharedFiles => $"Recognised as {KnownGames.Info(Game).Title} by known data shared by its editions; the edition is not known.",
        GameEvidence.IndexLayout => $"Recognised as {KnownGames.Info(Game).Title} by the layout of its index.",
        _ => $"Recognised as {KnownGames.Info(Game).Title} by the sizes of its resource directories only (inferred)."
    };
}

/// <summary>
/// The one identification of every game, in every format, from its bytes alone (R-3.1): (1) the known edition whose file hashes match best, (2) the game whose
/// shared files match, (3) the game the format's index layout names, (4) last, the game whose directory sizes match. Names of files or folders, executables and
/// file sizes are never used.
/// </summary>
public static class GameIdentifier
{
    /// <param name="data">The game's data files and directory sizes.</param>
    /// <param name="byIndexLayout">The game the format itself names (the classic v1 index), or <see cref="KnownGame.Unknown"/>.</param>
    public static GameIdentity Identify(GameFingerprint data, KnownGame byIndexLayout = KnownGame.Unknown)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (KnownEditions.Identify(data) is { } edition) return new(edition.Game, GameEvidence.EditionFiles, edition);
        foreach (var info in KnownGames.All)
            if (info.SharedFiles.Count(file => data.Hash(file.Key) is { } hash && file.Value.Contains(hash, StringComparer.OrdinalIgnoreCase)) >= KnownGames.RequiredSharedMatches)
                return new(info.Game, GameEvidence.SharedFiles);
        if (byIndexLayout != KnownGame.Unknown) return new(byIndexLayout, GameEvidence.IndexLayout);
        if (data.Shape is { } shape && KnownGames.All.FirstOrDefault(info => info.Shapes.Any(known => known.Matches(shape))) is { } byShape)
            return new(byShape.Game, GameEvidence.DirectoryShape);
        return GameIdentity.Unknown;
    }
}

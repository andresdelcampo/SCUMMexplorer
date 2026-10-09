using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// How the engine must behave for one game: the one place that maps a detected game onto engine variants, so that engine code asks
/// the profile ("does this version walk the v4 way?") instead of testing which game it is running. A new SCUMM version or game
/// adds a case here and, where it really differs, a reader or data source in its <c>V3</c>/<c>V4</c>-style folder.
/// </summary>
public sealed record ScummProfile(ScummVersion Version, ScummScriptDialect Dialect)
{
    /// <summary>Actor walking follows the v4 routines (<c>Actor</c>) instead of the old-v3 ones (<c>Actor_v3</c>).</summary>
    public bool UsesV4Walking => Version >= ScummVersion.V4;

    /// <summary>The game's resources live in a v4 archive (<c>DISK01.LEC</c>) instead of old-v3 <c>*.LFL</c> files.</summary>
    public bool UsesV4Archive => Version >= ScummVersion.V4;

    public static ScummProfile For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var dialect = game.Version >= ScummVersion.V4 ? ScummScriptDialect.V4
            : game.Id == GameId.IndianaJones3 ? ScummScriptDialect.V3Indy3
            : ScummScriptDialect.V3;
        return new ScummProfile(game.Version, dialect);
    }
}

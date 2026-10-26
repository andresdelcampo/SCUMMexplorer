using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Discovery;

/// <summary>
/// The entry point for opening a game: which layout the path is (a disc image or container files, <see cref="ArchiveLayout"/>; a folder of one file per room,
/// <see cref="LflFolderLayout"/>) and nothing else. Every layout lists its resources and identifies the game the same way (<see cref="GameIdentifier"/>, from the
/// data's bytes), so no layout names a game by a file or folder name.
/// </summary>
public sealed class GameFolderScanner
{
    /// <summary>
    /// S9 (18 October 2026): the folder of a disc that carries one copy of the game per language (Monkey Island 1 CD: ENGLISH, FRENCH, GERMAN, ITALIAN,
    /// SPANISH) to read instead of the default (ENGLISH). Null keeps the default. Since 19 October the app sets it from the language the user chose
    /// (<see cref="ListLanguages"/>, <see cref="GameLibrary.Languages"/>); a folder the disc lacks falls back to the default.
    /// </summary>
    public string? DiscFolder { get; init; }

    /// <summary>A fresh library of the same data set as <paramref name="game"/>: its folder, in its language (a worker's own reader, for one).</summary>
    public static GameLibrary Reopen(GameLibrary game) => new GameFolderScanner { DiscFolder = game.Language }.Scan(game.FolderPath);

    public GameLibrary Scan(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullPath = Path.GetFullPath(folderPath);
        if (ArchiveLayout.Open(fullPath) is { } source) return ArchiveLayout.Scan(source, DiscFolder);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Game folder not found: {fullPath}");
        return LflFolderLayout.Scan(fullPath);
    }

    /// <summary>What the game at <paramref name="folderPath"/> is told by (its data files and directory sizes), in whatever layout it is; null when the path is none.</summary>
    public static GameFingerprint? Fingerprint(string folderPath, string? discFolder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullPath = Path.GetFullPath(folderPath);
        if (ArchiveLayout.Open(fullPath) is { } source) return ArchiveLayout.Fingerprint(source, discFolder);
        return Directory.Exists(fullPath) ? LflFolderLayout.Fingerprint(fullPath) : null;
    }

    /// <summary>
    /// 19 October 2026: the languages the game at <paramref name="folderPath"/> holds when its data holds more than one, read from the data (today: a disc or folder with one
    /// copy of the same v5 game per folder, labelled by the folder's own name); empty for a game with one language. Another format with the case adds its own
    /// candidates in <see cref="ArchiveLayout.LanguagesOf"/>. Pass a language's <see cref="GameLanguage.Key"/> as <see cref="DiscFolder"/> to open it.
    /// </summary>
    public static IReadOnlyList<GameLanguage> ListLanguages(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        return ArchiveLayout.Open(Path.GetFullPath(folderPath)) is { } source ? ArchiveLayout.LanguagesOf(source) : [];
    }
}

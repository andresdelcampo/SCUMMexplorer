using System.IO;
using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// R-3.16 (19 October 2026): the language of a game whose data holds several (Monkey Island 1 CD). The choice sits under the game's name, only for such a game;
/// choosing another language reopens the game in it (play stops; caches are keyed by <see cref="GameLibrary.Key"/>), and the choice is remembered per game folder
/// in the profile (<c>game-languages.txt</c>: one line per folder, its full path, a tab and the language's key).
/// </summary>
public partial class MainWindow
{
    // A property, not a static field: the static fields of the other partial files (LastFolderFile) may not be set yet when this file's would be.
    private static string LanguagesFile => Path.Combine(Path.GetDirectoryName(LastFolderFile)!, "game-languages.txt");

    /// <summary>True while the list is filled for the opened game, so the fill is not taken for a choice.</summary>
    private bool _languageFill;

    private static Dictionary<string, string> ReadRememberedLanguages()
    {
        var remembered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(LanguagesFile)) return remembered;
            foreach (var line in File.ReadAllLines(LanguagesFile))
                if (line.Split('\t') is [var path, var key] && path.Length > 0 && key.Length > 0) remembered[path] = key;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return remembered;
    }

    private static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>
    /// The language remembered for a game folder, or null. A game read from a disc image may be remembered under the image's path while the folder that holds it is
    /// opened (or the other way round), so an entry whose image sits in the folder counts too.
    /// </summary>
    private static string? RememberedLanguage(string folder)
    {
        var remembered = ReadRememberedLanguages();
        if (remembered.Count == 0) return null;
        string full;
        try { full = FullPath(folder); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
        if (remembered.TryGetValue(full, out var key)) return key;
        var holder = File.Exists(full) ? Path.GetDirectoryName(full) : full;
        return remembered.FirstOrDefault(pair => string.Equals(Path.GetDirectoryName(pair.Key), holder, StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(pair.Key, holder, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private void RememberLanguage(GameLibrary game, string key)
    {
        var remembered = ReadRememberedLanguages();
        remembered[FullPath(game.FolderPath)] = key;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LanguagesFile)!);
            File.WriteAllLines(LanguagesFile, remembered.Select(pair => pair.Key + "\t" + pair.Value));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = "The language could not be remembered."; }
    }

    /// <summary>Fills the language list for the opened game: shown only when its data holds more than one language.</summary>
    private void ShowLanguageChoice(GameLibrary game)
    {
        _languageFill = true;
        try
        {
            LanguageCombo.Items.Clear();
            foreach (var language in game.Languages)
                LanguageCombo.Items.Add(new ComboBoxItem { Content = language.Label, Tag = language.Key });
            LanguageCombo.SelectedIndex = game.Languages.ToList().FindIndex(l => string.Equals(l.Key, game.Language, StringComparison.OrdinalIgnoreCase));
            LanguageCombo.Visibility = game.Languages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _languageFill = false; }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageFill || _game is not { } game || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string key } ||
            string.Equals(key, game.Language, StringComparison.OrdinalIgnoreCase)) return;
        RememberLanguage(game, key);
        // The thumbnails and room lists saved for that language are its own (keyed by language), so they may be reused.
        _roomCardsMayUseCache = true;
        try { OpenGameFolder(game.FolderPath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open the game in that language", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _roomCardsMayUseCache = false; }
    }
}

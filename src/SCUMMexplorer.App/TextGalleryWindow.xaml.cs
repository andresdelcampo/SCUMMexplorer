using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>A known cutscene that the texts of one script belong to, and how the main window plays it.</summary>
public sealed record GalleryCutscene(string Title, string Note, Action Play);

public partial class TextGalleryWindow : Window
{
    private sealed record GalleryItem(LoomTextEntry Entry, int? ConversationLines = null)
    {
        public string Text => Entry.Text;
        public string ListHeading => Entry.ConversationId is not null
            ? (Entry.RoomId is int sceneRoom ? $"ROOM {sceneRoom:00}  /  " : "") +
              (ConversationLines is int lines
                  ? $"{(Entry.ConversationKind ?? "SCRIPT DIALOGUE").ToUpperInvariant()} GROUP  /  {lines} LINES"
                  : $"{(Entry.ConversationKind ?? "SCRIPT DIALOGUE").ToUpperInvariant()}  /  LINE {Entry.ConversationLine:00}") +
              (Entry.ObjectId is int sceneObject ? $"  /  OBJECT {sceneObject}" : "") +
              (Entry.ObjectName is string sceneName ? $" · {sceneName}" : "")
            : Entry.RoomId is int roomId
                ? $"ROOM {roomId:00}" + (Entry.ObjectId is int objectId ? $"  /  OBJECT {objectId}" : "") +
                  (Entry.ObjectName is string objectName ? $" · {objectName}" : "") + $"  /  {Entry.Kind.ToUpperInvariant()}"
                : $"ELSEWHERE IN GAME  /  {Entry.Kind.ToUpperInvariant()}";
    }

    private readonly GameLibrary _game;
    private readonly LoomTextExtraction _extraction;
    private readonly Action<int> _visitRoom;
    private readonly IReadOnlyDictionary<int, GalleryCutscene> _cutscenes;
    private readonly ScummV3EgaRoomDecoder _roomDecoder = new();
    private LoomTextEntry? _selectedEntry;

    /// <param name="cutscenes">Known cutscenes by the indexed script that prints their texts (044, 046…).</param>
    private readonly Func<int, Task<BitmapSource?>>? _roomImage;

    /// <param name="roomImage">The room as it first looks in the game (the explorer's initial state), or null to show the stored background.</param>
    public TextGalleryWindow(GameLibrary game, LoomTextExtraction extraction, Action<int> visitRoom,
        IReadOnlyDictionary<int, GalleryCutscene>? cutscenes = null, Func<int, Task<BitmapSource?>>? roomImage = null)
    {
        _roomImage = roomImage;
        InitializeComponent();
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _visitRoom = visitRoom ?? throw new ArgumentNullException(nameof(visitRoom));
        _cutscenes = cutscenes ?? new Dictionary<int, GalleryCutscene>();

        CoverageText.Text = $"{extraction.Entries.Count:N0} texts found. Browse spoken lines, object names and other stored words.";
        ExtractionDetails.Text =
            $"{extraction.ResourcesVisited:N0} script ranges visited. {extraction.Coverage} " +
            $"{extraction.UnresolvedPaths:N0} unresolved paths; {extraction.ResourcesSkipped:N0} ranges skipped. " +
            "The current decoder follows reachable instructions only, so text behind undecoded or unreachable code may be missing. Storage order does not establish scene order." +
            (extraction.LeftoverEntries.Count == 0 ? "" : Environment.NewLine + Environment.NewLine +
                $"{extraction.LeftoverEntries.Count:N0} leftover index entries are not text sources: 00.LFL lists them, " +
                "but their offsets point inside another resource, so there is no script to read. They are not counted as skipped." +
                Environment.NewLine + string.Join(Environment.NewLine, extraction.LeftoverEntries.Select(item => item.Describe()))) +
            (extraction.Diagnostics.Count == 0 ? "" : Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, extraction.Diagnostics.Take(8)) +
                (extraction.Diagnostics.Count > 8 ? Environment.NewLine + "More details are not shown." : ""));

        RoomFilter.Items.Add(new ComboBoxItem { Content = "Every room", Tag = -1 });
        foreach (var roomId in extraction.Entries.Where(item => item.RoomId.HasValue)
                     .Select(item => item.RoomId!.Value).Distinct().Order())
            RoomFilter.Items.Add(new ComboBoxItem { Content = $"Room {roomId:00}", Tag = roomId });
        RoomFilter.SelectedIndex = 0;
        KindFilter.SelectedIndex = 0;
        UpdateList();
    }

    private void Filters_Changed(object sender, RoutedEventArgs e) => UpdateList();

    private void UpdateList()
    {
        if (TextList is null) return;
        var query = SearchBox.Text.Trim();
        var roomId = (RoomFilter.SelectedItem as ComboBoxItem)?.Tag as int? ?? -1;
        if (roomId >= 0) _ = GetRoomPicture(roomId); // prepare the picked room once; every text of it then shows at once
        var kind = (KindFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var candidates = _extraction.Entries.Where(entry =>
            (roomId < 0 || entry.RoomId == roomId) &&
            (kind.Length == 0 || kind == "Other"
                ? kind.Length == 0 || entry.Kind is "Stored line" or "Object label" or "Verb label"
                : kind == "Conversation" ? entry.ConversationId is not null
                : entry.Kind == kind) &&
            (query.Length == 0 || entry.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             (entry.ObjectName?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
             entry.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        var results = kind == "Conversation"
            ? candidates.GroupBy(entry => entry.ConversationId)
                .Select(group =>
                {
                    var first = group.OrderBy(entry => entry.ConversationLine).First();
                    var count = _extraction.Entries.Where(entry => entry.ConversationId == first.ConversationId)
                        .Select(entry => (entry.CodeOffset, entry.Text, entry.Speaker)).Distinct().Count();
                    return new GalleryItem(first, count);
                }).ToArray()
            : candidates.Select(entry => new GalleryItem(entry)).ToArray();
        TextList.ItemsSource = results;
        ExportButton.IsEnabled = results.Length > 0;
        ResultCount.Text = kind == "Conversation"
            ? $"{results.Length:N0} matching groups"
            : $"{results.Length:N0} matching texts";
        if (results.Length > 0) TextList.SelectedIndex = 0;
        else
        {
            _selectedEntry = null;
            SelectedKind.Text = "NO MATCHES";
            SelectedHeading.Text = "Try another word or choose a different room.";
            SelectedText.Text = "";
            SourceDetails.Text = "";
            VisitRoomButton.Visibility = Visibility.Collapsed;
            PlayCutsceneButton.Visibility = Visibility.Collapsed;
            PlaySpeechButton.Visibility = Visibility.Collapsed;
            RoomArtworkPreview.Source = null;
            RoomArtworkPreview.Visibility = Visibility.Collapsed;
        }
    }

    private async void TextList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextList.SelectedItem is not GalleryItem item) return;
        _selectedEntry = item.Entry;
        SelectedKind.Text = item.Entry.ConversationId is not null
            ? $"{(item.Entry.ConversationKind ?? "SCRIPT DIALOGUE").ToUpperInvariant()} GROUP"
            : item.Entry.Kind.ToUpperInvariant();
        var objectContext = item.Entry.ObjectId is int selectedObject
            ? $" · Object {selectedObject}" + (item.Entry.ObjectName is string selectedName ? $" · {selectedName}" : "")
            : "";
        SelectedHeading.Text = item.Entry.ConversationId is not null
            ? (item.Entry.RoomId is int conversationRoom ? $"Room {conversationRoom:00}" : "Elsewhere in game") +
              objectContext + (item.Entry.ConversationKind switch
              {
                  "Cutscene" => " · Cutscene lines in stored source order; branches and called scripts may change the played sequence.",
                  LoomTextExtractor.ChoiceConversationKind => " · Options offered to the player, each followed by the replies stored in its branch. Conditions can hide options or change replies.",
                  _ => " · Script dialogue in stored source order; branches and called scripts may change the played sequence."
              })
            : (item.Entry.RoomId is int selectedRoom ? $"Room {selectedRoom:00}" : "Elsewhere in game") + objectContext;
        if (item.Entry.ConversationId is string conversationId)
        {
            ShowConversation(_extraction.Entries.Where(entry => entry.ConversationId == conversationId)
                .OrderBy(entry => entry.ConversationLine)
                .GroupBy(entry => (entry.CodeOffset, entry.Text, entry.Speaker))
                .Select(group => group.First()).ToList());
        }
        else
        {
            ConversationLines.Visibility = Visibility.Collapsed;
            SelectedText.Visibility = Visibility.Visible;
            SelectedText.Text = item.Entry.Text;
        }
        RoomArtworkPreview.Source = null;
        RoomArtworkPreview.Visibility = Visibility.Collapsed;
        var location = item.Entry.ObjectId is int objectId
            ? $"Object {objectId}" + (item.Entry.ObjectName is string objectName ? $" · {objectName}" : "") +
              (item.Entry.VerbId is int verbId ? $" / verb 0x{verbId:X2}" : "")
            : item.Entry.ScriptId is int scriptId ? $"Indexed script {scriptId:00}" : "Room script";
        if (item.Entry.SourceObjectId is int sourceObjectId)
            location += $" / source object {sourceObjectId}" +
                (item.Entry.SourceObjectName is string sourceName ? $" · {sourceName}" : "");
        SourceDetails.Text = $"{item.Entry.Source}\n{location} / byte offset +0x{item.Entry.CodeOffset:X4}";
        VisitRoomButton.Visibility = item.Entry.RoomId.HasValue ? Visibility.Visible : Visibility.Collapsed;
        var cutscene = CutsceneFor(item.Entry);
        PlayCutsceneButton.Visibility = cutscene is null ? Visibility.Collapsed : Visibility.Visible;
        StopSpeech();
        PlaySpeechButton.Visibility = _game.Speech is not null && item.Entry.SpeechOffset is not null ? Visibility.Visible : Visibility.Collapsed;
        PlayCutsceneButton.ToolTip = cutscene is null ? null : $"Plays {cutscene.Title}, which prints this text. {cutscene.Note}";

        if (item.Entry.RoomId is not int roomId)
        {
            return;
        }

        var room = _game.Rooms.FirstOrDefault(resource => resource.ResourceId == roomId);
        if (room is null) return;
        try
        {
            var picture = await GetRoomPicture(roomId);
            if (!ReferenceEquals(_selectedEntry, item.Entry)) return;
            RoomArtworkPreview.Source = picture;
            RoomArtworkPreview.Visibility = picture is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            RoomArtworkPreview.Source = null;
            RoomArtworkPreview.Visibility = Visibility.Collapsed;
        }
    }

    private readonly Dictionary<int, Task<BitmapSource?>> _roomPictures = [];

    /// <summary>A room's picture, made once (the room's states are worked out on the first request) and kept for every later text.</summary>
    private Task<BitmapSource?> GetRoomPicture(int roomId)
    {
        if (_roomPictures.TryGetValue(roomId, out var cached)) return cached;
        return _roomPictures[roomId] = MakeRoomPicture(roomId);
    }

    private async Task<BitmapSource?> MakeRoomPicture(int roomId)
    {
        var room = _game.Rooms.FirstOrDefault(resource => resource.ResourceId == roomId);
        if (room is null) return null;
        try
        {
            BitmapSource? picture = _roomImage is null ? null : await _roomImage(roomId);
            if (picture is not null) return picture;
            var decoded = _game.V4ArchiveFor(room) is { } archive ? archive.DecodeRoom(roomId)
                : _game.Version is ScummVersion.V1 or ScummVersion.V2 ? MainWindow.DecodeOlderRoom(_game, await File.ReadAllBytesAsync(room.FilePath))
                : await _roomDecoder.DecodeAsync(room.FilePath);
            return CreateBitmap(decoded.Width, decoded.Height, decoded.Pixels.ToArray(), decoded.Palette);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { return null; }
    }

    private void VisitRoom_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry?.RoomId is not int roomId) return;
        _visitRoom(roomId);
        Close();
    }

    /// <summary>The known cutscene whose script prints this text, if any.</summary>
    private GalleryCutscene? CutsceneFor(LoomTextEntry entry) =>
        entry.ScriptId is int scriptId && _cutscenes.TryGetValue(scriptId, out var cutscene) ? cutscene : null;

    private readonly System.Windows.Media.MediaPlayer _speechPlayer = new();
    private string? _speechFile;

    private void StopSpeech()
    {
        _speechPlayer.Stop();
        _speechPlayer.Close();
        if (_speechFile is not null)
        {
            try { File.Delete(_speechFile); } catch (IOException) { }
            _speechFile = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopSpeech();
        base.OnClosed(e);
    }

    /// <summary>Plays the spoken line the selected text belongs to, from the game's speech file (MONSTER.SOU).</summary>
    private async void PlaySpeech_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is not { SpeechOffset: { } offset, SpeechLength: { } length } || _game.Speech is not { } speech) return;
        PlaySpeechButton.IsEnabled = false;
        try
        {
            var wave = await Task.Run(() => speech.ReadWave(offset, length));
            StopSpeech();
            _speechFile = Path.Combine(Path.GetTempPath(), $"scummexplorer-speech-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(_speechFile, wave);
            _speechPlayer.Open(new Uri(_speechFile));
            _speechPlayer.Play();
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
        {
            MessageBox.Show(this, ex.Message, "Speech", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { PlaySpeechButton.IsEnabled = true; }
    }

    private void PlayCutscene_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null || CutsceneFor(_selectedEntry) is not { } cutscene) return;
        Close();
        cutscene.Play();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var items = (TextList.ItemsSource as IEnumerable<GalleryItem>)?.ToArray() ?? [];
        if (items.Length == 0) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export game texts",
            FileName = "game-texts.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, BuildExportText(items), new System.Text.UTF8Encoding(false));
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, "Could not export game texts", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(this, ex.Message, "Could not export game texts", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string BuildExportText(IEnumerable<GalleryItem> items)
    {
        var output = new List<string>();
        foreach (var item in items)
        {
            var entry = item.Entry;
            var room = entry.RoomId is int roomId ? $"Room {roomId:00}" : "Elsewhere in game";
            var kind = entry.ConversationId is not null
                ? entry.ConversationKind ?? "Script dialogue"
                : entry.Kind;
            var objectInfo = entry.ObjectId is int objectId
                ? $" · Object {objectId}" + (entry.ObjectName is string objectName ? $" · {objectName}" : "")
                : "";
            output.Add($"{room} · {kind}{objectInfo}");

            if (entry.ConversationId is string conversationId)
            {
                output.AddRange(_extraction.Entries.Where(line => line.ConversationId == conversationId)
                    .OrderBy(line => line.ConversationLine)
                    .GroupBy(line => (line.CodeOffset, line.Text, line.Speaker))
                    .Select(group => group.First())
                    .Select(FormatConversationLine));
            }
            else
            {
                output.Add(entry.Text);
            }

            output.Add("");
        }

        return string.Join(Environment.NewLine, output);
    }

    // One colour per speaker, in order of appearance, so who talks is clear at a glance.
    private static readonly Color[] SpeakerColors =
    [
        Color.FromRgb(240, 176, 96), Color.FromRgb(120, 200, 232), Color.FromRgb(150, 214, 140),
        Color.FromRgb(214, 150, 200), Color.FromRgb(232, 120, 110), Color.FromRgb(200, 200, 120)
    ];

    /// <summary>A conversation as rows: the speaker (bold, in their colour) in one aligned column, the line beside it, wrapping under itself.</summary>
    private void ShowConversation(IReadOnlyList<LoomTextEntry> lines)
    {
        SelectedText.Visibility = Visibility.Collapsed;
        ConversationLines.Visibility = Visibility.Visible;
        ConversationLines.Children.Clear();
        var colours = new Dictionary<string, Color>();
        foreach (var entry in lines)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "speaker" });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string who;
            Color colour;
            if (entry.IsPlayerChoice) { who = "▸"; colour = Color.FromRgb(183, 132, 66); }
            else
            {
                who = SpeakerName(entry);
                if (!colours.TryGetValue(who, out colour)) colours[who] = colour = SpeakerColors[colours.Count % SpeakerColors.Length];
            }
            var name = new TextBlock
            {
                Text = who, FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = new SolidColorBrush(colour),
                Margin = new Thickness(entry.ChoiceVerbId.HasValue && !entry.IsPlayerChoice ? 22 : 0, 2, 14, 0), VerticalAlignment = VerticalAlignment.Top
            };
            var text = new TextBlock { Text = entry.Text, FontSize = 18, LineHeight = 25, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(243, 235, 221)) };
            Grid.SetColumn(text, 1);
            row.Children.Add(name);
            row.Children.Add(text);
            ConversationLines.Children.Add(row);
        }
    }

    /// <summary>The names LOOM's lines settle are LOOM's; another game's speakers keep the label the extractor gives them ("Actor 5", "Ego").</summary>
    private string SpeakerName(LoomTextEntry entry) =>
        _game.Id is GameId.Loom or GameId.LoomVga ? LoomSpeakerNames.Display(entry) : entry.Speaker ?? "Unknown speaker";

    private string FormatConversationLine(LoomTextEntry entry) =>
        entry.IsPlayerChoice ? $"▸ {entry.Text}"
        : entry.ChoiceVerbId.HasValue ? $"      {SpeakerName(entry)}: {entry.Text}"
        : $"{SpeakerName(entry)}: {entry.Text}";

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels, IReadOnlyList<uint> palette)
    {
        var colors = palette.Select(argb => Color.FromArgb((byte)(argb >> 24),
            (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)).ToList();
        var bitmap = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Indexed8, new BitmapPalette(colors), pixels, width);
        bitmap.Freeze();
        return bitmap;
    }
}

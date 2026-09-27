using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.App;

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
    private readonly LoomEgaRoomDecoder _roomDecoder = new();
    private LoomTextEntry? _selectedEntry;

    public TextGalleryWindow(GameLibrary game, LoomTextExtraction extraction, Action<int> visitRoom)
    {
        InitializeComponent();
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        _visitRoom = visitRoom ?? throw new ArgumentNullException(nameof(visitRoom));

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
            SelectedText.Text = string.Join(Environment.NewLine + Environment.NewLine,
                _extraction.Entries.Where(entry => entry.ConversationId == conversationId)
                    .OrderBy(entry => entry.ConversationLine)
                    .GroupBy(entry => (entry.CodeOffset, entry.Text, entry.Speaker))
                    .Select(group => group.First())
                    .Select(FormatConversationLine));
        }
        else SelectedText.Text = item.Entry.Text;
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

        if (item.Entry.RoomId is not int roomId)
        {
            return;
        }

        var room = _game.Rooms.FirstOrDefault(resource => resource.ResourceId == roomId);
        if (room is null) return;
        try
        {
            var decoded = await _roomDecoder.DecodeAsync(room.FilePath);
            if (!ReferenceEquals(_selectedEntry, item.Entry)) return;
            RoomArtworkPreview.Source = CreateBitmap(decoded.Width, decoded.Height,
                decoded.Pixels.ToArray(), decoded.Palette);
            RoomArtworkPreview.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            RoomArtworkPreview.Source = null;
            RoomArtworkPreview.Visibility = Visibility.Collapsed;
        }
    }

    private void VisitRoom_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry?.RoomId is not int roomId) return;
        _visitRoom(roomId);
        Close();
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

    private static string FormatConversationLine(LoomTextEntry entry) =>
        entry.IsPlayerChoice ? $"▸ {entry.Text}"
        : entry.ChoiceVerbId.HasValue ? $"      {entry.Speaker ?? "Unknown speaker"}: {entry.Text}"
        : $"{entry.Speaker ?? "Unknown speaker"}: {entry.Text}";

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

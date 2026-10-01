using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

public partial class GameStatusWindow : Window
{
    private readonly Func<LoomExplorationSession?> _session;
    private readonly Func<bool> _canEdit;
    private readonly Action _refreshRoom;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private LoomExplorationSession? _editedSession;
    private LoomGameStatus? _baseline;
    private bool _editing;
    private string? _displayedDrafts;
    private List<DraftRow> _rows = [];

    public sealed class DraftRow
    {
        public required int Slot { get; init; }
        public string Title => $"Draft {Slot + 1}";
        public int Note1 { get; set; }
        public int Note2 { get; set; }
        public int Note3 { get; set; }
        public int Note4 { get; set; }
        public required string Flags { get; init; }
        public required string Origin { get; init; }
        public int[] Notes => [Note1, Note2, Note3, Note4];
    }

    public GameStatusWindow(Func<LoomExplorationSession?> session, Func<bool> canEdit, Action refreshRoom)
    {
        InitializeComponent(); _session = session; _canEdit = canEdit; _refreshRoom = refreshRoom;
        foreach (var column in new[] { Note1Column, Note2Column, Note3Column, Note4Column }) column.ItemsSource = Enumerable.Range(1, 8).ToArray();
        NoteCountChoice.ItemsSource = Enumerable.Range(0, 9).Select(n => n == 1 ? "1 available note" : $"{n} available notes").ToArray();
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
        Refresh();
    }

    private void Refresh()
    {
        var session = _session(); var status = session?.GameStatus;
        if (_editing && !ReferenceEquals(session, _editedSession))
        {
            _editing = false; _baseline = null;
            EditMessage.Text = "The exploration session was replaced. Unsaved edits were discarded.";
        }
        SourceEvidence.Text = status?.Evidence ?? "No exploration session.";
        SessionSummary.Text = status is null ? "Open a LOOM room first." : !status.Supported ? "Status fields are unresolved for this game edition." :
            $"Current exploration · Room {session!.Snapshot.RoomId:00}. Values update as game scripts run.";
        EditButton.IsEnabled = status?.Supported == true && _canEdit();
        ApplyButton.IsEnabled = _editing && _canEdit();
        StaffChoice.IsEnabled = NoteCountChoice.IsEnabled = _editing && _canEdit();
        DraftGrid.IsReadOnly = !_editing || !_canEdit();
        EditButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        ApplyButton.Visibility = CancelButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        if (_editing) return;
        StaffChoice.SelectedIndex = status?.StaffHeld is bool held ? held ? 1 : 0 : -1;
        StaffOrigin.Text = status?.StaffOrigin ?? "Unresolved";
        NoteCountChoice.SelectedIndex = status?.AvailableNoteCount ?? -1;
        NotesOrigin.Text = status?.NotesOrigin ?? "Unresolved";
        var draftSignature = string.Join(";", (status?.Drafts ?? []).Select(d => $"{d.Slot}:{d.Packed}:{d.Origin}"));
        if (_displayedDrafts != draftSignature)
        {
        _rows = (status?.Drafts ?? []).Select(d => new DraftRow { Slot = d.Slot, Note1 = d.Notes.ElementAtOrDefault(0), Note2 = d.Notes.ElementAtOrDefault(1),
            Note3 = d.Notes.ElementAtOrDefault(2), Note4 = d.Notes.ElementAtOrDefault(3),
            Flags = (d.Learned is true ? "Learned" : d.Learned is false ? "Unlearnt" : "Unresolved") + (d.Reversible is true ? " · reverse" : ""), Origin = d.Origin }).ToList();
        DraftGrid.ItemsSource = _rows;
        _displayedDrafts = draftSignature;
        }
        if (string.IsNullOrEmpty(EditMessage.Text)) EditMessage.Text = _canEdit()
            ? "Note IDs match the eight positions on the game's distaff. Only contiguous available-note counts are stored by SCUMM. Draft flags and response targets are preserved."
            : "Inspection stays live. Finish the scene, draft, movement or dialogue to edit; paused playback remains locked.";
    }

    private void NoteCount_Changed(object sender, SelectionChangedEventArgs e)
    {
        var count = NoteCountChoice.SelectedIndex;
        if (NoteIdentities is not null) NoteIdentities.Text = count < 0 ? "Unresolved note identities" : count == 0 ? "No notes available" :
            "Available: " + string.Join(", ", Enumerable.Range(1, count).Select(i => $"Note {i}"));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit() || _session() is not { } session) return;
        _displayedDrafts = null;
        Refresh(); _editedSession = session; _baseline = session.GameStatus; _editing = true;
        EditMessage.Text = "Unsaved exploration choices. Apply preserves each draft's learning/reverse flags. Staff/note changes clear partial note input through the game's scripts.";
        Refresh();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DraftGrid.CommitEdit(DataGridEditingUnit.Cell, true); DraftGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!_editing || !_canEdit() || _session() is not { } session || !ReferenceEquals(session, _editedSession) || _baseline is null) return;
        var current = session.GameStatus;
        if (current.StaffHeld != _baseline.StaffHeld || current.AvailableNoteCount != _baseline.AvailableNoteCount ||
            !current.Drafts.Select(d => d.Packed).SequenceEqual(_baseline.Drafts.Select(d => d.Packed)))
        { EditMessage.Text = "Game status changed while editing. Cancel edits and inspect the new values before applying."; return; }
        if (StaffChoice.SelectedIndex < 0 || NoteCountChoice.SelectedIndex < 0 || _rows.Any(r => r.Notes.Any(n => n is < 1 or > 8)))
        { EditMessage.Text = "Choose staff possession, a note count and exactly four note IDs 1–8 for each draft."; return; }
        try
        {
            foreach (var row in _rows)
                if (!row.Notes.SequenceEqual(_baseline.Drafts.Single(d => d.Slot == row.Slot).Notes)) session.UpdateDraftSequence(row.Slot, row.Notes);
            if ((StaffChoice.SelectedIndex == 1) != _baseline.StaffHeld) session.UpdateStaffPossession(StaffChoice.SelectedIndex == 1);
            if (NoteCountChoice.SelectedIndex != _baseline.AvailableNoteCount) session.UpdateAvailableNotes(NoteCountChoice.SelectedIndex);
            _editing = false; _baseline = null; EditMessage.Text = "Applied explicit exploration choices to this session. Fresh replay starts replace these choices with their audited state.";
            _refreshRoom(); Refresh();
        }
        catch (InvalidOperationException ex) { EditMessage.Text = ex.Message; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { _editing = false; _baseline = null; _displayedDrafts = null; EditMessage.Text = "Unsaved edits discarded."; Refresh(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

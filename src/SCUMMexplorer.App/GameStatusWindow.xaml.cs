using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

/// <summary>A named yes/no fact about the game ("this scene happened"), shown as a checkbox. The game-specific code decides how they are found.</summary>
public sealed record StateFlag(string Key, string Label, string Detail);

public partial class GameStatusWindow : Window
{
    /// <summary>The generic state-flag list: what to show, how to read a flag and how to set it. Supplied by the game-specific code.</summary>
    public sealed record FlagSource(Func<LoomExplorationSession, IReadOnlyList<StateFlag>> Flags, Func<LoomExplorationSession, string, bool?> Read,
        Action<LoomExplorationSession, string, bool> Write, string Intro);

    public sealed class FlagRow : INotifyPropertyChanged
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required string Detail { get; init; }
        public bool Baseline { get; set; }
        private bool _checked;
        public bool Checked { get => _checked; set { if (_checked == value) return; _checked = value; PropertyChanged?.Invoke(this, new(nameof(Checked))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly FlagSource? _flagSource;
    private readonly List<FlagRow> _flagRows = [];
    private LoomExplorationSession? _flagSession;

    private readonly Func<LoomExplorationSession?> _session;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _advanced;
    private readonly Action _refreshRoom;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly LoomStatusArtwork? _artwork;
    private readonly string? _artworkError;
    private LoomExplorationSession? _editedSession;
    private LoomGameStatus? _baseline;
    private bool? _staffHeld;
    private int? _noteCount;
    private bool _loadingRows;
    private readonly List<DraftRow> _rows = [];

    public sealed class DraftRow : INotifyPropertyChanged
    {
        public required int Slot { get; init; }
        public string Title { get; private set; } = "";
        private int _note1, _note2, _note3, _note4;
        private bool? _learned, _reversible;
        private string _origin = "";
        public int Note1 { get => _note1; set => Set(ref _note1, value); }
        public int Note2 { get => _note2; set => Set(ref _note2, value); }
        public int Note3 { get => _note3; set => Set(ref _note3, value); }
        public int Note4 { get => _note4; set => Set(ref _note4, value); }
        public bool? Learned { get => _learned; set => Set(ref _learned, value); }
        public bool? Reversible { get => _reversible; private set => Set(ref _reversible, value); }
        public string Origin { get => _origin; private set => Set(ref _origin, value); }
        public bool Resolved => Learned.HasValue && Notes.All(n => n is >= 1 and <= 8);
        public int[] Notes => [Note1, Note2, Note3, Note4];
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
        public void Load(LoomDraftStatus draft)
        {
            Title = LoomDraftNames.Title(draft); PropertyChanged?.Invoke(this, new(nameof(Title)));
            Note1 = draft.Notes.ElementAtOrDefault(0); Note2 = draft.Notes.ElementAtOrDefault(1);
            Note3 = draft.Notes.ElementAtOrDefault(2); Note4 = draft.Notes.ElementAtOrDefault(3);
            Learned = draft.Learned; Reversible = draft.Reversible; Origin = draft.Origin;
            PropertyChanged?.Invoke(this, new(nameof(Resolved)));
        }
    }

    public GameStatusWindow(Func<LoomExplorationSession?> session, Func<bool> canEdit, Action refreshRoom,
        GameLibrary? game = null, Func<bool>? advanced = null, FlagSource? flags = null)
    {
        _flagSource = flags;
        InitializeComponent(); _session = session; _canEdit = canEdit; _refreshRoom = refreshRoom; _advanced = advanced ?? (() => false);
        _artwork = game is null ? null : LoomStatusArtwork.TryLoad(game, out _artworkError);
        double noteHeight = (_artwork?.Notes[0].PixelHeight ?? 20) * 2;
        NoteButtons.Height = noteHeight;
        foreach (var column in new[] { Note1Column, Note2Column, Note3Column, Note4Column }) column.ItemsSource = LoomNoteNames.Choices;
        for (int i = 1; i <= 8; i++)
        {
            int count = i;
            var button = new Button { Style = (Style)FindResource("ArtworkChoice"), Tag = count,
                Width = 48, Height = noteHeight, ToolTip = $"Available through {LoomNoteNames.Label(Math.Max(3, count))}" + (count < 3 ? " (minimum C, D, E)" : "") };
            System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
            var content = new StackPanel();
            content.Children.Add(new Image { Width = 48, Height = noteHeight, Stretch = Stretch.Fill, SnapsToDevicePixels = true });
            RenderOptions.SetBitmapScalingMode(content.Children[0], BitmapScalingMode.NearestNeighbor);
            if (_artwork is null) content.Children.Add(new TextBlock { Text = LoomNoteNames.Label(count), HorizontalAlignment = HorizontalAlignment.Center });
            button.Content = content;
            button.Click += (_, _) => { if (!_canEdit()) return; _staffHeld = true; _noteCount = Math.Max(3, count); UpdateSelectors(); UpdateControls(); };
            Canvas.SetLeft(button, 16 + (count - 1) * 48);
            NoteButtons.Children.Add(button);
        }
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
        Refresh();
    }

    private bool PendingChanges => _baseline is not null && (_staffHeld != _baseline.StaffHeld || _noteCount != _baseline.AvailableNoteCount ||
        _rows.Any(r => _baseline.Drafts.FirstOrDefault(d => d.Slot == r.Slot) is { } d && (!r.Notes.SequenceEqual(d.Notes) || r.Learned != d.Learned)) ||
        _flagRows.Any(f => f.Checked != f.Baseline));

    private static bool SameState(LoomGameStatus a, LoomGameStatus b) => a.StaffHeld == b.StaffHeld && a.AvailableNoteCount == b.AvailableNoteCount &&
        a.Drafts.Select(d => (d.Packed, d.ForwardVerb, d.ReverseVerb)).SequenceEqual(b.Drafts.Select(d => (d.Packed, d.ForwardVerb, d.ReverseVerb)));

    private void Refresh()
    {
        var session = _session(); var status = session?.GameStatus;
        if (!ReferenceEquals(session, _editedSession))
        {
            if (_editedSession is not null) EditMessage.Text = "The exploration session was replaced. Unapplied changes were discarded.";
            _baseline = null; _editedSession = session;
        }
        SourceEvidence.Text = (status?.Evidence ?? "No exploration session.") + (_artworkError is null ? "" : "\n" + _artworkError);
        SessionSummary.Text = status is null ? "Open a LOOM room first." : !status.Supported ? "Status fields are unresolved for this game edition." : $"Room {session!.Snapshot.RoomId:00}";
        var visibility = _advanced() ? Visibility.Visible : Visibility.Collapsed;
        SourceExpander.Visibility = StaffOrigin.Visibility = NotesOrigin.Visibility = StateSourceColumn.Visibility = visibility;
        if (!PendingChanges && status is not null)
        {
            _baseline = status; _staffHeld = status.StaffHeld; _noteCount = status.AvailableNoteCount;
            if (_rows.Count != status.Drafts.Count)
            {
                _rows.Clear(); _rows.AddRange(status.Drafts.Select(d => new DraftRow { Slot = d.Slot }));
                foreach (var row in _rows) row.PropertyChanged += (_, _) => { if (!_loadingRows) UpdateControls(); };
                DraftGrid.ItemsSource = _rows;
            }
            _loadingRows = true;
            foreach (var row in _rows) row.Load(status.Drafts.Single(d => d.Slot == row.Slot));
            _loadingRows = false;
        }
        RefreshFlags(session, status);
        StaffOrigin.Text = status?.StaffOrigin ?? "Unresolved"; NotesOrigin.Text = status?.NotesOrigin ?? "Unresolved";
        StaffOrigin.ToolTip = StaffOrigin.Text; NotesOrigin.ToolTip = NotesOrigin.Text;
        UpdateSelectors(); UpdateControls();
    }

    private void RefreshFlags(LoomExplorationSession? session, LoomGameStatus? status)
    {
        if (_flagSource is null || session is null || status?.Supported != true) { FlagExpander.Visibility = Visibility.Collapsed; return; }
        FlagExpander.Visibility = Visibility.Visible;
        if (!ReferenceEquals(_flagSession, session))
        {
            _flagSession = session; _flagRows.Clear();
            _flagRows.AddRange(_flagSource.Flags(session).Select(f => new FlagRow { Key = f.Key, Label = f.Label, Detail = f.Detail }));
            foreach (var row in _flagRows) row.PropertyChanged += (_, _) => UpdateControls();
            FlagList.ItemsSource = _flagRows; FlagIntro.Text = _flagSource.Intro;
            FlagFilter_Changed(null, null);
        }
        if (_flagRows.Any(f => f.Checked != f.Baseline)) return;
        foreach (var row in _flagRows)
        {
            var value = _flagSource.Read(session, row.Key) ?? false;
            row.Baseline = value; row.Checked = value;
        }
    }

    private void FlagFilter_Changed(object? sender, TextChangedEventArgs? e)
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_flagRows);
        var text = FlagFilter.Text.Trim();
        view.Filter = text.Length == 0 ? null : o => o is FlagRow r && (r.Label.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Detail.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateControls()
    {
        bool editable = _session()?.GameStatus.Supported == true && _canEdit();
        bool stale = _baseline is not null && _session() is { } s && !SameState(s.GameStatus, _baseline);
        StaffButton.IsEnabled = editable && _staffHeld.HasValue;
        NoteButtons.IsEnabled = editable && _noteCount.HasValue && _staffHeld.HasValue;
        DraftGrid.IsReadOnly = !editable;
        FlagList.IsEnabled = editable;
        ApplyButton.IsEnabled = !PendingChanges || editable && !stale;
        if (PendingChanges) EditMessage.Text = stale ? "Game status changed. Close and reopen to inspect the new values before applying." :
            editable ? "Unapplied changes" : "Unapplied changes. Finish playback, dialogue or movement to apply.";
        else if (!editable) EditMessage.Text = "Finish playback, dialogue or movement to edit.";
        else if (EditMessage.Text.StartsWith("Finish ", StringComparison.Ordinal) || EditMessage.Text.StartsWith("Unapplied", StringComparison.Ordinal)) EditMessage.Text = "";
    }

    private void UpdateSelectors()
    {
        StaffImage.Source = _staffHeld == true ? _artwork?.Staff : _artwork?.EmptyStaff;
        StaffLabel.Text = _staffHeld is true ? "Held" : _staffHeld is false ? "Not held" : "Unresolved";
        System.Windows.Automation.AutomationProperties.SetItemStatus(StaffButton, StaffLabel.Text);
        StaffButton.BorderThickness = new Thickness(_staffHeld == true ? 0 : 1);
        foreach (Button button in NoteButtons.Children)
        {
            int count = (int)button.Tag; bool selected = _staffHeld == true && _noteCount == count; bool known = _staffHeld == true && count <= _noteCount;
            var img = (Image)((StackPanel)button.Content).Children[0];
            img.Source = known ? _artwork?.Notes[count - 1] : _artwork?.EmptyNotes[count - 1];
            button.BorderThickness = new Thickness(known ? 0 : 1);
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "Selected limit" : known ? "Available" : "Unavailable");
        }
        NoteIdentities.Text = _noteCount is null ? "Unresolved" : _staffHeld != true ? "Staff required" : _noteCount == 0 ? "No notes available" : "Available: " + string.Join(", ", Enumerable.Range(1, _noteCount.Value).Select(LoomNoteNames.Label));
    }

    private void Staff_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit() || !_staffHeld.HasValue) return;
        _staffHeld = !_staffHeld.Value;
        _noteCount = _staffHeld == true ? Math.Max(3, _noteCount ?? 0) : 0;
        UpdateSelectors(); UpdateControls();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DraftGrid.CommitEdit(DataGridEditingUnit.Cell, true); DraftGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!PendingChanges) { Close(); return; }
        if (!_canEdit() || _session() is not { } session || !ReferenceEquals(session, _editedSession) || _baseline is null) { Refresh(); return; }
        if (!SameState(session.GameStatus, _baseline)) { EditMessage.Text = "Game status changed. Close and reopen before applying."; UpdateControls(); return; }
        if (_staffHeld is not bool held || _noteCount is not int count || _rows.Any(r => !r.Resolved)) return;
        var flagChanges = _flagRows.Where(f => f.Checked != f.Baseline).ToList();
        try
        {
            foreach (var row in _rows)
            {
                var old = _baseline.Drafts.Single(d => d.Slot == row.Slot);
                if (!row.Notes.SequenceEqual(old.Notes)) session.UpdateDraftSequence(row.Slot, row.Notes);
                if (row.Learned != old.Learned) session.UpdateDraftLearned(row.Slot, row.Learned!.Value);
            }
            if (held != _baseline.StaffHeld) session.UpdateStaffPossession(held);
            if (count != _baseline.AvailableNoteCount) session.UpdateAvailableNotes(count);
            foreach (var flag in flagChanges) { _flagSource!.Write(session, flag.Key, flag.Checked); flag.Baseline = flag.Checked; }
            _baseline = null; _refreshRoom(); Close();
        }
        catch (InvalidOperationException ex) { EditMessage.Text = ex.Message; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

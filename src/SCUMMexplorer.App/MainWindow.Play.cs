using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// Play in a verb-based game (REQUIREMENTS §7.1, Proposed R-7.7 to R-7.18; S6 Indy3Ega, 15–16 October 2026): the room view becomes the game's own screen,
/// composed every frame from a live <see cref="ScummGameSession"/> (<see cref="ScummPlayScreen"/>): the room, its objects and actors, the messages, the verb
/// area with the sentence line and inventory the game draws, and the game's crosshair cursor in place of the system pointer. Each press of the left button
/// is one click for the game's input script (<see cref="ScummSession.ClickScreen"/>); keys go as R-7.11 says. Space pauses, Esc skips a cutscene from
/// its override point, '.' ends the current line. The app chooses no verb, object or sentence.
/// </summary>
public partial class MainWindow
{
    private ScummGameSession? _play;
    private ScummPlayScreen? _playScreen;
    private string? _playFolder;
    private DispatcherTimer? _playTimer;
    private bool _playPaused;
    private int _playFrame;
    private ScummPlayFrame? _playShown;
    private WriteableBitmap? _playBitmap;

    private bool PlayActive => _playTimer is not null && _play is not null;

    /// <summary>Starts (or continues) play in the room the user opened: the session boots once per game; another room is visited from its state (state preview).</summary>
    private async Task StartPlayAsync(GameResource resource, CancellationToken token)
    {
        if (_game is not { } game || resource.ResourceId is not int roomId) return;
        try
        {
            if (_play is null || _playFolder != game.FolderPath)
            {
                StopPlay();
                ScummIndexedObjectSnapshot? objects = null;
                var session = await Task.Run(() => ScummGameSession.BootForPlay(game, out objects), token);
                if (token.IsCancellationRequested) return;
                _play = session;
                _playScreen = new ScummPlayScreen(game, session, objects!.States);
                _playFolder = game.FolderPath;
            }
            if (token.IsCancellationRequested) return;
            // A room the session is not in is entered as a state preview (the route to it is not played), as the room view's first visit is.
            if (_play.CurrentRoomId != roomId) _play.VisitRoom(resource);
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            StopPlay();
            if (IsAdvancedMode) StatusText.ToolTip = $"Play could not start: {ex.Message}";
            return;
        }
        _playPaused = false;
        _playTimer ??= new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _playTimer.Tick -= PlayTimer_Tick;
        _playTimer.Tick += PlayTimer_Tick;
        _playTimer.Interval = TimeSpan.FromMilliseconds(_play.FrameJiffies * 1000.0 / 60);
        _playTimer.Start();
        RoomDisplayGroup.Visibility = Visibility.Collapsed;
        PlayScreenImage.Visibility = Visibility.Visible;
        ObjectsNote.Text = "";
        ShowPlayFrame();
        PlayScreenImage.Focus();
    }

    /// <summary>Stops the frames and gives the room view back (another game, another kind of resource).</summary>
    private void StopPlay()
    {
        _playTimer?.Stop();
        _playTimer = null;
        PlayScreenImage.Visibility = Visibility.Collapsed;
        PlayScreenImage.Cursor = null;
        RoomDisplayGroup.Visibility = Visibility.Visible;
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (_play is null || _playPaused) return;
        _play.AdvanceFrame();
        _playFrame++;
        if (_playTimer is { } timer) timer.Interval = TimeSpan.FromMilliseconds(_play.FrameJiffies * 1000.0 / 60);
        ShowPlayFrame();
    }

    private void ShowPlayFrame()
    {
        if (_playScreen is null || _play is null) return;
        var frame = _playScreen.Compose(_playFrame);
        _playShown = frame;
        _playBitmap ??= new WriteableBitmap(ScummPlayFrame.Width, ScummPlayFrame.Height, 96, 96, PixelFormats.Bgra32, null);
        _playBitmap.WritePixels(new Int32Rect(0, 0, ScummPlayFrame.Width, ScummPlayFrame.Height), frame.Pixels, ScummPlayFrame.Width * 4, 0);
        PlayScreenImage.Source = _playBitmap;
        // R-7.9: the game's cursor replaces the system pointer while the game shows it, and nothing shows while it is off.
        PlayScreenImage.Cursor = Cursors.None;
        if (IsAdvancedMode)
            PlayScreenImage.ToolTip = $"Room {frame.RoomId:00} · input script {_play.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.VerbScript)} · " +
                                      (_play.DeliveredInput.LastOrDefault() is { } last ? $"last input runInputScript({last.Area}, {last.Value}, {last.Mode}): {last.Description}" : "no input yet") +
                                      (_playPaused ? " · paused" : "");
        else PlayScreenImage.ToolTip = null;
    }

    // The image's point as a game screen point (0..319, 0..199).
    private (int X, int Y)? PlayPoint(MouseEventArgs e)
    {
        if (PlayScreenImage.ActualWidth <= 0 || PlayScreenImage.ActualHeight <= 0) return null;
        var position = e.GetPosition(PlayScreenImage);
        var x = (int)(position.X / PlayScreenImage.ActualWidth * ScummPlayFrame.Width);
        var y = (int)(position.Y / PlayScreenImage.ActualHeight * ScummPlayFrame.Height);
        return x is >= 0 and < ScummPlayFrame.Width && y is >= 0 and < ScummPlayFrame.Height ? (x, y) : null;
    }

    private void PlayScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!PlayActive) return;
        _play!.PointAtScreen(PlayPoint(e));
        if (_playPaused) ShowPlayFrame();
    }

    private void PlayScreenImage_MouseLeave(object sender, MouseEventArgs e)
    {
        if (PlayActive) _play!.PointAtScreen(null);
    }

    // R-7.10: one press, one click, delivered at the next frame.
    private void PlayScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PlayActive || PlayPoint(e) is not { } point) return;
        PlayScreenImage.Focus();
        e.Handled = true;
        if (_play!.ClickScreen(point.X, point.Y, out var refused) is null && IsAdvancedMode) StatusText.ToolTip = $"Click not delivered: {refused}";
    }

    /// <summary>The engine's keys and the game's keys while playing; true when the key was taken.</summary>
    private bool PlayKeyDown(KeyEventArgs e)
    {
        if (!PlayActive || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F11) return false;
        if (key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)
        {
            // The game's pause key (VAR_PAUSE_KEY, Space in Indiana Jones) is the app's Pause.
            _playPaused = !_playPaused;
            ShowPlayFrame();
            e.Handled = true;
            return true;
        }
        if (key == Key.Escape && !_fullscreen)
        {
            // R-7.16: Esc skips a cutscene only from its override point (abortCutscene); otherwise the game reads it as its key.
            if (_play!.CutsceneSkippable) _play.PressKey(0x1B);
            e.Handled = true;
            return true;
        }
        int? code = key switch
        {
            >= Key.F1 and <= Key.F9 => 315 + (key - Key.F1),
            Key.Enter => 13,
            Key.Back => 8,
            >= Key.A and <= Key.Z when Keyboard.Modifiers == ModifierKeys.Control => 1 + (key - Key.A),
            _ => null
        };
        if (code is not int value) return false;
        _play!.PressPlayKey(value, out _);
        e.Handled = true;
        return true;
    }

    // Printable keys as the characters they type (verb shortcuts are lower-case letters; Script 24 reads '<' and '>').
    private void PlayTextInput(TextCompositionEventArgs e)
    {
        if (!PlayActive || string.IsNullOrEmpty(e.Text) || Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
        var character = e.Text[0];
        if (character is < ' ' or > '~' || character == ' ') return;
        e.Handled = true;
        if (character == '.') { _play!.SkipMessage(); return; }
        _play!.PressPlayKey(character, out _);
    }
}

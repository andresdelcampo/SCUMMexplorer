using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SCUMMexplorer.App;

/// <summary>
/// R-6.45 (user, 24 Oct 2026): the keys of the shared controls are one handler for every game. F11, Esc, Space and '.' act through the game's
/// <see cref="SCUMMexplorer.Core.Scumm.IRoomControlProvider"/> under the condition of the button each stands for; the game's own keys (LOOM's notes, a verb game's
/// shortcuts) are delivered by the game-specific handlers after these.
/// </summary>
public partial class MainWindow
{
    /// <summary>The game takes the user's clicks and keys now (LOOM's interactive session, a played game that is not a static preview).</summary>
    private bool GameTakesKeys => GameClickActive && HasDedicatedSession || PlayTakesInput;

    private bool SharedKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F11 && _game is not null && RoomDisplayLayout.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            SetFullscreen(!_fullscreen);
            return true;
        }
        var typing = Keyboard.FocusedElement is TextBox or ComboBox;
        var provider = CurrentRoomControls;
        var transport = provider.ReadTransport();
        if (key == Key.Escape)
        {
            // R-6.34: Skip when a cutscene plays (once it has set its skip point); in full screen Esc skips while a cutscene plays and otherwise leaves it.
            if (GameTakesKeys && transport.InCutscene && !typing && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                if (transport.CutsceneSkippable) { provider.Skip(); UpdatePlaybackControls(); }
                return true;
            }
            if (_fullscreen && !(GameTakesKeys && transport.InCutscene)) { e.Handled = true; SetFullscreen(false); return true; }
            return false;
        }
        if (key == Key.Space && !typing && Keyboard.Modifiers == ModifierKeys.None && (GameClickActive && HasDedicatedSession || PlayActive))
        {
            // R-6.32: the key acts under the Pause button's condition; a played game takes the key either way.
            if (transport.PauseEnabled) { provider.TogglePause(); UpdatePlaybackControls(); e.Handled = true; return true; }
            if (PlayActive) { e.Handled = true; return true; }
        }
        return false;
    }

    /// <summary>'.' is the Skip line key (R-6.35): it never reaches the game's scripts.</summary>
    private bool SharedTextInput(TextCompositionEventArgs e)
    {
        if (e.Text != "." || Keyboard.FocusedElement is TextBox or ComboBox || !GameTakesKeys) return false;
        e.Handled = true;
        var provider = CurrentRoomControls;
        if (provider.ReadTransport().SkipLineEnabled) { provider.SkipLine(); UpdatePlaybackControls(); }
        else StatusText.Text = "'.' (skip line): no text is shown.";
        return true;
    }
}

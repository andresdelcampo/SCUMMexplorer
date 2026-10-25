using System.Windows;
using System.Windows.Controls;

namespace SCUMMexplorer.App;

/// <summary>
/// R-6.49 (user, 25 Oct 2026): LOOM's scroll arrows and Left/Right keys for a game that is played. They move the game's own camera
/// (<see cref="SCUMMexplorer.Core.Scumm.ScummGameSession.PanCameraBy"/>), so the picture and the camera stay one thing and every click is a valid point of the game's screen.
/// </summary>
public partial class MainWindow
{
    private bool IsLeftPanButton(object? button) => ReferenceEquals(button, ScrollRoomLeftButton) || ReferenceEquals(button, PlayScrollLeftButton);

    private bool PlayPanCan(int direction)
    {
        if (_play is not { } play || play.CameraPanRange is not { } range || play.CameraX is not int x || play.InCutscene) return false;
        return direction < 0 ? x > range.Min : x < range.Max;
    }

    private void PlayPanBy(double sourcePixels)
    {
        if (_play is not { } play) return;
        if (play.InCutscene) { StopRoomPan(); return; }
        play.PanCameraBy((int)Math.Round(sourcePixels));
        UpdatePlayScrollButtons();
    }

    /// <summary>The arrows under the game's screen: shown where the room is wider than the screen and the player has control, as in LOOM.</summary>
    private void UpdatePlayScrollButtons()
    {
        if (!PlayActive || _play is not { } play) { PlayScrollButtonsPanel.Visibility = Visibility.Collapsed; return; }
        var framed = NormalRoomWidthToggle.IsChecked == true;
        var canScroll = framed && play.CameraPanRange is { } range && range.Max - range.Min > 0 && !play.InCutscene;
        PlayScrollButtonsPanel.Visibility = canScroll ? Visibility.Visible : framed ? Visibility.Hidden : Visibility.Collapsed;
        PlayScrollButtonsPanel.Width = Math.Max(1, Math.Min(RoomViewportHost.ActualWidth, (RoomViewportHost.ActualHeight - PlayScrollButtonsPanel.Height - 6) * 1.6));
        PlayScrollLeftButton.IsEnabled = canScroll && PlayPanCan(-1);
        PlayScrollRightButton.IsEnabled = canScroll && PlayPanCan(1);
    }
}

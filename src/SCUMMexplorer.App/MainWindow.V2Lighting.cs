using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    // Use the same source lighting as Play, retaining any explicitly inspected
    // stored image selection. Later engines keep their existing preview path.
    private byte[] ComposeRoomPreview(RoomImage room) =>
        _browsedScene is { EngineVersion: 1 or 2 } scene && ReferenceEquals(scene.Room, room)
            ? scene.Compose(_objectSelection)
            : _objectSelection.Compose(room);
}

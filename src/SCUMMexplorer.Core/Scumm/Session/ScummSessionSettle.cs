namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    protected ScummSettleResult SettleRoom(int frameLimit, int quietFrames, CancellationToken cancellationToken = default, Func<bool>? extraBusy = null, bool restlessActors = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var room = _currentRoom?.ResourceId ?? throw new InvalidOperationException("Visit a room before settling it.");
        var capture = CaptureSessionRoom();
        // An object whose state or position changes four times within 240 frames is an ambient animator (a crowd, a flame): it never rests in a
        // living room, so it does not keep the room from settling; it is shown in the phase of its loop the room was in when the rest began.
        var changes = new Dictionary<int, Queue<int>>();
        var previous = ObjectValues();
        var signature = Signature(changes, 0);
        var quiet = 0;
        var actorPlaces = new Dictionary<int, int>();
        for (var frame = 1; frame <= frameLimit; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdvanceFrame();
            if (_currentRoom?.ResourceId != room) return new(capture, ScummSettleOutcome.RoomLeft, frame, _currentRoom?.ResourceId);
            var now = ObjectValues();
            foreach (var (id, value) in now)
                if (!previous.TryGetValue(id, out var before) || before != value)
                {
                    if (!changes.TryGetValue(id, out var queue)) changes[id] = queue = new();
                    queue.Enqueue(frame);
                }
            // An actor that moves all the time (a patrolling guard, Indy 3's castle) never rests either, as an ambient object; the hero's own walk stays busy.
            if (restlessActors)
                foreach (var actor in Actors.Where(a => a.Room == room))
                {
                    var key = -actor.Number;
                    var at = (actor.X ?? 0) * 1000 + (actor.Y ?? 0);
                    if (actorPlaces.TryGetValue(key, out var was) && was != at)
                    {
                        if (!changes.TryGetValue(key, out var queue)) changes[key] = queue = new();
                        queue.Enqueue(frame);
                    }
                    actorPlaces[key] = at;
                }
            foreach (var queue in changes.Values)
                while (queue.Count > 0 && queue.Peek() < frame - 240) queue.Dequeue();
            previous = now;
            var current = Signature(changes, frame);
            var busy = InCutscene || Messages.HaveMsg != 0 || EgoMoving || CameraMoving || extraBusy?.Invoke() == true;
            capture = CaptureSessionRoom();
            if (current != signature) { signature = current; quiet = 0; }
            else quiet = busy ? 0 : quiet + 1;
            if (quiet >= quietFrames) return new(capture, ScummSettleOutcome.Settled, frame, null);
        }
        return new(CaptureSessionRoom(), ScummSettleOutcome.FrameLimit, frameLimit, null);
    }

    private Dictionary<int, int> ObjectValues()
    {
        var values = new Dictionary<int, int>(_runtime.ObjectStates.Count);
        var roomObjects = _runtime.IsV2 ? _currentObjects.Select(item => item.Id).ToHashSet() : null;
        foreach (var pair in _runtime.ObjectStates)
            if (roomObjects is null || roomObjects.Contains(pair.Key)) values[pair.Key] = pair.Value;
        foreach (var pair in _runtime.ObjectPositions)
            if (roomObjects is null || roomObjects.Contains(pair.Key)) values[pair.Key + 100000] = pair.Value.X * 1000 + pair.Value.Y;
        return values;
    }

    protected ScummRoomCapture CaptureSessionRoom() => new(Snapshot, Actors, CameraX, _runtime.ShadowPalette.ToArray(), _runtime.RoomColors.ToArray(),
        _runtime.GlobalVariables.GetValueOrDefault(_runtime.CurrentLightsVariable), _runtime.Flashlight, CurrentWalkBoxes)
        { RoomPointer = ScreenPointer is { } pointer ? (pointer.X, pointer.Y - MainScreen.Top)
              : Pointer is { } roomPointer ? (roomPointer.X - ViewLeft, roomPointer.Y) : (0, 0),
          ViewLeft = ViewLeft, ViewWidth = 320, ViewHeight = MainScreen.Bottom - MainScreen.Top };

    private long Signature(Dictionary<int, Queue<int>> changes, int frame)
    {
        bool Ambient(int id) => changes.TryGetValue(id, out var queue) && queue.Count >= 4;
        var hash = new HashCode();
        // v1/v2 background scripts animate objects in rooms that are not loaded. Their flags still
        // belong to persistent state, but cannot keep the current room's picture from settling.
        var roomObjects = _runtime.IsV2 ? _currentObjects.Select(item => item.Id).ToHashSet() : null;
        foreach (var pair in _runtime.ObjectStates.OrderBy(item => item.Key))
            if ((roomObjects is null || roomObjects.Contains(pair.Key)) && !Ambient(pair.Key)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        foreach (var pair in _runtime.ObjectPositions.OrderBy(item => item.Key))
            if ((roomObjects is null || roomObjects.Contains(pair.Key)) && !Ambient(pair.Key + 100000)) { hash.Add(pair.Key); hash.Add(pair.Value.X); hash.Add(pair.Value.Y); }
        foreach (var actor in Actors.Where(a => !Ambient(-a.Number))) { hash.Add(actor.Number); hash.Add(actor.Room); hash.Add(actor.X); hash.Add(actor.Y); hash.Add(actor.Visible); hash.Add(actor.Look.Costume); }
        foreach (var colour in _runtime.ShadowPalette.Take(16)) hash.Add(colour);
        foreach (var colour in _runtime.RoomColors.Take(16)) hash.Add(colour);
        hash.Add(CameraX);
        return hash.ToHashCode();
    }
}

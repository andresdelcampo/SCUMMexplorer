using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A room as the game's own scripts leave it on its first visit (S5, 14 October 2026): the background, the object records and images, the state captured
/// while the room was current (<see cref="State"/>) and the picture selection built from it. The index gives every object its starting state; what a
/// room shows comes from the entry hooks, the entry script and the child scripts, run until the room settled.
/// </summary>
public sealed record ScummRoomScene(int RoomId, RoomImage Room, IReadOnlyList<ScummRoomObjectRecord> Records, IReadOnlyList<RoomObjectImage> Images,
    ScummRoomCapture State, RoomObjectSelection Selection, ScummSettleOutcome Outcome, int Frames, int? LeftTo,
    Func<int, ScummCostume?> Costumes, bool IsFirstRoomOfTheGame, IReadOnlyList<string> Notes)
{
    public ScummSessionSnapshot Snapshot => State.Snapshot;
    /// <summary>v1/v2 draw only intrinsic bit 8; later engines use the whole state nibble.</summary>
    public byte IntrinsicStateMask { get; init; } = 0x0F;
    public int EngineVersion { get; init; } = 3;

    /// <summary>The object states the game's index gives (the picture selection needs them for objects the scripts did not touch).</summary>
    public IReadOnlyDictionary<int, byte> IndexedStates { get; init; } = new Dictionary<int, byte>();

    /// <summary>A selection of its own for a view: inspecting stored alternatives changes it, and the cached scene's must stay as the scripts left it.</summary>
    public RoomObjectSelection NewSelection() =>
        new(Images, Snapshot.ObjectStates, Snapshot.ObjectPositions, Records, Room, IndexedStates, Snapshot.DrawOrder, IntrinsicStateMask);

    /// <summary>The room's pixels with the objects the scripts made visible, drawn through the room colour table (<c>roomColorLegacy</c>).</summary>
    public byte[] Compose(RoomObjectSelection? selection = null)
    {
        var pixels = (selection ?? Selection).Compose(Room);
        var map = State.RoomColors;
        for (var i = 0; i < pixels.Length; i++) pixels[i] = map[pixels[i]];
        return ScummV2Lighting.Compose(pixels, Room.Width, Room.Height, State.Lights, State.Flashlight, EngineVersion,
            State.RoomPointer, State.ViewLeft, State.ViewWidth, State.ViewHeight);
    }

    /// <summary>The actors the scripts left in the room, as an indexed layer the size of the room.</summary>
    public ScummActorLayer ActorLayer() => ScummActorRenderer.Render(State.Actors, RoomId, Room.Width, Room.Height, Costumes,
        Selection.ComposeDepthMask(Room), State.WalkBoxes, Snapshot.ObjectClasses, Room.ExtraDepthMasks.Select(plane => plane.Pixels.ToArray()).ToArray(), EngineVersion, State.Lights);

    /// <summary>The objects with the actors over them.</summary>
    public byte[] ComposeWithActors()
    {
        var pixels = Compose();
        var layer = ActorLayer();
        for (var i = 0; i < pixels.Length; i++)
            if (layer.IsDrawn(i)) pixels[i] = layer.Pixels[i];
        return pixels;
    }

    /// <summary>The room's colours as the shadow palette (<c>roomOps roomPalette</c>) shows them.</summary>
    public IReadOnlyList<uint> DisplayPalette()
    {
        var shadow = State.ShadowPalette;
        var shown = new uint[Room.Palette.Count];
        for (var i = 0; i < shown.Length; i++) shown[i] = shadow[i] < Room.Palette.Count ? Room.Palette[shadow[i]] : Room.Palette[i];
        return shown;
    }

    /// <summary>
    /// The palette for the room picture and its objects: the colour table of <c>roomColorLegacy</c> (applied when they were drawn) and then the shadow
    /// palette (applied when the screen is shown). Actors are drawn through <see cref="DisplayPalette"/> only.
    /// </summary>
    public IReadOnlyList<uint> BackgroundPalette()
    {
        var map = State.RoomColors;
        var shadow = State.ShadowPalette;
        var shown = new uint[Room.Palette.Count];
        for (var i = 0; i < shown.Length; i++)
        {
            var drawn = map[i] < Room.Palette.Count ? map[i] : i;
            shown[i] = shadow[drawn] < Room.Palette.Count ? Room.Palette[shadow[drawn]] : Room.Palette[drawn];
        }
        return shown;
    }

    /// <summary>
    /// Whether the game's own object search (<c>findObject</c>) can select this object here: it has a box, it is not at 0, 0 (off the scene), the room owns it,
    /// it is not untouchable (class 24, R-6.15) and its parent chain is in the required states. Doorways and stored boxes outside these rules are not hotspots.
    /// </summary>
    public bool IsHotspot(ScummRoomObjectRecord record)
    {
        if (record.Width <= 0 || record.Height <= 0) return false;
        var at = Snapshot.ObjectPositions.TryGetValue(record.Id, out var moved) ? moved : new RoomObjectPosition(record.X, record.Y);
        if (at == new RoomObjectPosition(0, 0)) return false;
        if (Snapshot.ObjectOwners.GetValueOrDefault(record.Id, (byte)0x0F) != 0x0F || Snapshot.IsUntouchable(record.Id)) return false;
        if (IntrinsicStateMask == 8 && (Snapshot.ObjectStates.GetValueOrDefault(record.Id) & 2) != 0) return false;
        var current = record;
        for (var steps = 0; current.ParentLocalIndex != 0; steps++)
        {
            var parent = Records.FirstOrDefault(item => item.LocalIndex == current.ParentLocalIndex);
            if (steps > Records.Count || parent is null || (Snapshot.ObjectStates.GetValueOrDefault(parent.Id) & IntrinsicStateMask) != current.ParentRequiredState) return false;
            current = parent;
        }
        return true;
    }

    /// <summary><c>VAR_CURRENT_LIGHTS</c> bit 1 (ScummVM <c>LIGHTMODE_room_lights_on</c>): when clear, the game shows the room dark except under a flashlight.</summary>
    public bool RoomLit => (State.Lights & 2) != 0;

    /// <summary>Rectangles of stored images that show colour 0 where the game's scripts left the room (a gap).</summary>
    public IReadOnlyList<RoomGap> Gaps() => Selection.VisibleGaps();
}

/// <summary>
/// Runs a game's own scripts to the first-visit state of a room in a fresh generic session: the boot (the earliest normal route to the first
/// playable room), then, for any other room, a visit that does not play the route to it (<see cref="ScummGameSession.VisitRoom"/>), then the frames
/// until the room settles (<see cref="ScummGameSession.Settle"/>). Anything the run cannot follow is in the snapshot's uncertainties, never skipped.
/// </summary>
public static class ScummRoomFirstVisit
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<ScummRoomScene?>> Scenes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The first visit of a room, run once per game folder and room: the room view and the room card of the same room share it. Null when the run cannot be made
    /// (the plain background is shown instead; the app says so in Advanced only).
    /// </summary>
    public static ScummRoomScene? Cached(GameLibrary game, int roomId) =>
        Scenes.GetOrAdd($"{game.FolderPath}|{roomId}", _ => new Lazy<ScummRoomScene?>(() =>
        {
            try { return Run(game, roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException) { return null; }
        })).Value;

    public static ScummRoomScene Run(GameLibrary game, int roomId, int randomSeed = 0, Action<ScummGameSession>? prepareBoot = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        var profile = ScummProfile.For(game);
        var resource = game.Rooms.FirstOrDefault(item => item.ResourceId == roomId) ?? throw new InvalidDataException($"Room {roomId:00} is not in this game.");
        var data = ScummDataSources.For(game);
        ScummIndexedObjectSnapshot indexed;
        RoomImage room;
        if (data is ScummV2DataSource v2)
        {
            indexed = v2.ReadIndexedObjects();
            room = v2.ReadBackground(roomId);
        }
        else if (data is ScummV5DataSource v5)
        {
            // A v5 game (S5 MonkeyCd): the index's DOBJ gives the starting states, the resource file the room's picture.
            var holder = game.V4ArchiveFor(resource) ?? throw new InvalidDataException($"Room {roomId:00} is not in the game's resource file.");
            indexed = v5.ReadIndexedObjects();
            room = holder.DecodeRoom(roomId);
        }
        else if (profile.UsesV4Archive)
        {
            // A v4 game (one archive, or several disks: S5 MonkeyFloppy): the index (000.LFL, the same on every disk) gives the starting states, the
            // disk that holds the room gives its picture.
            var index = game.V4Archive as ScummV4Archive ?? game.V4Disks?.Disks.OfType<ScummV4Archive>().FirstOrDefault()
                ?? throw new InvalidDataException("The game has no v4 archive to read its index from.");
            var holder = game.V4ArchiveFor(resource) ?? throw new InvalidDataException($"Room {roomId:00} is on none of the game's disks.");
            indexed = index.ReadIndexedObjects();
            room = holder.DecodeRoom(roomId);
        }
        else
        {
            indexed = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
            room = new ScummV3EgaRoomDecoder().Decode(File.ReadAllBytes(resource.FilePath));
        }
        var session = new ScummGameSession(game, indexed, randomSeed, null, data);
        prepareBoot?.Invoke(session);
        var bootRoom = session.CurrentRoomId == roomId;
        if (!bootRoom) session.VisitRoom(resource);
        var settled = session.Settle();
        var records = data.ReadRoom(roomId).Objects;
        var snapshot = settled.State.Snapshot;
        // v5 objects store one image per state (S5 Indy4, S4b Monkey2): the settled state picks it.
        var images = data is ScummV5DataSource stateImages ? stateImages.ReadObjectImages(roomId, snapshot.ObjectStates) : data.ReadObjectImages(roomId);
        var stateMask = (byte)(profile.EngineVersion <= ScummVersion.V2 ? 8 : 15);
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, records, room, indexed.States, snapshot.DrawOrder, stateMask);
        var notes = new List<string>
        {
            bootRoom ? $"Room {roomId:00} is the first room of the game: the boot's own state after {settled.Frames} more frames."
                     : $"Room {roomId:00} was entered from the first room's state without playing the route to it (state preview), then ran {settled.Frames} frames.",
            settled.Outcome switch
            {
                ScummSettleOutcome.Settled => "The room settled: nothing changed for a while.",
                ScummSettleOutcome.RoomLeft => $"A script left the room for Room {settled.LeftTo:00}: the room is shown as it was the last time it was current.",
                _ => "The room never rested within the frame limit (ambient scripts keep changing it): the state at the limit is shown."
            }
        };
        return new ScummRoomScene(roomId, room, records, images, settled.State, selection, settled.Outcome, settled.Frames, settled.LeftTo,
            id => { try { return session.ReadCostume(id); } catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException) { return null; } },
            bootRoom, notes) { IndexedStates = indexed.States, IntrinsicStateMask = stateMask, EngineVersion = (int)ScummProfile.For(game).EngineVersion };
    }
}

using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// One frame of the game screen while the player plays (R-7.8, R-7.9): 320 × 200 colours, the room screen (rows <see cref="RoomTop"/> to
/// <see cref="RoomBottom"/>, the room from <see cref="ViewLeft"/>), the verb area and its verbs, and whether the game's cursor shows.
/// </summary>
public sealed record ScummPlayFrame(uint[] Pixels, int? RoomId, int RoomTop, int RoomBottom, int ViewLeft, ScummVerbAreaPicture Verbs, bool CursorShown)
{
    public const int Width = 320;
    public const int Height = 200;
}

/// <summary>
/// Composes the game screen of a live verb-based session, as ScummVM shows it (engines/scumm gfx.cpp, verbs.cpp, cursor.cpp, read 15 October 2026):
/// the text screen above the room screen (<c>initScreens</c>), the room with the objects and actors the scripts left (the same composition as a first
/// visit, <see cref="ScummRoomScene"/>), the messages with the game's charset, the verb screen with its text and image verbs, and the built-in cursor of the
/// version 3 and 4 games other than LOOM: cursor 0 of <c>ScummEngine_v5::resetCursors</c>, a 16 × 16 cross-hair with its hotspot at (8, 7) in the gap of
/// the cross (<see cref="CursorPoints"/>), in colour 15, 15, 7, 8 by <c>animateCursor</c>, one step every two frames (cursor.cpp and scumm_v4.h, read 16
/// October 2026: <c>ScummEngine_v3 : ScummEngine_v4 : ScummEngine_v5</c>, so version 3 has it too; before 16 October the screen drew the 23 × 21 crosshair
/// of <c>ScummEngine_v2</c>, found wrong by S6 Indy3Vga). Evidence level <c>engine</c>.
/// </summary>
public sealed class ScummPlayScreen
{
    private static readonly byte[] CursorColors = [15, 15, 7, 8];
    private readonly GameLibrary _game;
    private readonly ScummGameSession _session;
    private readonly IScummDataSource _data;
    private readonly IReadOnlyDictionary<int, byte> _indexedStates;
    private readonly Dictionary<int, (RoomImage Room, IReadOnlyList<ScummRoomObjectRecord> Records, IReadOnlyList<RoomObjectImage> Images)> _rooms = [];

    public ScummPlayScreen(GameLibrary game, ScummGameSession session, IReadOnlyDictionary<int, byte> indexedStates)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _indexedStates = indexedStates ?? throw new ArgumentNullException(nameof(indexedStates));
        _data = ScummDataSources.For(game);
    }

    /// <summary>The scene of the current room at this frame (objects, actors, palettes), or null outside a room.</summary>
    public ScummRoomScene? Scene()
    {
        if (_session.CurrentRoomId is not int roomId) return null;
        // One capture per scene (its snapshot also answers whether the session is in that room): the snapshot copies the variable tables, so it is not built twice (R-6.48).
        var capture = _session.CaptureRoom();
        if (capture.Snapshot.RoomId != roomId) return null;
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            var resource = _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId);
            if (resource is null) return null;
            var image = _data is ScummV2DataSource v2 ? v2.ReadBackground(roomId) : ScummProfile.For(_game).UsesV4Archive
                ? (_game.V4ArchiveFor(resource) ?? throw new InvalidDataException($"Room {roomId:00} is on none of the game's disks.")).DecodeRoom(roomId)
                : new ScummV3EgaRoomDecoder().Decode(File.ReadAllBytes(resource.FilePath));
            _rooms[roomId] = room = (image, _data.ReadRoom(roomId).Objects, _data.ReadObjectImages(roomId));
        }
        var snapshot = capture.Snapshot;
        // S6 Monkey2 (17 October 2026): a v5 object stores one image per state (IM01, IM02, …) and drawObject takes getObjectImage(ptr, getState(obj))
        // (object.cpp), so each frame picks the image of the object's current state, as the first visit does (ScummRoomScene); older versions keep one image.
        var images = _data is ScummV5DataSource stateImages ? stateImages.ReadObjectImages(roomId, snapshot.ObjectStates) : room.Images;
        byte intrinsicMask = _data is ScummV2DataSource ? (byte)8 : (byte)15;
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, room.Records, room.Room, _indexedStates, snapshot.DrawOrder, intrinsicStateMask: intrinsicMask);
        return new ScummRoomScene(roomId, room.Room, room.Records, images, capture, selection, ScummSettleOutcome.Settled, 0, null,
            id => { try { return _session.ReadCostume(id); } catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException) { return null; } },
            false, []) { IndexedStates = _indexedStates, IntrinsicStateMask = intrinsicMask, EngineVersion = (int)ScummProfile.For(_game).EngineVersion };
    }

    /// <summary>The screen at this frame; <paramref name="frame"/> drives the cursor's colour cycle.</summary>
    public ScummPlayFrame Compose(int frame)
    {
        var pixels = new uint[ScummPlayFrame.Width * ScummPlayFrame.Height];
        Array.Fill(pixels, 0xFF000000u);
        var (top, bottom) = _session.MainScreen;
        var viewLeft = _session.ViewLeft;
        var scene = Scene();
        // updatePalette (palette.cpp): with _shadowPalRemap (set by the v3 constructor outside CGA/Hercules/Mac b/w, scumm.cpp, read 16 October 2026) the
        // shadow palette of roomOps roomPalette remaps the whole displayed palette, so verbs, messages and the cursor follow it as the room does (S6 Indy3Vga:
        // the 256-colour gym's light switch darkens the screen through it).
        IReadOnlyList<uint> basePalette = scene?.DisplayPalette() ?? EgaPalette;
        if (scene is not null)
        {
            var room = scene.Compose();
            var background = scene.EngineVersion <= 2 ? scene.DisplayPalette() : scene.BackgroundPalette();
            var actors = scene.ActorLayer();
            var actorPalette = scene.DisplayPalette();
            var width = scene.Room.Width;
            for (var y = top; y < bottom; y++)
            {
                var roomY = y - top;
                if (roomY >= scene.Room.Height) break;
                for (var x = 0; x < ScummPlayFrame.Width; x++)
                {
                    var roomX = x + viewLeft;
                    if (roomX >= width) break;
                    var index = roomY * width + roomX;
                    pixels[y * ScummPlayFrame.Width + x] = actors.IsDrawn(index)
                        ? Colour(actorPalette, actors.Pixels[index]) : Colour(background, room[index]);
                }
            }
        }
        var verbs = _session.PlayVerbArea();
        for (var y = 0; y < verbs.Height; y++)
            for (var x = 0; x < verbs.Width; x++)
            {
                var screenY = ScummVerbArea.Top + y;
                if (screenY < bottom || screenY >= ScummPlayFrame.Height) continue;
                pixels[screenY * ScummPlayFrame.Width + x] = Colour(basePalette, verbs.Pixels[y * verbs.Width + x]);
            }
        if ((_session.PlayCharset(0) ?? _session.CurrentPlayCharset()) is { } charset)
        {
            var layer = charset.RenderMessages(_session.Messages.OnScreen, number => number is int n ? _session.PlayCharset(n) : null);
            for (var i = 0; i < layer.Pixels.Length; i++)
                if (layer.Pixels[i] != ScummV3MessageRenderer.Transparent) pixels[i] = Colour(basePalette, layer.Pixels[i]);
        }
        var cursor = _session.CursorShown;
        if (cursor && _session.ScreenPointer is { } pointer)
        {
            var colour = Colour(basePalette, CursorColors[(frame >> 1) & 3]);
            foreach (var (dx, dy) in _data is ScummV2DataSource ? OldCursorPoints : CursorPoints)
            {
                var (px, py) = (pointer.X + dx, pointer.Y + dy);
                if (px is >= 0 and < ScummPlayFrame.Width && py is >= 0 and < ScummPlayFrame.Height) pixels[py * ScummPlayFrame.Width + px] = colour;
            }
        }
        return new ScummPlayFrame(pixels, scene?.RoomId, top, bottom, viewLeft, verbs, cursor);
    }

    // ScummEngine_v5::resetCursors, cursor 0: one word per row, bit 15 is the left column; setBuiltinCursor paints the set bits.
    private static readonly ushort[] CrossHairImage =
        [0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0000, 0x7E3F, 0x0000, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0000];

    /// <summary>The cursor's lit pixels as offsets from the pointer (the hotspot, 8, 7 of the 16 × 16 image).</summary>
    public static IReadOnlyList<(int Dx, int Dy)> CursorPoints { get; } =
        [.. Enumerable.Range(0, 16).SelectMany(row => Enumerable.Range(0, 16).Where(bit => (CrossHairImage[row] & (1 << bit)) != 0).Select(bit => (15 - bit - 8, row - 7)))];
    // ScummEngine_v2::setBuiltinCursor, DOS 23x21 crosshair, hotspot (11,10).
    public static IReadOnlyList<(int Dx, int Dy)> OldCursorPoints { get; } = BuildOldCursor();
    private static IReadOnlyList<(int, int)> BuildOldCursor()
    {
        var points = new HashSet<(int, int)>();
        foreach (var sign in new[] { -1, 1 })
        {
            for (var i = 5; i <= 11; i++) points.Add((sign * i, 0));
            for (var i = 3; i <= 10; i++) points.Add((0, sign * i));
            foreach (var other in new[] { -1, 1 })
            {
                for (var i = 1; i <= 3; i++) { points.Add((sign * (5 + i), other * i)); points.Add((sign * i, other * (3 + i))); }
                points.Add((sign * 7, other)); points.Add((sign, other * 5));
            }
        }
        return points.ToArray();
    }
    private static uint Colour(IReadOnlyList<uint> palette, byte index) => index < palette.Count ? palette[index] | 0xFF000000u : 0xFF000000u;

    /// <summary>The 16 EGA colours (for the screen outside a room).</summary>
    public static readonly uint[] EgaPalette =
    [
        0xFF000000, 0xFF0000AA, 0xFF00AA00, 0xFF00AAAA, 0xFFAA0000, 0xFFAA00AA, 0xFFAA5500, 0xFFAAAAAA,
        0xFF555555, 0xFF5555FF, 0xFF55FF55, 0xFF55FFFF, 0xFFFF5555, 0xFFFF55FF, 0xFFFFFF55, 0xFFFFFFFF
    ];
}

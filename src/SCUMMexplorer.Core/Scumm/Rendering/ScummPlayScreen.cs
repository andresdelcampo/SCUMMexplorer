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
/// visit, <see cref="ScummRoomScene"/>), the messages with the game's charset, the verb screen with its text and image verbs, and the built-in crosshair
/// cursor of the v3 games other than LOOM (23 × 21, hotspot 11, 10; arms 5 to 11 pixels across and 3 to 10 down from it; colour 15, 15, 7, 8 by
/// <c>animateCursor</c>, one step every two frames; ScummVM's drawing, evidence level <c>engine</c>).
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
        _bitmapCursor = ScummProfile.For(game).UsesBitmapCursor;
    }

    private readonly bool _bitmapCursor;

    /// <summary>The scene of the current room at this frame (objects, actors, palettes), or null outside a room.</summary>
    public ScummRoomScene? Scene()
    {
        if (_session.CurrentRoomId is not int roomId || _session.Snapshot.RoomId != roomId) return null;
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            var resource = _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId);
            if (resource is null) return null;
            var image = ScummProfile.For(_game).UsesV4Archive
                ? (_game.V4ArchiveFor(resource) ?? throw new InvalidDataException($"Room {roomId:00} is on none of the game's disks.")).DecodeRoom(roomId)
                : new ScummV3EgaRoomDecoder().Decode(File.ReadAllBytes(resource.FilePath));
            _rooms[roomId] = room = (image, _data.ReadRoom(roomId).Objects, _data.ReadObjectImages(roomId));
        }
        var capture = _session.CaptureRoom();
        var snapshot = capture.Snapshot;
        var selection = new RoomObjectSelection(room.Images, snapshot.ObjectStates, snapshot.ObjectPositions, room.Records, room.Room, _indexedStates, snapshot.DrawOrder);
        return new ScummRoomScene(roomId, room.Room, room.Records, room.Images, capture, selection, ScummSettleOutcome.Settled, 0, null,
            id => { try { return _session.ReadCostume(id); } catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException) { return null; } },
            false, []) { IndexedStates = _indexedStates };
    }

    /// <summary>The screen at this frame; <paramref name="frame"/> drives the cursor's colour cycle.</summary>
    public ScummPlayFrame Compose(int frame)
    {
        var pixels = new uint[ScummPlayFrame.Width * ScummPlayFrame.Height];
        Array.Fill(pixels, 0xFF000000u);
        var (top, bottom) = _session.MainScreen;
        var viewLeft = _session.ViewLeft;
        var scene = Scene();
        IReadOnlyList<uint> basePalette = scene?.Room.Palette ?? EgaPalette;
        if (scene is not null)
        {
            var room = scene.Compose();
            var background = scene.BackgroundPalette();
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
            if (_bitmapCursor) DrawBitmapCursor(pixels, pointer.X, pointer.Y, colour);
            else DrawCrosshair(pixels, pointer.X, pointer.Y, colour);
        }
        return new ScummPlayFrame(pixels, scene?.RoomId, top, bottom, viewLeft, verbs, cursor);
    }

        // ScummEngine_v5::resetCursors, cursor 0 (the cross-hair of every version 4 and 5 game; cursor.cpp, read 16 October 2026): 16 × 16, bit 15 is the
    // left column, hotspot (8, 7); setBuiltinCursor paints the set bits in the cycling colour.
    private static readonly ushort[] CrossHairImage =
        [0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0000, 0x7E3F, 0x0000, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0080, 0x0000];

    private static void DrawBitmapCursor(uint[] pixels, int x, int y, uint colour)
    {
        for (var row = 0; row < 16; row++)
            for (var bit = 0; bit < 16; bit++)
            {
                if ((CrossHairImage[row] & (1 << bit)) == 0) continue;
                var (px, py) = (x - 8 + 15 - bit, y - 7 + row);
                if (px is >= 0 and < ScummPlayFrame.Width && py is >= 0 and < ScummPlayFrame.Height) pixels[py * ScummPlayFrame.Width + px] = colour;
            }
    }

    // setBuiltinCursor (v3 crosshair): from the hotspot, 7 pixels each side across starting 5 away, 8 pixels each side down starting 3 away.
    private static void DrawCrosshair(uint[] pixels, int x, int y, uint colour)
    {
        void Set(int px, int py)
        {
            if (px is >= 0 and < ScummPlayFrame.Width && py is >= 0 and < ScummPlayFrame.Height) pixels[py * ScummPlayFrame.Width + px] = colour;
        }
        for (var i = 0; i < 7; i++) { Set(x - 5 - i, y); Set(x + 5 + i, y); }
        for (var i = 0; i < 8; i++) { Set(x, y - 3 - i); Set(x, y + 3 + i); }
    }

    private static uint Colour(IReadOnlyList<uint> palette, byte index) => index < palette.Count ? palette[index] | 0xFF000000u : 0xFF000000u;

    /// <summary>The 16 EGA colours (for the screen outside a room).</summary>
    public static readonly uint[] EgaPalette =
    [
        0xFF000000, 0xFF0000AA, 0xFF00AA00, 0xFF00AAAA, 0xFFAA0000, 0xFFAA00AA, 0xFFAA5500, 0xFFAAAAAA,
        0xFF555555, 0xFF5555FF, 0xFF55FF55, 0xFF55FFFF, 0xFFFF5555, 0xFFFF55FF, 0xFFFFFF55, 0xFFFFFFFF
    ];
}

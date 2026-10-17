using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Classic v1 index and tiled images, sharing the v1/v2 room object and script layout.</summary>
public sealed class ScummV1DataSource : ScummV2DataSource
{
    private readonly string _indexPath;
    private readonly ScummV1Game _classicGame;
    public ScummV1DataSource(GameLibrary game) : this(game, ReadIndex(game)) { }
    private ScummV1DataSource(GameLibrary game, ScummV1Index index)
        : base(game, ScummScriptDialect.V1, index.Game == ScummV1Game.ManiacMansion ? 200 : 155)
    {
        _indexPath = Path.Combine(game.FolderPath, "00.LFL");
        _classicGame = index.Game;
    }
    private static ScummV1Index ReadIndex(GameLibrary game)
    {
        if (game.Version != ScummVersion.V1) throw new ArgumentException("The data source requires a v1 game.", nameof(game));
        return ScummV1Index.TryRead(Path.Combine(game.FolderPath, "00.LFL"))
            ?? throw new InvalidDataException("Invalid classic v1 index.");
    }
    public override ScummIndexedObjectSnapshot ReadIndexedObjects()
    {
        var bytes = LflEncoding.DecodeXorFF(File.ReadAllBytes(_indexPath));
        var count = _classicGame == ScummV1Game.ManiacMansion ? 800 : 775;
        if (bytes.Length < 2 + count || bytes[0] != 0x31 || bytes[1] != 0x0A)
            throw new InvalidDataException("Invalid classic v1 object table.");
        var states = new Dictionary<int, byte>();
        var owners = new Dictionary<int, byte>();
        for (var id = 0; id < count; id++)
        {
            states[id] = (byte)(bytes[2 + id] >> 4);
            owners[id] = (byte)(bytes[2 + id] & 15);
        }
        return new(states, owners);
    }
    protected override (int Width, int Height) ReadDimensions(byte[] bytes) => (bytes[4] * 8, bytes[5] * 8);
    public override IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => ScummOldRoomObjects.ReadV1Images(RoomBytes(room), _classicGame);
}

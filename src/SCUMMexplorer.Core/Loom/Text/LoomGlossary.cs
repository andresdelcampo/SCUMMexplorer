using System.Globalization;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>How well a glossary name is supported. A name never exists without one of these.</summary>
public enum GlossaryEvidence
{
    /// <summary>Read straight from instructions or from the game's own data and text.</summary>
    Decoded,
    /// <summary>ScummVM <c>engines/scumm</c> behaviour (the engine's own variable meanings).</summary>
    Engine,
    /// <summary>An interpretation from the game's text or the way scripts use the thing; not verified in a run.</summary>
    Reading,
    /// <summary>Seen in play.</summary>
    Play
}

public sealed record GlossaryEntry(string Name, GlossaryEvidence Evidence, string Source)
{
    public string EvidenceLabel => Evidence switch
    {
        GlossaryEvidence.Decoded => "decoded",
        GlossaryEvidence.Engine => "engine",
        GlossaryEvidence.Reading => "reading",
        _ => "play"
    };
}

/// <summary>
/// Evidence-carrying names for LOOM EGA's variables, bits, scripts, actors, rooms, sounds and objects. The table is the data
/// file <c>loom-glossary.json</c> (seeded from docs/loom/LOOM_SCRIPT_MAP.md); object names come from the game's own data. Anything
/// not listed stays unnamed: callers show the raw form (<c>var[0x0D1]</c>) rather than a guess.
/// </summary>
public sealed partial class LoomGlossary
{
    private sealed record Row(string Kind, string Key, string? To, string Name, string Evidence, string? Source, string? Scope = null);
    private sealed record File(List<Row> Entries);

    private readonly Dictionary<string, List<(int From, int To, GlossaryEntry Entry, string? Scope)>> _byKind = new();
    private readonly Dictionary<int, (int Room, string Name)> _objects = new();

    /// <summary>Optional names for story bits (for example the catalog scene that sets one); used only where the table has none.</summary>
    public Func<int, string?>? StoryBitLabel { get; set; }

    public static LoomGlossary Default { get; } = Create(null);

    /// <summary>A fresh glossary of the data file (a caller that sets <see cref="StoryBitLabel"/> must not change the shared <see cref="Default"/>).</summary>
    public static LoomGlossary CreateNew() => Create(null);

    /// <summary>The glossary with the object names of every room of the game.</summary>
    public static LoomGlossary Load(GameLibrary game) => Create(game);

    private static LoomGlossary Create(GameLibrary? game)
    {
        var glossary = new LoomGlossary();
        using var stream = typeof(LoomGlossary).Assembly.GetManifestResourceStream("SCUMMexplorer.Core.loom-glossary.json")
            ?? throw new InvalidOperationException("The glossary data is missing from the assembly.");
        var file = JsonSerializer.Deserialize<File>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The glossary data could not be read.");
        // The table's script, bit, actor and sound names were checked on the full game's data. A demo is another build
        // (its script 045 directs the preview), so those numbers are not named there; variables and rooms are kept.
        var demo = game is { IsDemo: true };
        var cd = game?.Id == GameId.LoomVga;
        foreach (var row in file.Entries.Where(row => cd ? row.Kind == "var" && row.Evidence == "engine"
            : !demo || row.Kind is not ("script" or "bit" or "actor" or "sound")))
        {
            var from = ParseNumber(row.Key);
            var to = row.To is null ? from : ParseNumber(row.To);
            var evidence = Enum.Parse<GlossaryEvidence>(row.Evidence, ignoreCase: true);
            if (!glossary._byKind.TryGetValue(row.Kind, out var list)) glossary._byKind[row.Kind] = list = [];
            list.Add((from, to, new GlossaryEntry(row.Name, evidence, row.Source ?? ""), row.Scope));
        }
        if (game is not null && game.Id == GameId.Loom)
        {
            glossary.ReadObjects(game);
            if (!demo)
            {
                glossary._bitShift = LoomStoryEvents.FirstBitFor(game) - LoomStoryEvents.FirstBit;
                glossary.ReadEgaReviewedNames(game);
            }
        }
        if (cd) glossary.ReadCdNames(game!);
        return glossary;
    }

    private void ReadObjects(GameLibrary game)
    {
        var directory = new ScummRoomObjectDirectory();
        foreach (var room in game.Rooms)
        {
            if (room.ResourceId is not int roomId) continue;
            try
            {
                var encoded = System.IO.File.ReadAllBytes(room.FilePath);
                var records = directory.Decode(encoded);
                foreach (var (id, name) in LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(encoded), records))
                    _objects.TryAdd(id, (roomId, name));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { }
        }
    }

    public GlossaryEntry? Variable(int index) => Find("var", index);

    /// <summary>An established array base, rather than an arbitrary address somewhere within a named range.</summary>
    internal GlossaryEntry? IndexedVariable(int index) => _byKind.TryGetValue("var", out var rows)
        ? rows.Where(row => row.From == index && row.To > index && row.Scope is null).Select(row => row.Entry).FirstOrDefault()
        : null;

    /// <summary>Bits from 0xD63 on are numbered <see cref="_bitShift"/> higher in this edition than in the Spanish release the table was written for (0 for it); the Spanish 0xD62 ("full animation", set by its boot) has no counterpart in the English scripts, where 0xD62 is the Spanish 0xD63.</summary>
    private int _bitShift;
    private const int SpanishShiftedFrom = 0xD63;

    public GlossaryEntry? Bit(int index)
    {
        if (_reviewedBits.TryGetValue(index, out var reviewed)) return reviewed;
        if (_bitShift != 0 && index >= SpanishShiftedFrom + _bitShift)
        {
            // The table names the Spanish numbering; the same role is read here one bit lower, and the entry says so.
            return Find("bit", index - _bitShift) is { } named
                ? new GlossaryEntry(named.Name, GlossaryEvidence.Reading, $"{named.Source}; Spanish release's bit 0x{index - _bitShift:X3}, this edition numbers it 0x{index:X3} (same position in the shifted layout, not checked entry by entry)")
                : null;
        }
        return Find("bit", index) ??
            (StoryBitLabel?.Invoke(index) is { } label
                ? new GlossaryEntry(label, GlossaryEvidence.Reading, "the catalog scene whose code sets this story bit")
                : null);
    }

    public GlossaryEntry? Script(int id) => Find("script", id);
    /// <summary>Actor numbers are reused from scene to scene; a name with a scope (for example <c>script:42</c>) applies only inside that script and wins over the general one.</summary>
    public GlossaryEntry? Actor(int id, params string?[] scopes) =>
        scopes.Where(scope => scope is not null).Select(scope => Find("actor", id, scope)).FirstOrDefault(entry => entry is not null) ?? Find("actor", id);
    public GlossaryEntry? Room(int id) => Find("room", id);
    public GlossaryEntry? Sound(int id) => Find("sound", id);

    /// <summary>The name stored in the game for a room object (object numbers are unique across rooms).</summary>
    public GlossaryEntry? Object(int room, int id) =>
        _objects.TryGetValue(id, out var found) && (room <= 0 || found.Room == room)
            ? new GlossaryEntry(found.Name, GlossaryEvidence.Decoded, $"the name stored in Room {found.Room:00}'s data") : null;

    /// <summary>The room that holds an object, when the game's data names it.</summary>
    public int? ObjectRoom(int id) => _objects.TryGetValue(id, out var found) ? found.Room : null;

    private GlossaryEntry? Find(string kind, int index, string? scope = null)
    {
        if (!_byKind.TryGetValue(kind, out var list)) return null;
        foreach (var (from, to, entry, entryScope) in list)
            if (index >= from && index <= to && entryScope == scope) return entry;
        return null;
    }

    private static int ParseNumber(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(text, CultureInfo.InvariantCulture);
}

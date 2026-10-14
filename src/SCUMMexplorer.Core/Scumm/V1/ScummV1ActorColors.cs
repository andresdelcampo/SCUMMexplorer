using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One <c>actorOps</c> instruction of a classic v1 script that sets an actor's costume or palette colour, with where it is.</summary>
/// <param name="Source">The script's label (global script, room entry/exit script, object verb script).</param>
/// <param name="Offset">The instruction's offset in its script.</param>
/// <param name="Actor">The actor number, or null when the script takes it from a variable.</param>
/// <param name="Value">The costume number or C64 colour number, or null when the script takes it from a variable.</param>
public sealed record ScummV1ActorSetting(string Source, int Offset, int? Actor, int? Value, bool IsCostume, string ActorOperand = "", bool InBootScript = false);

/// <summary>Where the colours of a classic v1 costume came from.</summary>
public enum ScummV1ColorBasis
{
    /// <summary>A script instruction gives the costume to an actor (and, for Zak, sets that actor's palette).</summary>
    Script,
    /// <summary>No script gives the costume away: the engine's start state (actor n wears costume n).</summary>
    InitialState,
    /// <summary>Nothing decides; a fixed colour, which is an estimate and is labelled so.</summary>
    Estimate
}

/// <summary>The two actor colours of a classic v1 costume (C64 colour numbers 0 to 15), how they were found and how many other colour pairs the game also uses for it.</summary>
public sealed record ScummV1CostumeColor(int Costume, int Skin, int Suit, ScummV1ColorBasis Basis, string Evidence, int Alternatives);

/// <summary>
/// Where the colours of a classic v1 costume come from. A v1 costume has two actor colours (pixel codes 1 and 2); ScummVM's <c>byleRLEDecode_C64</c> picks them
/// by the actor's number: for Maniac Mansion from two fixed 25-entry tables (<c>v1MMActorPalatte1</c> skin, <c>v1MMActorPalatte2</c> suit), for Zak McKracken
/// skin 8 (10 on the C64 only) and the actor's own palette slot <c>palette[actor]</c> as suit, which the scripts set with <c>actorOps</c> sub-opcode 2
/// (<c>o2_actorOps</c>: in version 1 the palette index is the actor number). The pairing of a costume with an actor is a script instruction too: <c>actorOps</c>
/// sub-opcode 4 (<c>setActorCostume</c>); before any script runs actor n wears costume n with palette 0 (<c>scumm.cpp</c> and <c>Actor::initActor</c>).
/// This class reads those instructions from every script of the game (ScummVM <c>script_v2.cpp</c>, <c>actor.cpp</c>, <c>costume.cpp</c>, <c>scumm.cpp</c>,
/// fetched 14 October 2026). An instruction whose actor or value is a variable cannot be resolved without running the script and is counted, not guessed.
/// </summary>
public sealed class ScummV1ActorColors
{
    // ScummVM costume.cpp: v1MMActorPalatte1 (skin) and v1MMActorPalatte2 (suit), indexed by actor number 0 to 24.
    private static readonly byte[] ManiacSkin = [8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8];
    private static readonly byte[] ManiacSuit = [0, 7, 2, 6, 9, 1, 3, 7, 7, 1, 1, 9, 1, 4, 5, 5, 4, 1, 0, 5, 4, 2, 2, 7, 7];

    // ScummVM scumm.cpp runBootscript: the boot script is script 1; the palettes it leaves are the actors' palettes when the game starts.
    private const int BootScript = 1;

    // The fixed colours the explorer used before it read the scripts; only a costume nothing decides still gets them.
    private const int EstimateSkin = 8;
    private const int EstimateSuit = 14;

    private static readonly Regex ActorOps = new(@"^(0x[0-9A-F]{2}|var\[0x[0-9A-F]{3}\]), (0x[0-9A-F]{2}|var\[0x[0-9A-F]{3}\]), sub=0x(0[24])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ScummV1Game _game;
    private readonly List<ScummV1ActorSetting> _settings;

    private ScummV1ActorColors(ScummV1Game game, List<ScummV1ActorSetting> settings, int unresolved)
    {
        _game = game;
        _settings = settings;
        UnresolvedInstructions = unresolved;
    }

    /// <summary>Every costume and palette setting found in the game's scripts (constant or not), in script order.</summary>
    public IReadOnlyList<ScummV1ActorSetting> Settings => _settings;

    /// <summary>The number of costume or palette instructions whose actor or value comes from a variable.</summary>
    public int UnresolvedInstructions { get; }

    /// <summary>The number of actors the engine creates (ScummVM <c>_numActors</c>: 25 for Maniac Mansion, 13 for Zak McKracken).</summary>
    public int ActorCount => _game == ScummV1Game.ManiacMansion ? 25 : 13;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ScummV1ActorColors> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The colours of a classic v1 game, read once per folder; null for any other game.</summary>
    public static ScummV1ActorColors? For(GameLibrary game) =>
        game.V1Game is null ? null : Cache.GetOrAdd(game.FolderPath, _ => Read(game));

    /// <summary>Reads the scripts of a classic v1 game folder.</summary>
    public static ScummV1ActorColors Read(GameLibrary game)
    {
        var gameKind = game.V1Game ?? throw new ArgumentException("Not a classic v1 game.", nameof(game));
        var reader = new ScummScriptReader(ScummScriptDialect.V2);
        var settings = new List<ScummV1ActorSetting>();
        var unresolved = 0;

        void Scan(ScummScriptDisassembly disassembly, string source, bool boot = false)
        {
            foreach (var instruction in disassembly.Instructions)
            {
                if (instruction.Operation != "actorOps") continue;
                var match = ActorOps.Match(instruction.Operands);
                if (!match.Success) continue; // sub 1 (sound), 3 (name), 5 (talk colour)
                var actor = TryConstant(match.Groups[1].Value);
                var value = TryConstant(match.Groups[2].Value);
                if (actor is null || value is null) unresolved++;
                settings.Add(new ScummV1ActorSetting(source, instruction.Offset, actor, value, match.Groups[3].Value == "04", match.Groups[1].Value, boot));
            }
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue && item.Offset.HasValue))
        {
            try
            {
                var code = ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath)), resource.Offset!.Value);
                if (code is not null) Scan(reader.DecodeCode(code, resource.ResourceId!.Value, resource.ContainerFileNumber ?? 0, resource.Offset.Value), resource.Name, resource.ResourceId == BootScript);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
        }
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            byte[] plain;
            try { plain = LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)); }
            catch (IOException) { continue; }
            foreach (var source in ScummOldScripts.ReadRoomScripts(plain, room.ResourceId!.Value))
            {
                try { Scan(reader.DecodeCode(source.Code, source.ObjectId ?? room.ResourceId.Value, source.Container, source.Offset, source.StartOffset), source.Label); }
                catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException) { }
            }
        }
        return new ScummV1ActorColors(gameKind, settings, unresolved);
    }

    private static int? TryConstant(string operand) =>
        operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.Parse(operand[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : null;

    /// <summary>The actors a script gives this costume (constant <c>actorOps</c> sub 4), in ascending order.</summary>
    public IReadOnlyList<int> ActorsOf(int costume) =>
        _settings.Where(s => s.IsCostume && s.Value == costume && s.Actor is not null).Select(s => s.Actor!.Value).Distinct().Order().ToArray();

    /// <summary>The colour numbers the scripts give an actor's palette slot (Zak McKracken; constant <c>actorOps</c> sub 2), in script order.</summary>
    public IReadOnlyList<int> PaletteValuesOf(int actor) =>
        _settings.Where(s => !s.IsCostume && s.Actor == actor && s.Value is not null).Select(s => s.Value!.Value).Distinct().ToArray();

    /// <summary>
    /// The colours of one costume. Maniac Mansion: the actor that wears it, then the engine's fixed tables by actor number. Zak McKracken: the palette the scripts
    /// set for the actor next to the instruction that gives it the costume. A costume no script gives to an actor is worn by the actor of its own number from the
    /// start (ScummVM <c>scumm.cpp</c>: <c>setActorCostume(i)</c> for every actor of a v1 game). Only when none of this decides is the result an estimate.
    /// </summary>
    public ScummV1CostumeColor ColorOf(int costume)
    {
        var assigned = new List<(int Skin, int Suit, string Evidence)>();
        var undecided = 0;
        foreach (var set in _settings.Where(s => s.IsCostume && s.Value == costume))
        {
            var where = $"{set.Source} @{set.Offset:X4} sets costume {costume} on actor {(set.Actor?.ToString() ?? set.ActorOperand)}";
            if (_game == ScummV1Game.ManiacMansion)
            {
                if (set.Actor is >= 0 and <= 24 and var actor)
                    assigned.Add((ManiacSkin[actor], ManiacSuit[actor], $"{where}; v1MMActorPalatte1/2[{actor}]"));
                else undecided++;
            }
            else if (NearestPalette(set) is { Value: { } suit } palette)
                assigned.Add((8, suit, $"{where}; palette {suit} set @{palette.Offset:X4}"));
            else if (set.Actor is { } constantActor && !HasPaletteSetting(constantActor))
                assigned.Add((8, BootPalette(constantActor) ?? 0, $"{where}; only the boot script (or no script) sets the palette of actor {constantActor}: {BootPalette(constantActor) ?? 0}"));
            else undecided++;
        }
        if (assigned.Count > 0)
        {
            var first = assigned[0];
            return new ScummV1CostumeColor(costume, first.Skin, first.Suit, ScummV1ColorBasis.Script, first.Evidence,
                assigned.Select(a => (a.Skin, a.Suit)).Distinct().Count() - 1 + undecided);
        }

        // No script gives the costume to an actor: the start state, actor n wears costume n.
        if (costume >= 0 && costume < ActorCount && undecided == 0)
        {
            if (_game == ScummV1Game.ManiacMansion)
                return new ScummV1CostumeColor(costume, ManiacSkin[costume], ManiacSuit[costume], ScummV1ColorBasis.InitialState,
                    $"actor {costume} wears costume {costume} from the start; v1MMActorPalatte1/2[{costume}]", 0);
            if (!HasPaletteSetting(costume) && !_settings.Any(s => s.InBootScript && s.IsCostume && s.Actor == costume))
                return new ScummV1CostumeColor(costume, 8, BootPalette(costume) ?? 0, ScummV1ColorBasis.InitialState,
                    $"actor {costume} wears costume {costume} from the start; only the boot script (or no script) sets its palette: {BootPalette(costume) ?? 0}", 0);
        }

        // Undecided: the palette in effect depends on what ran before. Use a palette value the scripts give one of the actors that wear it, if any (still an estimate).
        var actors = _settings.Where(s => s.IsCostume && s.Value == costume && s.Actor is not null).Select(s => s.Actor!.Value).Append(costume).Distinct();
        foreach (var actor in actors)
            if (PaletteValuesOf(actor) is { Count: > 0 } values)
            {
                var value = BootPalette(actor) ?? values[0];
                return new ScummV1CostumeColor(costume, EstimateSkin, value, ScummV1ColorBasis.Estimate,
                    $"the palette in effect for actor {actor} when it wears this costume depends on the run; {value} is {(BootPalette(actor) is null ? "the first value the scripts set for" : "what the boot script leaves in")} that actor's palette", 0);
            }
        return new ScummV1CostumeColor(costume, EstimateSkin, EstimateSuit, ScummV1ColorBasis.Estimate, "no script decides the colours of this costume", 0);
    }

    // Whether a script may set this actor's palette. A palette instruction on a variable actor counts as the palette of the costume it sits next to (the same
    // variable in the same script gives that costume), not of every actor; the one such case is Zak's Room 38 entry script.
    private bool HasPaletteSetting(int actor) =>
        _settings.Any(s => !s.IsCostume && !s.InBootScript && (s.Actor == actor ||
            s.Actor is null && !_settings.Any(c => c.IsCostume && c.Source == s.Source && c.ActorOperand == s.ActorOperand)));

    // What the boot script (script 1) leaves in an actor's palette (the last constant palette instruction for that actor, in script order), or null.
    private int? BootPalette(int actor) =>
        _settings.LastOrDefault(s => s.InBootScript && !s.IsCostume && s.Actor == actor && s.Value is not null)?.Value;

    // The palette the costume instruction leaves the actor with: the last palette op of the same actor operand in the same script after it and before the actor's next
    // costume op (the palette persists, so a later op in the span wins); if there is none, the closest palette op before it with no costume op in between.
    private ScummV1ActorSetting? NearestPalette(ScummV1ActorSetting costumeSet)
    {
        var script = _settings.Where(s => s.Source == costumeSet.Source && s.ActorOperand == costumeSet.ActorOperand).OrderBy(s => s.Offset).ToList();
        var index = script.IndexOf(costumeSet);
        ScummV1ActorSetting? last = null;
        for (var i = index + 1; i < script.Count && !script[i].IsCostume; i++) last = script[i];
        if (last is not null) return last;
        return index > 0 && !script[index - 1].IsCostume ? script[index - 1] : null;
    }
}

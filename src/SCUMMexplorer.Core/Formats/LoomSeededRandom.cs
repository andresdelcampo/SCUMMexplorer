using System.Text;

namespace SCUMMexplorer.Core.Formats;

/// <summary>One labelled stand-in value for <c>o5_getRandomNr</c>.</summary>
public sealed record LoomRandomPick(string Source, int Offset, int Max, int Value, int Seed, int Draw)
{
    public string Describe() =>
        $"getRandomNr at +0x{Offset:X4} picked {Value} of 0..{Max} (seeded pick, seed {Seed}, draw {Draw}); " +
        "what follows from it is an estimate, not a reached game state";
}

/// <summary>
/// Seeded replacement for the engine's random source. ScummVM's
/// <c>o5_getRandomNr</c> stores <c>getRandomNumber(max)</c>, a value from 0 to
/// max inclusive. The game's actual draw is unknowable, so each value is a
/// labelled pick. It depends only on the seed, the instruction and how often
/// that instruction has drawn, not on unrelated scripts running in between.
/// </summary>
public sealed class LoomSeededRandom(int seed)
{
    private readonly Dictionary<(string Source, int Offset), int> _draws = [];

    public int Seed => seed;

    public LoomRandomPick Pick(string source, int offset, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        var draw = _draws.GetValueOrDefault((source, offset));
        _draws[(source, offset)] = draw + 1;
        // FNV-1a over the site, then a 64-bit finalizer so the low bits mix.
        var hash = 14695981039346656037UL;
        foreach (var value in Encoding.UTF8.GetBytes($"{seed}|{source}|{offset}|{draw}"))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        hash ^= hash >> 33;
        hash *= 0xFF51AFD7ED558CCDUL;
        hash ^= hash >> 33;
        return new LoomRandomPick(source, offset, max, (int)(hash % (ulong)(max + 1)), seed, draw);
    }
}

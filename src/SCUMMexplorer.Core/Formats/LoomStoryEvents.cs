using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// A story bit (D72 and up) that some code of the game sets to 1: "the thing that sets it happened". The game keeps the
/// memory of conversations, cutscenes and handled objects in these bits (a scene sets one when it ran; rooms and later
/// scenes read it). <see cref="Setters"/> are the places that assign 1, scenes (global scripts) and object verbs first.
/// </summary>
public sealed record LoomStoryEvent(string Key, int Bit, IReadOnlyList<LoomStateSource> Setters)
{
    /// <summary>The scenes among the setters: global scripts from 044 on and object verbs, by their place names.</summary>
    public IEnumerable<LoomStateSource> Scenes => Setters.Where(s => s.ObjectId is not null || s.Where.StartsWith("Script ", StringComparison.Ordinal) && !LoomSceneStateScanner.IsSystemCode(s.Where));
}

public static class LoomStoryEvents
{
    /// <summary>First bit of the story range (bits below it belong to the engine, the staff, drafts and input).</summary>
    public const int FirstBit = 0xD72;

    /// <summary>The first story bit of this edition: the Spanish release's 0xD72 moved by the shift the status schema measures (the English release numbers bits from 0xD65 one lower).</summary>
    public static int FirstBitFor(GameLibrary game)
    {
        try { return FirstBit + LoomStatusSchema.Read(game).BitShift; }
        catch (InvalidDataException) { return FirstBit; }
    }

    /// <summary>The story bits the game's code can set to 1, in bit order. Scans every room, script and object verb once (about a second).</summary>
    public static IReadOnlyList<LoomStoryEvent> Read(GameLibrary game)
    {
        var moves = new LoomSceneStateScanner(game).FindStoryBitMoves(1);
        return moves.GroupBy(m => Bit(m.Instruction))
            .OrderBy(g => g.Key)
            .Select(g => new LoomStoryEvent($"bit[0x{g.Key:X3}]", g.Key,
                g.OrderBy(m => m.ObjectId is null).ThenBy(m => m.Where.StartsWith("Script ", StringComparison.Ordinal) ? 0 : 1).ThenBy(m => m.Room).ToArray()))
            .ToArray();
    }

    private static int Bit(string instruction)
    {
        var start = instruction.IndexOf("bit[0x", StringComparison.Ordinal) + 6;
        return int.Parse(instruction.AsSpan(start, instruction.IndexOf(']', start) - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}

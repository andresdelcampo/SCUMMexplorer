using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The costumes the game's own code dresses each actor in (<c>actorOps N, [costume, C]</c> with literals, in every script and verb). A test that a scene
/// needs an actor out of one costume (Indy 3 Room 13's entry, +0x0028: isNotEqual on the hero's costume and 2, the action clothes; the scene is Indy
/// changing into them) is offered the costume's siblings: the others the scripts that dress the actor in it dress it in (Script 52's outfits, 2 to 4).
/// </summary>
public sealed partial class ScummSceneSetupSearch
{
    private Lazy<IReadOnlyDictionary<string, List<(string Unit, int Costume)>>>? _dressed;

    private IReadOnlyDictionary<string, List<(string Unit, int Costume)>> Dressed => (_dressed ??= new(() =>
    {
        var found = new Dictionary<string, List<(string, int)>>(StringComparer.Ordinal);
        foreach (var unit in CodeUnits())
        {
            if (Read(unit.Label, unit.AsTrigger) is not { } script) continue;
            foreach (var ins in script.Instructions.Where(i => i.Operation == "actorOps"))
            {
                var actor = Literal(ins.Operands.Split(',', 2)[0].Trim());
                if (actor is null || CostumeOperand().Match(ins.Operands) is not { Success: true } m) continue;
                var costume = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var key = actor.Value.ToString(CultureInfo.InvariantCulture);
                var list = found.TryGetValue(key, out var known) ? known : found[key] = [];
                if (!list.Contains((unit.Label, costume))) list.Add((unit.Label, costume));
            }
        }
        return found;
    })).Value;

    /// <summary>The other costumes the scripts that dress the actor in <paramref name="costume"/> dress it in, in code order; empty when none does.</summary>
    private IReadOnlyList<int> SiblingCostumes(string actor, int costume)
    {
        if (!Dressed.TryGetValue(actor, out var dressed)) return [];
        var units = dressed.Where(d => d.Costume == costume).Select(d => d.Unit).ToHashSet(StringComparer.Ordinal);
        return dressed.Where(d => units.Contains(d.Unit) && d.Costume != costume).Select(d => d.Costume).Distinct().ToList();
    }

    private Lazy<IReadOnlyDictionary<int, IReadOnlyList<int>>>? _exits;

    /// <summary>
    /// The objects of each room whose verb code takes the hero to another room (<c>loadRoomWithEgo</c>): its doors. The way back through a door brings the
    /// hero in by it, which the arrival index cannot tell where the doors load rooms through variables (Indy 3's castle, Room 13).
    /// </summary>
    private IReadOnlyList<int> ExitObjects(int room) => (_exits ??= new(() =>
    {
        var found = new Dictionary<int, List<int>>();
        foreach (var unit in CodeUnits())
        {
            if (ObjectUnit().Match(unit.Label) is not { Success: true } m || Read(unit.Label, unit.AsTrigger) is not { } script ||
                !script.Instructions.Any(i => i.Operation == "loadRoomWithEgo")) continue;
            var (inRoom, id) = (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            var list = found.TryGetValue(inRoom, out var known) ? known : found[inRoom] = [];
            if (!list.Contains(id)) list.Add(id);
        }
        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value);
    })).Value.TryGetValue(room, out var exits) ? exits : [];

    [GeneratedRegex(@"^Room (\d+) object (\d+) verb ")]
    private static partial Regex ObjectUnit();

    [GeneratedRegex(@"\bcostume, 0x([0-9A-F]+)")]
    private static partial Regex CostumeOperand();
}

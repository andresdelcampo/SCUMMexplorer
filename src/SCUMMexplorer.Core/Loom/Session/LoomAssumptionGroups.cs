namespace SCUMMexplorer.Core.Loom;

public enum LoomAssumptionKind { Shortcut, Estimate, NotDrawn, Other }

public sealed record LoomAssumptionGroup(LoomAssumptionKind Kind, string Title, string Meaning, IReadOnlyList<string> Items);

/// <summary>
/// Sorts the session's assumption lines into the three kinds a reader cares about, each with one sentence on whether it changes
/// what is on screen. The lines themselves are unchanged (they stay the evidence); only their grouping is new.
/// </summary>
public static class LoomAssumptionGroups
{
    public static IReadOnlyList<LoomAssumptionGroup> Group(IEnumerable<string> lines)
    {
        var buckets = new Dictionary<LoomAssumptionKind, List<string>>();
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct())
        {
            var kind = Classify(line);
            if (!buckets.TryGetValue(kind, out var list)) buckets[kind] = list = [];
            list.Add(line);
        }
        return Enum.GetValues<LoomAssumptionKind>().Where(buckets.ContainsKey)
            .Select(kind => new LoomAssumptionGroup(kind, Title(kind), Meaning(kind), buckets[kind])).ToArray();
    }

    public static LoomAssumptionKind Classify(string line)
    {
        var text = line.ToLowerInvariant();
        if (text.Contains("not applied") || text.Contains("not drawn") || text.Contains("not simulated and") || text.Contains("presentation operation"))
            return LoomAssumptionKind.NotDrawn;
        if (text.Contains("selected from the catalog") || text.Contains("route to it was not executed") || text.Contains("activated directly") ||
            text.Contains("exploration action") || text.Contains("catalog visit") && !text.Contains("estimate"))
            return LoomAssumptionKind.Shortcut;
        if (text.Contains("estimate") || text.Contains("assumed") || text.Contains("seeded") || text.Contains("artwork of"))
            return LoomAssumptionKind.Estimate;
        return LoomAssumptionKind.Other;
    }

    private static string Title(LoomAssumptionKind kind) => kind switch
    {
        LoomAssumptionKind.Shortcut => "Arrived by a shortcut",
        LoomAssumptionKind.Estimate => "Estimates",
        LoomAssumptionKind.NotDrawn => "Effects not drawn",
        _ => "Other"
    };

    private static string Meaning(LoomAssumptionKind kind) => kind switch
    {
        LoomAssumptionKind.Shortcut => "You started here from a menu or a direct action instead of playing the route, so the state is that place's start state: earlier story events did not run unless you set them in Game Status.",
        LoomAssumptionKind.Estimate => "A position, camera, size, input or random pick had to be estimated. What is on screen may differ slightly from the original there.",
        LoomAssumptionKind.NotDrawn => "A script asks for an effect (such as lighting) or a resource load that this explorer does not draw. The picture may differ slightly from the original there.",
        _ => "Something the explorer could not settle; the line says what."
    };
}

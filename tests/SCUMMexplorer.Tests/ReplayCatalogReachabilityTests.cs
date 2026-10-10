using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The room popup lists a catalog entry in every room of its <c>membership</c> (start room plus rooms seen while it played).
/// Membership is generated data, so a regeneration could leave an entry that no room shows (REQUIREMENTS_FINDINGS, E-5).
/// </summary>
public class ReplayCatalogReachabilityTests
{
    private static JsonElement Catalog()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/SCUMMexplorer.App/loom-replays.json"));
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    [Fact]
    public void EveryCatalogEntryHasAMembershipAndIsListedInAnExistingRoom()
    {
        var root = Catalog();
        var members = root.GetProperty("membership").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetString()!);
        var folder = Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var rooms = new GameFolderScanner().Scan(folder).Rooms.Select(r => r.ResourceId).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

        var problems = new List<string>();
        foreach (var replay in root.GetProperty("replays").EnumerateArray())
        {
            var id = replay.GetProperty("id").GetString()!;
            if (!members.TryGetValue(id, out var member)) { problems.Add($"{id}: no membership"); continue; }
            var listedIn = member.GetProperty("rooms").EnumerateArray().Select(r => r.GetInt32())
                .Append(member.GetProperty("startRoom").GetInt32()).Where(rooms.Contains).ToList();
            if (listedIn.Count == 0) problems.Add($"{id} ({replay.GetProperty("category").GetString()}): no existing room lists it");
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(20)));
    }
}

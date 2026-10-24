using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// R-6.25 item 7 (24 Oct 2026): how many interactions an Interact overview of one room has, per game, counted from the same source the
/// Interact control reads (every object verb that decodes and is not plumbing, distinct per object). The numbers justify the grouped, scrollable overview.
/// </summary>
public sealed class RoomInteractionCountTests(ITestOutputHelper output)
{
    private static (int Objects, int Actions) Count(GameLibrary game, int room, bool loomRules)
    {
        var data = ScummDataSources.For(game);
        var stored = data.ReadRoom(room);
        var verbs = data.ReadObjectVerbs(room, stored.Objects);
        int objects = 0, actions = 0;
        foreach (var record in stored.Objects.DistinctBy(o => o.Id))
        {
            var count = 0;
            foreach (var verb in (verbs.GetValueOrDefault(record.Id) ?? []).DistinctBy(v => v.VerbId))
            {
                if (loomRules ? !(verb.VerbId is 0x36 or 0x38 || LoomDraftNames.ForVerb(verb.VerbId) is not null) : verb.VerbId >= 250) continue;
                try
                {
                    var code = data.ReadObjectVerb(room, record, verb.VerbId, out _);
                    if (code is { UndecodedOffset: null } && code.Instructions.Any(i => i.Offset == code.EntryOffset)) count++;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
            }
            if (count > 0) { objects++; actions += count; }
        }
        return (objects, actions);
    }

    [Theory]
    [InlineData("mmansion ega en", false)]
    [InlineData("mmansionE ega es", false)]
    [InlineData("zak ega en", false)]
    [InlineData("zakE ega en", false)]
    [InlineData("indy3 ega en", false)]
    [InlineData("loom ega mt32 en", true)]
    public void InteractionsPerRoom(string folder, bool loom)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var rows = new List<(int Room, int Objects, int Actions)>();
        foreach (var room in game.Rooms.Select(r => r.ResourceId).Where(id => id is not null).Select(id => id!.Value).Distinct())
        {
            try { var (o, a) = Count(game, room, loom); if (a > 0) rows.Add((room, o, a)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { }
        }
        Assert.NotEmpty(rows);
        var sorted = rows.Select(r => r.Actions).Order().ToArray();
        output.WriteLine($"{folder}: rooms with interactions {rows.Count}, objects/room max {rows.Max(r => r.Objects)}, actions/room min {sorted[0]} median {sorted[sorted.Length / 2]} " +
            $"p90 {sorted[(int)(sorted.Length * 0.9)]} max {sorted[^1]} (room {rows.MaxBy(r => r.Actions).Room}); room 1: " +
            (rows.FirstOrDefault(r => r.Room == 1) is { Actions: > 0 } r1 ? $"{r1.Objects} objects / {r1.Actions} actions" : "none"));
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

// Generated from decoded triggers and bounded fresh-session validation, never from
// a cutscene opcode alone. The fingerprint prevents applying a recipe to other data.
internal sealed record ReplaySourceFile(string Name, string Sha256);
internal sealed record ReplayRecipe(string Id, string Title, string Category, int Room, int ObjectId,
    int Verb, int[] ScriptIds, int[] Rooms, int Frames, string Source, LoomReplayStep[]? Steps = null, int SilentSteps = 0);
internal sealed record ReplayCoverage(string Title, string Status, string Source);
internal sealed record ReplayMembership(string Id, int StartRoom, int[] Rooms, int Frames, string Source);
internal sealed record LoomReplayCatalog(ReplaySourceFile[] SourceFiles, ReplayRecipe[] Replays,
    ReplayCoverage[] Coverage, string Summary, ReplayMembership[] Membership)
{
    public static LoomReplayCatalog Load(GameLibrary game)
    {
        if (game.Id != GameId.Loom) return Empty("Cutscene replay is available for LOOM EGA.");
        using var stream = typeof(LoomReplayCatalog).Assembly.GetManifestResourceStream("SCUMMexplorer.App.loom-replays.json");
        if (stream is null) return Empty("Replay discovery data is unavailable.");
        var catalog = JsonSerializer.Deserialize<LoomReplayCatalog>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (catalog is null) return Empty("Replay discovery data could not be read.");
        foreach (var file in catalog.SourceFiles)
        {
            var path = Path.Combine(game.FolderPath, file.Name);
            if (!File.Exists(path) || !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                return Empty("This edition has not been validated against the expanded replay catalog. The four established starts remain available; discovery and validation must be rerun for these files.");
        }
        return catalog with
        {
            Replays = catalog.Replays.Select(recipe => recipe with
                { Title = LoomDraftNames.ReplayTitle(recipe.Title, recipe.Verb) }).ToArray()
        };
    }
    private static LoomReplayCatalog Empty(string note) => new([], [], [], note, []);
}

internal sealed record CutscenePickerItem(string Title, string Category, string Note, Action? Play, string Status = "");

internal static class LoomReplayTitle
{
    public static string Format(int room, string description)
    {
        var prefix = $"Room {room:00} · ";
        return description.StartsWith(prefix, StringComparison.Ordinal) ? description : prefix + description;
    }
}

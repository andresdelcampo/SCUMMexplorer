using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// The rooms each CD scene plays out in, made once offline for the known disc (<c>.verification/cd-seed</c>: the scan, the derived start setups and a headless run of
/// each scene) and shipped with the app, so a first launch has the room lists at once instead of working them out in the background. It is used only when the opened game's
/// data fingerprint (<see cref="SCUMMexplorer.Core.Scumm.ScummV4Archive.Fingerprint"/>) matches the one it was made from; any other disc is scanned live as before.
/// </summary>
internal sealed record LoomCdSeed(string Fingerprint, Dictionary<string, int[]> Rooms)
{
    public static LoomCdSeed? Load(GameLibrary game)
    {
        if (game.V4Archive is not { } archive) return null;
        try
        {
            using var stream = typeof(LoomCdSeed).Assembly.GetManifestResourceStream("SCUMMexplorer.App.loom-cd-scenes.json");
            if (stream is null) return null;
            var seed = JsonSerializer.Deserialize<LoomCdSeed>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return seed is not null && string.Equals(seed.Fingerprint, archive.Fingerprint, StringComparison.OrdinalIgnoreCase) ? seed : null;
        }
        catch (JsonException) { return null; }
    }
}

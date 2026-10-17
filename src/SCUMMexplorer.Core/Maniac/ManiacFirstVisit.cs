using System.Collections.Concurrent;
using System.Security.Cryptography;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Core.Maniac;

/// <summary>
/// Reviewed, edition-fingerprinted player choices for the first-visit preview, not engine rules.
/// The same native room clicks used in the S4b records select Syd, Bernard and Start; no variables are assigned.
/// A changed menu fails closed until its input recipe is reviewed (R-14.2).
/// </summary>
public static class ManiacFirstVisit
{
    private static readonly HashSet<string> MenuFingerprints =
    [
        "AF22D5CC9774D55BA73F8B092372BB5CE23624F00A93860C55298559D5606E23", // enhanced Spanish
        "3F57F0FC889459D35080DF1D9CF339363C3EF80C00F30F36AF38F41D5D5061D9"  // classic English
    ];
    private const string Choice = "Player input assumed: the reviewed character-selection menu selects Syd, Bernard and Start through three room clicks; other parties and the route to a catalog room were not played.";
    private static readonly ConcurrentDictionary<string, Lazy<ScummRoomScene?>> Scenes = new(StringComparer.OrdinalIgnoreCase);

    public static ScummRoomScene? Cached(GameLibrary game, int roomId) =>
        Scenes.GetOrAdd($"{game.FolderPath}|{roomId}|syd-bernard-v1", _ => new(() =>
        {
            try { return Run(game, roomId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException) { return null; }
        })).Value;

    public static ScummRoomScene Run(GameLibrary game, int roomId, int randomSeed = 0)
    {
        var scene = ScummRoomFirstVisit.Run(game, roomId, randomSeed, session => Prepare(game, session));
        return scene with { Notes = [Choice, .. scene.Notes] };
    }

    public static void Prepare(GameLibrary game, ScummGameSession session)
    {
        var menu = game.Rooms.FirstOrDefault(room => room.ResourceId == session.CurrentRoomId)
            ?? throw new InvalidDataException("The boot has no character-selection room to review.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(menu.FilePath)));
        if (!MenuFingerprints.Contains(fingerprint))
            throw new InvalidDataException("This character-selection menu has no reviewed first-visit input recipe.");
        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) })
        {
            session.Click(x, y);
            for (var frame = 0; frame < 100; frame++) session.AdvanceFrame();
        }
        for (var frame = 0; frame < 5000 && !session.PlayerHasControl; frame++) session.AdvanceFrame();
        if (!session.PlayerHasControl || session.InCutscene)
            throw new InvalidOperationException("The reviewed party inputs did not finish the opening within the frame limit.");
    }
}

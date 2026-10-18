using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Core.Maniac;

/// <summary>Reviewed native startup input for cutscene previews only (R-8.34); ordinary Play keeps its unanswered menu.</summary>
public static class ManiacCutsceneSetup
{
    public const string PreparationNote = "Preview start assumes the reviewed character-menu clicks selecting Syd, Bernard and Start, then runs the opening to player control. No route to the catalog room was played; an already selected party is preserved.";

    public static ScummGameSession NewSession(GameLibrary game)
    {
        var session = ScummGameSession.BootForPlay(game, out _);
        ManiacFirstVisit.Prepare(game, session);
        return session;
    }

    public static ScummCutscenePlanner Planner(GameLibrary game) =>
        new(game, () => NewSession(game), PreparationNote, ManiacPreviewText.For(game));

    /// <summary>Only the source-reviewed startup room can receive assumed inputs. Prepare validates its complete file fingerprint.</summary>
    public static bool PrepareIfNeeded(GameLibrary game, ScummGameSession session)
    {
        // Room45 is the reviewed character menu in both supported editions, as recorded in S4b/S6.
        // No engine-layer room exception or story-state mutation is involved.
        if (session.CurrentRoomId != 45) return false;
        if (session.DeliveredInput.Count != 0 || session.HasPendingPlayInput)
            throw new InvalidOperationException("Finish the character selection before launching a scene; the preview preserves the choices already entered.");
        ManiacFirstVisit.Prepare(game, session);
        return true;
    }
}

using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

/// <summary>Disclosed room-state previews and native pickup actions, followed by genuine inventory inputs.
/// This is not an earned route through the mansion and never writes story/owner/actor state.</summary>
public static class ManiacClassicUseWithRoute
{
    public static ScummGameSession Prepare(string folder, ScummScriptObserver? observer = null)
    {
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        ManiacFirstVisit.Prepare(game, session);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 2));
        session.Settle();
        session.ActivateObjectVerb(142, 14); // Native radio pickup also places batteries inside its container.
        session.ActivateObjectVerb(142, 1);  // Native Open transfers the batteries to the current actor.
        Frames(session, 120);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 7));
        Frames(session, 10);
        session.ActivateObjectVerb(84, 14);
        Frames(session, 10);
        return session;
    }

    public static void Frames(ScummGameSession session, int count)
    { for (var frame = 0; frame < count; frame++) session.AdvanceFrame(); }

    public static ScummPlayInput Click(ScummGameSession session, int x, int y)
    {
        var input = session.ClickScreen(x, y, out var refused) ?? throw new InvalidOperationException(refused);
        Frames(session, 5);
        return input;
    }

    public static ScummPlayInput Verb(ScummGameSession session, int verb)
    {
        var item = session.PlayVerbArea().Items.Single(item => item.VerbId == verb && item.Mode == 1);
        return Click(session, item.X + 4, item.Y + 3);
    }

    public static ScummPlayInput Inventory(ScummGameSession session, int objectId)
    {
        var ego = session.Snapshot.KnownGlobalVariables[ScummVars.V2.Ego];
        var inventory = session.CaptureInventoryStatus().Items.Where(item => item.OwnerId == ego).OrderBy(item => item.Slot).ToArray();
        var slot = Array.FindIndex(inventory, item => item.ObjectId == objectId);
        if (slot is < 0 or >= 4) throw new InvalidOperationException("Target is not in the four displayed inventory slots.");
        return Click(session, slot % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + slot / 2 * 8);
    }
}

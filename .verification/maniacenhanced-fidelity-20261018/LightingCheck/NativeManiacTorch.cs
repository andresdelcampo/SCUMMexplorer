using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

/// <summary>Retained diagnostic for disclosed direct source pickups followed by genuine native inventory Use input.
/// The 18 October route acquires both items but does not complete insertion; battery mode intentionally fails
/// with its native input/trace evidence. No game variable or object state is assigned by the harness.</summary>
public static class NativeManiacTorch
{
    public static IReadOnlyList<string> Activate(GameLibrary game, ScummGameSession session)
    {
        var log = new List<string>();
        void Frames(int count) { for (var f = 0; f < count; f++) session.AdvanceFrame(); }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + "\n" + string.Join("\n", log) + "\n" + string.Join("\n", session.SessionTrace.TakeLast(50)));
        }
        void Click(int x, int y)
        {
            var input = session.ClickScreen(x, y, out var refused);
            Check(input is not null, refused ?? "Click returned no input.");
            Frames(5);
            log.Add($"{input}; sentence: {ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}; battery={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x032]")}");
        }
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 2));
        session.Settle();
        // The batteries have no pickup verb: the radio's real pickup stores them
        // under native container owner 14; Open releases them to the current actor.
        session.ActivateObjectVerb(142, 14);
        session.ActivateObjectVerb(142, 1);
        Frames(120);
        log.Add($"Radio acquisition: owner142={session.Snapshot.ObjectOwners.GetValueOrDefault(142)}, owner167={session.Snapshot.ObjectOwners.GetValueOrDefault(167)}, room={session.CurrentRoomId}");
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 7));
        Frames(10);
        session.ActivateObjectVerb(84, 14);
        Frames(10);
        log.Add($"Torch acquisition: owner84={session.Snapshot.ObjectOwners.GetValueOrDefault(84)}, room={session.CurrentRoomId}, control={session.PlayerHasControl}");
        var ego = session.Snapshot.KnownGlobalVariables[ScummVars.V2.Ego];
        Check(session.Snapshot.ObjectOwners.GetValueOrDefault(167) == ego && session.Snapshot.ObjectOwners.GetValueOrDefault(84) == ego, "Source pickups did not place both items in current inventory.");
        var items = session.CaptureInventoryStatus().Items.Where(i => i.OwnerId == ego).OrderBy(i => i.Slot).ToArray();
        var battery = Array.FindIndex(items, i => i.ObjectId == 167);
        var torch = Array.FindIndex(items, i => i.ObjectId == 84);
        Check(battery is >= 0 and < 4 && torch is >= 0 and < 4, "Held targets are not in the four displayed native slots.");
        var use = session.PlayVerbArea().Items.Single(v => v.VerbId == 11 && v.Mode == 1);
        Click(use.X + Math.Min(4, use.Width / 2), use.Y + 3);
        Click(battery % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + battery / 2 * 8);
        Click(torch % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + torch / 2 * 8);
        Click(8, session.MainScreen.Bottom + 4);
        for (var f = 0; f < 1200 && session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x032]") != 2; f++) Frames(1);
        Check(session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x032]") == 2 && session.Snapshot.ObjectOwners.GetValueOrDefault(167) == 0, "Native Use did not install the batteries.");
        session.ActivateObjectVerb(84, 4);
        Frames(120);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 5));
        Frames(120);
        session.PointAtScreen((179, 79));
        var capture = session.CaptureRoom();
        Check((capture.Lights & 4) != 0 && (capture.Lights & 2) == 0, "Source battery/Switch on path did not illuminate the dark room.");
        log.Add($"PASS source battery 2 / owner167=0; torch owner84={session.Snapshot.ObjectOwners[84]}; lights={capture.Lights}; strips={capture.Flashlight}");
        return log;
    }
}

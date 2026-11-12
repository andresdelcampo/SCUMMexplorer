using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

// The provider of the room controls for every game played through ScummGameSession (R-16.9).
public partial class MainWindow
{
    /// <summary>Every other game that is played through <see cref="ScummGameSession"/> (verb-based, v1/v2 Maniac/Zak, v4/v5) and its static previews.</summary>
    private sealed class PlayRoomControls(MainWindow w) : IRoomControlProvider
    {
        public RoomTransportState ReadTransport()
        {
            var active = w.PlayActive;
            var inCutscene = active && w._ctx.Play!.InCutscene;
            var talking = active && w._ctx.Play!.Messages.HaveMsg != 0;
            // R-6.32 (LOOM's PlaybackHasActivity): Pause is for cutscenes and scenes. A cutscene, a shown line, the hero walking by a script's order
            // or the camera panning is activity; the permanent ambient animations of a room (a campfire) are not, and keep running without it.
            var activity = active && (inCutscene || talking || w._ctx.Play!.EgoMoving || w._ctx.Play.CameraMoving);
            return new(active && (w._ctx.PlayPaused || activity), w._ctx.PlayPaused, active && w._ctx.PlayPaused, inCutscene, inCutscene && w._ctx.Play!.CutsceneSkippable, talking);
        }

        public RoomControlState ReadState(int? room, bool advanced)
        {
            var active = w.PlayActive;
            var shown = w._ctx.CurrentRoomResource is not null;
            var canStart = w.PlayCanStartCutscene;
            var entries = room is int roomId ? w.PlayCutsceneItems(roomId, advanced).Select(choice => ToEntry(choice.Item)).ToList() : [];
            var computing = w._ctx.PlayPlanner is not null && !w._ctx.PlayRoomListsDone && w._ctx.PlaySavedRoomLists is null;
            var listing = computing && entries.Count == 0;
            // R-8.5b: the bar stays in a static preview too; what cannot act there is disabled, not hidden.
            return new(ReadTransport(), active || shown, canStart && w._ctx.PlayPlanner is not null, canStart, entries,
                shown && canStart && entries.Count > 0, listing, computing,
                shown && (computing || w.ScanRunning), shown && w.ScanRunning);
        }

        public bool RoomListsDone => w._ctx.PlayRoomListsDone;

        public RoomPanelData? Panel()
        {
            if (w._ctx.GenericActionsRoom is not int room) return null;
            ScummScriptDisassembly? Find(string label) => w._ctx.GenericRoomScripts.FirstOrDefault(item => item.Label == label).Code;
            var locals = w._ctx.GenericRoomScripts.Where(item => item.Label.StartsWith("Local script ", StringComparison.Ordinal))
                .Select(item => int.Parse(item.Label["Local script ".Length..], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(room, Find("Room entry"), Find("Room exit"), locals, w._ctx.GenericLastAction, w._ctx.GenericAssumptions);
        }

        public RoomIntroEntry GameIntro() =>
            new(w.NativeIntroOffered, !w._ctx.IntroPreparing && (!w.PlayActive || !w._ctx.Play!.InCutscene), "Play Game Intro",
                launch => w.StartGenericIntroAsync(w._ctx.Game!, launch));

        public RoomGameStatusEntry GameStatus() => new(w.InventoryStatusAvailable,
            "Inspect held items and make explicit inventory choices.", w.ShowInventoryStatus);

        public RoomCutscenePicker GlobalCutscenes(bool advanced) =>
            new(w.PlayCutsceneItems().Select(choice => ToEntry(choice.Item)).ToList(), "");

        public RoomInteractState ReadInteract()
        {
            if (!w.GenericRoomActions || w._ctx.CurrentRoomResource is null) return new(false, false, null);
            if (w._ctx.GenericInteractTarget is int selected && w.GenericObjectActions(selected).Count == 0) w._ctx.GenericInteractTarget = null;
            var name = w._ctx.GenericInteractTarget is int target ? w.GenericObjectActions(target).FirstOrDefault()?.Target : null;
            // R-6.39: the overview is upfront; no object has to be selected first.
            return new(true, w._ctx.GenericSourceActions.Any(action => action.Executable) && w.GenericInteractionCanStart, name,
                DoubleClickLooks: w._ctx.Game is { } game && ScummProfile.For(game).DoubleClickLookVerb is not null);
        }

        public RoomInteractionOverview? InteractionOverview(bool advanced)
        {
            if (!w.GenericRoomActions || w._ctx.CurrentRoomResource is null || !w.GenericInteractionCanStart) return null;
            // R-8.35/R-8.38 (user, 31 Oct 2026: explore everything): every object of the room's data with its verbs, whether it is in the room now or
            // only later; the objects on screen come first. A pick sets the state the verb's code tests (§8.6).
            var present = w._ctx.PlayScreen?.Scene() is { } scene ? ScummRoomOverlays.Objects(scene, w._ctx.PlayDoorwayTargets).Select(item => item.ObjectId).ToHashSet() : [];
            var objects = w._ctx.GenericSourceActions.Where(action => action.Executable).Select(action => action.Object).Distinct()
                .OrderBy(id => present.Contains(id) ? 0 : 1).ToArray();
            if (objects.Length == 0) return null;
            var headings = RoomInteractionRules.NumberRepeats(objects.Select(id => w.GenericObjectActions(id)[0].Target).ToArray());
            var groups = objects.Select((id, index) => new RoomInteractionGroup(headings[index], w._ctx.GenericInteractTarget == id,
                w.GenericObjectActions(id).Select(action => new RoomInteractionChoice(GenericActionWord(action), advanced ? action.Source : null,
                    () => w.RunGenericRoomAction(action))).ToList())).ToList();
            return new(groups);
        }

        public void TogglePause()
        {
            w._ctx.PlayPaused = !w._ctx.PlayPaused;
            w.ShowPlayFrame();
        }

        public void Step() => w.StepPlayFrame();

        public void Skip()
        {
            w.SkipPlayCutscene();
            w.PlayScreenImage.Focus();
        }

        public void SkipLine()
        {
            if (!w.PlayActive) return;
            w._ctx.Play!.SkipMessage();
            w.PlayScreenImage.Focus();
        }

        public RoomInteractionMenu? InteractionMenu(bool advanced)
        {
            if (w._ctx.GenericInteractTarget is not int objectId) return null;
            var actions = w.GenericObjectActions(objectId);
            if (actions.Count == 0 || !w.GenericInteractionCanStart) return null;
            var heading = (actions[0].Target is { Length: > 0 } name ? name : $"Object {objectId}").ToUpperInvariant();
            return new(heading, actions.Select(action => new RoomInteractionChoice(GenericActionWord(action), advanced ? action.Source : null,
                () => w.RunGenericRoomAction(action))).ToList());
        }

        public RoomSoundMenu? SoundMenu(bool advanced)
        {
            if (w._ctx.Game is not { } game || w._ctx.RoomSoundChoices.Count == 0 || w.SoundDeviceLocked) return null;
            var cd = ScummCdAudioTracks.For(game);
            int music = 0, effects = 0;
            var choices = new List<RoomSoundChoice>();
            foreach (var choice in w._ctx.RoomSoundChoices)
            {
                var picked = choice;
                var kind = cd?.Plays(choice.Resource.ResourceId!.Value) == true ? $"♪ Music {++music}" : $"Sound effect {++effects}";
                choices.Add(new(w.RoomSoundLabel(choice.Resource.Name, kind, choice.Evidence, choice.Duration),
                    advanced ? choice.Evidence : null, () => _ = w.AuditionGenericRoomSoundAsync(game, picked, cd)));
            }
            return new(choices, w._ctx.AudioSurface == AudioSurface.Room, w.StopAudio);
        }
    }
}

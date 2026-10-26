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
            var inCutscene = active && w._play!.InCutscene;
            var talking = active && w._play!.Messages.HaveMsg != 0;
            // R-6.32 (LOOM's PlaybackHasActivity): Pause is for cutscenes and scenes. A cutscene, a shown line, the hero walking by a script's order
            // or the camera panning is activity; the permanent ambient animations of a room (a campfire) are not, and keep running without it.
            var activity = active && (inCutscene || talking || w._play!.EgoMoving || w._play.CameraMoving);
            return new(active && (w._playPaused || activity), w._playPaused, active && w._playPaused, inCutscene, inCutscene && w._play!.CutsceneSkippable, talking);
        }

        public RoomControlState ReadState(int? room, bool advanced)
        {
            var active = w.PlayActive;
            var shown = w._currentRoomResource is not null;
            var canStart = w.PlayCanStartCutscene;
            var entries = room is int roomId ? w.PlayCutsceneItems(roomId).Select(choice => ToEntry(choice.Item)).ToList() : [];
            var computing = w._playPlanner is not null && !w._playRoomListsDone && w._playSavedRoomLists is null;
            var listing = computing && entries.Count == 0;
            // R-8.5b: the bar stays in a static preview too; what cannot act there is disabled, not hidden.
            return new(ReadTransport(), active || shown, canStart && w._playPlanner is not null, canStart, entries,
                shown && canStart && entries.Count > 0, listing, computing,
                shown && (computing || w.ScanRunning), shown && w.ScanRunning);
        }

        public bool RoomListsDone => w._playRoomListsDone;

        public RoomPanelData? Panel()
        {
            if (w._genericActionsRoom is not int room) return null;
            ScummScriptDisassembly? Find(string label) => w._genericRoomScripts.FirstOrDefault(item => item.Label == label).Code;
            var locals = w._genericRoomScripts.Where(item => item.Label.StartsWith("Local script ", StringComparison.Ordinal))
                .Select(item => int.Parse(item.Label["Local script ".Length..], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(room, Find("Room entry"), Find("Room exit"), locals, w._genericLastAction, w._genericAssumptions);
        }

        public RoomIntroEntry GameIntro() =>
            new(w.NativeIntroOffered, !w._introPreparing && (!w.PlayActive || !w._play!.InCutscene), "Play Game Intro",
                launch => w.StartGenericIntroAsync(w._game!, launch));

        public RoomGameStatusEntry GameStatus() => new(w.InventoryStatusAvailable,
            "Inspect held items and make explicit inventory choices.", w.ShowInventoryStatus);

        public RoomCutscenePicker GlobalCutscenes(bool advanced) =>
            new(w.PlayCutsceneItems().Select(choice => ToEntry(choice.Item)).ToList(), "");

        public RoomInteractState ReadInteract()
        {
            if (!w.GenericRoomActions || w._currentRoomResource is null) return new(false, false, null);
            if (w._genericInteractTarget is int selected && w.GenericObjectActions(selected).Count == 0) w._genericInteractTarget = null;
            var name = w._genericInteractTarget is int target ? w.GenericObjectActions(target).FirstOrDefault()?.Target : null;
            // R-6.39: the overview is upfront; no object has to be selected first.
            return new(true, w._genericSourceActions.Any(action => action.Executable) && w.GenericRoomActionCanStart, name,
                DoubleClickLooks: w._game is { } game && ScummProfile.For(game).DoubleClickLookVerb is not null);
        }

        public RoomInteractionOverview? InteractionOverview(bool advanced)
        {
            if (!w.GenericRoomActions || w._currentRoomResource is null || !w.GenericRoomActionCanStart) return null;
            // Only objects that are in the room now (the game cannot act on a removed one); the whole list when the scene cannot be read.
            var present = w._playScreen?.Scene() is { } scene ? ScummRoomOverlays.Objects(scene, w._playDoorwayTargets).Select(item => item.ObjectId).ToHashSet() : null;
            var objects = w._genericSourceActions.Where(action => action.Executable).Select(action => action.Object).Distinct()
                .Where(id => present is null || present.Contains(id)).ToArray();
            if (objects.Length == 0) return null;
            var headings = RoomInteractionRules.NumberRepeats(objects.Select(id => w.GenericObjectActions(id)[0].Target).ToArray());
            var groups = objects.Select((id, index) => new RoomInteractionGroup(headings[index], w._genericInteractTarget == id,
                w.GenericObjectActions(id).Select(action => new RoomInteractionChoice(GenericActionWord(action), advanced ? action.Source : null,
                    () => w.RunGenericRoomAction(action))).ToList())).ToList();
            return new(groups);
        }

        public void TogglePause()
        {
            w._playPaused = !w._playPaused;
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
            w._play!.SkipMessage();
            w.PlayScreenImage.Focus();
        }

        public RoomInteractionMenu? InteractionMenu(bool advanced)
        {
            if (w._genericInteractTarget is not int objectId) return null;
            var actions = w.GenericObjectActions(objectId);
            if (actions.Count == 0 || !w.GenericRoomActionCanStart) return null;
            var heading = (actions[0].Target is { Length: > 0 } name ? name : $"Object {objectId}").ToUpperInvariant();
            return new(heading, actions.Select(action => new RoomInteractionChoice(GenericActionWord(action), advanced ? action.Source : null,
                () => w.RunGenericRoomAction(action))).ToList());
        }

        public RoomSoundMenu? SoundMenu(bool advanced)
        {
            if (w._game is not { } game || w._roomSoundChoices.Count == 0 || w.SoundDeviceLocked) return null;
            var cd = ScummCdAudioTracks.For(game);
            int music = 0, effects = 0;
            var choices = new List<RoomSoundChoice>();
            foreach (var choice in w._roomSoundChoices)
            {
                var picked = choice;
                var kind = cd?.Plays(choice.Resource.ResourceId!.Value) == true ? $"♪ Music {++music}" : $"Sound effect {++effects}";
                choices.Add(new(w.RoomSoundLabel(choice.Resource.Name, kind, choice.Evidence, choice.Duration),
                    advanced ? choice.Evidence : null, () => _ = w.AuditionGenericRoomSoundAsync(game, picked, cd)));
            }
            return new(choices, w._audioSurface == AudioSurface.Room, w.StopAudio);
        }
    }
}

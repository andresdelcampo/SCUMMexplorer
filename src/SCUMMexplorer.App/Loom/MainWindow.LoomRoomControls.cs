using System.Windows;
using System.Windows.Controls;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

// The LOOM provider of the room controls (R-16.9): LOOM EGA and CD fill the shared controls with their data.
public partial class MainWindow
{
    /// <summary>LOOM EGA and CD (and its demos): the reference. Reads the exploration session.</summary>
    private sealed class LoomRoomControls(MainWindow w) : IRoomControlProvider
    {
        private bool Active => w.GameClickActive && w.HasDedicatedSession;

        // R-8.5b: a static preview keeps its bar; Play Cutscene starts from there (stated start).
        private bool StaticPreview => w.HasDedicatedSession && w._catalogPreviewOnly && w._session is not null && w.SessionModeToggle.IsChecked == true;

        public RoomTransportState ReadTransport()
        {
            var active = Active;
            return new(active && w._playback is not null && (w._playbackPaused || w.PlaybackHasActivity), w._playbackPaused,
                w._playback is not null && w._playbackPaused, active && w.SessionInScene, w._session?.CutsceneSkippable == true,
                active && !string.IsNullOrWhiteSpace(w._session?.Messages.Talk?.Text));
        }

        public RoomControlState ReadState(int? room, bool advanced)
        {
            var active = Active;
            var staticPreview = StaticPreview;
            var entries = room is int shown ? w.RoomCutsceneItems(shown).Select(ToEntry).ToList() : [];
            // Until the background runs have listed the scenes that play out in rooms, a first launch shows the button disabled (nothing is known yet).
            var pending = w.ScannedCutscenes && !w._cdRoomListsDone && w._cdSavedRoomLists is null;
            var listing = pending && entries.Count == 0;
            var canStart = w.CanStartCutscene;
            return new(ReadTransport(), active || staticPreview, active && canStart, canStart, entries,
                (active && canStart || staticPreview) && !listing, listing, pending,
                active && (pending || w.ScanRunning), active && w.ScanRunning);
        }

        public bool RoomListsDone => w._cdRoomListsDone;
        public RoomPanelData? Panel() => w.BuildLoomPanel();

        public RoomIntroEntry GameIntro()
        {
            var game = w._game;
            var demo = game is { Id: GameId.Loom, IsDemo: true };
            var offered = game is { Id: GameId.Loom or GameId.LoomVga };
            // R-6.38: offered whenever the game is loaded (user, 25 Oct 2026); it no longer comes and goes with the room shown.
            return new(offered, !w._introPreparing && (w.CanStartCutscene || w._catalogPreviewOnly),
                demo ? "Play Demo Preview" : "Play Game Intro", _ =>
                {
                    if (demo) w.PlayDemoPreview();
                    else if (game!.Id == GameId.Loom) w.PlayOpening();
                    else w.PlayCdIntro();
                    return Task.CompletedTask;
                });
        }

        public RoomGameStatusEntry GameStatus() => new(w._game is not null && w._session is not null,
            "Inspect or explicitly edit this exploration session's staff, notes and drafts.", () => w.BuildGameStatusWindow().ShowDialog());

        public RoomCutscenePicker GlobalCutscenes(bool advanced)
        {
            var entries = w.PlayableCutsceneItems().Select(choice => ToEntry(choice.Item)).ToList();
            foreach (var gap in w._replayCatalog?.Coverage ?? [])
                entries.Add(new(gap.Title, "Needs validation", gap.Source, null, gap.Status));
            return new(entries, w._replayCatalog?.Summary ?? "");
        }

        public RoomInteractState ReadInteract()
        {
            var target = w._interactTarget;
            if (target is not null && !w._currentRoomObjectRecords.Any(item => item.Id == target.Id)) { w._interactTarget = null; target = null; }
            var name = target is not null && w._roomObjectNames.TryGetValue(target.Id, out var stored) ? stored : null;
            // R-6.39: the overview is upfront, so the button works as soon as the room has an action and the game can act.
            return new(true, w._session is not null && !w.SessionInScene && InteractableObjects().Any(), name, DoubleClickLooks: true);
        }

        /// <summary>
        /// The room's objects that answer a verb: those the game's pointer can pick now first, then those that are in the room only at other points of
        /// the story (R-8.35/R-8.38, user 31 October 2026: explore everything; a pick brings such an object in), each group in directory order.
        /// </summary>
        private IEnumerable<ScummRoomObjectRecord> InteractableObjects() =>
            w._session is not { } session ? [] :
            w._currentRoomObjectRecords.Where(record => w.InteractVerbs(record).Count > 0 && (record.X, record.Y) != (0, 0))
                .OrderBy(record => w.IsVerbHotspotAvailable(record, session.Snapshot) ? 0 : 1).ThenBy(item => item.LocalIndex);

        public RoomInteractionOverview? InteractionOverview(bool advanced)
        {
            if (w.SessionInScene) return null;
            var records = InteractableObjects().ToArray();
            if (records.Length == 0) return null;
            var headings = RoomInteractionRules.NumberRepeats(records.Select(record =>
                w._roomObjectNames.TryGetValue(record.Id, out var name) ? name : $"Hotspot {record.LocalIndex}").ToArray());
            var groups = records.Select((record, index) => new RoomInteractionGroup(headings[index], w._interactTarget?.Id == record.Id,
                w.InteractVerbs(record).Select(verb => new RoomInteractionChoice(LoomVerbNames.Label(verb.VerbId), null, () =>
                {
                    if (!w.SessionInScene) w.ActivateObjectVerb(record, verb, play: true);
                })).ToList())).ToList();
            return new(groups);
        }

        public void TogglePause()
        {
            if (w._playbackPaused) w.ResumeScenePlayback();
            else w.PauseScenePlayback();
        }

        public void Step()
        {
            if (w._playbackPaused) { w.StepPausedPlayback(); return; }
            w.StopScenePlayback();
            w.AdvanceOneFrame();
        }

        public void Skip() => w.SkipCutscene();

        public void SkipLine() => w.SkipTalk("the Skip line button, same as '.'");

        public RoomInteractionMenu? InteractionMenu(bool advanced)
        {
            if (w._interactTarget is not { } record) return null;
            var heading = w._roomObjectNames.TryGetValue(record.Id, out var name) ? name.ToUpperInvariant() : $"OBJECT {record.Id}";
            return new(heading, w.InteractVerbs(record).Select(verb => new RoomInteractionChoice(LoomVerbNames.Label(verb.VerbId), null, () =>
            {
                if (!w.SessionInScene) w.ActivateObjectVerb(record, verb, play: true);
            })).ToList());
        }

        public RoomSoundMenu? SoundMenu(bool advanced)
        {
            if (w.SessionInScene) return null;
            if (w._game?.Id == GameId.LoomVga) return CdSounds();
            if (w._roomSoundChoices.Count == 0) return null;
            int music = 0, effects = 0;
            var choices = new List<RoomSoundChoice>();
            foreach (var choice in w._roomSoundChoices)
            {
                var picked = choice;
                // Normal: the kind of sound, numbered within its kind (the game gives sounds no titles); Advanced: the resource's own name.
                var kind = picked.Music ? $"♪ Music {++music}" : $"Sound effect {++effects}";
                choices.Add(new(w.RoomSoundLabel(picked.Resource.Name, kind, picked.Evidence, picked.Duration),
                    advanced ? picked.Evidence : null, () => _ = w.AuditionRoomSoundAsync(picked)));
            }
            return new(choices, w._audioSurface == AudioSurface.Room, w.StopAudio);
        }

        /// <summary>The CD game's menu: the disc audio this room's scripts ask for, played on demand (an exploration action).</summary>
        private RoomSoundMenu? CdSounds()
        {
            if (w._cdRoomCues.Count == 0 || w._game?.V4Archive?.Disc is not { } disc) return null;
            var choices = new List<RoomSoundChoice>();
            int music = 0, voices = 0;
            foreach (var cue in w._cdRoomCues)
            {
                var picked = cue;
                // R-6.24: the same rows as every game: the kind numbered within its kind (a spoken line is a Voice, the rest Music), the duration, and
                // in Advanced the source; the line the voice speaks is the tooltip (the game's own text, so Normal may show it).
                var kind = cue.IsMusicOrNarration ? $"♪ Music {++music}" : $"Voice {++voices}";
                var advanced = w.IsAdvancedMode;
                var label = w.RoomSoundLabel($"CD cue {cue.Cue.DiscSeconds:0.#}", kind, cue.Source, TimeSpan.FromSeconds(cue.Cue.Seconds));
                var line = cue.Text.Length > 0 ? $"\"{cue.Text}\"" : null;
                var detail = $"{cue.Source}\nDisc second {cue.Cue.DiscSeconds:0.#} · {cue.Cue.Seconds:0.#} s";
                var tip = advanced ? line is null ? detail : detail + "\n" + line : line;
                choices.Add(new(label, tip, () => w._cdVoice.PlayNow(disc, picked.Cue)));
            }
            return new(choices, w._cdVoice.Playing, w._cdVoice.Stop);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// R-16.9: the one implementation of the room controls (playback bar, Play Cutscene, Interact, Sounds). It reads
/// an <see cref="IRoomControlProvider"/> and has no game, family or session-type test of its own; the providers
/// (<see cref="LoomRoomControls"/>, <see cref="PlayRoomControls"/>) only fill it with their data.
/// </summary>
public partial class MainWindow
{
    private IRoomControlProvider? _loomRoomControlsInstance, _playRoomControlsInstance;

    // The one place that picks the provider for the open game (composition, not UI behaviour).
    private IRoomControlProvider CurrentRoomControls =>
        PlayCutscenes ? _playRoomControlsInstance ??= new PlayRoomControls(this)
        : IsLoomGame ? _loomRoomControlsInstance ??= new LoomRoomControls(this)
        : NoRoomControls.Instance;

    private void RefreshRoomControls()
    {
        var provider = CurrentRoomControls;
        var state = provider.ReadState(_currentRoomResource?.ResourceId, IsAdvancedMode);
        ApplyTransport(state.Transport);
        PlaybackBar.Visibility = state.BarVisible ? Visibility.Visible : Visibility.Collapsed;
        CutsceneMenuButton.IsEnabled = state.GlobalCutsceneEnabled;
        CutsceneMenuButton.Content = "Play Cutscene";
        CutsceneMenuButton.ToolTip = state.CanStartCutscene ? RoomControlText.GlobalCutsceneTip : RoomControlText.GlobalCutsceneBusyTip;
        var entries = state.RoomCutscenes;
        RoomCutsceneButton.Visibility = state.BarVisible && (entries.Count > 0 || state.CutsceneListing) ? Visibility.Visible : Visibility.Collapsed;
        RoomCutsceneButton.IsEnabled = state.RoomCutsceneEnabled;
        RoomCutsceneButton.Content = ButtonWithSpinner(RoomControlText.RoomCutsceneCaption(entries.Count, state.CutsceneListing), state.ScanComputing);
        RoomCutsceneButton.ToolTip = RoomControlText.RoomCutsceneTip(state.CutsceneListing, state.CanStartCutscene, entries);
        CutsceneScanNote.Visibility = state.ScanNoteVisible ? Visibility.Visible : Visibility.Collapsed;
        CutsceneScanProgress.Visibility = state.ScanProgressVisible ? Visibility.Visible : Visibility.Collapsed;
        ApplyInteract(provider.ReadInteract());
    }

    // Cheap, on every frame of a live game.
    private void RefreshRoomTransport() { ApplyTransport(CurrentRoomControls.ReadTransport()); ApplyInteract(CurrentRoomControls.ReadInteract()); }

    private void ApplyTransport(RoomTransportState transport)
    {
        PlaybackPauseButton.IsEnabled = transport.PauseEnabled;
        PlaybackPauseButton.Content = RoomControlText.PauseCaption(transport.Paused);
        PlaybackPauseButton.ToolTip = RoomControlText.PauseTip;
        AdvanceFrameButton.IsEnabled = transport.StepEnabled;
        SkipCutsceneButton.IsEnabled = transport.InCutscene && transport.CutsceneSkippable;
        SkipCutsceneButton.ToolTip = RoomControlText.SkipTip(transport.InCutscene, transport.CutsceneSkippable);
        SkipLineButton.IsEnabled = transport.SkipLineEnabled;
    }

    private void ApplyInteract(RoomInteractState interact)
    {
        InteractButton.Visibility = interact.Visible ? Visibility.Visible : Visibility.Collapsed;
        InteractButton.Content = RoomControlText.InteractCaption(interact.TargetName);
        InteractButton.IsEnabled = interact.Enabled;
        InteractButton.ToolTip = RoomControlText.InteractTooltip(interact.DoubleClickLooks);
    }

    private void ShowInteractMenu(FrameworkElement anchor, PlacementMode placement)
    {
        if (CurrentRoomControls.InteractionMenu(IsAdvancedMode) is not { Choices.Count: > 0 } menu) return;
        OpenPopupMenu(anchor, placement, menu.Heading, menu.Choices.Select(choice => (choice.Label, choice.Source, choice.Run)));
    }

    /// <summary>The Interact button (R-6.39): the room's interactions grouped by object, upfront; groups collapse when there are many.</summary>
    private void ShowInteractOverview(FrameworkElement anchor, PlacementMode placement)
    {
        if (CurrentRoomControls.InteractionOverview(IsAdvancedMode) is not { Groups.Count: > 0 } overview) return;
        OpenGroupedPopupMenu(anchor, placement, "INTERACTIONS IN THIS ROOM",
            overview.Groups.OrderByDescending(group => group.Selected).Select(group =>
                (group.Heading, group.Selected, group.Choices.Select(choice => (choice.Label, choice.Source, choice.Run)).ToList())).ToList(),
            RoomInteractionRules.GroupsStartOpen(overview.Actions));
    }

    /// <summary>Like <see cref="OpenPopupMenu"/>, with one collapsible group per object (a header with the action count, then its rows).</summary>
    private static void OpenGroupedPopupMenu(FrameworkElement target, PlacementMode placement, string heading,
        IReadOnlyList<(string Heading, bool Selected, List<(string Label, string? Tip, Action Run)> Rows)> groups, bool allOpen)
    {
        var gold = new SolidColorBrush(Color.FromRgb(183, 132, 66));
        var text = new SolidColorBrush(Color.FromRgb(231, 224, 210));
        var hover = new SolidColorBrush(Color.FromRgb(46, 92, 116));
        var popup = new Popup { PlacementTarget = target, Placement = placement, StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.None };
        var host = new StackPanel { MinWidth = 190 };
        host.Children.Add(new TextBlock { Text = heading, Foreground = gold, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 7, 10, 3) });
        var list = new StackPanel();
        foreach (var (name, selected, rows) in groups)
        {
            var open = allOpen || selected;
            var body = new StackPanel { Visibility = open ? Visibility.Visible : Visibility.Collapsed };
            var header = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4, 10, 4), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Foreground = gold
            };
            void ShowHeader() => header.Content = new TextBlock
            {
                Text = $"{(body.Visibility == Visibility.Visible ? "▾" : "▸")} {name.ToUpperInvariant()}" + (body.Visibility == Visibility.Visible ? "" : $" · {rows.Count}"),
                FontSize = 11, FontWeight = FontWeights.Bold
            };
            ShowHeader();
            header.MouseEnter += (_, _) => header.Background = hover;
            header.MouseLeave += (_, _) => header.Background = Brushes.Transparent;
            header.Click += (_, _) => { body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; ShowHeader(); };
            list.Children.Add(header);
            foreach (var (label, tip, run) in rows)
            {
                var item = new Button
                {
                    Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 },
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(22, 4, 10, 4),
                    Background = Brushes.Transparent, Foreground = text, BorderThickness = new Thickness(0), ToolTip = tip
                };
                item.MouseEnter += (_, _) => item.Background = hover;
                item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
                item.Click += (_, _) => { popup.IsOpen = false; run(); };
                body.Children.Add(item);
            }
            list.Children.Add(body);
        }
        host.Children.Add(new ScrollViewer { Content = list, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        popup.Child = new Border
        {
            Child = host, Background = new SolidColorBrush(Color.FromRgb(16, 24, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(74, 89, 98)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(0, 0, 0, 4)
        };
        popup.IsOpen = true;
    }

    private void ShowRoomSoundsMenu()
    {
        if (CurrentRoomControls.SoundMenu(IsAdvancedMode) is not { } menu) return;
        var entries = new List<(string Label, string? Tip, Action Run)>();
        if (menu.Playing) entries.Add(("■ Stop", null, menu.Stop));
        entries.AddRange(menu.Choices.Select(choice => (choice.Label, choice.Tip, choice.Play)));
        OpenPopupMenu(RoomSoundsButton, PlacementMode.Top, "SOUNDS IN THIS ROOM", entries);
    }

    private void ShowRoomCutsceneMenu()
    {
        var state = CurrentRoomControls.ReadState(_currentRoomResource?.ResourceId, IsAdvancedMode);
        if (!state.RoomCutsceneEnabled) return;
        var items = state.RoomCutscenes;
        if (items.Count == 1) items[0].Start?.Invoke();
        else if (items.Count > 1)
            OpenPopupMenu(RoomCutsceneButton, PlacementMode.Top, "CUTSCENES IN THIS ROOM",
                items.Where(item => item.Start is not null).Select(item => (item.Title, IsAdvancedMode ? item.Note : null, item.Start!)));
    }

    private static RoomCutsceneEntry ToEntry(CutscenePickerItem item) => new(item.Title, item.Category, item.Note, item.Play, item.Status);

    private sealed class NoRoomControls : IRoomControlProvider
    {
        public static NoRoomControls Instance { get; } = new();
        public RoomTransportState ReadTransport() => RoomTransportState.Idle;
        public RoomControlState ReadState(int? room, bool advanced) => RoomControlState.Hidden;
        public RoomInteractState ReadInteract() => new(false, false, null);
        public RoomCutscenePicker GlobalCutscenes(bool advanced) => new([], "");
        public bool RoomListsDone => true;
        public RoomGameStatusEntry GameStatus() => RoomGameStatusEntry.None;
        public RoomIntroEntry GameIntro() => RoomIntroEntry.None;
        public RoomPanelData? Panel() => null;
        public void TogglePause() { }
        public void Step() { }
        public void Skip() { }
        public void SkipLine() { }
        public RoomInteractionMenu? InteractionMenu(bool advanced) => null;
        public RoomInteractionOverview? InteractionOverview(bool advanced) => null;
        public RoomSoundMenu? SoundMenu(bool advanced) => null;
    }

    /// <summary>LOOM EGA and CD (and its demos): the reference. Reads the exploration session.</summary>
    private sealed class LoomRoomControls(MainWindow w) : IRoomControlProvider
    {
        private bool Active => w.GameClickActive && w.IsLoomGame;

        // R-8.5b: a static preview keeps its bar; Play Cutscene starts from there (stated start).
        private bool StaticPreview => w.IsLoomGame && w._catalogPreviewOnly && w._session is not null && w.SessionModeToggle.IsChecked == true;

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

        /// <summary>The room's objects the game's pointer can pick now and that answer a verb, in directory order.</summary>
        private IEnumerable<ScummRoomObjectRecord> InteractableObjects() =>
            w._session is not { } session ? [] :
            w._currentRoomObjectRecords.OrderBy(item => item.LocalIndex)
                .Where(record => w.IsVerbHotspotAvailable(record, session.Snapshot) && w.InteractVerbs(record).Count > 0);

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

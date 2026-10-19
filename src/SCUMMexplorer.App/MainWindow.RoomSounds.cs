using System.Globalization;
using System.IO;
using System.Windows;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

public partial class MainWindow
{
    private GameLibrary? _genericSoundGame;
    private int? _genericSoundRoom;
    private int _genericSoundEventCount = -1;
    private Dictionary<int, string> _genericSoundCandidates = [];
    private Task _genericRoomSoundsTask = Task.CompletedTask;

    /// <summary>Retire all output/event sources before scanning another folder, including an unsuccessful open.</summary>
    private void ResetGameAudio()
    {
        ++_cutsceneLaunchGeneration;
        ++_roomMusicUpdateGeneration;
        _previewCancellation?.Cancel();
        StopPlay();
        StopScenePlayback();
        StopAudio();
        _sceneSound.Detach();
        _cdVoice.Detach();
        _playSpeech.Detach();
        _session = null;
        _idleSession = null;
        _idleSignature = null;
        _roomMusicActionsRoom = null;
        _roomSoundChoices = [];
        _cdRoomCues = [];
        _genericSoundGame = null;
        _genericSoundRoom = null;
        _genericSoundEventCount = -1;
        _genericSoundCandidates = [];
        RoomSoundsButton.Visibility = RoomMusicPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>Uses the same native data source as Play for all non-LOOM room sound lists.</summary>
    private bool TryUpdateGenericRoomSounds()
    {
        if (_game is not { } game || game.Id is GameId.Loom or GameId.LoomVga) return false;
        if (_currentRoomResource?.ResourceId is not int roomId)
        {
            RoomSoundsButton.Visibility = Visibility.Collapsed;
            return true;
        }
        RoomMusicPanel.Visibility = Visibility.Collapsed;
        if (!ReferenceEquals(_genericSoundGame, game) || _genericSoundRoom != roomId)
        {
            _genericSoundGame = game;
            _genericSoundRoom = roomId;
            _genericSoundEventCount = -1;
            _genericSoundCandidates = [];
            _roomSoundChoices = [];
            RoomSoundsButton.Visibility = Visibility.Collapsed;
            var generation = ++_roomMusicUpdateGeneration;
            _genericRoomSoundsTask = LoadGenericRoomSoundsAsync(game, roomId, generation);
        }
        PublishGenericRoomSounds(game, roomId);
        return true;
    }

    private async Task LoadGenericRoomSoundsAsync(GameLibrary game, int roomId, int generation)
    {
        Dictionary<int, string> candidates;
        try { candidates = await Task.Run(() => ScanGenericRoomSounds(game, roomId)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        { candidates = []; }
        if (generation != _roomMusicUpdateGeneration || !ReferenceEquals(game, _game) || _currentRoomResource?.ResourceId != roomId) return;
        _genericSoundCandidates = candidates;
        _genericSoundEventCount = -1;
        PublishGenericRoomSounds(game, roomId);
    }

    private void PublishGenericRoomSounds(GameLibrary game, int roomId)
    {
        var session = _playFolder == game.Key && _play?.CurrentRoomId == roomId ? _play : null;
        var count = session?.SoundEvents.Count ?? 0;
        if (_genericSoundEventCount == count) return;
        _genericSoundEventCount = count;
        var candidates = new Dictionary<int, string>(_genericSoundCandidates);
        foreach (var item in session?.SoundEvents ?? [])
            if ((item.RoomId == roomId || item.OriginRoomId == roomId) &&
                item.Event.Kind is ScummScriptEventKind.SoundStarted or ScummScriptEventKind.MusicStarted && item.Event.TargetId is int id)
                candidates.TryAdd(id, item.Event.Format());
        var sounds = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
            .GroupBy(r => r.ResourceId!.Value).ToDictionary(g => g.Key, g => g.First());
        var cd = ScummCdAudioTracks.For(game);
        _roomSoundChoices = candidates.Where(c => sounds.ContainsKey(c.Key)).OrderBy(c => c.Key)
            .Select(c => (sounds[c.Key], cd?.Plays(c.Key) == true, (TimeSpan?)null, c.Value)).ToList();
        RoomSoundsButton.Visibility = _roomSoundChoices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSoundDeviceControls();
    }

    /// <summary>Static possible starts: room entry/exit/local/object code and literal calls; no invented runtime variables.</summary>
    private static Dictionary<int, string> ScanGenericRoomSounds(GameLibrary game, int roomId)
    {
        // Archive readers own seekable streams; background work must not share those of live Play.
        if (game.V4Archive is not null || game.V4Disks is not null) game = GameFolderScanner.Reopen(game);
        var data = ScummDataSources.For(game);
        var found = new Dictionary<int, List<string>>();
        var cache = new Dictionary<int, ScummScriptDisassembly?>();
        int? Literal(string value, IReadOnlyDictionary<int, int> locals)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number)) return number;
            if (value.StartsWith("local[0x", StringComparison.OrdinalIgnoreCase) && value.EndsWith(']') &&
                int.TryParse(value.AsSpan(8, value.Length - 9), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var slot) && locals.TryGetValue(slot, out var bound)) return bound;
            return null;
        }
        void Scan(ScummScriptDisassembly? script, IReadOnlyDictionary<int, int> locals, string path, HashSet<int> calls, int depth)
        {
            if (script is null) return;
            foreach (var instruction in script.Instructions)
            {
                var operands = instruction.Operands.Split(',', 2, StringSplitOptions.TrimEntries);
                int? target = instruction.Operation is "startSound" or "startMusic" ? Literal(operands[0], locals) : null;
                if (instruction.Operation == "soundKludge")
                {
                    var words = instruction.Operands.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.TrimEntries);
                    // Same source-defined iMUSE start command as the executor: scope 0, command 8.
                    if (words.Length >= 2 && Literal(words[0], locals) == 8) target = Literal(words[1], locals);
                }
                if (target is int sound)
                {
                    if (!found.TryGetValue(sound, out var evidence)) found[sound] = evidence = [];
                    evidence.Add($"{path} → {instruction.Operation} at +0x{instruction.Offset:X4}");
                }
                if (depth >= 4 || instruction.Operation is not ("startScript" or "chainScript") ||
                    Literal(operands[0], locals) is not int child || !calls.Add(child)) continue;
                if (!cache.TryGetValue(child, out var code))
                {
                    code = child < data.GlobalScriptCount ? data.GlobalScript(child) : data.RoomLocalScript(roomId, child);
                    cache[child] = code;
                }
                var args = new Dictionary<int, int>();
                if (operands.Length > 1 && operands[1].IndexOf('[') is >= 0 and var start && operands[1].LastIndexOf(']') is var end && end > start)
                {
                    var values = operands[1][(start + 1)..end].Split(',', StringSplitOptions.TrimEntries);
                    for (var index = 0; index < values.Length; index++) if (Literal(values[index], locals) is int value) args[index] = value;
                }
                Scan(code, args, $"{path} → Script {child:00}", calls, depth + 1);
                calls.Remove(child);
            }
        }
        void Root(Func<ScummScriptDisassembly?> read, string label)
        {
            try { Scan(read(), new Dictionary<int, int>(), label, [], 0); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        }
        Root(() => data.RoomEntryScript(roomId), $"Room {roomId:00} entry");
        Root(() => data.RoomExitScript(roomId), $"Room {roomId:00} exit");
        foreach (var local in data.RoomLocalScriptIds(roomId)) Root(() => data.RoomLocalScript(roomId, local), $"Room {roomId:00} local {local}");
        var records = data.ReadRoom(roomId).Objects;
        var verbs = data.ReadObjectVerbs(roomId, records);
        foreach (var record in records)
            foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
                Root(() => data.ReadObjectVerb(roomId, record, verb.VerbId, out _), $"Room {roomId:00} object {record.Id} verb {verb.VerbId}");
        return found.ToDictionary(c => c.Key, c => string.Join(Environment.NewLine, c.Value.Distinct()) +
            Environment.NewLine + "Possible static starts; literal child-script calls followed to depth 4. Variable targets and deeper calls remain unresolved.");
    }

    private bool TryShowGenericRoomSounds()
    {
        if (_game is not { } game || game.Id is GameId.Loom or GameId.LoomVga) return false;
        if (_roomSoundChoices.Count == 0 || SoundDeviceLocked) return true;
        var entries = new List<(string Label, string? Tip, Action Run)> { ("■ Stop", null, StopAudio) };
        var cd = ScummCdAudioTracks.For(game);
        var index = 0;
        foreach (var choice in _roomSoundChoices)
        {
            var picked = choice;
            var kind = cd?.Plays(choice.Resource.ResourceId!.Value) == true ? "♪ Music" : "Sound";
            var label = RoomSoundLabel(choice.Resource.Name, $"{kind} {++index}", choice.Evidence, choice.Duration);
            entries.Add((label, IsAdvancedMode ? choice.Evidence : null, () => _ = AuditionGenericRoomSoundAsync(game, picked, cd)));
        }
        OpenPopupMenu(RoomSoundsButton, System.Windows.Controls.Primitives.PlacementMode.Top, "SOUNDS IN THIS ROOM", entries);
        return true;
    }

    private async Task AuditionGenericRoomSoundAsync(GameLibrary game,
        (GameResource Resource, bool Music, TimeSpan? Duration, string Evidence) choice, ScummCdAudioTracks? cd)
    {
        if (!ReferenceEquals(_game, game) || SoundDeviceLocked) return;
        if (cd?.Plays(choice.Resource.ResourceId!.Value) != true)
        {
            await AuditionRoomSoundAsync(choice);
            return;
        }
        if (CdSoundCombo.SelectedIndex != 1) return;
        _sceneSound.StopAll("a sound was picked from the room's sound list");
        StopAudio();
        SelectRoomCue(choice.Resource, choice.Evidence, false, true);
        _audioSurface = AudioSurface.Room;
        _audioCancellation = new CancellationTokenSource();
        var token = _audioCancellation.Token;
        try
        {
            var wave = await Task.Run(() => cd.ReadWave(choice.Resource.ResourceId.Value), token);
            if (token.IsCancellationRequested || !ReferenceEquals(_game, game) || wave.Length <= 44) return;
            var file = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(file, wave, token);
            if (token.IsCancellationRequested || !ReferenceEquals(_game, game)) { File.Delete(file); return; }
            _audioFile = file;
            _playWhenReady = true;
            _audioKnownDuration = TimeSpan.FromSeconds((wave.Length - 44) / (double)BitConverter.ToInt32(wave, 28));
            _audioPlayer.Open(new Uri(file));
            _audioReady = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        { if (!token.IsCancellationRequested && IsAdvancedMode) StatusText.ToolTip = ex.Message; }
    }
}

using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>
    /// True while the boot is still running: only after a session built with <c>stopAtOpening</c>, until the frames
    /// played since reach the point where the constructor would have stopped (script 001 has ended or chained on and no
    /// cutscene is open).
    /// </summary>
    public bool BootRunning => _booting;

    protected bool BootGoesOn => (_pending.Any(item => item.ScriptNumber == BootScript) || InCutscene) && _pending.Count > 0;

    protected void FinishBoot(int frames)
    {
        _booting = false;
        BootFrames = frames;
        if (_pending.Any(item => item.ScriptNumber == BootScript))
            _uncertainties.Add($"The boot (script 001) was still running after {frames} frames; the preview stopped following it.");
        _visitTrace.Add($"boot: {frames} frames after script 001's first room load; now in Room {_currentRoom?.ResourceId:00}");
    }

    /// <summary>Frames the boot may run after script 001's first room load.</summary>
    public const int BootFrameLimit = 5000;

    /// <summary>
    /// The boot's skill screen (Room 69) waits for the player: script 001 waits on a bit that one local script of the room sets once an
    /// answer is given. This starts that script as the player's answer would and returns the note that says so (an estimate: the choice
    /// is not simulated), or null when the boot is not waiting for such an input.
    /// </summary>
    public string? AssumeBootAnswer()
    {
        if (FindAwaitedInput() is not { } input || _currentRoom is null) return null;
        _uncertainties.Add(input.Note);
        _visitTrace.Add($"boot: starts Room {_currentRoom.ResourceId:00} local script {input.LocalScript} as the player's answer would");
        _dispatchScope++;
        try
        {
            RunStartedScript(input.LocalScript, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
        return input.Note;
    }

    /// <summary>Frames the completed boot ran after script 001's first room load.</summary>
    public int BootFrames { get; protected set; }

    /// <summary>
    /// Script 001 waiting (<c>breakHere</c>, then a branch back while a bit is 0)
    /// for a bit that exactly one local script of the current room sets to 1.
    /// Returns that local script and a note naming the room code that starts it.
    /// </summary>
    protected (int LocalScript, string Note)? FindAwaitedInput()
    {
        if (_currentRoom is null || _pending.FirstOrDefault(item => item.ScriptNumber == BootScript) is not { } boot) return null;
        var wait = boot.Script.Instructions.FirstOrDefault(item => item.Offset == boot.Offset);
        if (wait is not { Operation: "notEqualZero" } || !wait.Operands.StartsWith("bit[", StringComparison.Ordinal)) return null;
        var bit = wait.Operands[..(wait.Operands.IndexOf(']') + 1)];
        var setters = new List<(int Local, int Offset)>();
        var starters = new List<string>();
        for (var local = _globalScriptCount; local < 256; local++)
        {
            ScummScriptDisassembly? script;
            try { script = _data.RoomLocalScript(_currentRoom.ResourceId!.Value, local); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
            if (script is null) continue;
            foreach (var item in script.Instructions)
            {
                if (item.Operation == "move" && item.Operands == $"{bit} = 0x0001") setters.Add((local, item.Offset));
                if (item.Operation == "startScript" && item.Operands.StartsWith("0x", StringComparison.Ordinal))
                    starters.Add($"{local}:{item.Operands[..4]}:{item.Offset:X4}");
            }
        }
        if (setters.Count != 1) return null;
        var (setter, setOffset) = setters[0];
        var startedBy = starters.Where(item => Convert.ToInt32(item.Split(':')[1], 16) == setter)
            .Select(item => $"local {item.Split(':')[0]} +0x{item.Split(':')[2]}").ToArray();
        if (startedBy.Length == 0) return null;
        return (setter, $"Player input assumed: script 001 +0x{wait.Offset:X4} waits for {bit}, which only Room {_currentRoom.ResourceId:00} " +
                        $"local script {setter} sets (+0x{setOffset:X4}). The room's own code starts it after the player's input " +
                        $"({string.Join(", ", startedBy)}), so the preview starts it as if the player had answered. Choices made on that screen were not simulated.");
    }

    /// <summary><c>VAR_SOUNDCARD</c>: 0 in a session by default, 3 for AdLib in ScummVM and 4 for the Roland upgrade, whose sounds (33 to 64) the scripts then choose.</summary>
    public const string SoundCardVariable = ScummVars.SoundCard;

    /// <summary>The sound card the game's scripts see. Changing it takes effect at the scripts' next read, as it would in the game.</summary>
    /// <summary>The shadow palette of the room on screen: slot i is shown as colour <c>ShadowPalette[i]</c> (identity unless a script faded something).</summary>
    public IReadOnlyList<byte> ShadowPalette => _runtime.ShadowPalette;

    public int SoundCard
    {
        get => _runtime.GlobalVariables.GetValueOrDefault(SoundCardVariable);
        set => _runtime.SetGlobalVariable(SoundCardVariable, (ushort)value);
    }

    /// <summary>ScummVM runs the game's boot script (indexed script 1) at start.</summary>
    public const int BootScript = 1;

    /// <summary>
    /// Runs the boot script from its start to its first pause, entering the room it loads. The session is booting (<see cref="BootRunning"/>)
    /// from then on; the caller continues the boot by frames or ends it with <see cref="FinishBoot"/>.
    /// </summary>
    protected void RunBootScript()
    {
        _booting = true;
        _dispatchScope++;
        try
        {
            RunIndexedScript(BootScript, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
    }
}


using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// <c>o5_soundKludge</c> (opcode 0x4C from version 4 on, S4b Monkey2, 17 October 2026): the script hands a word list to the sound engine
/// (<c>Sound::soundKludge</c>), which passes it to iMUSE's <c>doCommand</c>; the answer goes to <c>VAR_SOUNDRESULT</c>. ScummVM master read 17 October 2026
/// (<c>script_v5.cpp</c>, <c>sound.cpp</c>, <c>imuse/imuse.cpp</c> <c>doCommand_internal</c>). The first word holds the command (low byte) and its scope (high
/// byte: 0 the sound engine, 1 a playing song's player). A list whose first word is -1 only flushes the queue.
/// <list type="bullet">
/// <item>start (0x0008), stop (0x0009) and stop all (0x000A, 0x000B) are sound events like <c>startSound</c>/<c>stopSound</c> (<c>SoundKludgeEvent</c>, S4b Indy4);</item>
/// <item>the status (0x000D) is answered from the session's sound clock, as <c>isSoundRunning</c>;</item>
/// <item>a player command for a song that is not playing answers -1 (<c>findActivePlayer</c> finds none);</item>
/// <item>a player command for a playing song (volume, pan, transpose, parts, hooks, loops, jumps, fades, its parameters) and the trigger queue are not
/// followed by the session's song clock: the answer is 0 and is labelled an estimate. The song's length stays the one its own data gives.</item>
/// </list>
/// The engine writes <c>VAR_SOUNDRESULT</c> when it processes its queue (at the end of the frame, or at once on a -1 list); the session writes it at the
/// command, which is the same value whenever the script flushes before it reads (the games' pattern).
/// </summary>
public sealed partial class ScummBoundedScriptExecutor
{
    /// <summary>The words of a <c>soundKludge</c> list as they are now, or null when one is unknown.</summary>
    internal static int[]? SoundKludgeWords(string operands, IDictionary<string, ushort> variables)
    {
        var open = ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) return null;
        var items = operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var words = new int[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            if (!TryReadValue(items[index], variables, out var value, out _)) return null;
            words[index] = (short)value;
        }
        return words;
    }

    private static string? ApplySoundKludge(ScummScriptInstruction instruction, ScummScriptRuntimeState? runtime,
        Dictionary<string, ushort> knownVariables, HashSet<string> estimated, bool onEstimatedPath, HashSet<string> limitations, List<string> trace)
    {
        if (SoundKludgeWords(instruction.Operands, knownVariables) is not { Length: > 0 } words)
            return $"soundKludge words are unknown ({instruction.Operands})";
        if (words[0] == -1)
        {
            trace.Add("soundKludge -1: the sound queue is processed now");
            return null;
        }
        int Word(int index) => index < words.Length ? words[index] : 0;
        var command = words[0] & 0xFF;
        var scope = words[0] >> 8;
        (bool Running, string? Estimate) Status(int sound) =>
            sound == 0 ? (false, null)
            : runtime?.SoundRunning is { } status ? status(sound)
            : (runtime is not null && runtime.ActiveSounds.Contains(sound), "the session's sound clock is unavailable");
        int result;
        string? estimate = null;
        string meaning;
        if (scope == 0)
        {
            switch (command)
            {
                case 8: result = 0; meaning = $"start sound {Word(1)}"; break;
                case 9: result = 0; meaning = $"stop sound {Word(1)}"; break;
                case 10 or 11: result = 0; meaning = "stop every sound"; break;
                case 13:
                    var (running, statusEstimate) = Status(Word(1));
                    result = running ? 1 : 0;
                    estimate = statusEstimate;
                    meaning = $"status of sound {Word(1)}: {(running ? "playing" : "silent")}";
                    break;
                case 6: result = 0; meaning = $"music volume {Word(1)}"; break;
                case 2 or 3: result = 0; meaning = "no-op command"; break;
                case 14 or 15 or 16:
                    result = 0;
                    meaning = command == 14 ? $"trigger on sound {Word(1)} marker {Word(2)}" : command == 15 ? "queue a command for a marker" : "clear the marker queue";
                    estimate = "the song's markers are not followed, so queued commands never run";
                    break;
                default:
                    result = 0;
                    meaning = $"sound-engine command {command}";
                    estimate = "the command is not followed by the session";
                    break;
            }
        }
        else if (scope == 1)
        {
            var sound = Word(1);
            var (running, statusEstimate) = Status(sound);
            if (command == 24) { result = 0; meaning = "player command 24"; }
            else if (command == 23)
            {
                result = 0;
                meaning = $"query of the marker queue ({sound})";
                estimate = "the song's markers are not followed, so the queue is taken as empty";
            }
            else if (command is 14 or 15 or 16)
            {
                result = 0;
                meaning = command == 14 ? $"trigger on sound {sound} marker {Word(2)}" : command == 15 ? $"queue a command for sound {sound}" : "clear the marker queue";
                estimate = "the song's markers are not followed, so queued commands never run";
            }
            else if (!running)
            {
                result = -1;
                estimate = statusEstimate;
                meaning = $"player command {command} for sound {sound}, which is not playing";
            }
            else if (command == 0 && runtime?.SoundParameter?.Invoke(sound, Word(2)) is { } parameter)
            {
                // Player::getParam (param, channel): the session answers the song's position from its timeline (S7 Monkey2, 18 October 2026: Script 76 waits
                // for beat 9 of sound 176 before LeChuck walks in).
                result = parameter.Value;
                meaning = $"parameter {Word(2)} of playing sound {sound}";
                estimate = parameter.Basis;
            }
            else if (command is 12 or 20 && runtime?.SoundHook?.Invoke(sound, Word(2), Word(3)) is { } followed)
            {
                // Player::setHook (class, value, channel); the session's song clock follows a jump hook on the song's own timeline.
                result = 0;
                meaning = $"hook class {Word(2)} value {Word(3)} for playing sound {sound}: {followed}";
                estimate = "the song's length follows the armed jump hook on its AdLib timeline; markers and other player commands are not followed";
            }
            else
            {
                result = 0;
                meaning = $"player command {command} for playing sound {sound}";
                estimate = "the session's song clock does not follow player commands (volume, parts, hooks, loops, jumps, parameters)";
            }
        }
        else
        {
            result = -1;
            meaning = $"command scope {scope}";
            estimate = "the command scope is not known to the session";
        }
        knownVariables[ScummVars.SoundResult] = unchecked((ushort)result);
        MarkEstimated(estimated, ScummVars.SoundResult, onEstimatedPath || estimate is not null);
        if (estimate is not null)
            limitations.Add($"soundKludge {meaning}: VAR_SOUNDRESULT = {result} is an estimate ({estimate}) (+0x{instruction.Offset:X4})");
        trace.Add($"soundKludge {meaning}: VAR_SOUNDRESULT = {result}{(estimate is null ? "" : " (estimate)")}");
        return null;
    }
}
